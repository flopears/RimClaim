using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// GameComponent — owns all teams and inter-player permission queries.
    ///
    /// Access: Find.GameComponent&lt;TeamRegistry&gt;()
    ///         or RcWorld.Teams shortcut.
    /// </summary>
    public class TeamRegistry : GameComponent
    {
        private List<TeamData> teams = new();
        private int nextTeamId = 0;

        public TeamRegistry(Game game) { }

        // ── Public read API ────────────────────────────────────────────────────

        public IReadOnlyList<TeamData> AllTeams => teams;

        public TeamData? GetTeam(int teamId)
            => teams.FirstOrDefault(t => t.teamId == teamId);

        /// <summary>Returns the team that contains both players, or null.</summary>
        public TeamData? GetSharedTeam(int playerA, int playerB)
            => teams.FirstOrDefault(t => t.HasMember(playerA) && t.HasMember(playerB));

        /// <summary>True if both players are on the same team.</summary>
        public bool AreTeammates(int playerA, int playerB)
        {
            if (playerA == playerB) return true; // always your own teammate
            return GetSharedTeam(playerA, playerB) != null;
        }

        /// <summary>
        /// Returns the permissions between two players.
        /// Returns a blank (all-false) permissions object if they are not teammates.
        /// </summary>
        public SharedPermissions GetPermissions(int playerA, int playerB)
        {
            if (playerA == playerB)
            {
                // Full self-access
                var full = new SharedPermissions();
                full.SetAll(true);
                return full;
            }
            return GetSharedTeam(playerA, playerB)?.permissions ?? new SharedPermissions();
        }

        // ── Synced mutations ───────────────────────────────────────────────────

        [SyncMethod]
        public void CreateTeam(string name)
        {
            teams.Add(new TeamData { teamId = nextTeamId++, teamName = name });
        }

        [SyncMethod]
        public void DisbandTeam(int teamId)
        {
            teams.RemoveAll(t => t.teamId == teamId);
        }

        [SyncMethod]
        public void AddMember(int teamId, int playerIndex)
        {
            var team = GetTeam(teamId);
            if (team == null) return;
            if (!team.memberPlayerIndices.Contains(playerIndex))
                team.memberPlayerIndices.Add(playerIndex);
        }

        [SyncMethod]
        public void RemoveMember(int teamId, int playerIndex)
        {
            var team = GetTeam(teamId);
            team?.memberPlayerIndices.Remove(playerIndex);

            // Auto-disband if team falls below 2 members
            if (team != null && team.memberPlayerIndices.Count < 2)
                teams.Remove(team);
        }

        [SyncMethod]
        public void RenameTeam(int teamId, string newName)
        {
            var team = GetTeam(teamId);
            if (team != null) team.teamName = newName;
        }

        // ── Permission setters (one per flag, all synced) ──────────────────────

        [SyncMethod] public void SetShareResources(int teamId, bool val) => SetPerm(teamId, p => p.shareResources = val);
        [SyncMethod] public void SetShareStorage(int teamId, bool val)   => SetPerm(teamId, p => p.shareStorage   = val);
        [SyncMethod] public void SetShareFurniture(int teamId, bool val) => SetPerm(teamId, p => p.shareFurniture = val);
        [SyncMethod] public void SetShareBuildings(int teamId, bool val) => SetPerm(teamId, p => p.shareBuildings = val);
        [SyncMethod] public void SetShareAreas(int teamId, bool val)     => SetPerm(teamId, p => p.shareAreas     = val);
        [SyncMethod] public void SetShareBills(int teamId, bool val)     => SetPerm(teamId, p => p.shareBills     = val);
        [SyncMethod] public void SetSharePawnBar(int teamId, bool val)   => SetPerm(teamId, p => p.sharePawnBar   = val);
        [SyncMethod] public void SetShareDoors(int teamId, bool val)     => SetPerm(teamId, p => p.shareDoors     = val);

        [SyncMethod]
        public void SetAllPermissions(int teamId, bool val)
            => SetPerm(teamId, p => p.SetAll(val));

        private void SetPerm(int teamId, System.Action<SharedPermissions> apply)
        {
            var team = GetTeam(teamId);
            if (team != null) apply(team.permissions);
        }

        // ── Serialization ──────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref teams, "teams", LookMode.Deep);
            Scribe_Values.Look(ref nextTeamId, "nextTeamId", 0);
            teams ??= new List<TeamData>();
        }
    }
}
