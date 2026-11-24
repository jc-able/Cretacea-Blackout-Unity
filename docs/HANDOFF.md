# Cretacea: Blackout (Unity) — handoff

Unity 6000.6.9f1, HDRP 17.6.0, Apple Silicon/Metal. Port of the Godot game at `~/CretaceaBlackout`
(repo jc-able/CretaceaBlackout, devlog in `docs/devlog/` there). First Unity build: Gemini 4 Neon swarm in
Antigravity, Nov 19 2026 21:28 to Nov 20 00:21 (notes: `~/.gemini/antigravity/brain/c65ff93a-…/scratch/`).
Since Nov 20 00:42: Claude (Fable 5.2) look pass, goal from JC: "the colors are off … make this game look
amazing … perfect … don't stop until it's completely done".

## How to drive the open editor (never close it; JC records his screen; VARCO Sound tab must stay docked)
```bash
source ~/CretaceaBlackoutUnity/Tools/lookdev/ucli.sh      # PATH + UNITY_PROJECT_PATH + helpers ev / evf / gv
unity status                                               # port 7800, state ready
unity command <group> <name> --arg value                   # groups: cretacea, capture, editor, script, scene, …
unity command editor.playmode start | stop                 # Play mode (run-in-background on)
unity command cretacea jump_chapter --chapter Intro|Night|Corridor|ServicePaths|Substation|OldYard|Escape|DawnCoast
gv Screenshots/x.png 1 1920 1080                           # Game view capture incl. HUD (Play mode)
unity command cretacea capture --output_path Screenshots/x.png --pos x,y,z --look_at x,y,z --fov 60   # free camera
evf file.cs                                                # run a C# snippet in the editor (Roslyn script)
```
- `--project-path` must come before any command argument (or use the env var, as ucli.sh does).
- Unity 6 `Light.intensity` is always in native units (candela for point/spot, lux for directional);
  `lightUnit` is only the inspector's display unit.
- Scenes are GENERATED: `unity command cretacea build_all_scenes` rebuilds MainWorld.unity + MainMenu.unity from
  `Assets/Editor/WorldBuilder/*`. Hand edits to the scenes are lost on rebuild.
- Git: local repo, checkpoint `8a8a371` = the project exactly as handed over. No remote.

## Baseline (before) captures
`Screenshots/baseline/` (git-ignored): `play_<Chapter>_5s.png` (Play mode, 1920x1080) and `aerial_*.png`.

## Diagnosis (Nov 20, 01:00)
1. Night presets used FIXED exposure EV 0.6–1.2 (moonlight) with lamps of hundreds to 85,000 cd: lit = clipped
   pure red/white, unlit = black. Day preset: 4200 K sun at 16° + dense warm fog = sepia monochrome.
2. Terrain 1024 m, 4 layers, 59 % rock / 34 % grass / 6.5 % mud, no terrain trees or detail layers; ~3,600
   separate vegetation objects, sparse. Reads as a desert basin.
3. Prop materials come from the FBX with base colour + normal only (no mask map); 62 renderers on the grey default.
4. Flashlight: 25° cone, centre points down the corridor, no spill, no bounce.

## State at Nov 20, 01:50 (look pass in progress)
- Commits: `f0eaedb` foundation (atmosphere, exposure, lamps, flashlight, materials), `941668e` look bible + briefs.
- Read `docs/LOOK_BIBLE.md` first: acceptance criteria, measured photometry, the lamp table, materials, commands.
- **Wave 1 (running):** five agents, each in its own clone `~/CretaceaUnityClones/{terrain,kit,zone1,zone23,ui}` with a
  hidden batch-mode editor (`Tools/lookdev/clone_editor.sh start|stop <clone>`), branch `look/<name>`, briefs in
  `docs/briefs/`. Pipeline per package: build → critic → fix. Workflow run `wf_391f9f45-b94`. Hand-ins land in
  `<clone>/docs/handin/<name>.md` + `<clone>/Screenshots/handin/`. Merge: `git fetch <clone> look/<name>` into the
  main project, then `unity command cretacea build_all_scenes`.
