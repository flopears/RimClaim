using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimClaim
{
    public enum TemporaryClaimReason { TradeDropoff, CaravanArrival, GravshipLanding, QuestReward }

    public class LandclaimZone : IExposable
    {
        // ── Identity ───────────────────────────────────────────────────────────
        public int       ownerPlayerIndex = -1;
        public CellRect  bounds;
        public bool      active           = true; // false when power lost

        // ── Tick rate ──────────────────────────────────────────────────────────
        public int       localTickRate    = 1;    // 0=paused, 1=normal, 2=2x, 3=3x
        public float     rareDebt         = 0f;
        public float     longDebt         = 0f;

        // ── Sync locks ─────────────────────────────────────────────────────────
        public bool      tradeLockActive       = false;
        public int       preTradTickRate        = 1;

        public bool      combatSyncActive      = false;
        public int       preCombatTickRate      = 1;
        public int       combatSyncId          = -1;

        public bool      escapeSyncActive      = false;
        public int       preEscapeTickRate      = 1;
        public int       escapeeThingId        = -1;

        public bool      gravshipEventLockActive = false;
        public int       preGravshipEventRate    = 1;

        public bool      globalPauseLockActive   = false;
        public int       preGlobalPauseRate      = 1;

        public bool      mapEventLockActive     = false;

        // ── Speed hook ─────────────────────────────────────────────────────────
        public bool      speedHookActive        = false;

        // ── Convenience ────────────────────────────────────────────────────────
        public bool IsLocked => tradeLockActive || combatSyncActive ||
                                escapeSyncActive || gravshipEventLockActive ||
                                globalPauseLockActive;

        public Color GetBorderColor(int viewerPlayerIndex)
        {
            var registry = RcWorld.Players_Safe;
            var owner    = registry?.GetPlayer(ownerPlayerIndex);
            if (owner == null) return Color.gray;

            if (ownerPlayerIndex == viewerPlayerIndex)
                return owner.playerColor;

            var teams  = RcWorld.Teams_Safe;
            var diplo  = RcWorld.Diplomacy_Safe;

            if (diplo?.AreEnemies(viewerPlayerIndex, ownerPlayerIndex) == true)
                return Color.red;

            if (teams?.AreTeammates(viewerPlayerIndex, ownerPlayerIndex) == true)
                return Color.Lerp(owner.playerColor, Color.white, 0.5f);

            return Color.gray;
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public void ExposeData()
        {
            Scribe_Values.Look(ref ownerPlayerIndex,        "ownerPlayerIndex",        -1);
            Scribe_Values.Look(ref bounds,                  "bounds");
            Scribe_Values.Look(ref active,                  "active",                  true);
            Scribe_Values.Look(ref localTickRate,           "localTickRate",           1);
            Scribe_Values.Look(ref rareDebt,                "rareDebt",                0f);
            Scribe_Values.Look(ref longDebt,                "longDebt",                0f);
            Scribe_Values.Look(ref tradeLockActive,         "tradeLockActive",         false);
            Scribe_Values.Look(ref preTradTickRate,         "preTradTickRate",         1);
            Scribe_Values.Look(ref combatSyncActive,        "combatSyncActive",        false);
            Scribe_Values.Look(ref preCombatTickRate,       "preCombatTickRate",       1);
            Scribe_Values.Look(ref combatSyncId,            "combatSyncId",            -1);
            Scribe_Values.Look(ref escapeSyncActive,        "escapeSyncActive",        false);
            Scribe_Values.Look(ref preEscapeTickRate,       "preEscapeTickRate",       1);
            Scribe_Values.Look(ref escapeeThingId,          "escapeeThingId",          -1);
            Scribe_Values.Look(ref gravshipEventLockActive, "gravshipEventLockActive", false);
            Scribe_Values.Look(ref preGravshipEventRate,    "preGravshipEventRate",    1);
            Scribe_Values.Look(ref globalPauseLockActive,   "globalPauseLockActive",   false);
            Scribe_Values.Look(ref preGlobalPauseRate,      "preGlobalPauseRate",      1);
            Scribe_Values.Look(ref speedHookActive,         "speedHookActive",         false);
        }
    }

    public class TemporaryLandclaim : IExposable
    {
        public int                  ownerPlayerIndex;
        public CellRect             bounds;
        public int                  expiryTick;
        public TemporaryClaimReason reason;

        public bool IsExpired(Map map) => map.MapTicks() >= expiryTick;

        public bool IsEmpty(Map map)
        {
            foreach (var cell in bounds.Cells)
                foreach (var thing in map.thingGrid.ThingsAt(cell))
                {
                    var comp = thing.TryGetComp<OwnershipComp>();
                    if (comp?.ownerPlayerIndex == ownerPlayerIndex) return false;
                }
            return true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ownerPlayerIndex, "ownerPlayerIndex", -1);
            Scribe_Values.Look(ref bounds,           "bounds");
            Scribe_Values.Look(ref expiryTick,       "expiryTick",       0);
            Scribe_Values.Look(ref reason,           "reason",           TemporaryClaimReason.TradeDropoff);
        }
    }

    public class CombatSync : IExposable
    {
        public int   syncId;
        public int   attackerThingId;
        public int   defenderThingId;
        public int   startedAtTick;
        public int   lastHitTick;
        public List<int> involvedZoneOwners = new();

        public bool IsResolved(Map map)
        {
            var attacker = map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(p => p.thingIDNumber == attackerThingId);
            if (attacker == null || attacker.Dead || attacker.Downed) return true;
            if (attacker.CurJob?.def != JobDefOf.AttackMelee &&
                attacker.CurJob?.def != JobDefOf.AttackStatic) return true;

            var defender = map.listerThings.AllThings
                .FirstOrDefault(t => t.thingIDNumber == defenderThingId);
            if (defender == null || defender.Destroyed) return true;

            if (map.MapTicks() - lastHitTick > Constants.CombatSyncInactivityTimeout)
                return true;

            return false;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref syncId,           "syncId",           -1);
            Scribe_Values.Look(ref attackerThingId,  "attackerThingId",  -1);
            Scribe_Values.Look(ref defenderThingId,  "defenderThingId",  -1);
            Scribe_Values.Look(ref startedAtTick,    "startedAtTick",    0);
            Scribe_Values.Look(ref lastHitTick,      "lastHitTick",      0);
            Scribe_Collections.Look(ref involvedZoneOwners, "involvedZoneOwners", LookMode.Value);
            involvedZoneOwners ??= new List<int>();
        }
    }
}
