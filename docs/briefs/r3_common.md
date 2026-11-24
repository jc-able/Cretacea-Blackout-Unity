# Round 3 — common brief (focused fixes after critic review 2)

Review 2 (twelve critics, 106 captures in `Screenshots/review2/`, findings in `docs/review2/<segment>.md`, second
opinions in `docs/review2/<segment>_check.md` when present) scored the game: UI 7, night outdoors 6 and 5, intro 5,
interiors 5, daylight 4 (review 1: 6, 4, 4, 3, 4, 4). Six blockers are left. The lead fixes the look-system ones in
parallel (dawn grade, blackout exposure and flashlight bounce, lightning, reflection probe lag, chain-link veil).
**You fix the content findings listed in your package brief, nothing else.** This is the last round: finish what you
start, leave nothing half-built, and do not regress anything.

Everything in `docs/briefs/_common.md` and `docs/briefs/r2_common.md` still applies (own clone, own hidden editor,
`UNITY_PROJECT_PATH` exported to your clone in every shell, helpers only in `$CLONE/Tools/agent/`, verify each finding
in a capture before fixing, same-view before/after pairs, perf numbers before/after with `Tools/lookdev/perf_here.sh`,
`route_test.py` must stay 45/45 and `verify_playmode_doors` 18/18 if you touch anything a player walks through).

- Branch `r3/<package>` is already checked out in your clone at the lead's master.
- First step after starting your editor: `unity command cretacea build_all_scenes --bake_navmesh false --timeout 1800`.
- The look system stays the lead's (same file list as round 2). Requests go into your hand-in.
- Hand-in: `docs/handin/r3_<package>.md`, captures in `Screenshots/handin_r3/`, commit on `r3/<package>`; do not stage
  `Assets/Scenes/*` or regenerated import settings.
- Free-camera preset captures show two capture artefacts that are NOT game bugs: rain and mist are spawned around the
  player (a free camera far from the player sees seams or none), and a preset forced from the shell keeps leftovers of
  the previous one for a few seconds. Judge weather and mist in Play-mode game views.
- Time box: aim to hand in within about 60 minutes. Do the blockers first.
