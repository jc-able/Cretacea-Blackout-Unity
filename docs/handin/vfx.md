# Hand-in: weather and effects (`look/vfx`)

Base `b234ad4`. Every claim below that says "verified" was checked on a Play-mode capture I looked at, or by a
number I read out of the running editor. Everything else is listed under "not verified".

## What changed and why

### 1. Rain that lives in the light (goal 1)
- `Cretacea/HDRP/RainParticle` (Assets/Shaders/CretaceaRainParticle.shader) is rewritten. The particle no longer
  emits a fixed number of nits. It reflects the light that reaches it:
  radiance = illuminance x scatter x phase, in nits, then HDRP pre-exposure, then HDRP fog
  (`EvaluateAtmosphericScattering`, including volumetric fog), so streaks fade into the haze.
  - Lights: the new `WeatherLightSampler` publishes the 8 strongest point/spot lights around the camera
    (candela x colour/kelvin x HDRP dimmer, range window, spot cone), with the flashlight always in slot 0. It also
    publishes the key directional light (moon, in lux), the lightning directional light (lux, live during a strike)
    and a sky ambient (0.6 x moon).
  - Phase: two lobes for water drops, forward (g 0.62) and back (g -0.35). Rain lit from behind glows around lamps;
    rain in the flashlight beam catches the light.
  - **No streak is brighter than its lamp.** Each light's contribution is capped: lamps 60 nits, signal lamps
    25 nits, flashlight 80 nits (lamp diffusers are 170 nits in the Look Bible). The total is capped at 110 nits.
  - Lamps under a roof are flagged. The rain ignores them, so an indoor lamp does not light rain through a wall.
- The streak system is configured in code (`StormRainOcclusionController.ConfigureParticleSystems`), so a rebuilt
  prefab cannot drift:
  - The emitter is a 34 x 17 x 34 m box that fills the whole fall column, 6 m ahead of the camera. Turning round
    never shows an empty patch.
  - Up to 9,800 drops/s at rain 1 (about 15k alive), falling at 8.5-10.5 m/s. Streaks are 12-20 mm wide, about
    0.55 m long (velocity-stretched), and at least 1 px wide on screen, so distant rain does not dissolve under TAA.
- Wind: gusting storm wind from the sea, 1-5.5 m/s scaled by rain, Perlin gusts, slow veer. It drives the slant
  and is published as `_CretaceaWind`.
- Rain density follows `_AtmoRainTarget`: storm 1.0, night-calm 0.45, none by day or at dawn.
- Verified on captures: streaks are bright and dense in the flashlight beam, faint blue in moonlight, invisible
  where there is no light, and slanted by the wind.

### 2. Never indoors, always outside the window (goal 2)
- The dead `Camera.RenderWithShader` roof map is replaced by `RoofOcclusionMap`:
  - It collects every RainBlocker slab (layer 13 renderers, plus colliders if any) as an oriented box.
  - It rasterises a top-down roof-height window on the CPU: 384^2 RFloat over 64 m, so 0.17 m per texel.
  - The window is redrawn when the player has moved 6 m, which costs well under 1 ms.
  - It fills the same `_CretaceaRainOcclusionTex` / `_CretaceaRainOcclusionParams` globals the shaders already
    read. `.w = 1` marks the map as valid.
- The rain shader discards every fragment below the roof top. Under a roof there is no rain; rain outside the
  window keeps falling. The broken lobby skylight panes (18, 25, 26) have no blocker by design, so a rain shaft
  falls through them into the lobby.
- The stop-gap `_coverBlend` (a raycast that could never hit, because the blockers have no colliders) is removed.
- Splashes, drips and the light sampler all use the same roof data (`RoofHeightAt`, `IsCovered`).
- Verified by numbers: under the Records corridor the roof sits at 3.62 m (covered = true). Outside the lobby glass
  there is no roof, while 0.5 m inside it the roof is at 11.36 m.
- Verified on captures: no rain in the Corridor or the Night office; rain falls in the lobby only under the broken
  skylight.

