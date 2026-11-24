# Story + game flow map (Cretacea: Blackout, Unity port)

Paths relative to ~/CretaceaBlackoutUnity (Scripts under Assets/Scripts). "not verified" = not confirmed in code/live.

## 0. Architecture in one screen

- `Cretacea.Story.StoryDirector.Instance` (StoryDirector.cs:28) on `[Managers]/[StoryDirector]`, created by the builder with
  `autoStartOnAwake=true`, **`editorStartCheckpoint=Night`** (Editor/WorldBuilder/CretaceaWorldAssembler.cs:1020-1064) => plain Play in
  MainWorld starts at CHAPTER 1, not the intro. `Start()` (SD:116-128) uses `gs.CurrentCheckpoint` only if flag `launch_from_menu`.
- `enum CheckpointId { Intro, Night, Records, Dock, Paddock, Substation, Yard }` (Core/GameLayers.cs:5-14); `AtmospherePreset {GoldenDay,
  DuskTransition, NightCalm, NightStormBlackout, DawnCoast}` (:27-34); `LightGroupId {VCMain, VCEmergency, VCExit, Plaza, Paths, DockMain,
  Talon, PaddockBattery, SubBattery, YardBattery, AuxSub, AuxRoute, AuxYard}` (:36-51).
- Beats come from (a) `StoryTriggerVolume` boxes (Core/StoryTriggerVolume.cs: oneShot, Player layer, `GameEvents.RaiseStoryTriggerEntered`)
  dispatched in SD:1500-1587 and (b) interactable callbacks wired in SD:359-422. All systems talk via static events (GameEvents.cs:78-140).
- `GameState` (flags, documents, checkpoint, Substation checklist) is DontDestroyOnLoad (GameState.cs:89-100): survives reloads and jumps.
  Flags: blackout, collapse, manual_read, master_key, flashlight, aux_power, fuel_primed, gen_running, gate_started, e0_open, launch_from_menu.
- Death/restart/menu: `Cretacea.UI.MenuController` reloads scene "MainWorld" (CretaceaWorldAssembler.cs:978). MainWorld.unity is
  BINARY-serialized (6 MB, not greppable); MainMenu.unity is text.

## 1. Beat sheet (in play order)

Coordinates are Unity world (X east, Z north). SD = Assets/Scripts/Story/StoryDirector.cs. Trigger boxes are
`WorldArchitectureKit.BuildStoryTrigger(parent, id, center, size, oneShot)` (Editor/WorldBuilder/WorldArchitectureKit.cs:863).
"Facility state N" / "B" = `ApplyNightFacilityState(blackout:false/true)` (SD:294-357), detailed in section 7.

**PROLOGUE (CheckpointId.Intro)** - only from Menu NEW GAME (MenuController.cs:283-288) or jump_chapter Intro.
- `StartFromCheckpoint(Intro)` -> facility state N (NightCalm snap) -> `OpeningCutsceneRoutine` (SD:428-538): GoldenDay snap,
  lightning OFF, groups VCMain ON, Talon ON, Plaza OFF, VCEmergency OFF; title "CRETACEA PARK / Isla Tormenta | 5:42 PM" (4 s);
  music calm_day, ambience day, letterbox, fade-in 3 s, skip prompt. 8 glide shots, ~52 s (table 1a). Then
  `FinishOpeningCutscene` (SD:569-583) -> Chapter 1 routine.
- Showcase actors (AI off, despawned at end; SD:540-565): brachiosaurus `intro_sauropod_a` at meadow_sauropod_a (-40, terrainY, -150)
  yaw 160, `_b` at (-95, ty, -185) yaw 60; 3 triceratops `intro_trike_0..2` at (-18,ty,-142)+i*(7,0,-4) yaw 110/125/140;
  T. rex `intro_trex` at (64, gy, -28) yaw 180 (anchors Zone4_TerrainJungleAndDawnCoastBuilder.cs:531-533).

**CHAPTER 1 - NIGHT SHIFT (CheckpointId.Night)** `StartChapter1NightShiftRoutine` (SD:589-640)
- ApplyCheckpointPreset(Night) + **SaveCheckpoint(Night)** (writes save) + facility state N. Player -> `start` (-64.5, 0.05, 28.0)
  yaw 0 pitch -6 (Zone1_VisitorCenterBuilder.cs:125). Desk flashlight `pickup_flashlight` reset (SD:610-613). `terminal`
  (-61.0, 0.95, 26.0) enabled+locked, `control_console` disabled, `door_security` open, `door_control` closed.
  Ambience vc_night, music night_unease, fade 1.5 s (jump) / 2 s (after intro), title "23:05 / Night Shift - ..." 3 s.
  +2.8 s objective "Take your tactical flashlight from the security desk." + VO vo_dana_01 (radio).
- 1.1 Flashlight pickup (FlashlightPickup.cs:113-139): equip anim + beam auto-ON, toast, `RaiseFlashlightPickedUp` ->
  SD:642-650 unlock terminal, objective "Clock in at the security terminal."
- 1.2 Terminal used (SD:652-672): toast "SHIFT START 23:07 ...", +1.2 s VO dana_02, dana_03, objective "Walk east to the
  Control Room and log the paddock feeds.", `door_control` opens (animated), console unlocked.
