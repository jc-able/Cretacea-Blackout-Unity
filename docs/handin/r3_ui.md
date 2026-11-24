# Round 3 hand-in: UI polish (`r3/ui`)

Clone `~/CretaceaUnityClones/ui`, based on ec344ef. Captures: `Screenshots/handin_r3/` (see its `INDEX.md`), raw sets in
`Screenshots/r3ui_before/` and `Screenshots/r3ui_final/`. Capture scripts: `Tools/agent/r3ui_before.sh`,
`Tools/agent/r3ui_after.sh`. Every finding below was first seen in the before capture of this clone.

## Findings, in brief order

1. **Major: the end screen dims the payoff image** (`MenuController.BuildEndingOverlay`, rewritten).
   - The flat 0.46 backdrop is gone. A neutral vertical gradient plate (0.22 at the top under the wordmark, 0.12 across
     the horizon band, 0.30 at the bottom), a 0.28 vignette (was 0.55) and a soft elliptical 0.5 pool behind the card only.
   - The overlay stays invisible for 2.5 s (clean dawn shot, `02_end_hold_clean_after.png`), then fades in over 1 s with
     the card rising 28 px; the RETURN button gets focus only once the card is up (unscaled time).
   - Headline 54 px (stat values 50 -> 42 px): the headline is the largest line on the card. Credits and font licences moved
     to their own card behind a CREDITS button (toggles to STATS; pad/keyboard left-right between the two buttons).
     'Built in Unity 6 HDRP for Isla Tormenta.' is shown as 'Made with Unity 6 HDRP' (string swapped in
     `FillEndingCard`; the source line lives in `GameState.BuildCompletionStatsSummary`, see requests).
   - Card moved from the screen centre (top edge at 440 ref px from the top, tangent to the sun) to the bottom third:
     card top at 596 ref px from the top, buttons 99-161 ref px above the bottom (clear of the cutscene letterbox).
   - Proof: `01` vs `03`, `04`, and the real ending camera from the route test: `25` (review 2) vs `24` (this branch):
     the sun is now a bright disc with its glint, about 130 ref px above the card's top edge. The clone still has the old
     brown dawn and the sun still clips to a pale band at its centre: that part is the lead's DawnCoast key light.
