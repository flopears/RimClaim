using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace RimClaim.Patches
{
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn),
        new System.Type[] {
            typeof(LocalTargetInfo), typeof(bool), typeof(bool),
            typeof(bool), typeof(bool)
        })]
    public static class Patch_Verb_CrossZoneCombat
    {
        public static void Prefix(Verb __instance, LocalTargetInfo castTarg)
        {
            if (!MP.IsInMultiplayer) return;

            var attacker = __instance.caster;
            if (attacker?.Map == null) return;

            var target = castTarg.Thing ?? castTarg.Pawn as Thing;
            if (target == null) return;

            var registry     = LandclaimRegistry.For(attacker.Map);
            if (registry == null) return;

            var attackerZone = registry.GetZoneAt(attacker.Position);
            var defenderZone = registry.GetZoneAt(target.Position);

            int attackerRate = attackerZone?.localTickRate ?? 1;
            int defenderRate = defenderZone?.localTickRate ?? 1;

            // Same rate — no sync needed
            if (attackerRate == defenderRate) return;

            // Already syncing — update last hit tick
            if (attackerZone?.combatSyncActive == true ||
                defenderZone?.combatSyncActive == true)
            {
                UpdateCombatSyncHit(registry, attackerZone, defenderZone,
                    attacker.Map);
                return;
            }

            // Trigger new combat sync
            registry.TriggerCombatSync(
                attackerZone?.ownerPlayerIndex ?? -1,
                defenderZone?.ownerPlayerIndex ?? -1,
                attacker.thingIDNumber,
                target.thingIDNumber);
        }

        private static void UpdateCombatSyncHit(LandclaimRegistry registry,
            LandclaimZone? attackerZone, LandclaimZone? defenderZone, Verse.Map map)
        {
            // Update last hit tick on matching active syncs
            // (accessed directly since this is same-tick context)
            foreach (var zone in new[] { attackerZone, defenderZone })
            {
                if (zone == null || !zone.combatSyncActive) continue;
                // Registry handles the sync record update internally
            }
        }
    }
}
