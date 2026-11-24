# Round 6 package: the T. rex roar and the creatures' last motion defects

Clone: `~/CretaceaUnityClones/creatures`. Branch `r6/trex`.
You own: `Assets/Scripts/AI/**` (TrexAI, DinoControllerBase, DinoProceduralRig, DinoConfig, ParkVisitor,
ParkVisitorCrowd, HerbivoreAI), `Assets/Art/Dinosaurs/**` (clips, import settings), `Assets/Animations/Dinosaurs/**`,
`Assets/Prefabs/Dinosaurs/**`, `Assets/Prefabs/Characters/**`, `Assets/Editor/DinoAnimatorBuilder.cs`. Not
`Assets/Scripts/Story/**`: if a scripted beat needs a new position, timing or call, write it under "Requests for the
lead" with exact values. Keep clip names, bone names and the roar's length (StoryDirector plays `Roar_Massive` for
3.2 s in the valley and 3.5 s in the opening).

Read `docs/review4/motion.md` (and `docs/review5`-era notes in `docs/handin/r5_creatures.md`), then look at
`~/CretaceaUnityClones/vfx/Screenshots/review4/motion/` (intro_trex_sheet, trex_walk_side_sheet, visitors_sheet).
Motion tools: `Tools/lookdev/motion_set.sh` (slowed time-scale frame sequences → MP4 + contact sheet). Headless
Blender for clip work: `/Applications/Blender.app/Contents/MacOS/Blender -b -P <script.py> -- <args>`; the clips were
authored procedurally by the first AI (`Assets/Art/Dinosaurs/dino_manifest.json`).

Goals, in order:
1. **Major — the roar is a rubber-suit pose** (`intro_trex_sheet` f_12–f_26: the head drops to the grass for about
   0.6 s, then snaps to shoulder height in one frame). Make it a predator's roar, by re-authoring `Roar_Massive` or by
   a procedural layer in `DinoProceduralRig`/`TrexAI` during roars: 0.4–0.6 s anticipation (head draws back, never
   lower than the hips), a 25–35° forward-and-up thrust of head and neck with the spine within about 15° of level,
   the jaw wide (40–50°) at the peak with a 0.6–1.0 s hold and a tremor, the tail lifting as a counterweight, weight
   on planted feet (no sliding), then a settle into idle. No single-frame snaps anywhere in the clip. Check it in
   the opening (gallery window shot), in the valley (the reveal and `DelayedTrexInvestigateRoar`) and in a free side
   view.
2. **The jaw hangs open 25–30° in every walking and holding frame** (`trex_walk_side_sheet` minor): nearly closed
   (0–6°) at rest and walking, with a breathing flutter; open only for roars, bites and sniffs.
3. **Nobody at the glass reacts to the roar** (`intro_trex_sheet` minor): visitors flinch, step back or turn their
   heads within 0.2–0.5 s of the roar peak, with varied timing and no two identical.
4. **The sauropod's neck is rigid while it walks** (`visitors_sheet` minor): a gentle counter-sway and bob, if cheap.

Evidence: before/after motion clips and contact sheets for the opening roar, a valley roar, a free side view of the
roar and the walk, and the visitors at the glass; perf at the valley entry with the T. rex in view before and after.
