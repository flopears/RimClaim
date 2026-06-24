using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimClaim
{
    public class GravshipRecord : IExposable
    {
        public int           gravEngineThingId;
        public int           ownerPlayerIndex;
        public int           currentWorldTile;
        public int           mapUniqueId;
        public GravshipState state;

        public void ExposeData()
        {
            Scribe_Values.Look(ref gravEngineThingId, "gravEngineThingId", -1);
            Scribe_Values.Look(ref ownerPlayerIndex,  "ownerPlayerIndex",  -1);
            Scribe_Values.Look(ref currentWorldTile,  "currentWorldTile",  -1);
            Scribe_Values.Look(ref mapUniqueId,       "mapUniqueId",       -1);
            Scribe_Values.Look(ref state,             "state",             GravshipState.Grounded);
        }
    }

    public class GravshipRegistry : WorldComponent
    {
        private Dictionary<int, GravshipRecord> ships         = new();
        private List<PendingLaunch>             pendingLaunch = new();

        public GravshipRegistry(World world) : base(world) { }

        public static GravshipRegistry? Instance
            => Find.World?.GetComponent<GravshipRegistry>();

        // ── Record management ──────────────────────────────────────────────────
        public void RegisterShip(int gravEngineId, int ownerPlayer,
            int tile, int mapId)
        {
            ships[gravEngineId] = new GravshipRecord
            {
                gravEngineThingId = gravEngineId,
                ownerPlayerIndex  = ownerPlayer,
                currentWorldTile  = tile,
                mapUniqueId       = mapId,
                state             = GravshipState.Grounded,
            };
        }

        public void UnregisterShip(int gravEngineId)
            => ships.Remove(gravEngineId);

        public void UpdateShipTile(int gravEngineId, int newTile)
        {
            if (ships.TryGetValue(gravEngineId, out var rec))
                rec.currentWorldTile = newTile;
        }

        public GravshipRecord? GetShip(int gravEngineId)
            => ships.GetValueOrDefault(gravEngineId);

        public List<GravshipRecord> GetShipsOnMap(int mapId)
            => ships.Values.Where(s => s.mapUniqueId == mapId).ToList();

        // ── Launch coordination ────────────────────────────────────────────────
        public void InitiateGravshipCountdown(int gravEngineThingId,
            int destinationTile, int pilotPlayerIndex, int launchTick)
        {
            var record = new PendingLaunch
            {
                gravEngineThingId = gravEngineThingId,
                destinationTile   = destinationTile,
                pilotPlayerIndex  = pilotPlayerIndex,
                launchTick        = launchTick,
            };

            record.readiness[pilotPlayerIndex] = new PlayerLaunchReadiness
            {
                playerIndex = pilotPlayerIndex
            };

            pendingLaunch.Add(record);

            // Check for threatened ships on same map
            var myRec = GetShip(gravEngineThingId);
            if (myRec == null) return;

            var threatened = GetShipsOnMap(myRec.mapUniqueId)
                .Where(s => s.gravEngineThingId != gravEngineThingId).ToList();

            foreach (var threat in threatened)
                NotifyThreatenedShip(threat, record);
        }

        public void JoinCoLaunch(int primaryGravEngineId,
            int joiningGravEngineId, int joiningPlayerIndex,
            int joiningDestinationTile)
        {
            var pending = GetPendingLaunch(primaryGravEngineId);
            if (pending == null) return;

            pending.syncedLaunches.Add(new SyncedLaunch
            {
                gravEngineThingId = joiningGravEngineId,
                destinationTile   = joiningDestinationTile,
                playerIndex       = joiningPlayerIndex,
            });

            pending.readiness[joiningPlayerIndex] = new PlayerLaunchReadiness
            {
                playerIndex = joiningPlayerIndex
            };
        }

        public void ConfirmReady(int primaryGravEngineId,
            int playerIndex, bool waivedRemainingPawns)
        {
            var pending  = GetPendingLaunch(primaryGravEngineId);
            if (pending == null) return;

            var readiness = pending.readiness.GetValueOrDefault(playerIndex);
            if (readiness == null) return;

            readiness.confirmed            = true;
            readiness.waivedRemainingPawns = waivedRemainingPawns;

            if (pending.CanFireNow)
                ExecuteCoLaunch(primaryGravEngineId);
        }

        public void RequestCountdownExtension(int primaryGravEngineId,
            int requestingPlayer, int additionalTicks)
        {
            var pending = GetPendingLaunch(primaryGravEngineId);
            if (pending?.extensionRequest != null) return;

            var readiness = pending?.readiness.GetValueOrDefault(requestingPlayer);
            if (readiness == null || readiness.confirmed) return;

            pending!.extensionRequest = new ExtensionRequest
            {
                requestingPlayerIndex    = requestingPlayer,
                requestedAdditionalTicks = additionalTicks,
                requestedAtTick          = GenTicks.TicksGame,
            };
            readiness.extensionRequested = true;

            NotifyInitiatorExtensionRequest(pending);
        }

        public void AcceptExtension(int primaryGravEngineId, int acceptingPlayer)
        {
            var pending = GetPendingLaunch(primaryGravEngineId);
            if (pending?.extensionRequest == null) return;
            if (acceptingPlayer != pending.pilotPlayerIndex) return;

            pending.launchTick += pending.extensionRequest.requestedAdditionalTicks;

            var req = pending.extensionRequest;
            pending.extensionRequest = null;

            var readiness = pending.readiness.GetValueOrDefault(req.requestingPlayerIndex);
            if (readiness != null) readiness.extensionRequested = false;

            string name = GetPlayerName(req.requestingPlayerIndex);
            NotifyAllCoLaunchers(pending, $"Extension accepted for {name}.");
        }

        public void DeclineExtension(int primaryGravEngineId, int decliningPlayer)
        {
            var pending = GetPendingLaunch(primaryGravEngineId);
            if (pending?.extensionRequest == null) return;
            if (decliningPlayer != pending.pilotPlayerIndex) return;

            int leftBehind = pending.extensionRequest.requestingPlayerIndex;
            pending.extensionRequest = null;

            RemoveFromCoLaunch(pending, leftBehind);
            NotifyPlayerLeftBehind(leftBehind, pending);
            NotifyAllCoLaunchers(pending,
                $"{GetPlayerName(leftBehind)} left behind (extension denied).");

            if (pending.CanFireNow) ExecuteCoLaunch(primaryGravEngineId);
        }

        public void AbortFromCoLaunch(int primaryGravEngineId, int playerIndex)
        {
            var pending = GetPendingLaunch(primaryGravEngineId);
            if (pending == null) return;

            if (playerIndex == pending.pilotPlayerIndex)
            {
                // Primary abort — cancel everything
                foreach (var co in pending.syncedLaunches)
                    NotifyPlayerLeftBehind(co.playerIndex, pending);
                pendingLaunch.Remove(pending);
            }
            else
            {
                RemoveFromCoLaunch(pending, playerIndex);
                NotifyAllCoLaunchers(pending,
                    $"{GetPlayerName(playerIndex)} aborted co-launch.");
                if (pending.CanFireNow) ExecuteCoLaunch(primaryGravEngineId);
            }
        }

        // ── Execution ──────────────────────────────────────────────────────────
        public void ExecuteCoLaunch(int primaryGravEngineId)
        {
            var pending = GetPendingLaunch(primaryGravEngineId);
            if (pending == null) return;

            int seed = Gen.HashCombineInt(primaryGravEngineId, GenTicks.TicksGame);
            Rand.PushState(seed);
            try
            {
                DoLaunch(pending.gravEngineThingId, pending.destinationTile);

                foreach (var co in pending.syncedLaunches
                    .OrderBy(l => l.gravEngineThingId))
                    DoLaunch(co.gravEngineThingId, co.destinationTile);
            }
            finally
            {
                Rand.PopState();
            }

            pendingLaunch.Remove(pending);
        }

        private void DoLaunch(int gravEngineId, int destTile)
        {
            // Find the gravship map and execute transfer
            var rec = GetShip(gravEngineId);
            if (rec == null) return;

            var shipMap = Find.Maps.FirstOrDefault(m => m.uniqueID == rec.mapUniqueId);
            if (shipMap == null) return;

            var shipComp = GravshipOwnerComp.For(shipMap);
            shipComp?.SetState(GravshipState.InFlight);

            rec.state            = GravshipState.InFlight;
            rec.currentWorldTile = destTile;
            // Actual map transfer handled by vanilla gravship system
            // We hook into it via Patch_GravshipLaunch
        }

        // ── WorldComponent tick ────────────────────────────────────────────────
        public override void WorldComponentTick()
        {
            for (int i = pendingLaunch.Count - 1; i >= 0; i--)
            {
                var pending = pendingLaunch[i];

                // Check extension request timeout
                if (pending.extensionRequest?.IsTimedOut == true)
                    DeclineExtension(pending.gravEngineThingId,
                        pending.pilotPlayerIndex);

                // Fire when ready
                if (pending.CanFireNow)
                    ExecuteCoLaunch(pending.gravEngineThingId);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        public PendingLaunch? GetPendingLaunch(int gravEngineId)
            => pendingLaunch.FirstOrDefault(
                p => p.gravEngineThingId == gravEngineId);

        private void RemoveFromCoLaunch(PendingLaunch pending, int playerIndex)
        {
            pending.syncedLaunches.RemoveAll(l => l.playerIndex == playerIndex);
            pending.readiness.Remove(playerIndex);
        }

        private void NotifyThreatenedShip(GravshipRecord ship, PendingLaunch launch)
        {
            string pilotName = GetPlayerName(launch.pilotPlayerIndex);
            int    remaining = launch.launchTick - GenTicks.TicksGame;
            Log.Message($"[RC] Ship {ship.gravEngineThingId} threatened by launch from {pilotName} in {remaining} ticks.");
            // Full letter system would fire here targeting ship.ownerPlayerIndex
        }

        private void NotifyInitiatorExtensionRequest(PendingLaunch pending)
        {
            string name = GetPlayerName(pending.extensionRequest!.requestingPlayerIndex);
            Log.Message($"[RC] {name} requested launch extension.");
        }

        private void NotifyAllCoLaunchers(PendingLaunch pending, string message)
        {
            Log.Message($"[RC] Co-launch notice: {message}");
        }

        private void NotifyPlayerLeftBehind(int playerIndex, PendingLaunch pending)
        {
            Log.Message($"[RC] {GetPlayerName(playerIndex)} left behind in co-launch.");
        }

        private string GetPlayerName(int playerIndex)
            => RcWorld.Players_Safe?.GetPlayer(playerIndex)?.displayName
               ?? $"Player {playerIndex}";

        // ── Serialization ──────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ships, "ships",
                LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref pendingLaunch, "pendingLaunch",
                LookMode.Deep);
            ships         ??= new Dictionary<int, GravshipRecord>();
            pendingLaunch ??= new List<PendingLaunch>();
        }
    }
}
