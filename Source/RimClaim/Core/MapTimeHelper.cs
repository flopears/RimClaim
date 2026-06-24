using Multiplayer.API;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// Safe wrapper for map tick access.
    /// Uses Zetrith's per-map async tick when MP is active,
    /// falls back to global TicksGame in singleplayer.
    /// </summary>
    public static class MapTimeHelper
    {
        public static int MapTicks(this Map map)
        {
            if (MP.enabled)
            {
                try { return map.MapTicks(); }
                catch { /* fallthrough */ }
            }
            return Find.TickManager?.TicksGame ?? 0;
        }
    }
}
