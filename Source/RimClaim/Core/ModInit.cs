using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace RimClaim
{
    /// <summary>
    /// Key distinction:
    ///   MP.enabled         = the Multiplayer mod DLL is loaded (always true if mod is installed)
    ///   MP.IsInMultiplayer = we are currently IN an active multiplayer session
    ///
    /// MP.RegisterAll() must be called with MP.enabled (DLL present).
    /// All gameplay guards must check MP.IsInMultiplayer (in a session).
    /// RcLocal.PlayerIndex is only valid when MP.IsInMultiplayer is true.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModInit
    {
        public const string MOD_ID     = "rc.rimclaim";
        public const string HARMONY_ID = "com.rimclaim.core";

        static ModInit()
        {
            var harmony = new Harmony(HARMONY_ID);

            // Patch each class individually so one bad patch (e.g. an ambiguous
            // method match) logs a warning instead of taking down the whole mod.
            int ok = 0, failed = 0;
            foreach (var type in typeof(ModInit).Assembly.GetTypes())
            {
                try
                {
                    var attrs = type.GetCustomAttributes(
                        typeof(HarmonyPatch), inherit: true);
                    if (attrs.Length == 0) continue;

                    new PatchClassProcessor(harmony, type).Patch();
                    ok++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    Log.Warning($"[RimClaim] Skipped patch {type.Name}: {e.Message}");
                }
            }
            Log.Message($"[RimClaim] Harmony patches applied: {ok} ok, {failed} skipped.");

            if (MP.enabled)
            {
                MP.RegisterAll();
                SyncWorkerRegistration.RegisterAll();
                Log.Message("[RimClaim] Multiplayer API registered (session not yet active).");
            }

            Log.Message("[RimClaim] Loaded. Players tab visible in Architect > Misc.");
        }
    }

    /// <summary>
    /// Polls until an active multiplayer session exists, then does first-time setup.
    /// Handles the common workflow: start colony → then host via Zetrith.
    /// </summary>
    public class RcStartupComponent : GameComponent
    {
        private bool setupComplete = false;
        private int  pollCounter   = 0;

        public RcStartupComponent(Game game) { }

        public override void GameComponentTick()
        {
            if (setupComplete) return;
            if (++pollCounter % 60 != 0) return; // once per second

            int localIndex = RcLocal.PlayerIndex;
            if (localIndex < 0)
            {
                // Faction not ready yet — try again next poll
                return;
            }

            Log.Message($"[RimClaim] Active MP session. Local player index (faction loadID)={localIndex}");

            var registry = Current.Game.GetComponent<PlayerRegistry>();
            if (registry == null)
            {
                Log.Warning("[RimClaim] PlayerRegistry GameComponent not found.");
                return;
            }

            // Register local player keyed by faction loadID
            if (registry.GetPlayer(localIndex) == null)
            {
                var faction = Faction.OfPlayer;
                if (faction == null) return;

                registry.RegisterPlayer(
                    localIndex,
                    RcLocal.PlayerName,
                    faction.loadID);
            }

            DoFirstTimeSetup(registry);
            setupComplete = true;
        }

        private void DoFirstTimeSetup(PlayerRegistry registry)
        {
            // Auto-complete BasicTerritory research
            var research = DefDatabase<ResearchProjectDef>
                .GetNamedSilentFail("RC_BasicTerritory");
            if (research != null && !research.IsFinished)
            {
                Find.ResearchManager.FinishProject(research, doCompletionDialog: false);
                Log.Message("[RimClaim] RC_BasicTerritory auto-researched.");
            }

            GiveStartingClaimPost();
            Log.Message("[RimClaim] Setup complete.");
        }

        private void GiveStartingClaimPost()
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RC_ClaimPost");
            if (def == null)
            {
                Log.Warning("[RimClaim] RC_ClaimPost def not found — is the DLL built?");
                return;
            }

            // Give to local player's first colonist only
            Pawn? pawn = null;
            foreach (var map in Find.Maps)
            {
                pawn = map.mapPawns.FreeColonistsSpawned
                    .FirstOrDefault(p => p.Faction == Faction.OfPlayer);
                if (pawn != null) break;
            }

            if (pawn == null)
            {
                Log.Warning("[RimClaim] No colonist found to give claim post to.");
                return;
            }

            if (pawn.inventory.innerContainer.Any(t => t.def == def))
            {
                Log.Message("[RimClaim] Player already has a claim post.");
                return;
            }

            var post = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            pawn.inventory.innerContainer.TryAdd(post);

            Messages.Message(
                $"[RimClaim] {pawn.LabelShort} received a claim post — place it to stake your territory.",
                pawn, MessageTypeDefOf.PositiveEvent, false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref setupComplete, "rcSetupComplete", false);
        }
    }
}
