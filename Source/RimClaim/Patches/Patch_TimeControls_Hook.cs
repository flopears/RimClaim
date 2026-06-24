using UnityEngine;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim.Patches
{
    [HarmonyPatch(typeof(TimeControls), nameof(TimeControls.DoTimeControlsGUI))]
    public static class Patch_TimeControls_Hook
    {
        private static TimeSpeed lastSpeed = TimeSpeed.Normal;

        public static void Prefix()
        {
            if (!MP.IsInMultiplayer || !SpeedHookManager.IsHooked) return;

            var localPlayer = RcWorld.Players_Safe?.GetLocalPlayer();
            if (localPlayer == null) return;

            // Tint buttons in player's color when hooked
            UnityEngine.GUI.color = UnityEngine.Color.Lerp(
                UnityEngine.Color.white,
                localPlayer.playerColor, 0.35f);

            lastSpeed = Find.TickManager.CurTimeSpeed;
        }

        public static void Postfix()
        {
            UnityEngine.GUI.color = UnityEngine.Color.white;

            if (!MP.IsInMultiplayer || !SpeedHookManager.IsHooked) return;

            // If vanilla changed the speed, intercept and revert
            var currentSpeed = Find.TickManager.CurTimeSpeed;
            if (currentSpeed == lastSpeed) return;

            // Revert global speed
            Find.TickManager.CurTimeSpeed = lastSpeed;

            // Apply to claim instead
            SpeedHookManager.TryHandleSpeedInput((int)currentSpeed);
        }
    }

    [HarmonyPatch(typeof(TimeControls), "DoTimeControlsGUI")]
    public static class Patch_SpaceBar_ClaimPause
    {
        public static bool Prefix()
        {
            if (!MP.IsInMultiplayer || !SpeedHookManager.IsHooked) return true;
            if (!KeyBindingDefOf.TogglePause.IsDownEvent) return true;

            return !SpeedHookManager.TryHandlePause();
        }
    }
}
