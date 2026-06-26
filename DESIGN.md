# RimClaim — Complete Design Document

Built on Zetrith's Multiplayer (`rwmt/Multiplayer`). Requires Harmony.
Namespace: `RimClaim` | Prefix: `RC_` | Package: `rc.rimclaim`

---

## 1. Core Philosophy

> **The simulation is shared. The experience is personal. The land is yours.**

- Zetrith's lockstep handles networking. We add sovereignty, visibility, and territory.
- All game-state mutations go through `[SyncMethod]`. Never mutate fields directly.
- Visibility/rendering patches are client-local — never synced, zero desync risk.
- `ExposeData` on every field that must survive save/load.
- All constants in `Core/Constants.cs`.
- All component access via `RcWorld.*` static shortcuts.

---

## 2. Networking Foundation

Built ON TOP of Zetrith's Multiplayer. We do not replace networking.
Connection flow is identical to Zetrith: Steam P2P or direct IP.

```
Game.Tick()
  → TickManager.DoSingleTick()     ← map tick (affected by speed/pause)
      → Map.Tick()                 ← each loaded map
  → World.WorldTick()              ← world tick (SEPARATE — always advances)
      → WorldObject.Tick()         ← caravans, gravships, sites
      → WorldComponent.Tick()      ← GravshipRegistry, EventRouter
```

**Critical:** `Find.TickManager.TicksGame` = map tick.
World objects use world tick (implicit in `WorldObject.Tick()`).
Per-map async: `map.AsyncTime().mapTicks`.
Landclaim zones: sub-map tick multiplier on top of map tick.

Always know which tick context you are in. Use the right counter.

---

## 3. Player Identity

### PlayerRegistry (GameComponent)
Ground truth for all players. Survives reconnects and save/load.
Each player gets a stable `playerIndex` from Zetrith's `MP.LocalPlayerIndex`.
Linked to a RimWorld `Faction` via `factionLoadId`.

### PlayerData
```
playerIndex        int      Zetrith MP index — stable forever
displayName        string   Steam/username
factionLoadId      int      links to Faction.loadID
playerColor        Color    UI tinting, map overlays
isConnected        bool     current session state
emergencyPausesToday int    resets each in-game day
```

---

## 4. Ownership Model

### OwnershipComp (ThingComp)
Injected via PatchOperation onto all `Building` and `Item` ThingDefs.
```
ownerPlayerIndex   int     -1 = unclaimed, ≥0 = player
teamShared         bool    teammates can access
```
All mutations via `[SyncMethod] SetOwner(int, bool)`.

### ZoneOwnershipData (MapComponent)
Parallel system for zones (not ThingWithComps).
`Dictionary<int zoneID, int playerIndex>`

### Ownership Rules
- Buildings auto-claim to constructor on completion
- Zones auto-claim to drawer on first cell placed
- Items inherit stockpile ownership when hauled in
- Items stamped with buyer's playerIndex on trade completion
- No manual Claim gizmo — ownership flows automatically from the above rules
- Owners see Share/Unshare and Release gizmos on things they own

### Steal System
- AI haul of owned items: silently blocked
- Player-ordered haul of another's item: steal prompt
- Neutral target: confirmation dialog + diplomatic record
- Enemy target: "Seize" — no confirmation, PVP action
- Victim notified immediately with Jump/Confront/Dismiss options

### Temporary Claim Bubbles (optional setting)
When items drop outside a claim (trade, caravan arrival, gravship landing,
quest reward) a temporary spatial claim spawns around the drop zone.
Expires when all items collected or after 1 in-game day.
Suppressed if items land inside another player's claim.

---

## 5. Territory — Landclaim System

### The Landclaim Block (Tier 2) / Claim Post (Tier 1)

**Claim Post** — Tier 1, Neolithic research, 1×1, no power, 15-tile radius.
  Stuffable: Metallic/Stony/Woody. 75 stuff units. No component cost.
  Per-zone tick rate control (1×/2×/3×). Speed hook. Ownership enforcement.
  Auto-registers claim zone immediately on construction.

