# CLAUDE.md

Notes for AI coding agents (Claude Code and others) working in this repository. Read `README.md` first: it has the
requirements, controls, story, project layout and known limitations.

## What this is
Cretacea: Blackout, a first-person survival-horror game made with **Unity 6000.6.9f1** and **HDRP 17.6**. It is the
Unity port of the Godot game at https://github.com/jc-able/Cretacea-Blackout-Godot. Open it with exactly this Unity
version: another version upgrades or downgrades the project.

## The scenes are generated: never hand-edit them
- `Assets/Scenes/MainMenu.unity` and `Assets/Scenes/MainWorld.unity` are written by the builders in
  `Assets/Editor/WorldBuilder` (one per zone, plus `CretaceaWorldAssembler.cs`), then `Assets/Editor/LookPass` runs.
- To change the level, change a builder, stop Play mode and run **Cretacea > World > Build All Scenes (MainWorld +
  MainMenu)**. Hand edits to the scenes are lost on the next rebuild. A rebuild also rewrites the generated helper
  files (terrain tiles, foliage LODs and impostors, grass atlases).
- A rebuild does not re-apply render settings or regenerate the atmosphere profiles. After changing
  `LookRenderSettings.cs` or `LookAtmosphereProfiles.cs`, also run **Cretacea > Look > Run Look Pass On Open Scene**
  or **Cretacea > Look > Regenerate Atmosphere Profiles**.
- `MainWorld.unity` (78 MB) and the terrain assets are binary, so Git diffs show nothing. Review the builder change.

## Where the code lives
- `Assets/Scripts/<Folder>` is the game code. Each folder except `Dev` has its own assembly definition
  (`Cretacea.Core`, `Cretacea.Player`, `Cretacea.AI`, `Cretacea.Story`, `Cretacea.Interact`, `Cretacea.UI`,
  `Cretacea.Audio`, `Cretacea.Rendering`). `Dev` (the playthrough bot, its navigation mesh and the build self-test)
  has none. When code in one folder starts using another folder, add the reference to its `.asmdef`.
- `Assets/Editor` is editor-only code: the world builders, the look pass, importers and the `unity` CLI commands.
- Main systems: `Core/GameState.cs` (checkpoint, story flags, objective), `Core/SaveManager.cs` (JSON save and
  settings), `Core/GameEvents.cs` (static event hub), `Story/StoryDirector.cs` (what happens and when),
  `Player/PlayerController.cs`, `AI/DinoControllerBase.cs` with `RaptorAI.cs`, `TrexAI.cs` and `HerbivoreAI.cs`,
  `UI/MenuController.cs`, `UI/HudController.cs` and `UI/JournalController.cs` (all UI is built in code),
  `Core/AtmosphereController.cs` (the five atmosphere presets and the weather), `Audio/SoundBank.cs` and
  `Audio/AudioManager.cs`. The input bindings are defined in code in `Core/CretaceaInput.cs`.

## Testing without touching the player's save
- Saves and settings go to Unity's persistent data folder. For test runs, set the environment variable
  `CRETACEA_SAVE_DIR` to another folder.
- Self-test of a built player: start it with `-cretaceaSmoke <folder>`. It visits every chapter start, writes a
  screenshot per stop and a `report.txt` (frame times, errors) into that folder, then quits
  (`Assets/Scripts/Dev/SmokeTestRunner.cs`). README.md, "Making a build", has a full command.

## Optional: the `unity` command-line bridge
`Packages/manifest.json` includes `com.unity.pipeline`. The editor scripts register commands with its `[CliCommand]`
attribute, so keep the package installed. With the separate `unity` CLI tool and an open editor, an agent can run
`unity command cretacea build_all_scenes`, `unity command cretacea jump_chapter --chapter Night` or
`unity command cretacea verify_playmode_doors`. The CLI's install steps are in the "Install the Unity CLI" section of
`Library/PackageCache/com.unity.pipeline@*/README.md` after the first import. None of this is needed to open, play,
change or build the game.

## Tools and docs
- `Tools/lookdev`: test and capture scripts (Python 3 and bash). Set `UNITY_PROJECT_PATH` to the project folder
  first. `route_test.py` checks every story beat in Play mode; `playthrough.py` runs the playthrough bot.
- `Tools/lookdev/lead` (paths from the original machine) and `Tools/agent` (one-off scripts that code comments point
  to) are kept as records.
- `docs/LOOK_BIBLE.md` has the lighting rules and measured light values; `docs/HANDOFF.md` is the agents' log of
  fixes, measurements and traps. Commit hashes in the docs refer to the private development history.
