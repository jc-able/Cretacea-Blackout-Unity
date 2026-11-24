# Round 6 — common brief (review 4's open majors)

The owner's goal for the whole job, word for word:
> Go ahead and complete the task and make this game look amazing. Right now, the colors are off. It doesn't look
> right. We need it to be perfect. Don't stop until it's completely done.

and after round 4:
> It should look like a professional triple a game and the storyline mechanics and ability to complete the game
> should make sense as well. It should be ultra realistic looking

State: review 4 (`docs/review4/<segment>.md`, captures in `~/CretaceaUnityClones/vfx/Screenshots/review4/`, read-only)
scored the final state UI 8.5, intro 7–7.5, daylight 7, interiors 7, night a 7, night b 7, playability 7, motion
5.5–6: 0 blockers, 16 majors. Fixed since (in your base commit): khaki dawn sea, glove lit from above, torch tail cap,
stealth hint, T. rex turn rate (60°/s), slate day sea, dead CCTV bank, records beacon leak, the storm moon's clipped
glint on wet ground (angular diameter 14°), the blackout corridor capture artefact (`capture_set.sh` order). Round 6
takes the rest in four packages: `trex` (the roar and the creatures' last motion defects), `rooms` (interiors),
`valley` (the valley and the storm at night), `island` (the island from the air and at the ending).

Everything in `docs/briefs/_common.md`, `r2_common.md`, `r3_common.md`, `r4_common.md` and `r5_common.md` still
applies (own clone, own hidden editor started with `Tools/lookdev/clone_editor.sh start <clone>`, `UNITY_PROJECT_PATH`
exported to your clone in EVERY shell that calls unity or a Tools/lookdev script, helpers only in
`$CLONE/Tools/agent/`, verify before you fix, same-view before/after evidence, no new packages, no downloads, no paid
generation, motion evidence for motion claims), with these changes:

- Branch `r6/<package>` is already checked out in your clone at the lead's commit. First step after starting your
  editor: `unity command cretacea build_all_scenes --bake_navmesh false --timeout 1800`; use the NavMesh build
  (without the flag) before route tests and before hand-in.
- **Capture the BEFORE frames first**, from the same positions as review 4 where a finding names one
  (`Tools/lookdev/capture_set.sh`, `nightlab.sh <name> <checkpoint> x y z yaw pitch on|off <outdir>`, `motion_set.sh`,
  `unity command cretacea capture --pos x,y,z --look_at x,y,z ...`). Pitch: positive looks DOWN.
- **Night frames between strikes:** `ev 'Cretacea.Core.AtmosphereController.Instance.HoldLightningForCapture(0f); return "hold";'`
  (release with `-1f`). A checkpoint start re-applies the atmosphere preset, so set any runtime light property AFTER
  it.
- **Long commands go to the background**: a tool call that blocks for more than ~2 minutes gets the agent killed.
  Run `route_test.py`, NavMesh builds and motion captures with `&` and a log file, then poll the log with short calls.
- Images: at most ~25 Reads in total, never wider than 1280 px, contact sheets first.
- Never edit a script on disk while Play mode runs in your editor. Stop a process only by a PID you started; never
  `pkill`/`killall`. Never run anything against the owner's project `~/CretaceaBlackoutUnity` or launch a built game.
  Never run the scripts in `Tools/lookdev/lead/` (they hard-code the lead's clone); read them for positions only.
- Do not change values the lead set in round 6's base unless your brief says so: the sea colours in
  `LookTerrainWater.cs`, the storm moon in `AtmosphereController.GetKeyLook`, the flashlight budget statics.
- The game must still pass before hand-in: zero compile errors and warnings, `Tools/lookdev/route_test.py` 45/45 on a
  NavMesh build, `unity command cretacea verify_playmode_doors` (18/18 doors, 5/5 documents), no new console errors.
  Report `Tools/lookdev/perf_here.sh` before and after in your heaviest view (built player: chapter 1 about 73 fps,
  outdoor chapters 80–115 fps); do not lose more than 5 %.
- Hand-in: `docs/handin/r6_<package>.md` (each goal: what you did, the evidence, what is not done and why; then
  "Requests for the lead" with exact values), captures in `Screenshots/handin_r6/` with `INDEX.md` (same-view pairs
  named `<view>_before.png` / `<view>_after.png`), commit on `r6/<package>` without `Assets/Scenes/*` and without
  regenerated materials you do not own (`git status` before you commit; revert files outside your ownership).
- Time box: about 75 minutes. A finished, verified subset beats a broken whole.
