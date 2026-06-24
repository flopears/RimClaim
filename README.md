# RimWorldTogether

A bespoke player-sovereignty multiplayer layer for RimWorld, built on top of
[Zetrith's Multiplayer mod](https://github.com/rwmt/Multiplayer).

## Phase 1 Foundation — Implemented

This is the Phase 1 foundation build. Everything here compiles, saves, loads, and syncs.

### What's built

| System | Files | Status |
|---|---|---|
| Player identity (`PlayerRegistry`) | `Components/PlayerRegistry.cs`, `Core/PlayerData.cs` | ✅ |
| Team system (`TeamRegistry`) | `Components/TeamRegistry.cs`, `Core/TeamData.cs` | ✅ |
| Diplomacy (`DiplomacyRegistry`) | `Components/DiplomacyRegistry.cs` | ✅ |
| Pause negotiation (`SpeedNegotiator`) | `Components/SpeedNegotiator.cs` | ✅ |
| Building ownership (`OwnershipComp`) | `Comps/OwnershipComp.cs` | ✅ |
| Zone ownership (`ZoneOwnershipData`) | `Components/ZoneOwnershipData.cs` | ✅ |
| Auto-claim on construction | `Patches/Patch_Construction_AutoClaim.cs` | ✅ |
| Door access control | `Patches/Patch_Door_Ownership.cs` | ✅ |
| Colonist bar filtering | `Patches/Patch_ColonistBar_Filter.cs` | ✅ |
| Bills tab ownership gate | `Patches/Patch_BillsTab_Ownership.cs` | ✅ |
| Hauling ownership inheritance | `Patches/Patch_Hauling_OwnershipInherit.cs` | ✅ |
| Players tab UI (Team + Enemies) | `UI/MainTabWindow_Players.cs` | ✅ |
| MP sync workers | `Sync/SyncWorkerRegistration.cs` | ✅ |
| XML Defs (GameComponent, MapComponent, MainTab) | `Defs/` | ✅ |
| Translation strings | `Languages/English/Keyed/Keyed.xml` | ✅ |
| Ownership comp injection (PatchOperation) | `Patches/OwnershipComp_Inject.xml` | ✅ |

### What comes next (Phase 2+)

- [ ] Bill tab full read-only mode for teammates
- [ ] Designation and zone overlay rendering filters
- [ ] Inventory/gear tab ownership gating
- [ ] Hauling job: refuse hauling non-owned items without permission
- [ ] `EventRouter` MapComponent (round-robin event targeting)
- [ ] Threat point scaling (N-player multiplier)
- [ ] PVP: resource raiding job, door breaching, peace flow
- [ ] Pause request UI overlay (soft pause notification widget)
- [ ] Multi-map async time integration

## Building

```
set RIMWORLD_PATH=C:\Program Files (x86)\Steam\steamapps\common\RimWorld
set MP_API_PATH=C:\...\Multiplayer\Current\Assemblies
cd Source\RimWorldTogether
dotnet build
```

The compiled `RimWorldTogether.dll` outputs to `Assemblies/`.

## Development workflow

1. Build → DLL copies to `Assemblies/`
2. Launch RimWorld twice from the same install
3. Host a session in Instance 1, join via LAN from Instance 2
4. Check the Debug Log (Dev Mode) for any desync warnings

## Architecture principles

- **The simulation is shared. The experience is personal.**
- All game-state mutations go through `[SyncMethod]` — never mutate fields directly.
- Visibility/rendering patches are client-local — never synced, zero desync risk.
- `ExposeData` on every field that must survive a save/load.
- All constants live in `Core/Constants.cs` — never scatter magic numbers.
- All component access goes through `RwtWorld.*` static shortcuts.
