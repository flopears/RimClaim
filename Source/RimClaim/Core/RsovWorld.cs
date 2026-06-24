using Verse;

namespace RimClaim
{
    public static class RcWorld
    {
        public static PlayerRegistry    Players    => Current.Game.GetComponent<PlayerRegistry>()    ?? throw new System.InvalidOperationException("[RC] PlayerRegistry not found.");
        public static TeamRegistry      Teams      => Current.Game.GetComponent<TeamRegistry>()      ?? throw new System.InvalidOperationException("[RC] TeamRegistry not found.");
        public static DiplomacyRegistry Diplomacy  => Current.Game.GetComponent<DiplomacyRegistry>() ?? throw new System.InvalidOperationException("[RC] DiplomacyRegistry not found.");
        public static SpeedNegotiator   Pause      => Current.Game.GetComponent<SpeedNegotiator>()   ?? throw new System.InvalidOperationException("[RC] SpeedNegotiator not found.");

        public static PlayerRegistry?    Players_Safe   => Current.Game.GetComponent<PlayerRegistry>();
        public static TeamRegistry?      Teams_Safe     => Current.Game.GetComponent<TeamRegistry>();
        public static DiplomacyRegistry? Diplomacy_Safe => Current.Game.GetComponent<DiplomacyRegistry>();
        public static SpeedNegotiator?   Pause_Safe     => Current.Game.GetComponent<SpeedNegotiator>();
    }
}
