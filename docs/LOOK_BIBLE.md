# Cretacea: Blackout (Unity) — Look Bible

The owner's verdict on the first Unity build: **"the colors are off, it doesn't look right … make this game look
amazing … it needs to be perfect."** This file is the shared reference for everyone working on the look.
Read it fully before changing anything.

## 1. The game in one paragraph
First-person horror on Isla Tormenta, a tropical dinosaur park. Prologue: golden afternoon, the park open. Then one
night: a storm, a blackout, a security guard with a flashlight who must restore auxiliary power at Substation 3 and
leave through Service Exit E-0 while raptors and a T. rex roam. Ending: dawn at the coast. Reference mood: the storm
night of *Jurassic Park* (1993) — wet concrete, warm practical lamps in blue rain, red beacons, lightning.

## 2. What "amazing" means here (acceptance criteria)
1. **Natural colour.** Day is not sepia, night is not black-and-red. Every frame has a full tonal range: readable
   shadows, mid-tones, and highlights that are not clipped flat.
2. **A believable place.** Nothing reads as a primitive: no bare boxes, no one texture stretched across a wall, no
   lamp without a fixture, no fixture without a lamp, no floating or intersecting props, no blank sign plates.
3. **A lush island.** Green jungle hills, dense vegetation, real water. Never a desert.
4. **Night you can play.** With the flashlight off you can still see silhouettes outdoors (blue moonlit storm);
   lamps make warm pools; the flashlight reveals texture and depth, not a white disc on black.
5. **Coherence.** One palette, one lighting logic, one UI style across all chapters.
6. **It still plays.** The full route works from the security office to the dawn coast; no new console errors;
   60 fps at 1080p on this Mac (Apple M5 Max) in the editor's Play mode.

## 3. Palette and lighting logic
| Preset (enum `AtmospherePreset`) | Where | Look |
|---|---|---|
| `GoldenDay` | prologue | low warm sun (27°), blue sky with cumulus and moving cloud shadows, saturated tropical greens, blue shade |
| `DuskTransition` | prologue end | storm front, sun under the cloud deck, teal shade, last amber light |
| `NightCalm` | chapter 1, power on | moon through broken cloud, light rain, tungsten/sodium practicals |
| `NightStormBlackout` | chapters 2–6 | heavy rain haze, lightning, blue ambient; lamps are islands; red only as beacons |
| `DawnCoast` | ending | morning sun 8.5° over the sea, blue sky with gold-lit cumulus, pale steel-blue water with a bright sun path |

- **Warm vs cool:** artificial light is warm (2400–3600 K) or signal-coloured; nature at night is blue. Cool white
  (4300–5600 K) only for fluorescent tubes, floodlights and the flashlight.
- **Red** = emergency / danger accents (small pools, beacons). **Amber** = hazard and service points.
  **Green** = the escape route (AuxRoute bollards, E-0). **Cyan** = screens. Never flood a whole room in one
  saturated colour.

## 4. Exposure and units (measured, do not guess)
- HDRP physical units. Unity 6 stores point/spot intensity in **candela**, directional in **lux**, whatever the
  inspector's display unit says. `lux at a surface = candela / distance²`.
- Verified in this project: a 50 % sRGB grey card (21 % linear) 5 m from a 1000 cd lamp at EV 4 renders at 0.154
  linear, i.e. `pixel = lux × albedo / π / (1.2 × 2^EV)`.
- **`Color` values on materials are sRGB.** `SetColor("_BaseColor", grey 0.5)` is 21 % albedo; a tint of 1.5 is
  ×2.4 in linear.
