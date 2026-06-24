using System.Linq;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim.Patches
{
    // ════════════════════════════════════════════════════════════════════════
    // DISABLED for 1.6.
    //
    // RimWorld 1.6 removed FloatMenuMakerMap.AddHumanlikeOrders and replaced
    // the whole right-click menu system with FloatMenuContext / GetProviderOptions.
    // The steal-from-owner float menu option needs to be reimplemented against
    // the new FloatMenuContext API. Disabled for now so it doesn't error.
    //
    // PawnOwnershipHelper is kept here because other patches use it.
    // ════════════════════════════════════════════════════════════════════════

    public static class PawnOwnershipHelper
    {
        public static int GetPawnPlayerIndex(Pawn pawn)
        {
            if (pawn?.Faction == null) return -1;
            if (!pawn.Faction.IsPlayer) return -1;
            return pawn.Faction.loadID;
        }
    }
}