- 1.3 BLACKOUT: trigger `control_switchover` center (-37.0, 1.5, 29.8) size 7x3x3.8 (needs HasFlashlight && !blackout;
  Zone1_VisitorCenterBuilder.cs:289) OR console at (-37.0, 0.95, 31.2) -> `SwitchoverBlackoutRoutine` (SD:686-782). Timeline:
  t0 objective cleared, walk speed x0.78 | 0.8 VO dana_04 | 4.8 pa_chime | 6.1 VO pa_01 | 8.1 lightning 1.0 (no thunder) +
  thunder sound + trauma .32 | 8.7 transformer explosion SFX at (-60, 10, 200) | **9.05 VCMain OFF fade 1.2 s flicker, Plaza OFF 2.0 s
  flicker, Paths OFF 2.0 s flicker, DockMain OFF 1.0 s flicker, all CCTV -> static, flashlight flicker 1.5 s**, ambience vc_dark,
  music stops | 11.65 Talon OFF 0.4 s flicker | **13.05 VCEmergency ON 0.3 s, flag blackout, atmosphere NightStormBlackout blend 8.0 s
  (done at 21.05), lightning rate 1.0** | 14.05 alarm | 15.25 VO pa_02 | 18.85 music tension, forced look at talon_gate_view
  (-36, 2.5, 42) | 20.15 lightning 1.0+thunder, `talon_gate` AnimateOpenTo(1, 6.0 s) | VO pa_03 | 22.35 lightning .85 + 2 breakout
  raptors `breakout_raptor_0/1` at (-36,0,56)/(-33.5,0,59), scripted run to (-36/-34, 0, 36) then (-80/40, 0, 30) at 6.5, despawn
  (SD:784-801) | 24.75 look released, speed 1.0, VO dana_05, `door_wing` re-locked, `lobby_damage` + `lobby_shutter_closed` shown,
  lobby skylight rain ON, shutter sparks ON | 25.75 objective "Get to the main entrance in the Lobby Atrium."
- 1.4 `lobby_beat` trigger center (-31, 2.0, -6.5) size 37x4x50 (needs blackout; Zone1...:652) -> SD:809-839: glass crack SFX at
  (-24, 9, 0), forced look at lobby_shutter_look (-32, 2, -31.6), VO vance_01, raptor `lobby_glass_raptor` (-58,0,-38) -> (-36,0,-36)
  speed 1.6, stares 2.2 s, lightning .65, flees to (10,0,-60). +8.5 s objective "Try the Staff Exit at the west end of the Lobby."
  + VO dana_06. `shutter_control` use = toast "No power..." (SD:803-807).
- 1.5 `staff_exit_collapse` trigger center (-55.5, 1.5, -10.0) size 5.5x3x3.6 (needs blackout; Zone1...:527) -> SD:853-879: groan
  at staff_exit_look (-64.5, 1.2, -10.0), +1 s rubble object `staff_exit_collapse` shown, flag collapse, trauma .82, damage flash
  .28, flashlight flicker 1.1 s, +2.5 s VO dana_07, +5.4 s "The radio dies.", objective "Search the Records Annex (east end of
  Staff Corridor) for the 1996 evacuation binder.", `SpawnWingRaptor()`. `staff_exit_door` (-72,0,-9.8) stays locked.

**CHAPTER 2 - RECORDS ANNEX (CheckpointId.Records)** - no trigger; the checkpoint is saved when the manual is read.
- Wing raptor `wing_raptor` (SD:929-946) at wing_cold (-7,0,-4), aggression .85, patrol (-7,0,9) (3,0,15) (-7,0,-4) (3,0,-3)
  (0,0,-11); spawned only if no active raptor. It sits behind `door_wing` (-2,0,19, locked until manual).
- Manual `doc_manual` at anchor manual (-3.6, 0.82, 27.3) behind `door_records` (-9,0,22). Reading ends after all pages are closed
  (DocumentPickup.cs:130-147) -> SD:905-927: flags manual_read + master_key, `door_wing` unlocked, toast, **SaveCheckpoint(Records)**,
  VO vance_02, objective "Restore auxiliary power at Substation 3 (Paddock 4), then reach Exit E-0. Route: Feed Prep -> Loading
  Dock -> Service Path." Feed Prep doors door_cold/door_vet/door_cold_s; `dock_roller` (8,0,-11) is a jammed shutter you crouch under;
  `dock_crouch_hint` trigger (7.1,1.5,-11.0) 1.4x3x3.2 repeating toast.

**CHAPTER 3 - SERVICE PATHS (CheckpointId.Dock)**
- `reach_dock` trigger (10.2, 1.5, -11.0) 2.8x3x3.8 -> SD:964-972: wing raptor despawned, **SaveCheckpoint(Dock)**, objective
  "Follow the concrete service path east to the Paddock 4 service gate.", ambience paths, path-lamp sparks VFX ON. No title card
  in normal play (title only on jump, SD:952-962).
- `paths_corner2` (55,2,14) 5x4x4.8 -> raptor `paths_cross_raptor` raptor_cross_a (64,0,30) -> raptor_cross_b (64,0,-6) speed 7,
  lightning .75+thunder, despawn <= 4.2 s (SD:980-993). `paths_mid` (71,2,6) 5x4x5 -> bush rustle + distant call (SD:1589-1596).
  `paths_end` (85,2,-3.5) 5x4x5.5 -> objective "The Paddock 4 service gate is jammed. Crouch {crouch} under the bent north leaf."
  `service_gate_box` (89.6,1.25,-6.0) -> toast "Dead. No power..." (all triggers Zone2_ServicePathsBuilder.cs:134-136,213).

**CHAPTER 4 - TYRANT VALLEY (CheckpointId.Paddock)** (no jump_chapter target; see 3)
- `enter_paddock` (93.5,1.5,-2.2) 2.2x3x4 -> SD:1010-1018: **SaveCheckpoint(Paddock)**, objective "Cross Tyrant Valley to Substation 3.
  Stay low in tall grass - keep your light off near it.", ambience paddock, music night_unease, T. rex `valley_trex` at trex_drink
  (192, terrainY, 40) yaw 160, 9-point patrol (SD:81-92) from index 1, aggression .9.
- `trex_reveal` (128,2,0) 9x8x16 -> SD:1041-1051: lightning 1.0, far roar, caption, +0.45 s lightning .8+thunder, VO vance_03, music tension.
- `doc_jeep_radio` (audio log, jeep at ~(183.5, ty+0.9, -21)) -> VO ranger_01, ranger_02 (SD:1053-1064).
- `enter_substation` box center (268, 6.8, 0) size 38x10x36 (Zone3_SubstationAndYardBuilder.cs:98-102) -> SD:1104-1120:
  **SaveCheckpoint(Substation)**, T. rex leashed to substation_center (268, 2.3, 0), objective "Restore Auxiliary Circuit B - reset all
  three tripped breakers (n/3)."

