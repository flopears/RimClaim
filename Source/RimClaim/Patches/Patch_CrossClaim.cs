using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimClaim.Patches
{
    // ── Wanderer Join Round-Robin ──────────────────────────────────────────────
    [HarmonyPatch(typeof(IncidentWorker_WandererJoin), "TryExecuteWorker")]
    public static class Patch_WandererJoin_RoundRobin
    {
        public static bool Prefix(IncidentParms parms)
        {
            if (!MP.IsInMultiplayer) return true;

            var router = (parms.target as Map)?.GetComponent<EventRouter>();
            if (router == null) return true;

            int targetPlayer = router.GetNextEventTarget();
            var faction      = RcWorld.Players_Safe?.GetFaction(targetPlayer);
            if (faction != null) parms.faction = faction;

            return true;
        }
    }

    // ── Prisoner Escape Claim Slow ─────────────────────────────────────────────
    [HarmonyPatch(typeof(JobDriver_Flee), "TryMakePreToilReservations")]
    public static class Patch_PrisonerEscape_ClaimSlow
    {
        public static void Postfix(JobDriver_Flee __instance)
        {
            if (!MP.IsInMultiplayer) return;

            var pawn = __instance.pawn;
            if (pawn == null || !pawn.IsPrisoner) return;

            var registry = LandclaimRegistry.For(pawn.Map);
            if (registry == null) return;

            var zone = GetEscapeResponseZone(pawn, registry);
            if (zone == null) return;

            registry.TriggerEscapeSync(zone.ownerPlayerIndex, pawn.thingIDNumber);
        }

        private static LandclaimZone? GetEscapeResponseZone(Pawn escapee,
            LandclaimRegistry registry)
        {
            var spatial = registry.GetZoneAt(escapee.Position);
            if (spatial != null) return spatial;

            // Fallback to nearest claim of whoever the escapee belongs to
            int ownerPlayer = PawnOwnershipHelper.GetPawnPlayerIndex(escapee);
            return registry.GetZoneByOwner(ownerPlayer);
        }
    }

    // ── Draft State → Exit Job Check ───────────────────────────────────────────
    [HarmonyPatch(typeof(Pawn_DraftController),
        nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    public static class Patch_Draft_ExitJobCheck
    {
        public static void Postfix(Pawn_DraftController __instance, bool value)
        {
            if (!MP.IsInMultiplayer || value) return; // being drafted — skip

            var pawn     = __instance.pawn;
            var registry = LandclaimRegistry.For(pawn.Map);
            if (registry == null) return;

            var currentZone = registry.GetZoneAt(pawn.Position);
            if (currentZone == null) return;

            var diplo = RcWorld.Diplomacy_Safe;
            if (diplo == null) return;

            int pawnOwner = PawnOwnershipHelper.GetPawnPlayerIndex(pawn);
            if (pawnOwner < 0) return;

            if (diplo.AreEnemies(pawnOwner, currentZone.ownerPlayerIndex))
                AssignForceExitJob(pawn, currentZone, pawn.Map);
        }

        public static void AssignForceExitJob(Pawn pawn,
            LandclaimZone hostileZone, Map map)
        {
            var exitCell = FindValidExitCell(pawn, hostileZone, map);

            if (exitCell.IsValid)
            {
                var job = JobMaker.MakeJob(JobDefOf.Goto, exitCell);
                job.exitMapOnArrival = true;
                pawn.jobs.StopAll();
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
            else
            {
                Log.Message($"[RC] {pawn.LabelShort} trapped in hostile claim — no exit.");
            }
        }

        private static IntVec3 FindValidExitCell(Pawn pawn,
            LandclaimZone hostileZone, Map map)
        {
            // Find nearest map edge cell not requiring hostile doors
            foreach (var cell in CellRect.WholeMap(map).EdgeCells)
            {
                if (hostileZone.bounds.Contains(cell)) continue;
                if (!pawn.CanReach(cell, PathEndMode.OnCell,
                    Danger.Deadly)) continue;
                return cell;
            }
            return IntVec3.Invalid;
        }
    }

    // ── Ceremony Attack Tracking ───────────────────────────────────────────────
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage),
        new System.Type[] { typeof(DamageInfo) })]
    public static class Patch_Pawn_TrackCeremonyAttacker
    {
        public static void Postfix(Thing __instance, DamageInfo dinfo)
        {
            if (!MP.IsInMultiplayer) return;
            if (__instance is not Pawn pawn) return;

            var ceremony = pawn.Map?
                .GetComponent<CeremonyTracker>()?.ActiveCeremony;
            if (ceremony == null) return;
            if (!ceremony.IsParticipant(pawn)) return;

            var attacker = dinfo.Instigator as Pawn;
            if (attacker == null) return;

            int attackerPlayer = PawnOwnershipHelper.GetPawnPlayerIndex(attacker);
            if (attackerPlayer < 0 || attackerPlayer == ceremony.ownerPlayerIndex)
                return;

            pawn.Map!.GetComponent<CeremonyTracker>()!
                .RecordAttacker(attackerPlayer);
        }
    }

    // ── Draft During Ceremony Warning ──────────────────────────────────────────
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_Draft_CeremonyWarning
    {
        public static bool Prefix(Pawn_JobTracker __instance, Job job)
        {
            if (!MP.IsInMultiplayer) return true;
            if (job.def != JobDefOf.AttackMelee &&
                job.def != JobDefOf.AttackStatic) return true;

            // Pawn_JobTracker.pawn is protected — read it via Traverse
            var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null) return true;

            var ceremony = pawn.Map?
                .GetComponent<CeremonyTracker>()?.ActiveCeremony;
            if (ceremony == null) return true;
            if (!ceremony.IsParticipant(pawn)) return true;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RC_AttackDuringCeremonyDesc".Translate(),
                () => { /* player confirmed — no-op, vanilla continues next time */ }));

            return false; // block until confirmed
        }
    }

    // ── Pathfinder Enemy Claim Block ───────────────────────────────────────────
    // REMOVED: PathFinder.FindPath has an unstable signature across versions and
    // isn't cleanly patchable (pawn is not a direct parameter). Enemy access is
    // already restricted by the door-ownership patch. A pathfinder-level block
    // can be reimplemented later via a cost-offset on enemy claim cells if needed.
}