### 3. Surfaces answer the rain (goal 3)
- **Ripples and crowns.** Up to 3,200 impact raycasts/s in a 10 m disc (two thirds of them in front of the camera)
  emit ripple particles: an expanding double ring plus an impact dot. Half of the impacts also throw two crown
  droplets that sparkle in the beam. Nothing spawns on covered or steep ground.
- **Roof-edge drips.** Eave points are found from the roof map, on the open-sky side of each slab edge, 1.8-14 m
  above the player. Drips run off at about 3.5 drips/s per metre of edge. At the dock: 306 eave points and about
  570 live drips and crown droplets, read out of the running editor.
- **Puddles.** The `LookWeather` step places up to 220 HDRP decals (`WxPuddle_*`):
  - Only on open-sky asphalt, concrete, paving, gravel or mud that is flat (normal.y > 0.985).
  - Clustered by noise, 1.4-4.3 m, using a generated 2x2 atlas of ragged puddle shapes (`wx_puddle_atlas.png`).
  - `MAT_WetPuddleDecal` is configured as dark, still water: smoothness 0.96, flat normal, base-colour alpha as
    opacity.
  - The zone builders' own `PathPuddle_*` decals get one atlas cell each.
  - Puddles fade in only when the ground is soaked (wetness > 0.45).
- **WetnessDriver.** At start every outdoor renderer gets runtime clones of its HDRP/Lit materials:
  - "Outdoor" means not under a RainBlocker, not skinned, not on the viewmodel, blocker or UI layer.
  - Material ASSETS are never modified. Indoor renderers keep the shared materials, so rooms stay dry.
  - Soaking happens in about 15 s and drying takes about 90 s. Porous rough surfaces darken to x0.68, and
    smoothness rises toward 0.52-0.86.
  - The palette's authored-wet materials (`*Wet*`: AsphaltWet, MudGround, ConcreteFloorWet) work the other way
    round: they dry out (lighter, duller) when the weather is dry, so the GoldenDay roads are no longer glossy.
  - If the wetness target jumps by more than 0.3 in a single frame, the driver snaps to it. That only happens on a
    checkpoint load or chapter jump; story blends take 4-8.5 s and still soak and dry gradually.
  - Verified: 49 outdoor materials were cloned. In a wet/dry A/B on the ServicePaths walls they visibly darken and
    get a sheen. At DawnCoast the wetness was read falling from 1.0 toward the preset's 0.5 (0.90 after 8 s).

### 4. Air (goal 4)
- `LookWeather` places `LocalVolumetricFog` volumes with a generated tiling 3D noise (`wx_mist_noise3d.asset`,
  32^3), scrolling slowly:
  - 15 stream mist ribbons hugging the water (3.4 m tall, mean free path 4 m).
  - One valley ground layer at the stream's level (mean free path 13 m, top at floor + 5.8 m), so the mist settles
    only in the low ground.
  - Lobby haze (mean free path 120 m) and warehouse haze (90 m).
- **Dust motes** (`DustMotesController`): about 700 tiny motes (7-14 mm) drift in a 7 m box ahead of the camera,
  only indoors. They are lit only by the flashlight and the room's own lamps (no sky light), so they appear in the
  beam and vanish outside it. Verified in the Corridor capture.
- The mist is subtle by design. Measured A/B in the valley: flashlight off, the mist band is +15 % brighter;
  flashlight on, the beam centre is +21 %, and the beam is not whited out.

### 5. Lightning you can see (goal 5)
- `LightningBoltController` watches `_LightningFlashIntensity`. On every strike (rising edge) it puts a soft
  lit-cloud glow where the strike is. In 55 % of strikes it also builds a forked bolt:
  - A camera-facing ribbon mesh with midpoint displacement and 3-6 branches, 380-900 m away, from the cloud base
    (260-380 m) to the ground.
  - The bolt comes from the lightning light's direction, pulled into view 60 % of the time.
  - It uses the new `Cretacea/HDRP/LightningBolt` shader: additive, 9,000-nit core, pre-exposed, partly fogged.
    Its brightness follows the flash level frame by frame, so it flickers with the double stroke and dies with the
    fade.
