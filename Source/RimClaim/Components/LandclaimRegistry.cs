using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public class LandclaimRegistry : MapComponent
    {
        // ── State ──────────────────────────────────────────────────────────────
        private List<LandclaimZone>      zones          = new();
        private List<TemporaryLandclaim> tempClaims     = new();
        private List<CombatSync>         activeSyncs    = new();
        private int                      nextSyncId     = 0;

        // Cached set of thingIDs inside any claim — rebuilt each tick boundary
        private HashSet<int> claimedThingIds = new();

        public bool gravshipEventActive = false;

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
                return;
            }

            zones.Add(new LandclaimZone
            {
                ownerPlayerIndex = ownerPlayerIndex,
                bounds           = bounds,
                active           = true,
                localTickRate    = 1,
            });
            RebuildClaimedThingIds();
        }

        public void UnregisterZone(int ownerPlayerIndex)
        {
            zones.RemoveAll(z => z.ownerPlayerIndex == ownerPlayerIndex);
            RebuildClaimedThingIds();
        }

        public void SetZoneActive(int ownerPlayerIndex, bool active)
        {
            var zone = GetZoneByOwner(ownerPlayerIndex);
            if (zone != null) zone.active = active;
        }

        public void SetLocalTickRate(int ownerPlayerIndex, int rate)
        {
            var zone = GetZoneByOwner(ownerPlayerIndex);
            if (zone == null) return;
            if (zone.IsLocked) return;
            zone.localTickRate = Mathf.Clamp(rate,
                0, Constants.MaxLocalTickRate);
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

        // ── MapComponentTick ───────────────────────────────────────────────────
        private int tickCounter = 0;

        public override void MapComponentTick()
        {
            tickCounter++;

            // NOTE: Per-claim tick multiplication is DISABLED pending a safe
            // async-time-based reimplementation. zone.ProcessTick is not called.
            // Speed hook buttons still set localTickRate (stored, displayed) but
            // do not yet multiply ticks. Zones tick at global rate for now.

            // Resolve combat syncs
            if (tickCounter % Constants.CombatSyncResolveInterval == 0)
                ResolveCombatSyncs();

            // Resolve escape syncs
            if (tickCounter % Constants.EscapeSyncResolveInterval == 0)
                ResolveEscapeSyncs();

            // Expire temp claims
            if (tickCounter % 120 == 0)
                ExpireTempClaims();
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
