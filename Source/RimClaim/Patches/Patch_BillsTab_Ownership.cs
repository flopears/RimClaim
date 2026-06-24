using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim.Patches
{
    /// <summary>
    /// Blocks the Bills tab on workbenches owned by another player,
    /// unless the owner has team-shared the bench with bills permission.
    ///
    /// Harmony target: ITab_Bills.FillTab()
    ///
    /// Rendering patch only — no sync.
    /// </summary>
    [HarmonyPatch(typeof(ITab_Bills), "FillTab")]
    public static class Patch_BillsTab_Ownership
    {
        public static bool Prefix(ITab_Bills __instance)
        {
            if (!MP.IsInMultiplayer) return true;

            var selected = Find.Selector.SingleSelectedThing;
            if (selected == null) return true;

            var comp = selected.TryGetComp<OwnershipComp>();
            if (comp == null || comp.IsUnclaimed || comp.IsOwnedByLocal) return true;

            // Another player owns this bench — check team access
            var teams = RcWorld.Teams_Safe;
            if (teams != null)
            {
                var perms = teams.GetPermissions(RcLocal.PlayerIndex, comp.ownerPlayerIndex);
                if (perms.shareBills || perms.shareBuildings) return true;
            }

            // Draw a "not your bench" message instead of the bills UI
            DrawLockedMessage(__instance);
            return false; // skip original FillTab
        }

        private static void DrawLockedMessage(ITab_Bills tab)
        {
            // We need to get the tab's rect. ITab exposes a protected size via reflection.
            var sizeField = typeof(ITab).GetField("size",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var size = sizeField != null ? (Vector2)sizeField.GetValue(tab) : new Vector2(320f, 200f);

            var rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            Widgets.Label(rect,
                "RC_BillsTabLocked".Translate());
        }
    }
}
