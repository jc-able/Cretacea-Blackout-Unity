# Cretacea: Blackout — Master AAA Unity 6 HDRP Architecture & Level Blueprint

**Target Project:** `~/CretaceaBlackoutUnity`  
**Engine & Pipeline:** Unity `6000.6.9f1` (`arm64` Apple Silicon) · **HDRP 17.6.0** (`com.unity.template.hdrp-blank`)  
**Target Hardware:** Apple M5 Max (18-core CPU, 40-core GPU, 128 GB Unified Memory, Metal 4)  
**Source References:** `antigravity_scratch/godot_audit.md` · `antigravity_scratch/godot_visual_audit/visual_report.md` · `antigravity_scratch/toolchain_guide.md` (on the original machine, not included)  
**Scope Rule:** `~/CretaceaBlackout` is **strictly read-only**. All build work happens inside `~/CretaceaBlackoutUnity`.

---

## Executive Directives (Non-Negotiable AAA Standards)

1. **Zero Godot Bug Carryover:** Every geometric, collision, flashlight, door, prop-placement, dinosaur-rigging, UI, lighting, and audio defect cataloged in `godot_audit.md` (§3) and `visual_report.md` (§2–§6) is eliminated **by construction** in the contracts below.
2. **Modular Architecture, Never Raw Box Veneers:** All walls are solid double-sided modular assemblies with **true cut openings** for doors and windows. Floating non-colliding veneer slabs (`door_wing`, `dock_roller`), unbroken corridor dado rails across doorways, and fake glass pasted onto solid walls are strictly prohibited.
3. **Ironclad Flashlight & Cutscene Discipline:** `PlayerEquipment.HasFlashlight` starts `false` in Chapter 1; the FPS viewmodel (`flashlight_fps_rig.fbx`) is **strictly hidden** whenever `!HasFlashlight || GameStateManager.InCutscene || GameStateManager.IsMainMenu`, and renders via an HDRP Viewmodel Custom Pass + forward raycast pullback so it never clips through walls.
4. **Metal 4 HDRP Guardrails:** **Ray Tracing OFF** (unsupported on `StandaloneOSX`). Achieve true AAA atmosphere through **Froxel Volumetric Fog**, **Half-Res Denoised SSGI**, **SSR**, **SSAO**, **Contact Shadows**, **DBuffer Decals**, **Subsurface Scattering (SSS)** on wet dinosaur skin, **Adaptive Probe Volumes (APV) + per-room Box-Projected Reflection Probes**, **ACES Tonemapping**, and explicit **EV100 Exposure Volumes**.
5. **Parallel Zero-Collision Engineering:** Four Unity build workers (`unity-core-scripts`, `unity-level-builder`, `unity-dino-ai`, `unity-ui-audio`) own disjoint folders, assemblies, scenes, and prefabs (§7) communicating solely through immutable interfaces and ScriptableObject/event contracts (§3).

---

## 1. Coordinate System, Scene Topology & Complete Unity World Map

### 1.1 Godot → Unity Coordinate Conversion Rule
Godot uses **Right-Handed Y-Up** (`+X` East, `+Y` Up, `+Z` South; `yaw = 0°` faces `-Z` North, `yaw = -90°` faces `+X` East).  
Unity uses **Left-Handed Y-Up** (`+X` East, `+Y` Up, **`+Z` North**; `yaw = 0°` faces `+Z` North, `yaw = +90°` faces `+X` East).

$$\text{Unity}(x, y, z) = \text{Godot}(x, y, -z) \qquad \text{UnityYaw} = -\text{GodotYaw}$$

- **Continuous Progression Direction:** Remains strictly **West (`-X`) → East (`+X`)**, from `X = -101.5` to `X = +548.0` (~650 m seamless journey).
- **Cardinal Axes in Unity:** East = `+X`, West = `-X`, **North = `+Z`**, **South = `-Z`**, Up = `+Y`.
- **Every coordinate in this blueprint is already converted to Unity World Space `(X, Y, Z)`** unless explicitly noted.

### 1.2 Scene Topology & Modular Prefab Architecture
To guarantee zero loading stalls across the 650 m continuous world **and** zero `.unity` scene merge conflicts between workers:

| Scene / Prefab Asset | Purpose | Owner Worker |
|---|---|---|
| `Assets/Scenes/Boot.unity` | Lightweight bootstrapper loading persistent services (`GameStateManager`, `SettingsManager`, `AudioManager`, `InputManager`) then `MainMenu.unity`. | `unity-core-scripts` |
| `Assets/Scenes/MainMenu.unity` | **Live real-time 3D HDRP Menu Scene** (rain-lashed Isla Tormenta Visitor Center / Talon Ridge facade at night with volumetric emergency beacons, wet asphalt reflections, and periodic lightning — **replaces broken baked `menu_bg.jpg`**). | `unity-ui-audio` |
| `Assets/Scenes/IslaTormenta.unity` | Single continuous gameplay world housing the unified sculpted `Unity Terrain`, HDRP Sky/Fog/APV Volumes, Light Groups, Reflection Probes, NavMesh Surfaces, and nested Prefab roots for all 5 Zones + `PlayerRoot` + `HUDCanvas` + `StoryDirector` + `DinosaurSpawners`. | `unity-level-builder` |
| `Assets/Prefabs/World/Zone_01_VisitorCenter.prefab` | Visitor Center, Plaza, Viewing Gallery, Loading Dock, Talon Ridge Pen. | `unity-level-builder` |
| `Assets/Prefabs/World/Zone_02_ServicePaths.prefab` | Concrete Jungle Service Paths, open North T-Branches, Paddock 4 Service Gate. | `unity-level-builder` |
| `Assets/Prefabs/World/Zone_03_TyrantValley.prefab` | Valley Props, Stream Bridge/Culvert, Jeep, Watchtowers, Substation 3 Compound. | `unity-level-builder` |
| `Assets/Prefabs/World/Zone_04_OldYard.prefab` | Old 1990s Maintenance Yard, Warehouse B, Workshop/Office Block, Exit Court, E-0 Gate. | `unity-level-builder` |
| `Assets/Prefabs/World/Zone_05_BackdropAndCoast.prefab` | Titan Meadows, Volcanic Mountain Ring (open East!), Ocean Surface, Dawn Coastal Cliffs & Evac Beacon. | `unity-level-builder` |

### 1.3 Macro World Layout (Unity World Space)
| Zone | Unity `X` Range (W→E) | Unity `Z` Range (S→N) | Base Floor `Y` | Audio Reverb Zone |
|---|---|---|---|---|
| **Titan Meadows (South, Opening)** | `-260.0 .. +188.0` | `-378.0 .. -98.0` | `-6.2` (lake `-8.8`) | `Reverb_OpenValley` |
| **Main Gate & Plaza** | `-101.5 .. +30.0` | `-96.0 .. -32.0` | `0.0` | `Reverb_ExteriorPlaza` |
| **T. rex Viewing Gallery** | `+30.0 .. +56.0` | `-70.0 .. -40.0` | `0.0` | `Reverb_GalleryHall` |
| **Visitor Center ("The Atrium")** | `-72.0 .. +8.0` | `-32.0 .. +34.0` | `0.0` | Per-room (`vc_offices` / `vc_lobby` / `vc_wing`) |
| **Covered Loading Dock** | `+8.0 .. +22.0` | `-18.0 .. -2.0` | `0.0` (canopy `5.2`) | `Reverb_SemiExterior` |
| **Talon Ridge Raptor Pen (North)** | `-90.0 .. +20.0` | `+42.0 .. +130.0` | `0.0` | `Reverb_ExteriorPlaza` |
| **Service Paths (Enclosed Jungle)** | `+21.5 .. +91.0` | `-11.3 .. +34.0` | `0.0` (walls `5.0`) | `Reverb_ConcreteCanyon` |
| **Paddock 4 — Tyrant Valley** | `+91.0 .. +401.0` | `-200.0 .. +200.0` | `-1.6 .. +2.3` (Terrain) | `Reverb_OpenValley` |
| **Substation 3 Compound** | `+250.0 .. +286.0` | `-17.0 .. +17.0` | `2.3` (concrete pad) | `Reverb_IndustrialPad` |
| **Old Maintenance Yard (1990s)** | `+401.0 .. +505.0` | `-95.0 .. +50.0` | `1.0` | `Reverb_YardExterior` / `Reverb_MetalWorkshop` |
| **Coast Road & Dawn Ocean Overlook** | `+505.0 .. +548.0` | `-49.8 .. -42.2` (cliff `-106..+14`) | `1.0` (ocean `-40.0`) | `Reverb_OceanCliff` |

### 1.4 Zone 1 — Visitor Center Room-by-Room Blueprint (`Y = 0.0`)
All interior walls use beveled modular concrete/plaster/tile panels (`0.25 m` thickness, single unified collider + visual mesh, zero separate veneer boxes). Every room has a dedicated **Box-Projected Reflection Probe** and `IndoorRainExclusionVolume`.

