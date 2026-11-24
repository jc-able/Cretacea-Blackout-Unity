# Round 4 hand-in: Visitor Center light and fixtures (`r4/lights`)

Base `3987803`. I took over this package from an engineer who was cut off. They had left no source edits, only
before-captures (`Screenshots/r4before`) and helper scripts (`Tools/agent_r4/`). I kept those and did the work below.
All before/after pairs come from Play-mode game views in this clone, made with the review's own camera positions
(`capture_set.sh` / `route_test.py`). Captures are in `Screenshots/handin_r4/` (see `INDEX.md`). Numbers come from
`Tools/agent_r4/st.py` (frame mean, % of pixels under 16/255, R/G and B/G of the frame means, % red-dominant pixels).

## Goals

### 1. Major: the lobby is one amber-brown. DONE
- `Light_Lobby_Main` 3600 K -> 4300 K (the neutral key). `Light_Lobby_Grazer` 3600 K -> 4800 K, 13,000 -> 12,000 cd.
  The wall texture is warm in itself, so at 4000 K the walls still measured R/G 1.26. The accents (skeleton,
  banners, reception: 3000-3300 K) and the mezzanine downlights are unchanged, so they carry the warm side.
- New `Light_Lobby_MoonFill`: a 7000 K, 9000 cd spot, 26 m out over the glass roof on the moon's side (ESE). It casts a
  cached Shadow.Low shadow, so the roof beams draw on the floor. It sits in the VCMain group and is about 11 lux on
  the floor. Honest note: at the lit-room exposure, 8-12 lux of moonlight lifts the floor by only 1-2/255 (measured
  with the light on and off). The visible cool comes from the neutral key and the cooler walls, not from this light.
- Result (`01_lobby_on`): frame R/G 1.26 / B/G 0.79 -> **1.15 / 0.88**. Floor and walls now read neutral grey-beige
  and the skeleton stays warm. The hand in the base clone capture rendered cyan (shaders still compiling). The
  review's own frame measures 1.26 / 0.79.

### 2. Major: a red emergency head floods the corridor wall crimson. DONE
- `Light_Corr_Emerg`: 150 deg -> 64 deg cone, straight down (Throw.Down), 150 cd, 5 m range, volumetric 0.45.
- Lens: `LookFixtures.Emergency` now makes the beacon glow at 12 nits (`EmergencyGlowNits`; it was 70). The heads use
  their own material copy, `MAT_beacon_red_EmergGlow`. They share the beacon_red model with the zone 2-3 beacons
  (`Beacon()`, still 70 nits), and one shared `_Glow` asset took whichever luminance was written last.
- Result (`02_corridor_dark`, `13_zoom_units_lenses` bottom): red-dominant pixels 23.2 % -> **2.9 %**. The lens reads
  saturated red, no longer pink-white. Each head now draws a small red scallop on its own wall foot and floor.
- Bug found on the way: the red heads, the records amber beacon and the cold/server blue lights had come out black.
  `LookLights.Zone1.cs` read `Red`/`Amber`/`CoolBlue` from `LookLights.cs`, but static initialisers of a partial class
  run in a file order that C# does not guarantee. The Zone 1 table now declares its own `Z1Red`/`Z1Amber`/`Z1CoolBlue`
  ahead of the array, and `LookLights.Kit.cs` spells its blue out. **Lead: `LookLights.Zone23.cs` and `.Menu.cs`
  may have the same hazard if they use the core colours.**

### 3. Major: the battery unit reads as two orange eyes. DONE
- `LookFixtures.TwinHeadBody` (Visitor Center units): light-grey housing (`MAT_Z1_BatteryHousing`), a dark front
  band, a dark grille and test button, dark head cans with steel bezels, and a 4 x 1.4 cm charge LED at 10 nits
  (`MAT_Z1_BatteryChargeLED`). Heads are aimed 58 deg down (was 35). Lenses are 7 nits (`MAT_Z1_BattLens_K*`, under
  the ~12.7-nit blackout white), so they stop clipping.
- `BattSpill`: a 0.55 cd shadowless point 28 cm in front of each unit (new rule `^BattSpill$`). It follows its unit
  through `LampBounce`, which switches a follower off below 0.5 cd, hence 0.55. It lights the housing and a faint
  halo on the wall.
