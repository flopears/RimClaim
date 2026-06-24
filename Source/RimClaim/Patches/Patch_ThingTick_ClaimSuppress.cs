using Verse;

namespace RimClaim.Patches
{
    // ════════════════════════════════════════════════════════════════════════
    // DISABLED — Per-claim tick multiplication via Thing.Tick patching is a
    // catastrophic performance problem (Thing.Tick runs millions of times/sec).
    //
    // The per-claim speed feature needs to be reimplemented using Zetrith's
    // async-time map registration, NOT a Harmony patch on Thing.Tick.
    //
    // The ThreadStatic flag is kept so other code referencing it still compiles.
    // No Harmony attributes here means nothing is patched — zero overhead.
    // ════════════════════════════════════════════════════════════════════════
    public static class ClaimTickContext
    {
        [System.ThreadStatic]
        public static bool InClaimTick = false;
    }
}