**CHAPTER 5 - SUBSTATION 3 (CheckpointId.Substation)** stations = `SubstationStation` (Interact/SubstationStation.cs)
- Breakers hold 1.2 s at breaker_1 (256, 2.3, 13), breaker_2 (258, 2.3, -13), breaker_3 (282, 2.3, 13) (Zone3...:135-137), each noise 55 m.
  Fuel pump hold 4.0 s at (263.8, 3.35, 11). Generator starter (273.5, 3.45, 7.1) press x3: #1 "It coughs and dies...", #2 T. rex
  Investigate + roar after 2.3 s (SD:1128-1157), #3 runs: gen_engine_loop, **T. rex HuntPlayer + ChaseRubberBand .75** (SD:1137-1146).
  Transfer lever hold 2.2 s at (277.6, 3.55, -7.0) -> `RestoreAuxiliaryPowerAndChaseRoutine` (SD:1193-1215): flag aux_power, T. rex
  teleported to (258, gy, 22) yaw 125; +0.4 s **AuxSub ON 0.35 s flicker**, floodlights SFX, **AuxRoute ripple 0.12 s per light with
  surge**, **AuxYard ON 0.5 s**, **SaveCheckpoint(Substation)**, `StartTrexChase` (SD:1223-1250): stamina full, drain x0.15, music
  chase, objective "RUN! Follow the green route lights east to the Old Maintenance Yard!", `sub_blocker` hidden, fence smash at
  (250, 2.3, 0), trauma .65, Fence_Bash + roar, aggression 3.0. Rubber band per frame (SD:1252-1273): if player >3 m/s
  band = lerp(0.72, 1.30, invlerp(13, 30, dist)) else 1.12. **Atmosphere stays NightStormBlackout after power is restored.**
- `yard_early_warn` (398, 2.5, -22.8) 4x4x6, only if !aux_power -> VO vance_04 (Zone3...:343).

**CHAPTER 6 - OLD MAINTENANCE YARD (CheckpointId.Yard)**
- `reach_yard` (403, 2.5, -22.8) 2.2x4x3.4 (Zone3...:344) -> SD:1279-1299: chase off, wall impact SFX at yard_gap (401,1,-22.8),
  trauma .55, T. rex scripted to (391,1,-22.8), roars 3.2 s, then retreats to (330, gy, 40) and patrols; **SaveCheckpoint(Yard)**;
  ambience yard, music tension; `SpawnYardRaptorsAndSetupE0` (SD:1324-1365): objective "Find Service Exit E-0 on the east side of
  the Old Maintenance Yard.", `gate_box` (503.8, 2.15, -52.6) locked unless aux_power, hold progress restored from GameState;
  raptors `yard_raptor_a` (445,1,20) patrol (445,1,12)(472,1,20)(480,1,-2)(452,1,-4) and `yard_raptor_b` (470,1,-30) patrol
  (470,1,-30)(486,1,-52)(472,1,-78)(452,1,-40), aggression .8.
- `yard_site_map` (442, 2.5, -16) -> VO vance_05. Halvorsen log doc at (420.4, 1.83, -11.4).
- E-0 box hold (cumulative, preserveHoldProgress) -> SD:1367-1388: progress saved to GameState, leaves strain open <= 18 %,
  grind SFX + noise 25 m every 8 %, >5 % flag gate_started + objective "Keep holding OPEN on E-0 - progress is saved if you hide.
  They can hear the motor!". Complete -> SD:1390-1410: flag e0_open, `e0_gate_leaves` (505,1,-46) open in 1.0 s, objective
  "GO! Sprint out through Service Exit E-0!".

**ESCAPE + ENDING**
- `through_e0_gate` (515, 3.0, -46) 3.2x4.5x9.2 (Zone3...:511) -> SD:1412-1446: gate slams shut in 1.35 s, +1.3 s raptors run into the
  chain-link (Bite_Lunge), music ending.
- `coast_ending` (536.5, 3.5, -46) 4.5x5x12 (Zone4...:565) -> `DawnCoastEndingRoutine` (SD:1448-1494), table 1b.

VO: 17 queued lines (dana_01-07, pa_01-03, vance_01-05, ranger_01-02; SD:1645-1672), dropped on StartFromCheckpoint (SD:284).

### 1a. Intro cutscene

`CameraDirector.GlideCutsceneShot(fromPos, toPos, lookFrom, lookTo, seconds, fovStart, fovEnd, abort)` (Player/CameraDirector.cs:204-250):
SmoothStep lerp of position and look target, Cinemachine `[CutsceneCinemachineCamera]` (far clip 4000), hard cut between shots
(only the first shot calls SwitchToCutsceneCamera). Pauses with GameState.IsPaused; uses Time.deltaTime. Calls at SD:460-531.

| # | from -> to (pos) | look from -> to | s | FOV | atmosphere / events |
|---|---|---|---|---|---|
| 1 | (-176,1.5,-252) -> (-92,9.5,-150) | (-120,4,-190) -> (-36,6,-42) | 12.0 | 46->44 | GoldenDay; meadow glide to VC |
| 2 | (-8,1.65,-51) -> (-24,1.75,-47.5) | (-70,5,-30) -> (-78,6,-28) | 8.5 | 50->46 | VC glass facade |
| 3 | (-44,1.5,-91.5) -> (-41,1.8,-92.5) | (-40,6,-150) -> (-38,10,-150) | 7.5 | 42->38 | sauropod call |
| 4 | (44,1.65,-47) -> (47,1.7,-51) | (63,4,-40) -> (66,4.5,-52) | 6.0 | 52->48 | T. rex walks to (66,gy,-52) |
| 4b | (47,1.7,-51) -> (48,1.75,-52) | (66,4.5,-52) -> (66,5.5,-54) | 3.8 | 48->42 | Roar_Massive, crowd SFX, trauma .28 |
| 5 | (-10,34,-150) -> (-20,30,-136) | (-34,2,-40) -> (-36,2,-36) | 4.0 | 55 | "Closing time"; **DuskTransition blend 4.0 s** |
| 5b | (-20,30,-136) -> (-26,26,-120) | (-36,2,-36) -> (-40,2,-30) | 4.5 | 55 | **Plaza ON 1.2 s; NightCalm blend 4.5 s**; music night_unease |
| 6 | (-86,3.2,27) -> (-73.8,1.85,28.4) | (-65,1.55,28.5) -> (-65,1.45,28.5) | 5.5 | 50->38 | "23:04"; lightning ON; flash .9 at end |