- Exposure is **automatic (histogram) inside a narrow range per preset**: day EV 12.0–14.2 (compensation +0.7),
  dusk 6.5–10.5 (+0.15), night 3.3–5.0, storm 3.0–4.7, dawn 10.2–12.6 (0). The night world is balanced around
  **EV 4**. **Inside, the night exposure never drops below EV 3.4** (`Look_Indoor` overrides the lower limit; the
  runtime keeps the preset's own floor when it is higher): when the power dies the camera must not open two stops and
  show the dark room as bright as the lit one.
- **Tone curve: HDRP Custom on every preset, never ACES.** ACES crushed everything under about 2 % to black (measured:
  night ground 0.005 → 0.0015), which made the nights unreadable and the day look underexposed. The curve has almost no
  toe (0–0.10) and a long shoulder (`shoulderLength` 2.4 stops), so a hot spot at 2–4× white keeps its texture. All
  five presets use the same mode, so they cross-fade without a jump. The night presets also lift black to about 7/255
  (`blackLift` 0.0022). Bloom only above 0.9 (threshold), intensity 0.06–0.10.
- **Night colour is blue-grey, not teal:** white balance −1…−2 with tint +2 (negative tint is green), moon filter
  (0.86–0.88, 0.93, 1.0), saturation −10…−14, shadows only slightly blue. The far haze has its own dim blue-grey colour
  (`fogConstant`), so distant things stand as silhouettes.
- **No baked lighting.** Screen-space global illumination is on in every look: it is the real bounce between
  surfaces. Rays that leave the screen fall back to the reflection probes and then the sky; every room has its own
  probe, which wins over the sky inside its box (do not override `rayMiss` in `Look_Indoor`: an enum switches as soon
  as the volume has any weight and blackens the shadows of whatever is seen from inside). Indoors (`Look_Indoor`,
  faded in while the camera is enclosed) the bounce runs at its real strength: indirect diffuse 1.0 in a lit room at night, 2.2 in the blackout, plus a dim cool base of 1 lux from four sides and below while the camera is in an enclosed blackout room (`BlackoutBaseLux`: five shadowless directional lights; a dark-adapted eye sees a room, a camera at a fixed floor does not) (walls between the
  battery pools still read), 0.85 by day; reflections 0.25; occlusion also on direct light (0.65) so furniture sits on
  the floor. The grade is neutral in lit rooms and 5 points cooler in the blackout. (Until review 2 this multiplier
  was 0.20 'to keep the sky out', which also switched the bounce off: charcoal ceilings.) Each room's main lamp also
  gets a bounce light from under the floor (share 0.95) and a small wall-fill point light (0.11): see
  `LookLights.EnsureBounce`. **Battery units get neither** and run at 0.65 of their table value indoors: in a blackout
  they are islands (pools of 60–100/255), and the flashlight's pool is the brightest thing in the room.
- Sunset and moonrise are real: the key light sinks to 7° under the horizon and the new one rises from there; the look
  cross-fade follows the sun's height (`AtmosphereController.BlendRoutine`). Camera cuts reset exposure history. The
  ending does not blend: the night fades to black and the morning fades in settled (a storm night against a rising
  sun played the first shot with a clipped sky).
- **Sun and moon discs are drawn a few stops over white, not at their real radiance** (`ApplyDiscLook`: surface tint
  0.03, flare multiplier 0.00006–0.00011, flare 5–9°). At 10^8 nits the sun was a clipped four-degree blob with a lemon
  rim, bloom over a third of the sky and stair steps where clouds crossed it. The light itself is unchanged.
- **Lightning** is 85 lux in the storm (70 in the calm night) plus a glow on the whole cloud deck over the player
  (`LightningBoltController`, 8 nits) and on the clouds at the strike (14 nits): the sky carries the flash and the
  world shows as silhouettes that keep their texture. The glow is placed for the player's camera: judge lightning in
  Play-mode game views (`AtmosphereController.HoldLightningForCapture(1)`), never in free-camera captures.
- **Outdoor reflection probes hand over to the live sky while the sky is changing** (probe weight 0 during a preset
  blend and until the first capture after it): a twilight capture shown at the night exposure was a white rectangle of
  mirror on the wet plaza.
- Night targets on this game's dark surfaces (most albedos are 0.05–0.30): moonlight ≈ 10–14 lux on the ground,
  lamp pools outdoors ≈ 130 lux, lit rooms ≈ 110 lux on walls, emergency pools ≈ 35 lux.
- **Flashlight** (12,000 cd, `FlashlightController`): the beam is dimmed so its hot spot never lands above a budget
  that follows the surface: what the eye sees is lux × albedo. 120 lux on a surface of albedo 0.25 (brick), falling
  with albedo^1.5 (bright surfaces are the glossy ones): about 40 lux on white tile, 140 at most on dark paint, 30 at
  least. The albedo comes from `SurfaceAlbedoMap`, a table the Look Pass bakes for every solid collider
  (`LookAlbedoMap`, step `albedo`, last step of a scene build: walls are merged per material, so a collider no
  longer knows what it looks like). The distance is the median of five rays inside the hot spot, so a chair back in
  the way does not black out the room. The bounce is a 165° cone of 0.4 % of the beam along the surface normal,
  1.5–3.2 m off the surface: a wall sends light back into the room, not along itself (as a point light it lit the
  ceiling above it as brightly as the beam lit the wall). The cookie's soft field is 45 % weaker straight up (the
  hand and the bezel shade it), the shaft in the air is 0.22 indoors and 0.42 outdoors, and the arm has its own
  18 cd fill on rendering layer 5 (the glove sat at 6/255 in every blackout room). Measured in the Records room,
  wall pool / ceiling block: 148 / 161 in review 3, 146 / 85 now. Lamp lenses
  25 nits (`LookFixtures.DiffuserNits`), palette diffusers 90, signal colours 14–22 nits: brighter lenses bloom into
  blobs at night exposure.
