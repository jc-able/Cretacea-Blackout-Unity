# Hand-in: kit (lamp fixtures, signs, roofs, reflection probes)

Branch `look/kit`, base commit 941668e. Every change is central (kit or Look Pass), so all zones get it, including
zones other agents are rebuilding right now. No zone builder files were touched. Public signatures are unchanged
(one optional parameter was added at the end of `BuildIlluminatedSign`).

## What changed and why

### 1. Every lamp has a body, and the body glows with its lamp (`LookFixtures.Apply`, new step `fixtures`)
- New post-build step, wired into `LookPass.Run` (step name `fixtures`, between `lights` and `shadows`) and into
  `RunOnBuild` right after the lamp calibration. Those are the only lines changed in `LookPassCli.cs`.
- It goes through the scene's lights by name family and builds a fixture next to each light (`Fixture_<light name>`,
  a sibling of the light). Each fixture's lens is added to the light's `LightGroupMember.emissiveRenderers`, so the
  lens dims, flickers and goes dark with its light group. Checked in Play mode: the control-room troffer is lit in
  `Night` and dark in `Corridor`, and the red emergency heads are dark in `Night` and glowing in `Corridor`.
- Last run: all 102 non-directional lights are matched. Nothing is reported unmatched. Families:
  - ceiling mains (`*_Main`, gift shop) → recessed troffer (dark frame, diffuser, louvre bars), flush under the
    ceiling (found by raycast). The lamp moves to 7 cm under the diffuser. Rooms whose ceiling has space also get
    companion troffers on a 3.0 × 2.4 m grid (14 in total). Each is paired with the nearest lamp, so the whole
    ceiling dies together.
  - staff corridor mains → the builder's housings stay. Their diffusers get the fixture lens material.
  - server and cold-room blue lamps → 0.6 m ceiling panels with a blue lens.
  - emergency lamps (`*Emerg*`) → a battery twin-head unit with red lenses and a green charge LED. It is
    wall-mounted when a wall is within 1.3 m, otherwise ceiling-hung. The corridor's `beacon_red` bodies are made
    to glow instead.
  - lobby mains and Warehouse B → high-bay bell shade on a drop rod to the truss or ceiling.
  - storage bulb → bare bulb, socket and cord from a ceiling rose.
  - fossil uplights → floor cans on the plinth.
  - path lamps → glowing lens under the `lamp_post` FBX head. The light moves from the pole axis to under the head.
  - plaza bollards and AuxRoute green bollards → glowing collar near the top. The AuxRoute cube cap is hidden and
    replaced by a domed lens.
  - substation bulkheads → the `lamp_bulkhead` FBX glows (a `_Glow` copy of its material that uses the base map as
    the emission map).
  - floods (Talon, AuxSub, CourtAux) and the E-0 green overhead → flood or signal head (housing, fins, visor,
    yoke, lens) on a crossarm from the bare mast or pylon. The light moves into the lens.
  - workshop pendants → green enamel bell with a lens. The builder's flat box shade is hidden.
  - security desk task lamp → green pendant on a cord over the desk. Records manual beacon → amber beacon on the desk.
  - strobes, beacons, kiosk and jeep hazard → `beacon_red` FBX (red) or a caged dome (amber/green), on a stalk
    down to the surface below or on a bracket to the nearest wall.
  - station status LEDs, the charging LED, the jeep radio glow, the dock storm backlight (it stands for outdoor
    light), the disabled skylight fill and the distant evac lights are skipped on purpose.
- No fixture part casts shadows or has a collider, so a body can never block its own lamp (cached shadows,
  0.12 m near plane), and NavMesh and gameplay are untouched. The step is idempotent: everything `Fixture_*` is
  deleted and rebuilt, and lights that were moved land on the same spot again.
- **Lens luminance: deliberate deviation from the brief.** With the palette's 170-nit diffusers, the current bloom
  turns every lit fixture at night into a halo several hundred pixels wide. Play-mode captures of the security
  office and the workshop show the halo, and it disappears when only the lens renderers are disabled. Fixture
  lenses therefore use their own materials at `LookFixtures.DiffuserNits = 60` (about 3× the night scene white).
  Coloured lenses keep the palette's 45–70 nits. This is one constant, if the lead prefers 170 or retunes bloom.