- Lead (main editor) meanwhile: story fixes, camera/viewmodel, lightning shadows, rain/wetness, menu scene.
- Traps found:
  - Changing an HDRP asset from code breaks the renderer until `EditorUtility.RequestScriptReload()`.
  - Material `Color` values are sRGB (tint 1.5 = ×2.4 linear).
  - `CretaceaPbrImporter` forces import settings by path: the flashlight cookie was read as sRGB (spill 11 % → 1 %).
  - Ceiling point lights blast the ceiling and fool auto exposure: use wide downward spots.
  - Batch-mode game view is tiny unless `PlayModeWindow.SetCustomRenderingResolution(1280, 720, …)` is called.
  - Play-mode tests write the save file: sandbox with SessionState "Cretacea.SaveDir" (`sandbox_saves` in ucli.sh).
  - `build_all_scenes` takes ~4 s and discards the open scene; the Look Pass runs inside it.

## State at Nov 20, 02:10
- Commit `b234ad4`: story lighting fixes (no lamps in the day intro, lamps off at dawn, flashlight soft-lock), weather
  globals (`_AtmoRainTarget`, `_AtmoWetnessTarget`, `_AtmoWeatherDriven`), lightning takes the cascaded shadows during a
  strike, camera dithering/NaN stop/TAA high, FOV 68, arm pivot (0.23, -0.225, 0.49), brightness hook
  (`AtmosphereController.UserBrightnessEV`, profile asset `Look_UserBrightness`).
- **Wave 2 (running, workflow `wf_e5083eb3-d01`):** clones `creatures`, `vfx` (base `b234ad4`), briefs
  `docs/briefs/creatures.md`, `vfx.md`.
- Test trap: a hunting dinosaur kills the test player and the red death screen darkens Game-view captures.
  Call `Cretacea.AI.DinoSpawner.Instance.DespawnAll()` after a jump, or use free-camera captures.
- After a `unity recompile`, wait (`unity status --until-ready`) before the next command, or it fails with
  "No Pipeline instance found" and a following jump runs in EDIT mode.
- Measured: moon/terrain photometry is correct (grey card under a 100 lux key light at EV 4 = 0.235 linear).
- Performance before the vegetation rebuild: substation view 5.9 M triangles, GPU 20 ms; corridor 12 M triangles.

## State at Nov 20, 05:15 — all seven packages merged
- Both workflows finished (wave 1 `wf_391f9f45-b94`: terrain, kit, zone1, zone23, ui; wave 2 `wf_e5083eb3-d01`:
  creatures, vfx). Every branch `look/<name>` is merged into `master`; hand-in notes are in `docs/handin/`.
  The clones in `~/CretaceaUnityClones/` are no longer needed (their editors are stopped).
- **A fresh checkout needs one `unity command cretacea build_all_scenes`**: the hill jungle instances, shore foam
  mask, instanced LOD meshes and impostor textures are generated and git-ignored.
- Lead changes after the foundation (all in `git log`):
  - **Tone curve:** ACES crushed everything under ~2 % to black (measured: night ground 0.005 -> 0.0015). Every preset
    now uses HDRP's Custom curve with per-preset toe values, so presets cross-fade without a switch in the curve.
  - **Night:** ambient x1.5 outdoors, sky-tinted fog x3, moon low ahead of the route (azimuth 115, elevation 29-31) so
    wet ground glints back and silhouettes read; lightning 190 lux.
  - **Indoor ambient:** `Look_Indoor.asset` (indirect diffuse 0.42, reflections 0.30) is faded in by
    `AtmosphereController.UpdateIndoor()` while the camera is enclosed (9 rays). No baked lighting anywhere.
  - **Bounce:** `Cretacea.Rendering.LampBounce` + `LookLights.EnsureBounce`: a wide shadowless spot under the floor
    below each room's main lamp (46 lights). Zone 1 adds small `_Wash` lights of its own.
  - **Flashlight:** 16-bit cookie (import format RGBA64, never R16: a single channel tints the beam red), one smooth
    bell, hot spot capped at 95 lux on the surface it hits (`HotSpotMaxLux`), stronger bounce.
  - **Camera cuts** reset exposure/TAA history (`CameraDirector.OnBeginContextRendering`); the day-night dip scales the
    sun's intensity so the sky dims with it; reflection probes re-render when the atmosphere changes
    (`_CretaceaAtmosphereEpoch`, `_CretaceaAtmosphereSettled`).
  - **Performance** (editor Game view 2313x1486, quiet machine): 107-146 fps at eight test spots, 0.9-2.8 M triangles.
    Three things got it there: GPU Resident Drawer + GPU occlusion culling (HDRP assets), volumetric fog buffer at
    16.67 % / 64 slices (the project default was 50 % / 96), and `LampShadowBudget` (live shadow pass only on the five
    lamps nearest the viewer; with all 67 on, the terrain was redrawn 67 times per frame).
