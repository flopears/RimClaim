using Verse;

namespace RimClaim
{
    public enum DiplomacyEventType
    {
        DeclaredEnemy,
        StoleGoods,
        CeremonyInterrupted,
        CeremonySabotaged,
        PeaceDeclared,
    }

    public class DiplomacyRecord : IExposable
    {
        public int fromPlayer;
        public int toPlayer;
        public DiplomacyEventType eventType;
        public int tickOccurred;

        public string GetLabel(PlayerRegistry registry)
        {
            string from = registry?.GetPlayer(fromPlayer)?.displayName ?? $"Player {fromPlayer}";
            string to   = registry?.GetPlayer(toPlayer)?.displayName ?? $"Player {toPlayer}";
            switch (eventType)
            {
                case DiplomacyEventType.DeclaredEnemy:        return $"{from} declared {to} enemy";
                case DiplomacyEventType.PeaceDeclared:        return $"{from} offered peace to {to}";
                case DiplomacyEventType.StoleGoods:           return $"{from} stole from {to}";
                case DiplomacyEventType.CeremonyInterrupted:  return $"{from} interrupted {to}'s ceremony";
                case DiplomacyEventType.CeremonySabotaged:    return $"{from} sabotaged {to}'s ritual";
                default:                                      return $"{from} → {to}: {eventType}";
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref fromPlayer,    "from",  -1);
            Scribe_Values.Look(ref toPlayer,      "to",    -1);
            Scribe_Values.Look(ref eventType,     "type");
            Scribe_Values.Look(ref tickOccurred,  "tick",  0);
        }
    }
}
