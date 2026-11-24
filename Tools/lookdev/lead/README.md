# Lead scripts (copies of the session's working scripts)

These ran from the session's scratch folder; the copies here are the record. Paths inside them point at folders on the
original machine (the owner's project in `~/CretaceaBlackoutUnity`, the verification clone in `~/CretaceaUnityClones/vfx`,
Homebrew in `/opt/homebrew`): change them before reuse.

- `lead_pass.sh <tag>`: full pass on the verification clone (recompile, profiles, `build_all_scenes`, doors, capture
  set into `Screenshots/<tag>`, route test).
- `final_build3.sh`: rebuild scenes, build the player on the clone, two self-test runs with the save sandboxed.
- `sync_main.sh [check]`: bring the owner's open project to branch `lead/verify` of the clone. Only when his editor
  answers commands. Never closes the editor.
- `chapter_shots.sh <out>`: the eight same-view chapter frames + a before/after sheet against `Screenshots/baseline`.
- `fl_lab2.sh`, `atmo_lab.sh`, `storm_lab.sh`: measurement labs (flashlight in five dark rooms, day and dawn views,
  storm sky).
- `round4_workflow.js`, `final_review_r4.js`: the multi-agent workflow scripts (six fix packages; twelve critics).
