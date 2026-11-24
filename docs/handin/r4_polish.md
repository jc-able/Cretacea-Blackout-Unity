# Round 4 hand-in: interface and cinematic polish (`r4/polish`)

Base `3987803`. This package was started by another engineer who was cut off by a tool restart. I reviewed their
uncommitted diff (key atlas, HUD chip/hint, caption-in-bar, journal wording, map labels, document spacing, controls
bands), kept all of it (each part checked in a capture below), and did goals 5, 7, 8, 9 and 10 myself.
Captures: `Screenshots/handin_r4/` (see `INDEX.md`). "Before" frames are the lead's `_refs/final` set (same views,
same capture scripts: `Tools/lookdev/capture_set.sh ui|intro`, `route_test.py`).

## Goals

1. **Controls page** (taken over, verified). Every keycap is now drawn by `docs/handin/ui_tools/make_key_atlas.py`
   with one 40 px legend (E, F, C and SHIFT no longer come from the old project art with smaller legends; WASD, MOUSE,
   CTRL and the sticks joined the atlas: one keycap style). The row bands are an opaque (24,22,20) on the (13,13,13)
   card instead of 0.035 alpha white, which the linear-blended UI turned into solid tan (56,51,43): about a quarter
   of the old contrast. Pair `01_controls_*`.
2. **HUD flashlight chip and tablet hint** (taken over, verified). Glow and dot share one centre and the glow is a
   radial falloff sprite (`CretaceaUiFactory.GetGlowSprite`), so the lamp reads as a lit dot with a halo, not an
   eclipse (`02_chip_zoom_pair`, 3x crop). The objective banner is sized top-down (header 40, copy, 14, hint 30, 20 px
   under it), so the TAB / FIELD TABLET hint has the same air under it as above the header (`03_objective_hint_*`).
3. **Document reader** (taken over, verified). Transcript paragraph spacing 12 -> 55 (TMP units are 1/100 em): the
   items 7.1-7.4 now stand apart like the paper beside them (`04_document_reader_pair`).
4. **Tablet** (taken over, verified). In the Staff Corridor the corridor label moves to the half of the corridor away
   from the player and YOU sits beside the dot on the side away from the heading wedge (no ghost 'AFF CORRIDOR', no
   wall line under the plate). LOADING DOCK is top-anchored about 6 px under the dock's wall. The document list keeps
   the title of every known document (it now names the 1996 Preserve Evacuation Manual) and words the missing state
   one way, as a small NOT FOUND tag before the place; the detail panel says NOT FOUND too. The manual's hint steers by
   landmark ("at the far end of the Staff Corridor past the Control Room"), and the footer says how SYSTEMS unlocks.
   Pairs `05_journal_map_*`, `06_journal_documents_*`.
5. **Art direction** (new). `CretaceaUiFactory.DressKitCard` gives the modal cards (pause, settings, controls, chapter
   select, document column) and the field tablet the park-security kit's hardware: a faint worn-paint grain (wrapped
   value noise with speckle and scratches, a few sRGB levels: the UI blends in linear), a pressed-steel bevel (light
   line under the top edge, dark over the bottom), four slotted screw heads turned different ways in the corners.
   Card titles use the game's stencil face (Stardos Stencil, as on the title and end screens) and the park badge sits
   in the top-right corner of each card (left of the title on the tablet). Body text, buttons and colours are
   unchanged, so legibility is untouched. Pairs `07_pause_*`, `08_settings_after`, `09_tablet_after`.
6. **Ending captions** (taken over, verified). In a letterboxed cutscene the sound caption sits centred inside the lower
   black bar with no plate (the bar is the plate); in gameplay it stays above the subtitles. The ending's roar caption is
   now under the picture, clear of the hero boat and the rail (`10_ending_caption_*`); the intro's thunder caption
   uses the same place.
7. **Rail shot** (new). `StoryDirector.SpawnRailShowcaseDinos`: three trikes at 26, 19 and 15 m from the lens, each in
   its own slot right of the brachiosaurus's tail: the far one grazes 3/4 away to the right, the middle one walks away,
   the near one turns 3/4 toward the lens and the brachiosaurus. The brachiosaurus walks 3 m further north and only
   11 m (on its mark 2.5 s into the shot), and the shot-3 lens turns about 10 degrees right and opens 50/48 -> 58/56
   degrees, so the brachiosaurus holds the left third. Staged with a free camera first (three iterations), then checked
   in two Play-mode intro runs at both sampled frames of the shot (08 = start, 09 = end): separate animals, nobody
   behind anybody, about 45 px of air between the tail tip and the nearest trike. Pair `11_rail_shot_*`.
