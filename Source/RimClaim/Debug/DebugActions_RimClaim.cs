using System.Linq;
using System.Text;
using LudeonTK;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim.Debug
{
    /// <summary>
    /// Dev-mode (LudeonTK) debug actions for exercising RimClaim's ownership
    /// system in a live game. They appear under the "RimClaim" category in the
    /// in-game debug action menu (Dev mode → "Open debug actions menu").
    ///
    /// The mod's logic is gated on <see cref="MP.IsInMultiplayer"/>, so all of
    /// these refuse to run outside a Multiplayer session and tell you why — that
    /// keeps test results honest rather than silently exercising SP-only paths.
    ///
    /// Mutating actions route through the same methods the real gizmos use
    /// (OwnershipComp.SetOwner, ZoneOwnershipData.SetZoneOwner, …) so they go
    /// through Multiplayer's command sync and stay deterministic across clients.
    /// </summary>
    public static class DebugActions_RimClaim
    {
        private const string Cat = "RimClaim";

        // ── Guard ───────────────────────────────────────────────────────────────
        /// <summary>
        /// Returns true and lets the action proceed only inside an MP session.
        /// Otherwise posts a visible message and returns false.
        /// </summary>
        private static bool RequireMultiplayer()
        {
            if (MP.IsInMultiplayer) return true;
            Messages.Message(
                "[RimClaim] Debug actions require an active Multiplayer session — " +
                "the ownership system is MP-only.",
                MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        // ── Read-only: full state dump ───────────────────────────────────────────
        [DebugAction(Cat, "Dump ownership state to log",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpState()
        {
            if (!RequireMultiplayer()) return;

            var sb = new StringBuilder();
            sb.AppendLine("===== [RimClaim] Ownership state dump =====");
            sb.AppendLine($"Local player index (Faction.OfPlayer.loadID): {RcLocal.PlayerIndex}");
            sb.AppendLine($"Local player name: {RcLocal.PlayerName}");

            // Players
            var players = Current.Game.GetComponent<PlayerRegistry>();
            sb.AppendLine();
            sb.AppendLine($"-- Players ({players?.AllPlayers.Count ?? 0}) --");
            if (players != null)
            {
                foreach (var p in players.AllPlayers)
                {
                    sb.AppendLine(
                        $"  [{p.playerIndex}] {p.displayName} " +
                        $"connected={p.isConnected} faction={p.factionLoadId}");
                }
            }

            // Teams + permissions
            var teams = Current.Game.GetComponent<TeamRegistry>();
            sb.AppendLine();
            sb.AppendLine($"-- Teams ({teams?.AllTeams.Count ?? 0}) --");
            if (teams != null)
            {
                foreach (var t in teams.AllTeams)
                {
                    string members = string.Join(", ", t.memberPlayerIndices);
                    sb.AppendLine($"  team[{t.teamId}] \"{t.teamName}\" members=[{members}]");
                    var pm = t.permissions;
                    sb.AppendLine(
                        $"      resources={pm.shareResources} storage={pm.shareStorage} " +
                        $"furniture={pm.shareFurniture} buildings={pm.shareBuildings} " +
                        $"areas={pm.shareAreas} bills={pm.shareBills} " +
                        $"pawnBar={pm.sharePawnBar} doors={pm.shareDoors}");
                }
            }

            // Claimed things on the current map
            var map = Find.CurrentMap;
            sb.AppendLine();
            if (map == null)
            {
                sb.AppendLine("-- No current map --");
            }
            else
            {
                int claimed = 0;
                foreach (var thing in map.listerThings.AllThings)
                {
                    var comp = thing.TryGetComp<OwnershipComp>();
                    if (comp == null || comp.IsUnclaimed) continue;
                    claimed++;
                    if (claimed <= 50) // cap the spam
                    {
                        sb.AppendLine(
                            $"  {thing.LabelCap} @ {thing.Position} " +
                            $"owner={comp.ownerPlayerIndex} shared={comp.teamShared}");
                    }
                }
                sb.AppendLine($"-- Claimed things on '{map}' : {claimed} " +
                              (claimed > 50 ? "(first 50 listed) --" : "--"));

                // Zone ownership
                var zoneData = ZoneOwnershipData.For(map);
                sb.AppendLine();
                int ownedZones = 0;
                if (zoneData != null)
                {
                    foreach (var zone in map.zoneManager.AllZones)
                    {
                        if (zoneData.IsUnclaimed(zone)) continue;
                        ownedZones++;
                        sb.AppendLine(
                            $"  zone \"{zone.label}\" (#{zone.ID}) " +
                            $"owner={zoneData.GetOwner(zone)} shared={zoneData.IsShared(zone)}");
                    }
                }
                sb.AppendLine($"-- Owned zones on '{map}' : {ownedZones} --");
            }

            sb.AppendLine("===== end dump =====");
            Log.Message(sb.ToString());
            Messages.Message("[RimClaim] Ownership state written to log.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        // ── Mutations on the current selection ───────────────────────────────────
        [DebugAction(Cat, "Claim selected things for local player",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ClaimSelected()
        {
            if (!RequireMultiplayer()) return;
            int me = RcLocal.PlayerIndex;
            int n = ForEachSelectedOwnershipComp(c => c.SetOwner(me, false));
            Messages.Message($"[RimClaim] Claimed {n} thing(s) for player {me}.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        [DebugAction(Cat, "Unclaim selected things",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UnclaimSelected()
        {
            if (!RequireMultiplayer()) return;
            int n = ForEachSelectedOwnershipComp(c => c.SetOwner(-1, false));
            Messages.Message($"[RimClaim] Unclaimed {n} thing(s).",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        [DebugAction(Cat, "Toggle team-share on selected things",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleShareSelected()
        {
            if (!RequireMultiplayer()) return;
            int n = ForEachSelectedOwnershipComp(c => c.SetTeamShared(!c.teamShared));
            Messages.Message($"[RimClaim] Toggled team-share on {n} thing(s).",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        // ── Zone mutation on the selected zone ───────────────────────────────────
        [DebugAction(Cat, "Claim selected zone for local player",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ClaimSelectedZone()
        {
            if (!RequireMultiplayer()) return;

            var zone = Find.Selector.SelectedZone;
            if (zone == null)
            {
                Messages.Message("[RimClaim] No zone selected.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            var data = ZoneOwnershipData.For(zone.Map);
            if (data == null) return;
            data.SetZoneOwner(zone.ID, RcLocal.PlayerIndex, shared: false);
            Messages.Message(
                $"[RimClaim] Claimed zone \"{zone.label}\" for player {RcLocal.PlayerIndex}.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        // ── Helper ───────────────────────────────────────────────────────────────
        /// <summary>
        /// Applies <paramref name="apply"/> to the OwnershipComp of every selected
        /// thing that has one. Returns how many comps were affected.
        /// </summary>
        private static int ForEachSelectedOwnershipComp(System.Action<OwnershipComp> apply)
        {
            int n = 0;
            foreach (var obj in Find.Selector.SelectedObjects.ToList())
            {
                if (obj is not Thing thing) continue;
                var comp = thing.TryGetComp<OwnershipComp>();
                if (comp == null) continue;
                apply(comp);
                n++;
            }
            return n;
        }
    }
}
