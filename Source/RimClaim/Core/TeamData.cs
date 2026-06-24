using System.Collections.Generic;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// Granular permissions that govern what teammates can access from each other.
    /// Every field maps to a checkbox in the Team tab UI.
    /// </summary>
    public class SharedPermissions : IExposable
    {
        /// <summary>Teammates can haul from each other's stockpiles.</summary>
        public bool shareResources = false;

        /// <summary>Teammates' stockpiles appear accessible (green) to each other.</summary>
        public bool shareStorage = false;

        /// <summary>Beds, chairs, and other assignable furniture are mutually usable.</summary>
        public bool shareFurniture = false;

        /// <summary>Production buildings, research benches, etc. are mutually accessible.</summary>
        public bool shareBuildings = false;

        /// <summary>Zones/areas are visible and usable by all teammates.</summary>
        public bool shareAreas = false;

        /// <summary>Teammates can view and add bills to each other's workbenches.</summary>
        public bool shareBills = false;

        /// <summary>Each teammate's pawns appear in your colonist bar.</summary>
        public bool sharePawnBar = false;

        /// <summary>Teammates can open each other's doors.</summary>
        public bool shareDoors = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref shareResources, "shareResources", false);
            Scribe_Values.Look(ref shareStorage,   "shareStorage",   false);
            Scribe_Values.Look(ref shareFurniture, "shareFurniture", false);
            Scribe_Values.Look(ref shareBuildings, "shareBuildings", false);
            Scribe_Values.Look(ref shareAreas,     "shareAreas",     false);
            Scribe_Values.Look(ref shareBills,     "shareBills",     false);
            Scribe_Values.Look(ref sharePawnBar,   "sharePawnBar",   false);
            Scribe_Values.Look(ref shareDoors,     "shareDoors",     false);
        }

        /// <summary>Returns true if any sharing permission is enabled.</summary>
        public bool AnySharing =>
            shareResources || shareStorage  || shareFurniture || shareBuildings ||
            shareAreas     || shareBills    || sharePawnBar   || shareDoors;

        /// <summary>Enables all permissions at once (full team alliance).</summary>
        public void SetAll(bool value)
        {
            shareResources = shareStorage  = shareFurniture = shareBuildings =
            shareAreas     = shareBills    = sharePawnBar   = shareDoors     = value;
        }
    }

    /// <summary>
    /// A named group of players with a shared permission set.
    /// Stored in TeamRegistry.
    /// </summary>
    public class TeamData : IExposable
    {
        public int teamId;
        public string teamName = "Team";

        /// <summary>playerIndex values of all members.</summary>
        public List<int> memberPlayerIndices = new();

        /// <summary>
        /// Single permission set that applies to all member pairs.
        /// (Future: per-pair permissions are possible but complex — keep it simple first.)
        /// </summary>
        public SharedPermissions permissions = new();

        public bool HasMember(int playerIndex)
            => memberPlayerIndices.Contains(playerIndex);

        public void ExposeData()
        {
            Scribe_Values.Look(ref teamId,   "teamId",   0);
            Scribe_Values.Look(ref teamName, "teamName", "Team");
            Scribe_Collections.Look(ref memberPlayerIndices, "members", LookMode.Value);
            Scribe_Deep.Look(ref permissions, "permissions");

            memberPlayerIndices ??= new List<int>();
            permissions         ??= new SharedPermissions();
        }
    }
}
