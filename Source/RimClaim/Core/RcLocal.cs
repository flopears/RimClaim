using System.Collections.Generic;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// The Multiplayer API does NOT expose a local player index.
    /// We use the player's FACTION as the stable identity anchor.
    ///
    /// The local player index = Faction.OfPlayer.loadID. This is stable across
    /// the session, unique per player colony, and available immediately without
    /// any registration step (no circular dependency).
    ///
    /// Returns -1 only if not in multiplayer or no player faction exists yet.
    /// </summary>
    public static class RcLocal
    {
        public static int PlayerIndex
        {
            get
            {
                if (!MP.IsInMultiplayer) return -1;
                var faction = Faction.OfPlayer;
                return faction?.loadID ?? -1;
            }
        }

        public static string PlayerName => MP.IsInMultiplayer ? (MP.PlayerName ?? "Player") : "Player";

        /// <summary>
        /// The player index for a specific faction (used when resolving
        /// ownership of pawns/buildings belonging to other players).
        /// </summary>
        public static int IndexForFaction(Faction faction)
            => faction?.loadID ?? -1;
    }

    /// <summary>
    /// net472 / older C# in RimWorld lacks Dictionary.GetValueOrDefault.
    /// Provide it as an extension so existing call sites compile.
    /// </summary>
    public static class DictionaryExtensions
    {
        public static TValue? GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dict, TKey key)
            where TValue : class
        {
            return dict.TryGetValue(key, out var value) ? value : null;
        }
    }
}
