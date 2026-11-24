# r5 playthrough log: what the bot met, chapter by chapter

The bot (`Assets/Scripts/Dev/PlaythroughBot.cs`, driven by `Tools/lookdev/playthrough.py`) plays from one start
with dinosaurs active: it reads the objective text, finds what the text names in the story's own data (interactables
in `WorldRegistry`, `StoryTriggerVolume`s, anchors), walks there on a NavMesh path by feeding move and mouse-look
through `CretaceaInput.BotOverride`, aims at what it uses and presses or holds Interact through `PlayerInteractor`,
opens closed doors it runs into, crouches in tall grass near the T. rex and under crawl gaps, sneaks (crouched,
light off) when a raptor within 16 m is not hunting, sprints in the chase, reads documents (closes them with
Interact after 2.5 s) and presses RETRY CHECKPOINT on the death screen. No teleports, no direct trigger or
interactable calls. Raw logs: `Tools/agent/pt*_log.txt` (untracked); frames: `Screenshots/handin_r5/`.

Runs (game seconds; the hidden editor runs at about 22 fps):

| Run | Start | Result |
|---|---|---|
| 1-2 | Night | Stuck in the security office: the scene's NavMeshes are for raptor/T. rex and do not fit a 1 m door -> the bot builds its own player NavMesh at runtime |
| 3 | Night | **Stuck at the Control Room door (real blocker, fixed)**, then stale mesh walked into the Lobby shutter (bot fix) |
| 4 (Records) | Records checkpoint | **Lobby beat replayed after the restart, objective regressed (real bug, fixed)** |
| 5-6 (Records) | Records | 2 deaths in Feed Prep, then (with sneaking) Records -> Dock -> Paddock -> Substation -> chase -> Yard -> E-0 with **0 deaths**; **no objective after E-0 (real gap, fixed)** |
| 7-8 | Night | Ch1 clean (Control Room door now passable); Feed Prep dock crawl: 6-7 deaths, then a post-retry path stall. **Not finished** |
| 9 | Substation checkpoint | Substation -> chase -> Yard -> E-0 -> coast road -> dawn ending -> end screen -> main menu -> Continue, 0 deaths |
| 10 | Yard checkpoint | Yard -> E-0 -> ending -> end screen -> Main Menu -> CONTINUE -> pause -> RESTART CHECKPOINT: all worked, 0 deaths |

## Chapter 1 - Night Shift (Night checkpoint)
- Objectives: clear and in order (flashlight -> terminal -> Control Room -> Lobby -> Staff Exit -> Records Annex).
  The bot found every target from the text alone. Time: 141-149 s. Deaths: 0.
- **Blocker (fixed):** the Control Room door is a 1.6 m leaf hinged 95 degrees into the Staff Corridor; the round-4
  utility cart on the opposite wall (x -38.9..-38.3, z 19.1..20.0) leaves 0.60 m between cart corner and leaf tip.
  The player capsule needs 0.68 m, so a player walking from Security cannot reach the room once the door opens
  (route_test teleports past it). Capture 01. Fix: the leaf stops at 72 degrees (`DoorController.SetOpenAngle`,
  applied at chapter start): gap 1.1 m. Capture 02; runs 7-8 pass it (the bot still brushes the leaf for ~20 s
  before it slides round, a human walks round at once).
- **Soft-lock risk (fixed):** one-shot story volumes refused by their precondition stayed spent: walking through the
  huge `lobby_beat` box before the blackout, or into `reach_yard` before Substation 3, made the beat unreachable
  later. Now a refused beat re-arms its volume (`StoryDirector.RearmTrigger`).
- **Order (fixed):** the Staff Exit collapse could fire before the Lobby beat (then Dana sends you to the Staff Exit
  after it has already collapsed). It now waits for the Lobby beat (`lobby_done` flag).
- Prompts all showed (flashlight, terminal, console area trigger, manual).

## Chapter 2 - Records Annex / Feed Prep (Records checkpoint)
- Manual found and read from the objective; master key toast, route objective. Doors opened with the real prompt
  (door_records, door_wing, door_vet, door_cold).
- **Story regression (fixed):** after a Records restart (or Continue) the Ch1 Lobby beat and Staff Exit collapse
  could replay (their guards only checked `blackout`), the objective went back to "Search the Records Annex" and
  the manual, already read, had no prompt. Capture 03. Both beats now require `!collapse`.
- **Difficulty:** Feed Prep killed the bot 2 of 2 times before it learned to sneak, then 0 (run 6), then 6-7 times in
  runs 7-8, almost all at (7.6, -11): the jammed dock roller door, which must be crawled at 1.35 m/s, 8 m from the
  wing raptor's `wing_south` patrol point. `wing_south` is removed from the patrol (the raptor still sweeps hall,
  cold store and vet room). Run 8 still died there, so this needs a human check: the bot keeps crawling toward the
  goal when hunted instead of backing off, which a person would do. Not verified as fixed.

## Chapter 3 - Loading Dock / Service Paths (Dock)
- Objective clear, raptor crossing fired naturally at `paths_corner2`, bush scare at `paths_mid`, gate objective at
  `paths_end`; the bot crouched under the bent gate leaf from the objective's own words. 45 s, 0 deaths.

## Chapter 4 - Tyrant Valley (Paddock)
- Crossed in tall grass crouched with the light off while the T. rex was within 60 m; 77 s, 0 deaths.
- The `enter_substation` box reaches outside the fence: "Substation reached" fires on the west side and the
  breakers are inside, so a player walks round the fence to the gate (natural, no change).

## Chapter 5 - Substation 3
- Breakers 1-3 (hold), fuel pump (hold 4 s), generator starter (three pulls), transfer lever (hold): all found from
  the objective and its counter. 86-145 s, 0 deaths. Chase: survived every time (rubber band + stamina 0.15).

## Chapter 6 - Old Maintenance Yard and the ending
- E-0 found from the objective, held 9 s with the raptors active (0 deaths).
- **Gap (fixed):** after the gate slammed, no objective at all; the ending trigger is 20 m down the road. The bot stood
  there 6 minutes. Capture 04. Now "Follow the coast road east, away from the park." (capture 05) and the ending
  clears the objective as it starts.

## Menus and flow
- Death -> YOU WERE KILLED -> RETRY CHECKPOINT: works (runs 5-8, every death respawned at Records).
- End screen -> RETURN TO MAIN MENU -> CONTINUE (loads Yard) -> Pause -> RESTART CHECKPOINT: works (run 10).
- Not exercised by the bot: main menu NEW GAME with the intro (`playthrough.py --menu` exists, not run), chapter select.

## Not achieved
- The brief's goal 5 (two finishes in a row from one start with dinosaurs active) is **not met**: the full runs from
  Night die repeatedly at the Feed Prep dock crawl; the route from Records to the end was completed in pieces with
  0 deaths (runs 6, 9, 10).
