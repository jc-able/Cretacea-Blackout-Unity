# Package: CREATURES AND PEOPLE  (branch `look/creatures`)

**Problem.** The four dinosaurs carry the game, and the owner's first request was "fix the animations of the
dinosaurs". Their hides read slightly plastic (blank AO, near-uniform smoothness, stray metallic values, one
material for skin, teeth, claws and eyes, no response to rain), nobody has checked their animation frame by frame
in this build, and the daytime prologue is lifeless: five visitor characters are imported but never placed.
Read `docs/maps_2026-11-20/digest_ui_dinos.md` (dinosaur part) and `notes_ui_dinos.md`.

**You own:** `Assets/Editor/DinoAnimatorBuilder.cs`, `Assets/Art/Materials/Dinosaurs/*`, `Assets/Art/Materials/Characters/*`,
derived texture maps under `Assets/Art/Dinosaurs/*/Textures/`, `Assets/Prefabs/Dinosaurs/*`, new
`Assets/Prefabs/Characters/*`, `Assets/Scripts/AI/DinoProceduralRig.cs`, `Assets/Scripts/AI/DinoAnimationEventRelay.cs`,
new runtime scripts under `Assets/Scripts/AI/` (e.g. wetness, eye glint), `Assets/ScriptableObjects/AI/*`, and in
`Assets/Scripts/Story/StoryDirector.cs` ONLY the actor-spawning part of the opening cutscene (visitors).
Do not touch AI behaviour (`RaptorAI`, `TrexAI`, `HerbivoreAI`, `DinoControllerBase` logic, `DinoSpawner` timing).

## Goals, in priority order
1. **Animation you would not be embarrassed by.** For each species and each clip (see
   `Assets/Art/Dinosaurs/dino_manifest.json`): record a frame sequence in your clone (Play mode, spawn or use the
   chapter where it appears, free camera from the side and the front, 8–12 frames across the clip) and look at it.
   Fix what is wrong where you can fix it here: feet sliding or hovering (ground alignment, foot IK weights), root
   offset, clipping through the ground, jaw not opening on roars, head/neck look-at snapping, stiff tails, pops at
   loop points and blends, wrong playback speed for the locomotion speed. Say exactly which defects remain and why
   (e.g. needs re-skinning in Blender).
2. **Hide that reads as hide.** Per species: a real cavity/AO channel and smoothness variation in the mask map
   (derive from the normal map/height/albedo with Python if no bake exists: scales matte, folds darker, wet
   highlights along ridges), metallic 0, a tiling detail normal for close-ups, subsurface only where it belongs
   (a `_SubsurfaceMaskMap`: teeth, claws, horns, eyes = 0), eyes that catch light (high smoothness island or a
   separate eye material), teeth and claws that look like keratin and bone. Put the values in
   `BuildOrUpdateHdrpMaterial` so a rebuild keeps them. Keep the SSS diffusion profile registered
   (`Assets/Settings/Rendering/SSS_DinoSkin.asset` is already in the look profiles' list).
3. **Wet in the rain.** A small runtime component on each dinosaur: MaterialPropertyBlock-driven wet look
   (raise smoothness, darken albedo slightly) that follows the weather (`Shader.GetGlobalFloat("_GlobalWetness")`,
   set by the atmosphere controller: 0.08 by day, 1.0 in the storm) and dries indoors.
4. **Eyes in the dark.** In the night chapters a raptor's eyes should catch the flashlight (retro-reflective
   glint) — subtle, never a glowing cartoon eye.
5. **People in the prologue.** Prefabs for the park guide and the four visitors with simple Animator controllers
   from their embedded clips; spawn about 16 across the plaza, the viewing rail and the gallery during the opening
   cutscene (positions in the digest), remove them when it ends. Correct heights, feet on the ground, no T-poses.
6. LODs for the dinosaurs if cheap (they are 24–50 k triangles, one mesh each).

## Verify
Frame sequences per clip (keep them in `Screenshots/handin/anim_<species>_<clip>_NN.png`, read only the ones you
need), close-ups in `GoldenDay` and `NightStormBlackout` with the flashlight
(`Cretacea.Player.PlayerController.Instance.Flashlight.SetHasFlashlight(true, true, false)`), the prologue with
people. Useful: `Cretacea.AI.DinoSpawner.Instance` (see `notes_story_flow.md` for spawn hooks),
`unity command cretacea jump_chapter --chapter Intro`. A hunting dinosaur will kill the test player and put a red
death screen over your captures: use free-camera captures (`cretacea capture --pos … --look_at …`) for looks.
