using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim.Patches
{
    [HarmonyPatch(typeof(Designator_ZoneAdd), "CanDesignateCell")]
    public static class Patch_ZoneDesignator_ClaimCheck
    {
        public static void Postfix(IntVec3 c, ref AcceptanceReport __result)
        {
            if (!MP.IsInMultiplayer || !__result.Accepted) return;

            var map = Find.CurrentMap;
            if (map == null) return;

            var registry = LandclaimRegistry.For(map);
            if (registry == null) return;

            if (!registry.CanPlayerZoneAt(c, RcLocal.PlayerIndex))
                __result = "RC_ZoneRequiresClaim".Translate();
        }
    }

    [HarmonyPatch(typeof(Designator_Build), "CanDesignateCell")]
    public static class Patch_BuildDesignator_ClaimCheck
    {
        public static void Postfix(IntVec3 c, ref AcceptanceReport __result)
        {
            if (!MP.IsInMultiplayer || !__result.Accepted) return;

            var map = Find.CurrentMap;
            if (map == null) return;

            var registry = LandclaimRegistry.For(map);
            if (registry == null) return;

            if (!registry.CanPlayerBuildAt(c, RcLocal.PlayerIndex))
                __result = "RC_CantBuildEnemyTerritory".Translate();
        }
    }
}
