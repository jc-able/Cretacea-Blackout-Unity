# Round 2 — common brief (fix wave after critic review 1)

Six independent critics reviewed the merged game on Nov 20 (79 captures, `Screenshots/review1/`) and scored it
3–6 out of 10. Their findings are in `docs/review1/<segment>.md`. The lead already fixed the look-system findings in
commit `07fd73d` (tone curve with a long highlight shoulder, neutral day white balance, blue night with lifted blacks,
dawn on the horizon, sunset/moonrise transitions, blended chain-link, upper-wall fill lights, bloom). **Your job is the
findings in your area that are about content: geometry, dressing, materials, effects, UI.**

Everything in `docs/briefs/_common.md` still applies (your own clone, your own hidden editor, never the owner's
project, work loop, hand-in), with these changes:

- Branch: `fix/<package>` (already checked out in your clone, based on the lead's master).
- First step after starting your editor: `unity command cretacea build_all_scenes --bake_navmesh false --timeout 1800`
  (a fresh checkout has no generated terrain data until the first build).
- **Keep every helper script you write inside your own clone** (`$CLONE/Tools/agent/`). The scratchpad folder is
  shared between agents; in the first wave two agents overwrote each other's helpers and sent commands to the wrong
  editor. Always pass `--project-path "$CLONE"` or export `UNITY_PROJECT_PATH="$CLONE"` in every shell.
- **Verify before you fix.** For each finding: look at the named capture (`Screenshots/review1/...`, read at most two
  images per Read call), then capture the same view in your clone (the lead's fixes may already have changed it) with
  `Tools/lookdev/capture_set.sh` (see its camera positions), `Tools/lookdev/nightlab.sh` or `cretacea capture`.
  Fix what is still wrong. If a finding is wrong or already fixed, say so in your hand-in with the capture that shows it.
- Priority: blockers, then majors, then minors. Do the visible, cheap wins first; do not spend an hour on one minor.
- The look system is the lead's: do not edit `Assets/Editor/LookPass/LookAtmosphereProfiles.cs`,
  `Assets/Scripts/Core/AtmosphereController.cs`, `Assets/Scripts/Player/FlashlightController.cs`,
  `Assets/Editor/LookPass/LookLights.cs` (the core table), `LookRenderSettings.cs`, `LookPassCli.cs`,
  `WorldMaterialPalette.cs`. If you need a change there, write it under "Requests for the lead".
  You may add `palette.Surface(...)`-style materials from your own files, and lamp rules in your own area table.
- Performance is part of the look: the game now runs at 107–146 fps (editor, 2313x1486). Do not add shadow-casting
  lights without need, per-frame allocations, or thousands of GameObjects. Check `Tools/lookdev/perf_here.sh` before
  and after in one heavy view of yours and report both numbers.
- Useful tools: `Tools/lookdev/frame_stats.py file.png` (clipped %, near-black %, red/blue balance),
  `Tools/lookdev/route_test.py --out Screenshots/route` (the whole story, 38 checks; run it once before hand-in if you
  touched anything a player walks through or interacts with: it must stay 38/38),
  `unity command cretacea verify_playmode_doors` (18/18 doors, 5/5 documents, both NavMeshes).
- Hand-in file: `docs/handin/r2_<package>.md`. Captures: `Screenshots/handin_r2/` with before/after pairs of the
  views the critics named. Commit on `fix/<package>`; do not stage `Assets/Scenes/*`.
- Images: read at most ~30 screenshots in total, never wider than 1280 px, one or two per Read call.

## Addendum (05:45): second opinions
A second critic re-checked every finding: `docs/review1/<segment>_check.md` lists what was **kept** (with measurements
and often the exact file and line that causes it), what was **dropped** (not real or a capture artefact: do not spend
time on those) and what the first critic **missed**. Read the check file for your segments before fixing anything else.
