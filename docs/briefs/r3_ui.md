# Round 3 package: UI polish (review 2: UI 7/10, one major, a dozen minors)

Clone: `~/CretaceaUnityClones/ui`. Branch `r3/ui`.
You own: `Assets/Scripts/UI/**` except the title-card code in `HudController.cs` (the intro package is changing where
title cards sit this round: do not touch `OnTitleCard`/title-card layout), `Assets/Art/UI/**`, `Assets/Resources/UI/**`.
Do not touch story, lighting or scene builders. Requests for those go into your hand-in.

Read `docs/review2/ui.md` and `docs/review2/ui_check.md` (the second opinion: what was kept, corrected and added;
if it is not in your clone read it at `~/CretaceaBlackoutUnity/docs/review2/ui_check.md`).
The lead has already rebuilt the dawn look (blue sky, gold clouds, pale sea, soft sun) and the ending now fades
through black into a settled morning; your clone still shows the old brown dawn. Judge your end screen for layout and
plate, not for the colour of the scene behind it.

Goals, in order:
1. **Major — the end screen dims the payoff image.** `MenuController.BuildEndingOverlay` uses
   `CreateBackdrop(root, "EndingRoot", 0.46f)` plus a 0.55 vignette: clipped highlights turn into dead grey and the
   morning becomes a dusk. Use a neutral gradient/radial plate: about 0.15-0.25 over the sky and the sea, about 0.5 only
   behind the stats card, vignette at most 0.3. Hold the clean dawn shot for about 2.5 s before the card comes in
   (fade/slide in). Make the headline the largest thing on the card (today 30 ref px, smaller than the stat values),
   move the credits and font licences off the stats card (a CREDITS button or a small roll below), reword
   'Built in Unity 6 HDRP for Isla Tormenta.' to 'Made with Unity 6 HDRP'. Keep the card clear of the sun (the sun sits
   right of centre near the horizon line; the card top edge was tangent to it).
2. **Tablet tabs twitch**: `CretaceaButtonFx` scales small buttons 3 % on hover/selected; the tabs are pivoted at an
   edge, so the row jumps 6 px. No scaling for tabs (or for anything not pivoted at its centre).
3. **No controls reference anywhere.** Add a CONTROLS page (keyboard and gamepad, from the key-glyph atlas the HUD
   already uses) reachable from the pause menu and the settings; the game supports an Xbox controller.
4. **Flashlight chip wording**: '[F] LIGHT ON' reads as an instruction. Name the action and let a dot/icon carry the
   state ('[F] FLASHLIGHT' with a lit amber icon vs a grey one). `HudController.RefreshFlashlightChipText`.
5. **Subtitles**: wrap at about 760-800 ref px and balance two-line captions (no orphaned last word).
6. **Tablet**: unfound documents show 'UNKNOWN DOCUMENT' without their location until the player has reached that
   zone (today the list spoils the T. rex valley and the route); hide the MASTER KEY row until the manual has been
   read; lock/dim the SYSTEMS tab until `manual_read` instead of a mostly empty page; map labels: the highlighted
   room's label needs at least 4.5:1 on the amber fill, the YOU dot must not cover a room label (fix the near-player
   fade), STORAGE and VET must fit their rooms, a 1-2 px dark outline round the YOU dot.
7. **Document reader transcript**: hanging indent 3em (it is 2.6em: wrapped lines start one character left), a little
   paragraph spacing between numbered items.
8. **Objective copy**: lead with what the player can see, keep the compass word as the second clue
   ('...in the Records Annex, at the far end of this corridor past the Control Room').
9. **Settings**: the brightness slider is bipolar: fill from the centre tick, show '+0'; add 'ESC BACK' footer.
   Consistency: the pause card's chapter line in the tablet's amber Barlow Condensed style; 'TAB / ESC' with a
   separator; multi-letter keycaps wide enough that the glyph stays near 60 % of the chip height.
10. If time is left: the park badge or stencil wordmark in the pause and tablet headers, faint paper/CRT grain on panels.

Verify every change in a Play-mode capture at 1280x720 (`Tools/lookdev/capture_set.sh Screenshots/x ui`, plus your own
for the end screen: `Cretacea.Core.GameEvents.RaiseGameFinished("03:20|0|1/5")` or the real ending via the route
test) AND with a gamepad-less keyboard pass through pause -> settings -> controls -> back. `route_test.py` must stay
45/45 (it checks HUD/objective strings: if you change objective copy, update the test's expectations only where the
test reads the exact text, and say so in the hand-in). Hand-in: `docs/handin/r3_ui.md`, captures in
`Screenshots/handin_r3/`, commit on `r3/ui` without the generated scenes. Time box: about 60 minutes.
