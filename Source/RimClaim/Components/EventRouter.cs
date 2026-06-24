using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    public enum RcEventClass
    {
        ClaimLocal,
        MapWide,
        WorldLevel,
        CrossClaim,
        GravshipEvent,
    }

    public class RcIncidentExtension : DefModExtension
    {
        public RcEventClass eventClass                = RcEventClass.ClaimLocal;
        public string         consequenceDescription    = string.Empty;
    }

    public class EventRouter : MapComponent
    {
        private int roundRobinCursor = 0;

        public EventRouter(Map map) : base(map) { }

        public static EventRouter? For(Map map) => map?.GetComponent<EventRouter>();

        // ── Round-robin ────────────────────────────────────────────────────────
        public int GetNextEventTarget()
        {
            var players = GetActivePlayers();
            if (players.Count == 0) return -1;
            int target = players[roundRobinCursor % players.Count].playerIndex;
            roundRobinCursor++;
            return target;
        }

        private List<PlayerData> GetActivePlayers()
            => RcWorld.Players_Safe?.ConnectedPlayers()
               .Where(p => p.GetFaction() != null).ToList()
               ?? new List<PlayerData>();

        // ── Incident routing ───────────────────────────────────────────────────
        public bool TryRouteIncident(IncidentDef incident, IncidentParms parms)
        {
            var ext       = incident.GetModExtension<RcIncidentExtension>();
            var evClass   = ext?.eventClass ?? RcEventClass.ClaimLocal;

            switch (evClass)
            {
                case RcEventClass.GravshipEvent:
                    var reg = LandclaimRegistry.For(map);
                    reg?.TriggerGravshipEvent();
                    incident.Worker.TryExecute(parms);
                    return true;

                case RcEventClass.ClaimLocal:
                    int target  = GetNextEventTarget();
                    if (target < 0) return false;
                    var faction = RcWorld.Players_Safe?.GetFaction(target);
                    if (faction != null) parms.faction = faction;
                    incident.Worker.TryExecute(parms);
                    return true;

                case RcEventClass.MapWide:
                case RcEventClass.WorldLevel:
                case RcEventClass.CrossClaim:
                    incident.Worker.TryExecute(parms);
                    return true;
            }
            return false;
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref roundRobinCursor, "roundRobinCursor", 0);
        }
    }
}