| Room | Unity Bounds `X` / `Z` (m) | Size (`W×D`) | Ceil `Y` | Floor / Wall Surface Tag | Reverb / Amb | Critical Fixes & AAA Set-Dressing |
|---|---|---|---|---|---|---|
| **Security Office** *(Ch. 1 Spawn)* | `X: -72..-58`<br>`Z: +22..+34` | `14 × 12` | `3.45` | `Tile` (border) + `Concrete` / Carpet center | `Reverb_OfficeTight` / `vc_offices` | • **Fix `POS-9`:** Spawn player at `(-64.5, 0.05, +28.0)`, **`UnityYaw = 0°` (facing `+Z` NORTH directly at the CCTV desk & live glowing screens!)**.<br>• **Fix `FL-1/FL-4`:** Place `flashlight_pickup.fbx` at `(-64.0, 0.82, +31.0)` with subtle pulsing amber charging LED + `walkie_talkie` `(-64.2, 0.80, +31.0)` + `memo_switchover` `(-65.8, 0.81, +30.9)` + `id_badge.png` lanyard on desk.<br>• **Fix `DOOR-5`:** Cut **real recessed steel-framed exterior window** in west wall (`X = -72.0, Z = +27.0..+30.0, Y = 1.1..2.5`) with rain-streaked glass & mullions so Opening Cutscene Shot 6 pushes smoothly into the lit office.<br>• Terminal desk `(-61.0, 0.0, +26.0)` with glowing monitor prompt.<br>• 6 live low-FPS (10 Hz) RenderTexture CCTV monitors mounted on north wall (`Z = +33.65`). |
| **Locker Room** | `X: -58..-48`<br>`Z: +22..+34` | `10 × 12` | `3.45` | `Tile` / `plaster_new` | `Reverb_OfficeTight` | 3 locker banks (`Z = +33.3`), wooden bench `(-53, 0, +29)`, uniform clutter, ceiling ductwork & recessed LED troffers. |
| **Control Room** | `X: -48..-26`<br>`Z: +22..+34` | `22 × 12` | `3.45` | `Concrete` (carpet inset) / `plaster_new` | `Reverb_ControlRoom` | • **Fix `DOOR-4`:** **Exclude 17 m North Observation Window** (`X = -45.5..-28.5, Y = 0.85..3.15, Z = +34.0`) from Occlusion Culling! Lower sill slightly to `0.85 m` so Talon Ridge sliding gate (`Z = +42.0`) and electrified pylons are dramatically framed.<br>• 2×4 tiered `control_console`s (`Z = +31.5, +28.3`, baked 24° yaw fixed in `asset-foundry` FBX) + chairs.<br>• **Fix `UX-5`:** Replace invisible floor trigger with an interactive glowing **Primary Paddock Telemetry Console** at `(-37.0, 0.95, +31.2)` (*"[E] Log Talon Ridge & Tyrant Valley Feeds"*) PLUS fallback area trigger if player stands at the window.<br>• **Fix `POS-10`:** Link east status wall (`X = -26.25`) + `map_park_modern.png` emission directly to `vc_main` power group (flickers and dies cleanly in blackout!). |
| **Server Room** | `X: -26..-18`<br>`Z: +22..+34` | `8 × 12` | `3.45` | `Tile` (raised floor) / `plaster_new` | `Reverb_OfficeTight` | • **Fix visual audit:** Rotate all 4 `server_rack`s (`X = -24.5..-21.5, Z = +31.0`) to face **SOUTH (`-Z`)** toward corridor entry so blinking green/amber drive LEDs and cable trays are visible. Overhead perforated cable tray + cold blue accent light (`vc_main`). |
| **Records Annex (1990s)** *(Ch. 2 Spawn)* | `X: -18..0`<br>`Z: +22..+34` | `18 × 12` | `3.45` | `Tile` / `brick_old` + peeling green plaster | `Reverb_OfficeTight` | 3×3 `archive_shelf` rows (`X = -15.5..-8.7, Z = +26.6..+32.6`); Old desk `(-3.5, 0, +27.5)` + `crt_monitor` + `binder_manual` at `(-3.6, 0.82, +27.3)` illuminated by focused warm-red emergency work lamp; Ch. 2 spawn `(-6.0, 0.05, +24.5, yaw = 140°)`. |
| **Janitorial / Storage Closet** *(NEW — Fixes `DOOR-8`)* | `X: 0..+8`<br>`Z: +22..+34` | `8 × 12` | `3.45` | `Concrete` / unpainted block | `Reverb_OfficeTight` | **Eliminates empty floorless skybox void behind `door_storage`!** Built with real concrete floor, ceiling, storage shelves, mop bucket, jerry cans, electrical junction box, and frosted wire-glass door window so inspecting locked `door_storage` reveals a grounded, believable room. |
| **Staff Corridor** | `X: -72..+8`<br>`Z: +19..+22` | `80 × 3` | `3.25` | `Concrete` / `plaster_new` | `Reverb_Corridor` | • **Fix `COL-3`:** Segment stainless dado rails (`Y = 1.0`) & rubber skirting **strictly between doorway jambs** — zero bars crossing doorways.<br>• 10 recessed ceiling lights (`vc_main`), 5 rotating shadow-casting `beacon_red` emergency lights (`vc_emergency`, **Fix `POS-12`**: clean emissive dome, NO protruding red cubes), illuminated green EXIT signs pointing toward Lobby (`X = -31`), Staff Exit (`X = -50`), and Records (`X = -9`).<br>• Room plaques mounted on BOTH corridor and interior sides of doorways (`UX-5`). |
| **Break Room** | `X: -72..-50`<br>`Z: +6..+19` | `22 × 13` | `3.45` | `Tile` / `plaster_new` | `Reverb_OfficeTight` | **Visual upgrade:** Dress with real kitchenette counter, sink, coffee maker, vending machine, refrigerator, 3 lunch tables with chairs (replacing bare slabs), bulletin board with `fire_assembly.png`, and wet floor sign. |
| **Staff Exit Corridor** | `X: -72..-50`<br>`Z: -12..-8` | `22 × 4` | `3.25` | `Concrete` / `plaster_new` | `Reverb_Corridor` | • Accessed via framed Lobby archway `X = -50, Z = -11..-9` (keep Lobby planter clear at `(-45.5, 0, -5.5)`!).<br>• **Fix visual audit:** Above the collapse point (`X = -64.5, Z = -10.0`), author a **real jagged broken ceiling slab opening** with bent rebar, dangling conduit, and rain/dust pouring down once `collapse` triggers (instead of piling debris below an intact flat ceiling!).<br>• **Fix `DOOR-9`:** Align exterior `staff_exit_door` strictly to `X = -72.0, Z = -10.6..-9.0`. |
| **Gift Shop (Showcase)** | `X: -72..-50`<br>`Z: -32..-12` | `22 × 20` | `3.45` | `Concrete` / south glass facade | `Reverb_OfficeTight` | Behind south glass wall (`Z = -32.0`); shelves, counter, park merchandise signage; brightly lit during Opening Day cutscene. |
| **Lobby Atrium** | `X: -50..-12`<br>`Z: -32..+19` | `38 × 51` | `11.0` (skylight) | `Concrete` (wet in storm) / `boardform` | `Reverb_AtriumLarge` | • **Fix `POS-11`:** Align steel truss skylight roof strictly to **`X = -50.0..-12.0`** (`6 × 6.33 m` bays) — zero 3 m west gap, zero 3.35 m east overhang, zero east wall light-leak slit (`Y = 3.4..3.8` fully sealed!).<br>• **Fix visual audit:** Add real architectural steel/glass **staircase connecting Lobby floor to North Mezzanine** (`Y = 4.6, Z = +17.3`).<br>• **Fix visual audit (`trex_skeleton`):** Place 12 m T. rex fossil cast on beveled stone museum plinth `(-31.0, 0.6, -5.0)` with **4 dramatic warm museum uplights** (visible in both Opening Day and faintly lit by emergency bounce in blackout) and compound primitive colliders (`COL-8`).<br>• Replace rustic park log benches with sleek modern concrete/teak atrium benches + `planter_concrete`s.<br>• **Opening Cutscene:** Retract corrugated security shutter (`X = -35..-29, Z = -31.6`) **OPEN** during golden-hour daytime; drop shutter jammed + sparking motor `(-28.6, 3.6, -31.4)` + storm debris + broken skylight pane shafts during blackout!<br>• **Fix `UX-5`:** Illuminated signs on Lobby walls clearly labeling `← STAFF CORRIDOR` (`X = -31, Z = +19`) and `STAFF EXIT ←` (`X = -50, Z = -10`). |
| **Feed Prep Hall** | `X: -12..+8`<br>`Z: +3..+19` | `20 × 16` | `3.45` | `Tile` (`floor_tiles_wet`) / `tiles_white` | `Reverb_TileWet` / `vc_wing` | • **Fix `COL-1`/`DOOR-1`:** Eliminate the non-colliding tile veneer behind `door_wing` (`Z = +18.85`). Wall `Z = +19.0` has a true framed opening at `X = -2.6..-1.4`.<br>• 3 stainless butcher counters (`X = -8, -3, +2, Z = +11`), overhead steel meat rail (`X = +5.5`) with 4 `hanging_meat`s **seated flush on steel hooks** (`POS-13`), Keeper's note `(4.2, 0.92, +14.0)`. |
| **Cold Storage** | `X: -12..-2`<br>`Z: -9..+3` | `10 × 12` | `3.45` | `Tile` (`floor_tiles_wet`) / `tiles_white` | `Reverb_TileWet` | Cold blue frosted light, vapor/mist local volumetric fog, 6 hooks w/ 3 `hanging_meat`s, Wing Raptor initial spawn `(-7.0, 0.0, -4.0)`. |
| **Vet Clinic** | `X: -2..+8`<br>`Z: -9..+3` | `10 × 12` | `3.45` | `Tile` (`floor_tiles_wet`) / `tiles_white` | `Reverb_TileWet` | Stainless exam table `(3.0, 0.0, -3.0)`, medical supply cabinets `(6.9, 0.0, +1.0)`, overturned broken `feeding_cage` `(0.0, 0.0, -7.0)` with claw decals. |
| **Wing South Corridor** | `X: -12..+8`<br>`Z: -13..-9` | `20 × 4` | `3.45` | `Tile` (`floor_tiles_wet`) / `tiles_white` | `Reverb_TileWet` | • **Fix `COL-2`/`DOOR-2`:** Eliminate the non-colliding tile veneer at `X = 7.82`! Wall `X = 8.0` has a **true `3.0 × 3.0 m` open steel-framed loading bay doorway** (`Z = -12.5..-9.5`) with jammed corrugated `dock_roller` resting at `Y = 1.25 m` (clear crouch gap underneath), guide tracks, illuminated green `EXIT` sign above, and volumetric blue storm light + rain streaks visibly pouring through the gap from the Loading Dock! |
| **Covered Loading Dock** *(Ch. 3 Spawn)* | `X: +8..+22`<br>`Z: -18..-2` | `14 × 16` | `5.2` (canopy) | `Concrete` (`concrete_ext`) | `Reverb_SemiExterior` | `utility_truck` `(15.5, 0.0, -5.5)`, pallets/crates/drums (`Z = -16.6`), Ch. 3 spawn `(18.0, 0.05, -10.0, yaw = +90°)`, opens east at `X = +21.5, Z = -11.5..-6.5` into Service Paths. |

### 1.5 Zone 2 — Service Paths Blueprint (`Y = 0.0`, Walls `5.0 m`)
- **Main Route Polyline (Unity `X, Z`):** `(21.5, -9)` → `(40, -9)` → `(40, +14)` → `(64, +14)` → `(78, -2)` → `(91, -4)` (corridor clear width `4.6 m`, `half-width = 2.3 m`).
- **CRITICAL FIX (`DOOR-7`) — Open Both North T-Branches (`Z = +14 → +34`):**
  - Cut clean `4.6 m` wide openings in the north retaining wall at **`X = 37.7..42.3, Z = +16.3`** and **`X = 61.7..66.3, Z = +16.3`**.
  - **Delete** any floating backdrop ceiling slab (`Y = 4.75`) over the branches so open stormy sky, rain, and hanging jungle canopy silhouette the passages!
  - **North Branch 1 (`X = 40, Z = +14..+34`):** Contains upgraded `wrecked_kiosk` `(40.6, 0.0, +20.5)` with flickering neon/emergency light + grounded `fallen_log` blocking `Z = +26.0` + dramatic lighting so player clearly sees the ruined staff break kiosk and dead end.
  - **North Branch 2 (`X = 64, Z = +14..+34`):** Contains chainlink security gate + `STAFF ONLY` sign `(64.0, 0.0, +24.0)` and the **scripted raptor sprint crossing** from `raptor_cross_a (64.0, 0.0, +30.0)` → `raptor_cross_b (64.0, 0.0, -6.0)` illuminated by a timed lightning flash + sparking `lamp_post`!
- **Prop Grounding & Foliage Fixes (`POS-6`, `POS-14`, Visual Audit):**
  - Inset all Service Path `lamp_post`s cleanly `0.35 m` inside the concrete curb (`half-width - 0.35 m`) — zero poles buried 90% in retaining walls.
  - Place visible `blood_smear` + raptor claw decals directly on the interior retaining wall along the bend at `(65.8, 1.35, +11.8)` facing the player's approach.
  - Keep all palm trees, ferns, and monstera strictly **outside** the concrete walls (`> 0.8 m` outside wall outer faces) with `hanging_vines` draped organically over the top 5 m concrete parapet edge.
- **Paddock 4 Service Gate (`X = 91.0, Z = -7.1..-0.9`):**
  - South leaf solid (`Z = -6.5..-3.4`), North leaf bent upward (`Z = -3.38..-1.42`, clean `1.22 m` bottom clearance for crouched passage at `Z = -2.4`).
  - Place dead `gate_control_box` cleanly mounted on a concrete bollard/post at `(89.6, 0.0, -6.0)` (not buried in wall).
  - **Fix `POS-6`:** Angle `fallen_log` with one end firmly embedded in the muddy earth bank `(93.4, 0.0, -0.4)` and trunk resting directly across the bent top corner of the north steel gate leaf `(91.2, 2.35, -2.2)`.

### 1.6 Zone 3 — Paddock 4: Tyrant Valley & Substation 3 Blueprint
- **Seamless Unified Unity Terrain (Fixes Visual Audit Voids & Floating Cliffs):**
  - Replace Godot's disconnected flat slabs + floating heightfield with **ONE continuous sculpted Unity Terrain** (`1024 × 1024 m` spanning `X: -320..+704`, `Z: -440..+584`, `heightmapResolution = 1025`), seamlessly blending Titan Meadows (`Y ≈ -6.2`), Visitor Center plateau (`Y = 0.0`), Service Paths perimeter, Tyrant Valley bowl (`Y = -1.6 .. +2.3`), Substation 3 pad (`Y = 2.3`), Old Yard plateau (`Y = 1.0`), and the Eastern Coastal Drop-Off down to `Y = -40.0` ocean floor. **Zero world-edge voids; zero 4 m floating cliffs.**
  - **4 Terrain Paint Layers (Splatmap-Tagged for Footsteps!):** `0 = Mud/Dirt` (`Mud`), `1 = Tropical Grass` (`Grass`), `2 = Wet Gravel Road` (`Gravel`), `3 = Basalt Rock/Cliff` (`Concrete`).
