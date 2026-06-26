using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public class LandclaimRegistry : MapComponent
    {
        // ── Reflection for calling protected Tick/TickRare/TickLong ────────────
        private static readonly System.Reflection.MethodInfo s_tick =
            AccessTools.Method(typeof(Thing), "Tick");
        private static readonly System.Reflection.MethodInfo s_tickRare =
            AccessTools.Method(typeof(Thing), "TickRare");
        private static readonly System.Reflection.MethodInfo s_tickLong =
            AccessTools.Method(typeof(Thing), "TickLong");
        private static readonly object[] s_noArgs = System.Array.Empty<object>();
        private static readonly bool s_canReflect =
            s_tick != null && s_tickRare != null && s_tickLong != null;

        static LandclaimRegistry()
        {
            if (!s_canReflect)
                Log.Warning("[RimClaim] Reflection lookup failed — tick multiplication disabled.");
        }

        // ── State ──────────────────────────────────────────────────────────────
        private List<LandclaimZone>      zones          = new();
        private List<TemporaryLandclaim> tempClaims     = new();
        private List<CombatSync>         activeSyncs    = new();
        private int                      nextSyncId     = 0;

        // Cached set of thingIDs inside any claim — rebuilt each tick boundary
        private HashSet<int> claimedThingIds = new();

        // Fast-path flag: true if any zone on any map has rate 0
        private static bool s_anyMapHasPaused = false;

        public static bool AnyPausedZones => s_anyMapHasPaused;

        public bool gravshipEventActive = false;

        public int ZoneVersion { get; private set; } = 0;

        public LandclaimRegistry(Map map) : base(map) { }

        // ── Static accessor ────────────────────────────────────────────────────
        public static LandclaimRegistry? For(Map map)
            => map?.GetComponent<LandclaimRegistry>();

        // ── Public read API ────────────────────────────────────────────────────
        public IReadOnlyList<LandclaimZone>      AllZones    => zones;
        public IReadOnlyList<TemporaryLandclaim> TempClaims  => tempClaims;

        public LandclaimZone? GetZoneAt(IntVec3 cell)
            => zones.FirstOrDefault(z => z.active && z.bounds.Contains(cell));

        public LandclaimZone? GetZoneByOwner(int playerIndex)
            => zones.FirstOrDefault(z => z.ownerPlayerIndex == playerIndex);

        public bool IsInAnyClaim(IntVec3 cell)
            => zones.Any(z => z.active && z.bounds.Contains(cell));

        public bool OverlapsExistingClaim(CellRect proposed, int requestingPlayer)
        {
            int buffer = RcMod.Settings?.BufferZoneWidth
                         ?? Constants.BufferZoneWidth;
            var expanded = proposed.ExpandedBy(buffer);
            return zones.Any(z =>
                z.ownerPlayerIndex != requestingPlayer &&
                z.bounds.Overlaps(expanded));
        }

        public bool CanPlayerBuildAt(IntVec3 cell, int playerIndex)
        {
            var zone = GetZoneAt(cell);
            if (zone == null) return true; // unclaimed — build allowed
            if (zone.ownerPlayerIndex == playerIndex) return true;
            var diplo = RcWorld.Diplomacy_Safe;
            return diplo == null || !diplo.AreEnemies(playerIndex, zone.ownerPlayerIndex);
        }

        public bool CanPlayerZoneAt(IntVec3 cell, int playerIndex)
        {
            var zone = GetZoneAt(cell);
            if (zone == null) return false; // no claim = no zone
            return zone.ownerPlayerIndex == playerIndex;
        }

        // ── Synced mutations ───────────────────────────────────────────────────

        public void RegisterZone(int ownerPlayerIndex, CellRect bounds)
        {
            var existing = GetZoneByOwner(ownerPlayerIndex);
            if (existing != null)
            {
                existing.bounds = bounds;
                existing.active = true;
                ZoneVersion++;
                return;
            }

            zones.Add(new LandclaimZone
            {
                ownerPlayerIndex = ownerPlayerIndex,
                bounds           = bounds,
                active           = true,
                localTickRate    = 1,
            });
            ZoneVersion++;
            RebuildClaimedThingIds();
        }

        public void UnregisterZone(int ownerPlayerIndex)
        {
            zones.RemoveAll(z => z.ownerPlayerIndex == ownerPlayerIndex);
            ZoneVersion++;
            RebuildClaimedThingIds();
        }

        public void SetZoneActive(int ownerPlayerIndex, bool active)
        {
            var zone = GetZoneByOwner(ownerPlayerIndex);
            if (zone == null || zone.active == active) return;
            zone.active = active;
            ZoneVersion++;
        }

        public void NotifyPauseChanged() => RefreshPausedFlag();

        public void SetLocalTickRate(int ownerPlayerIndex, int rate)
        {
            var zone = GetZoneByOwner(ownerPlayerIndex);
            if (zone == null) return;
            if (zone.IsLocked) return;
            int oldRate = zone.localTickRate;
            zone.localTickRate = Mathf.Clamp(rate,
                0, Constants.MaxLocalTickRate);
            if ((oldRate == 0) != (zone.localTickRate == 0))
                RefreshPausedFlag();
        }

        public void SetSpeedHookActive(int ownerPlayerIndex, bool active)
        {
            var zone = GetZoneByOwner(ownerPlayerIndex);
            if (zone != null) zone.speedHookActive = active;
        }

        public void BeginTradeLock(int ownerPlayerIndex, int traderThingId)
        {
            var zone = GetZoneByOwner(ownerPlayerIndex);
            if (zone == null || zone.tradeLockActive) return;
            zone.preTradTickRate = zone.localTickRate;
            zone.localTickRate   = 0;
            zone.tradeLockActive = true;
        }

        public void EndTradeLock(int ownerPlayerIndex)
        {
            var zone = GetZoneByOwner(ownerPlayerIndex);
            if (zone == null || !zone.tradeLockActive) return;
            zone.localTickRate   = zone.preTradTickRate;
            zone.tradeLockActive = false;
        }

        public void TriggerCombatSync(int attackerZoneOwner, int defenderZoneOwner,
            int attackerThingId, int defenderThingId)
        {
            var sync = new CombatSync
            {
                syncId          = nextSyncId++,
                attackerThingId = attackerThingId,
                defenderThingId = defenderThingId,
                startedAtTick   = map.MapTicks(),
                lastHitTick     = map.MapTicks(),
            };

            foreach (int owner in new[] { attackerZoneOwner, defenderZoneOwner })
            {
                if (owner < 0) continue;
                var zone = GetZoneByOwner(owner);
                if (zone == null || zone.combatSyncActive) continue;
                zone.preCombatTickRate  = zone.localTickRate;
                zone.localTickRate      = 1;
                zone.combatSyncActive   = true;
                zone.combatSyncId       = sync.syncId;
                sync.involvedZoneOwners.Add(owner);
            }

            activeSyncs.Add(sync);
        }

        public void TriggerEscapeSync(int holdingZoneOwner, int escapeeThingId)
        {
            var zone = GetZoneByOwner(holdingZoneOwner);
            if (zone == null || zone.escapeSyncActive) return;
            zone.preEscapeTickRate = zone.localTickRate;
            zone.localTickRate     = 1;
            zone.escapeSyncActive  = true;
            zone.escapeeThingId    = escapeeThingId;
        }

        public void TriggerGravshipEvent()
        {
            gravshipEventActive = true;
            foreach (var zone in zones)
            {
                zone.preGravshipEventRate    = zone.localTickRate;
                zone.localTickRate           = 1;
                zone.gravshipEventLockActive = true;
            }
        }

        public void ResolveGravshipEvent()
        {
            gravshipEventActive = false;
            foreach (var zone in zones)
            {
                zone.localTickRate           = zone.preGravshipEventRate;
                zone.gravshipEventLockActive = false;
            }
        }

        public void RegisterTempClaim(int ownerPlayerIndex, CellRect bounds,
            TemporaryClaimReason reason)
        {
            tempClaims.Add(new TemporaryLandclaim
            {
                ownerPlayerIndex = ownerPlayerIndex,
                bounds           = bounds,
                expiryTick       = map.MapTicks() + Constants.TempClaimDurationTicks,
                reason           = reason,
            });
        }

        // ── Pause snapshots ────────────────────────────────────────────────────
        private Dictionary<int, PawnSnapshot> pauseSnapshots = new();

        private struct PawnSnapshot
        {
            public Dictionary<string, float> needs;
            public long ageBio;
            public long ageChrono;
        }

        private void SnapshotPawn(Pawn p)
        {
            if (pauseSnapshots.ContainsKey(p.thingIDNumber)) return;
            var snap = new PawnSnapshot
            {
                needs = new Dictionary<string, float>(),
                ageBio = p.ageTracker.AgeBiologicalTicks,
                ageChrono = p.ageTracker.AgeChronologicalTicks,
            };
            if (p.needs?.AllNeeds != null)
            {
                foreach (var need in p.needs.AllNeeds)
                    snap.needs[need.def.defName] = need.CurLevel;
            }
            pauseSnapshots[p.thingIDNumber] = snap;
        }

        private void RestorePawn(Pawn p)
        {
            if (!pauseSnapshots.TryGetValue(p.thingIDNumber, out var snap)) return;
            if (p.needs?.AllNeeds != null)
            {
                foreach (var need in p.needs.AllNeeds)
                {
                    if (snap.needs.TryGetValue(need.def.defName, out float val))
                        need.CurLevel = val;
                }
            }
            p.ageTracker.AgeBiologicalTicks = snap.ageBio;
            p.ageTracker.AgeChronologicalTicks = snap.ageChrono;
        }

        // ── MapComponentTick ───────────────────────────────────────────────────
        private int tickCounter = 0;

        public override void MapComponentTick()
        {
            tickCounter++;

            // Freeze pawns in paused zones: snapshot on first tick, restore every tick
            for (int z = 0; z < zones.Count; z++)
            {
                var zone = zones[z];
                if (!zone.active || zone.localTickRate != 0) continue;
                foreach (var cell in zone.bounds)
                {
                    if (!cell.InBounds(map)) continue;
                    var things = map.thingGrid.ThingsListAtFast(cell);
                    for (int i = 0; i < things.Count; i++)
                    {
                        if (things[i] is Pawn p && p.Position == cell)
                        {
                            SnapshotPawn(p);
                            RestorePawn(p);
                        }
                    }
                }
            }

            // Tick multiplication: extra Tick() calls for zones running > 1x
            if (s_canReflect)
            {
                for (int z = 0; z < zones.Count; z++)
                {
                    var zone = zones[z];
                    if (!zone.active || zone.localTickRate <= 1) continue;

                    int extra = zone.localTickRate - 1;
                    TickZone(zone, extra, TickerType.Normal);

                    zone.rareDebt += extra;
                    while (zone.rareDebt >= 250)
                    {
                        zone.rareDebt -= 250;
                        TickZone(zone, 1, TickerType.Rare);
                    }

                    zone.longDebt += extra;
                    while (zone.longDebt >= 2000)
                    {
                        zone.longDebt -= 2000;
                        TickZone(zone, 1, TickerType.Long);
                    }
                }
            }

            // Clear snapshots for pawns no longer in paused zones
            if (tickCounter % 60 == 0 && pauseSnapshots.Count > 0)
            {
                var toRemove = new List<int>();
                foreach (var kvp in pauseSnapshots)
                {
                    var thing = map.listerThings.AllThings
                        .FirstOrDefault(t => t.thingIDNumber == kvp.Key);
                    if (thing == null || !thing.Spawned) { toRemove.Add(kvp.Key); continue; }
                    var pZone = GetZoneAt(thing.Position);
                    if (pZone == null || pZone.localTickRate != 0)
                        toRemove.Add(kvp.Key);
                }
                foreach (var id in toRemove) pauseSnapshots.Remove(id);
            }

            if (tickCounter % Constants.CombatSyncResolveInterval == 0)
                ResolveCombatSyncs();

            if (tickCounter % Constants.EscapeSyncResolveInterval == 0)
                ResolveEscapeSyncs();

            if (tickCounter % 120 == 0)
                ExpireTempClaims();
        }

        // ── Tick multiplication ───────────────────────────────────────────────
        private void TickZone(LandclaimZone zone, int count, TickerType type)
        {
            Patches.TickSuppressor.InExtraTick = true;
            try
            {
                var method = type switch
                {
                    TickerType.Rare => s_tickRare,
                    TickerType.Long => s_tickLong,
                    _              => s_tick,
                };

                foreach (var cell in zone.bounds)
                {
                    if (!cell.InBounds(map)) continue;
                    var things = map.thingGrid.ThingsListAtFast(cell);
                    for (int i = things.Count - 1; i >= 0; i--)
                    {
                        var thing = things[i];
                        if (thing.Destroyed || thing.Position != cell) continue;

                        bool match = type == TickerType.Normal
                            ? (thing.def.tickerType == TickerType.Normal || thing is Pawn)
                            : thing.def.tickerType == type;
                        if (!match) continue;

                        for (int t = 0; t < count; t++)
                        {
                            if (thing.Destroyed) break;
                            try { method.Invoke(thing, s_noArgs); }
                            catch (System.Exception e)
                            {
                                Log.ErrorOnce(
                                    $"[RC] Extra {type} tick on {thing}: {(e.InnerException ?? e).Message}",
                                    thing.thingIDNumber ^ 0x7C3A1);
                            }
                        }
                    }
                }
            }
            finally { Patches.TickSuppressor.InExtraTick = false; }
        }

        private void RefreshPausedFlag()
        {
            s_anyMapHasPaused = false;
            foreach (var m in Find.Maps)
            {
                var reg = For(m);
                if (reg == null) continue;
                foreach (var z in reg.zones)
                {
                    if (z.active && z.localTickRate == 0)
                    {
                        s_anyMapHasPaused = true;
                        return;
                    }
                }
            }
        }

        private void ResolveCombatSyncs()
        {
            for (int i = activeSyncs.Count - 1; i >= 0; i--)
            {
                var sync = activeSyncs[i];
                if (!sync.IsResolved(map)) continue;

                foreach (int owner in sync.involvedZoneOwners)
                {
                    var zone = GetZoneByOwner(owner);
                    if (zone == null || zone.combatSyncId != sync.syncId) continue;
                    zone.localTickRate    = zone.preCombatTickRate;
                    zone.combatSyncActive = false;
                    zone.combatSyncId     = -1;
                }
                activeSyncs.RemoveAt(i);
            }
        }

        private void ResolveEscapeSyncs()
        {
            foreach (var zone in zones.Where(z => z.escapeSyncActive))
            {
                var escapee = map.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(p => p.thingIDNumber == zone.escapeeThingId);

                bool resolved = escapee == null ||
                                escapee.Dead ||
                                !escapee.IsPrisoner ||
                                !zone.bounds.Contains(escapee.Position);

                if (!resolved) continue;
                zone.localTickRate    = zone.preEscapeTickRate;
                zone.escapeSyncActive = false;
                zone.escapeeThingId   = -1;
            }
        }

        private void ExpireTempClaims()
        {
            tempClaims.RemoveAll(tc =>
                tc.IsExpired(map) || tc.IsEmpty(map));
        }

        private void RebuildClaimedThingIds()
        {
            claimedThingIds.Clear();
            foreach (var zone in zones)
            {
                if (!zone.active) continue;
                foreach (var cell in zone.bounds.Cells)
                    foreach (var thing in map.thingGrid.ThingsAt(cell))
                        claimedThingIds.Add(thing.thingIDNumber);
            }
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref zones,       "zones",      LookMode.Deep);
            Scribe_Collections.Look(ref tempClaims,  "tempClaims", LookMode.Deep);
            Scribe_Collections.Look(ref activeSyncs, "activeSyncs",LookMode.Deep);
            Scribe_Values.Look(ref nextSyncId,        "nextSyncId", 0);
            Scribe_Values.Look(ref gravshipEventActive,"gravshipEvent", false);
            zones       ??= new List<LandclaimZone>();
            tempClaims  ??= new List<TemporaryLandclaim>();
            activeSyncs ??= new List<CombatSync>();
            RebuildClaimedThingIds();
        }
    }
}