- Root cause of "no housing at all": the unit was mounted on the wall behind `Pier_1`, a pilaster 11 cm proud of the
  wall. The nearest-wall probe runs at the lamp's height, so only the heads stuck out of the pier (see
  `15_battery_unit_free_cam_before_fix`). The new `FaceAt()` re-probes the face at the unit's own height.
- Result (`04_exit_wall_on`, `13_zoom_units_lenses` top): in the torch the unit now reads as a grey box with a dark
  band and two cans. With the torch off (`03`), the two warm lenses and the spill halo show at this distance. The
  housing is small at 7 m.

### 4. Major: the loading dock is nearly black. DONE
- `Light_Dock_Batt` now hangs from the canopy between two rafters at (17.2, 4.9, -10), over the walk from the roller
  door to the Service Road 3 exit. It was on the wall behind the player. Rule: 1300 cd, 100 deg, 12 m, Shadow.Low
  (cached 256), volumetric 0.45. The fixture is a ceiling twin-head on a hanger.
- `Dock_Pad`: dry `ConcreteFloor` (was `AsphaltWet`) and a `CorrugatedPainted` grey deck (was rust-red
  `CorrugatedMetal`). New `Dock_WetEdge_E` / `_S` are thin `ConcreteFloorWet` strips along the open east and south
  edges, with no collider.
- `Light_Dock_Main` is now a high-bay (bell, driver, drop rod) at y 4.45 instead of four flat ceiling panels
  (`LookFixtures.Build`: `Dock_Main` -> `HighBay`).
- Result (`05`, `06`, `16`): under 16/255, torch on 53.9 % -> **31 %**, torch off 64.7 % -> **23.5 %**. The median
  went 14.9 -> 29 (torch on) and 12.1 -> 31 (torch off). The player crosses one warm pool. The review's < 20 % target
  is not fully met: what remains dark is the jungle outside the open sides and the far deck corners.

### 5. Minors
- Night plaza with pinprick lamps (`intro_16`): `Light_PlazaLamp` 1300 cd / 110 deg -> 2400 cd / 95 deg, volumetric
  0.4. In `12_intro_plaza_aerial` the pools read as distinct warm discs on blue-grey paving: region mean RGB
  36/33/34 (mauve) -> 48/47/55. The intro clock is wall-clock, so the after frame is one slot earlier and the blend
  state differs slightly.
- Wall packs red and maroon (`intro_17`): `Light_SecWest_WallPack(_n)` 2700 K -> 3300 K, volumetric 0.5-0.6 ->
  0.20-0.22. `11_intro_wallpacks`: the halos and wall pools now read amber, not maroon.
- Street-lamp lens offset in its housing: `PathLamp` now fits a 0.54 x 0.60 m lens to the measured housing (x
  -0.31..0.46, z 0.20..1.04), tilted 5.7 deg to follow the underside. The old flat 0.30 x 0.48 lens hid in the lip
  at the back and hung out at the front. `14` top: the lens now fills the housing. At dusk exposure it still reads
  as a warm, fairly flat panel.
- Gift-shop ceiling band: a new `^Light_GiftShop_Wash` rule at 240 cd (was 450). The band dropped only from mean
  109 / peak 204 to 97 / 176 (`09`). PARTLY done: the washes are not the main source. The likely source is a ceiling
  fill or troffer cone near the back wall, which I did not track down in the time box.
- Office stair-step corner: the shadowed `Light_Security_Main` went to 150 deg (1024 map), and the security
  companions (`Fixture_Lamp_Light_Security_*_Grid*`, Shadow.Low) went from 256 to 512
  (`LookFixtures.SharpenSecurityCompanions`). `14` bottom: the steps are fainter but still visible at zoom. NOT
  fully fixed. The next suspect is the HDRP soft-shadow filter quality, which is the lead's setting in the HDRP asset.
