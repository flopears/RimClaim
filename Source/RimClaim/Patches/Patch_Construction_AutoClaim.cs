using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaim.Patches
{
    /// <summary>
    /// Auto-claims a building for the player whose pawn finished constructing it.
    ///
    /// Harmony target: Frame.CompleteConstruction(Pawn)
    ///   — called when a pawn's construction job completes and the frame becomes a real building.
    ///
    /// The new Thing is spawned inside CompleteConstruction. We hook Postfix to find
    /// the freshly-placed building and set its owner.
    ///
    /// This IS a state mutation, so we route through OwnershipComp.SetOwner ([SyncMethod]).
    /// The Postfix fires on all clients identically (deterministic tick), and the
    /// SetOwner call inside it is itself a SyncMethod — but because CompleteConstruction
    /// is already executing in tick context (inside a JobDriver toil), the call is safe.
    /// </summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class Patch_Construction_AutoClaim
    {
        public static void Postfix(Frame __instance, Pawn worker)
        {
            if (!MP.IsInMultiplayer) return;
            if (worker == null) return;

            // Find which player owns this worker
            var registry = RcWorld.Players_Safe;
            if (registry == null) return;

            int playerIndex = -1;
            foreach (var player in registry.AllPlayers)
            {
                var faction = player.GetFaction();
                if (faction != null && faction == worker.Faction)
                {
                    playerIndex = player.playerIndex;
                    break;
                }
            }
            if (playerIndex < 0) return;

            // The Frame has been replaced by the real building at this point.
            // The building spawns at the frame's position — find it.
            var map      = worker.Map;
            var pos      = __instance.Position; // frame position is valid even post-completion
            var newThing = map?.thingGrid.ThingAt(pos, ThingCategory.Building);

            if (newThing == null) return;

            var comp = newThing.TryGetComp<OwnershipComp>();
            if (comp == null) return;

            // Claim for the constructing player, unshared by default
            comp.SetOwner(playerIndex, shared: false);
        }
    }

    /// <summary>
    /// Auto-claim zones when a player draws them.
    ///
    /// Harmony target: Zone.AddCell(IntVec3)
    ///   — called when a zone is first created and expanded.
    ///
    /// We detect zone creation (when the zone has exactly 1 cell after AddCell)
    /// and claim it for the local player.
    ///
    /// This fires client-locally during the drag gesture (UI layer), but the
    /// actual zone creation is a synced command from Zetrith's MP. We set ownership
    /// after the fact via ZoneOwnershipData.SetZoneOwner ([SyncMethod]).
    /// </summary>
    [HarmonyPatch(typeof(Zone), nameof(Zone.AddCell))]
    public static class Patch_Zone_AutoClaim
    {
        public static void Postfix(Zone __instance)
        {
            if (!MP.IsInMultiplayer) return;

            // Only claim at zone birth (first cell)
            if (__instance.cells.Count != 1) return;

            var ownerData = ZoneOwnershipData.For(__instance.Map);
            if (ownerData == null) return;

            // Already claimed
            if (!ownerData.IsUnclaimed(__instance)) return;

            ownerData.SetZoneOwner(__instance.ID, RcLocal.PlayerIndex, shared: false);
        }
    }

    /// <summary>
    /// Cleans up zone ownership data when a zone is deleted.
    ///
    /// Harmony target: Zone.Delete()
    /// </summary>
    [HarmonyPatch(typeof(Zone), nameof(Zone.Delete), new System.Type[] { })]
    public static class Patch_Zone_Cleanup
    {
        public static void Prefix(Zone __instance)
        {
            if (!MP.IsInMultiplayer) return;
            var ownerData = ZoneOwnershipData.For(__instance.Map);
            ownerData?.OnZoneDeleted(__instance);
        }
    }
}
