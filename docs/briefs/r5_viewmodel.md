# Round 5 package: the first-person hand and flashlight

Clone: `~/CretaceaUnityClones/ui`. Branch `r5/viewmodel`.
You own: in `Assets/Scripts/Player/FlashlightController.cs` ONLY the viewmodel motion (`UpdateViewmodelMotionAndPullback`
and new helpers it calls; do not touch the beam, the bounce, the hot-spot budget, the cookie, `ConfigureHdrpLights` or
the viewmodel fill light), `Assets/Scripts/Player/ViewmodelGripOffset.cs`, new scripts under `Assets/Scripts/Player/`,
`Assets/Art/Props/Flashlight/**`, `Assets/Animations/Player/**`, `Assets/Art/Materials/Props/Flashlight/**`, the
player-rig block of `Assets/Editor/WorldBuilder/CretaceaWorldAssembler.cs` (where `vmRoot`, `swayPivot` and the
flashlight lights are built and placed, around lines 470-580), the flashlight section of `LookMaterials.cs`, and the
flashlight pickup on the desk (`Assets/Scripts/Interact/FlashlightPickup.cs` and its model) so it matches the held
torch.

Today the held arm (`flashlight_fps_rig.fbx`, made by the first AI) reads as a lumpy mitten pointing forward; the torch
is hidden inside the fist except for its lens. The owner asked to "fix the hand". Goals:
1. **A hand that looks like a shipped AAA horror game's** (RE7, Alien: Isolation, The Last of Us at night): a gloved
   right hand with readable fingers wrapped round a recognisable tactical flashlight (barrel, head, tail switch), a
   sleeve or cuff running to the screen edge so nothing ends in mid-air, believable scale for a 68° FOV, and a pose
   that holds the torch forward at chest height to the right of centre. Candidate source, already generated and paid
   for (read-only): `~/CretaceaBlackout/assets/models/viewmodel/flashlight_hand/flashlight_hand.glb` + its Color,
   NormalGL and ORM textures (Tripo; notes: "Dark gloved hand gripping a long black tactical flashlight, whole barrel
   visible, cuff at the wrist ... rotate so the lens points down the camera's forward axis and the forearm comes back
   toward the camera; handedness not verified (mirror X if it reads as a left hand) ... 0.46 m long as generated").
   Convert it with headless Blender (decimate if it costs too much; keep the silhouette), add a sleeve that continues
   the cuff, or improve the current rig if that turns out better. Decide by looking at both in the Game view.
2. **Materials**: leather or nylon glove with sheen and seams that read under the torch spill and in lit rooms, matte
   rubber grip and anodised aluminium on the torch, a glass lens with the LED behind it. The beam must leave from the
   lens (move `FlashlightSpotLight` with the model). Keep every viewmodel renderer on rendering layer 5 as well
   (`FlashlightController.ViewmodelFillLayer`: the arm's own fill light) and with decals off.
3. **Animation that feels alive** (spring-damper, never jittery): idle breathing, walk and sprint bob with a little
   rotation, crouch offset, look sway with lag, a small dip on each footstep, the thumb press on the toggle, the
   existing equip raise from below, lowering while the tablet or a document is open and while holding an interaction
   (levers, generator), a slight tremor when a predator is near (`SetNearestPredatorDistance`), and the existing
   wall pull-back. Show each in a motion capture (see r5_common.md).
4. The torch on the security office desk (the pickup) is the same model as the one in the hand.
Evidence: lit office, dark corridor with the beam on, outdoor rain, sprint, crouch, document open, wall pull-back,
near a raptor; before/after stills and motion clips. Perf in the security office before and after.