- `ForceBolt()` exists for tests and scripted moments.
- Verified: a natural strike at Substation showed a forked bolt over the fence line.

### 6. Sparks (goal 6)
- `LookWeather` restyles `ShutterSparksVfx` and `PathLampSparksVfx`, which the zone builders made with HDRP/Unlit
  and an emissive material:
  - Self-lit stretched streaks (`MAT_WxSparks`, rain shader in spark mode, 2,600 nits, white-hot to amber).
  - World collision with bounce.
  - A `SparkGlow` component: a runtime warm point light (70 cd peak, 2600 K, 6 m, flickering) that follows the
    number of live sparks. It is created at runtime, so the lamp table never sees it.
- StoryDirector still triggers the bursts as before.
- Verified: in the lobby shutter capture, sparks fall and bounce, and the glow read 57 cd during a burst.

## Performance
Measured in the ServicePaths start view, flashlight on, storm. I used FrameTimingManager medians over 400 frames,
interleaving weather off and on twice:
- off 17.25 / 18.01 ms CPU-main and 19.77 / 21.18 ms GPU
- on 16.74 / 18.57 ms CPU-main and 19.52 / 21.56 ms GPU

The difference is inside the run-to-run noise (about 0.5 ms), within the 1.5 ms budget.
`diagnostics get_performance` single samples swing 10-80 ms in the hidden editor and are not usable for this.

## Files
Owned and changed:
- `Assets/Scripts/Rendering/StormRainOcclusionController.cs`. The public API is kept: `RainIntensity`,
  `GlobalWetness`, `ForceRefreshOcclusion()`, `SetFollowTarget()`. Added: `Instance`, `Wind`, `RoofHeightAt()`,
  `IsCovered()`, `RecollectRoofs()`, `RoofEdgeCount`, `DripCount`. The legacy serialized fields are kept so
  scenes and prefabs deserialize cleanly.
- `Assets/Shaders/CretaceaRainParticle.shader`
- `Assets/Settings/Rendering/MAT_RainStreaks.mat`, `MAT_RainSplashes.mat`, `MAT_WetPuddleDecal.mat`

New:
- `Assets/Scripts/Rendering/RoofOcclusionMap.cs`, `WeatherLightSampler.cs`, `WetnessDriver.cs`, `DustMotesController.cs`,
  `LightningBoltController.cs`, `SparkGlow.cs`
- `Assets/VFX/Shaders/CretaceaLightningBolt.shader`, `Assets/VFX/MAT_WxSparks.mat`, `Assets/VFX/Resources/WxLightningBolt.mat`
- `Assets/Art/VFX/wx_puddle_atlas.png`, `Assets/Art/VFX/wx_mist_noise3d.asset` (generated locally by `LookWeather`)
- `Assets/Editor/LookPass/LookWeather.cs`

The companion components (`WeatherLightSampler`, `WetnessDriver`, `LightningBoltController`, `DustMotesController`)
are added by the rain controller at runtime, so the prefab is unchanged.

Touched outside the package, as the brief allowed: `Assets/Editor/LookPass/LookPassCli.cs`. It adds the `weather`
step to `AllSteps`, to `Run`, and to `RunOnBuild` (MainWorld only).

Not touched (unchanged): `CretaceaRainOcclusionDepth.shader` (now unused), `CretaceaWetSurface.shader` (still reads
the same globals and works with the new map), `PF_StormRainSystem.prefab`.

## Lamp rules added
None. The spark glow lights are created at runtime, so they are not subject to the lamp table.

## Requests for the lead (files I do not own)
1. **Stream water glows white or orange at night (high priority).** The 29 `StreamWater_*` boxes in Tyrant Valley
   use `MAT_DawnOcean` (unlit ocean shader, `_LuminanceScale` 2600). At night they render as blinding light bars;
   standing next to the stream blows out the whole frame. This also hides the valley mist. It belongs to the
   materials/water owner (Zone4 builder + `CretaceaOcean.shader`): give the stream its own lit or low-luminance
   material.