2. **Tablet tabs twitch** (`CretaceaButtonFx`): no scaling for any control not pivoted at its centre (tabs pivot at the
   right edge, map-view and page buttons at an edge) and a new `noScale` flag. Centre-pivoted small buttons keep the 3 %.
   Proof: `07`/`08` (chosen tab keeps the row's 9 px gaps). Not measured pixel by pixel.
3. **No controls reference**: new CONTROLS page (`BuildControlsModal`, public `OpenControls(GameObject returnTarget)`),
   ten rows (move, look, sprint, crouch, interact/hold, flashlight, tablet, pause, skip cutscene, menu select/back),
   keyboard and Xbox columns from the key atlas (text caps of the same style for WASD, MOUSE, sticks, CTRL). Bindings are
   copied from `CretaceaInput`. Reached from the pause menu (new CONTROLS entry) and Settings (CONTROLS button in the
   footer); BACK / Esc / pad B returns to where it came from. Proof: `09`, `10`, `11`, `22`. The menu path was driven
   by invoking the buttons from the shell (no real key presses in the hidden editor): Esc handling is code-read only.
4. **Flashlight chip wording** (`HudController.RefreshFlashlightChipText`): '[F] FLASHLIGHT' always; a lit amber dot with
   a soft halo when the beam is on, a hollow grey ring when it is off. Proof: `17`, `18`.
5. **Subtitles**: wrap 988 -> 780 ref px (box max 860), and `BalancedWrapWidth` finds the narrowest width that keeps the
   same line count, so two-line captions split near the middle. Proof: `17` (06's line, previously 74 chars + 'now!').
6. **Tablet**
   - Unfound documents show 'UNKNOWN DOCUMENT / Not yet found' until their zone's chapter is reached (memo: Night,
     manual: Records, keeper's note: manual read (Feed Prep opens with the key), jeep log: Paddock, Halvorsen: Yard).
     Proof `07`/`08`.
   - MASTER KEY row hidden until `manual_read` (documents row closes up). Proof `05`/`06`.
   - SYSTEMS tab locked before `manual_read`: 'SYSTEMS · LOCKED', not interactable, skipped by left/right navigation,
     `OpenJournal(1)` falls back to the current tab. Proof `06`, `08`.
   - Map (`ParkMapView`): the near-player fade measured `textBounds`, which is empty before a label's mesh exists, so it
     never fired; it now measures the font's preferred size (cached) and fades within 30 px. The target room's label is
     cream + bold on the amber wash (about 6.5:1; near-black ink was tried and would be about 2:1 on this 16 % wash). The
     beacon sits in the lower part of north-wing rooms. STORAGE fits (shrinks to 15 px with tight tracking, floor 13 px),
     VET moved to x 5.4 between the route and the dock wall. YOU dot outline now opaque, about 2 px. Proof `05`/`06`.
   - Not verified in a capture: the tablet after `manual_read` (setting the flag from the shell closed the tablet each
     time, probably a story reaction), so the unlocked SYSTEMS tab and the restored key row are code-read only.
7. **Document transcript**: numbered items use a 3em tab stop (`<pos=3em>`) with `indent=3em / line-indent=-3em`, and the
   reader sets `paragraphSpacing` 12 for typed pages. Proof `23` (wrapped lines align under the item text).
8. **Objective copy** (`ObjectiveGuide`): landmark first, compass second: Control Room 'down the Staff Corridor past the
   Lockers (east)', main entrance 'doors, straight across the Lobby (south end)', Staff Exit 'beside the Gift Shop, at
   the far side of the Lobby (west end)', Records Annex 'at the far end of the Staff Corridor past the Control Room
   (east)', E-0 'the big service gate across the Old Maintenance Yard (east side)'. route_test.py does not read these
   strings: no test change. Proof `21`, `06` (side panel).
9. **Settings and consistency**: brightness fill runs from a centre tick to the knob (`CretaceaUiFactory.MakeSliderBipolar`)
   and reads '+0'; footer has '[Esc] BACK' and the shortened note; pause chapter line in the tablet's amber Barlow
   Condensed 24 px; tablet footer '[Tab / Esc] CLOSE TABLET'; key atlas regenerated with one 40 px legend for every key
   (multi-letter caps got wider instead of smaller type: TAB 74 -> 83 px, ESC 72 -> 80, ENTER, SPACE, VIEW, MENU).
   `docs/handin/ui_tools/make_key_atlas.py` changed accordingly. Proof `12`/`13`, `14`/`15`, `09`.
10. Not done (time): park badge in the pause/tablet headers, panel grain.

## Files
`Assets/Scripts/UI/{MenuController,HudController,JournalController,ParkMapView,ObjectiveGuide,CretaceaButtonFx,CretaceaUiFactory}.cs`,
`Assets/Resources/UI/Keys/key_atlas.{png,json}`, `docs/handin/ui_tools/make_key_atlas.py`, `Tools/agent/r3ui_*.sh`,
this file. Title-card code in HudController untouched. Public signatures unchanged; added `MenuController.OpenControls`,
`CretaceaUiFactory.MakeSliderBipolar`, `CretaceaButtonFx.noScale`.

## Checks
- `unity recompile`: 0 errors, 0 warnings. Play-mode capture runs: 0 console errors; the only warnings are 'Failed to
  create agent because there is no valid NavMesh' from the no-navmesh build used for captures (not from this package).
- route_test.py against the clone after a full rebuild with NavMesh: **45 / 45 passed** in 262 s, no new console errors
  (`Screenshots/r3ui_route/`).
- Perf: no per-frame work added (the subtitle balance runs once per caption, about 12 `GetPreferredValues` calls; label
  extents are cached). perf_here.sh not run (UI only, no world change).

## Requests for the lead
- `GameState.BuildCompletionStatsSummary`: change 'Built in Unity 6 HDRP for Isla Tormenta.' to 'Made with Unity 6 HDRP'
  at the source (the UI swaps the exact string today).
- The ending camera: if the sun moves, the card top is at 596 ref px from the top (the old sun bottom was ~440).