8. **Forked neck in the opening glide** (new). The rail brachiosaurus and the rail trikes are spawned at the cut to
   shot 2 (the facade glide looks the other way), not with the shot-1 animals, so no frame before shot 3 contains them.
   Pair `12_opening_glide_*` (intro_03).
9. **Thunder plate and first gameplay view** (new). The close strike fires 1.4 s into the first night shot
   (`IntroStormFlash`), so the bolt and the flash are in the picture (intro_17 shows the bolt over the roof) and the
   '[THUNDER CRACKS OVERHEAD]' caption plays in the letterbox bar during the shot (intro_18), never over the desk.
   After the cutscene the player looks 15 degrees down (it was 6 degrees UP: Unity pitch is positive-down) at the desk,
   the flashlight and the CCTV monitors instead of the troffer grid. Pairs `13_first_night_shot_after`,
   `14_first_gameplay_view_*`.
10. **Closing glide** (new). Shot 5 opens level on the west sky (sun sinking under the storm deck, the cloud front over
    the hills, 60 degree lens) and swings down to the plaza across shots 5 and 5b while the light goes. Pair
    `15_closing_glide_*` (intro_14).

## Files touched
- `Assets/Scripts/UI/CretaceaUiFactory.cs` (glow sprite; `DressKitCard`, `AddParkBadge`, wear texture)
- `Assets/Scripts/UI/MenuController.cs` (controls bands, transcript spacing, dressed modal cards, stencil titles, badge)
- `Assets/Scripts/UI/JournalController.cs` (document wording, systems footer note, dressed tablet, badge, stencil title)
- `Assets/Scripts/UI/HudController.cs` (chip lamp, objective banner padding, caption in the letterbox bar)
- `Assets/Scripts/UI/ParkMapView.cs` (corridor label and YOU plate in the corridor, LOADING DOCK label)
- `Assets/Resources/UI/Keys/key_atlas.png|json`, `docs/handin/ui_tools/make_key_atlas.py` (one keycap style)
- `Assets/Scripts/Story/StoryDirector.cs`: only `OpeningCutsceneRoutine`, its intro helpers
  (`SpawnOpeningShowcaseDinos`, new `SpawnRailShowcaseDinos`, new `IntroStormFlash`, the intro actor constants) and
  `FinishOpeningCutscene` (first-view pitch). The T. rex reveal routine and `DawnCoastEndingRoutine` are untouched
  (the ending caption fix lives in the HUD).
- No lamp rules, no scene builder, no look-system file.

## Verification
- Recompile: 0 errors, 0 warnings. Play sessions for the UI set, two intro runs and the ending: 0 console errors.
- Final `build_all_scenes` with NavMesh (raptor and T. rex NavMesh baked, no warnings), then Play: UI set 0 errors,
  `route_test.py` **45 / 45** (plays the intro, reads the HUD, plays the ending; "console: no new errors during the
  run"), `verify_playmode_doors` passed: 18/18 doors, 5/5 documents, both NavMeshes valid.
- Perf: not measured. The changes are a few static UI images per modal card (built once), a 256x256 wear texture
  built once, and staging changes in the intro; no lamps, no per-frame work.

## Not done / rough edges
- The flash itself is verified by the bolt in intro_17; its peak (about 0.5 s long) was not caught at its brightest.
- With the caption in the bar, the intro's radio subtitle in the first gameplay seconds still sits over the desk's
  lower edge (standard subtitle position, unchanged).
- The transcript's gap between 'SECTION 7' and item 7.1 is now a blank line plus a paragraph space (slightly roomy).

## Requests for the lead
- intro_17 (not mine: light colour): the three wall packs still read maroon on the dark wall.
- intro_20 (not mine: room props): desk lamp, mug and water cooler are still primitive boxes; they are now nearer the
  centre of the first gameplay view, so they matter more.
- One capture session in this clone showed the viewmodel glove flat cyan for several seconds after an editor restart
  (HDRP async shader compile placeholder); it cleared by itself. Worth a shader warm-up for the built player if it
  ever shows there.