- Tools in `Tools/lookdev/`: `route_test.py` (38 checks over the whole story + 29 screenshots), `capture_set.sh`
  (rooms, presets, UI, intro), `nightlab.sh`, `presetshots.sh`, `perf.sh`, `perf_here.sh`,
  `make_flashlight_cookie.py`, `clone_editor.sh`.
- Verification at this point: recompile 0 errors / 0 warnings; `build_all_scenes` ok; `verify_playmode_doors` 18/18
  doors, 5/5 documents, both NavMeshes valid; `route_test.py` 38/38; standalone macOS build succeeded
  (`Builds/CretaceaBlackout.app`, 2.35 GB, 0 errors) and its menu scene runs clean.
- Built player self-test: `CretaceaBlackout.app/Contents/MacOS/CretaceaBlackoutUnity -cretaceaSmoke <folder>`
  (optionally `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`) visits every chapter start, writes a
  screenshot per stop and `report.txt`, then quits (`Assets/Scripts/Dev/SmokeTestRunner.cs`).

## State at Nov 20, 09:00 — round 2 merged, final build made
- Critic review 1 (twelve Opus reviewers, 79 captures, `docs/review1/`): scores 3–6 of 10; 7 blockers, 46 majors,
  38 minors reported; second opinions kept 90, dropped 20, added 17. Round 2 (workflow `wf_98d4d988-c61`): five fix
  packages on `fix/{weather,island,zone1,zone23,ui}`, each built → reviewed → fixed, all merged (`docs/handin/r2_*.md`,
  captures in `Screenshots/handin_r2/`). Review 2 capture set: `Screenshots/review2/` (106 frames), findings in
  `docs/review2/` once written.
- Lead fixes after review 1 (see `git log`): long highlight shoulder (2.4 stops), neutral day white balance, blue-grey
  night with black lifted to ~7/255, constant fog colour at dusk/night/storm, soft moon shadows, dawn sun on the horizon
  with a valley mist, sunset/moonrise/sunrise transitions that follow the sun, 12 km far clip, clear glass, clean
  plaster, 60 cm ceiling tiles, blended chain-link, wall-fill lights, T. rex reveal backlit by directed strikes,
  camera cuts reset exposure, intro window shot kept clear of jungle.
