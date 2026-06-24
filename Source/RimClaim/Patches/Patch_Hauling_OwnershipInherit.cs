using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim.Patches
{
    /// <summary>
    /// When an item comes to rest in a stockpile cell, it inherits that
    /// stockpile's ownership. We hook TryPlaceDirect on GenPlace, which fires
    /// whenever a thing is placed on the map (including haul completion),
    /// deterministically on all clients.
    /// </summary>
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn),
        new[] { typeof(Thing), typeof(IntVec3), typeof(Map),
                typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
    public static class Patch_GenSpawn_OwnershipInherit
    {
        public static void Postfix(Thing __result, IntVec3 loc, Map map)
        {
            if (!MP.IsInMultiplayer) return;
            if (!Constants.HaulingInheritsOwnership) return;
            if (__result == null || map == null) return;
            if (__result.def.category != ThingCategory.Item) return;

            var itemComp = __result.TryGetComp<OwnershipComp>();
            if (itemComp == null) return;

            // Is this cell inside a stockpile zone?
            var zone = map.zoneManager.ZoneAt(loc) as Zone_Stockpile;
            if (zone == null) return;

            var ownerData = ZoneOwnershipData.For(map);
            if (ownerData == null || ownerData.IsUnclaimed(zone)) return;

            int stockpileOwner   = ownerData.GetOwner(zone);
            bool stockpileShared = ownerData.IsShared(zone);

            if (itemComp.ownerPlayerIndex == stockpileOwner) return;

            itemComp.SetOwner(stockpileOwner, stockpileShared);
        }
    }
}
