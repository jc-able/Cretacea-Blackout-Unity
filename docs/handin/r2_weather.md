# Round 2 hand-in: weather (rain, wet surfaces, puddles, storm haze, lightning)

Branch `fix/weather` (base 8ed04fa), clone `~/CretaceaUnityClones/vfx`. Every finding was checked in a capture of my
clone first (`Screenshots/wx_before/`, taken on the base commit, with the lead's 07fd73d fixes in). Each fix is proven with
a same-view before/after pair in `Screenshots/handin_r2/` (see its INDEX.md).

## What changed and why (by finding)

### 1. Rain read as snow (major): fixed
Still true on the base: short, fat white dashes of one size all over the dark sky (`01_before_22_green`).
- `StormRainOcclusionController.ConfigureParticleSystems`: streaks are now motion-blurred lines **0.45-0.8 m** long
  (`velocityScale` 0.022 → 0.075), drops 3-7 mm wide (was 12-20 mm), fall speed 6-10 m/s per drop (was 8.5-10.5),
  wind 0.7-1.3x per drop (stronger slant variation), per-drop opacity 0.55-1, min screen size ~1.5 px (was 2-3 px).
  Fewer, fainter splash-crown droplets (they were the white dots at the feet).
- `CretaceaRainParticle.shader` (streak mode): an even line with soft ends instead of a pointed dash; per-drop
  brightness 0.45-1.3. **Opacity follows real pixel coverage** (`_CoverageDistance` 3.5 m / distance), so rain is dense
  next to the camera and thins into a faint sheet, never bead chains. Per-lamp and total luminance caps are now a soft
  shoulder (`L/(1+L/cap)`) instead of a hard clamp, so drops in the torch's hot spot stay brighter than drops in its spill.
- `MAT_RainStreaks` (set by `LookWeather.ConfigureStreakMaterial`): alpha 0.7 → 0.5, `_MaxNits` 70 → 22, `_SkyLight`
  1 → 0.3 (moon and sky give only a blue-grey hint, ~1 nit), occlusion 0.06.
- Result: `02_after_22_green`, `04_after_19_sub`, `08_after_27_coast`: thin slanted streaks, bright only in the beam and
  near lamps, invisible against the black sky far from light.

### 2. Flat light-grey rectangles on the ground (major): fixed (two causes)
- Puddles (`19` right of the hand): `MAT_WetPuddleDecal_Atlas` blended smoothness and normal from "mask-map blue x fade"
  (`_MaskBlendSrc/_NormalBlendSrc = 1`, no mask map) = the whole projector box, over a **light** 0.55 grey albedo.
  `LookWeather.ConfigurePuddleMaterial`: both blend sources 0 (atlas alpha), dark water albedo (sRGB 0.15-0.17, 92 %),
  smoothness 0.97, and the lightning flash emission masked by a new `Assets/Art/VFX/wx_puddle_mask.png` (RGB copy of the
  atlas alpha). Verified in HDRP's `DecalData.hlsl` (line 97/154: 1 = maskMapBlend, 0 = base-colour alpha).
- The road rectangle in `23` was **not** a puddle: it is `YardOil_0`, a palette grime decal (`MAT_DecalGrimeLeak`) with the
  same `_MaskBlendSrc = 1` bug, stamping a matte 0.5-smoothness box on the glossy wet slab. Fixed at runtime in
  `WetnessDriver.FixOutdoorDecals`: outdoor decals without a mask map get runtime copies that blend from their own alpha
  and wet up with the slab (601 decals, a handful of materials). Request for the lead below (fix it in the palette).
- Result: `04_after_19_sub` (dark puddles mirroring the red beacon), `06_after_23_yard`.

### 3. Wet ground sparkled like glitter / hoar-frost (major): fixed
- `TerrainWetnessDriver`: per-layer wet profiles. Gravel/soil/grass drain (darker, gloss capped at 0.40-0.46, gravel
  0.42), mud holds a film (0.70), rock 0.58. Normal maps flattened as water fills the relief (`_NormalScale0-7` x
  0.55-0.8). Gloss follows wetness² (storm 1, night 0.56, dawn 0.25, dusk 0.12), so dawn/dusk terrain stays rough
  (also fixes the dawn "grass standing on a mirror", `28_ending_1`).
- `WetnessDriver`: surface classes. Draining ground (gravel, mud, grass, soil) gets darker (x0.74) with a low sheen
  (max 0.5), normals x0.65. Authored-wet asphalt (`MAT_AsphaltWet`, smoothness up to 0.92) is capped at 0.85 when soaked
  with normals x0.34: that was the coast-road glitter under the low storm moon.
- Not done: geometric specular AA. It only acts on geometric-normal curvature (useless on flat slabs and terrain), and a
  keyword enabled on runtime clones has no variant in a player build. Flattening the normals is what removed the sparkle.
- Result: `02_after_22_green` (dark wet gravel, no speckle), `15_after_coast_storm` (smooth sheen, no sparkle field).

### 4. Plaza bone dry in the rain (major): fixed (three causes)
- No puddles: `LookWeather.IsPuddleSurface` did not accept `MAT_Z1_PlazaPaving` / `PlazaStrip`: now 49 puddles on the plaza.
- No reflections: HDRP SSR skips every pixel below `minSmoothness` (default **0.9**, not set in the lead's profiles), so
  wet ground at 0.5-0.86 only mirrored the dark sky probe. `WetnessDriver` adds a runtime global Volume (priority 50) that
  overrides **only** the SSR smoothness thresholds (min 0.5, fade start 0.62), weighted by the water film (0 when dry: day
  and dawn unchanged) and **only while the camera is outdoors** (`WeatherLightSampler.CameraIndoors` false, blended over
  ~0.7 s), so rooms and the dock canopy keep their probe reflections and no other package's interiors change. Measured on
  the camera's volume stack: min smoothness 0.9 → 0.5 outdoors in the storm, 0.9 under the dock canopy.
- Too light to read: wet stone loses 30-50 % albedo; the old slab darkening was ~6 %. Flat sealed slabs (paving, plaza,
  concrete, asphalt, tile, stone) now darken to x0.6, the film covers the whole slab (smoothness range bottom +0.48, up to
  0.76; top 0.9) and the normal map drops to x0.4.
- Merged meshes (`Z1_Merged_MAT_BrushedSteel_0`, railings etc.) were classed as floors because their bounds are flat:
  now only ground materials count as floor in a merged mesh.
- Result: `10_after_plaza_nightcalm`, `12_after_plaza_storm`: darker wet paving, bollard and lamp-post reflection streaks,
  the shop window mirrored in the slab. The near lamps are 120° downlights at 4.3 m: their mirror points are right behind the
  hand or outside their own cone, so that particular view shows the reflections near the horizon, not under the hero lamp.

### 5. The storm did not read as a storm from the plaza (major, downgraded to minor by the check): improved
- **Rain curtains** (new `Assets/Shaders/CretaceaRainCurtain.shader`, material `Assets/VFX/Resources/WxRainCurtain.mat`,
  built in `StormRainOcclusionController.EnsureRainCurtains`): two camera-centred rings at 55 m and 110 m (one mesh, 192
  triangles). Thin falling threads with wind slant and drifting gust bands, lit by the weather light globals (sky, moon,
  lightning, cloud flicker), depth-faded against the scene, soft-capped at half the scene white, opacity x rain^1.5 (dusk
  rain 0.25 barely shows, the storm does). The far hill now stands behind a rain haze (`12_after_plaza_storm`).
- **Distant cloud flicker** (`LightningBoltController`): between the lead's main strikes (13-32 s apart in the storm) the
  cloud deck flickers somewhere ahead every 2.5-6.5 s (2-4 pulses), glowing on the clouds and on the rain curtains. No light,
  no thunder, no shadows: the strike light stays the lead's. `13_after_plaza_storm_flicker` shows the hill silhouetted.
  Test hook: `LightningBoltController.ForceFlicker(holdSeconds)`.
- The real fix for the black storm sky and the hazeless far hill (fog colour = sky colour, depth extent 150 m) is the
  lead's: see requests.

### 6. Other weather items
- Dock specks (`13`, minor): they were the indoor dust motes, which switch on under any roof, the dock canopy included.
  `DustMotesController`: 90 → 32 motes/s, max 700 → 260, 4-9 mm, alpha 0.5, cap 24 nits. `17_after_13_dock`: the specks are gone.
- Junction puddle milky (`15`, minor): fixed by the puddle material (dark water, shaped gloss). `19_after_15_junction`.
- Dawn: puddles now fill at wetness 0.35-0.75 (were 0.2-0.6), so at dawn (0.5) they are faint, not mirrors on the yard
  slab (check file, `28_ending_2`). DuskTransition (rain 0.25, wetness 0.35): light rain, no curtains, faint sheen
  (`20_after_dusk`). GoldenDay: no rain, no puddles, no curtains, no flicker (`21_after_day`). DawnCoast: no rain
  (`22_after_dawn`).

## Files touched
- `Assets/Scripts/Rendering/StormRainOcclusionController.cs` (streak tuning, crowns, rain curtains)
- `Assets/Scripts/Rendering/WetnessDriver.cs` (surface classes, slab film and darkening, normals, wet SSR volume, outdoor
  decal fix, puddle fill curve)
- `Assets/Scripts/Rendering/TerrainWetnessDriver.cs` (per-layer wet profiles, normal scale, wetness² gloss)
- `Assets/Scripts/Rendering/LightningBoltController.cs` (distant cloud flicker, `ForceFlicker`)
- `Assets/Scripts/Rendering/DustMotesController.cs` (sparser, finer motes)
- `Assets/Editor/LookPass/LookWeather.cs` (puddle material and mask, plaza puddles, streak material, curtain material)
- `Assets/Shaders/CretaceaRainParticle.shader` (streak shape, coverage opacity, soft caps)
- `Assets/Shaders/CretaceaRainCurtain.shader` (new), `Assets/VFX/Resources/WxRainCurtain.mat` (new)
- `Assets/VFX/Shaders/CretaceaLightningBolt.shader` (`_UseLocalFlash/_LocalFlash` for the flicker)
- `Assets/Art/VFX/wx_puddle_mask.png` (new), `Assets/Settings/Rendering/MAT_RainStreaks.mat`,
  `Assets/Settings/Rendering/MAT_WetPuddleDecal_Atlas.mat` (regenerated by the Look Pass)
- No lamp rules added. Public signatures unchanged (only additions: `ForceFlicker`, `FlickersSeen`).

## Requests for the lead
1. `WorldMaterialPalette.CreateDecalMaterial`: set `_MaskBlendSrc = 0` and `_NormalBlendSrc = 0` for decals without a mask
   map (e.g. `MAT_DecalGrimeLeak`). With 1 the decal's smoothness covers its whole box (the light rectangles). My runtime
   copy handles outdoor decals in Play; indoor decals and edit mode still have it.
2. `LookAtmosphereProfiles` SSR: set `minSmoothness` ~0.5 / `smoothnessFadeStart` ~0.62 in the night, storm and dusk
   profiles. Then my runtime "WetReflectionsVolume" (WetnessDriver) can go, or stay as is (it only overrides those two values).
3. Storm haze: constant-colour fog in a dim blue-grey (or fogDepthExtent ~400 m with albedo ~0.75) so the haze beyond
   150 m glows instead of turning black. My curtains only reach 55-110 m.
4. Lightning rate: `AtmosphereController.lightningRate` 0.5 gives 13-32 s between main strikes in the storm. About 1.0-1.2
   (5-16 s) makes the lit strike itself visible in a 10 s look. The cloud flicker covers the gap meanwhile.
5. Check file, missed item: the storm has a clear-sky moon (crisp crate and rail shadows in yard/coast). Softer, dimmer moon
   shadows in `NightStormBlackout` would match the heavy rain.
6. `Tools/lookdev/ucli.sh` hard-codes `UNITY_PROJECT_PATH=$HOME/CretaceaBlackoutUnity`, so `nightlab.sh`,
   `capture_set.sh` and `perf_here.sh` run against the **owner's** editor even from a clone. I used clone-safe copies in my
   own `Tools/agent/` (not committed).

## Performance
Storm plaza view, Play mode, 1280x720 hidden editor, 8 samples each, two runs: weather ON GPU 4.6 / 5.5 ms, rain curtains
and wet-SSR volume OFF GPU 4.8 / 4.4 ms (CPU 21-24 ms both ways, batch-editor overhead). The new pieces cost at most
~0.5 ms, inside the noise. Rain particles: max 26 000 (was 22 000), no shadow-casting lights, no per-frame allocations.
`Tools/lookdev/perf_here.sh` itself targets the owner's project (request 6), so I measured with a clone copy.

## Dock frame got darker (open question for the lead)
`13_ch3_loading_dock` measured darker after (median luma 24.5 → 20, near-black 1.8 → 4.3 %, 3 recaptures consistent). The
dock floor and the truck are under the canopy, so WetnessDriver does not touch them. I tested and excluded two causes:
the SSR override (now off under roofs, no change) and the old rain/mote settings (restored on runtime instances, no change).
Before vs after, the jungle beyond the dock is less moonlit, which suggests the moving cloud shadows over the moon at
capture time. The lighter "before" truck also had the white specks over it. Exposure at the dock is the lead's major
(review 1, blocker → major); please re-check it on the merged build.

## Verified / not verified
- Verified in captures I looked at: the after images in `Screenshots/handin_r2/` (contact sheets); the SSR threshold on
  the camera stack (outdoors 0.5, under a roof 0.9).
- Route test `Tools/lookdev/route_test.py --project <clone>`: **38/38 passed** (275 s), frames in `Screenshots/route_r2/`.
  Run before the last two small changes (puddle fill curve, SSR outdoors-only); after those: Play-mode chapter smoke test
  (Night, Corridor, ServicePaths, Substation, OldYard, Escape, DawnCoast) with no new console errors or warnings. Flicker
  count rises ~1 per 5-6 s in the storm, 0 in NightCalm and at dawn.
- Final scene build with NavMesh: both NavMeshes baked, no build warnings. Compile: 0 errors, 0 warnings. The one console
  error in the session log (`CretaceaPbrImporter.cs:247`, "RGBA 64 bit ... not valid") came at the first build on the
  base commit, before any change of mine.
- Not verified: a player build (Resources material keeps the curtain shader; the SSR override is runtime only); the lead's
  main strike at its peak (my capture missed the flash timing; the cloud flicker is verified); 60 fps at 1080p
  (I measured at 720p in the hidden editor).

## Known rough edges
- The rain curtains are camera-centred rings, so their streak pattern drifts slightly when you walk (it reads as distant
  rain, but a sharp eye can see it does not parallax).
- Glossy wet slabs in the yard and substation are close to mirrors under the lamps; that is the Jurassic Park storm look,
  but the lead may want `slabSmoothnessMax` 0.9 → 0.85.
- Puddle shapes come from a 2x2 atlas (4 shapes, random yaw and size); at 49 per plaza a repeat could be spotted.

---

## Fix round (after the art director's review of the first hand-in)

New capture set: `Screenshots/handin_r2/` (16 files + INDEX.md; the first set moved to `Screenshots/handin_r2_first/`).
"Base" captures were taken in this session on 8ed04fa (weather files reverted, recompiled, scenes rebuilt) with the same
cameras, so every pair is a true same-view pair. Measurements: `Tools/agent/stats.py` (luma mean/median/sd/p99.5, local
7x7-median residual), `Tools/agent/seam.py` (column steps). Helpers stay in `Tools/agent/` (not committed).

### High: dawn ending, the whole Old Yard rendered as a mirror: fixed
- Reproduced: real ending (E-0, `coast_ending`, frame 9 s later): WetnessDriver wetness 0.81 while the target was 0.50,
  on f3ed67e **and** on the base (`01`, `02`).
- `WetnessDriver.Update`: the drying rate is now `max(rate, |target - prevTarget| / dt)`, so the driver never lags a
  scripted fade. `Apply`: the water film (slab gloss, decal gloss, wet-SSR weight) is scaled by a rain weight
  `Lerp(0.35, 1, saturate(rain / 0.4))` (dawn 0.35, NightCalm 0.45 and storm 1.0 keep the full film); authored-wet asphalt
  takes its colour from the soak and its gloss from the film. `TerrainWetnessDriver`: gloss = wet² x Lerp(0.3, 1, rain).
- After: wetness 0.50 at the crane shot, damp dark yard, no mirror (`03`); route run `Screenshots/route_fx/28_ending_1..3`:
  no grass mirror, no yard mirror, sea unchanged.

### High: dock checkpoint darker on this branch: not caused by this branch (evidence)
Bisected at the Dock checkpoint as asked (3 loads each, same time since load, (10.4, 0.06, -11) yaw 90, torch on):
| Build | medians | % < 16/255 |
|---|---|---|
| f3ed67e as handed in | 17.2 / 14.4 / 19.9 | 46 / 56 / 38 |
| f3ed67e, WetnessDriver + wet-SSR volume + TerrainWetnessDriver + curtains + flicker all off at runtime | 19.9 / 14.4 / 19.9 | 38 / 56 / 38 |
| **8ed04fa base** (all weather files reverted, rebuilt) | 14.5 / 19.3 / 18.3 | 56 / 39 / 43 |
| fix round | 14.6 / 19.7 / 15.0 | 55 / 39 / 54 |
The dock floor, truck and canopy are not cloned (they are under the roof map; checked with a raycast + renderer listing).
The base gives the same spread of 14.5-19.3, so the first hand-in's single "before" frame (median 24.5) was one bright
load, not the branch. The spread itself repeats in a 2-load cycle on both builds (timing of the lead's cloud shadows /
exposure at load), so the dock exposure stays the lead's review-1 major. I did not revert the dust-mote soft cap: with every
runtime weather piece off the numbers did not move, and the motes are a few dozen 4-9 mm dots.

### Medium: rain vanished from lamp light and was dimmed in lightning: fixed
- `CretaceaRainParticle.shader`: the lightning term has its own `_FlashLight` weight (1.0) instead of `_SkyLight` (0.3);
  the coverage thinning now applies only to the sky/moon share (`alpha *= lerp(cov, 1, lampShare)`), so a streak in a lamp
  cone, the beam or a strike glints as a whole line; a daylight cap floor `_ExposedCap` (0.3 x scene white, streak
  material only) so dusk rain is not capped at night nits. `LookWeather.ConfigureStreakMaterial`: `_CoverageDistance`
  3.5 -> 8, `_MaxNits` 22 -> 35. Dust motes keep no sky/lightning (`_FlashLight` 0 in `DustMotesController`).
- Measured, storm plaza: in the real lamp cone (650-900 x 110-260; the hero lamp `Light_PlazaLamp_6` projects to
  ~(725, 100) and is a 120 degree downlight) pixels > +12 over the local median: base 1.35 %, f3ed67e 1.31 %, after 1.77 %;
  dark sky (1000-1280 x 60-250) > +6: base 0.47 %, f3ed67e 0 %, after 0.04 % (rain stays invisible on the black sky).
  The critic's region (690-1000 x 140-200) lies mostly above the lamp head, outside its cone; there it is 0.08 %, so the
  target ">= 0.5 % there" is not met and should not be: the rain there is lit only by the sky. Visual: `07`, `14`;
  the triggered strike frame (`Screenshots/fx_final/strike_2.png`) shows the whole rain sheet lit by the flash.

### Medium: rain-curtain seam due north: fixed
- `EnsureRainCurtains`: the arc coordinate is scaled so each ring holds a whole number of streak cells; the period goes in
  uv2.y. `CretaceaRainCurtain.shader`: both noises use a lattice wrapped by its period (streaks P and 2P, gusts
  max(4, round(P x 0.0075)) bands), the time scroll and the y lattice wrap at 1024 (and 2048 for the second octave), so
  `_Time.y` no longer wears the hash down.
- Measured: f3ed67e minus base, largest column step at x = 571 (3.36 / 2.81 in rows 110-150 / 150-195); after: 1.07 (at
  x 593) / 1.49. Direct test with north at the screen centre (yaw 0, curtains on minus curtains off, two frames): steps at
  x 639-641 are 0.01-0.78 against a median of 0.18-0.36; the largest steps (x 567, 707) sit on object silhouettes in both frames.

### Medium: plaza still read dry in the named NightCalm view: improved
- `LookWeather.PlacePuddles`: plaza paving accepts noise field >= 0.5 (0.6 elsewhere) and a hero cluster of 6 puddles is
  seeded in x -50..-34, z -86..-70, clear of the pole and bollards by 0.6 m (202 spots accepted, 77 puddles placed over 6 map cells).
- `WetnessDriver`: slab film = SmoothStep((w - 0.1) / 0.6) x rain weight (NightCalm 0.75 now reaches the full film); a
  diagnostic run with the paving at 0.97 showed that the windows and far lamps only mirror sharply above ~0.85, so a soaked
  slab now spans 0.85-0.94 (bottom cap `slabSmoothnessMinCap` 0.85, top 0.94; was 0.76-0.9).
- After (`05` vs `04`): lobby windows, lamp posts and bollards throw reflections in the far half; left band (0-280 x 405-470)
  p99.5 55 -> 81 (f3ed67e -> after), sd 5.9 -> 10.0; centre band p99.5 138 -> 153. The foreground stays matte: it mirrors
  the dark night sky, which is physically right; the hero lamp's own mirror point is at the bottom edge behind the hand
  (5 m ahead, 2.6 m above the eye), so "every lamp throws a streak" holds for the lamps in the frame, not the hero lamp.

### Medium: storm haze too faint, no natural flicker or strike captured: fixed
- `CurtainRadii` {55, 110, 180}; per-ring opacity near 0.18 / mid 0.35 / far 0.5 (`_RingOpacity`, uv2.x), `_Scatter` 0.06 -> 0.09.
- Far-hill crop (300-600 x 250-330): mean base 34.0, f3ed67e 40.0, after 48.3; fine detail (> +6 residual) 11.1 % -> 10.6 % -> 6.5 %.
- Natural 20-frame sequence in the storm plaza, no ForceFlicker (`Screenshots/fx_final/stormseq_01..20`): the capture
  round trip is ~1.3 s, so the frames are 1.3 s apart (26 s in all, not 0.5 s); 5 cloud flickers and 1-2 main strikes in
  that time; `09` is a natural main strike with the bolt, `08` a natural flicker. Not done: the same sequence at route
  frame 22. Requests 3-4 (fog, strike rate) stay with the lead.

### Medium: glitter fix not proven, plants frosted: proven / partly fixed
- `10`/`11` (coast) and `12`/`13` (valley) are now same-camera pairs shot on 8ed04fa and on this branch (the critic
  capture is gone from the set). Glitter on the coast asphalt and the valley gravel is gone in the after frames.
- Foliage: alpha-clipped plant materials (and fern/palm/vine/tree/impostor names) now get a foliage profile in
  `WetnessDriver`: albedo x0.85 linear, smoothness never raised (capped at 0.45 while wet); before, the flat ferns were
  classed as floors and rose to 0.7 (the frosted groundcover). The pale groundcover right of the path in `13`/`14` is
  terrain-detail grass, which no weather code touches: request 7 below.
- `TerrainWetnessDriver`: gloss x Lerp(0.3, 1, rain) (dawn verge stays matte).

### Medium: wet-SSR runtime volume: thresholds raised, indoor gate dropped, cost measured at 1080p
- `wetSsrMinSmoothness` 0.5 -> 0.70, fade start 0.62 -> 0.76: wet slabs (0.85-0.94) and puddles (0.97) qualify, foliage
  (<= 0.62), rock/bark (0.72 fades in at 0.70-0.76 only weakly) and dry props do not. Authored-wet asphalt keeps its bottom
  >= 0.72 when soaked. The `CameraIndoors` gate is removed (the plaza seen from the lobby keeps its wet reflections).
- Perf, storm yard (403.2, 1.1, -22.8), torch on, game view 1920x1080, 32 samples each, GPU median: volume on 6.7 / 6.3 ms,
  off 7.4 / 7.4 ms, volume and curtains off 9.0 ms (one first "on" run read 13.0 ms right after the load: warm-up). The
  difference is inside the noise; CPU 14-25 ms is batch-editor overhead. Delete the volume once request 2 lands.

### Medium: merged concrete/granite/tile meshes got floor gloss on their walls: fixed
- `WetnessDriver.Build`: in a merged renderer only ground-only names (paving, plaza, asphalt, gravel, mud, road) count as
  floor; the log now lists the wetted merged meshes. This run: `Z1_Merged_MAT_ConcreteBoardform_0` and
  `Z1_Merged_MAT_Z1_KerbConcrete_0` are now wall clones (no floor gloss), `PlazaStrip` and `MudGround` stay floors;
  DarkGranitePlinth, LabTileWhite and CeilingTiles are under the roof map and not cloned. Glow sign materials are no longer
  cloned at all. Request 8 below (separate merge groups).

### Medium: slab darkening in sRGB: fixed
- All darkening values are now linear albedo multipliers converted to sRGB (`k^(1/2.2)`): slab 0.6, drain 0.68, floor 0.8,
  wall 0.84, foliage 0.85. Plaza paving (porosity 0.39) now loses ~30 % linear (was 55 %). Plaza foreground mean
  61 -> 72 (base 82), substation pad visibly lighter with the same reflections (`Screenshots/fx_a2/19_sub.png`).

### Low: wet gravel lost its texture: improved
- `TerrainWetnessDriver` gravel normal 0.60 -> 0.85, albedo 0.70 -> 0.80; `WetnessDriver.drainWetNormal` 0.65 -> 0.85.
  Route frame 22 gravel (560-760 x 400-500): sd 11.4 -> 12.0, p99.5 83 -> 93. The torch does not hit that region at pitch 0,
  so this is moon-lit gravel; the gravel is dark but textured (`14`). Not a broad torch highlight yet.

### DuskTransition and DawnCoast checks
- Dusk (`15`): paving darker (foreground mean 65 -> 57), a faint sheen only at the far paving, no puddles (wetness 0.35 is
  below the 0.35 puddle start). Rain streaks are barely visible even with the daylight cap floor: honest result, "light rain
  starting", no sheen claim.
- Dawn: the real ending (not SetPreset) is now checked in `03` and `route_fx/28_ending_*`: no rain, no curtains, no mirror;
  puddles at ~0.3 fade.

### Fix round: files touched
`WetnessDriver.cs`, `TerrainWetnessDriver.cs`, `StormRainOcclusionController.cs`, `DustMotesController.cs`,
`LookWeather.cs`, `CretaceaRainParticle.shader`, `CretaceaRainCurtain.shader`, regenerated `MAT_RainStreaks.mat`,
`WxRainCurtain.mat`, `MAT_WxSparks.mat` (new `_FlashLight` / `_ExposedCap` defaults). Public signatures unchanged.

### Fix round: requests for the lead (in addition to 1-6 above)
7. LookMaterials owner: grass / PlantCards `_SmoothnessRemapMax` 0.62 -> 0.32 and specular occlusion on for the terrain
   detail grass cards (the frosted groundcover in `13`, `14`; WetnessDriver does not touch terrain details).
8. Zone 1 owner: keep walkable slabs and wall boxes in separate merge groups in `ConsolidateStaticPrimitives`.
9. Dock exposure (review-1 major) is unchanged by this branch: see the bisect table.

### Fix round: verified / not verified
- Verified in captures I looked at: `03`, `05`, `07`, `08`, `09`, `11`, `13`, `14`, `15` and their befores; route frames
  `28_ending_1..3`; sub and yard (`fx_a2`) not regressed.
- Route test (`route_test.py --project <clone> --out Screenshots/route_fx`) on the final build with NavMesh: **38/38** in
  274 s, no new console errors; chapter jumps Night, Substation, OldYard, Escape, DawnCoast after it: no errors.
  Compile 0 errors / 0 warnings; build warnings none; both NavMeshes baked.
- Not verified: the 20-frame sequence at route frame 22; true 0.5 s frame spacing (capture latency 1.3 s); a player build;
  60 fps in a real 1080p window (the 1080p numbers are the hidden editor's custom render resolution).