- **Measurements (lead, in JC's editor unless noted; game view 2313x1486):**
  | what | before | after |
  |---|---|---|
  | Substation, triangles per frame (GPU Resident Drawer + GPU occlusion) | 5.75 M | 1.65 M |
  | Substation, GPU frame time, volumetric fog buffer 50 %/96 slices → 16.67 %/64 | 36.3 ms | 13.4 ms |
  | Lit security office (chapter 1), all lamps drawing live shadows → 5 nearest | 35 fps (27 ms CPU) | 107 fps |
  | Same office after round 2 added lamps (121 shadow lamps) → budget of 18 within 36 m | 55 fps | 64–68 fps |
  | Eight test spots, final build state (`Tools/lookdev/perf.sh`) | 35–60 fps in chapter 1 | 64–127 fps, 1.4–6.3 M tris |
  | Dawn ending frame, pixels with a clipped channel (`frame_stats.py`, free camera at the overlook) | 21.8 % | 1.0 % |
  | Near-black pixels (<8/255): dusk valley / corridor without flashlight / loading dock | 25.5 / 54.7 / 48.0 % | 0.1 / 2.6 / 4.1 % |
  | Day plaza, red/blue balance of mid-tones / of shade | 1.19 / 1.19 | 1.03 / 0.85 |
  | Storm meadow, red/blue balance of dark tones (teal → blue-grey) | 0.16 | 0.84 |
- Verification on the final state (commit `a1bc547` + later): recompile 0 errors / 0 warnings; `build_all_scenes` ok;
  `verify_playmode_doors` 18/18 doors, 5/5 documents, both NavMeshes; `route_test.py` 45/45 (38 story checks + 7
  on-the-ground checks) with 0 new console errors; standalone build `Builds/CretaceaBlackout.app` 2.43 GB, 0 errors,
  0 warnings; its self-test (`-cretaceaSmoke`, 1920x1080 window): PASS, 0 errors, menu 115 fps, chapters 73–104 fps,
  MainWorld loads in 2.8 s.
- Clones: every `look/*` and `fix/*` branch is merged into `master` (checked with `git merge-base --is-ancestor`).

## State at Nov 20, 12:25 — round 3 merged, review 3 done, checkpoint build made
- **Review 2** (twelve Opus reviewers, `Screenshots/review2/`, `docs/review2/<segment>.md` + `<segment>_check.md`):
  first / second critic UI 7/7, night outdoors 6/5.5 and 5/5.5, intro 5/5, interiors 5/5, daylight 4/4. After the second
  opinions: 4 blockers, 38 majors, 50 minors (15 dropped, 14 added). Blockers: the far jungle read as a burnt forest
  from the air and as a sandstone hill at dusk, the dawn ending was a brown monochrome, a white rectangle of mirror on
  the wet plaza in the opening's night aerial.
- **Round 3** (workflow `wf_199b8b45-f46` + one UI agent; briefs `docs/briefs/r3_*.md`, hand-ins `docs/handin/r3_*.md`):
  five packages on `r3/{island,zone1,zone23,intro,ui}`, each built → reviewed by a critic → fixed, all merged.
  - island: the far trees were dark because the impostor bake graded every texel of the solid tree atlases as bark;
    leaf-green atlases, crown caps for views from above, greener grass and moss, varied valley trees.
  - zone1: even ceilings (upward fills on their own light layer), room dressing, one battery unit per room, sage
    plaster, round planted tree islands, kiosk backs, dressed west wall, roof detail.
  - zone23: service-path walls broken up, a proper lantern bracket, LED flood heads, corrugated containers, wet yard
    bays, coast road with rails instead of fences.
  - intro: the T. rex walks into the gallery window and roars between the mullions, sauropods and trikes in view, a
    crowd of 54, title cards in one lower-left block.
  - ui: end-screen plate and hold, CONTROLS page (pause and settings), tablet spoilers hidden, flashlight chip,
    balanced subtitles, map labels, brightness slider, key caps.
- **Lead fixes in round 3** (all in `git log 739eb36..`; rules in `docs/LOOK_BIBLE.md` section 4):
  - Blackout reads as a blackout: indoor night exposure floor (EV 3.4), flashlight bounce at its real size (0.6 % of
    the beam, 1.5 m off surfaces, 1.2 m under ceilings), beam 12,000 cd with an angle-aware hot-spot budget of 90 lux
    (median of five rays), battery units without bounce at 0.65, the screen-space bounce at real strength indoors
    (the old 0.20 'keep the sky out' value had switched it off: charcoal ceilings), 3x in the blackout.
  - Lightning: 85 / 70 lux plus a glow on the whole cloud deck; its brightness follows the look during a blend
    (`HoldLightningForCapture` for captures).
  - Sun and moon discs drawn a few stops over white inside a soft flare; dawn retuned (sun 8.5°, EV 10.2–12.6, cool
    balance, blue shadows, thin sea haze); the ending fades through black into a settled morning.
  - Outdoor reflection probes hand over to the live sky during atmosphere blends; wet paving 0.80; afterglow fades with
    the sun's depth.
  - Chain-link is cut-out dark wire again (plain mips, cut-off just above the mean alpha: near = wires, far = posts).
  - Storm outdoors: sky fill 1.7, contrast 9, toe 0.02, moon shadows 0.55; dusk +0.15; day haze 3600 m.
  - Ground mist follows the weather (`GroundMistDriver`); rain follows a cutscene camera; puddles on the valley's
    terrain road; ground sampling finds the right terrain tile (`WorldRegistry.SampleGroundHeight`, test tools).
- **Things that went wrong and were undone** (so nobody repeats them): a probes-only `rayMiss` override in
  `Look_Indoor` blackened outdoor shadows seen from inside (an enum switches at any weight); a 'performance regression'
  that was a script reload in the middle of a Play-mode session (never edit scripts on disk while Play mode runs in
  the owner's editor: a click into the editor reloads them); `Terrain.activeTerrain` is an arbitrary tile.
- **Where this state lives:** branch `lead/verify` in the clone `~/CretaceaUnityClones/vfx` (hidden batch editor,
  port 7803), not yet in the owner's project. At 10:32 a trial `build run` in the owner's editor raised Unity's modal
  "save modified scenes?" dialog (the open scene was dirty after a Play session) and the editor has answered no command
  since; computer-use access to the Unity window was declined, so only the owner can dismiss it. Everything after
  commit `3905f04` was built and verified on the clone. `Tools/lookdev/lead/sync_main.sh` brings the owner's project to
  `lead/verify` once his editor answers again.
- **Review 3** (twelve Opus reviewers, 113 captures in `Screenshots/review3/`, findings `docs/review3/<segment>.md`,
  second opinions `<segment>_check.md`): first / second critic UI 8/8, daylight 6.5/6.5, intro 7/6.5, night outdoors
  6/6 and 7/6.5, interiors 6/6. **0 blockers, 23 majors, 61 minors** (13 dropped, 15 added by the second opinions).
  Review 2's four blockers are confirmed fixed (green far jungle, olive-gold dusk hill, a real sunrise, no mirror
  rectangle).
- **Quick fixes after review 3** (commit `3fb4102`): intro actors cleared before the closing aerial, T. rex mark moved
  so its head is clear of the mullion, barred and braced E-0 gate leaves, locker duffels and boots, a dark pool behind
  the end-screen wordmark, dock battery lamp 1000 cd, cloud sun dimmers at dusk and dawn, umbrella crown caps on the
  tree impostors, no floor decals on the flashlight arm, storm preset captures from the Records checkpoint.
- **Checkpoint build** (clone, commit `3fb4102`): `build run` Succeeded, 2,579,974,224 bytes, 0 warnings, 0 errors.
  Self-test at 1920x1080, two runs, both PASS with 0 errors: menu 120 fps, MainWorld up in 2.1-2.5 s, security office
  73-74 fps, records 77-83, dock 78-115, valley 117, substation 117, yard 115-118. Copied to
  `~/CretaceaBlackoutUnity/Builds/CretaceaBlackout_R3.app`. The first launch of a new build compiles its shaders
  (16.6 s in the menu, 7.8 s entering the world, measured once); later launches are clean.
- Verification of that state on the clone: recompile 0 errors / 0 warnings; doors, 5/5 documents, both NavMeshes valid;
  `route_test.py` 45/45 with no new console errors.
- **More traps:**
  - `Tools/lookdev/ucli.sh` falls back to the owner's project when `UNITY_PROJECT_PATH` is not exported first.
  - BuildPlayer in a GUI editor asks to save a dirty open scene in a modal dialog: run `build_all_scenes` right before
    `build run` and never after a Play session, or build in a batch-mode clone (no dialog there).
  - The built player's self-test writes the real save file unless `CRETACEA_SAVE_DIR` is set (the build script sets
    it). The game binary has no `-version`: any launch opens the game.
  - Free-camera storm frames showed hard vertical seams across the sky: the rain-curtain cylinders around the player,
    seen from outside. `StormRainOcclusionController.CaptureAnchor` (set by `capture_set.sh`) moves rain, curtains and
    the roof map to the capture camera.
  - Walls and dressing are merged per material at build time, so their colliders have no renderer: anything that
    needs "what did this ray hit look like" asks `SurfaceAlbedoMap` (baked by the Look Pass, step `albedo`).