Total ~51.8 s. Shot 4 aims at the Viewing Gallery; shot 6 pushes into the Security Office west window (anchor
opening_shot6_window (-72, 1.8, 28.5), Zone1...:126). Skip = hold SkipCutscene >= 1.15 s unscaled (SD:1674-1697); the HUD only
shows "Skip Intro" and ignores the callback passed at SD:453 (HudController.cs:764-772). A skip jumps straight to Ch1 without the
Dusk/NightCalm blends (Ch1 snaps NightCalm via facility state N).

### 1b. Ending cutscene

`DawnCoastEndingRoutine` (SD:1448-1494) + `CameraDirector.PlayDawnCoastCraneShot(ending_view)` (CameraDirector.cs:258-282).
ending_view = (543.0, 1.05, -46.0) (Zone4...:562). Not skippable (no abort func).
- t0: SetCutscene(true), controls off, flashlight beam off + suppressed, music ending 3 s, ambience dawn 5.5 s, lightning OFF,
  **DawnCoast blend 8.5 s** (so the first ~8.5 s of the crane shot show a storm->dawn cross-fade), letterbox ON 2 s,
  title "Isla Tormenta | 5:51 AM" 3.2 s.
- Crane: cut to (540.5, 2.70, -46.0) -> (543.4, 2.70, -46.0), look (900,-12,-40) -> (900,-2,-40), 5.5 s, FOV 48->44 |
  far T. rex roar SFX at (380,10,-20) + caption + trauma .2 | hold 1.1 s | (543.4,2.70,-46) -> crane peak (536.0, 15.25, -42.0),
  look -> valley (310, 14, -15), 3.8 s, FOV 44->50 | hold 1.5 s | -> (546.0, 10.55, -49.0), look -> (900, 6, -40), 4.2 s, FOV 50->46.
  Crane total ~16.1 s.
- Then fade to black 3 s (+0.2), letterbox off, title "CRETACEA: BLACKOUT / You made it out." 3.5 s, +4.8 s SetCutscene(false),
  `RaiseGameFinished(BuildCompletionStatsSummary())` (GameState.cs:480-489) -> MenuController ending panel over the black.
- LOOK NOTE: builder DawnCoast sun = Euler(7.5, -90, 0), 28000 lux, EV 8.9 (CretaceaWorldAssembler.cs:883-887) => sun sits dead
  centre of every east-facing crane frame. The lead's capture play_DawnCoast_5s.jpg confirms: letterbox + blown orange sun
  filling the upper frame, flat sky band and a hard horizontal seam where the sea surface ends.

## 2. jump_chapter targets: world state after the jump

Command: `unity command cretacea jump_chapter -- --chapter <name>` (CretaceaWorldAssembler.cs:35, 1588-1608). Aliases: records->Corridor,
dock|paths->ServicePaths, **paddock->Substation**, yard->OldYard, e0->Escape, ending->DawnCoast (CretaceaWorldAssembler.cs:1610-1629).
Play mode (`JumpChapterInPlayMode`, CretaceaWorldAssembler.cs:1631-1693): unpause, clear skip prompt/letterbox (not for Intro),
`StoryDirector.StartFromCheckpoint(spec.Checkpoint, freshPreset:true)`, then teleport to spec.PlayerPos ONLY for Corridor,
ServicePaths, Escape, DawnCoast, then flashlight (`SetCutsceneSuppressed(Intro)`, `SetHasFlashlight(spec.HasFlashlight, autoTurnOn)`).
The spec's AtmospherePreset/Blackout/AuxPower fields are NOT used in Play mode (only Edit mode); the real state comes from
`GameState.ApplyCheckpointPreset` (GameState.cs:345-404) + `ApplyNightFacilityState` (SD:294-357). No jump writes the save file
by itself except Night (SD:593) and OldYard via the reach_yard trigger (below). The play-mode jump does NOT reload the scene: fired
StoryTriggerVolumes stay fired, broken doors stay broken, docs stay consumed.

Light-group shorthand: **N** = VCMain, Plaza, Paths, DockMain, Talon ON; VCEmergency OFF. **B** = those five OFF; VCEmergency ON.
Always ON in both: VCExit, PaddockBattery, SubBattery, YardBattery. Aux = AuxSub+AuxRoute+AuxYard = flag aux_power.