- **Service Road 3 Polyline (Unity `X, Z`):** `(91, -3.5)` → `(120, 0)` → `(160, +8)` → `(200, +4)` → `(240, 0)` → `(262, 0)` → `(310, -8)` → `(360, -20)` → `(401, -22.8)`. Painted cleanly on the Terrain splatmap (terminating sharply at Substation 3's concrete apron edge `X = 250` & `X = 286` — **zero gravel bleeding across the concrete pad!**).
- **Stream & Walkable Culvert Bridge (Fixes `POS-7`):**
  - Stream centerline: `stream_x(z) = 205 + 18 * sin(-z / 45) + 6 * sin(-z / 13)`, shallow water plane (`HDRP Water` / animated wet-ripple shader, tagged `Water` for splash footsteps!).
  - Where Service Road 3 crosses the stream (`X = 201, Z = +4`), build a **real concrete box-culvert bridge** with open `1.4 m` crouchable underpass tunnel + guardrails — doubling as an atmospheric T. rex hide spot!
- **7 Tall-Grass Concealment Patches (Unity `X, Z`, radius `m`):** `(140, -16) r=17`, `(152, +26) r=14`, `(226, +32) r=15`, `(232, -24) r=13`, `(320, +10) r=18`, `(352, -44) r=15`, `(118, +20) r=10`. Each has a `Trigger` collider (`TallGrassZone`) + instanced Poly Haven grass/ferns with GPU player/T. rex trampling!
- **Valley Trees & Landmarks:**
  - **Fix `COL-6`:** Every medium/large valley tree has a real `CapsuleCollider` on its trunk (`r = 0.25..0.55 m`) AND `NavMeshObstacle (carve)` so neither the player nor T. rex walks through trees!
  - **T. rex Reveal Trigger:** `(128.0, terrainY + 2.0, 0.0)` (T. rex spawns drinking at stream `trex_drink` `X = 192, Z = +40`).
  - **Broken Bait Pen:** `(150.0, terrainY, +62.0)` with smashed `feeding_cage`.
  - **Overturned Ranger Jeep (`POS-15` + 5/5 Doc Fix):** `(182.0, terrainY, -22.0)` firmly seated to terrain normal (`roll = 92°`), blinking amber hazard lights (HDRP volumetric point lights), plus visible glowing **dashboard radio handset** inside the cab at `(183.5, terrainY + 0.9, -21.0)` — interacting plays Ranger audio log AND calls **`GameStateManager.AddDocument("jeep_radio")`** so `5/5` documents is achievable!
  - **4 Watchtowers:** `(108, +150)`, `(236, -150)`, `(382, +140)`, `(140, -120)` with sweeping volumetric searchlight beams (dying in blackout except faint red aircraft warning beacons).
  - **T. rex 9-Point Patrol Loop (Unity `X, Z`):** `(176,+58)`, `(212,+40)`, `(238,+82)`, `(300,+58)`, `(334,-26)`, `(284,-62)`, `(222,-52)`, `(168,-34)`, `(150,+18)`.

- **Substation 3 Compound (`center = (268.0, 2.3, 0.0)`, pad `X: 250..286, Z: -17..+17`):**
  - Perimeter high-voltage chainlink fence with West Gate opening (`X = 250, Z = -3.2..+3.2`, checkpoint `sub_checkpoint` at `(252.0, 2.35, 0.0, yaw = +90°)`) and East Gate opening (`X = 286, Z = -3.2..+3.2`).
  - **Thick `SubBlocker` (`Layer: DinoBlocker`, `2.5 m` thick outside fence at `X = 247.5..288.5, Z = ±19.5`):** Prevents T. rex legs/snout from EVER clipping into the compound until the dramatic `Fence_Bash` smash destroys the west/north-west fence section on chase start!
  - **Every Machine Grounded, Oriented & Always Inspectable (`POS-1..4`, `UX-4`):**
    1. **Breaker #1:** `(256.0, 2.3, +13.0)`, facing **SOUTH (`180°`, inward)**, blinking red LED → green on reset.
    2. **Breaker #2 (`POS-4` FIX):** `(258.0, 2.3, -13.0)`, rotated **`0°` facing NORTH (inward toward walkway!)** — never facing the south fence!
    3. **Breaker #3:** `(282.0, 2.3, +13.0)`, facing **SOUTH (`180°`, inward)**.
    4. **Fuel Tank & Manual Hand Pump (`POS-3` FIX):** Yellow horizontal diesel tank `(266.0, 2.3, +12.5)` seated cleanly on **two bolted concrete cradle saddles**, connected by braided rubber fuel hose to floor-bolted steel stand holding `FuelPump` at `(263.8, 2.3, +11.0)`.
    5. **Diesel Generator & Starter Panel (`POS-3` FIX):** `generator_diesel` grounded flush on concrete vibration plinth `(272.0, 2.3, +9.0)`; steel grating walkway (`Surface: Metal`, real collider!) at `(272.0, 2.33, +6.6)`; Starter Control Panel mounted on **bolted steel legs** at `(273.5, 2.3, +7.1)` — **prompt clears immediately once 3rd pull succeeds!**
    6. **Switch Cabinet & Transfer Lever (`POS-1` FIX):** `switch_cabinet` grounded at `(279.0, 2.3, -7.0)`; heavy animated `TransferLever` physically bolted onto an **attached steel pedestal housing** at `(277.6, 2.3, -7.0)` connected directly to the cabinet by dual steel conduits!
    7. **Mounted Lighting (`POS-2` FIX):** All `lamp_bulkhead` fixtures bolted flush onto concrete posts or switch-cabinet steel mast; 4 towering `8 m` `aux_sub` sodium floodlight masts at `(252, +15)`, `(284, +15)`, `(252, -15)`, `(284, -15)` with volumetric fog cones.
    8. **Green Escape Route Beacons (`POS-8` FIX):** `aux_route` sequential green strobe bollards start **STRICTLY EAST of Substation 3 (`X = 288, 304, 320, 336, 352, 368, 384, 398` along Service Road 3 → Old Yard Squeeze Gap `(401, 1.0, -22.8)`)** — NEVER west of Substation 3!

### 1.7 Zone 4 — Old Maintenance Yard (1990s) Blueprint (`Y = 1.0`)
- **Perimeter Walls (`5.0 m` weathered concrete, `X: 401..505, Z: -95..+50`):**
  - **West Wall (`X = 401.0`):**
    - **Squeeze Gap (`Z = -23.6..-22.0`):** Clear `1.6 m` wide pedestrian gap (blocks T. rex via `DinoBlocker` slab!). **Fixes:** (1) **Delete the hovering concrete cube** above `(401, 6.2, -22.8)` — replace with a proper weathered concrete lintel header seamlessly integrated into the wall; (2) Hinge the bent rusty pedestrian door **outward** outside the `1.4 m` clear gap with a real `BoxCollider` (`COL-5`).
    - Chained vehicle gate (`Z = -36..-28`, locked).
  - **Warehouse B (`X: 414..456, Z: +4..+36, h = 8.0`):** Corrugated steel shell with 2 deliberate storm-torn roof holes (localized rain shafts), open east doorway (`Z = +18..+24`) and south doorway (`X = 429..436`), `warehouse_b_decal.png` stencil on facade, racks, `utility_truck`, crates/pallets/drums.
  - **Workshop / Office Block (`X: 412..462, Z: -22..-10, h = 3.5`):**
    - **Central Pipe Corridor (`Z: -18..-14`):** West entry (`X = 412, Z = -16.8..-15.2`); **East Exit (`DOOR-3` FIX):** `workshop_door_e` hinge cleanly placed at **`(462.0, 1.0, -15.2)` spanning `Z = -16.8..-15.2`** inside a real cut steel frame (never buried in the wall!).
    - **1996 Site Plan Map (`map_preserve_1996.png`):** Mounted on south corridor wall `(442.0, 2.2, -17.85)` lit by battery emergency sconce + Vance VO trigger (`vo_vance_05`).
    - **Pipe & Lighting Fixes (`POS-2`, `POS-5`):** Route all 5 overhead industrial steam pipes cleanly into wall flanges; mount upgraded pipe-flange `industrial_valve_wheel`s directly on horizontal pipe T-junctions; replace ceiling-floating wall bulkheads with **suspended industrial pendant/cage strip fixtures** mounted to steel ceiling joists.
    - **Partitioned North & South Rooms (Fixes Visual Audit 50 m Empty Hallways):**
      - Partition North Wing (`Z: -14..-10`) into **Site Foreman's Office** (`X: 412..432`, accessed via `office_door` at `X = 422..423, Z = -14.0`, desk `(420, 1.0, -11.2)`, CRT, `log_halvorsen`, filing cabinets) and **Parts Archive** (`X: 432..462`, accessed via open doorway `X = 442..443, Z = -14.0`, shelving, rusted parts).
      - **Fix `DOOR-6`:** Cut **6 real recessed broken industrial multi-pane windows** along North Wall `Z = -10.0` with dirty cracked glass (`glass_dirt.png`), mullions, and storm moonlight streaming across the office floor!
      - Partition South Wing (`Z: -22..-18`) into **Electrical/Battery Closet** (`X: 412..438`, accessed via `tool_door` at `X = 432..433, Z = -18.0`) and **Tool Workshop** (`X: 438..462`, accessed via `X = 452..453, Z = -18.0`, workbench `(452, 1.0, -21.2)`, `toolbox_case`, pegboard).
    - **Zero Indoor Foliage:** Strictly cull all trees/ferns/grass inside `X: 411..463, Z: -23..-9` and `Warehouse B` footprint!
  - **Exit Court & E-0 Service Exit (`X: 462..505, Z: -67..-22`):**
    - Covered porch (`X = 466.5, Z = -55..-29`), south Loading Dock (`Z = -67..-59`), 3 `aux_yard` floodlight poles.
    - **E-0 Double Gate (`X = 505.0, Z = -50.0..-42.0`, `POS-16` FIX):**
      - `e0_leaf_a` hinge `(505.0, 1.0, -42.0)` (north leaf, `4.0 m` wide) & `e0_leaf_b` hinge `(505.0, 1.0, -50.0)` (south leaf, `4.0 m` wide).
      - **CRITICAL FIX:** Both leaves swing **OUTWARD EAST (`+X` onto the Coast Road: `leaf_a` rotates `-95°`, `leaf_b` rotates `+95°` around Y)** so the sprint line from the `gate_control_box` `(503.8, 1.0, -52.6)` straight through the gate center `(505.0, 1.0, -46.0)` is 100% unobstructed!
      - Brightly illuminate the `gate_control_box` with an amber/green status cage lamp + `SERVICE EXIT E-0` backlit sign `(504.6, 5.2, -52.3)` + `e0_exit_decal.png` stencil on the asphalt.
  - **2 Yard Raptors (Unity `X, Z`):**
    - Raptor A spawns `(445, 1.0, +20)`, patrols `(445,+12) → (472,+20) → (480,-2) → (452,-4)`.
    - Raptor B spawns `(470, 1.0, -30)`, patrols `(470,-30) → (486,-52) → (472,-78) → (452,-40)`.

### 1.8 Zone 5 — Backdrop, Coast Road & Dawn Ending Vista (`X: 505..548+`)
- **Coast Road (`X: 505..547, Z: -49.8..-42.2`, `Y = 1.0`):** Fenced rain-slick asphalt ending at timber/steel coastal overlook rail (`X = 546.0`, `ending_view = (543.0, 1.05, -46.0)`).
- **CRITICAL FIX (Dawn Ocean Vista & Ending Camera):**
  - **Open the Eastern Mountain Ring completely for `X > 520, Z: -240..+160`**! Frame the view with dramatic Poly Haven `coastal_cliff_01` headlands to the North-East and South-East, opening onto an expansive **shimmering HDRP Ocean plane (`Y = -40.0`, animated Gerstner waves + golden sunrise specular sun-path + white coastal surf foam)**.
  - Add a **pulsing amber/white Coast Guard Evac Beacon / distant rescue boat or helicopter navigation strobes** out over the sunlit morning bay to sell survival and rescue!
  - **Ending Camera Choreography:** When the final distant T. rex roar booms from inland (`(380, 10, -20)`), instead of spinning the player camera at eye-level into the blank east wall of the Old Yard, smoothly blend to a **Cinemachine crane shot rising from `(543, 2.7, -46)` → `(536, 15.0, -46)`**, looking back **over** the yard walls across mist-filled emerald Tyrant Valley & distant Visitor Center towers before sweeping gracefully back toward the golden ocean sunrise!

### 1.9 Master Door, Gate & Archway Registry (All 27 Openings — Unity Coordinates)
Every door uses `Assets/Prefabs/Architecture/Doors/` with a matching beveled jamb frame, header lintel, real hardware, and verified opening coordinates.

| # | Door ID | Unity Hinge `(X, Y, Z)` | Opening Range (Unity) | Size `W×H` | Clear Swing Direction | Sound Prefix | Lock / Behavior | Godot Defect Eliminated |
|---|---|---|---|---|---|---|---|---|
| 1 | `door_security` | `-63.0, 0, +22.0` | `X: -63.0..-62.0` | `1.0×2.2` | Inward North (`+Z`) | `door_interior` | Auto-opened Ch. 1 | Dado rail segmented (`COL-3`) |
| 2 | `door_lockers` | `-53.5, 0, +22.0` | `X: -53.5..-52.5` | `1.0×2.2` | Inward North (`+Z`) | `door_interior` | Unlocked | Dado rail segmented (`COL-3`) |
| 3 | `door_control` | `-37.8, 0, +22.0` | `X: -37.8..-36.2` | `1.6×2.2` | Double Inward (`+Z`) | `door_interior` | Auto-opened on clock-in | Dado rail segmented (`COL-3`) |
| 4 | `door_server` | `-22.5, 0, +22.0` | `X: -22.5..-21.5` | `1.0×2.2` | Inward North (`+Z`) | `door_interior` | Unlocked | Dado rail segmented (`COL-3`) |
| 5 | `door_records` | `-9.5, 0, +22.0` | `X: -9.5..-8.5` | `1.0×2.2` | Inward North (`+Z`) | `door_old` | Unlocked | Dado rail segmented (`COL-3`) |
| 6 | `door_storage` | `+3.5, 0, +22.0` | `X: +3.5..+4.5` | `1.0×2.2` | Locked (glass window) | `door_interior` | Permalocked | Real dressed Storage Closet behind (`DOOR-8`) |
| 7 | `door_break` | `-61.5, 0, +19.0` | `X: -61.5..-60.5` | `1.0×2.2` | Inward South (`-Z`) | `door_interior` | Unlocked | Dado rail segmented (`COL-3`) |
| 8 | `arch_lobby` | `-32.2, 0, +19.0` | `X: -32.2..-29.8` | `2.4×2.4` | Open Framed Arch | — | Always open | Dado rail removed; Lobby signage added |
| 9 | `door_wing` | `-2.6, 0, +19.0` | `X: -2.6..-1.4` | `1.2×2.2` | Inward South (`-Z`) | `door_interior` | Locked until Manual read | **Tile veneer behind door deleted (`COL-1`/`DOOR-1`)** |
| 10 | `arch_staff_exit` | `-50.0, 0, -9.0` | `Z: -11.0..-9.0` | `2.0×2.4` | Open Framed Arch | — | Always open | Planter moved clear; illuminated EXIT sign |
| 11 | `staff_exit_door` | `-72.0, 0, -9.0` | `Z: -10.6..-9.0` | `1.6×2.3` | Outward West (Locked) | `door_metal` | Locked; blocked by collapse | **Aligned 0.0 m error to wall frame (`DOOR-9`)** |
| 12 | `door_cold` | `-7.5, 0, +3.0` | `X: -7.5..-6.1` | `1.4×2.25` | South into Cold (`-Z`) | `door_metal` | Unlocked; Raptor-bashable (4 hits) | Clean swing; clears 1.70 m raptor |
| 13 | `door_vet` | `+2.3, 0, +3.0` | `X: +2.3..+3.7` | `1.4×2.25` | South into Vet (`-Z`) | `door_metal` | Unlocked; Raptor-bashable (4 hits) | Clean swing; clears 1.70 m raptor |
| 14 | `arch_cold_vet` | `-2.0, 0, -2.4` | `Z: -3.6..-2.4` | `1.2×2.25` | Open Framed Arch | — | Always open | Clean opening |
| 15 | `door_cold_s` | `-7.5, 0, -9.0` | `X: -7.5..-6.1` | `1.4×2.25` | North into Cold (`+Z`) | `door_metal` | Unlocked; Raptor-bashable (4 hits) | Clean swing |
| 16 | `arch_vet_s` | `+2.3, 0, -9.0` | `X: +2.3..+3.7` | `1.4×2.25` | Open Framed Arch | — | Always open | Clean opening |
| 17 | `dock_roller` | `+8.0, 0, -11.0` | `Z: -12.5..-9.5` | `3.0×3.0` (`1.25 m` gap) | Vertical Jammed Shutter | — | Crouch under (`1.25 m` gap) | **Tile veneer deleted; visible storm light underneath (`COL-2`/`DOOR-2`)** |
| 18 | `talon_gate` | `-41.0, 0, +42.0` | `X: -41.0..-31.0` | `10.0×7.5` | Slides `+9.5 m East` | `gate_heavy_slide` | Fails OPEN in blackout | **Control Room window excluded from Occlusion (`DOOR-4`)** |
| 19 | `service_gate_p4` | `+91.0, 0, -2.4` | `Z: -6.6..-1.4` | `1.95×4.85` (`1.22 m` gap) | North leaf bent up | `ui_denied` | Crouch under (`1.22 m` gap) | Grounded `fallen_log` (`POS-6`) |
| 20 | `yard_squeeze_gap` | `+401.0, 1.0, -22.8` | `Z: -23.6..-22.0` | `1.6×3.2` | Open squeeze + collided bent leaf outside | — | Blocks T. rex (`DinoBlocker`) | Hovering cube deleted; bent door solid outside lane (`COL-5`) |
| 21 | `arch_workshop_w` | `+412.0, 1.0, -15.2` | `Z: -16.8..-15.2` | `1.6×2.3` | Open Framed Arch | — | Always open | Clean opening |
| 22 | `office_door` | `+422.0, 1.0, -14.0` | `X: 422..423` | `1.0×2.2` | North into Office (`+Z`) | `door_old` | Unlocked; Raptor-bashable | Partitioned Foreman Office behind |
| 23 | `arch_parts_n` | `+442.0, 1.0, -14.0` | `X: 442..443` | `1.0×2.25` | Open Framed Arch | — | Always open | Partitioned Parts Archive behind |
| 24 | `tool_door` | `+432.0, 1.0, -18.0` | `X: 432..433` | `1.0×2.2` | South into Elec Closet (`-Z`) | `door_old` | Unlocked; Raptor-bashable | Partitioned room behind |
| 25 | `arch_tool_s` | `+452.0, 1.0, -18.0` | `X: 452..453` | `1.0×2.25` | Open Framed Arch | — | Always open | Partitioned Tool Room behind |
| 26 | `workshop_door_e` | `+462.0, 1.0, -15.2` | `Z: -16.8..-15.2` | `1.6×2.25` | Outward East (`+X`) | `door_old` | Unlocked; Raptor-bashable | **Exact frame match — never buried in wall (`DOOR-3`)** |
| 27 | `e0_gate_leaves` | `A:(505,1,-42)`<br>`B:(505,1,-50)` | `Z: -50.0..-42.0` | `2×(4.0×3.8)` | **BOTH SWING OUTWARD EAST (`+X`)** | `gate_motor_grind` / `clunk` / `slam` | Hold OPEN `9.0 s` cumulative | **Clear sprint path from control box (`POS-16`)** |

---

## 2. HDRP 17.6.0 Lighting, Atmosphere, Weather & Rendering Spec

### 2.1 Mandatory macOS Metal 4 HDRP Asset Settings (`HDRP_Cretacea_M5Max.asset`)
Configured by `unity-tools-scout` and locked for all workers:
- **Ray Tracing:** **OFF** (`StandaloneOSX` does not support DXR/Metal RT in HDRP 17.6).
- **Lit Shader Mode:** Deferred Only (supports SSS + DBuffer Decals + fast multi-light storm interiors).
- **Volumetric Fog:** **ON** — `Control Mode: Manual`, `Slice Distribution Uniformity: 0.75`, `Depth Slices: 80`, `Fog Resolution: 50% (Half)`, `Reprojection: ON`, `Denoising: ON`. Use `LocalVolumetricFog` volumes in Cold Storage, Atrium rain shafts, Tyrant Valley stream mist, and Substation compound.
- **Global Illumination:** **Adaptive Probe Volumes (APV)** (3-brick resolution `1 m / 3 m / 9 m` covering Visitor Center, Paths, Substation, Yard) **+ Half-Resolution Denoised SSGI** (`Quality: Medium`, `Max Ray Steps: 32`).
- **Reflections:** **Screen Space Reflections (SSR)** (Max Smoothness Fade `0.55`, wet concrete/tile floors shine!) **+ Box-Projected Reflection Probes** inside every Visitor Center room, Loading Dock, Workshop, and Warehouse B (**completely eliminates Godot's skybox-mirroring interior floors!**).
- **Ambient Occlusion & Shadows:** **GTAO / SSAO** (Full-res off / Half-res denoised, radius `0.85 m`) + **Contact Shadows** (critical for grounding props & dinosaur feet!) + **Shadow Atlas `4096 × 4096`** (Punctual) + **Directional Cascade Count `4` (`2048 px`)** + **Shadow Caching** for static emergency lights.
- **Decals:** **DBuffer Decals ON** (`Albedo + Normal + MaskMap`, `Max Draw Distance: 60 m`) for T. rex footprints, raptor tracks, blood smears, wall grime, moss, cracks, and stenciled signs (`warehouse_b_decal`, `e0_exit_decal`, `hazard_tape`).
- **Subsurface Scattering (SSS):** **ON** (`High Quality`) with a dedicated `SSS_DinoSkin.asset` Diffusion Profile for wet organic dinosaur hide & frills.
- **Upscaling & Post-Processing:** **Spatial-Temporal Post-Processing (STP)** at `0.85` render scale (optional user setting `Quality / Balanced / Native`), **ACES Tonemapping**, subtle Film Grain (`0.12`), Chromatic Aberration (`0.04` during damage/roar), Bloom (`Threshold: 1.15, Intensity: 0.28` for emergency beacons/sparks/flashlight rim), explicit **Fixed / Auto-Clamped EV100 Exposure Volumes** (`InteriorDark: EV -1.0..+2.0`, `ExteriorStorm: EV 0.0..+3.0`, `GoldenDay: EV 11.5`, `DawnCoast: EV 9.5`).

### 2.2 Atmosphere Presets (`EnvironmentDirector.cs`)
| Preset ID | Sky HDRI (4K CC0) | Sky Exposure (EV) | Key Light (Lux / Temp) | Volumetric Fog (Base / Atten Dist / Albedo) | Rain / Lightning |
|---|---|---|---|---|---|
| `GoldenDay` *(Opening)* | `belfast_sunset_puresky` | `11.5` | Sun `65,000 Lux`, `4200 K` | Base `2 m`, Atten `180 m`, Warm `#D8B894` | Rain `0%` / Lightning OFF |
| `DuskTransition` *(Opening)* | `approaching_storm` | `7.5` | Sun `8,000 Lux`, `3200 K` | Base `4 m`, Atten `120 m`, Slate `#6A7385` | Rain `20%` / Lightning OFF |
| `NightCalm` *(Ch. 1 Pre-Blackout)* | `kloppenheim_02_puresky` | `0.8` | Moon `1.2 Lux`, `7500 K` | Base `3 m`, Atten `75 m`, Blue `#223045` | Rain `45%` / Rate `0.5` |
| `NightStormBlackout` *(Ch. 1–6)* | `kloppenheim_07_puresky` | `0.35` | Moon `0.75 Lux`, `8200 K` | Base `2.5 m`, Atten `42 m`, Storm `#162230` | Rain `100%` / Rate `1.0` |
| `DawnCoast` *(Ending)* | `qwantani_moonrise_puresky` + Sunrise tint | `9.2` | Low Sun `24,000 Lux`, `3600 K` | Base `6 m`, Atten `220 m`, Gold-Mist `#E6B98A` | Rain `0%` / Lightning OFF |

### 2.3 Light Group Power Matrix (`LightGroupController.cs`)
Every light (plus its paired emissive fixture mesh / status wall / map quad) registers to one `LightGroupId`. **All key interior emergency lights cast real-time or cached shadows (`VIS-3` fix — zero red light bleeding through concrete walls!).**

| Group ID | Opening Day | Night Calm (Ch. 1) | Blackout (Ch. 1–5) | Aux Power On (Ch. 5–6) | Special Rules |
|---|---|---|---|---|---|
| `vc_main` | ON | ON | **OFF** | OFF | Controls **Control Room `status_wall` + `map_park_modern` emission (`POS-10` fix!)** + Lobby fossil uplights (dim backup `15%` in blackout so silhouette reads!). |
| `vc_emergency` | OFF | OFF | **ON** | ON | Rotating/pulsing shadow-casting red beacons (`beacon_red`), corridor red sconces. |
| `vc_exit` | ON | ON | ON | ON | Battery-backed green `EXIT` & directional signs. |
| `plaza` | OFF | ON | **OFF** | OFF | Plaza bollards & facade wash. |
| `paths` | OFF | ON | **OFF** | OFF | Service Path overhead lamps (except `paths_flicker_lamp` spark fault!). |
| `dock_main` | OFF | ON | **OFF** | OFF | Loading dock overhead floodlights. |
| `talon` | ON | ON | **OFF** | OFF | Talon Ridge pen floodlights (die dramatically 1.4 s before emergency lights click on!). |
| `paddock_battery` | OFF | ON | ON | ON | Watchtower red aircraft beacons, overturned Jeep hazards, flickering road beacons. |
| `sub_battery` | OFF | ON | ON | ON | Blinking red breaker LEDs, pump lamp, generator fault lamp (turn solid green per stage!). |
| `yard_battery` | OFF | ON | ON | ON | Office emergency lamp, 1996 site plan light, E-0 control box beacon. |
| `aux_sub` | OFF | OFF | OFF | **ON** | 4 Substation 3 `8 m` sodium floodlights (`35,000 lm`, volumetric dimmer `1.8`). |
| `aux_route` | OFF | OFF | OFF | **RIPPLE ON** | **Strictly EAST of Substation (`X >= 286 → 401`)**, ripples on at `0.12 s` intervals (`POS-8` fix!). |
| `aux_yard` | OFF | OFF | OFF | **ON** | Old Yard floodlights + E-0 green gate overhead lamp. |

### 2.4 HDRP Weather, Rain-Occlusion & Live CCTV Systems
1. **VFX Graph Storm Rain (`VFX_StormRain.vfx`):**
   - Follows playerXZ (`28 × 28 m` box volume around camera) + dedicated localized `VFX_LobbySkylightRain.vfx` emitters positioned strictly under the 3 broken Lobby roof panes during blackout.
   - **Indoor Occlusion Mask (Fixes Visual Audit indoor white squares!):** Top-down orthographic Depth Camera (`RainOcclusionCam`, `Layer: RainBlocker`, `512 × 512`, updates only on zone change or roof breach!) passed to VFX Graph so rain particles & splashes immediately die if `particle.y < roofDepthHeight`!
   - **Ground Splashes:** Textured quad using **`splash_sheet.png` (4-frame alpha-blended flipbook)** — never untextured white square quads!
   - **Dynamic Wetness & Puddles (`SG_WetSurface_HDRP.shadergraph`):** Reads global `_GlobalWetness` (`0..1`), `_RainIntensity`, `puddle_mask.png`, and `rain_ripples_normal.png` (4×4 animated flipbook) on outdoor roofs/roads/pads (masked to `0` on dry interior floors via vertex color or material flag!).
2. **Live Security Office CCTV (`SecurityCCTVController.cs`):**
   - 6 fixed HDRP Cameras (`256 × 144` RenderTextures, **rendered round-robin 1 camera every 6 frames = ~10 Hz per screen**, near-zero GPU cost on M5 Max!) showing: (1) Talon Ridge Gate, (2) Tyrant Valley Stream, (3) Main Plaza, (4) Lobby Atrium, (5) Loading Dock, (6) Staff Corridor.
   - On Blackout (`StoryDirector.TriggerBlackout()`), switches all 6 monitor materials to an animated **CRT Static Snow + `NO SIGNAL`** Shader Graph!
3. **Dynamic Foliage Trampling (`FoliageTrampleDriver.cs`, Fixes `VIS-5`):**
   - Updates `Shader.SetGlobalVector("_PlayerTramplePos", new Vector4(px, py, pz, 1.1f))` and `Shader.SetGlobalVector("_TRexTramplePos", new Vector4(tx, ty, tz, 3.2f))` every frame for `SG_FoliageWindTrample.shadergraph`.

---

## 3. AAA Gameplay, Collision, Player, Flashlight & State Machine Contract (`Assets/Scripts/`)

### 3.1 Unity Physics Layers & Collision Matrix
Define exact Layers in `ProjectSettings/TagManager.asset`:

| Layer # | Name | Collides With | NavMesh Bake | Purpose |
|---|---|---|---|---|
| `0` | `Default` / `Environment` | All physical layers | Walkable / Obstacle | Static walls, floors, Terrain, grounded props, tree trunks |
| `6` | `Player` | `Default`, `Door`, `Trigger`, `Interactable` *(ray only)* | Ignored | Player `CharacterController` |
| `7` | `Dinosaur` | `Default`, `Door`, `DinoBlocker` | Ignored | Raptor & T. rex `CharacterController`/Capsule |
| `8` | `Interactable` | Player interaction raycast only (`isTrigger = true`) | Ignored | Focusable interact volumes |
| `9` | `Trigger` | `Player` only (`isTrigger = true`) | Ignored | Story triggers, `InteriorZone`, `TallGrassZone` |
| `10` | `DinoBlocker` | `Dinosaur` ONLY (**ignores `Player`!**) | `Not Walkable` for T. rex | Substation fence barrier (destroyed on chase), Old Yard squeeze gap blocker, electric fences |
| `11` | `Door` | `Player`, `Dinosaur` | **Excluded from bake** (dynamic `NavMeshObstacle` only when locked!) | Hinged doors & E-0 gate leaves |
| `12` | `Viewmodel` | None | Ignored | FPS Flashlight rig rendered exclusively by HDRP Viewmodel Custom Pass |
| `13` | `RainBlocker` | None (RainOcclusionCam only) | Ignored | Invisible roof depth proxies covering every building & canopy |

### 3.2 Core Interfaces & Event Bus (`Cretacea.Core`)
```csharp
namespace Cretacea.Core
{
    public enum CheckpointId { Intro, Night, Records, Dock, Paddock, Substation, Yard }
    public enum SurfaceType { Concrete, Tile, Gravel, Metal, Mud, Grass, Water }
    public enum AtmospherePreset { GoldenDay, DuskTransition, NightCalm, NightStormBlackout, DawnCoast }
    public enum LightGroupId
    {
        VCMain, VCEmergency, VCExit, Plaza, Paths, DockMain, Talon,
        PaddockBattery, SubBattery, YardBattery, AuxSub, AuxRoute, AuxYard
    }
    public enum DinoState { Idle, Patrol, Investigate, Alert, Hunt, Attack, Search, Scripted }

    public interface IInteractable
    {
        string InteractId { get; }
        string PromptText { get; }
        string LockedHint { get; }            // Non-empty when prerequisite unmet (UX-4!)
        float HoldDuration { get; }           // 0 = instant press
        bool PreserveHoldProgress { get; }    // true for E-0 Gate
        bool IsEnabled { get; }
        bool IsLocked { get; }
        Transform FocusAnchor { get; }
        void OnInteract(PlayerController player);
        void OnHoldProgress(PlayerController player, float normalized01);
        void OnLockedInteract(PlayerController player);
    }

    public static class GameEvents
    {
        // Progression & World
        public static event Action<CheckpointId> OnCheckpointReached;
        public static event Action<string> OnObjectiveChanged;
        public static event Action<string, string, float> OnSubtitleRequested;      // speaker, text, dur
        public static event Action<string, float> OnToastMessageRequested;          // text, dur
        public static event Action<string, float> OnSoundCaptionRequested;          // caption, dur
        public static event Action<string> OnDocumentCollected;                     // docId
        public static event Action<string, string, Action> OnShowDocumentRequested; // assetKey, summary, onClose
        public static event Action<LightGroupId, bool, float> OnLightGroupChanged;  // id, on, fadeSec
        public static event Action<AtmospherePreset, float> OnAtmosphereChanged;
        public static event Action<bool, string> OnInteriorChanged;                 // inside, zoneId
        // Player & Stealth
        public static event Action<bool> OnFlashlightAcquiredChanged;
        public static event Action<bool> OnFlashlightToggled;
        public static event Action<float, bool> OnStealthVisibilityChanged;         // visibility 0..2, concealed
        public static event Action<Vector3, float, string> OnNoiseEmitted;          // pos, radius, category
        public static event Action<float, float> OnCameraTraumaRequested;           // trauma, decay
        public static event Action<float> OnDamageFlashRequested;
        public static event Action OnPlayerDied;
        // AI & Audio
        public static event Action<string, DinoState> OnDinoStateChanged;           // dinoId, state
        public static event Action<Vector3, float, Quaternion> OnTRexFootprint;
        public static event Action<string, float> OnMusicStateRequested;            // trackState, fadeSec
        public static event Action<string, bool> OnAmbienceZoneRequested;           // zoneKey, indoors
    }
}
```

### 3.3 `PlayerController` & `FlashlightController` (`Cretacea.Player`)
- **CharacterController Metrics:** `radius = 0.34f`, `stepOffset = 0.35f`, `slopeLimit = 48f`, `skinWidth = 0.04f`.
  - Standing: `height = 1.75f`, `center = (0, 0.875, 0)`, `eyeY = 1.62f`.
  - Crouched: `height = 1.05f`, `center = (0, 0.525, 0)`, `eyeY = 0.98f` (smoothly lerped at `10/s`; standing up runs `Physics.SphereCast` up `0.75 m` with `r = 0.30` against `Environment | Door` so player never clips ceilings/roller-doors!).
- **Movement & Stamina:** Walk `2.7 m/s`, Sprint `5.7 m/s`, Crouch `1.35 m/s`, Accel `10`, Friction `11`, Gravity `18 m/s²`. Stamina drains over `7.5 s` (`StaminaDrainMult = 0.15f` during Substation→Yard T. rex chase), `1.1 s` regen delay, `5.0 s` full recharge, exhausted threshold `0.35`.
- **Battle-Tested Input Ergonomics:**
  - Fresh Sprint stands up a crouched player if headroom is clear.
  - Pressing Crouch *while* holding Sprint latches `_crouchOverSprint` until Sprint is released (seamless sprint-dive under Loading Dock roller & bent Service Gate).
  - Gamepad `L3` click toggles `_sprintLatch` (clears on crouch, exhaustion, or neutral stick `> 0.3 s`; requires forward stick `> 0.6`).
- **Stealth Visibility Formula:**
  $$\text{vis} = (crouching ? 0.55 : 1.0) \times (inGrass ? (crouching ? 0.18 : 0.50) : 1.0) \times (flashlightOn ? 1.90 : 1.0) \times \left(0.70 + 0.60 \cdot \frac{v_{horiz}}{5.7}\right)$$
  - `IsConcealed = inGrass && crouching && !flashlightOn` (`vis ≈ 0.07`). Broadcasts to HUD Stealth Eye every `0.1 s`.
- **Footstep Noise Emission:** Crouch `3 m`, Walk `9 m`, Sprint `24 m` × (`Metal: 1.5`, `Water: 1.3`, `Grass: 0.7`, `Mud: 0.85`, `Concrete/Tile/Gravel: 1.0`).
- **IRONCLAD `FlashlightController` State Rules (Fixes `FL-1/2/3/4` & `COL-4`):**
  1. Single source of truth: `bool ShouldShowViewmodel => HasFlashlight && !GameStateManager.InCutscene && !GameStateManager.IsMainMenu && !IsDead;`
  2. Evaluated on `Awake`, `OnCheckpointLoaded`, `OnCutsceneChanged`, and `SetHasFlashlight(bool)` — **cutscenes NEVER directly set `viewmodelRoot.SetActive(true)`!**
  3. On Ch. 1 start: `HasFlashlight = false`, `IsBeamOn = false` → `flashlight_fps_rig` is **100% disabled**.
  4. On pickup from Security Desk (`flashlight_pickup.fbx`): plays `foley_pickup`, sets `HasFlashlight = true`, triggers `Anim_EquipRaise` (`0.45 s`) + auto-clicks light ON with subtle thumb animation.
  5. **Zero Wall-Clipping:** `flashlight_fps_rig` lives on `Layer 12 (Viewmodel)` rendered by `HDRP_ViewmodelCustomPass` (`ClearDepth = true`, matching lighting probes) **PLUS** a forward SphereCast (`0.65 m`) that smoothly pulls the arm/light back (`-0.18 m Z`, `-12° pitch`) when pressed against a wall/door!
  6. **Light Specs:** HDRP SpotLight (`34 m`, inner `14°`, outer `25°`, `3,200 Lumens`, `5200 K` warm-white, `flashlight_cookie.png`, `VolumetricDimmer = 1.6`, `Shadows = ON`) + local bounce fill PointLight (`1.6 m`, `12 Lumens`, no shadows).

### 3.4 `StoryDirector`, Checkpoints & Complete 5/5 Document Tracking (`Cretacea.Story`)
- **7 Checkpoints (`CheckpointId`):** `Intro` → `Night` (`(-64.5, 0.05, +28.0)`, **`yaw = 0°` North!**) → `Records` (`(-6.0, 0.05, +24.5)`, `yaw = 140°`) → `Dock` (`(18.0, 0.05, -10.0)`, `yaw = 90°`) → `Paddock` (`(93.5, terrainY, -2.5)`, `yaw = 85°`) → `Substation` (`(252.0, 2.35, 0.0)`, `yaw = 90°`, or `(285.0, 2.35, -4.0)` if `aux_power == true` for chase retry) → `Yard` (`(404.0, 1.05, -22.8)`, `yaw = 90°`).
- **All 5 Collectible Documents (Fixes Godot `4/5` Bug!):**
  1. `memo_switchover` — Security Desk `(-65.8, 0.81, +30.9)` (`memo_switchover.png`)
  2. `manual` — Records Annex Desk `(-3.6, 0.82, +27.3)` (`manual_p1.png` + `manual_p2.png`, **required**, grants Master Key & unlocks `door_wing` + unlocks Route Map in Tab Journal!)
  3. `keeper_note` — Feed Prep Counter `(4.2, 0.92, +14.0)` (`keeper_note.png`)
  4. `jeep_radio` — Overturned Ranger Jeep `(183.5, terrainY + 0.9, -21.0)` (plays `vo_ranger_01/02` **AND calls `GameStateManager.AddDocument("jeep_radio")`**!)
  5. `log_halvorsen` — Yard Foreman's Office Desk `(420.4, 1.85, -11.3)` (`log_halvorsen.png`)
- **Contextual Substation State Machine (Fixes `UX-4`):**
  All 6 Substation stations (`Breaker 1/2/3`, `FuelPump`, `GeneratorStarter`, `TransferLever`) have active `IInteractable` components from the moment the player enters Paddock 4:
  - Interacting early displays clear diegetic `LockedHint` (e.g., *"No circuit power — reset all 3 tripped breakers first (1/3)"*, *"Fuel line dry — prime the hand pump first"*, *"Generator offline — start the diesel engine first"*).
  - Active step's LED/lamp blinks amber/red and emits subtle audio hum; completed steps lock solid green and update the Tab Journal checklist.
  - On 3rd Starter pull (`gen_attempts == 3`): disables `GeneratorStarter` interactable cleanly (no lingering `[E] Pull the starter` prompt!), starts `gen_engine_loop`, alerts T. rex into `Hunt`, unlocks `TransferLever` (`2.2 s` hold).
  - On Transfer completion: sets `aux_power = true`, teleports T. rex safely to west fence `(258, terrainY, +22)` if stuck at east gate, ripples `aux_route` green lights strictly EAST (`X >= 286`), plays `Fence_Bash` + `fence_smash` destroying `SubBlocker`, sets `StaminaDrainMult = 0.15f`, and starts the rubber-banded chase to `yard_gap`.
- **E-0 Gate Escape (`Zone 4`):**
  - `HoldDuration = 9.0f`, `PreserveHoldProgress = true` (letting go to hide preserves high-water progress!).
  - Strains leaves to `18%` while grinding (emits `25 m` machine noise drawing Yard Raptors); at `100%`, swings both leaves **OUTWARD EAST (`+X`)** over `1.0 s` with `gate_open_clunk`.
  - Crossing `(515.0, 1.0, -46.0)` slams leaves shut behind player (`1.4 s` + `gate_close_slam`), scripts chasing raptors slamming into the closed mesh (`chainlink_impact`), fades into `ending` music, and triggers `EndingCutsceneDirector` at `(537.0, 1.0, -46.0)`.

---

## 4. AAA UX, HUD, Menus & Input Architecture (`Cretacea.UI`)

### 4.1 InputSystem Action Map (`Assets/Input/CretaceaControls.inputactions`)
- **Device Auto-Detection:** Seamlessly switches all HUD button prompts (`[E]` / `[X]`, `[F]` / `[Y]`, `[C]` / `[B]`, `[Shift]` / `[LB/L3]`, `[Tab]` / `[View]`, `[Esc]` / `[Menu]`) on last-used device using clean crisp glyph sprites (`key_*.png` + Xbox icon set).
- **Bindings:** Move (`WASD` / Left Stick), Look (`Mouse` / Right Stick), Sprint (`Left Shift` / `LB` or `L3` latch), Crouch (`C` or `Ctrl` / `B`), Flashlight (`F` / `Y`), Interact (`E` / `X`), Journal/Map (`Tab` / `View`), Pause (`Esc` / `Menu`), Skip Cutscene Hold (`Space` / `X` hold `1.2 s`).
- **Gamepad Polish:** Menu D-Pad/Stick navigation with `0.35 s` initial delay + `0.12 s` repeat rate; dual-motor Xbox rumble driven by `CameraTrauma` (roars, ceiling collapse, fence smash, generator start, lightning close).

### 4.2 HUD Layout & Visual Polish (Fixes `UX-1..6` & Visual Audit Layering Bugs)
Built in **Unity UI (uGUI Canvas, Scale With Screen Size `1920 × 1080`)** utilizing the 11 migrated HUD art textures (`objective_bar.png`, `damage_red.png`, `vignette.png`, `interact_ring.png`, `key_*.png`) and SIL OFL fonts (`Barlow Condensed` headers, `Barlow` body, `Courier Prime` timestamps/terminals, `Stardos Stencil` warnings):
1. **Persistent-Subtle Top-Left Objective Banner (`UX-1`):**
   - Displays current objective continuously at calm `55%` opacity (pulses to `100%` for `4.5 s` whenever updated) with a tiny `[Tab] Journal / Map` chip underneath — players are NEVER left wondering what to do in the dark.
2. **Dynamic Stealth / Visibility Eye (`UX-2`):**
   - Centered quietly at bottom-above-subtitles (or top-center below compass notch): minimalist stylized eye icon + subtle darkened screen vignette that smoothly interpolates across 3 states from `PlayerController.Visibility`:
     - **Closed Eye (Muted Cool Grey):** `Concealed` (`vis <= 0.15`, crouched in grass with light off).
     - **Half-Open Eye (Amber Tint):** `Shadowed` (`0.15 < vis <= 0.85`).
     - **Wide Eye (Soft Crimson Pulse):** `Exposed` (`vis > 0.85` or active flashlight/sprint near predator).
   - Auto-fades out completely during safe Chapter 1 pre-blackout and cutscenes so it never clutters non-stealth moments!
3. **Minimal Bottom-Left Equipment & Progress Strip (`UX-3`):**
   - Only fades in on state change or while Journal is open: Flashlight status (`[F] ON/OFF`), Master Key icon (once Manual read), and active Substation checklist progress (`Breakers 2/3`).
4. **Center Interaction Reticle & Radial Hold Ring (`UX-6`):**
   - Soft `4 px` dot expanding on focus; crisp radial fill ring (`interact_ring.png`) for hold interactions; clear locked-hint subline in warm amber when prerequisite is unmet (`UX-4`); dedicated `E-0 GATE: 42% (PROGRESS SAVED)` bar during finale hold.
5. **Tactical Journal Overlay (`Tab` / `View` Toggle — `UX-1/3`):**
   - Semi-transparent dark blurred panel (`TimeScale` remains live or optional pause toggle — default **non-pausing diegetic glance** with crisp backdrop blur, Esc closes):
     - **Left Pane — Route Map:** Displays **`hud_minimap_route.png`** (unlocked in detailed form after reading the 1996 Manual, shows Facility layout before!) with a subtle pulsing amber player-zone marker (`Visitor Center` → `Service Paths` → `Tyrant Valley` → `Substation 3` → `Old Yard` → `Exit E-0`).
     - **Right Pane — Objective & Checklist + Collected Docs (`X/5`):** Live multi-step Substation 3 checklist (`[✓] Reset 3 Breakers`, `[ ] Prime Fuel Pump`, `[ ] Start Generator`, `[ ] Throw Transfer Lever`) + clickable tabs to re-read any of the 5 collected documents (`memo_switchover`, `manual`, `keeper_note`, `jeep_radio` transcript, `log_halvorsen`)!
6. **Subtitles, Sound Captions & Safe-Area Layering (Fixes Visual Audit UI Overlaps):**
   - Subtitles sit cleanly centered bottom (`Y = 72 px` above safe margin) with dark gradient pill — **never overlapping the flashlight hand** (which stays right-offset) and **hidden cleanly whenever Pause/Settings modal is open**.
   - **Settings & Pause Modal Discipline:** Opening Settings from Main Menu or Pause **explicitly hides/fades the Main Menu logo & buttons (`CanvasGroup.alpha = 0, blocksRaycasts = false`)** and **hides Chapter/Timestamp Title Cards**, guaranteeing ZERO text-on-text collisions!
   - **Screens Required:** `MainMenu` (Continue / New Game / Chapters / Settings / Quit over live 3D HDRP scene), `ChapterSelect` (7 chapters w/ unlocked states), `PauseMenu`, `SettingsPanel` (Graphics preset + STP scale, Mouse/Gamepad Sensitivity, Invert Y, FOV `65..95`, Master/Music/SFX/VO volume, Subtitles, Sound Captions), `DocumentViewer` (zoom + page flip), `DeathScreen` (quick instant checkpoint reload), `EndingStatsScreen` (`Time`, `Deaths`, `Documents X/5`, Credits).

---

## 5. Dinosaur AI, Rigging & Animation Contract (`Cretacea.AI`)

### 5.1 Asset Alignment with `dino-rigger` Pipeline
`dino-rigger` delivers re-rigged, weight-cleaned, zero-spike FBX creatures + `dino_manifest.json` from `scratch/dino_pipeline/ready_for_unity/<species>/`, imported into `Assets/Art/Models/Dinosaurs/<species>/`:

| Species | Target Scale (World) | Doorway / Nav Clearance | Required FBX Animation Clips (`Generic` Rig) | Material Setup |
|---|---|---|---|---|
| **`Velociraptor`** | Length `~3.6 m`, **Stalking Height `~1.70 m`** | Clears all `2.20 m` doorways cleanly (zero head clipping!) | `Idle_Alert`, `Stalk_Walk`, `Sprint_Run`, `Sniff_Inspect`, `Roar_Call`, `Bite_Lunge`, `Turn_Left`, `Turn_Right` | HDRP Lit + `SSS_DinoSkin` + 4K raw textures + dynamic rain wetness coat |
| **`Tyrannosaurus`** | Length `~12.0 m`, Height `~4.4 m` | Valley open terrain + watchtower/Substation sightlines | `Idle_Heavy`, `Walk_Stomp`, `Charge_Run`, `Roar_Massive`, `Look_Scan`, `Bite_Snap`, `Fence_Bash` | HDRP Lit + `SSS_DinoSkin` + 4K raw textures + wet rain sheen |
| **`Brachiosaurus`** *(Opening)* | Length `~17.0 m` | Titan Meadows | `Idle_Graze`, `Walk_Majestic`, `Head_Lift_Call` (head straight forward in bind pose — fixes `ANIM-6`!) | HDRP Lit + `SSS_DinoSkin` + 4K textures |
| **`Triceratops`** *(Opening)* | Length `~7.5 m` | Titan Meadows | `Idle_Alert`, `Walk_Heavy`, `Snort_Threat` (natural amber-brown eyes, cleaned front feet!) | HDRP Lit + `SSS_DinoSkin` + 4K textures |

- **Standardized Bone Naming Contract (`dino-rigger` → `unity-dino-ai`):**
  `Root`, `Pelvis`, `Spine_01..03`, `Neck_01..03`, `Head`, `Jaw`, `Tail_01..06`, `Leg_L/R_Upper`, `Leg_L/R_Lower`, `Ankle_L/R`, `Foot_L/R`, `Arm_L/R_Upper`, `Arm_L/R_Lower`, `Hand_L/R`.
  Every creature FBX has **pivot at ground `Y = 0`**, facing **`+Z` forward**, with exact per-clip `natural_speed_mps` recorded in `dino_manifest.json`!

### 5.2 Unity `NavMesh` & Dual Agent Setup (`com.unity.ai.navigation`)
- Bake **two distinct `NavMeshSurface` Agent Types** in `IslaTormenta.unity`:
  1. **`Agent_Raptor`:** `Radius = 0.45 m`, `Height = 1.75 m`, `StepHeight = 0.45 m`, `MaxSlope = 45°`. Covers Visitor Center (Feed Prep, Cold Storage, Vet Clinic, Corridor, Lobby), Service Paths, and Old Maintenance Yard (Warehouse B, Workshop, Exit Court).
  2. **`Agent_TRex`:** `Radius = 1.45 m`, `Height = 4.50 m`, `StepHeight = 0.75 m`, `MaxSlope = 40°`. Covers **all of Paddock 4 (Tyrant Valley)**! Valley trees, rock boulders, Substation perimeter (`DinoBlocker`), and Old Yard west wall carry `NavMeshObstacle (Carve)` so the **T. rex actually navigates around trees, boulders, and fences** (`COL-6`, `ANIM-7` fixed!).

### 5.3 Animator Controllers & Animation Rigging Stack (Zero Skating, Zero Shearing, Live Jaw!)
Every active predator prefab (`PF_Velociraptor.prefab`, `PF_Tyrannosaurus.prefab`) mounts:
1. **`Animator` (`AC_Velociraptor.controller` / `AC_Tyrannosaurus.controller`):**
   - **Layer 0 (`BaseLocomotion`):** 1D Blend Tree (`Speed`: `0 → Idle`, `stalkSpeed → Stalk_Walk`, `walkSpeed → Walk`, `runSpeed → Sprint_Run/Charge_Run`) with animator playback speed dynamically scaled by `agent.velocity.magnitude / clipNaturalSpeed` (clamped `0.75..1.35`) so feet match ground speed with **zero skating**!
   - **Turn-In-Place Sub-State (`ANIM-7` fix):** Triggers `Turn_Left` / `Turn_Right` when `Speed < 0.3 && |SignedAngleToTarget| > 45°`.
   - **Action Triggers:** Separate `TriggerAlertRoar` (`Roar_Call` / `Roar_Massive` — **Raptor NEVER plays `Bite_Lunge` from 20 m away!**), `TriggerBite` (`Bite_Lunge` / `Bite_Snap` with animation event `OnBiteHitFrame` at `0.35 s` Raptor / `0.55 s` T. rex), `TriggerSniff` (`Sniff_Inspect` / `Look_Scan`), `TriggerBash` (`Bite_Lunge` door-ram / `Fence_Bash`).
   - **Footstep Animation Events:** Placed cleanly on exact foot-plant frames of every walk/stalk/run clip — firing `OnFootstepLeft()` / `OnFootstepRight()` (plays surface-aware `raptor_step` / `trex_step`, camera trauma proportional to distance, and T. rex mud `footprint_trex` DBuffer Decal!).
2. **Unity Animation Rigging (`RigBuilder` — Fixes `ANIM-2`, `ANIM-4`, `ANIM-5`):**
   - **Head & Neck Tracking (`MultiAimConstraint`):** Distributed chain (`Neck_01: 0.25`, `Neck_02: 0.35`, `Head: 0.65`) smoothly aiming at target (`Player.EyePosition`, noise source, or look-ahead point) with angular clamp `±65°` yaw / `±30°` pitch — **hierarchically propagates child bones with zero shearing!**
   - **Active Procedural Jaw (`JawRigDriver.cs`):** Drives `Jaw` local X rotation (`0°..38°`) dynamically from active vocalization audio RMS envelope (`roar`, `screech`, `chirp`, `snort`) + additive blend during `Bite` frames!
   - **Two-Bone Foot IK (`FootIKGrounder.cs`):** Casts downward rays from `Foot_L` & `Foot_R` onto `Environment` every `LateUpdate`, adjusting pelvis drop and foot IK targets + surface normal alignment so claws stay **100% planted on sloped Tyrant Valley terrain and concrete steps** (zero 26 cm sinking!).

### 5.4 `DinosaurAI` Perception & State Machine (`Cretacea.AI`)
- **Parameters (Matching Tested Gameplay Balance):**
  - **Velociraptor:** Walk `1.9`, Stalk `1.3`, Run `6.3 m/s`; Sight `24 m` / `130°` FOV; Hearing `1.5×`; Attack `2.7 m` / Kill `2.3 m`; Lose Time `5.0 s`; **Bashes closed unlocked doors (`1.1 s` cooldown, breaks door open on 4th hit with splinter SFX & sparks!)**.
  - **Tyrannosaurus:** Walk `2.6`, Stalk `1.8`, Run `7.0 m/s`; Sight `62 m` / `150°` FOV; Hearing `1.25×`; Attack `7.5 m` / Kill `5.0 m`; Lose Time `7.0 s`; Leashed near Substation 3 once player enters compound; **Chase Rubber-Band:** During active chase, if player runs (`> 3.0 m/s`), `speedMult = Mathf.Lerp(0.72f, 1.30f, Mathf.InverseLerp(13f, 30f, dist))`, else `1.10f`. Cannot kill player through `DinoBlocker`!
- **State Transition Bugfix (`AUD-5`):**
  - Non-visual loud noises (`breaker`, `generator`, `e0_gate`) route to **`Investigate`** (or if already `Alert`/high-awareness and escalating to `Hunt`, **immediately reset `unseenTimer = 0f`** so it NEVER instant-transitions `Hunt → Search` on the next tick and never plays screech + sniff in one frame!).
  - Visual perception scales by `PlayerController.Visibility`: `awarenessRate = baseRate * player.Visibility * distFalloff * aggression`. `>= 0.45` → `Alert` (plays `raptor_chirp` / `trex_snort`, holds gaze `1.4 s / 2.2 s`); `>= 1.0` → `Hunt` (plays `Roar_Call` / `Roar_Massive` + chase music).

---

## 6. Audio, Surface Footsteps & VARCO Sound Integration (`Cretacea.Audio`)

### 6.1 Unity `AudioMixer` Topology (`Assets/Audio/CretaceaMixer.mixer`)
```text
Master  (-3 dB Attenuation + Brickwall Limiter @ -1 dB → MasterVol exposed param)
├── Music       (Dual-source equal-power crossfade; auto-ducks -6 dB during VO; MusVol)
├── Ambience    (Indoor loops full-band; auto-ducks -6 dB during VO; AmbVol)
│   └── AmbOut  (Outdoor storm/jungle loops; LowPass Cutoff: 1,400 Hz indoors ↔ 20,000 Hz outdoors!)
├── VO          (Sequential VoiceQueue; pauses on PauseMenu; radio squelch intro; VoVol)
├── UI          (Unscaled / ignores pause; UiVol)
└── World       (Spatial 3D blend 1.0 + AudioReverbZone routing; SfxVol)
    ├── SFX
    ├── Dinos
    └── Foley
```
- **Music Track Note:** `mus_chase_01` has a `22.5 s` quiet intro — `MusicController.cs` **starts playback at `time = 22.5f`** on combat/chase start so intensity hits on beat 1!

### 6.2 Surface-Accurate Footsteps (`SurfaceAudioDetector.cs` — Fixes `AUD-3`)
Every floor mesh, grating, water plane, and Terrain layer resolves cleanly to one of 7 `SurfaceType` enums:
1. **Raycast down `1.3 m`** from player feet on every step cycle:
   - If hit collider has a `SurfaceTag` component (or `PhysicsMaterial` named `PM_Concrete`, `PM_Tile`, `PM_Gravel`, `PM_Metal`, `PM_Mud`, `PM_Grass`, `PM_Water`) → return that exact surface immediately.
   - If hit collider is `UnityEngine.Terrain` → sample `TerrainData.GetAlphamaps()` at world `(x, z)` and return the dominant Terrain Layer's mapped `SurfaceType` (`0=Mud, 1=Grass, 2=Gravel, 3=Concrete`)!
   - If standing in shallow stream collider (`Layer: Trigger`, `WaterSurfaceTag`) → override to `SurfaceType.Water`!
2. Guarantees **all 7 surface footstep sets (`Concrete`, `Tile`, `Gravel`, `Metal`, `Mud`, `Grass`, `Water`) play reliably** across Visitor Center tiles, Substation steel grating, Service Road gravel, Tyrant Valley grass/mud, and stream crossings!

### 6.3 3D Audio Wall Occlusion & Zone Reverb (`AudioOcclusion3D.cs` — Fixes `AUD-4`)
- Attached to spatial 3D emitters (`Dinos`, `Sparks`, `Generator`, `Alarms`, `Doors`).
- Every `0.15 s` (staggered), casts a linecast from emitter to `AudioListener` against `Environment | Door`:
  - **Clear LOS:** `AudioLowPassFilter.cutoffFrequency = 22000 Hz`, `occlusionGain = 1.0`.
  - **Occluded by 1+ walls/closed doors:** Smoothly filters down to `cutoffFrequency = 950 Hz` (`-10 dB` gain), making raptors stalking behind Feed Prep tile walls or generators outside Substation walls sound genuinely muffled and terrifying!
- Paired with 7 `AudioReverbZone` presets (`OfficeTight`, `Corridor`, `AtriumLarge`, `TileWet`, `ConcreteCanyon`, `MetalWorkshop`, `OpenValley`).

### 6.4 `SoundBank` ScriptableObject & VARCO Sound Drop-In Architecture
`SoundBank.asset` (`Assets/ScriptableObjects/Audio/SoundBank.asset`) maps every logical `SoundId` (matching Godot's 132 slot IDs + `Audio/varco_sound_brief.json` from `asset-foundry`) to an array of `AudioClip` takes, pitch/volume jitter, mixer group, and 3D rolloff curve.
- **Drop-In Priority Resolver (`Editor/SoundBankAutoBinder.cs`):** Automatically prefers freshly generated WAV/OGG files inside **`Assets/VARCOSound/Generated/<slot_id>*.wav`** over migrated legacy audio in **`Assets/Audio/`** — so the user or audio worker can generate clips inside Unity's VARCO Sound window at any time and click *Rebind SoundBank* with zero code edits!
- **6 Missing Critical VARCO Slots (MUST be populated for AAA zero-silence):**
  `foley_crouch`, `foley_stand_up`, `foley_grass_enter`, `glass_crack_large`, `bush_rustle_close`, `crowd_gasp_cheer`.
- **9 Priority Stand-In Upgrade VARCO Slots:**
  `amb_coast_dawn_loop`, `amb_building_creak_loop`, `amb_interior_hum_loop`, `raptor_sniff`, `raptor_breath_close`, `fuel_pump_stroke`, `fuel_pump_primed`, `foley_step_grass_*` + `foley_step_metal_*`, `foley_pickup`.
- **Reused Clean Assets:** All 17 Fish Audio VO clips (`vo_dana_01..07`, `vo_pa_01..03`, `vo_vance_01..05`, `vo_ranger_01..02`), all 12 Music tracks, and ~110 verified VARCO SFX/UI/Ambience clips migrated by `asset-foundry`.

---

## 7. Folder Structure, `Cretacea.*` Namespaces, File Ownership Split & Parallel Build Phases

### 7.1 Standardized Unity `Assets/` Layout
```text
Assets/
├── Art/
│   ├── Materials/          # HDRP Lit / Wet / Glass / SSS materials (auto-generated + tuned)
│   ├── Models/
│   │   ├── Architecture/   # Modular beveled walls, doorframes, windows, trusses, pipes
│   │   ├── Characters/     # 5 Humanoid visitors & park guide FBX (from asset-foundry)
│   │   ├── Dinosaurs/      # Velociraptor, Tyrannosaurus, Brachiosaurus, Triceratops (from dino-rigger)
│   │   ├── Foliage/        # Poly Haven + Tripo vegetation + LODs
│   │   ├── Props/          # 4K raw Tripo props + upgraded kiosk, valves, flashlight_pickup/fps_rig
│   │   └── Rocks/          # Poly Haven cliffs & boulders + LODs
│   ├── Shaders/            # SG_WetSurface_HDRP, SG_FoliageWindTrample, SG_CCTVStatic, SG_OceanDawn
│   ├── Textures/           # Packed HDRP PBR sets (BaseMap, Normal, MaskMap) + HDRIs + Decals
│   └── UI/                 # Migrated Documents (6), Maps (3), Signs (22), HUD (11), Fonts (6)
├── Animations/
│   ├── Dinosaurs/          # AC_Velociraptor, AC_Tyrannosaurus, AC_Herbivore + Avatar Masks
│   └── Player/             # AC_FlashlightFPS (Idle, Equip, Click, WalkBob, SprintLower)
├── Audio/                  # CretaceaMixer.mixer + Migrated VO/, Music/, Amb/, SFX/, UI/
├── VARCOSound/Generated/   # Drop-in folder for Unity VARCO Sound generated WAVs (6 missing + 9 upgrades)
├── Editor/                 # CretaceaPbrImporter.cs, CretaceaCliTools.cs, SoundBankAutoBinder.cs
├── Input/                  # CretaceaControls.inputactions
├── Prefabs/
│   ├── Architecture/       # Doors/, Windows/, ModularKit/
│   ├── Dinosaurs/          # PF_Velociraptor, PF_Tyrannosaurus, PF_Brachiosaurus, PF_Triceratops
│   ├── Gameplay/           # PF_PlayerRoot, PF_Interactables/, PF_Triggers/
│   ├── UI/                 # PF_HUDCanvas, PF_MainMenuCanvas
│   └── World/              # Zone_01..05 prefabs + Props/
├── Rendering/              # HDRP_Cretacea_M5Max.asset, Global Volumes, ViewmodelCustomPass, VFX/
├── Scenes/                 # Boot.unity, MainMenu.unity, IslaTormenta.unity
├── ScriptableObjects/      # Audio/SoundBank.asset, Story/ChapterManifest.asset, AI/DinoConfigs/
└── Scripts/
    ├── Core/               # Cretacea.Core.asmdef
    ├── Player/             # Cretacea.Player.asmdef
    ├── Interact/           # Cretacea.Interact.asmdef
    ├── Story/              # Cretacea.Story.asmdef
    ├── World/              # Cretacea.World.asmdef
    ├── AI/                 # Cretacea.AI.asmdef
    ├── UI/                 # Cretacea.UI.asmdef
    ├── Audio/              # Cretacea.Audio.asmdef
    └── Cutscenes/          # Cretacea.Cutscenes.asmdef
```

### 7.2 Assembly Dependency Graph (Strictly Acyclic — Zero Circular Refs!)
- `Cretacea.Core` → *(no project dependencies)*: Enums, `IInteractable`, `GameEvents`, `GameStateManager`, `SaveSystem`, `SettingsManager`, `SurfaceTag`, `WorldRegistry`.
- `Cretacea.Audio` → `Cretacea.Core`
- `Cretacea.Player` → `Cretacea.Core`, `Cretacea.Audio`
- `Cretacea.Interact` → `Cretacea.Core`, `Cretacea.Player`, `Cretacea.Audio`
- `Cretacea.World` → `Cretacea.Core`, `Cretacea.Interact`
- `Cretacea.AI` → `Cretacea.Core`, `Cretacea.Player`, `Cretacea.Interact`, `Cretacea.Audio`
- `Cretacea.UI` → `Cretacea.Core`, `Cretacea.Audio`, `Cretacea.Player`
- `Cretacea.Cutscenes` → `Cretacea.Core`, `Cretacea.World`, `Cretacea.AI`, `Cretacea.UI`, `Cretacea.Audio`, `Cretacea.Player`
- `Cretacea.Story` → `Cretacea.Core`, `Cretacea.World`, `Cretacea.Interact`, `Cretacea.Player`, `Cretacea.AI`, `Cretacea.Audio`, `Cretacea.Cutscenes`

### 7.3 Strict 4-Worker Parallel File Ownership Split (Zero Merge Collisions)
Once `unity-tools-scout` (project scaffold), `dino-rigger` (creature FBXs), and `asset-foundry` (props/textures/UI/audio) land their staged outputs, **4 Unity build workers execute in parallel** with strictly disjoint write boundaries:

| Worker Name | Exclusive Write Paths (Files/Folders Owned) | Read-Only Dependencies | Core Deliverable |
|---|---|---|---|
| **`unity-core-scripts`** | `Assets/Scripts/Core/`<br>`Assets/Scripts/Player/`<br>`Assets/Scripts/Interact/`<br>`Assets/Scripts/Story/`<br>`Assets/Scripts/World/`<br>`Assets/Input/`<br>`Assets/Animations/Player/`<br>`Assets/Prefabs/Gameplay/`<br>`Assets/Scenes/Boot.unity` | `Assets/Art/Models/Props/flashlight_*`<br>`Assets/Rendering/` | Complete core architecture, `GameEvents`, `GameStateManager`, `SaveSystem`, `PlayerController`, ironclad `FlashlightController` + Viewmodel Custom Pass hookup, `DoorController` (bashable/locked), `IInteractable` stations, `StoryDirector` (all 7 chapters + `5/5` docs), `EnvironmentDirector`, `LightGroupController`, `SecurityCCTVController`, `FoliageTrampleDriver`, `WorldRegistry`. |
| **`unity-level-builder`** | `Assets/Scenes/IslaTormenta.unity`<br>`Assets/Prefabs/World/`<br>`Assets/Prefabs/Architecture/`<br>`Assets/Art/Models/Architecture/`<br>`Assets/Art/Materials/`<br>`Assets/Art/Shaders/`<br>`Assets/Rendering/Volumes/`<br>`Assets/Rendering/VFX/` | `Cretacea.Core` + `World` + `Interact` scripts<br>`Assets/Art/Models/Props,Foliage,Rocks/`<br>`Assets/Art/Textures/` | Seamless unified Unity Terrain, all 5 modular Zone prefabs (`VisitorCenter`, `ServicePaths`, `TyrantValley`, `OldYard`, `BackdropAndCoast`) at exact §1 coordinates, all 27 framed doors & real windows, grounded Substation 3, east-only green route beacons, open Dawn Ocean Vista, APV + Reflection Probes + VFX Graph Rain w/ indoor occlusion, dual `NavMeshSurface` bake. |
| **`unity-dino-ai`** | `Assets/Scripts/AI/`<br>`Assets/Animations/Dinosaurs/`<br>`Assets/Prefabs/Dinosaurs/`<br>`Assets/ScriptableObjects/AI/` | `Cretacea.Core`, `Player`, `Interact`, `Audio`<br>`Assets/Art/Models/Dinosaurs/` (`dino_manifest.json`) | `PF_Velociraptor`, `PF_Tyrannosaurus`, `PF_Brachiosaurus`, `PF_Triceratops` with HDRP SSS materials, `AC_*` Animator Controllers (velocity-matched blend trees + Turn-In-Place + Footstep events), Animation Rigging (`MultiAimConstraint` + active `JawRigDriver` + `FootIKGrounder`), `DinosaurAI` state machine + T. rex rubber-band chase + door-bash + cutscene `DinoActor`. |
| **`unity-ui-audio`** | `Assets/Scripts/UI/`<br>`Assets/Scripts/Audio/`<br>`Assets/Scripts/Cutscenes/`<br>`Assets/Audio/`<br>`Assets/Prefabs/UI/`<br>`Assets/Scenes/MainMenu.unity`<br>`Assets/ScriptableObjects/Audio/`<br>`Assets/Editor/SoundBankAutoBinder.cs` | `Cretacea.Core`, `Player`, `World`, `AI`<br>`Assets/Art/UI/`<br>`Assets/VARCOSound/Generated/` | Live 3D HDRP `MainMenu.unity`, `CretaceaMixer.mixer`, `AudioManager` + `MusicController` (`22.5 s` chase offset) + `VoiceQueue` + `SurfaceAudioDetector` (7 surfaces) + `AudioOcclusion3D`, `SoundBank` + VARCO drop-in auto-binder, full HUD (`ObjectiveBanner`, `StealthEye`, `TacticalJournal` w/ `hud_minimap_route.png` + Substation checklist + 5/5 Docs, radial hold ring, non-overlapping Pause/Settings/DocumentViewer/Death/End), `OpeningCutsceneDirector` & `EndingCutsceneDirector` (Cinemachine crane sunrise shot!). |

### 7.4 Phased Parallel Integration & Automated Verification Gates
- **Phase 0 — Contract Lock (First 10 min):** `unity-core-scripts` writes `Assets/Scripts/Core/*.cs` (`GameEvents`, interfaces, enums, `WorldRegistry`, `.asmdef` files) FIRST and posts `@all Core contracts landed` so the other 3 workers compile cleanly against live C# types immediately!
- **Phase 1 — Parallel Build Sprint (All 4 Workers Simultaneously):**
  - `unity-core-scripts` finishes Player, Flashlight, Doors, Interactables, StoryDirector, Environment/CCTV.
  - `unity-level-builder` builds unified Terrain, 5 Zone Prefabs, Lighting/APV/Probes, Rain VFX, and wires `IslaTormenta.unity`.
  - `unity-dino-ai` imports `dino-rigger` FBXs, builds Animators, Animation Rigging (IK/Head/Jaw), and `DinosaurAI` prefabs.
  - `unity-ui-audio` builds `MainMenu.unity`, `CretaceaMixer`, `SoundBank`, Surface Footsteps/Occlusion, full HUD/Journal, and Opening/Ending Cinemachine cutscenes.
- **Phase 2 — Scene Binding, NavMesh & APV Bake:**
  - `unity-level-builder` drops `PF_PlayerRoot`, `PF_HUDCanvas`, `PF_AudioManager`, `PF_StoryDirector`, and Dino Spawner anchors into `IslaTormenta.unity`, registers named anchors in `WorldRegistry`, runs `cretacea.navmesh_bake` (both Raptor & T. rex NavMeshes) and bakes APV + Reflection Probes.
- **Phase 3 — Automated QA & Visual Verification Gate (Zero Defects Required):**
  1. Run `cretacea.scene_audit` CLI tool (`Assets/Editor/CretaceaCliTools.cs`) — asserts: **zero missing scripts**, **zero pink/magenta shaders**, **all 27 doors aligned to frame bounds**, **zero floating Substation/Workshop props**, **zero indoor foliage overlaps**, **Ch. 1 `HasFlashlight == false` and viewmodel inactive**, **`5/5` document pickups registered**, **both Raptor & T. rex NavMeshes valid**.
  2. Run `cretacea.capture` automated camera tour across **all 24 critical hero & bug-regression viewpoints** (Live Main Menu, Opening Plaza & Security Window Push-In, Ch. 1 Spawn facing live CCTV + Desk Flashlight without held light, Control Room overlooking visible Talon Ridge Gate, Lobby Atrium w/ lit T. rex Skeleton + aligned Skylight + Mezzanine Stairs, Staff Exit broken ceiling, `door_wing` & `dock_roller` with zero walls behind them, Open Service Path T-branches w/ Kiosk & Raptor Crossing, Tyrant Valley Stream Culvert & Grounded Jeep, Grounded Substation 3 + East-Only Green Route, Old Yard Clean Squeeze Gap + Partitioned Workshop + Fixed `workshop_door_e` + Outward E-0 Gate, Dawn Coast Open Ocean Sunrise Vista, and Close-Up Studio Shots of all 4 Dinosaurs).
  3. Visually inspect every captured PNG before shipping — zero carryover bugs permitted.
