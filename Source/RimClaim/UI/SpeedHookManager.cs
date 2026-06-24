using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// Client-local manager. Intercepts vanilla speed button input
    /// and redirects it to the active landclaim when hooked.
    /// NEVER synced — purely local input remapping.
    /// </summary>
    public static class SpeedHookManager
    {
        private static Building_LandclaimBlock? hookedClaim = null;

        public static bool IsHooked  => hookedClaim != null;
        public static int? HookedRate =>
            hookedClaim == null ? null :
            LandclaimRegistry.For(hookedClaim.Map)?
                .GetZoneByOwner(RcLocal.PlayerIndex)?.localTickRate;

        public static void SetHook(Building_LandclaimBlock? claim)
        {
            hookedClaim = claim;
        }

        /// <summary>
        /// Called from Patch_TimeControls_Hook when player presses a speed button.
        /// Returns true if we consumed the input.
        /// </summary>
        public static bool TryHandleSpeedInput(int requestedSpeed)
        {
            if (hookedClaim == null) return false;

            var registry = LandclaimRegistry.For(hookedClaim.Map);
            if (registry == null) return false;

            // Blocked during gravship events
            if (registry.gravshipEventActive)
            {
                Messages.Message("RC_SpeedLockedGravshipEvent".Translate(),
                    MessageTypeDefOf.RejectInput, false);
                return true; // consumed but not applied
            }

            // Clamp to valid range (Space = 0 for pause)
            int clamped = Mathf.Clamp(requestedSpeed, 0, Constants.MaxLocalTickRate);
            registry.SetLocalTickRate(RcLocal.PlayerIndex, clamped);
            return true;
        }

        /// <summary>
        /// Handle Space bar — pause/unpause the claim only.
        /// </summary>
        public static bool TryHandlePause()
        {
            if (hookedClaim == null) return false;

            var registry = LandclaimRegistry.For(hookedClaim.Map);
            if (registry == null) return false;

            if (registry.gravshipEventActive) return true; // blocked, consume

            var zone = registry.GetZoneByOwner(RcLocal.PlayerIndex);
            if (zone == null) return false;

            int newRate = zone.localTickRate == 0 ? zone.preTradTickRate : 0;
            if (newRate == 0) zone.preTradTickRate = zone.localTickRate;
            registry.SetLocalTickRate(RcLocal.PlayerIndex, newRate);
            return true;
        }
    }
}