| target -> checkpoint | preset (snap 0 s) | groups | flashlight | player | doors/props/flags | dinos |
|---|---|---|---|---|---|---|
| Intro -> Intro | GoldenDay (cutscene), then Dusk 4 s, NightCalm 4.5 s, then Ch1 | VCMain, Talon ON; Plaza OFF (ON at shot 5b); Emerg OFF; rest N; Aux OFF | none, suppressed | NOT moved (spec (-42,1.5,-92) unused); cutscene cam; Ch1 teleports to start | flags cleared (blackout, collapse, manual_read, aux_power, e0_open); shutter/damage hidden | 2 brachio, 3 trike, intro T. rex (AI off) |
| Night -> Night | NightCalm, lightning rate 0.5 | N, Aux OFF | none (desk pickup) | start (-64.5,0.05,28) yaw 0 pitch -6 | door_security open, door_control closed, door_wing locked, talon_gate closed, CCTV live; save written (checkpoint 1) | none |
| Corridor -> Records | NightStormBlackout, rate 1.0, skylight rain ON | B, Aux OFF | owned, beam ON | SD: spawn_records (-6,0.05,24.5) yaw 140, then CLI (-56,0.05,20.5) yaw 90 pitch -2 | blackout+collapse flags; lobby damage + closed shutter + collapse rubble shown; talon_gate open; CCTV static; door_wing locked UNLESS manual_read survives from earlier state; sub_blocker shown | wing_raptor at (-7,0,-4) |
| ServicePaths -> Dock | NightStormBlackout | B, Aux OFF | owned, beam ON | SD: dock (18,0.05,-10) yaw 90, then CLI (40,0.05,6.5) yaw 0 pitch -2 | + manual_read, master key; path-lamp sparks ON; title "CHAPTER 3" | none until paths_corner2 |
| Substation -> Substation | NightStormBlackout | B; Aux = whatever aux_power was BEFORE the jump | owned, beam ON | no aux: sub_checkpoint (252,2.35,0) yaw 90 / aux: (285,2.35,-4) yaw 90 | no aux: stations reset, objective breakers 0/3 / aux (carried over from an OldYard/Escape/DawnCoast jump or finished run): stations complete, sub_blocker hidden, generator loop, chase in 1.4 s | valley_trex near (300,gy,55) patrol idx 4 / aux: at (258,gy,22) hunting |
| OldYard -> Yard | NightStormBlackout | B + Aux ON (preset MarkTransferComplete) | owned, beam ON | SD: yard_gap+(3,0.05,0) = (404,1.05,-22.8) yaw 90 | gate_box unlocked, hold progress = GameState.E0GateProgress (never reset except New Game); e0 leaves open if e0_open survives; sub_blocker hidden | yard_raptor_a/b; no T. rex |
| Escape -> Yard | as OldYard | B + Aux ON | owned, beam ON | CLI (495.5,1.05,-47.5) yaw 82 pitch -2 (10 m from gate_box) | as OldYard | yard raptors (b patrols near the gate) |
| DawnCoast -> Yard | Yard state, then CLI DawnCoast snap, lightning OFF | **still B + Aux ON** (night lamps/emergency state is not touched) | revoked + suppressed, GameState.HasFlashlight=false | CLI (538,1.05,-46) yaw 90 pitch -2 | rain 0, wetness .35 (StormRainOcclusionController) | yard raptors far west |

Side effects worth knowing before capturing:
- **DawnCoast jump auto-plays the ending.** (538,1.05,-46) is inside the `coast_ending` box (x 534.25..538.75, y 1..6, z -52..-40), so
  the trigger fires on the next physics step (once per Play session) and `DawnCoastEndingRoutine` takes the camera: letterbox, crane,
  fade to black ~16 s, end screen ~24 s (confirmed: shots_base/play_DawnCoast_5s.jpg shows letterbox). A 2nd jump = free FPS view.
- **OldYard jump spawns inside the `reach_yard` box** (x 401.9..404.1) -> `OnReachedOldYard` runs (camera trauma .55 + wall-impact SFX,
  SaveCheckpoint(Yard) writes the save) - likely, not verified live. Escape/DawnCoast teleport again in the same frame, so they do not.
- Substation spawn (252,2.35,0) is inside `enter_substation` (x 249..287): EnterSubstationCompound re-runs (idempotent, no save).
- Corridor: walking into the lobby re-runs the Ch1 lobby/collapse beats (see 6.4).
- Paddock has no jump target: use `StoryDirector.Instance.StartFromCheckpoint(CheckpointId.Paddock, true)` (paddock_entry
  (93.8,0.05,-2.2) yaw 85, valley T. rex at (192,gy,40), title "CHAPTER 4 - TYRANT VALLEY"; SD:999-1008).
- Edit-mode jump (CretaceaWorldAssembler.cs:1695-1800) MUTATES THE OPEN SCENE: moves Player/Main Camera/SceneView, sets
  `sharedProfile` of whichever global Volume FindObjectsByType returns first to Preset_*.asset (Intro=OpeningDusk, Night/ServicePaths/
  Substation=BlackoutStorm, Corridor=InteriorEmergency, OldYard/Escape=PowerRestored, DawnCoast=DawnCoast; :52-56, 82-164), re-aims and
  re-lights KeyDirectionalLight (:1744-1773), toggles Light.enabled per LightGroupMember (:1802-1832). Don't save the scene after it.

## 3. Driving the flow from the editor CLI (script eval)

Play mode only. Namespaces = asmdefs: Cretacea.Core (GameState, GameEvents, WorldRegistry, AtmosphereController,
LightGroupController, enums), .Story, .Player (PlayerController, CameraDirector, FlashlightController), .Interact, .AI (DinoSpawner),
.UI (MenuController). Holds = repeated `OnHoldProgress` then `OnInteract` (Player/PlayerInteractor.cs:168-197), so `OnInteract(null)`
completes any hold instantly; the interactor argument is ignored by story logic.

- Chapter jump / restart: `StoryDirector.Instance.StartFromCheckpoint(CheckpointId.X, true)` (SD:196; true = apply flag preset).
  `StoryDirector.Instance.RestartCurrentCheckpoint()` (SD:271-275; freshPreset=false: Intro/Substation keep current flags, other
  chapters re-apply their preset inside the switch). Neither reloads the scene (triggers stay fired). Full reset like the death
  screen = `MenuController.Instance` private `OnRestartCheckpointClicked` (scene reload) - no public wrapper; alternative:
  `GameState.Instance.SetFlag("launch_from_menu"); UnityEngine.SceneManagement.SceneManager.LoadScene("MainWorld")`.
- Teleport: `PlayerController.Instance.Teleport(new Vector3(x,y,z), yawDeg, pitchDeg)` (Player/PlayerController.cs:600-618; disables CC,
  resets crouch/velocity). Controls: `SetControlEnabled(bool)` (:588). Revive: `ReviveForCheckpoint()` (:649-661).
