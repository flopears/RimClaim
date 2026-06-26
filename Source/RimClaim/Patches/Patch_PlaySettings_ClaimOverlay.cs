using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim.Patches
{
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings_ClaimOverlay
    {
        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView) return;
            if (!MP.IsInMultiplayer) return;

            row.ToggleableIcon(ref ClaimOverlay.showOverlay,
                TexButton.RC_ClaimOverlay,
                "RC_ShowClaims".Translate());
        }
    }
}