**Landclaim Block** — Tier 2, Industrial research, 2×2, 200W power, 25-tile radius.
  Stuffable: Metallic/Stony/Woody. 150 stuff + 4 CompIndustrial + 30 Plasteel.
  Per-zone tick rate control (1×/2×/3×). Speed hook. Breakdownable.
  Auto-registers claim zone immediately on construction.

**Material effects on HP (base 300 for beacon, 150 for post):**
| Material | HP Factor | Flammable | Notes |
|---|---|---|---|
| Plasteel | 2.0× | No | Maximum durability |
| Uranium | 1.5× | No | Dense, heavy |
| Granite | 1.5× | No | Best stone |
| Steel | 1.0× | No | Standard |
| Marble | 1.0× | No | Beautiful |
| Sandstone | 0.8× | No | Easy early stone |
| Wood | 0.5× | Yes | Early placeholder — replace ASAP |
| Gold | 0.6× | No | Soft, fragile, very beautiful |

**Power loss:** Claim suspends (not deleted). Spatial enforcement off.
Items retain ownership stamps. Zone boundary stops rendering.
PVP implication: destroying power = collapsing enemy's claim field.

**Building destroyed:** Claim dissolves. Items stamped, not protected.
Orphaned zones remain drawn but unenforced. Rebuild to restore.

### Territory Rules
```
Inside your claim:
  ✓ Build, zone, mine, deconstruct
  ✓ Tick rate control

Outside all claims (no-man's-land):
  ✓ Build, mine, deconstruct
  ✗ Place zones (zones require a claim)

Inside neutral player's claim:
  ✗ Build, zone
  ✓ Walk through
  ✓ Mine if team permission

Inside enemy claim:
  ✗ Build, zone, mine (unless PVP active)
  ✓ Walk through (doors locked)
```

### Buffer Zone
Minimum 5-tile gap between any two players' claims (configurable).
No-man's-land between players. First-come first-served for resources.
Default build-allowed, zone-not-allowed.

### Mod Settings — Territory
- Border placement: Strict / Lenient
- Temporary claim bubbles: On / Off
- Max landclaims per player (default 3)
- Default claim radius (default 25)
- Buffer zone width (default 5)

### Gravship Compatibility
Landclaim block on gravship substructure travels with the ship.
Claim INACTIVE while ship is in flight or orbit.
On landing: `OnShipLanded()` re-registers zone at new location.
On launch: `OnShipLaunched()` unregisters zone. Old territory abandoned.

---

## 6. Per-Claim Tick Rate

### Mechanism: Tick Debt Accumulation
```
Each global map tick:
  For each LandclaimZone:
    zone.tickDebt += zone.localTickRate
    while (tickDebt >= 1.0):
      TickAllThingsInZone()
      tickDebt -= 1.0

Things inside claims: suppressed from normal map tick pass
Things outside claims: tick once per global tick (normal)
```

Rate 0 = paused. Rate 1 = global. Rate 2 = 2×. Rate 3 = 3× (max).
A thing's rate = rate of the claim containing its `Position` cell.
Multi-cell buildings: rate determined by origin cell.

### Speed Hook
Toggle on landclaim block gizmo: "Hook Game Speed Buttons: ON/OFF"

OFF: vanilla speed buttons control global tick rate normally.
ON: speed buttons (1/2/3/4/Space) control THIS claim's local rate.
    Global tick unaffected. Other players completely unaffected.
    Buttons tinted in player's color when hooked.
    Space = pause/unpause your claim only (no budget cost).

Speed hook locked during:
- Active gravship events (entire hook system locked)
- Active trade lock on the claim

### HUD When Hooked
```
┌──────────────────────────────────┐
│  🏴 Your Zone: ▶▶ 2x  [unhook]  │
│  Global: ▶ 1x                   │
└──────────────────────────────────┘
```

---

## 7. Team System

### TeamRegistry (GameComponent)
Teams are named groups with granular shared permissions.

### SharedPermissions
```
shareResources    bool   haul each other's items
shareStorage      bool   use each other's stockpiles
shareFurniture    bool   beds, chairs, assignables
shareBuildings    bool   production buildings
shareAreas        bool   zones/areas visible and usable
shareBills        bool   view and add bills
sharePawnBar      bool   see each other's colonist bar
shareDoors        bool   open each other's doors
```