- **All lamp values live in one table:** `Assets/Editor/LookPass/LookLights.cs` (regex on the light's name →
  candela, kelvin or colour, range, volumetric dimmer, shadows, throw). A builder may pass any intensity to
  `WorldArchitectureKit.BuildHdrpLight`; the Look Pass overrides it. **To add a lamp: place it in the builder with a
  clear name, then add a rule to the table** (unmatched lights are reported by `cretacea look_pass`).
- Ceiling fixtures are wide downward spots (`Throw.Down`), never bare point lights under a ceiling (the hot spot on
  the ceiling fools the auto exposure). Lamps cast cached shadows (static world); only the five lamps nearest the
  viewer also draw live shadows for moving things (`LampShadowBudget`: with all lamps live, the terrain was redrawn once
  per lamp per frame, 17 ms).
- Red emergency lamps are small pools (120–150 cd, no bounce). Never flood a room in one colour.

## 5. Materials
- Architecture materials come from `WorldMaterialPalette` (`Assets/Editor/WorldBuilder/WorldMaterialPalette.cs`):
  exact texture set, **world-space triplanar mapping** at a real tile size (so any box of any size shows the texture
  at the right scale), a smoothness range. Use the palette properties: `ConcreteFloor(/Wet)`, `ConcreteExterior`,
  `ConcreteBoardform`, `ConcreteCracked`, `AsphaltWet`, `PlasterClean` (white), `PlasterPeeling90s` (green),
  `DrywallPaint` (beige), `CeilingTiles`, `BrickOld`, `BrickPainted`, `LabTileWhite`, `FloorTileClean(/Wet)`,
  `CarpetDark`, `BrushedSteel`, `DarkPaintedMetal` (dark grey), `PaintedMetalGreen`, `PaintedMetalYellow`,
  `RustedIron`, `CorrugatedMetal`, `WoodPlanks`, `DarkGranitePlinth`, `RockyCliff`, `PavingStone`, `GravelGround`,
  `MudGround`, `RoofBitumen`, `GlassArchitectural`, `ChainlinkFence`, emissives (`EmissiveWarmWhite`, `…CoolWhite`,
  `…Red`, `…Amber`, `…Green`, `…Cyan`). Add a new one with `palette.Surface(...)` if you need it.
- Pick materials for what the surface IS: ceilings = `CeilingTiles`, office walls = `PlasterClean`/`DrywallPaint`,
  service corridors = `PlasterPeeling90s`/`ConcreteBoardform`, wet rooms = `LabTileWhite` + `FloorTileWet`,
  outdoor slabs = `ConcreteCracked`/`AsphaltWet`/`PavingStone`, roofs = `RoofBitumen`/`CorrugatedMetal`.
  Light walls and ceilings matter: they bounce light and make rooms read.
- Imported models (props, vehicles, rocks, plants) get their materials from `LookMaterials`
  (`Assets/Art/Materials/Models/…/MAT_<stem>.mat`, with the mask map bound; plants: alpha clip, two-sided,
  translucent). Do not hand-edit FBX-embedded materials.
- Moving parts (door leaves) automatically get UV-mapped copies (`…/World/Moving/*_UV0.mat`).

## 6. How the project is built (important)
- `MainWorld.unity` and `MainMenu.unity` are **generated**: `unity command cretacea build_all_scenes` rebuilds both
  from `Assets/Editor/WorldBuilder/*` (Zone 1–4 builders + `WorldArchitectureKit` + `CretaceaWorldAssembler`), then
  runs the Look Pass and saves. **Never hand-edit a scene; change the builder or the Look Pass.**
