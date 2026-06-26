using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public class SpeedHookHUD : GameComponent
    {
        private static Vector2 pausePanelPos = new(-1f, -1f);
        private static bool dragging = false;
        private static Vector2 dragOffset;

        public SpeedHookHUD(Game game) { }

        public override void GameComponentOnGUI()
        {
            if (!MP.IsInMultiplayer) return;
            if (Current.ProgramState != ProgramState.Playing) return;

            DrawGravshipEventLock();
            DrawPausePanel();

            if (SpeedHookManager.IsHooked)
                DrawHookIndicator();
        }

        private void DrawHookIndicator()
        {
            var registry    = RcWorld.Players_Safe;
            var localPlayer = registry?.GetLocalPlayer();
            var color       = localPlayer?.playerColor ?? Color.white;

            int?   rate    = SpeedHookManager.HookedRate;
            string rateStr = rate == null  ? "?" :
                             rate == 0     ? "Paused" :
                             $"{rate}x";

            var rect = new Rect(
                Verse.UI.screenWidth / 2f - 90f,
                Verse.UI.screenHeight - 180f,
                180f, 40f);

            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.7f));
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 3f, rect.height), color);

            GUI.color = color;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 8f, 18f),
                $"Zone: {rateStr}");
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 8f, 16f),
                $"Global: {Find.TickManager.CurTimeSpeed}");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private void DrawPausePanel()
        {
            var negotiator = Current.Game?.GetComponent<SpeedNegotiator>();
            var pr = Current.Game?.GetComponent<PlayerRegistry>();
            if (negotiator == null || pr == null) return;

            bool hasPending = negotiator.PendingRequest != null;
            bool isPaused = negotiator.IsPaused;
            int remaining = pr.EmergencyPausesRemaining(RcLocal.PlayerIndex);

            float panelH = 50f;
            if (isPaused) panelH += 24f;
            if (hasPending) panelH += 24f;
            float panelW = 200f;

            if (pausePanelPos.x < 0)
                pausePanelPos = new Vector2(8f, Verse.UI.screenHeight / 2f - panelH / 2f);

            var panelRect = new Rect(pausePanelPos.x, pausePanelPos.y, panelW, panelH);

            HandleDrag(ref panelRect);

            Widgets.DrawBoxSolid(panelRect, new Color(0.08f, 0.08f, 0.12f, 0.85f));
            Widgets.DrawBox(panelRect);

            float y = panelRect.y + 4f;
            float x = panelRect.x + 6f;
            float w = panelW - 12f;

            // PAUSED banner
            if (isPaused)
            {
                GUI.color = new Color(1f, 0.6f, 0.2f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(x, y, w - 60f, 20f), "GAME PAUSED");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                if (Widgets.ButtonText(new Rect(panelRect.xMax - 66f, y, 58f, 20f), "Resume"))
                    negotiator.Unpause(RcLocal.PlayerIndex);
                y += 24f;
            }

            // Pending pause request
            if (hasPending)
            {
                var pending = negotiator.PendingRequest!;
                var players = RcWorld.Players_Safe;
                bool isMe = pending.requestingPlayer == RcLocal.PlayerIndex;

                Text.Font = GameFont.Tiny;
                if (isMe)
                {
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(x, y, w, 20f), "Pause request pending...");
                    GUI.color = Color.white;
                }
                else
                {
                    string who = players?.GetPlayer(pending.requestingPlayer)?.displayName ?? "?";
                    GUI.color = new Color(0.5f, 0.6f, 1f);
                    Widgets.Label(new Rect(x, y, w - 100f, 20f), $"{who} wants pause");
                    GUI.color = Color.white;

                    if (Widgets.ButtonText(new Rect(panelRect.xMax - 106f, y, 46f, 20f), "OK"))
                        negotiator.AcceptSoftPause();
                    if (Widgets.ButtonText(new Rect(panelRect.xMax - 56f, y, 48f, 20f), "No"))
                        negotiator.ObjectToPause(RcLocal.PlayerIndex);
                }
                Text.Font = GameFont.Small;
                y += 24f;
            }

            // Pause buttons + budget
            float btnW = (w - 4f) / 2f;
            if (Widgets.ButtonText(new Rect(x, y, btnW, 22f), "Pause"))
                negotiator.RequestSoftPause(RcLocal.PlayerIndex);

            GUI.color = remaining > 0 ? new Color(0.9f, 0.3f, 0.3f) : Color.gray;
            if (Widgets.ButtonText(new Rect(x + btnW + 4f, y, btnW, 22f), "EMERG"))
            {
                if (remaining > 0)
                    negotiator.RequestEmergencyPause(RcLocal.PlayerIndex);
                else
                    Messages.Message("No emergency pauses left today.",
                        MessageTypeDefOf.RejectInput, false);
            }
            GUI.color = Color.white;
            y += 24f;

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(x, y, w, 16f),
                $"Emergency: {remaining}/{Constants.MaxEmergencyPausesPerDay} remaining");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void HandleDrag(ref Rect rect)
        {
            var evt = Event.current;
            var headerRect = new Rect(rect.x, rect.y, rect.width, 14f);

            if (evt.type == EventType.MouseDown && headerRect.Contains(evt.mousePosition))
            {
                dragging = true;
                dragOffset = evt.mousePosition - new Vector2(rect.x, rect.y);
                evt.Use();
            }

            if (dragging)
            {
                if (evt.type == EventType.MouseDrag || evt.type == EventType.MouseUp)
                {
                    pausePanelPos = evt.mousePosition - dragOffset;
                    pausePanelPos.x = Mathf.Clamp(pausePanelPos.x, 0, Verse.UI.screenWidth - rect.width);
                    pausePanelPos.y = Mathf.Clamp(pausePanelPos.y, 0, Verse.UI.screenHeight - rect.height);
                    rect.x = pausePanelPos.x;
                    rect.y = pausePanelPos.y;
                }
                if (evt.type == EventType.MouseUp)
                    dragging = false;
                if (evt.type == EventType.MouseDrag)
                    evt.Use();
            }
        }

        private void DrawGravshipEventLock()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            var reg = LandclaimRegistry.For(map);
            if (reg == null || !reg.gravshipEventActive) return;

            var rect = new Rect(
                Verse.UI.screenWidth / 2f - 140f,
                40f, 280f, 26f);

            Widgets.DrawBoxSolid(rect, new Color(0.6f, 0f, 0f, 0.85f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect, "GRAVSHIP EVENT — All speeds locked 1x");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