### 2. Signs with words (`WorldArchitectureKit.BuildIlluminatedSign` + `LookSigns`)
- A sign is now an artwork quad from `Assets/Art/UI/Signs/*.png`, alpha-clipped and fitted to the art's aspect
  ratio, in front of a dark backing plate, on both sides. The artwork is chosen from the sign text, the object
  name or a textured material (`LookSigns.ResolveStem`). Examples: SECURITY → security_office, CONTROL ROOM →
  control_room, RECORDS → records, FEED PREP → feed_prep, any EXIT → emergency_exit, E-0 → e0_exit_decal,
  STAFF → staff_only.
- `BuildFramedDoor` now passes its `overheadSignText`. Before this, the door signs sat inside the lintel and
  could not be seen at all.
- Texts with no artwork get a world-space TextMeshPro label on the dark plate (HDRP SDF-lit material). This was
  checked with a temporary test sign ("MAINTENANCE B-2"); none of the current texts need it.
- Exit signs (emergency_exit, E-0) are back-lit at 60 nits and paired with their group (VCExit, the always-on
  battery wayfinding group; AuxYard for E-0). Room plaques are printed plates with no emission: they are lit by
  the room and the flashlight, so they never glow in the blackout.
- Post pass `LookSigns.Apply`: when a sign is embedded in a wall, each face is moved onto that side's wall
  surface. A face that looks into a wall less than 0.35 m away is removed.

### 3. Roofs and exteriors (`LookRoofs`)
- Every flat roofed ceiling slab with nothing above it gets a bitumen roof cap (12 cm overhang). Edges that face
  outside get a concrete parapet with a dark metal coping; edges shared with a neighbouring roof get none. Roofs of
  room shells larger than 90 m² get 1–2 rooftop HVAC units (deterministic). Last run: 16 caps and 43 parapet
  runs. The cap is fitted to the ceiling's final bounds, so Zone 1's ceiling insets are respected.
- RainBlocker proxies (layer 13) get `MAT_RainBlockerHidden`, a fully alpha-clipped HDRP Unlit material, in the
  kit and again in the post pass (62 slabs). They can no longer show in any colour camera or probe: the lobby
  skylight now shows glass and sky instead of a grey slab. They keep their layer and renderer for the rain
  occlusion camera.

### 4. Reflection probes that tell the truth (`ConfigureTruthfulProbe`, `LookProbes`, `ReflectionProbeRefresh`)
- Room probes are realtime OnDemand, multiplier 1 (was 0.45), sky clear (the sky shows through windows), a
  culling mask without Viewmodel and RainBlocker, and 256 px cubes (outdoor probes 128 px). Re-rendering all 20
  probes in the same frame on the blackout overflowed the 4096² atlas: 25 "No more space in Reflection Probe
  Atlas" errors on the Corridor jump, at 512 px and still at 256 px. The refresher now issues at most one probe
  render per frame in the whole scene. Retested Night→Corridor→Night→Corridor and then the full chapter tour:
  0 errors.
- New runtime component `Cretacea.Rendering.ReflectionProbeRefresh`: renders on enable (chapter start), again
  2.5 s later (exposure settled), and 0.2 s after every light-group change. Outdoor probes also refresh every
  15 s while the atmosphere blends. Renders are staggered and skipped during a lightning flash.
- `LightGroupController`: only the hook. It bumps a global shader float `_CretaceaLightEpoch` when a group
  finishes changing (instant set, end of fade, end of ripple). Cretacea.Rendering does not reference Cretacea.Core,
  so a shader global was used rather than editing an asmdef. Checked in Play mode: the epoch rises on a chapter
  jump, and `HDProbe.m_RealtimeRenderCount` of the staff-corridor probe went 4→5 after the blackout. Probes
  outside every camera's view render when they next come into view (HDRP keeps the request).
