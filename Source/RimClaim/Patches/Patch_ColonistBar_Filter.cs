using Verse;

namespace RimClaim.Patches
{
    // ════════════════════════════════════════════════════════════════════════
    // DISABLED pending correct 1.6 class names.
    //
    // The colonist-bar filtering (hide teammates' pawns per sharePawnBar
    // permission) targeted ColonistBarColonistGetter / ColonistBarDrawer,
    // which don't resolve in 1.6. This is a visibility-only convenience and
    // does not affect core territory/ownership function.
    //
    // To reinstate: find the correct 1.6 type that builds the colonist bar
    // entry list (likely RimWorld.ColonistBar or a nested helper) and patch
    // its ordering/entry method.
    // ════════════════════════════════════════════════════════════════════════
}
