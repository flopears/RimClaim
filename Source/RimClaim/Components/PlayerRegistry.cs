using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// GameComponent — one per save. Ground truth for all connected/historical players.
    ///
    /// Access: Find.GameComponent&lt;PlayerRegistry&gt;()
    ///         or the static RcWorld.Players shortcut.
    /// </summary>
    public class PlayerRegistry : GameComponent
    {
        // ── State ──────────────────────────────────────────────────────────────
        private List<PlayerData> players = new();

        /// <summary>
        /// Monotonically-increasing counter used to hand out stable playerIndex values.
        /// Never decremented even if a player leaves.
        /// </summary>
        private int nextPlayerIndex = 0;

        // ── Pre-defined player colors (cycled for new players) ─────────────────
        private static readonly Color[] PaletteColors =
        {
            new Color(0.33f, 0.66f, 1.00f), // blue
            new Color(0.90f, 0.35f, 0.35f), // red
            new Color(0.40f, 0.85f, 0.40f), // green
            new Color(1.00f, 0.80f, 0.20f), // yellow
            new Color(0.85f, 0.40f, 0.85f), // purple
            new Color(0.25f, 0.85f, 0.85f), // cyan
            new Color(1.00f, 0.55f, 0.10f), // orange
            new Color(0.90f, 0.90f, 0.90f), // white
        };

        // ── Constructor ────────────────────────────────────────────────────────
        public PlayerRegistry(Game game) { }

        // ── Public read API ────────────────────────────────────────────────────
        public IReadOnlyList<PlayerData> AllPlayers => players;

        public PlayerData? GetPlayer(int playerIndex)
            => players.FirstOrDefault(p => p.playerIndex == playerIndex);

        public PlayerData? GetLocalPlayer()
        {
            if (!MP.IsInMultiplayer) return null;
            return GetPlayer(RcLocal.PlayerIndex);
        }

        public bool TryGetPlayer(int playerIndex, out PlayerData data)
        {
            data = GetPlayer(playerIndex)!;
            return data != null;
        }

        /// <summary>Returns all currently-connected players.</summary>
        public List<PlayerData> ConnectedPlayers()
            => players.Where(p => p.isConnected).ToList();

        /// <summary>Returns the faction for a given playerIndex, or null.</summary>
        public Faction? GetFaction(int playerIndex)
            => GetPlayer(playerIndex)?.GetFaction();

        // ── Synced mutations ───────────────────────────────────────────────────

        /// <summary>
        /// Called when a client joins. Creates or reactivates their PlayerData.
        /// Must be called from host-side connection logic via SyncMethod so all
        /// clients update simultaneously.
        /// </summary>
        public void RegisterPlayer(int mpPlayerIndex, string displayName, int factionLoadId)
        {
            // Check if this player has connected before (reconnect scenario)
            var existing = GetPlayer(mpPlayerIndex);
            if (existing != null)
            {
                existing.isConnected   = true;
                existing.displayName   = displayName;
                existing.factionLoadId = factionLoadId;
                existing.lastSeenTick  = GenTicks.TicksGame;
                return;
            }

            // New player — assign the next stable index
            // (mpPlayerIndex IS the stable index from Zetrith's system)
            var data = new PlayerData
            {
                playerIndex   = mpPlayerIndex,
                displayName   = displayName,
                factionLoadId = factionLoadId,
                isConnected   = true,
                lastSeenTick  = GenTicks.TicksGame,
                playerColor   = PaletteColors[players.Count % PaletteColors.Length],
            };
            players.Add(data);

            if (mpPlayerIndex >= nextPlayerIndex)
                nextPlayerIndex = mpPlayerIndex + 1;

            Log.Message($"[RimClaim] Registered player {displayName} " +
                        $"(index={mpPlayerIndex}, faction={factionLoadId})");
        }

        /// <summary>Called when a client disconnects.</summary>
        public void MarkPlayerDisconnected(int playerIndex)
        {
            var p = GetPlayer(playerIndex);
            if (p == null) return;
            p.isConnected = false;
            Log.Message($"[RimClaim] Player {p.displayName} disconnected.");
        }

        /// <summary>Change a player's display color. Cosmetic — still synced for consistency.</summary>
        public void SetPlayerColor(int playerIndex, float r, float g, float b)
        {
            var p = GetPlayer(playerIndex);
            if (p == null) return;
            p.playerColor = new Color(r, g, b, 1f);
        }

        /// <summary>
        /// Decrements or resets an emergency pause. Called from SpeedNegotiator.
        /// </summary>
        public void ConsumeEmergencyPause(int playerIndex)
        {
            var p = GetPlayer(playerIndex);
            if (p == null) return;

            int today = GenDate.DaysPassed;
            if (p.emergencyPauseResetDay != today)
            {
                p.emergencyPausesToday   = 0;
                p.emergencyPauseResetDay = today;
            }
            p.emergencyPausesToday++;
        }

        public int EmergencyPausesRemaining(int playerIndex)
        {
            var p = GetPlayer(playerIndex);
            if (p == null) return 0;
            int today = GenDate.DaysPassed;
            if (p.emergencyPauseResetDay != today) return Constants.MaxEmergencyPausesPerDay;
            return Mathf.Max(0, Constants.MaxEmergencyPausesPerDay - p.emergencyPausesToday);
        }

        // ── Tick (daily reset handled here) ───────────────────────────────────
        public override void GameComponentTick()
        {
            // Runs every tick — keep it trivially cheap.
            // Daily reset of emergency pause counters is done lazily in
            // EmergencyPausesRemaining / ConsumeEmergencyPause — no tick work needed.
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref players, "players", LookMode.Deep);
            Scribe_Values.Look(ref nextPlayerIndex, "nextPlayerIndex", 0);

            // Guard against null list after deserialization
            players ??= new List<PlayerData>();
        }
    }
}
