using System.Linq;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    public enum CeremonyType
    {
        RoyalBestowing,
        IdeologyRitual,
        BiotechGestation,
        AnomalyContainment,
        GravshipLaunch,
    }

    public class ActiveCeremony : IExposable
    {
        public int          ownerPlayerIndex;
        public CeremonyType type;
        public int          anchorThingId;
        public int          startedAtTick;
        public int          lastAttackerPlayerIndex = -1;

        public bool IsParticipant(Pawn pawn)
        {
            if (pawn?.Map == null) return false;
            var anchor = pawn.Map.listerThings.AllThings
                .FirstOrDefault(t => t.thingIDNumber == anchorThingId);
            if (anchor == null) return false;
            return pawn.Position.DistanceTo(anchor.Position) < 10f;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ownerPlayerIndex,         "ownerPlayerIndex",         -1);
            Scribe_Values.Look(ref type,                     "type");
            Scribe_Values.Look(ref anchorThingId,            "anchorThingId",            -1);
            Scribe_Values.Look(ref startedAtTick,            "startedAtTick",            0);
            Scribe_Values.Look(ref lastAttackerPlayerIndex,  "lastAttackerPlayerIndex",  -1);
        }
    }

    public class CeremonyTracker : MapComponent
    {
        public ActiveCeremony? ActiveCeremony;

        public CeremonyTracker(Map map) : base(map) { }

        public static CeremonyTracker? For(Map map)
            => map?.GetComponent<CeremonyTracker>();

        public void RegisterCeremony(int ownerPlayerIndex,
            CeremonyType type, int anchorThingId)
        {
            ActiveCeremony = new ActiveCeremony
            {
                ownerPlayerIndex = ownerPlayerIndex,
                type             = type,
                anchorThingId    = anchorThingId,
                startedAtTick    = map.MapTicks(),
            };
        }

        public void ClearCeremony()
        {
            ActiveCeremony = null;
        }

        public void RecordAttacker(int attackerPlayerIndex)
        {
            if (ActiveCeremony != null)
                ActiveCeremony.lastAttackerPlayerIndex = attackerPlayerIndex;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref ActiveCeremony, "activeCeremony");
        }
    }
}