- 9 outdoor probes: plaza, three service-path bends, substation pad, AuxRoute, Old Yard, exit court and coast road.

### 5. Static flags
- Kit geometry that never moves gets `BatchingStatic | OccludeeStatic` (about 1,780 renderers; static shadow
  casters are still set by the lights step). These stay dynamic: doors, interactables, animated or physics
  objects, anything under a scripted Cretacea component, and every renderer driven by a light group (emissive
  MPB). Tall-grass renderers (3,100, under `TallGrassZoneVolume`) are left dynamic on purpose, because static
  batching would break their instancing.

## Files touched
- `Assets/Editor/WorldBuilder/WorldArchitectureKit.cs`: `BuildIlluminatedSign` (art faces, plates, label,
  optional `text` at the end), `BuildSignLabel`, `BuildFramedDoor` passes the sign text,
  `ConfigureTruthfulProbe`, the room probe uses it, and the rain blocker gets the hidden material.
- `Assets/Editor/LookPass/LookPassCli.cs`: two call lines plus `fixtures` in `AllSteps`.
- New: `Assets/Editor/LookPass/LookFixtures.cs`, `LookSigns.cs`, `LookRoofs.cs`, `LookProbes.cs`.
- New: `Assets/Scripts/Rendering/ReflectionProbeRefresh.cs`.
- `Assets/Scripts/Core/LightGroupController.cs`: `LightEpochId` and `NotifyGroupSettled()`, called at three places.
- Generated materials (regenerated on every build): `Assets/Art/Materials/World/Signs/MAT_Sign_*.mat`,
  `MAT_SignLabel_HDRP.mat`, `World/Fixtures/MAT_*_Glow.mat`, `World/Look/MAT_RainBlockerHidden.mat`,
  `World/MAT_Lens_*.mat`.
- `LookLights.Kit.cs` is unchanged. No new lamps were added, so no lamp rules were needed.

## Requests for the lead
- **Bloom versus emissives:** 170-nit palette emissives (`EmissiveWarmWhite`/`CoolWhite`) bloom into very large
  halos at night in Play mode. Fixtures use 60 nits. Either confirm that, or retune bloom (intensity/scatter) and
  raise `LookFixtures.DiffuserNits`.
- `CretaceaCliTools.capture` (`cretacea capture`) temp camera renders every layer. It should mask Viewmodel (12)
  and RainBlocker (13) like the gameplay camera. Free-camera captures in Play mode show the floating flashlight
  viewmodel.
- The HDRP reflection atlas is 4096². If more realtime probes are wanted, enlarge it in `LookRenderSettings`.
- **Incident:** the shared scratchpad helper `env.sh` was overwritten by the creatures agent while I was working.
  One sourced command then sent `editor.playmode start` and `jump_chapter Night` to the creatures clone's editor
  (port 7807) at 02:11. I put it back straight away (`editor.playmode stop`, verified `isPlaying=False`) and
  deleted the one stray capture I had written into `creatures/Screenshots/play/`. Nothing else was run there. From
  then on, every command passed `--project-path` explicitly. Please tell the creatures agent, in case their
  editor state surprised them.

## Not verified / rough edges
- Final smoke test (NavMesh baked; Night, Corridor, ServicePaths, Substation, OldYard, Escape, DawnCoast): 0
  errors and 1 warning, HDRP's "ScriptableRuntimeReflectionSystemSettings.system is assigned more than once" at
  Play start. It is HDRP-internal and probably not from this package, but I did not compare against the base
  commit.
- The 60 fps budget was not measured. There are about 1,500 extra small renderers (fixtures, roof parts), with no
  shadows or colliders.
- Watchtower strobes, the jeep hazard and the Talon strobes were only checked from a distance.
- Probes that are outside the view when a group switches re-render on the next frame they are seen. That relies
  on HDRP behaviour and was not proven with a capture.
- A twin-head emergency unit on the ceiling is small and reads mostly as two red dots in the blackout.
- The dock canopy also gets a cap and parapet, which is acceptable for a flat canopy. Rooftop units are limited
  to room shells.