- Give flashlight: `PlayerController.Instance.Flashlight.SetHasFlashlight(true, true, false)` (Player/FlashlightController.cs:218-242; also
  sets GameState.HasFlashlight). To advance Ch1 as if picked up: `GameEvents.RaiseFlashlightPickedUp()` (GameEvents.cs:156 ->
  SD:642-650 unlocks terminal + objective). Beam: `Flashlight.SetBeamEnabled(bool, silent:true)` (:244).
- Interact by id: `WorldRegistry.Instance.GetInteractable("<id>").OnInteract(null)`. SimpleInteractable ids: `terminal`,
  `control_console` (both singleUse; need `SetEnabledState(true)` + `SetLockedState(false)` if not yet unlocked), `shutter_control`,
  `service_gate_box` (repeatable toasts), `gate_box` (E-0; locked until aux_power; OnInteract = gate fully opens). Docs:
  `doc_memo_switchover`, `doc_manual`, `doc_keeper_note`, `doc_jeep_radio`, `doc_log_halvorsen` (DocumentPickup.cs:38) - paged docs open the
  modal and need `MenuController.Instance.CloseDocumentModal()` once per page (UI/MenuController.cs:242-250); each first read
  writes the save (GameState.cs:209-219). Flashlight desk: `pickup_flashlight` (unregistered once hidden, see 6).
- Substation in order: `GetInteractable("substation_Breaker1"|"substation_Breaker2"|"substation_Breaker3"|"substation_FuelPump")`,
  `"substation_GeneratorStarter"` x3, then `"substation_TransferLever"` (SubstationStation.cs:47, 223-294). Only the TransferLever
  path runs the light ripple + chase (SD:1159-1166). `GameState.MarkTransferComplete()` alone sets aux_power but runs NO lights/chase;
  to see the restored state use it then `StartFromCheckpoint(CheckpointId.Substation, false)` (chase variant) or jump OldYard.
- Story beats: `GameEvents.RaiseStoryTriggerEntered("<id>")` (GameEvents.cs:177) -> SD:1500-1587. Ids + preconditions:
  control_switchover (flashlight && !blackout), lobby_beat / staff_exit_collapse (blackout), dock_crouch_hint, reach_dock, paths_corner2,
  paths_mid, paths_end, enter_paddock, trex_reveal, enter_substation, yard_early_warn (!aux_power), reach_yard, yard_site_map,
  through_e0_gate, coast_ending (= play the ending). Each fires once per StartFromCheckpoint (`_firedBeats`, SD:52, 200).
- Doors: `WorldRegistry.Instance.GetDoor("<id>")` -> `Open(null, true)`, `Close(true)`, `SetLocked(false)`, `SetNormalizedOpen(0..1)`;
  `AnimateOpenTo(t, s)` (Interact/DoorController.cs:122-215). Ids: door_security/lockers/control/server/records/storage(locked)/break/
  wing, staff_exit_door(locked), door_cold/vet/cold_s, dock_roller(jammed), talon_gate, office_door, tool_door, workshop_door_e,
  e0_gate_leaves (Zone1...:424-431,515,721-724; Zone2...:246; Zone3...:432-445,505).
- Checkpoint: in memory `GameState.Instance.ApplyCheckpointPreset(cp)` (no file write); `GameState.Instance.SaveCheckpoint(cp)` WRITES the
  save (GameState.cs:306-312). Flags: `SetFlag/ClearFlag/GetFlag("aux_power")` etc.
- Skip intro: no public API. Hold SkipCutscene 1.15 s, or reflection: set private bool `_skipRequested` (SD:57) on
  StoryDirector.Instance, or just jump Night.
- Lighting by hand: `AtmosphereController.Instance.SetPreset(AtmospherePreset.X, blendSec)` (Core/AtmosphereController.cs:267),
  `.LightningEnabled`, `.LightningRate`, `.TriggerLightningFlash(i01, thunder)`; `LightGroupController.EnsureInstance().SetGroup(
  LightGroupId.X, on, fadeSec, electricalFlicker)` / `IsGroupEnabled(id)` (Core/LightGroupController.cs:83-120). Event equivalents:
  `GameEvents.RaiseAtmosphereChanged(preset, s)`, `RaiseLightGroupChanged(id, on, s)` (GameEvents.cs:173-174).
- Dinos (for calm captures): `Cretacea.AI.DinoSpawner.Instance.DespawnAll()`, `SetSpeciesActive("raptor", false)`,
  `Spawn("trex", pos, yaw, "id")`, `GetActiveDinos()` (AI/DinoSpawner.cs:67-218). There is NO god mode; only T. rex/raptor
  `CanKillPlayer` (AI/DinoControllerBase.cs:789-798) calls `PlayerController.KillPlayer`.
- Fixed camera for captures: `CameraDirector.Instance.SwitchToCutsceneCamera(pos, lookAt, fov)` / `SwitchToFpsCamera()`
  (Player/CameraDirector.cs:166-196). StartFromCheckpoint always switches back to FPS (SD:224).
- Pause: `GameState.Instance.SetPaused(bool)` sets Time.timeScale 0/1 (GameState.cs:134-142) - the only timeScale writer in Scripts.

## 4. Fail states: death, checkpoint restart, game over, end screen

- Death: raptor/T. rex bite (AI/RaptorAI.cs:470-472, AI/TrexAI.cs:441-443) when `CanKillPlayer` (kill range + clear Linecast,
  AI/DinoControllerBase.cs:789-798) -> `PlayerController.KillPlayer` (PlayerController.cs:637-647: controls off, look at attacker, flashlight
  hidden, damage flash 1.0, trauma .95) -> `GameState.RecordDeath` (GameState.cs:290-298: IsGameOver, **save written**) -> death panel
  "YOU WERE KILLED" + RETRY CHECKPOINT / MAIN MENU (MenuController.cs:252-259, 611-628). No slow-mo/death cam; world keeps running.