### Players Tab UI
Bottom bar tab button → two sub-tabs: Team and Enemies.
Team sub-tab: player list, team membership, permission checkboxes.
Enemies sub-tab: player list, neutral/enemy status, declare/peace buttons.

---

## 8. Diplomacy and PVP

### DiplomacyRegistry (GameComponent)
One-sided relations: Player A can declare B enemy without B's consent.
PVP activates when EITHER player has declared the other enemy.
Backed by RimWorld faction relations (`SetRelationDirect`).

### Diplomatic History
Per-pair event log displayed in Enemies sub-tab:
```
DeclaredEnemy, StoleGoods, CeremonyInterrupted,
CeremonySabotaged, PeaceDeclared
```
No automatic penalties — social information only.

### PVP Rules
- Pausing: Tier 2 and 3 pauses locked
- Combat: faction hostility enables pawn attacks
- Doors: enemy pawns locked out of owned doors
- Enemy claim entry: pathfinder blocks destination in enemy claim
- Seizure: enemy can seize goods dropped in their claim
- Caravan attack: "Attack Caravan" float menu option appears

### Enemy Pawn in Your Claim (PVP Activates)
- Undrafted: forced "leave map" job assigned immediately
- Drafted: player in control — no forced job
- On undraft: forced leave job re-assigned
- No valid exit: pawn trapped — owner notified with Draft convenience button

### Peace
Both players must click "Offer Peace" to end PVP.
One-sided peace declaration visible in diplomacy record.

---

## 9. Pause System

### Three Tiers

**Tier 1 — Speed Negotiation (always active)**
Game runs at slowest requested speed among all players.
Silent democratic slowdown. No blocking, no voting.

**Tier 2 — Soft Pause Request**
Any player requests pause → notification to others (5s auto-allow).
One objection cancels it.
Blocked during PVP.

**Tier 3 — Emergency Pause**
Instant pause. 3 per in-game day budget.
Free (no budget) during active combat sync.
Blocked during PVP.
Blocked during gravship events UNLESS all your pawns are dead/downed.

### Speed Hook Interaction
When hook is ON, Space pauses/unpauses the claim only.
Does not consume emergency pause budget.
Does not affect other players.

---

## 10. Event System

### EventRouter (MapComponent)
Round-robin cursor assigns targeted events to players.
Scales with player count: `base × (1 + (N-1) × 0.6)`

### Event Classification

**ClaimLocal** — targets one player's claim, ticks at claim rate.
Examples: raids, manhunter packs, crop blight, psychic drone.

**MapWide** — affects whole map. No forced speed change.
Players choose their own speed. Consequences scale with claim rate.
Warning gizmo on landclaim block shows active map-wide event.
Examples: solar flare, toxic fallout, eclipse, volcanic winter.

**WorldLevel** — world tick origin. No claim interaction.
Examples: caravan arrival, orbital trader, faction war declaration.

**CrossClaim** — global tick rate, boundary rules apply naturally.
Examples: prisoner escape, wanderer join, disease outbreak, berserk pawn.

**GravshipEvent** — forces ALL claims on map to 1x. Speed hooks locked.
Restores speeds when event resolves.
Emergency pauses locked unless all player's pawns dead/downed.
Examples: orbital mechanoid attack, hull breach, engine failure.

### Cross-Claim Entity Rules
- Prisoner escape: holding claim drops to 1x. Restores when escapee leaves claim.
  Escapee assumes tick rate of wherever they are afterward.
  Combat sync fires if chase crosses zone boundaries.
- Wanderer joins: round-robin targeted player. Ticks at destination claim rate.
- Disease outbreak: ticks at claim rate of infected pawn's current location.
  Advisory warning if claim is fast.
- Berserk pawn: assumes tick rate of current claim. Combat sync on cross-zone attack.

### Ceremony Handling
CeremonyTracker (MapComponent) records active ceremony, owner, anchor building.

**Royalty bestowing:**
Shuttle lands in triggering player's claim (fallback to open map).
Ceremony ticks at claim rate. Completing faster at 3x is a real benefit.
Other players unaffected.

**Ceremony cancellation:**
- B declares enemy but does nothing: ceremony continues
- B attacks non-participant (combat sync fires, ceremony unaffected)
- B attacks participant/building: vanilla cancel + CeremonyInterrupted logged
- A attacks B's guests (confirmation dialog first): ceremony cancels
- B declares enemy, undrafted pawns walk out, ceremony continues

