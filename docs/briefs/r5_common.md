# Round 5 — common brief (the owner's new request: hand, animations, a game you can finish, realism)

The owner (JC) wrote, word for word, after round 4:
> And also fix the hand and all animations as well. It should look like a professional triple a game and the
> storyline mechanics and ability to complete the game should make sense as well. It should be ultra realistic looking

That is the bar for this round: what a player sees in motion and what a player can actually do, not only still
frames. Four packages run in parallel: the first-person hand and flashlight (`viewmodel`), every creature and person
in motion (`creatures`), a real start-to-finish playthrough with the story and mechanics fixed where they break or
confuse (`playthrough`), and the island's remaining vegetation problems (`island2`).

Everything in `docs/briefs/_common.md`, `r2_common.md`, `r3_common.md` and `r4_common.md` still applies (own clone,
own hidden editor started with `Tools/lookdev/clone_editor.sh`, `UNITY_PROJECT_PATH` exported to your clone in EVERY
shell that calls unity or a Tools/lookdev script, helpers only in `$CLONE/Tools/agent/`, verify before you fix,
same-view before/after evidence, the look system stays the lead's, no new packages, no downloads, no paid
generation), with these changes:

- Branch `r5/<package>` is already checked out in your clone at the lead's commit. First step after starting your
  editor: `unity command cretacea build_all_scenes --bake_navmesh false --timeout 1800` (use the NavMesh build,
  without the flag, before route tests and before hand-in).
- **Motion needs motion evidence.** A still frame cannot show foot sliding, popping or a stiff hand. For every
  animation claim, capture a frame sequence in Play mode (for example 12-24 Game-view frames at 0.08-0.15 s
  intervals) and assemble it with ffmpeg (`/opt/homebrew/bin/ffmpeg`) into a short MP4 (1280x720, under 15 s) plus a
  contact-sheet JPEG of 6-8 frames. Look at the contact sheets yourself before you claim anything. Put both in
  `Screenshots/handin_r5/` and list them in its `INDEX.md`.
- Blender is available headless for model and clip work: `/Applications/Blender.app/Contents/MacOS/Blender -b -P
  <script.py> -- <args>` (keep scripts in `$CLONE/Tools/agent/`). Sources outside your clone are read-only; convert
  into your clone's `Assets/`.
- Never edit a script on disk while Play mode runs in your editor. Storm and blackout frames come from a blackout
  checkpoint. Saves are sandboxed by `clone_editor.sh`; keep it that way.
- The game must still pass before hand-in: zero compile errors and warnings, `Tools/lookdev/route_test.py` 45/45 on a
  NavMesh build (if your package changes the story so a check must change, change the check and say why), and
  `unity command cretacea verify_playmode_doors`. Report `Tools/lookdev/perf_here.sh` before and after in your
  heaviest view: the built player runs chapter 1 at about 73 fps and outdoor chapters at about 115 fps.
- Hand-in: `docs/handin/r5_<package>.md` (each goal: what you did, the evidence that proves it, what is not done and
  why; then "Requests for the lead"), captures in `Screenshots/handin_r5/` with `INDEX.md`, commit on `r5/<package>`
  without `Assets/Scenes/*` and without regenerated materials you do not own (`git status` before you commit; revert
  files outside your ownership).
- Time box: about 75 minutes. A finished, verified subset beats a broken whole.