- Retry checkpoint (death panel or pause menu "RESTART CHECKPOINT", MenuController.cs:297-302, 421, 616): unpause, set flag
  `launch_from_menu`, **reload scene "MainWorld"** (MenuController.cs:312-320). GameState survives (DontDestroyOnLoad), new
  StoryDirector.Start -> `StartFromCheckpoint(CurrentCheckpoint, freshPreset:false)`. Records/Dock/Paddock/Yard re-apply their preset
  in the switch (SD:240-267); Night re-applies in its routine (SD:592); Substation does not, so breaker/fuel/generator progress made
  before death is kept (in memory only; not in the save file).
- "Game over" = the death panel; there are no lives or fail timers. The chase has no fail state besides being bitten.
- End screen: `RaiseGameFinished(stats)` (SD:1493) -> MenuController ending panel with stats text (time, deaths, documents x/5, credits;
  GameState.cs:480-489) and RETURN TO MAIN MENU (MenuController.cs:261-269, 635-652); HUD hides itself (HudController.cs:779-782).
  Player controls are never re-enabled, the cutscene camera stays on the last crane pose, the screen stays faded to black. The
  ending does NOT write the save (last save is the Yard checkpoint or a later doc read), so CONTINUE afterwards resumes in the Yard.
- Main menu (MainMenu.unity): CONTINUE (only if the save exists, MenuController.cs:275-281), NEW GAME (ResetForNewGame + Intro,
  :283-288), CHAPTER SELECT (all 7 CheckpointIds incl. Paddock, always unlocked, :534-577; ApplyCheckpointPreset + load, :290-295).

## 5. Save data: keys and file path

- `Path.Combine(Application.persistentDataPath, "cretacea_save.json")` + `cretacea_settings.json` (Core/SaveManager.cs:47-53); company
  `DefaultCompany`, product `CretaceaBlackoutUnity` (ProjectSettings/ProjectSettings.asset:15-16) => Editor Play AND a Mac build write
  **`~/Library/Application Support/DefaultCompany/CretaceaBlackoutUnity/cretacea_save.json`** (exists: 267 B, 00:55,
  checkpoint 1, flags [], unlocked [0,1]; no settings file yet). No separate test path, no PlayerPrefs. Atomic write via `.tmp`
  (SaveManager.cs:151-166), JsonUtility.
- Keys (SaveManager.cs:8-21): version, checkpoint (int CheckpointId), playTimeSeconds, deathCount, hasFlashlight, hasMasterKey,
  e0GateProgress, flags[], documents[], unlockedCheckpoints[]. Breaker/fuel/generator progress is NOT saved; on load aux_power =>
  MarkTransferComplete, else reset (GameState.cs:454-457).
- Writers: `GameState.SaveCheckpoint` (GameState.cs:306-312) at SD:593 (every Ch1 start, incl. plain Play), :922 manual, :968 reach_dock,
  :1013 enter_paddock, :1109 enter_substation, :1213 transfer, :1295 reach_yard; `AddDocument` (GameState.cs:216); `RecordDeath` (:296).
  SubstationStation and jumps (except Night/OldYard) do not write. CONTINUE appears only if the file exists (MenuController.cs:183).
- Safe test: copy the file aside before Play and back afterwards. `SaveManager.DeleteSave()` (SaveManager.cs:100) - never without asking.

## 6. Fragile / broken spots in the flow

1. **Ch1 soft-lock: desk flashlight can vanish for good.** `FlashlightPickup.OnEnable` hides itself (`SetActive(false)`) when
   GameState.HasFlashlight is already true (FlashlightPickup.cs:65-72,107-111) and `OnDisable` unregisters it (:74-78,
   WorldRegistry.cs:236-240). The Ch1 routine finds it only through the registry (SD:610-613), so `ResetForChapter1` never runs and
   the terminal stays locked ("Grab your tactical flashlight..."). Hits: pause -> RESTART CHECKPOINT in Ch1 after the pickup (scene
   reload with HasFlashlight still true in the DDOL GameState), CONTINUE from a Night save written after the pickup (e.g. memo read),
   and any Play-mode jump back to Night/Intro after picking it up. Code-read, not verified live. Fix: keep a direct ref
   (`FindAnyObjectByType<FlashlightPickup>(FindObjectsInactive.Include)`) or stop unregistering on disable.
2. Reverse case: when HasFlashlight was false at scene load and you jump to a blackout chapter, the desk pickup stays on the desk
   with its pulsing amber LED; using it fires `OnFlashlightPickedUp` -> objective regresses to "Clock in at the security terminal."
   and the terminal -> console path can replay the whole blackout (SD:642-684).
3. `reach_yard` has no aux_power gate (SD:1567-1570) while `yard_early_warn` assumes early arrival (SD:1562-1565): arriving before
   the Substation saves checkpoint Yard; a retry/Continue then applies the Yard preset -> MarkTransferComplete (GameState.cs:395-402)
   = Substation skipped, E-0 unlocked. Also drags the T. rex across the map to the yard wall (SD:1288-1293).
4. Ch1 beats `lobby_beat` / `staff_exit_collapse` re-fire in Ch2 (gate = flag blackout only, SD:1512-1520) after a Records restart or
   Corridor jump: objective regresses, Dana VO replays.
5. Play-mode jumps don't reload the scene: oneShot StoryTriggerVolumes stay fired (no caller of `ResetTrigger`, StoryTriggerVolume.cs:26),
   broken doors stay broken (DoorController.cs:238-249), read docs stay consumed, `manual_read`, `e0_open` and `E0GateProgress`
   carry over (cleared only by Intro/Night presets / ResetForNewGame, GameState.cs:322-363), and aux_power decides which Substation
   variant you get. For a clean beat test: stop/start Play, or raise the beat with `GameEvents.RaiseStoryTriggerEntered`.
6. UI panels survive CLI jumps: MenuController hides the death/ending panels only on `OnCheckpointReached` (MenuController.cs:80-88),
   i.e. when a checkpoint is SAVED. After a death or the end screen, jumping to Corridor/ServicePaths/Substation/Escape/DawnCoast
   leaves "YOU WERE KILLED"/the ending panel (and HUD suppression) over your capture; Night and OldYard (reach_yard) clear it.
