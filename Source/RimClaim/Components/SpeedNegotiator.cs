using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public enum PauseRequestState { None, Pending, Paused }

    /// <summary>
    /// Tracks a soft pause request — one player asks to pause, others have a window to object.
    /// </summary>
    public class PauseRequest : IExposable
    {
        public int requestingPlayer = -1;
        public int requestedAtTick  = 0;
        public List<int> objectors  = new();

        public bool IsExpired =>
            GenTicks.TicksGame - requestedAtTick > Constants.SoftPauseTimeoutTicks;

        public void ExposeData()
        {
            Scribe_Values.Look(ref requestingPlayer, "requestingPlayer", -1);
            Scribe_Values.Look(ref requestedAtTick,  "requestedAtTick",  0);
            Scribe_Collections.Look(ref objectors, "objectors", LookMode.Value);
            objectors ??= new List<int>();
        }
    }

    /// <summary>
    /// GameComponent — manages the three-tier pause system.
    ///
    /// Tier 1: Speed negotiation (always active — game runs at slowest requested speed).
    /// Tier 2: Soft pause request (vote with auto-allow timeout).
    /// Tier 3: Emergency pause (rationed 3/day, instant).
    ///
    /// Access: Find.GameComponent&lt;SpeedNegotiator&gt;()
    /// </summary>
    public class SpeedNegotiator : GameComponent
    {
        // ── Tier 1: Speed negotiation ──────────────────────────────────────────
        private Dictionary<int, int> requestedSpeeds = new(); // playerIndex → TimeSpeed (int)

        // ── Tier 2: Soft pause ─────────────────────────────────────────────────
        private PauseRequest? pendingRequest = null;

        // ── State ──────────────────────────────────────────────────────────────
        private bool isPaused = false;

        public bool IsPaused => isPaused;
        public PauseRequest? PendingRequest => pendingRequest;

        public SpeedNegotiator(Game game) { }

        // ── Tier 1: Speed ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the negotiated game speed: minimum of all players' requested speeds.
        /// </summary>
        public int NegotiatedSpeed
        {
            get
            {
                if (!requestedSpeeds.Any()) return 1;
                return requestedSpeeds.Values.Min();
            }
        }

        [SyncMethod]
        public void SetRequestedSpeed(int playerIndex, int speed)
        {
            speed = Mathf.Clamp(speed, 1, 4);
            requestedSpeeds[playerIndex] = speed;
            ApplyNegotiatedSpeed();
        }

        private void ApplyNegotiatedSpeed()
        {
            if (isPaused) return;
            var ts = (TimeSpeed)Mathf.Clamp(NegotiatedSpeed, 1, 4);
            if (Find.TickManager.CurTimeSpeed != ts)
                Find.TickManager.CurTimeSpeed = ts;
        }

        // ── Tier 2: Soft pause request ─────────────────────────────────────────

        [SyncMethod]
        public void RequestSoftPause(int playerIndex)
        {
            // Only one pending request at a time
            if (pendingRequest != null) return;

            // PVP check: if the requesting player is in PVP with anyone, disallow
            var diplo = Current.Game.GetComponent<DiplomacyRegistry>();
            if (diplo != null)
            {
                var registry = Current.Game.GetComponent<PlayerRegistry>();
                foreach (var other in registry?.ConnectedPlayers() ?? new List<PlayerData>())
                {
                    if (other.playerIndex != playerIndex && diplo.AreEnemies(playerIndex, other.playerIndex))
                    {
                        Messages.Message("RC_CantPauseDuringPVP".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                }
            }

            pendingRequest = new PauseRequest
            {
                requestingPlayer = playerIndex,
                requestedAtTick  = GenTicks.TicksGame,
            };
        }

        [SyncMethod]
        public void ObjectToPause(int playerIndex)
        {
            if (pendingRequest == null) return;
            if (!pendingRequest.objectors.Contains(playerIndex))
                pendingRequest.objectors.Add(playerIndex);

            // Cancel the request — one objection is enough
            pendingRequest = null;
        }

        [SyncMethod]
        public void AcceptSoftPause()
        {
            pendingRequest = null;
            ExecutePause();
        }

        // ── Tier 3: Emergency pause ────────────────────────────────────────────

        [SyncMethod]
        public void RequestEmergencyPause(int playerIndex)
        {
            var pr = Current.Game.GetComponent<PlayerRegistry>();
            if (pr == null) return;

            // PVP check
            var diplo = Current.Game.GetComponent<DiplomacyRegistry>();
            if (diplo != null)
            {
                foreach (var other in pr.ConnectedPlayers())
                {
                    if (other.playerIndex != playerIndex && diplo.AreEnemies(playerIndex, other.playerIndex))
                    {
                        Messages.Message("RC_CantPauseDuringPVP".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                }
            }

            if (pr.EmergencyPausesRemaining(playerIndex) <= 0)
            {
                Messages.Message("RC_NoEmergencyPausesLeft".Translate(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            pr.ConsumeEmergencyPause(playerIndex);

            var player = pr.GetPlayer(playerIndex);
            Messages.Message(
                "RC_EmergencyPause".Translate(player?.displayName ?? "Player"),
                MessageTypeDefOf.CautionInput, false);

            ExecutePause();
        }

        // ── Unpause ────────────────────────────────────────────────────────────

        [SyncMethod]
        public void Unpause(int playerIndex)
        {
            if (!isPaused) return;
            isPaused = false;

            foreach (var map in Find.Maps)
            {
                var reg = LandclaimRegistry.For(map);
                if (reg == null) continue;
                foreach (var zone in reg.AllZones)
                {
                    if (zone.globalPauseLockActive)
                    {
                        zone.localTickRate = zone.preGlobalPauseRate;
                        zone.globalPauseLockActive = false;
                    }
                }
            }

            ApplyNegotiatedSpeed();
        }

        // ── Internal pause execution ───────────────────────────────────────────

        private void ExecutePause()
        {
            isPaused = true;

            foreach (var map in Find.Maps)
            {
                var reg = LandclaimRegistry.For(map);
                if (reg == null) continue;
                foreach (var zone in reg.AllZones)
                {
                    if (!zone.globalPauseLockActive)
                    {
                        zone.preGlobalPauseRate = zone.localTickRate;
                        zone.globalPauseLockActive = true;
                    }
                    zone.localTickRate = 0;
                }
                reg.NotifyPauseChanged();
            }
        }

        // ── Tick: process soft-pause timeout ──────────────────────────────────

        private int tickCounter = 0;
        public override void GameComponentTick()
        {
            // Check every 30 ticks (~0.5s) to keep it cheap
            if (++tickCounter % 30 != 0) return;

            if (pendingRequest != null && pendingRequest.IsExpired)
            {
                // Timeout reached with no objections → auto-pause
                AcceptSoftPause();
            }
        }

        // ── Serialization ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref requestedSpeeds, "requestedSpeeds",
                LookMode.Value, LookMode.Value);
            Scribe_Deep.Look(ref pendingRequest, "pendingRequest");
            Scribe_Values.Look(ref isPaused, "isPaused", false);
            requestedSpeeds ??= new Dictionary<int, int>();
        }
    }
}
