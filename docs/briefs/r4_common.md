# Round 4 — common brief (focused fixes after critic review 3)

Review 3 (twelve critics, 113 captures; findings in `docs/review3/<segment>.md`, second opinions in
`docs/review3/<segment>_check.md`) scored the game: UI 8, daylight 6.5, intro 6.5, night outdoors 6 and 6.5,
interiors 6 (review 2: 7, 4, 5, 6, 5, 5). No blocker is left. 23 majors and 61 minors are. After the captures were
taken the lead already fixed nine majors (commit `3fb4102`: herd out of the closing aerial, T. rex head clear of the
mullion, barred E-0 gate leaves, locker duffels and boots, end-screen title pool, stronger dock battery lamp, cloud
sun dimmers, umbrella crown caps, no floor decals on the flashlight arm).
**You fix the findings listed in your package brief, nothing else.** Leave nothing half-built and do not regress
anything: this build already passes the whole story test and ships as a working player.

Reference captures (read-only, outside your clone):
- `~/CretaceaUnityClones/_refs/review3/**`: the frames the critics saw (the file names in the findings).
- `~/CretaceaUnityClones/_refs/final/**`: the same views after the lead's quick fixes. This is your starting point.

Everything in `docs/briefs/_common.md`, `r2_common.md` and `r3_common.md` still applies (own clone, own hidden editor,
`UNITY_PROJECT_PATH` exported to your clone in every shell, helpers only in `$CLONE/Tools/agent/`, same-view
before/after pairs, the look system stays the lead's), with these changes:

- Branch `r4/<package>` is already checked out in your clone at the lead's commit.
- First step after starting your editor: `unity command cretacea build_all_scenes --bake_navmesh false --timeout 1800`.
- Read the `_check.md` file of your segments before anything else: it lists what was kept (with measurements and
  often the file and line that causes it), what was dropped (do not spend time there) and what was missed.
- Verify before you fix: capture the view in your clone first. The lead's quick fixes may already have changed it.
  If a finding is already fixed or wrong, say so in your hand-in with the capture that shows it.
- Order: the majors in your brief, then its minors, cheapest visible win first. Do not spend an hour on one minor.
- Never edit a script on disk while Play mode runs in your editor (a reload in the middle of Play breaks the
  singletons and gives false errors and false frame rates). Stop Play, edit, recompile, rebuild, play again.
- Storm and blackout frames must come from a blackout checkpoint (`StartFromCheckpoint(CheckpointId.Records, true)`
  or later), not from `SetPreset(NightStormBlackout)` on a powered park. Rain, mist and the rain curtains are placed
  around the player: judge weather in Play-mode game views, not from a free camera far from the player.
- The game must still pass before hand-in: `Tools/lookdev/route_test.py` 45/45, `unity command cretacea
  verify_playmode_doors` (18 doors, 5 documents, both NavMeshes valid), zero compile errors and warnings, no new
  console errors.
- Performance is part of the look. The built player runs chapter 1 at 73 fps and the outdoor chapters at 115 fps
  (1920x1080). No new shadow-casting lamp without need, no per-frame allocations, no thousands of GameObjects.
  Report `Tools/lookdev/perf_here.sh` before and after in your heaviest view.
- Hand-in: `docs/handin/r4_<package>.md` (each goal: what you did, the before/after pair that proves it, what is not
  done and why; then "Requests for the lead"), captures in `Screenshots/handin_r4/` with an `INDEX.md`, commit on
  `r4/<package>`; do not stage `Assets/Scenes/*` or regenerated import settings.
- Time box: aim to hand in within about 70 minutes. A finished, verified subset beats a broken whole.
