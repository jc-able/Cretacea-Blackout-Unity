# r5/playthrough hand-in: a playthrough bot, and the story fixed where it broke

Detailed per-chapter findings: `docs/handin/r5_playthrough_log.md`. Captures and motion: `Screenshots/handin_r5/`
(INDEX.md).

## Goal 1 - playthrough bot (done)
- `Assets/Scripts/Core/CretaceaInput.cs`: dev-only `BotOverride` (static, null by default). While it is set, every
  input property reads it instead of the devices; requests are latched once per frame so "pressed this frame" lasts
  exactly one frame for every reader, like a key. When null, every property runs the exact expression it had before.
- `Assets/Scripts/Dev/PlaythroughBot.cs`: the bot. Reads the objective text, maps it to an interactable / story
  trigger / anchor from the story's own data, walks NavMesh paths with move + mouse-look input, side-steps along
  obstacles the mesh does not know, opens closed doors with the real prompt, crouches under low ceilings, in tall
  grass near the T. rex and when a raptor is close but not hunting (light off), sprints in the chase, reads and
  closes documents with Interact, presses RETRY on death, and after the end screen runs Main Menu -> CONTINUE ->
  Pause -> RESTART CHECKPOINT. Logs objectives, chapters with times, deaths, stuck points (20 s), missing prompts
  (4 s aimed, no focus), locked/blocked doors, unknown objectives.
- `Assets/Scripts/Dev/PlayerNavMesh.cs`: a runtime NavMesh for a person (radius 0.34, crouch height 1.1 m) over the
  route, built async in 0.2-0.6 s and rebuilt when doors open/close, after each objective change (beats switch the
  shutter/collapse/fence) and after a retry. Needed because the baked meshes are raptor/T. rex sized and do not fit
  through a 1 m door.
- `Tools/lookdev/playthrough.py [--menu] [--passive-dinos] [--checkpoint X] [--timeout s] [--frame-every s]`:
  starts Play, starts the bot, polls it, captures a Game-view frame on every problem and periodically, stops at the
  end screen. `--checkpoint` is for development only (the brief's run starts from Night or the menu).

## Goal 2 - record per chapter (done): `docs/handin/r5_playthrough_log.md`

## Goal 3 - fixes (StoryDirector, DoorController, GameState)
1. **Control Room unreachable on foot** (blocker): the 95-degree corridor-side leaf + utility cart left 0.60 m.
   New `DoorController.SetOpenAngle(float)`; chapter 1 sets door_control to 72 degrees (1.1 m). Captures 01/02.
2. **Spent one-shot volumes** (soft-lock risk): a beat refused by its precondition (lobby before the blackout,
   switchover without the flashlight, yard before aux power, staff exit before the lobby) re-arms its volume.
3. **reach_yard without aux power** saved the Yard checkpoint early (retry skipped Substation 3, E-0 dead): gated on
   `aux_power` (re-armed).
4. **Staff Exit before the Lobby**: waits for the Lobby beat (`lobby_done` flag; cleared at Night, set at Records+ in
   `GameState.ApplyCheckpointPreset`).
5. **Ch1 beats replaying after a Records restart / Continue** (objective regressed, manual unusable): both beats
   require `!collapse`. Capture 03.
6. **No objective after E-0**: "Follow the coast road east, away from the park." 1.2 s after the slam; the ending
   clears the objective. Captures 04/05.
7. **Feed Prep dock crawl**: wing raptor patrol no longer includes `wing_south` (8 m from the crawl gap). Not proven
   to be enough (see below).

## Goal 4 - menus and flow (mostly verified)
Death -> RETRY CHECKPOINT (many times), End screen -> Main Menu -> CONTINUE -> Pause -> RESTART CHECKPOINT (run 10,
capture 10 and m02). Not run: NEW GAME from the menu with the intro, chapter select.

## Goal 5 - two finishes in a row (NOT met)
Every segment was completed by the bot on foot with dinosaurs active: Night -> Records (runs 7, 8: 0 deaths in Ch1),
Records -> Yard -> E-0 (run 6: 0 deaths), Substation -> ending -> menus (run 9), Yard -> ending -> menus (run 10).
But the full runs from Night died 6-7 times at the Feed Prep dock crawl and were stopped; no single start reached the
end. The bot's weakness there: when hunted it keeps crawling toward the goal instead of backing off.

## Checks
- `unity recompile`: 0 errors, 0 warnings. Final `build_all_scenes` with NavMesh (raptor + T. rex baked).
- `route_test.py`: **45/45** (254 s) on the NavMesh build, no new console errors.
- `verify_playmode_doors`: 18/18 doors, 5/5 documents, flashlight OK, both NavMeshes valid.
- Perf: not measured (`perf_here.sh` not run); nothing rendered changed. The bot's runtime NavMesh exists only while
  the bot runs.

## Files touched
`Assets/Scripts/Core/CretaceaInput.cs`, `Assets/Scripts/Core/GameState.cs`, `Assets/Scripts/Interact/DoorController.cs`,
`Assets/Scripts/Story/StoryDirector.cs`, new `Assets/Scripts/Dev/PlaythroughBot.cs`, `Assets/Scripts/Dev/PlayerNavMesh.cs`,
new `Tools/lookdev/playthrough.py`, docs. No builder, scene, HUD or route_test change.

## Requests for the lead
- Feed Prep: a human should play Records -> Dock once. If the dock crawl is still a death trap, the fix belongs in
  the raptor AI (hearing/sight while the player crawls) or in moving the crawl gap's approach out of the patrol's
  sight line (Zone1 builder), not in my files.
- Consider making all Staff Corridor doors swing into their rooms (`BuildFramedDoor(... primaryOpenAngleDeg: -95)`
  in Zone1_VisitorCenterBuilder): every corridor door currently swings 95 degrees into a 2.9 m corridor; only
  door_control met a prop, and I worked round it at runtime.
- The ranger jeep log says 00:04 although the blackout starts about 23:10 and the player reaches the valley well
  before midnight; the scanned page image probably carries the time too, so I left it.

## Not verified / rough edges
- Goal 5 as above. The bot brushes the Control Room leaf for ~20 s before it slides past (a human walks round it).
- After a retry in Feed Prep the bot once stalled on a path for 20 s (logged STUCK) before recovering.
- Main-menu NEW GAME start (`--menu`) and chapter select were not run.