## State at Nov 20, 18:10 — round 4, round 5, final build
- **Round 4** (workflow script `Tools/lookdev/lead/round4_workflow.js`; briefs `docs/briefs/r4_*.md`, hand-ins
  `docs/handin/r4_*.md`): six packages on `r4/{island,valley,rooms,lights,zone23,polish}` in the clones `terrain`,
  `creatures`, `zone1`, `kit`, `zone23`, `ui`, each built → reviewed by a critic → fixed. A tool restart at 12:48
  stopped the first six agents 18 minutes in; they were relaunched with a take-over note and continued from the
  files left in the clones.
  - island (`r4/island`): impostor caps as a ragged 16-point rim sunk into the leaf cards (the straight ledges are
    gone, a fine mosaic remains at dusk), a shadows-only crown at the play-area impostor LOD, the open-valley jungle
    tree graded greener with darker trunks, tall grass in three looks mixed by noise with a broken front, a varied
    belt of species and heights round the Old Yard for the ending crane shot, no trees in the pond, basalt-grey
    boulders. Merged with two corrections by the lead (pond trees removed only after the whole scatter, so the
    forest no longer re-rolls; the branch's regenerated lens and beacon materials not taken).
  - valley (`r4/valley`): the T. rex reveal is staged in the open gap down Service Road 3, 46 m out, on the strikes,
    with a moon-side rim light on its own light layer and torch eyeshine (`TrexMoonRim`); an aviation obstruction
    mast with two red lamps, amber battery flashers at the stream bridge, retro-reflective delineators and chevrons
    that light up in the torch (`TorchReflectorGroup`, one component for all), puddles and two glossy rut strips
    along the road (`LookWeather.PlaceValleyRoadWater`).
  - rooms (`r4/rooms`): lockers, corridor, lobby north half and feed prep dressed; towel, fiche reader, stock boxes,
    fridge, keyboard, phone, logbook, water cooler and whiteboard rebuilt as recognisable objects; CCTV feeds on the
    control-room monitors; the taken flashlight disappears from the desk.
  - lights (`r4/lights`): lobby key 4300 K with a cool moon fill through the roof (frame R/G 1.26 → 1.15); corridor
    emergency heads 64° straight down with 12-nit lenses (red-dominant pixels 23 % → 3 %); battery units with a
    grey housing, dark band, charge LED and heads aimed down; loading dock re-lit and re-surfaced (pixels under
    16/255 with the torch on 54 % → 31 %); found the lamp-table colour bug.
  - zone23 (`r4/zone23`): service-path walls with a pump-room door, PADDOCK 4 arrow, ferns over the coping and runoff
    streaks; E-0 gate with signals and motor housings that reads closed from the yard; warehouse roof and perimeter
    wall detail for the crane shot; a knife-switch transfer lever that visibly throws; bollard collar and the
    knocked-down bollard fixed.
  - polish (`r4/polish`): controls page legend sizes and lighter bands, HUD chip, objective hint padding, document
    paragraphs, tablet labels and wording, park badge and worn hardware on the menu cards, ending captions in the safe
    area, three separate trikes on the rail shot, the rail brachiosaurus hidden until its shot, the first night shot
    carries the lightning and its caption, the first gameplay view looks down at the desk, the closing glide shows
    the storm front. After the merge the lead moved the rail brachiosaurus off the visitor promenade (critic) and
    gave the intro flash a fixed direction.
  - Reviews of the packages: only the island and polish critics finished (`docs/handin/r4_island_critic.md`,
    `r4_polish_critic.md`); the other four critic runs and both fix runs failed on a usage limit at 13:54. The lead
    reviewed the other four packages' captures before merging.
- Verification after the merge (clone, commit `de6e8d9`): recompile 0 errors / 0 warnings; doors 18/18, 5/5
  documents, both NavMeshes valid; `route_test.py` 45/45 with no new console errors (16:19).
- **Lead fixes during round 4** (`git log 3987803..lead/verify`, lead-owned files only):
  - Flashlight: the hot-spot budget follows the albedo of what the beam lands on (`SurfaceAlbedoMap`, baked by the
    Look Pass for 5,975 colliders; 120 lux on brick, about 40 on white tile); the bounce is a 165° cone along the
    surface normal; the cookie's soft field is 45 % weaker straight up; the shaft is 0.22 indoors; the arm has an
    18 cd fill of its own; decals are switched off on every viewmodel renderer. Records room, wall pool / ceiling:
    148 / 161 in review 3, 145 / 75 now. White tile: 1,299 px at 250+ → 206, core mean 230 → 192. Glove 6 → 21/255.
  - Blackout rooms: a 1 lux cool base from four sides and below while the camera is enclosed (`BlackoutBaseLux`),
    indoor bounce 3.0 → 2.2. Darkest tenth of three dark rooms 10–11 → 12–14/255, pixels under 16/255 33 % → 23 %.
  - A lamp-table bug: `Red`, `Amber`, `CoolBlue` were static fields in `LookLights.cs` read by tables in other files
    of the partial class; C# leaves the initialiser order between files open, so a table could read (0,0,0,0).
    They are properties now. (Found by the lights agent: its emergency heads had come out black.)
  - Weather: free-camera captures take the weather with them (`CaptureAnchor`); wet floors are judged by nine probes
    (the substation pad sat dry under a canopy probe); storm walls darker and shinier (0.68 / 0.5); stream and valley
    mist thinner, fading over their whole height; valley mist keeps 75 % at dawn.
  - Atmosphere: afternoon sun 20° at 5500 K (the '5:42 PM' card had white mid-afternoon light); dawn haze 2200 m to
    60 m, bluer zenith, cooler balance, a faint green-white sun filter; flash colour and storm shadow tint less blue.
  - Lightning bolt: a thinner channel that tapers, five to nine forks that thin to a point and fork again.
  - Story: the service-path raptor crosses the junction in a straight line (`ScriptMoveDirect`: with a baked NavMesh
    the agent went round the junction and never crossed in view); the transfer lever throws an arc flash and a spark.
  - Shadows: the two nearest shadowed lamps get twice their shadow map while the player is within 7 m.

- **Model switch.** Claude Fable 5.2 hit its weekly usage limit at 13:54 (four round 4 critic runs and both fix runs
  failed on it). The owner switched the session to Claude Opus 5.5 at 16:03 and added a request: "fix the hand and
  all animations as well. It should look like a professional triple a game and the storyline mechanics and ability
  to complete the game should make sense as well. It should be ultra realistic looking". At 18:05 he asked to wrap
  up within the hour with a complete, playable game, so the planned review 4 (16 critics) was not run.
- **The owner's editor** answered again at about 17:43 (he dismissed the save dialog). `Tools/lookdev/lead/sync_main.sh`
  then brought `~/CretaceaBlackoutUnity` to the verified branch inside the open editor (scenes rebuilt there, doors
  18/18, VARCO Sound windows still open). The stale 10:32 build finished into `Builds/CretaceaBlackout.app` first and
  was replaced by the final build below.
- **Round 5** (briefs `docs/briefs/r5_*.md`, hand-ins `docs/handin/r5_*.md`, workflow `Tools/lookdev/lead/` copy of
  the round 4 script pattern): four packages, each built → critic → fix except the playthrough package (built only;
  stopped for the deadline).
  - viewmodel (`r5/viewmodel`): the first AI's mitten arm is replaced by the Tripo scan of a gloved hand gripping a
    tactical torch from the Godot version (`~/CretaceaBlackout/assets/models/viewmodel/flashlight_hand`), converted
    with headless Blender into glove, torch body, rubber grip, lens and LED submeshes plus a jacket sleeve; the beam
    leaves the lens; the desk pickup is the same model. `ViewmodelMotion.cs`: breathing, a gait bob locked to the
    footsteps, look sway that trails, sprint/crouch carry poses, toggle kick, equip raise, lowering for documents,
    the tablet and held interactions, wall tuck, and a tremor when a predator is near (`PredatorProximityProbe`).
    The beam is stabilised against the hand's jitter. The thumb does not move (the scan is one shell).
  - creatures (`r5/creatures`): scripted moves no longer crab-walk or walk backwards, heavy animals no longer slide
    sideways at NavMesh corners, standing pivots re-plant the feet in steps (no treadmill), the T. rex tail is
    lowered and calmer, the pelvis bob is halved, a footfall dip and head stabiliser give the T. rex weight and switch
    off for roars, roars wait until the animal faces its target. All procedural (no clip rework).
  - playthrough (`r5/playthrough`): a playthrough bot (`Assets/Scripts/Dev/PlaythroughBot.cs`, dev-only input
    override in `CretaceaInput`, runtime player NavMesh, `Tools/lookdev/playthrough.py`) that plays on foot with real
    input and real interactions. It found and the package fixed: the Control Room door left 0.60 m (a player could
    not get in; opened to 72°), one-shot volumes spent by a refused beat (soft-lock risk; re-armed), the Yard
    checkpoint saved before auxiliary power, chapter 1 beats replaying after a Records restart, no objective after
    E-0. Every segment was finished on foot with dinosaurs active, and the late game through the end screen, menu,
    Continue and restart checkpoint; a single run from the first room was not completed because the bot kept dying
    at the Feed Prep dock crawl (it does not back off when hunted; with sneaking it got through with 0 deaths).
  - island2 (`r5/island2`): no star crowns from the air, greener valley band, pond cleared with a ragged shore and
    reeds, varied ending belt with palms and ferns, basalt rock instead of orange, compressed atlases; meadow GPU
    8.3 → 5.5 ms. The dusk-hill impostor mosaic remains (needs an impostor shader).
- Lead after the merges: service-path raptor despawn 4.2 → 6.0 s (it vanished in view after the creatures'
  acceleration change). Merged hand + creatures + island verified at 18:10: doors 18/18, route test 45/45, no new
  console errors.
- **Final verification and build** (clone, commit `e84ece8`, 18:10-18:29; `docs/handin/final_build.txt`): scenes rebuilt
  with NavMesh; doors 18/18, documents 5/5, both NavMeshes valid; `route_test.py` 45/45 with no new console errors;
  player build Succeeded, 2,621,450,016 bytes, 111.5 s, 0 warnings, 0 errors. Self-test at 1920x1080, two runs, both
  PASS with 0 errors: menu 120 fps, MainWorld up in 2.4-2.5 s, chapters 64-111 fps (run 1) and 71-80 fps (run 2),
  measured while another session's After Effects build was running. Copied to
  `~/CretaceaBlackoutUnity/Builds/CretaceaBlackout.app` (the stale 10:32 build moved to `Builds/_superseded/`).
- **Full playthrough from the first room** (18:36-18:45, after the Feed Prep raptor's aggression went 0.85 → 0.5):
  the playthrough bot played Night → Records → Feed Prep and the dock crawl → service path → Tyrant Valley →
  Substation 3 → chase → Yard → E-0 → coast → end screen on foot with real input, dinosaurs active, in 507 s with
  0 deaths, then main menu → Continue → pause → restart checkpoint (`docs/handin/final_playthrough_log.txt`). The
  player was rebuilt with the fix (0 errors, 0 warnings; self-test PASS twice, 0 errors; `docs/handin/final_build.txt`).
- **New Game run from the main menu** (20:36-20:50, after fixing the bot's walking mesh, which a menu start built
  from the menu scene: `PlayerNavMesh.ResetForScene`, rebuilt when the active scene changes): the opening cinematic,
  all of chapter 1 and the Records Annex played on foot (Night 146 s). The bot then died once at the Feed Prep dock
  crawl, and after RETRY CHECKPOINT it stood in the Records Annex in front of `door_records`, closed but not locked,
  until the timeout: its path planner treats closed doors as walls there. A player opens it with E. From the Records
  checkpoint on, the run from the first room finished the game (507 s, 0 deaths). Log:
  `docs/handin/final_playthrough_newgame_log.txt`.
- **Review 4** (19:15 captures of the final state, 16 Opus critics, `docs/review4/<segment>.md`): first / second critic
  UI 8.5/8.5, intro 7.5/7, daylight 7/7, interiors 7/7, night outdoors 7/7 and 7/7, playability 7/7, motion 6/5.5.
  0 blockers, 16 majors, 95 minors (review 3: 0 / 23 / 61, with no motion or playability segment). Fixed after the
  captures: the khaki dawn sea and horizon, the glove lit from above by a light that is not there, the torch's tail
  cap facing the eye, no stealth hint before the first raptor (commit `65114cf`); the T. rex and triceratops turning
  faster than their legs, now 60°/s (`966773a`); the slate-grey day sea, now blue-teal with a teal-green dawn sea
  (`e883cab`: stronger, bluer body scattering in `LookTerrainWater.cs`; GoldenDay near sea saturation 0.22 → 0.36,
  hue 178° → 186°; route test 45/45); the CCTV bank in the first playable frame that looked switched off and the
  records desk beacon lighting the floor through the desk (`6c7b2ac`: the screens use `CretaceaCctvScreen`, which
  ignores the controller's `_EmissiveColor` and reads its own `_EmissionNits`, 8 → 40 in `MAT_CctvScreen_Live.mat`;
  the beacon's range 4.5 → 1.0 m in `LookLights.Zone1.cs`; bank mean 37 → 51/255, under-desk floor 91 → 38/255 and
  R/G 1.47 → 1.01); the clipped glare on the wet ground under the torch (`8714fa5`). That glare was not the torch:
  by elimination (beam specular off, max smoothness, SSR off, probes off, viewmodel hidden, beam off, nearby lights
  off: all unchanged) it was the storm moon's own specular, a 0.9° disc 29° up at azimuth 115°, so its glint sits
  29° below the horizon ahead-right of the eastward route and follows the player. `AtmosphereController.GetKeyLook`
  NightStormBlackout angular diameter 0.9° → 14° (HDRP turns the angular diameter into the light's max smoothness);
  with lightning held off, yard glare box px ≥ 240: 3,342 → 0 (peak 255 → 161), substation 2,854 → 0 (peak 206);
  the far wet ground toward the moon gets a soft sheen instead; the valley is unchanged; the disc is not visible
  through the storm clouds at any size. Note for labs: a checkpoint start re-applies the preset, so set light
  properties after it. The review's 'blackout corridor lit like a powered hall' (ui) is a capture artefact: a cold
  checkpoint start, a DawnCoast snap and 20 s later all give the dark corridor (walls ~40/255); `capture_set.sh`
  now shoots the UI set before the free-camera preset captures. Still open from review 4: the dusk-hill impostor
  mosaic (needs an impostor shader), the T. rex roar pose, the break room and gift shop still sparse, the first
  gameplay view still evenly lit (dim the troffers, dress the north wall), the valley between strikes still a narrow dark band, the ending crane shot's even
  'broccoli' hills, rain rings and glints on the valley road, no beach from the air.
  (This paragraph was dropped by mistake in `90dd168` and restored afterwards.)
- **Known limits at hand-over** (from reviews 3-4 and round 5, not fixed in time): the dusk-hill impostor mosaic; dawn
  meadow trees a little brown; tall grass still saturated; the
  first launch of a new build compiles shaders (a stall of several seconds in the menu and when the world loads);
  the held hand's fingers do not move; the review 4 items still open (above).