2. **Strike brightness.** At Substation a strike (`KeyLook.lightningLux` 320) turns the concrete yard nearly white.
   Consider about 150-200 lux, or a lower lightning volumetric dimmer (`AtmosphereController`).
3. Optional: `AtmosphereController.lobbyBrokenSkylightRainSystems` / `SetLobbySkylightRain` are now redundant. The
   global rain already falls through the broken panes because they have no RainBlocker.
4. Optional: the terrain cannot be made wet or dry at runtime without cloning its TerrainData or TerrainLayers
   (assets). If the terrain should react to rain, the terrain owner could expose a wet and a dry layer set per
   preset.

## Not verified
- Drips from roof edges: verified by count (306 eave points, about 570 live drips/droplets at the dock), but I did
  not get a clean capture of an eave with drips; my one framing faced a wall.
- That the mist reads well in the final valley, because the stream glow (request 1) dominates every valley view.
  I measured the mist A/B numerically with the stream renderers hidden at runtime.
- Behaviour in a standalone player build. The lightning material loads from `Resources` and the mote and drip
  materials are cloned from the rain material, which should keep the shaders in a build, but I did not build a
  player.
- Rain in the DuskTransition preset (rain 0.25). I did not capture it.
- 1080p / 60 fps in the windowed editor. All timings come from the hidden 1280x720 editor.

## Console at hand-in
- Zero compile errors and zero warnings from `unity recompile`.
- After the final NavMesh rebuild and a Play session through ServicePaths, Substation, Corridor, the lobby,
  Tyrant Valley, DawnCoast and Intro: zero console errors.
- One warning: "ScriptableRuntimeReflectionSystemSettings.system is assigned more than once". HDRP prints this when
  it re-initialises after a script reload; it is not raised by this package's code.

## Known rough edges
- The ripples are particles on any open-sky flat ground. They do not follow sloped terrain closely, and none
  appear on slopes steeper than 53 degrees.
- The puddles are placed by noise clusters, not by real low points (the slabs are perfectly flat boxes).
- The wetness driver clones materials once at scene start; renderers spawned later stay dry.
- Lightning bolts are a single mesh per strike. The sky glow is a billboard, not a cloud-volume light.

---

# Fix round (art director review of 3402845)

Every number below was read from a Play-mode capture or out of the running editor in this round. Captures:
`Screenshots/handin/` (index in `INDEX.md`); the round-1 captures are kept in `Screenshots/round1/` as the "before".
Where this section disagrees with the text above, this section is current.

## Findings and what I did

1. **Wetness driver halves the lit night (high).** Reproduced: same view, round 1 cut the ServicePaths walls and lamp
   patches by 44-77 % and the Substation beam hotspot by 44 %.
   - `WetnessDriver`: clones are now keyed by (material, up-facing). Up-facing means `bounds.size.y < 0.5 x min(size.x, size.z)`.
   - The bottom of the smoothness range rises by at most +0.05, capped at 0.5 (`Min(smoothMin + 0.05, 0.5)`). The old
     `Max(smoothMin, 0.52)` killed the rough retro-reflection that the flashlight, which sits at the camera, relies on.
   - Walls: darkening 0.92, sheen x 0.3, wet top halfway to the floor value.
   - Floors: darkening 0.9 (was 0.68), wet smoothness max 0.7 (was 0.86). I picked these with a runtime parameter
     sweep in the Substation view (A/B at wetness 0 and 1). 0.78 and 0.8 still cost the beam 34-37 %.
   - Result against the b234ad4 captures:
     - ServicePaths, 01 → 02: left wall -3 %, lamp patches +6 % and -1 %, right wall -5 %, kiosk roof +7 %.
     - Substation, 06 → 07: beam hotspot -22 %, left yard -55 %. Part of that yard region lies on the SubPad_Puddle1
       decal, and a direct wetness 0 → 1 A/B of the same frame gives -15 to -21 %.
   - No -0.5 EV request is needed for ServicePaths. Substation is at the limit.
