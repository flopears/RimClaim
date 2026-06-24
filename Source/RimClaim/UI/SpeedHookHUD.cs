using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public class SpeedHookHUD : GameComponent
    {
        public SpeedHookHUD(Game game) { }

        public override void GameComponentOnGUI()
        {
            if (!MP.IsInMultiplayer) return;
            if (Current.ProgramState != ProgramState.Playing) return;

            DrawGravshipEventLock();

            if (!SpeedHookManager.IsHooked) return;
            DrawHookIndicator();
        }

        private void DrawHookIndicator()
        {
            var registry   = RcWorld.Players_Safe;
            var localPlayer = registry?.GetLocalPlayer();
            var color      = localPlayer?.playerColor ?? Color.white;

            int?   rate    = SpeedHookManager.HookedRate;
            string rateStr = rate == null  ? "?" :
                             rate == 0     ? "⏸ Paused" :
                             $"{"▶▶▶".Substring(0, rate.Value)} {rate}×";

            var rect = new Rect(
                Verse.UI.screenWidth / 2f - 110f,
                Verse.UI.screenHeight - 65f,
                220f, 44f);

            // Background
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.75f));

            // Color strip
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f, rect.height), color);

            // Zone speed line
            GUI.color = color;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 2f,
                rect.width - 8f, 20f),
                $"🏴 Zone: {rateStr}");
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 22f,
                rect.width - 8f, 20f),
                $"Global: {Find.TickManager.CurTimeSpeed.ToString()}");
            GUI.color = Color.white;
        }

        private void DrawGravshipEventLock()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            var reg = LandclaimRegistry.For(map);
            if (reg == null || !reg.gravshipEventActive) return;

            var rect = new Rect(
                Verse.UI.screenWidth / 2f - 140f,
                Verse.UI.screenHeight - 90f,
                280f, 28f);

            Widgets.DrawBoxSolid(rect, new Color(0.6f, 0f, 0f, 0.85f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "⚠  GRAVSHIP EVENT — All speeds locked 1×");
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