- The menu scene is skipped by the fixtures step (no `World` root). Its exit sign gets the new sign art from the
  kit.

## Fix round (art director review of 423fc1d)

Everything below is in files this package owns. Captures: `Screenshots/handin/` (14 files, INDEX.md); the first
hand-in's captures are kept in `Screenshots/round1/` for before/after. Last build log line (with NavMesh):
`fixtures: ... troffer=8, troffer grid companions=14, ...; signs=11 faces snapped to walls=10 faces culled=4 free signs
moved onto their wall=4; roofs: caps=16 parapet runs=43 rooftop units=17; rain blockers hidden=62; probes: outdoor
added=9, total OnDemand+masked=20; static flags: static=1793 dynamic=3354`. `lights calibrated=98 unmatched=0`.

### Findings and what was done
1. **Lit fixtures render as blown-out halos (high).** Reproduced in Play mode (Night: security, control, lockers).
   Volumetric fog is *off* at night in the live stack (`Fog.enableVolumetricFog=false`), so the halo is bloom
   (threshold 0, scatter 0.68, intensity 0.2). Done here: (a) kit rules in `LookLights.Kit.cs` lower the volumetric
   dimmer of the interior mains to 0.10-0.15 and the workshop pendants to 0.30 (matters for the storm preset);
   (b) measured lens luminance against the *current* bloom (sheet 02): 60 nit = blob, 40 = blob, 25 = frame and
   louvre grid read. I also tried the critic's proposed bloom (threshold 0.9, scatter 0.5, intensity 0.1) on the
   loaded NightCalm profile in Play mode and restored it afterwards (nothing saved): a 60-nit lens is still a soft blob.
   `LookFixtures.DiffuserNits` is now **25** (deviation from the bible's 170, documented in the code). Bloom stays a
   lead decision (request below). Result: sheets 01, 02, 11. Workshop pendants are better but still glow (sheet 12).
2. **Fixture bodies cut dark holes in their own pools (high).** Every light that gets a fixture now has contact
   shadows forced off (`NoContactShadows`, after calibration). Sheet 05 (E-0 control box pool bright at its centre),
   sheet 09 (bollard base), sheet 08 (desk beacon).
3. **E-0 signal head floats; path lamp 0 buried (high).** `Flood()` with no mast now searches for a wall (doors and
   interactables excluded) at the lamp height and up to 1.5 m lower, mounts the head on a bracket + wall plate there
   and moves the lamp into the lens; with no wall either, it stands on a post. The E-0 head now sits on the gate
   lintel above the sign (sheet 05). The other 8 floods keep their masts (sheet 06). A first version put a post in
   the middle of the gate passage; that is why the wall search goes lower. Path lamp 0: the `lamp_post` FBX itself is
   inside the wall (Zone 2 geometry), so its light pools on the wall; request below (sheet 09).
4. **E-0 sign is an amber stencil (medium).** `LookSigns.SignMaterial`: stems `e0_exit*` keep the alpha clip, base
   and emission become escape green (0.22, 0.95, 0.36), no emission map, so only the letters glow green. The sign was
   authored 0.38 m in front of the lintel; the free-sign wall gap is now 0.5 m, so it moves onto the lintel and loses
   its back face (sheet 05). The sideways arrow is in the art: request below.
5. **Probe throttle spaced requests, not renders (medium).** Confirmed in HDRP source
   (`ReflectionProbeTextureCache.GetReflectionProbeSizeInAtlas`: < 512 px x4, >= 512 px x2, so 256 and 512 both take
   a 1024 tile; the comment in `ConfigureTruthfulProbe` is corrected, resolutions unchanged). `ReflectionProbeRefresh`
   now keeps a render pending on its own side and only calls `RequestRenderNextUpdate()` when the probe's influence
   box is inside `Camera.main`'s frustum (planes cached once per frame), still one probe per frame in the scene.
   Test (Play): Night spawn facing north = 19 of 20 pending, turn to yaw 120 = all rendered over the next frames;
   blackout jump looking at the floor = 11 pending, turn = 10 more rendered; 0 console errors (no atlas errors).
   Sheet 03 shows a floor reflection with a stale Night probe vs the refreshed blackout probe (the refreshed one
   carries the red emergency light; the floor tiles are rough, so the difference is subtle).
6. **Outdoor probes re-render every 15 s (medium).** Periodic pass removed (field `outdoorPeriod` deleted). Outdoor
   probes render on enable, at the 2.5 s pass, on light changes, on `ReflectionProbeRefresh.RequestAllOutdoor()` and
   when the global float `_CretaceaAtmosphereEpoch` changes (tested: bumping it rendered the 3 outdoor probes in view).
   **Frame times** (hidden batch editor, Play, 1920x1080 custom game-view resolution, ProfilerRecorder; three other
   agents' editors were running at the same time, so absolute numbers are low and noisy): OldYard 30 s = 47 fps
   (main thread 21.1 ms, GPU 22.2 ms avg, p99 42 ms). A/B in OldYard with every Fixture_/LookRoof_/outdoor probe
   object disabled vs enabled: 48.0 fps / GPU 21.98 ms vs 47.0 fps / GPU 22.38 ms (about 0.4 ms; a second "on"
   sample was disturbed by the re-enable re-rendering cached shadows and probes). Night spawn: ~21-23 fps, GPU
   ~50-54 ms with or without the 14 companion lamps (difference inside the noise). Corridor jump + 6 s: 23 fps,
   max 117 ms GPU frame. 60 fps at 1080p is **not** met in this environment even with the package disabled; it was
   not measured in the owner's windowed editor.
7. **Room plaques double-sided; free signs float (medium).** `BuildIlluminatedSign`: a printed (not back-lit) sign
   under a `Door_*` root builds only side 0 (local -Z = the corridor for every kit door). `LookSigns.Apply`: when a
   free sign's face is culled against a wall, the rest of the sign moves onto that wall (4 signs moved). Sheet 01
   (no plaque inside the control room), sheet 04 (plaque from the corridor).
8. **14 companion troffers without lamps (medium).** Each companion now has its own lamp
   (`Fixture_Lamp_<main>_Grid<i>`, downward spot, own `LightGroupMember` in the main's group with the main's backup
   factor, lens paired to it). Kit rule `^Fixture_Lamp_.*_Grid\d+$` (Shadow.Low so a lamp near a wall cannot leak into
   the next room, volumetric 0.10). The room main's table candela is shared evenly: main and companions each get
   candela/(n+1) (from the table, so re-runs never divide twice). All 14 are in the lockers room (1400 cd / 15 = 93 cd
   each). Sheets 02, 11.
9. **Dead diffusers near-black (medium).** White and tungsten lens materials get an opal base (0.85, 0.85, 0.82);
   emission unchanged. Sheet 04 (dead corridor diffuser in the flashlight).
10. **Bulkhead glow double-linearised (low).** `LensColor` returns the sRGB encoding of the CCT colour, so the glass
   glows amber like its lamp (sheet 09). The grey housing still glows a little because the model's whole base map is
   the emission map.
11. **Roofs (low).** Rooftop duct sits on the roof with a flashing (was floating 15 cm); unit grille dark instead of
   rust-orange corrugated sheet; no parapet towards a neighbour roof within 2.5 m (sampled at 0.35, 1.4, 2.5 m);
   sheet-metal sheds (Warehouse B, dock pad) get a corrugated fascia + capping instead of a concrete parapet
   (sheet 10). Not fixed: the narrow notch on the west wing roof is still fenced (its neighbour is more than 1.2 m
   lower, so it does not count as a neighbour), and roof caps stay `RoofBitumen` (near-black in the day preview).

Also done: exit signs. In the blackout a 60-nit exit sign was a green halo; 22 nit still a clipped box. `LookSigns.
BacklitNits` = **10** (about the blackout scene white): the green field and white pictogram read (sheet 04, menu
sheet 13). Server/cold-room lamps (kit rules) now throw down like every other ceiling fixture. The `AllSteps` edit in
`LookPassCli.cs` is reverted; instead the permitted `Run` call line runs fixtures whenever `lights` runs
(`wanted.Contains("fixtures") || wanted.Contains("lights")`), because calibration resets what fixtures set
(contact shadows, the companion split). Static flags (goal 5) unchanged: they only matter if the lead enables static
batching / bakes occlusion; `staticShadowCaster` (set by the lights step) is the flag that feeds cached shadows.

### Files touched in this round
`Assets/Editor/LookPass/LookFixtures.cs`, `LookLights.Kit.cs` (kit rules + `RuleCandela` / `ApplyRuleTo`),
`LookSigns.cs`, `LookRoofs.cs`, `LookPassCli.cs` (AllSteps reverted, Run call line), `Assets/Editor/WorldBuilder/
WorldArchitectureKit.cs` (single-sided door plaques, probe comment), `Assets/Scripts/Rendering/ReflectionProbeRefresh.cs`.
Regenerated materials: `MAT_Lens_*`, `MAT_lamp_bulkhead_Glow`, `MAT_Sign_e0_exit_decal_Lit`, `MAT_Sign_emergency_exit_Lit`.

### Requests for the lead (fix round)
- **Bloom (LookAtmosphereProfiles, night/storm):** with threshold 0 every lens and exit sign blooms. If bloom is
  retuned (e.g. threshold ~0.9, scatter ~0.5, intensity ~0.1), raise `LookFixtures.DiffuserNits` (25) and
  `LookSigns.BacklitNits` (10) and re-check: the proposed bloom alone did not rescue a 60-nit lens.
- **AtmosphereController:** when a `SetPreset` blend finishes, call
  `Shader.SetGlobalFloat("_CretaceaAtmosphereEpoch", ++epoch)` (no assembly reference needed) or
  `Cretacea.Rendering.ReflectionProbeRefresh.RequestAllOutdoor()`, so outdoor probes re-render after a preset change.
- **Zone 2 owner:** path lamp 0's `lamp_post` stands inside the wall at about x 30, z -8; pull it onto the path so its
  head and pool are over the path.
- **Zone 3 owner:** the E-0 artwork has a sideways arrow although the sign hangs over the gate; arrow-free gate art (or
  a green `emergency_exit` box above the gate) would fix the last E-0 issue.
- `cretacea capture` should mask layers 12/13 (viewmodel shows in free-camera Play captures, e.g. the corridor).
- **Incident (this round):** the workflow scratchpad folder is shared by all agents. My capture helper `cap.sh` there
  was overwritten by the zone23 agent's version (pointing at the zone23 clone) at 03:29. Before I noticed, four of my
  capture calls ran `cretacea capture` (a temporary camera render, no scene change) in the zone23 editor and wrote
  `d_aerial_vc.png`, `d_roofs_west.png`, `d_rooftop_unit.png`, `d_warehouse_roof.png` into
  `zone23/Screenshots/fix/`. I deleted exactly those four files and moved my helpers into
  `kit/Screenshots/_tools/`. My earlier `cap.sh` (pointing at kit) sat in the shared folder from about 03:00 to 03:29,
  so another agent may have rendered captures in the kit editor; nothing foreign was found in kit/Screenshots.
  Please tell the zone23 agent, and give agents per-agent scratch folders.

### Not verified / rough edges (fix round)
- 60 fps at 1080p: not met in this shared hidden editor even with the package disabled; never measured in the
  owner's windowed editor.
- Probe proof is a frustum/request trace plus one subtle floor-reflection pair; no wet-ground outdoor reflection was
  captured.
- Workshop pendants and the substation floods seen face-on still bloom; the ceiling twin-head reads as two red dots.
- With the lights 7 cm under their diffusers, lit ceilings around troffers stay dark (downward throw by design).
- Edit-mode NightCalm previews show the dawn evac light (85 000 cd) as a bright streak on the eastern horizon.