- The Look Pass (`Assets/Editor/LookPass/*`, command `cretacea look_pass`) owns: HDRP render settings, the five
  atmosphere Volume Profiles (`Assets/Resources/Atmosphere/Look_<Preset>.asset`, generated from
  `LookAtmosphereProfiles.cs`), materials, the lamp table, static shadow casters, the edit-mode preview.
- Runtime look code: `Assets/Scripts/Core/AtmosphereController.cs` (cross-fades the five profiles, drives sun/moon,
  lightning, wetness, rain), `Assets/Scripts/Player/FlashlightController.cs` (beam, cookie, bounce light).
- Subsystem maps written on Nov 20 (line references are to the handed-over code, before the look pass):
  `docs/maps_2026-11-20/notes_*.md` and `digest_*.md`.

## 6b. Performance settings that belong to the look
- GPU Resident Drawer + GPU occlusion culling are on in every HDRP asset (rooms hide the jungle behind their walls).
- Volumetric fog buffer: 16.67 % of the screen, 64 slices (`LookAtmosphereProfiles`). The project default was 50 % /
  96 slices and cost 20+ ms for the same picture.
- Camera far clip is 12 km (the sea must reach the horizon); `cretacea capture` uses the same value.
- Measured Nov 20 in the editor at 2313x1486: 107–146 fps at eight spots, 0.9–2.8 M triangles
  (`Tools/lookdev/perf.sh`). Standalone at 1080p: 105–118 fps (`-cretaceaSmoke`).

## 7. Driving the editor from the shell
```bash
export PATH="$HOME/.unity/bin:$PATH"; export UNITY_NO_BANNER=1
export UNITY_PROJECT_PATH=<project folder you were given>      # NEVER the owner's ~/CretaceaBlackoutUnity unless told
unity status                                                   # your editor: port, state "ready"
unity recompile "$UNITY_PROJECT_PATH" --format json            # compile; read errors from the JSON
unity command cretacea build_all_scenes --bake_navmesh false --timeout 1800   # rebuild scenes (edit mode only)
unity command cretacea look_pass                               # re-apply the look on the open scene
unity command cretacea look_preview --preset GoldenDay         # edit-mode atmosphere preview
unity command editor.playmode start ; unity status --until-ready --timeout 180
unity command cretacea jump_chapter --chapter Intro|Night|Corridor|ServicePaths|Substation|OldYard|Escape|DawnCoast
unity command capture game_view --source screen --width 1280 --height 720 --output-path Screenshots/x.png   # with HUD
unity command cretacea capture --output_path Screenshots/x.png --pos x,y,z --look_at x,y,z --fov 60 --width 1280 --height 720 --warmup_frames 6
unity command script eval --file some.cs                       # run a C# snippet in the editor
unity command editor.playmode stop
unity command console get --level error --tail 30 --format json
```
- Command arguments go after the group/name; `--project-path` (or the env var) must be set.
- Judge the look in **Play mode** (`capture game_view`): edit mode has no blending, no flashlight, no HUD.
- After a preset change wait ~3 s before capturing (auto exposure adapts).
- Changing an HDRP asset setting from code breaks the live renderer ("Blitter is already initialized") until a
  script reload: call `EditorUtility.RequestScriptReload()` afterwards.
- Stop Play mode before recompiling or rebuilding scenes.

## 8. Rules for everyone
- Stay inside the files you own. Need a change in someone else's file? Write it in your hand-in notes; do not edit.
- Keep public method signatures stable (add optional parameters at the end if you must).
- No new packages, no downloads, no paid generation. Use the assets already in `Assets/Art`.
- Zero compile errors and zero new console errors before you hand in. No new warnings.
- Gameplay geometry is sacred: do not move colliders, triggers, anchors, doors, interactables or NavMesh-relevant
  floors unless your task says so; if you add a solid object on a walkable route, prove the route still works.
- Process hygiene: stop only processes you started, by PID. Never `pkill` / `killall` by name. Never close or
  restart the owner's editor (the one with the window, project `~/CretaceaBlackoutUnity`).
- Images: read at most ~25 screenshots in total, never wider than 1280 px (`sips -Z 1280 file --out copy`).
- Hand-in: a short notes file (what changed, why, file list, what you could not verify) and a git commit on your
  branch. Never commit `Assets/Scenes/*.unity`, `Assets/Art/Terrain/CretaceaTerrain.asset` or other generated
  output unless your task says so.
