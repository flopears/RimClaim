using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    public enum GravshipState { Grounded, InFlight, InOrbit }

    public class GravshipOwnerComp : MapComponent
    {
        public int           ownerPlayerIndex     = -1;
        public bool          teamShared           = false;
        public GravshipState State                = GravshipState.Grounded;
        public int           currentWorldTile     = -1;
        public int           destinationWorldTile = -1;
        public int           gravEngineThingId    = -1;

        // Boarding whitelist for neutral players
        private HashSet<int> boardingWhitelist    = new();

        public GravshipOwnerComp(Map map) : base(map) { }

        public static GravshipOwnerComp? For(Map map)
            => map?.GetComponent<GravshipOwnerComp>();

        public bool CanPlayerNavigate(int playerIndex)
        {
            if (playerIndex == ownerPlayerIndex) return true;
            if (!teamShared) return false;
            var teams = RcWorld.Teams_Safe;
            return teams != null &&
                   teams.GetPermissions(playerIndex, ownerPlayerIndex).shareBuildings;
        }

        public bool CanPawnBoard(Pawn pawn)
        {
            if (pawn?.Faction == null) return true; // NPC
            var registry = RcWorld.Players_Safe;
            if (registry == null) return true;

            foreach (var player in registry.AllPlayers)
            {
                if (player.GetFaction() != pawn.Faction) continue;

                if (player.playerIndex == ownerPlayerIndex) return true;

                var diplo = RcWorld.Diplomacy_Safe;
                if (diplo?.AreEnemies(ownerPlayerIndex, player.playerIndex) == true)
                    return false;

                if (boardingWhitelist.Contains(pawn.thingIDNumber)) return true;

                var teams = RcWorld.Teams_Safe;
                if (teams?.AreTeammates(ownerPlayerIndex, player.playerIndex) == true
                    && teamShared) return true;

                return false;
            }
            return true;
        }

        public void SetOwner(int playerIndex, bool shared)
        {
            ownerPlayerIndex = playerIndex;
            teamShared       = shared;
        }

        public void SetState(GravshipState newState)
        {
            State = newState;
            var reg = LandclaimRegistry.For(map);
            if (reg == null) return;

            if (newState == GravshipState.Grounded)
            {
                // Re-activate claim block if present
                var block = map.listerBuildings
                    .AllBuildingsColonistOfClass<Building_LandclaimBlock>()
                    .FirstOrDefault();
                block?.OnShipLanded(map);
            }
            else
            {
                // Deactivate — ship is in flight/orbit
                var block = map.listerBuildings
                    .AllBuildingsColonistOfClass<Building_LandclaimBlock>()
                    .FirstOrDefault();
                block?.OnShipLaunched();
            }
        }

        public void InvitePawnAboard(int pawnThingId)
        {
            boardingWhitelist.Add(pawnThingId);
        }

        public void SetDestination(int playerIndex, int destTile)
        {
            if (!CanPlayerNavigate(playerIndex)) return;
            destinationWorldTile = destTile;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ownerPlayerIndex,     "ownerPlayerIndex",     -1);
            Scribe_Values.Look(ref teamShared,           "teamShared",           false);
            Scribe_Values.Look(ref State,                "state",                GravshipState.Grounded);
            Scribe_Values.Look(ref currentWorldTile,     "currentWorldTile",     -1);
            Scribe_Values.Look(ref destinationWorldTile, "destinationWorldTile", -1);
            Scribe_Values.Look(ref gravEngineThingId,    "gravEngineThingId",    -1);
            Scribe_Collections.Look(ref boardingWhitelist, "boardingWhitelist",  LookMode.Value);
            boardingWhitelist ??= new HashSet<int>();
        }
    }
}
