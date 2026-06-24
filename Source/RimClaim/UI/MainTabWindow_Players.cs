using System.Collections.Generic;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim.UI
{
    /// <summary>
    /// The "Players" main tab window.
    /// Two sub-tabs: Team and Enemies.
    /// Opened via the bottom-bar tab button defined in MainTabDef.xml.
    /// </summary>
    public class MainTabWindow_Players : MainTabWindow
    {
        // ── Sub-tab state ──────────────────────────────────────────────────────
        private enum SubTab { Team, Enemies }
        private SubTab activeSubTab = SubTab.Team;

        // ── Window sizing ──────────────────────────────────────────────────────
        public override Vector2 RequestedTabSize =>
            new Vector2(Constants.PlayersPanelWidth, 480f);

        // ── Draw ───────────────────────────────────────────────────────────────
        public override void DoWindowContents(Rect inRect)
        {
            if (!MP.IsInMultiplayer)
            {
                var msgRect = inRect.ContractedBy(20f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.gray;
                Widgets.Label(msgRect,
                    "RimClaim\n\nStart a multiplayer session via Zetrith's Multiplayer mod to use territory and player features.");
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Sub-tab buttons
            float tabBtnW = inRect.width / 2f;
            var teamRect   = new Rect(inRect.x,          inRect.y, tabBtnW, 30f);
            var enemyRect  = new Rect(inRect.x + tabBtnW, inRect.y, tabBtnW, 30f);

            if (Widgets.ButtonText(teamRect,  "RC_TeamSubTab".Translate(),
                active: activeSubTab == SubTab.Team))
                activeSubTab = SubTab.Team;

            if (Widgets.ButtonText(enemyRect, "RC_EnemiesSubTab".Translate(),
                active: activeSubTab == SubTab.Enemies))
                activeSubTab = SubTab.Enemies;

            var contentRect = new Rect(inRect.x, inRect.y + 34f,
                                       inRect.width, inRect.height - 34f);

            switch (activeSubTab)
            {
                case SubTab.Team:    DrawTeamTab(contentRect);    break;
                case SubTab.Enemies: DrawEnemiesTab(contentRect); break;
            }
        }

        // ── Team sub-tab ───────────────────────────────────────────────────────
        private Vector2 teamScroll;

        private void DrawTeamTab(Rect rect)
        {
            var registry = RcWorld.Players_Safe;
            var teams    = RcWorld.Teams_Safe;
            if (registry == null || teams == null) return;

            float y = 0f;
            var viewRect = new Rect(0f, 0f, rect.width - 20f, CalcTeamHeight(registry, teams));
            Widgets.BeginScrollView(rect, ref teamScroll, viewRect);

            // All connected players
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "Players:");
            y += 28f;

            foreach (var player in registry.ConnectedPlayers())
            {
                DrawPlayerRow(new Rect(0f, y, viewRect.width, Constants.PlayerRowHeight), player, teams);
                y += Constants.PlayerRowHeight + 4f;
            }

            y += 8f;
            Widgets.DrawLineHorizontal(0f, y, viewRect.width);
            y += 8f;

            // Teams list
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "Teams:");
            y += 28f;

            foreach (var team in teams.AllTeams)
            {
                y = DrawTeamBlock(new Rect(0f, y, viewRect.width, 0f), team, registry);
            }

            // Form new team button
            y += 8f;
            if (Widgets.ButtonText(new Rect(0f, y, 140f, 30f), "RC_CreateTeam".Translate()))
            {
                teams.CreateTeam("New Team");
            }

            Widgets.EndScrollView();
        }

        private float CalcTeamHeight(PlayerRegistry reg, TeamRegistry teams)
        {
            float h = 60f; // header
            h += reg.ConnectedPlayers().Count * (Constants.PlayerRowHeight + 4f);
            h += 50f; // team header + spacing
            foreach (var t in teams.AllTeams)
                h += 30f + t.memberPlayerIndices.Count * 24f + 200f; // perm checkboxes
            h += 50f; // create button
            return h;
        }

        private void DrawPlayerRow(Rect rect, PlayerData player, TeamRegistry teams)
        {
            // Color swatch
            var swatchRect = new Rect(rect.x, rect.y + (rect.height - Constants.PlayerColorSwatchSize) / 2f,
                                      Constants.PlayerColorSwatchSize, Constants.PlayerColorSwatchSize);
            Widgets.DrawBoxSolid(swatchRect, player.playerColor);

            // Name
            var nameRect = new Rect(swatchRect.xMax + 6f, rect.y,
                                    rect.width - swatchRect.width - 6f - 100f, rect.height);
            Widgets.Label(nameRect, player.displayName +
                (player.playerIndex == RcLocal.PlayerIndex ? " (you)" : ""));

            // Invite / Leave button
            if (player.playerIndex != RcLocal.PlayerIndex)
            {
                var team = teams.GetSharedTeam(RcLocal.PlayerIndex, player.playerIndex);
                var btnRect = new Rect(rect.xMax - 100f, rect.y + 4f, 96f, rect.height - 8f);
                if (team == null)
                {
                    if (Widgets.ButtonText(btnRect, "RC_InviteToTeam".Translate()))
                    {
                        // Create a new team with both players, or add to existing own team
                        var myTeam = GetMyTeam(teams);
                        if (myTeam != null)
                            teams.AddMember(myTeam.teamId, player.playerIndex);
                        else
                        {
                            teams.CreateTeam("Team");
                            // We can't get the new teamId synchronously here since
                            // CreateTeam is a SyncMethod. It'll show up next frame.
                            // The invite will need a second click — acceptable UX for Phase 1.
                        }
                    }
                }
                else
                {
                    if (Widgets.ButtonText(btnRect, "Remove"))
                        teams.RemoveMember(team.teamId, player.playerIndex);
                }
            }
        }

        private float DrawTeamBlock(Rect startRect, TeamData team, PlayerRegistry registry)
        {
            float y = startRect.y;
            float w = startRect.width;

            // Team header
            var headerRect = new Rect(0f, y, w - 70f, 28f);
            Widgets.Label(headerRect, team.teamName);
            if (Widgets.ButtonText(new Rect(w - 68f, y, 64f, 26f), "RC_DisbandTeam".Translate()))
            {
                RcWorld.Teams.DisbandTeam(team.teamId);
                return y;
            }
            y += 30f;

            // Members
            foreach (var memberIdx in team.memberPlayerIndices)
            {
                var p = registry.GetPlayer(memberIdx);
                Widgets.Label(new Rect(10f, y, w - 10f, 22f),
                    $"  • {p?.displayName ?? $"Player {memberIdx}"}");
                y += 24f;
            }

            // Permission checkboxes
            y += 4f;
            y = DrawPermissionCheckboxes(new Rect(0f, y, w, 0f), team);
            y += 8f;

            return y;
        }

        private float DrawPermissionCheckboxes(Rect startRect, TeamData team)
        {
            float y = startRect.y;
            float x = startRect.x + 10f;
            float w = startRect.width - 10f;
            var perms = team.permissions;

            bool allShared = perms.AnySharing &&
                perms.shareResources && perms.shareStorage && perms.shareFurniture &&
                perms.shareBuildings && perms.shareAreas   && perms.shareBills     &&
                perms.sharePawnBar   && perms.shareDoors;

            // "Share everything" master toggle
            bool allVal = allShared;
            Widgets.CheckboxLabeled(new Rect(x, y, w, 22f), "RC_Perm_All".Translate(), ref allVal);
            if (allVal != allShared) RcWorld.Teams.SetAllPermissions(team.teamId, allVal);
            y += 24f;

            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_Resources".Translate(),
                perms.shareResources, v => RcWorld.Teams.SetShareResources(team.teamId, v));
            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_Storage".Translate(),
                perms.shareStorage,   v => RcWorld.Teams.SetShareStorage(team.teamId, v));
            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_Furniture".Translate(),
                perms.shareFurniture, v => RcWorld.Teams.SetShareFurniture(team.teamId, v));
            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_Buildings".Translate(),
                perms.shareBuildings, v => RcWorld.Teams.SetShareBuildings(team.teamId, v));
            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_Areas".Translate(),
                perms.shareAreas,     v => RcWorld.Teams.SetShareAreas(team.teamId, v));
            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_Bills".Translate(),
                perms.shareBills,     v => RcWorld.Teams.SetShareBills(team.teamId, v));
            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_PawnBar".Translate(),
                perms.sharePawnBar,   v => RcWorld.Teams.SetSharePawnBar(team.teamId, v));
            DrawPerm(ref y, x, w, team.teamId, "RC_Perm_Doors".Translate(),
                perms.shareDoors,     v => RcWorld.Teams.SetShareDoors(team.teamId, v));

            return y;
        }

        private static void DrawPerm(ref float y, float x, float w,
            int teamId, string label, bool currentVal,
            System.Action<bool> setter)
        {
            bool v = currentVal;
            Widgets.CheckboxLabeled(new Rect(x + 14f, y, w - 14f, 22f), label, ref v);
            if (v != currentVal) setter(v);
            y += 24f;
        }

        private TeamData? GetMyTeam(TeamRegistry teams)
        {
            foreach (var t in teams.AllTeams)
                if (t.HasMember(RcLocal.PlayerIndex)) return t;
            return null;
        }

        // ── Enemies sub-tab ────────────────────────────────────────────────────
        private Vector2 enemyScroll;

        private void DrawEnemiesTab(Rect rect)
        {
            var registry = RcWorld.Players_Safe;
            var diplo    = RcWorld.Diplomacy_Safe;
            if (registry == null || diplo == null) return;

            float rowH    = Constants.PlayerRowHeight + 4f;
            var viewRect  = new Rect(0f, 0f, rect.width - 20f,
                               registry.ConnectedPlayers().Count * rowH + 40f);
            Widgets.BeginScrollView(rect, ref enemyScroll, viewRect);

            float y = 0f;
            foreach (var player in registry.ConnectedPlayers())
            {
                if (player.playerIndex == RcLocal.PlayerIndex) continue;

                bool isEnemy = diplo.AreEnemies(RcLocal.PlayerIndex, player.playerIndex);
                DrawEnemyRow(new Rect(0f, y, viewRect.width, Constants.PlayerRowHeight),
                             player, isEnemy, diplo);
                y += rowH;
            }

            Widgets.EndScrollView();
        }

        private void DrawEnemyRow(Rect rect, PlayerData player, bool isEnemy,
                                  DiplomacyRegistry diplo)
        {
            // Color swatch
            Widgets.DrawBoxSolid(
                new Rect(rect.x, rect.y + 8f, Constants.PlayerColorSwatchSize,
                         Constants.PlayerColorSwatchSize),
                player.playerColor);

            // Name + status
            string status = isEnemy ? ("RC_StatusEnemy".Translate()) : ("RC_StatusNeutral".Translate());
            var statusColor = isEnemy ? Color.red : Color.green;
            GUI.color = statusColor;
            Widgets.Label(new Rect(rect.x + Constants.PlayerColorSwatchSize + 8f, rect.y,
                          100f, rect.height), status);
            GUI.color = Color.white;

            Widgets.Label(new Rect(rect.x + Constants.PlayerColorSwatchSize + 110f, rect.y,
                          rect.width - 240f, rect.height), player.displayName);

            // Declare enemy / peace button
            var btnRect = new Rect(rect.xMax - 130f, rect.y + 4f, 126f, rect.height - 8f);
            if (!isEnemy)
            {
                if (Widgets.ButtonText(btnRect, "RC_DeclareEnemy".Translate()))
                {
                    diplo.SetRelation(RcLocal.PlayerIndex, player.playerIndex,
                                      DiplomacyState.Enemy);
                }
            }
            else
            {
                if (Widgets.ButtonText(btnRect, "RC_DeclarePeace".Translate()))
                {
                    diplo.DeclarePeace(RcLocal.PlayerIndex, player.playerIndex);
                }
            }
        }
    }
}
