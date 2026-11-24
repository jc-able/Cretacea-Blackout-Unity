# Common brief for every look-pass package

You work in **your own clone** of the project with **your own hidden Unity editor**. The owner's project
(`~/CretaceaBlackoutUnity`, the editor with a window) is off limits: never run a command against it.

## Start
```bash
export CLONE=<your clone folder>                 # given in your task
export PATH="$HOME/.unity/bin:$PATH"; export UNITY_NO_BANNER=1; export UNITY_PROJECT_PATH="$CLONE"
bash "$CLONE/Tools/lookdev/clone_editor.sh" start "$CLONE"     # ~30 s; hidden editor, sandboxed saves, 1280x720 game view
git -C "$CLONE" checkout -b look/<package>
```
Read `docs/LOOK_BIBLE.md` (rules, units, materials, commands) and your package brief before editing.
Subsystem maps: `docs/maps_2026-11-20/digest_<area>.md` (issues + how-to) and `notes_<area>.md` (details). Their line
numbers refer to the code before the look pass; trust the code over the notes.

## Work loop
1. Small edit → `unity recompile "$CLONE" --format json` (must be 0 errors, 0 warnings).
2. `unity command cretacea build_all_scenes --bake_navmesh false --timeout 1800` (about 5–20 s) — edit mode only.
3. Look: edit-mode free camera (`cretacea look_preview --preset …` then `cretacea capture --pos … --look_at …`)
   or Play mode (`editor.playmode start`, `cretacea jump_chapter --chapter …`, wait 4 s, `capture game_view`).
4. Read the capture (≤ 1280 px). Fix what you see. Repeat.
- Write a first complete version early and improve in steps; do not plan the whole file in one thought.
- Any command that may run longer than ~90 s: start it in the background and poll with short calls.
- Stop Play mode before recompiling. If the editor dies: `clone_editor.sh start` again. Never touch another PID.
- Check the console for errors after each build and each Play session
  (`unity command console get --level error --tail 20 --format json`).

## Hand-in (required)
1. Final full rebuild **with** NavMesh (`build_all_scenes` without `--bake_navmesh false`) and a Play-mode smoke test:
   start, jump to each chapter your work touches, no new console errors.
2. Capture set in `$CLONE/Screenshots/handin/` (1280×720 PNG, 8–16 files, named `NN_what.png`), same views before
   and after where possible, plus `INDEX.md` (one line per file: what it shows, where the camera is).
3. `docs/handin/<package>.md`: what changed and why, files touched, lamp rules added, requests for the lead
   (changes you need in files you do not own), what you could not verify, known rough edges.
4. `bash "$CLONE/Tools/lookdev/clone_editor.sh" stop "$CLONE"` then
   `git -C "$CLONE" add -A && git -C "$CLONE" commit -m "<package>: <summary>"` on your branch
   (the repo ignores Screenshots/, Library/, scenes are NOT ignored: do **not** stage `Assets/Scenes/*.unity`,
   `Assets/Scenes/*.unity.meta` changes or `Assets/Art/Terrain/CretaceaTerrain.asset` unless your brief says so:
   `git -C "$CLONE" restore --staged Assets/Scenes` before committing).
5. Return the structured result. Be exact about what is verified (you looked at a capture) and what is not.
