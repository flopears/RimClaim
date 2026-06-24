using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim.Patches
{
    /// <summary>
    /// Prevents pawns from opening doors they don't have permission to use.
    ///
    /// Harmony target: Building_Door.PawnCanOpen(Pawn)
    ///
    /// Logic:
    ///   - Unclaimed doors: anyone can open (vanilla behaviour)
    ///   - Owned, not shared: only owner's pawns
    ///   - Owned, team-shared: owner + teammates with shareDoors permission
    ///   - Enemy pawns: always locked regardless of ownership
    /// </summary>
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    public static class Patch_Door_Ownership
    {
        public static bool Prefix(Building_Door __instance, Pawn p, ref bool __result)
        {
            // Only active in multiplayer
            if (!MP.IsInMultiplayer) return true;

            var comp = __instance.TryGetComp<OwnershipComp>();
            if (comp == null || comp.IsUnclaimed) return true; // vanilla handles it

            // Enemy faction: always locked
            var diplo    = RcWorld.Diplomacy_Safe;
            var registry = RcWorld.Players_Safe;
            if (diplo != null && registry != null)
            {
                int doorOwner = comp.ownerPlayerIndex;
                int pawnOwner = GetPawnPlayerIndex(p, registry);

                if (pawnOwner >= 0 && pawnOwner != doorOwner)
                {
                    if (diplo.AreEnemies(doorOwner, pawnOwner))
                    {
                        __result = false;
                        return false;
                    }
                }
            }

            // Delegate to OwnershipComp.LocalPlayerCanAccess
            // We can't use LocalPlayerIndex here because the pawn might belong to
            // a different player on the same simulation. We check pawn faction instead.
            if (!CanPawnAccessDoor(__instance, p, comp))
            {
                __result = false;
                return false;
            }

            return true; // let vanilla run its own additional checks
        }

        private static bool CanPawnAccessDoor(Building_Door door, Pawn pawn, OwnershipComp comp)
        {
            var registry = RcWorld.Players_Safe;
            if (registry == null) return true;

            int pawnOwner = GetPawnPlayerIndex(pawn, registry);
            if (pawnOwner < 0) return true;             // NPC / wildlife: always allow
            if (pawnOwner == comp.ownerPlayerIndex) return true; // owner always allowed

            if (!comp.teamShared) return false;

            var teams = RcWorld.Teams_Safe;
            if (teams == null) return false;

            return teams.GetPermissions(pawnOwner, comp.ownerPlayerIndex).shareDoors;
        }

        /// <summary>
        /// Finds the playerIndex whose faction matches the pawn's faction.
        /// Returns -1 if the pawn is not a player-controlled colonist.
        /// </summary>
        private static int GetPawnPlayerIndex(Pawn pawn, PlayerRegistry registry)
        {
            if (pawn?.Faction == null) return -1;
            foreach (var player in registry.AllPlayers)
            {
                var faction = player.GetFaction();
                if (faction != null && faction == pawn.Faction)
                    return player.playerIndex;
            }
            return -1;
        }
    }
}