2. **Puddle decals project onto walls (high).** `LookWeather.PlacePuddles`:
   - The decal sits at hit + 0.05 m, pivot (0, 0, 0.1), depth 0.25 m, so the box spans 17 cm under to 7.5 cm over the
     ground. Angle fade is 20-40°.
   - A spot is accepted only if 8 edge rays (corners and edge midpoints) hit the same collider within 3 cm and
     `Physics.CheckBox(hit + 0.45 up, (w/2, 0.35, h/2), yaw)` is empty. Otherwise the puddle shrinks x0.7 once or is skipped.
   - `NormalizeBuilderPuddles` gives the builders' puddles (PathPuddle_*, SubPad_Puddle*, Lobby_SkylightPuddle_*) the
     same box and angle fade.
   - The 4.6 m ServicePaths road now gets no placed puddles, only the builder's PathPuddle_1-3. Capture 05 (right)
     shows the wall base clean.
3. **Puddles read as shadows (medium).**
   - New material `MAT_WetPuddleDecal_Atlas.mat`: base colour 0.55, decal blend 0.6, smoothness 0.96.
   - It uses a ripple normal flattened to 30 %, `Assets/Art/VFX/wx_puddle_ripple_normal.png`. LookWeather generates
     it from `rain_ripples_normal.png`, because HDRP decals have no normal scale.
   - The puddle fade window is now wetness 0.2-0.6 (was 0.45-0.9), so the puddles fill early and are still there at
     dawn (fade about 0.7 at wetness 0.5, read out).
   - Lightning in the water: the material has Affect Emission on. `WetnessDriver` drives `_EmissiveColor` on one
     runtime copy as (0.6, 0.7, 1) x 6 nits x `_LightningFlashIntensity`.
   - **Deviation:** I did not author a Decal Shader Graph. A hand-written graph file is the riskiest thing in this
     package and cannot be checked in the editor UI here. Without Fresnel, 6 nits is the 30 nits of the brief times an
     average grazing Fresnel of about 0.2.
   - Verified: the emission value follows the flash (read out), and 05 (right) shows the puddle mirroring the lamps.
   - Not verified: I have no capture that isolates a puddle during a strike.
4. **A natural restrike spawns a second bolt (medium).**
   - Added `_lastStrike`; the edge test is now `flash > 0.35 && prev < 0.15 && Time.time - _lastStrike > 0.6 &&
     Time.time >= _forcedUntil`, and `OnStrike` sets `_lastStrike`.
   - Verified by counters: 6 × `TriggerLightningFlash(0.6)` raised `StrikesSeen` by exactly 6 (7 → 13). Before, each
     one counted twice, because the restroke dip goes to 0.12 and re-rises to 0.51.
   - The hand-in strikes 04 and 09 are natural strikes (no ForceBolt). 09 shows a forked bolt.
5. **Ripples glow like coins (medium).**
   - Ripple start size is 0.07-0.18 m. The impact dot is x0.4 (was 1.6).
   - `MAT_RainSplashes`: scatter 0.08, opacity 0.6. The brief asked for 0.04 / 0.5, but at those values the rings
     vanished completely in the beam, so I measured and settled on 0.08 / 0.6.
   - Rings spawn only where the hit collider's material is a palette `*Wet` material (AsphaltWet, ConcreteFloorWet,
     FloorTileWet) or inside a filled WxPuddle/PathPuddle footprint, looked up through `WetnessDriver.IsInPuddle`.
     Elsewhere a drop throws only its crown droplets.
   - Capture 05 (left): small faint rings, no bullseyes.