- Detached plant-leaf shadow (`office_on`): not touched. Per the check this is SSGI in `Look_Indoor` (lead's file).
- Blackout walls at 10-14/255 with the torch off: not touched. The kept fix is a probe-ambient floor in `Look_Indoor`
  (lead's file). Corridor torch-off frame, mean 29.7 -> 23.8: the red flood no longer lifts it.
- Desk lamp without light in the security office: not done (time box).

## Performance (`perf_here.sh`, editor Play mode, security office, 8 samples)
| view | before (3987803) | after |
|---|---|---|
| office_on (Night) | gpu 11.8 ms, cpu 45.1 ms, 7.22 M tris | gpu 11.1 ms, cpu 44.4 ms, 6.99 M tris |
| office_dark (Records, torch on) | gpu 5.1 ms, cpu 23.7 ms | gpu 6.6 ms, cpu 26.4 ms |
| final build with NavMesh, second run | | office_on gpu 13.0 / cpu 40.4 ms; office_dark gpu 4.2 / cpu 19.5 ms |

The hidden editor's numbers move by 1-2 ms from run to run, both ways: two after-runs bracket the before-run. I see
no reliable slowdown.

New lights: one shadowed moon fill (lobby, VCMain, cached Shadow.Low), one tiny shadowless `BattSpill` per Visitor
Center battery unit (range 0.8 m), and `Light_Dock_Batt` now casts a cached Low shadow. No per-frame allocations.

## Files touched
- `Assets/Editor/LookPass/LookLights.Zone1.cs`: rules for lobby main/grazer/moon fill, corridor emergency, dock
  battery, plaza lamps, wall packs, gift-shop washes, security main, `BattSpill`; file-local signal colours.
- `Assets/Editor/LookPass/LookLights.Kit.cs`: inline blue (static-init order).
- `Assets/Editor/LookPass/LookFixtures.cs`: battery unit body, lens, LED, spill and `FaceAt`; emergency glow 12 nits
  with its own material copy (`GlowModel` variant); street-lamp lens fit; dock high-bay; security companion 512 maps;
  moon fill skipped as a fixture.
- `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs`: only the dock (`Dock_Pad` materials, wet edge strips,
  `Light_Dock_Main`, `Light_Dock_Batt` placement) and the new `Light_Lobby_MoonFill` placement. No room furniture
  or wall dressing.
- Generated material assets (committed with the source because the look pass writes them):
  `MAT_Z1_BatteryHousing`, `MAT_Z1_BatteryChargeLED`, `MAT_Z1_BattLens_K3200/K3300`,
  `Fixtures/MAT_beacon_red_EmergGlow`.

## Requests for the lead
1. Check `LookLights.Zone23.cs` and `LookLights.Menu.cs` for the same static-initialiser hazard: rules that use the
   core `Red`/`Amber`/`CoolBlue`/`Wide` from `LookLights.cs`. `Wide` is a const, so it is safe; colour fields are not.
2. Blackout walls 10-14/255: the probe-ambient floor in `Look_Indoor`, as in the check file.
3. Office plant-leaf shadow: SSGI denoise or ray length in `Look_Indoor`.
4. Office corner stair-step: if it matters, the HDRP soft-shadow filter quality (HDRP asset) is the remaining lever.
5. `LampBounce` disables any follower under 0.5 cd. A threshold relative to `share` would allow fainter spill lights.

## Hand-in checks
- Final `build_all_scenes` with NavMesh: ok (both NavMeshes baked). 0 compile errors, 0 warnings.
- `route_test.py`: **45 / 45** in 270 s (`Screenshots/r4route`). Its own 13_ch3_loading_dock frame: 30.7 % under
  16/255, median 29.8 (review 3: 57 % / 13).
- `verify_playmode_doors`: 18/18 doors, 5/5 documents, both NavMeshes valid, flashlight OK.
- Play smoke (Intro, Night, Corridor, ServicePaths, plus the Night/Records/Dock checkpoints used for captures):
  0 console errors. The only warnings are the engine's reflection-system notice and "no valid NavMesh" from the
  intermediate builds made without NavMesh.

## Not verified / rough edges
- The intro pairs are not frame-exact (wall-clock timer).
- With the torch off at 7 m, the battery housing is small. It reads clearly only in the torch or close up.
- The dock floor is clean and evenly beige under the new pool. Grime decals there would help (dressing, not lights).
