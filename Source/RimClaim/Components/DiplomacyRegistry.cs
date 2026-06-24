using System.Collections.Generic;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    public enum DiplomacyState
    {
        Neutral = 0,
        Enemy   = 1,
    }

    /// <summary>
    /// GameComponent — tracks pairwise player relations (Neutral vs Enemy).
    /// Relations are one-sided: Player A can declare B an enemy without B's consent.
    /// PVP activates when EITHER player has declared the other an enemy.
    ///
    /// Access: Find.GameComponent&lt;DiplomacyRegistry&gt;()
    ///         or RcWorld.Diplomacy shortcut.
    /// </summary>
    public class DiplomacyRegistry : GameComponent
    {
        // Stored as flat parallel lists for Scribe compatibility
        // Key encoding: "A:B" where A < B (canonical order)
        private List<string> relationKeys   = new();
        private List<int>    relationValues = new(); // DiplomacyState as int

        public DiplomacyRegistry(Game game) { }

        // ── Public read API ────────────────────────────────────────────────────

        public DiplomacyState GetRelation(int a, int b)
        {
            string key = MakeKey(a, b);
            int idx = relationKeys.IndexOf(key);
            return idx >= 0 ? (DiplomacyState)relationValues[idx] : DiplomacyState.Neutral;
        }

        /// <summary>
        /// True if EITHER player has declared the other an enemy.
        /// This is the condition that activates PVP.
        /// </summary>
        public bool AreEnemies(int a, int b)
            => GetRelation(a, b) == DiplomacyState.Enemy ||
               GetRelation(b, a) == DiplomacyState.Enemy;

        // ── Synced mutations ───────────────────────────────────────────────────

        /// <summary>
        /// Sets Player A's stance toward Player B.
        /// Also updates actual RimWorld faction relations so pawns can fight.
        /// </summary>
        public void SetRelation(int fromPlayer, int toPlayer, DiplomacyState state)
        {
            if (fromPlayer == toPlayer) return;

            SetRelationInternal(fromPlayer, toPlayer, state);
            ApplyFactionRelation(fromPlayer, toPlayer);
        }

        public void DeclarePeace(int fromPlayer, int toPlayer)
        {
            // One-sided peace — only resets THIS player's stance
            // Both sides must declare peace to fully end PVP
            SetRelationInternal(fromPlayer, toPlayer, DiplomacyState.Neutral);
            ApplyFactionRelation(fromPlayer, toPlayer);
        }

        // ── Internal helpers ───────────────────────────────────────────────────

        private void SetRelationInternal(int fromPlayer, int toPlayer, DiplomacyState state)
        {
            // Store as directed (A→B), not canonical-keyed, so one-sided stances are possible
            string key = $"{fromPlayer}>{toPlayer}";
            int idx = relationKeys.IndexOf(key);
            if (idx >= 0)
            {
                relationValues[idx] = (int)state;
            }
            else
            {
                relationKeys.Add(key);
                relationValues.Add((int)state);
            }
        }

        private DiplomacyState GetRelationDirected(int from, int to)
        {
            string key = $"{from}>{to}";
            int idx = relationKeys.IndexOf(key);
            return idx >= 0 ? (DiplomacyState)relationValues[idx] : DiplomacyState.Neutral;
        }

        private void ApplyFactionRelation(int playerA, int playerB)
        {
            var registry = Current.Game.GetComponent<PlayerRegistry>();
            var factionA = registry?.GetFaction(playerA);
            var factionB = registry?.GetFaction(playerB);
            if (factionA == null || factionB == null) return;

            bool hostile = AreEnemies(playerA, playerB);
            var kind = hostile ? FactionRelationKind.Hostile : FactionRelationKind.Neutral;

            // SetRelationDirect avoids generating news events or letters
            factionA.SetRelationDirect(factionB, kind);
            factionB.SetRelationDirect(factionA, kind);
        }

        // ── Utility ────────────────────────────────────────────────────────────

        // Canonical key — A < B — used only for symmetric read
        private static string MakeKey(int a, int b)
            => a < b ? $"{a}:{b}" : $"{b}:{a}";

        // ── Diplomatic history ─────────────────────────────────────────────────

        public void RecordEvent(int fromPlayer, int toPlayer,
            DiplomacyEventType type)
        {
            Log.Message($"[RC] Diplomatic event: Player {fromPlayer} → Player {toPlayer}: {type}");
            // Full history log would be stored and displayed in Players tab
        }

        public void RecordCeremonyInterruption(int attackerPlayer,
            int ceremonyOwner)
        {
            RecordEvent(attackerPlayer, ceremonyOwner,
                DiplomacyEventType.CeremonyInterrupted);
        }

        // ── Serialization ──────────────────────────────────────────────────────

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref relationKeys,   "rKeys",   LookMode.Value);
            Scribe_Collections.Look(ref relationValues, "rValues", LookMode.Value);
            relationKeys   ??= new List<string>();
            relationValues ??= new List<int>();
        }
}
}