6. **Mist and haze invisible (medium).** Root cause: the single ValleyGroundMist box sat at the stream bed. That is the
   minimum water height, -2.9 m, while the valley floor lies between -1.5 and +2 m, so the mist was under the terrain.
   - `PlaceMist` now tiles the valley into 34 × 46 m cells. Each cell sits on its own ground height (25th percentile of
     25 rays), and only the low half of the valley gets mist (28 cells). Each cell is a box from floor -0.5 to +1.3 m,
     45 % top fade, mean free path 2.5 m, Max blending, distance fade 110-180 m.
   - Stream ribbons: mean free path 2 m, height 2.2 m, Max blending.
   - Lobby haze 45 m, warehouse haze 40 m.
   - Measured, StreamWater_* hidden, looking toward the moon:
     - Flashlight off: mist band +42 %.
     - Flashlight on: band +32 %, beam centre +24 %.
   - Lobby haze A/B with the flashlight on: upper air +67 %, beam +36 %.
   - Looking away from the moon, the moonlit mist adds only +7 %. The global fog anisotropy (0.55-0.62) back-scatters
     little; that is the lead's fog setting, not mine.
7. **Rain streaks: white needles and bead chains (medium).** velocityScale 0.022 (about 0.2 m), `_NearFade` (0.8, 3.0),
   `_MaxNits` 70, flashlight cap 45 nits, minParticleSize 0.0026. The shader multiplies streak alpha by `saturate(14 / camDist)`.
   To keep the shorter streaks readable, the streak opacity went from 0.55 to 0.7. Pixels ≥ 200 in one frame: 171 in the
   close-up and 173 in 02 (round-1 close-up: 875). No pixel reaches 250.
8. **Main menu changed through the shared puddle material (medium).**
   - `MAT_WetPuddleDecal.mat` is restored byte for byte from b234ad4. The world's puddles use the new
     `MAT_WetPuddleDecal_Atlas.mat`, assigned in `PlacePuddles` and `NormalizeBuilderPuddles`.
   - Verified in Play mode: the MenuPuddle_* decals use MAT_WetPuddleDecal with uvScale 1, and the menu frame matches
     the critic's baseline (16, last panel).
9. **Puddle cap cut off the east (medium).**
   - All accepted spots are collected first (236). Then up to 30 per 100 × 100 m cell are kept in hash order, cap 400.
   - Result: 138 placed in 9 cells, of which 72 are between x 400 and 550 (Escape court, 13). The stricter footprint
     test is why the total is lower than round 1's 220.
10. **Lamp-lit rain (medium).** `WeatherLightSampler`:
    - A lamp counts as indoor only if it is covered AND all 8 points of a 1.5 m ring around it are covered.
    - Slot picking: flashlight first, then at least 5 outdoor lamps, then the rest by score. Indoor lamps stay flagged
      for the motes.
    - A lamp with no line of sight to the camera (a ray from 0.4 m off the lamp, layers 0 | 3, window glass ignored)
      gets colour x0.25.
    - Verified by read-out: inside the office the two outdoor lamps outside the window take slots 0-1.
    - **Extra bug found and fixed** while checking the window view: no rain was visible through any window, even
      during a strike. `MAT_GlassArchitectural` has "receive SSR (transparent)" on, which renders a transparent depth
      prepass, so every particle behind glass failed the depth test.
      - Proof: hiding the 12 GlassPane renderers made the rain appear.
      - `StormRainOcclusionController` now gives window glass a runtime copy with `_ReceivesSSRTransparent = 0` and
        the prepass off. The asset is untouched.
      - Frame-difference test in the window region: 894 moving pixels with rain, 7 without (round 1: 1 vs 6).
      - Capture 11 shows the rain sheet outside during a strike, with the room dry.
    - Lamp-lit rain with the flashlight off: at ServicePaths the only lit lamp is the 420 cd amber kiosk lamp, 14 m
      away, so its rain is physically dim (03). At Substation (08) the yard lamps are on.
11. **Spark glow (low).**
    - `SparkGlow`: 220 cd, 4.5 m range, hard shadows while burning (switched off with the light).
    - `PathLampSparksVfx` had emission rate 0 and no bursts, so it never emitted when StoryDirector played it.
      `StyleSparks` now gives such a system a looping burst of 14-28 sparks every 1.7 s (probability 0.75).
    - 14: lobby shutter burst (glow 137-193 cd read out) and path lamp burst with a warm glow on the nearby wall edge.
