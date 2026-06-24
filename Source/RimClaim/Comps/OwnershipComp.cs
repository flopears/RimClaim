using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// Attached to all ThingWithComps-derived objects (buildings, furniture) via
    /// PatchOperation in Patches/OwnershipComp_Inject.xml.
    ///
    /// Tracks:
    ///   ownerPlayerIndex — which player "owns" this thing (-1 = unclaimed)
    ///   teamShared       — whether teammates of the owner can also access it
    /// </summary>
    public class CompProperties_Ownership : CompProperties
    {
        public CompProperties_Ownership()
        {
            compClass = typeof(OwnershipComp);
        }
    }

    public class OwnershipComp : ThingComp
    {
        // ── State ──────────────────────────────────────────────────────────────
        /// <summary>-1 = unclaimed/world. ≥0 = playerIndex of the owner.</summary>
        public int ownerPlayerIndex = -1;

        /// <summary>
        /// If true, teammates of the owner can interact with this thing
        /// according to the team's SharedPermissions.
        /// </summary>
        public bool teamShared = false;

        // ── Convenience ────────────────────────────────────────────────────────
        public bool IsClaimed     => ownerPlayerIndex >= 0;
        public bool IsUnclaimed   => ownerPlayerIndex < 0;

        /// <summary>True if the local player owns this thing.</summary>
        public bool IsOwnedByLocal =>
            MP.IsInMultiplayer && ownerPlayerIndex == RcLocal.PlayerIndex;

        /// <summary>
        /// True if the local player can access this thing —
        /// either because they own it, it's unclaimed, or it's team-shared
        /// and they are a teammate with the right permission.
        /// </summary>
        public bool LocalPlayerCanAccess
        {
            get
            {
                if (!MP.IsInMultiplayer) return true;           // SP: always
                if (IsUnclaimed)  return true;           // unclaimed: always
                if (IsOwnedByLocal) return true;         // own it: always

                if (!teamShared) return false;           // not shared: no

                // Check team + specific permission for this type of thing
                var teams = Current.Game.GetComponent<TeamRegistry>();
                if (teams == null) return false;

                var perms = teams.GetPermissions(RcLocal.PlayerIndex, ownerPlayerIndex);
                return parent switch
                {
                    Building_Door  => perms.shareDoors,
                    IBillGiver     => perms.shareBuildings || perms.shareBills,
                    Building       => perms.shareBuildings,
                    _              => perms.shareResources,
                };
            }
        }

        // ── Synced mutations ───────────────────────────────────────────────────

        /// <summary>
        /// Claim this thing for a player. Pass -1 to unclaim.
        /// All callers must route through here — never mutate ownerPlayerIndex directly.
        /// </summary>
        public void SetOwner(int playerIndex, bool shared = false)
        {
            ownerPlayerIndex = playerIndex;
            teamShared       = shared;
        }

        /// <summary>Transfer ownership to another player (e.g. gifting).</summary>
        public void TransferOwner(int toPlayerIndex)
        {
            ownerPlayerIndex = toPlayerIndex;
            // teamShared stays as-is
        }

        /// <summary>Toggle team sharing without changing the owner.</summary>
        public void SetTeamShared(bool shared)
        {
            teamShared = shared;
        }

        // ── Gizmos (right-click UI on selected things) ─────────────────────────
        public override System.Collections.Generic.IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!MP.IsInMultiplayer) yield break;
            if (!IsOwnedByLocal && !IsUnclaimed) yield break;

            // "Claim" button for unclaimed things
            if (IsUnclaimed)
            {
                yield return new Command_Action
                {
                    defaultLabel = "RC_Claim".Translate(),
                    defaultDesc  = "RC_ClaimDesc".Translate(),
                    icon         = TexButton.RC_Claim,
                    action       = () => SetOwner(RcLocal.PlayerIndex, false)
                };
                yield break;
            }

            // Already owned by local player
            yield return new Command_Action
            {
                defaultLabel = teamShared ? "RC_Unshare".Translate() : "RC_ShareWithTeam".Translate(),
                defaultDesc  = teamShared ? "RC_UnshareDesc".Translate() : "RC_ShareWithTeamDesc".Translate(),
                icon         = teamShared ? TexButton.RC_Unshare : TexButton.RC_Share,
                action       = () => SetTeamShared(!teamShared)
            };

            yield return new Command_Action
            {
                defaultLabel = "RC_Unclaim".Translate(),
                defaultDesc  = "RC_UnclaimDesc".Translate(),
                icon         = TexButton.RC_Unclaim,
                action       = () => SetOwner(-1, false)
            };
        }

        // ── Inspect string ─────────────────────────────────────────────────────
        public override string CompInspectStringExtra()
        {
            if (!MP.IsInMultiplayer || IsUnclaimed) return null!;

            var registry = Current.Game.GetComponent<PlayerRegistry>();
            var owner    = registry?.GetPlayer(ownerPlayerIndex);
            string ownerName = owner?.displayName ?? $"Player {ownerPlayerIndex}";

            string shared = teamShared ? " (shared)" : "";
            return $"Owner: {ownerName}{shared}";
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ownerPlayerIndex, "ownerPlayerIndex", -1);
            Scribe_Values.Look(ref teamShared,       "teamShared",       false);
        }
    }
}
