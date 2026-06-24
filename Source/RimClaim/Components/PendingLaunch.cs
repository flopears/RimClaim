using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    public class SyncedLaunch : IExposable
    {
        public int gravEngineThingId;
        public int destinationTile;
        public int playerIndex;

        public void ExposeData()
        {
            Scribe_Values.Look(ref gravEngineThingId, "gravEngineThingId", -1);
            Scribe_Values.Look(ref destinationTile,   "destinationTile",   -1);
            Scribe_Values.Look(ref playerIndex,       "playerIndex",       -1);
        }
    }

    public class PlayerLaunchReadiness : IExposable
    {
        public int        playerIndex;
        public bool       confirmed            = false;
        public bool       waivedRemainingPawns = false;
        public bool       extensionRequested   = false;
        public List<int>  pawnsAboard          = new();

        public void ExposeData()
        {
            Scribe_Values.Look(ref playerIndex,          "playerIndex",          -1);
            Scribe_Values.Look(ref confirmed,            "confirmed",            false);
            Scribe_Values.Look(ref waivedRemainingPawns, "waivedRemainingPawns", false);
            Scribe_Values.Look(ref extensionRequested,   "extensionRequested",   false);
            Scribe_Collections.Look(ref pawnsAboard, "pawnsAboard", LookMode.Value);
            pawnsAboard ??= new List<int>();
        }
    }

    public class ExtensionRequest : IExposable
    {
        public int requestingPlayerIndex;
        public int requestedAdditionalTicks;
        public int requestedAtTick;

        public bool IsTimedOut =>
            GenTicks.TicksGame - requestedAtTick >
            Constants.ExtensionRequestTimeoutTicks;

        public void ExposeData()
        {
            Scribe_Values.Look(ref requestingPlayerIndex,    "requestingPlayer",  -1);
            Scribe_Values.Look(ref requestedAdditionalTicks, "additionalTicks",   0);
            Scribe_Values.Look(ref requestedAtTick,          "requestedAtTick",   0);
        }
    }

    public class PendingLaunch : IExposable
    {
        public int    gravEngineThingId;
        public int    destinationTile;
        public int    pilotPlayerIndex;
        public int    launchTick;
        public bool   isEmergency;

        public List<SyncedLaunch>                          syncedLaunches = new();
        public Dictionary<int, PlayerLaunchReadiness>      readiness      = new();
        public ExtensionRequest?                           extensionRequest;

        public bool AllPlayersReady
            => readiness.Values.All(r => r.confirmed);

        public bool HasExceededMaxWait
            => GenTicks.TicksGame > launchTick + Constants.CoLaunchMaxWaitTicks;

        public bool CanFireNow
            => GenTicks.TicksGame >= launchTick &&
               (AllPlayersReady || HasExceededMaxWait);

        public void ExposeData()
        {
            Scribe_Values.Look(ref gravEngineThingId, "gravEngineThingId", -1);
            Scribe_Values.Look(ref destinationTile,   "destinationTile",   -1);
            Scribe_Values.Look(ref pilotPlayerIndex,  "pilotPlayerIndex",  -1);
            Scribe_Values.Look(ref launchTick,        "launchTick",        0);
            Scribe_Values.Look(ref isEmergency,       "isEmergency",       false);
            Scribe_Collections.Look(ref syncedLaunches, "syncedLaunches",  LookMode.Deep);
            Scribe_Collections.Look(ref readiness,    "readiness",
                LookMode.Value, LookMode.Deep);
            Scribe_Deep.Look(ref extensionRequest, "extensionRequest");
            syncedLaunches ??= new List<SyncedLaunch>();
            readiness      ??= new Dictionary<int, PlayerLaunchReadiness>();
        }
    }
}