12. **Drips (low).**
    - The drip material keeps `_UseRoofOcclusion = 1`.
    - With that on, every drip was discarded, because the eave points sat inside the eave's own texel (z -9.70 under
      a roof that ends at -9.5). `FindRoofEdges` now walks outward to the first open (or lower-roof) texel and starts
      the drip one texel past it.
    - Verified by read-out: 259 live drips at the warehouse eave. A drips-only frame differs from a no-drips frame
      in 6,620 pixels along the wall.
    - The capture (15) shows them only faintly.

Ownership: `LookPassCli.cs` is back to the two allowed lines. The extra `weather` entry in `AllSteps` is reverted, so
`look_pass` "all" no longer runs it; run it with `--steps weather` or on build.

## Files touched in this round
- `Assets/Scripts/Rendering/WetnessDriver.cs`, `WeatherLightSampler.cs`, `LightningBoltController.cs`, `SparkGlow.cs`,
  `StormRainOcclusionController.cs`
- `Assets/Shaders/CretaceaRainParticle.shader`
- `Assets/Editor/LookPass/LookWeather.cs`
- `Assets/Settings/Rendering/MAT_RainStreaks.mat`, `MAT_RainSplashes.mat`, `MAT_WetPuddleDecal.mat` (restored to b234ad4),
  new `MAT_WetPuddleDecal_Atlas.mat`
- New `Assets/Art/VFX/wx_puddle_ripple_normal.png` (generated locally)
- `Assets/Editor/LookPass/LookPassCli.cs` (reverted the `AllSteps` edit)

## Performance
These are `diagnostics get_performance` medians in the ServicePaths view, flashlight on, weather on vs off: the rain
object and the `[Weather]` root disabled, interleaved, 25-40 samples each.
- GPU: on 29.7 / 26.4 / 28.4 ms, off 19.7 / 28.5 / 28.4 ms.
- CPU: on 21.0 / 22.5 / 26.1 ms, off 18.8 / 26.9 / 22.5 ms.

The hidden editor swings more than the effect, so I cannot confirm the 1.5 ms budget to better than about ±3 ms.
The last, longest pair is GPU-equal and +3.6 ms CPU at the median, +1.0 ms at the 25th percentile. What this round
added: 28 small mist volumes in place of 1, a decal emissive pass, at most 8 short raycasts per frame, and spark
shadows only during a burst. It removed the glass transparent depth prepass.

## Requests for the lead (new)
1. **Window glass (`WorldMaterialPalette.BuildGlassMaterial`):** set `_ReceivesSSRTransparent` to 0, so the transparent
   depth prepass is off at the source. Until then the runtime copy in `StormRainOcclusionController` does it.
2. **SSR in the night profiles**, so the puddles mirror the lamps and the sky, not only the probes.
3. **Fog anisotropy** (0.55-0.62 in the night profiles): the moonlit valley mist is visible only toward the moon.
   About 0.3 would let it read from every direction.
4. Optional: the CLI `jump_chapter DawnCoast` leaves the end panel over the game view for later jumps (a known flow
   issue). It hid several of my first captures.

## Still not verified / rough edges
- A puddle during a strike (emission) on its own capture. The emission value is verified only by read-out.
- Drips read only faintly in the capture (15).
- Lamp-lit rain around a strong lamp with the flashlight off: 08 shows the Substation yard lamps, but the streaks
  there are small at 720p.
- The 1.5 ms budget: see Performance.
- Standalone player build: not built. The glass copy uses `HDMaterial.ValidateMaterial`, which is runtime API.

## Console at hand-in (this round)
- `unity recompile`: 0 errors, 0 warnings.
- Final `build_all_scenes` with NavMesh (raptor and T. rex baked).
- Play smoke test: Night, Corridor, ServicePaths, Substation, OldYard, Escape, DawnCoast, Intro. Console: 0 errors,
  0 warnings.