7. Atmosphere blends interpolate key-light lux LINEARLY but exposure EV linearly in log space, and lerp sun Euler angles
   (Core/AtmosphereController.cs:325-348); the HDRI swaps instantly at blend start (:319-323). DuskTransition->NightCalm (intro shots
   5b-6: 8000->1.2 lux, EV 7.5->1.2, yaw -68->135) and NightStormBlackout->DawnCoast (ending, 8.5 s: 0.7->28000 lux, EV 0.6->8.9,
   yaw 145->-90) pass through frames several stops over-exposed (mid-blend ~14000 lux at EV 4.75) with the sun sweeping across the sky.
8. Plain Play = Night start = **SaveCheckpoint(Night) overwrites the save** with fresh in-memory state (documents [], unlocked [0,1],
   deaths 0) (SD:124-126, 593). The file on disk now is exactly that (written 00:55), so any earlier owner progress is already gone.
9. Domain reload is DISABLED on Play (ProjectSettings/EditorSettings.asset:29-30 = 1): statics survive Play sessions
   (`SaveManager._cachedSettings` SaveManager.cs:50; `InteriorZoneVolume._activeInteriorDepth`, reset at SD:210). GameEvents +=/-= are
   balanced in every file; `ClearAllListeners` is never called.
10. Coroutines: StartFromCheckpoint stops only the director's coroutines (SD:277-288); light-group fades are safe (SetGroup stops the
   group's transition, LightGroupController.cs:90-114, and facility state sets all 13 groups). No DDOL object runs coroutines, so
   nothing leaks across a scene reload. Only timeScale writer: GameState.SetPaused (GameState.cs:138); death does not pause.
11. `cameraDirector.GlideCutsceneShot` is called without a null check (SD:460); world/lightGroups/atmosphere come from EnsureInstance.
   Intro spec position (-42,1.5,-92) is never applied in Play mode (CretaceaWorldAssembler.cs:88 vs 1660).

## 7. Lighting state per beat (for the look pass)

Only `AtmosphereController` touches exposure (Fixed mode, `fixedExposureEV` per preset; Core/AtmosphereController.cs:407-412);
interiors do NOT change exposure (OnInteriorChanged only drives audio: AudioManager.cs:164, SD:1598-1620). Preset values are
serialized by the builder (CretaceaWorldAssembler.cs:802-919) and only fall back to the code defaults (AtmosphereController.cs:163-254)
if the array has < 5 entries. The two tables differ (GoldenDay EV 11.2/62000 lux/fog mfp 220 vs 11.5/65000/180; DawnCoast
28000 lux Euler(7.5,-90) mfp 480 vs 24000 Euler(9,88) mfp 220). The lead measured mfp 180 (= code default) while the DawnCoast
capture shows the sun in the east (= builder table): check `presets` on [Managers]/[AtmosphereController] live (not verified).

| beat | preset (blend) | groups | lightning | flashlight |
|---|---|---|---|---|
| Intro shots 1-4b (0-38 s) | GoldenDay (snap) | VCMain, Talon, Paths, DockMain, VCExit, 3 batteries ON; Plaza, Emerg, Aux OFF | off | hidden |
| Intro shot 5 | -> DuskTransition 4.0 s | same | off | hidden |
| Intro shot 5b-6 | -> NightCalm 4.5 s; Plaza ON 1.2 s | N | on from shot 6, flash .9 at end | hidden |
| Ch1 until console | NightCalm (snap) | N, Aux OFF, CCTV live | rate 0.5 (flash every 13-32 s) | none -> owned after pickup (beam auto-ON) |
| Ch1 blackout t=9..13 s | NightCalm | VCMain/Plaza/Paths/DockMain fade 1-2 s w/ flicker, Talon at 11.65 s, Emerg ON at 13.05 s | flashes at 8.1, 20.15, 22.35 s | flicker 1.5 s |
| Ch1 after t=13 s -> Ch6 | NightStormBlackout (8.0 s blend in Ch1; snap on any later chapter start) | B, Aux OFF; skylight rain ON; CCTV static | rate 1.0 (flash every 6.5-16 s, peak 14000 lux x .55-1.0; AtmosphereController.cs:120-128, 48) | owned, beam ON at chapter starts |
| Ch5 transfer -> Ch6 | NightStormBlackout (unchanged) | + AuxSub ON .35 s flicker, AuxRoute ripple 0.12 s/light, AuxYard ON .5 s | rate 1.0 | owned |
| Ending | -> DawnCoast 8.5 s | unchanged (B + Aux) | off | beam off, hidden |

Exposure sanity check (HDRP exposure = 1/(1.2*2^EV100); 18 % grey Lambert under E lux -> pixel = 0.18*E/pi/(1.2*2^EV)): GoldenDay
62000 lux @ EV 11.2 -> 1.26 (~2.8 stops over mid-grey 0.18); DawnCoast 28000 @ 8.9 -> 2.8 (~4 stops over); NightCalm moon 1.2 lux @ 1.2
-> 0.025 (~2.9 under); Blackout 0.7 lux @ 0.6 -> 0.022 (~3 under); a lightning peak 14000 lux @ EV 0.6 -> ~440 (pure white). Sunny-16
equivalents would be roughly EV 14.3 (62000 lux) and EV 13.2 (28000 lux). Calculation only, not measured.

Toggled scene objects must be saved ACTIVE so their WorldAnchor registers before caching (SD:160-166): lobby_shutter_closed,
lobby_damage (Zone1...:598,636), staff_exit_collapse (:525), sub_blocker (Zone3...:132), generator (:151). Spark VFX `ShutterSparksVfx`
/ `PathLampSparksVfx` burst 24 particles every 0.45-2.2 s (SD:1699-1715); shutter sparks run in every blackout chapter (SD:345-349).