**Ideology rituals:** Altar owner's claim rate. Non-owner participants
get 50% secondary outcome benefit.

**Biotech/Anomaly:** All building-based, tick at claim rate naturally.
Monolith activation = Class MapWide (player's own speed, consequence warning).

---

## 11. Trading

### Trade Isolation
Opening a trade dialog pauses ONLY the trading player's landclaim.
Other players' claims keep running. No notification to uninvolved players.

Trader pawn exempted from claim pause (keeps ticking).
Items staged in trade get `tradeLockActive` flag — blocks haul jobs.

### Multi-Player Trade
Other players on same map get: Join Trade / Step Away / Watch.
Join: shared trade window, items tagged by contributor, split ownership by value.
Step Away: their pawns tick (claim unpaused for them) but can't touch trade items.
Watch: frozen with trader, read-only window.
All co-traders must click Ready before trade executes.

### ExecuteTrade (SyncMethod)
The ONLY game-state mutation point. Everything in the dialog is display.
Items given: removed from map.
Items received: spawned and immediately stamped with buyer's playerIndex.
Temporary claim bubble created if setting enabled.

### Trader Location Rules
| Trader location | Trade possible | Goods drop | Bubble | Note |
|---|---|---|---|---|
| Unclaimed | Yes | Unclaimed | If enabled | Normal |
| Buyer's claim | Yes | Inside claim | Skipped | Already protected |
| Neutral claim | Yes | Neutral's claim | Skipped | Neutral notified |
| Allied claim | Yes | Ally's claim | Skipped | Ownership stamp protects |
| Enemy claim | No — can't reach | N/A | N/A | Path blocked |
| Enemy claim (mid-session) | Session continues | Enemy's claim | Skipped | Enemy can confiscate |

### Trade Timer (optional)
Configurable per-session limit (60/120/180/300s or OFF).
Auto-closes dialog on timeout. Map unpauses. No trade occurs.

---

## 12. Caravans

### Core Principle
Caravans are COMMANDED, not simulated.
Position advances via discrete `CommitStep` SyncMethods, not continuous ticking.
Every decision (move, encounter, trade, form, split) is a player command.

### CaravanOwnerComp (WorldObjectComp)
```
ownerPlayerIndex   int    who controls this caravan
teamShared         bool   teammates can give orders
```
Auto-claims on `FormAndCreateCaravan`.
Other players cannot give move orders to caravans they don't own.

### Movement
`CommitStep(fromTile, toTile)` SyncMethod.
Fires when owner's client detects step countdown = 0.
All clients execute simultaneously at same tick.
Fuel consumption inside CommitStep — no drift possible.

### Encounter Resolution
`TriggerEncounter(caravanId, tileIndex)` SyncMethod.
`Rand.PushState(seed)` where seed = `Hash(tileIndex, TicksGame)`.
Guarantees identical encounter even if prior RNG drifted.
`Rand.PopState()` after generation.

### Caravan Forming
Dialog is LOCAL STATE ONLY.
`FormCaravan(ownerPlayer, pawnIds[], itemIds[], stackCounts[], destTile)` SyncMethod.
Only the Confirm button touches game state.

### Caravan Trade
Dialog is LOCAL STATE ONLY.
`ExecuteTrade(traderThingId, TradeItem[] given, TradeItem[] received)` SyncMethod.

### World Map Visibility
Enemy caravans visible but no float menu options.
Neutral caravans: visible, no interaction.
Own caravan: full float menu.
Teammate caravan: move orders if teamShared.
Enemy caravan (PVP): "Attack Caravan" option.

---

## 13. Gravships (Odyssey DLC)

### Three States
**Grounded:** substructure is part of colony map. Normal map rules.
**In Flight:** gravship IS its own map. Ticks independently (async).
**In Orbit:** gravship map at vacuum altitude. Orbital encounters possible.

### GravshipOwnerComp (MapComponent on ship map)
```
ownerPlayerIndex      int
teamShared            bool
state                 GravshipState
currentWorldTile      int
destinationWorldTile  int
ticksUntilNextStep    int
```

### GravshipWorldObj (WorldObjectComp on world object)
World-tick-context step tracking. Authoritative on position.
GravshipMapComp reads from it for display. One-way dependency.

### Launch Transaction
Countdown fires via `InitiateGravshipCountdown` SyncMethod.
`launchTick = now + max(30s floor, longestBoardingPath + 10s buffer)`
`ExecuteGravshipLaunch` fires at `launchTick` automatically (tick-driven, not player-triggered).
`Rand.PushState(stableSeed)` wraps entire launch transaction.
Things sorted by `thingIDNumber` before transfer (deterministic order).

### Landing Transaction
`ExecuteGravshipLand` SyncMethod.
`Rand.PushState(seed = Hash(destTile, TicksGame))` before map generation.
`map.AsyncTime().QueueAction()` for transfer (fires at tick boundary, not mid-tick).

### Boarding
All non-aboard, non-drafted pawns get sprint-urgency boarding jobs on countdown start.
Drafted pawns: player in control, manual direction required.
Animals/mechs included in boarding count.
Downed during boarding: rescue option offered, countdown may extend.

### Co-Launch
When Player A launches and Player B's ship is threatened:
- "Launch With Them": B joins, readiness gate, fires at same tick
- "Emergency Launch": independent 10s countdown, fires before A
- "Dismiss": final warning at 10s, B accepts consequence

### Extension Request
B can request more time from A during co-launch boarding window.
A gets non-dismissable decision prompt (60s timeout → auto-decline).
A accepts: launchTick extended, all notified.
A declines: B removed from co-launch, B gets Emergency Launch shortcut.
Only one pending request at a time.

### Gravship Events
Force ALL claims on the ship map to 1x.
Speed hooks locked.
Restores on event resolution.
Emergency pauses locked unless all pawns dead/downed.

---

## 14. Desync Risk Register

| Risk | Mitigation |
|---|---|
| Caravan movement RNG | Discrete step SyncMethods, not tick-driven |
| Encounter map generation | TriggerEncounter SyncMethod + Rand.PushState(seed) |
| Trade dialog state | Dialog is display-only. Single ExecuteTrade SyncMethod |
| Caravan form dialog | Dialog is display-only. Single FormCaravan SyncMethod |
| Gravship launch transfer | ExecuteGravshipLaunch SyncMethod + sorted thingIDNumber |
| Gravship landing map gen | Rand.PushState(Hash(tile, tick)) in ExecuteGravshipLand |
| Fuel consumption | Inside CommitStep SyncMethod |
| Two pilot consoles | Pilot console locked to owner |
| Orbital encounter RNG | TriggerOrbitalEncounter SyncMethod + seeded Rand |
| TicksGame in map context | Use map.AsyncTime().mapTicks instead |
| Cross-map item transfers | QueueAction to fire at tick boundary |
| Zone tick suppression | HashSet<int> of claimed thingIDs, O(1) lookup |
| Multi-cell building on border | Tick rate = origin cell's zone (Strict mode prevents this) |
| Combat sync race | Verb.TryStartCastOn Prefix fires before damage |
| Co-launch ordering | OrderBy(thingIDNumber) before all transfer loops |
| Extension request timeout | Auto-decline after 60s if no response |

---

## 15. File Structure

```
RimClaim/
  About/About.xml
  Assemblies/RimClaim.dll          ← build output
  Defs/
    GameComponentDefs.xml
    MapComponentDefs.xml
    MainTabDef.xml
    ThingDefs/
      LandclaimBuildings.xml
    ResearchDefs/
      TerritoryResearch.xml
    JobDefs/
      RsovJobs.xml
    IncidentDefs/
      RcIncidentExtensions.xml
  Patches/
    OwnershipComp_Inject.xml
    IncidentClassification.xml
  Languages/English/Keyed/Keyed.xml
  Textures/RSOV/UI/
    [icon PNGs]
  Source/RimClaim/
    RimClaim.csproj
    Core/
      ModInit.cs
      Constants.cs
      PlayerData.cs
      TeamData.cs
      RcWorld.cs
      TexButton.cs
      RcSettings.cs
    Components/
      PlayerRegistry.cs
      TeamRegistry.cs
      DiplomacyRegistry.cs
      SpeedNegotiator.cs
      ZoneOwnershipData.cs
      LandclaimRegistry.cs
      LandclaimZone.cs
      EventRouter.cs
      CeremonyTracker.cs
      TradeSessionRegistry.cs
      GravshipRegistry.cs
      CaravanRegistry.cs
    Comps/
      OwnershipComp.cs
      CaravanOwnerComp.cs
      GravshipOwnerComp.cs
      GravshipWorldObjComp.cs
      TradeLockComp.cs
    Buildings/
      Building_LandclaimBlock.cs
      Building_ClaimPost.cs
    Jobs/
      JobDriver_BoardGravship.cs
    Patches/
      Patch_Door_Ownership.cs
      Patch_ColonistBar_Filter.cs
      Patch_BillsTab_Ownership.cs
      Patch_Construction_AutoClaim.cs
      Patch_Hauling_OwnershipInherit.cs
      Patch_TimeControls_Hook.cs
      Patch_ZoneDesignator_ClaimCheck.cs
      Patch_BuildDesignator_ClaimCheck.cs
      Patch_ThingTick_ClaimSuppress.cs
      Patch_Verb_CrossZoneCombat.cs
      Patch_PrisonerEscape_ClaimSlow.cs
      Patch_Draft_ExitJobCheck.cs
      Patch_Pathfinder_EnemyClaimBlock.cs
      Patch_FloatMenu_StealOption.cs
      Patch_CaravanFloatMenu.cs
      Patch_CaravanForm.cs
      Patch_CaravanTrade.cs
      Patch_PilotConsole.cs
      Patch_GravshipLaunch.cs
      Patch_GravshipLand.cs
      Patch_GravshipStep.cs
      Patch_GravshipBoarding.cs
      Patch_ShuttleLanding_ClaimPreference.cs
      Patch_RitualOutcome_MultiPlayer.cs
      Patch_Ceremony_CancelOnAttack.cs
      Patch_Draft_CeremonyWarning.cs
      Patch_WandererJoin_RoundRobin.cs
      Patch_Draft_ExitJobCheck.cs
      Patch_Pawn_TrackCeremonyAttacker.cs
      Patch_StuffDialog_ClaimWarning.cs
    Sync/
      SyncWorkerRegistration.cs
    UI/
      MainTabWindow_Players.cs
      SpeedHookHUD.cs
      LaunchBoardingPanel.cs
```

---

## 16. Implementation Phases

### Phase 1 — Foundation ✓ (built)
PlayerRegistry, TeamRegistry, DiplomacyRegistry, OwnershipComp,
ZoneOwnershipData, basic Harmony patches, Players tab UI.

### Phase 2 — Territory ✓ (built)
LandclaimBlock, ClaimPost, LandclaimRegistry, LandclaimZone,
tick multiplication (reflection + comp fallback), tick suppression (paused zones),
SpeedHookManager, zone/build enforcement, combat sync, prisoner escape sync.
Zone pause gizmo (0x/1x/2x/3x). Door ownership patch covers pathfinder block.
Steal system deferred (RimWorld 1.6 FloatMenuContext API change).

### Phase 3 — Pause Refinement (current)
SpeedNegotiator (3-tier: negotiation, soft pause, emergency pause),
soft pause request UI (notification banner with Allow/Object buttons),
emergency pause budget UI (top-right HUD with remaining count),
pause during PVP lock. Per-map SpeedNegotiator deferred pending
Zetrith async-time API integration.

### Phase 4 — Transport
CaravanOwnerComp, CaravanStepComp, CaravanRegistry, GravshipOwnerComp,
GravshipRegistry, PendingLaunch co-launch system, all transport patches.

### Phase 5 — Events and Ceremonies
EventRouter, CeremonyTracker, RwtIncidentExtension, incident XML classification,
all ceremony patches, round-robin wanderer join, disease/berserk rules.

### Phase 6 — Trade
TradeSessionRegistry, claim-local trade pause, multi-player trade window,
temporary claim bubbles, trader location rules, confiscation system.

### Phase 7 — Polish
Performance profiling, desync test suite (two-instance local),
mod settings UI, edge case hardening, DLC compatibility testing.
