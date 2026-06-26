using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public class Building_LandclaimBlock : Building
    {
        private bool graceperiodActive = true;
        private int  placedAtTick      = -1;
        private bool zoneInitialized   = false;

        // ── Spawn ──────────────────────────────────────────────────────────────
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            try
            {
                base.SpawnSetup(map, respawningAfterLoad);

                if (!respawningAfterLoad && !zoneInitialized)
                {
                    placedAtTick = GenTicks.TicksGame;

                    if (MP.IsInMultiplayer)
                    {
                        int ownerIdx = this.Faction?.loadID ?? -1;
                        if (ownerIdx >= 0)
                        {
                            zoneInitialized = true;
                            DoRegisterZone(ownerIdx);
                        }
                    }
                    else
                    {
                        zoneInitialized = true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RimClaim] SpawnSetup CRASHED: {e}");
            }
        }

        public override void PostMake()
        {
            try
            {
                base.PostMake();
                graceperiodActive = true;
            }
            catch (System.Exception e)
            {
                Log.Error($"[RimClaim] PostMake CRASHED: {e}");
            }
        }

        // ── Tick ────────────────────────────────────────────────────────────────
        // Buildings without tick-requiring comps may NOT get Tick() called.
        // All init logic lives in TickRare instead.
        protected override void Tick()
        {
            try { base.Tick(); }
            catch (System.Exception e) { Log.Error($"[RimClaim] base.Tick(): {e}"); }
        }

        // NOT a SyncMethod — called from tick context, must not be synced.
        private void DoRegisterZone(int playerIndex)
        {
            var registry = LandclaimRegistry.For(Map);
            if (registry == null)
            {
                Log.Warning("[RimClaim] LandclaimRegistry not found on map.");
                return;
            }

            bool isBeacon = def.defName == "RC_LandclaimBlock";
            int radius = isBeacon
                ? (RcMod.Settings?.DefaultClaimRadius ?? Constants.DefaultClaimRadius)
                : Constants.DefaultPostRadius;
            var proposed = CellRect.CenteredOn(Position, radius);

            if (registry.OverlapsExistingClaim(proposed, playerIndex))
            {
                Messages.Message("RC_ClaimOverlap".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            int existing = registry.AllZones.Count(z => z.ownerPlayerIndex == playerIndex);
            int maxClaims = RcMod.Settings?.MaxClaimsPerPlayer ?? Constants.MaxClaimsPerPlayer;
            if (existing >= maxClaims)
            {
                Messages.Message("RC_TooManyClaims".Translate(maxClaims), MessageTypeDefOf.RejectInput);
                return;
            }

            registry.RegisterZone(playerIndex, proposed);
        }

        // ── TickRare: fires every ~250 ticks for ALL buildings ─────────────
        public override void TickRare()
        {
            try
            {
                base.TickRare();

                if (placedAtTick >= 0 && graceperiodActive &&
                    GenTicks.TicksGame - placedAtTick > Constants.ClaimGracePeriodTicks)
                    graceperiodActive = false;

                // Power check — suspend claim when power lost (Tier 2 only)
                if (zoneInitialized && MP.IsInMultiplayer)
                {
                    var powerComp = GetComp<CompPowerTrader>();
                    if (powerComp != null)
                    {
                        int ownerIdx = this.Faction?.loadID ?? -1;
                        if (ownerIdx >= 0)
                        {
                            var registry = LandclaimRegistry.For(Map);
                            var zone = registry?.GetZoneByOwner(ownerIdx);
                            if (zone != null && zone.active != powerComp.PowerOn)
                                registry!.SetZoneActive(ownerIdx, powerComp.PowerOn);
                        }
                    }
                }

                // Fallback: if SpawnSetup couldn't register (faction not ready)
                if (!zoneInitialized && Spawned && MP.IsInMultiplayer)
                {
                    int ownerIdx = this.Faction?.loadID ?? -1;
                    if (ownerIdx < 0) return;

                    zoneInitialized = true;
                    DoRegisterZone(ownerIdx);
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RimClaim] TickRare CRASHED: {e}");
                zoneInitialized = true; // don't spam errors
            }
        }

        // ── Gravship integration ───────────────────────────────────────────────
        public void OnShipLanded(Map destinationMap)
        {
            if (!MP.IsInMultiplayer) return;
            zoneInitialized = false;
        }

        public void OnShipLaunched()
        {
            if (!MP.IsInMultiplayer) return;
            var registry = LandclaimRegistry.For(Map);
            registry?.UnregisterZone(this.Faction?.loadID ?? -1);
            zoneInitialized = false;
        }

        // ── Destroy ────────────────────────────────────────────────────────────
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            try
            {
                if (MP.IsInMultiplayer && zoneInitialized)
                {
                    var registry = LandclaimRegistry.For(Map);
                    registry?.UnregisterZone(this.Faction?.loadID ?? -1);
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[RimClaim] Destroy cleanup CRASHED: {e}");
            }
            base.Destroy(mode);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;

            if (!MP.IsInMultiplayer || !zoneInitialized) yield break;

            int localIdx = RcLocal.PlayerIndex;
            if (localIdx < 0) yield break;

            var registry = LandclaimRegistry.For(Map);
            var myZone = registry?.GetZoneByOwner(localIdx);
            if (myZone == null) yield break;

            bool isPaused = myZone.localTickRate == 0;
            yield return new Command_Action
            {
                defaultLabel = isPaused ? "[⏸]" : "⏸",
                defaultDesc = "Pause this claim zone. Nothing inside ticks.",
                Disabled = myZone.IsLocked,
                action = () => registry!.SetLocalTickRate(localIdx, 0)
            };

            for (int rate = 1; rate <= Constants.MaxLocalTickRate; rate++)
            {
                int r = rate;
                bool isCurrent = myZone.localTickRate == r;
                yield return new Command_Action
                {
                    defaultLabel = isCurrent ? $"[{r}x]" : $"{r}x",
                    defaultDesc = $"Set claim speed to {r}x.",
                    Disabled = myZone.IsLocked,
                    action = () => registry!.SetLocalTickRate(localIdx, r)
                };
            }

            bool hooked = myZone.speedHookActive;
            yield return new Command_Toggle
            {
                defaultLabel = "RC_SpeedHook".Translate(),
                defaultDesc = "RC_SpeedHookDesc".Translate(),
                icon = TexButton.RC_SpeedHook,
                isActive = () => myZone.speedHookActive,
                Disabled = myZone.IsLocked || (registry?.gravshipEventActive ?? false),
                toggleAction = () =>
                {
                    registry!.SetSpeedHookActive(localIdx, !hooked);
                    SpeedHookManager.SetHook(!hooked ? this : null);
                }
            };
        }

        // ── Inspect string ────────────────────────────────────────────────────
        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (!MP.IsInMultiplayer) return sb.ToString();

            int localIdx = RcLocal.PlayerIndex;
            var registry = LandclaimRegistry.For(Map);
            var myZone = registry?.GetZoneByOwner(localIdx);

            if (myZone != null)
            {
                sb.AppendLine(myZone.active ? "Claim field: Active" : "Claim field: Inactive");
                sb.AppendLine($"Zone speed: {myZone.localTickRate}x");
                if (myZone.IsLocked)
                    sb.AppendLine("Speed locked");
            }
            else if (!zoneInitialized)
            {
                sb.AppendLine("Claim field: Initializing...");
            }

            sb.AppendLine($"Material: {Stuff?.LabelAsStuff.CapitalizeFirst() ?? "none"}");
            return sb.ToString().TrimEndNewlines();
        }

        // ── ExposeData ────────────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref graceperiodActive, "graceperiodActive", true);
            Scribe_Values.Look(ref placedAtTick, "placedAtTick", -1);
            Scribe_Values.Look(ref zoneInitialized, "zoneInitialized", false);
        }
    }
}
