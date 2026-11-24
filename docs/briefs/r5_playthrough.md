# Round 5 package: a real playthrough, start to finish, and a story that makes sense

Clone: `~/CretaceaUnityClones/zone1`. Branch `r5/playthrough`.
You own: `Assets/Scripts/Story/**`, `Assets/Scripts/Interact/**`, `Assets/Scripts/Core/GameState.cs`,
`Assets/Scripts/Core/CretaceaInput.cs` (a dev-only bot override; real input must behave exactly as before),
`Assets/Scripts/Dev/**`, new `Tools/lookdev/playthrough.py`, `Tools/lookdev/route_test.py` (only where a story fix
needs a check to change), trigger volumes and story anchors in the zone builders (positions and sizes only), and the
HUD objective / waypoint code in `Assets/Scripts/UI/HudController.cs` and `JournalController.cs`.

The owner asked that "the storyline mechanics and ability to complete the game should make sense". `route_test.py`
proves every beat works when it teleports between them and fires triggers itself; nothing yet proves a player can
walk from the first room to the ending. Goals:
1. **Build a playthrough bot** that plays like a player: one start from the main menu's New Game (or the Night
   checkpoint), then no teleports and no direct trigger calls. It walks along NavMesh paths by feeding the game's own
   move and look input (add a dev-only override to `CretaceaInput` that the game reads instead of the devices while
   set), looks at what it uses, uses the real interaction (`PlayerInteractor` raycast plus Interact press or hold),
   crouches in the tall grass, switches the flashlight, reads the documents the story needs, sprints in the chase.
   Dinosaurs stay active; offer a passive-dinosaur option only to isolate a path problem. It decides where to go
   from the story's own data (objective, anchors, interactables), as a player follows the objective text.
2. **Record for every chapter**: did the objective say what to do and where; was the target reachable on foot;
   did the trigger or interaction fire naturally; time taken; deaths; stuck points (no progress for 20 s); doors
   that blocked; prompts that did not show; anything a first-time player would not understand. Write it to
   `docs/handin/r5_playthrough_log.md` with frames of each problem.
3. **Fix what blocks or confuses**: objectives and their wording, waypoints or the route map, door states, trigger
   volumes, interaction prompts and ranges, checkpoint and death/respawn flow, stealth and chase tuning that is
   unfair or broken, story text that contradicts what the player sees. Read every objective, subtitle, document and
   journal entry in order and make the story consistent (who you are, why you go where you go, what the next step
   is). Keep the story; fix its logic.
4. **Menus and flow**: death and restart at each checkpoint, pause → restart checkpoint, main menu → Continue and
   chapter select, the ending → end screen → main menu.
5. Run the bot until it finishes the game twice in a row with dinosaurs active, and keep `route_test.py` 45/45.
