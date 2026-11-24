# Round 2 hand-in: UI (journal, objectives, map)

Branch `fix/ui`, clone `~/CretaceaUnityClones/ui`, base `8ed04fa`. Captures: `Screenshots/handin_r2/` (see `INDEX.md`
there; `before/` holds the same views at the base commit). All captures 1280x720 from the clone's hidden editor in
Play mode (`capture game_view`), checked by eye.

## What changed, finding by finding (docs/review1/ui.md + ui_check.md)

| # | Finding | Status | Proof |
|---|---|---|---|
| 1 | Documents tab: flat beige card, debug rows `1. [UNCOLLECTED - ...]` | **Fixed.** Dark card like every other card. Rows are two lines with a page icon: title (or `NOT FOUND`) in label caps, location hint below (`Security Office, on the desk`). Missing: faint page outline + `NOT YET RECOVERED` + one hint line. Recovered: the real page art (the reader's paper) with a drop shadow, `RECOVERED · <place>`, title, transcript in Courier Prime Bold. No brackets anywhere. | 05/06, 07 |
| 2 | Tablet spoils the manual (route, E-0, 6-step sequence before `manual_read`) | **Fixed.** Before the flag: floor plan only (no park map button), route status shows `EXIT ROUTE · UNKNOWN` (no Substation/E-0 rows), Systems shows `AUXILIARY POWER / NO DATA / Locate the 1996 evacuation manual in the Records Annex.` After reading, route, E-0, the park map and the procedure appear, so the toast "Route map added to Journal" is true. | 01/02, 03/04, 08, 09 |
| 3 | New player cannot tell where to go | **Fixed.** (a) Objective copy: one short line that names the place, through `ObjectiveGuide` (e.g. both Records strings now read "Find the 1996 evacuation manual in the Records Annex, east end of the Staff Corridor."). The long post-manual objective is given one leg at a time from the player's position (Feed Prep/dock, service path, Tyrant Valley) and re-shows the banner when the leg changes. (b) Map rebuilt from world data (see below), Visitor Center floor plan with every room incl. Records Annex, objective beacon on the room (room highlighted), true world-XZ markers, `YOU` dot with a heading wedge, `YOU` flips above the dot when it would touch the beacon. (c) The objective card names the place under the objective (ring + `RECORDS ANNEX`). | 02, 08, 14b, 17 |
| 4 | Three-line objective vs journal hint | **Verified** with the longest real objective and a deliberately 3-line unknown string: the banner grows, hint stays below. | 14b |
| 5 | Reader/journal/pause open over a chapter or cutscene | **Fixed (robust).** `MenuController.DismissStoryOverlays()` closes the reader *without* running its page chain, the tablet and pause/settings on: cutscene start, checkpoint reached, chapter title card, `StartFromCheckpoint` (it always hides the skip prompt), external unpause; plus a per-frame safety net (reader open while `InCutscene`/`IsGameOver`). Death now also releases `IsDocumentOpen`. Tested: reader open → `jump_chapter Intro` → reader gone, `5:42 PM` title card visible; reader open → Records restart → `docOpen=False`, `doc_manual.CanInteract=True`; pause open → `jump_chapter Night` → no pause panel, `timeScale=1`. | 15 |
| m | Route map looks like a programmer's diagram | **Fixed.** New `ParkMapView`: warm hillshade + 5 m contours (25 m index) sampled from the island terrain, sea and Titan Meadows lake from their real levels, Tyrant Valley stream from the builder's own `EvaluateStreamX`, buildings/fences/roads at their builder footprints, amber route (active leg solid, legs ahead dashed, done legs muted), green only for E-0, labels in the UI palette placed clear of linework, compass, scale bar (100 m / 10 m), 24 px inside the card. | 02, 08 |
| m | Systems: three NEXT rows, tuning values, mixed caps | **Fixed.** Breakers are `TO DO` (parallel), header counter `BREAKERS 0 / 3`, NEXT only for the single next step (warm plate only on it), `(hold)` without seconds. `TRANSFER` kept (check file dropped that point). | 09 |
| m | Reader: summary repeats transcript, transcript ≠ page, no hanging indent, key last | **Fixed.** Per-page transcripts copied word for word from both manual pages (and the memo, keeper's note, jeep log and Halvorsen log, which also did not match their pages); summary hidden when a transcript exists; TMP hanging indents on numbered lines; `[E]  CONTINUE` key first. Halvorsen's log is handwritten, so it uses the hand font like the keeper's note. | 10/11 |
| m | Settings: two things look lit | **Fixed.** Accent buttons rest on the normal fill with a 1 px amber outline; only focus/hover gets the #6E4415 fill. Active tab / selected row now use the same "chosen" look (dark fill, amber outline + label), so focus stays distinguishable. | 12/13 |
| m | Brightness `0.0 EV`, untestable hint | **Partly.** Shows `-5 … +5` (0 = as designed), hint rewritten. No calibration image, no subtitle size/background rows (need new settings fields in `SaveManager` — see requests). | 13 |
| m | Flashlight: toast + chip = two prompt styles | **Fixed.** The pickup toast is dropped the frame it is raised (only that one); the F chip pulses amber once. All toasts now use the label caps style. | tested in Play mode: toast alpha 0 right after `FlashlightPickup.OnInteract`, chip pulse (no hand-in capture) |
| m→M | Eye always `EXPOSED`, banner never fades, chip permanent | **Fixed.** Eye shows only while a predator is alert or for 3 s after a state change, never over the tablet. Objective banner: full 4.5 s, muted 4.5 s, then fades out; back on a new objective, a new leg, or when the tablet closes. Light chip fades ~3.5 s after a toggle. | 14 |
| m | Reticle vanishes on bright surfaces | **Fixed.** 7 px dot (92 %) on an 11 px black 55 % disc. | 14 (zoom checked) |
| m | Smallest text | **Fixed.** Journal hint 18→21, state labels 18→21, footer 19→21, keycap glyph 50→60 % of chip, status labels 20→21. | 02, 09 |
| m | Olive card tint (#15150D) | **Root cause found and fixed.** The overlay UI is stored in an **8-bit linear** buffer: a dark sRGB value snaps to the steps 0, 13, 22, 28, 34 … per channel (measured with a test strip: (0.078, 0.071, 0.066) → #16160D, 0.02 → #000000). `PanelCard` now sits exactly on the warm step #221C16 (measured #211C15); inner slots on #0D0D0D. | 06, 16 |
| m | End card on flat black, tiny credits | **Fixed.** On `GameFinished` the HUD lifts the screen fade (2.5 s) and the ending backdrop is 0.46, so the dawn coast shows behind the card; credits 22 px. | 16 |
| m | 1 px undimmed right column | **Not reproduced** (check file dropped it as a capture artefact). Clone captures are native 1280x720: x=1279 equals x=1278 under every modal (e.g. end screen (42,41,39) vs (42,41,41)). |
| new | Spaced hyphens as dashes (check file) | **Fixed** in my strings (em dashes, also inside the transcripts where the pages use them) and in the HUD for story text (toasts, subtitles, captions, title cards: " - " → " — "; ranges without spaces untouched). Fonts all contain U+2014. |
| new | Settings lack resolution/quality/V-Sync/shake/rebinding | Not done: needs settings fields and systems outside `UI/` (request below). |

## Files touched (all owned)
- `Assets/Scripts/UI/JournalController.cs` — map page (two views + view switch), gating on `manual_read`, systems copy/labels, documents tab, per-page transcripts (`GetPageTranscript`, `IsHandwritten`; `GetTranscript` kept).
- `Assets/Scripts/UI/MenuController.cs` — story-overlay dismissal, reader column (page transcript, key-first CONTINUE), brightness scale + hint, ending backdrop/credits.
- `Assets/Scripts/UI/HudController.cs` — objective copy via `ObjectiveGuide`, banner timing + leg updates, eye/chip timing, flashlight pulse, toast style, reticle, dash typography, ending fade lift.
- `Assets/Scripts/UI/CretaceaUiFactory.cs` — `PanelCard` on the 8-bit step, accent buttons outline-only, `SetButtonChosen`, keycap glyph 60 %.
- New: `Assets/Scripts/UI/ObjectiveGuide.cs` (objective copy + world targets), `Assets/Scripts/UI/ParkMapGraphic.cs` (vector map layer), `Assets/Scripts/UI/ParkMapView.cs` (map views + terrain texture).
- `Tools/agent/*` — my clone-pinned helper scripts.
- No lamp rules, no materials, no scene or builder changes. `Assets/Resources/UI/Maps/hud_minimap_route.png` is no longer used by the journal (left in place).

## Requests for the lead
1. **`Tools/lookdev/ucli.sh` hard-codes `UNITY_PROJECT_PATH=$HOME/CretaceaBlackoutUnity`.** `capture_set.sh`, `nightlab.sh`, `perf_here.sh` (and `route_test.py` by default) therefore drive the **owner's** editor even when an agent exported its clone path. I did not use them; I used copies in `Tools/agent/` pinned to my clone. Please make `ucli.sh` respect an existing `UNITY_PROJECT_PATH`.
2. Objective strings: the HUD/tablet show `ObjectiveGuide` copy; please move the same copy into `StoryDirector.cs` (lines ~675-1591) so the source and the display agree (the guide falls back to the raw string for anything it does not know, so nothing breaks either way).
3. `DocumentPickup`: add a public `CancelReading()` (clears `_isShowing`, `SetDocumentOpen(false)`). Until then `MenuController` resets the private latch by reflection when it aborts a reader.
4. Overlay UI precision: the overlay canvas is composited from an 8-bit **linear** target (dark UI bands/snaps). If HDRP lets the UI render in sRGB-encoded 8-bit or 16-bit float (`LookRenderSettings`), every dark panel and gradient gets smoother; I picked panel colours on the current steps meanwhile.
5. Settings (Core `SaveManager.Settings`): add `subtitleScale`, `subtitleBackground`, and optionally resolution / quality / V-Sync / camera-shake fields; I will wire the rows in `MenuController` once the fields exist.
6. The `[F] Toggle Tactical Flashlight` toast in `FlashlightPickup.cs:133` can simply be removed (the HUD now hides it the frame it appears).

## Verification
- Compile: 0 errors, 0 warnings after every step. Console: 0 errors in the Play sessions (after the one `CanvasRenderer` exception of my first map build, fixed).
- Final `build_all_scenes` **with** NavMesh: status ok, raptor + T. rex NavMeshes baked, 18 doors, 5 documents.
- `route_test.py --project <clone>`: **38 / 38** in 246 s (incl. "console: no new errors"). `verify_playmode_doors`: 18/18 doors, 5/5 documents, flashlight OK, both NavMeshes valid. Console after the final Play sessions: 0 errors.
- Performance (hidden editor, 1280x720, corridor -40,0.1,20.5): HUD only GPU 6.6 ms / CPU 18.5 ms; tablet open on the park map GPU 5.1 ms / CPU 23.3 ms; documents GPU 4.8 / CPU 20.0 (batch-editor CPU is noisy; the tablet only redraws its map mesh on state changes, markers move by anchors, no per-frame allocations). The park terrain texture (800x516) is built once at scene load: ~0.22 s.

## Not verified / rough edges
- No 1920x1080 player build (critic's 1 px column and the stretched-capture concern): judged on native 1280x720 editor captures only.
- Gamepad navigation of the new map view buttons wired (tabs ↓ → view buttons, ←/→ between them) but only tested by mouse/eval, not with a pad.
- The leg-by-leg objective switch points (x = 8 m, 92 m) are by world X; a player walking back west sees the earlier leg again (intended).
- The VC floor plan has no door gaps; rooms are drawn as closed outlines.
- Calibration image and subtitle options not done (needs Core fields).
- On the floor plan the Loading Dock beacon sits on the room label (north-wing rooms already carry their label high to avoid this).
- The terrain map still shows soft straight lines where the hand-built play area meets the generated island (real slope breaks, blurred).
- 14b shows the master-key chip because the earlier capture run in the same Play session had read the manual.

## Fix round (art-director review of the round-2 hand-in)
Base of this round: `0ed778e`. Captures: `Screenshots/handin/` (16 + 1 files, `INDEX.md` there; same views as
`Screenshots/handin_r2/` where they exist). All captures 1280x720, clone's hidden editor, Play mode, `capture game_view`,
fresh Play session after the final NavMesh build; checked by eye or by pixel measurement as noted.

| Finding | What I did | Proof |
|---|---|---|
| **High: map labels collide with route, beacon, stream, player** | Park map: TYRANT VALLEY / PADDOCK 4 moved to x 295 (clear of the stream at x ~202 and the fence at 397); WAREHOUSE B is now `WHSE B` centred inside its 42 m box; SERVICE PATH lifted clear of the route bend; TORMENTA BAY moved inside the frame. Floor plan: `FEED`/`PREP` at (-7, 12) beside the route, LOADING DOCK label at (15, -4.5) with the dock beacon (`ObjectiveGuide.LoadingDock`) at (15, -13.5), VET moved off the north-south route through the vet doors, SERVICE/PATH on two lines east of the dock. Any label within 40 px of the player dot (text bounds, not the 260 px box) drops to alpha 0.2, so the wedge never sits on a full-strength word (covers the ch1 spawn under SECURITY OFFICE and STAFF CORRIDOR in the corridor). `YOU` sits on a black 0.6 plate. | 01, 06, 07 |
| **Medium: olive cast on three surfaces** | E-0 status box and the systems step rows on `SlotFill` #0D0D0D; floor-plan sheet #0D0D0D, grid opaque #1C160D (both exact 8-bit linear steps); HUD sound caption, subtitle banner and `SubtitleBg` pure black at 0.62 (cannot tint). | 01/06 (sheet), 08 (E-0 box), 17 (caption over the night scene) |
| **Medium: floor plan reads as a diagram (holes, no doors, route through walls)** | The two empty cells: the service void west (x -72..-50, z -8..6) is filled and hatched at 45 degrees (no access); the cell south of the east wing (x -12..8, z -32..-13) is outdoors in the builder (it lies outside `Wall_Wing_SouthExterior`), so it is drawn as yard ground and the building outline now follows the real walls. 18 doorways from `Zone1_VisitorCenterBuilder` are cut into the wall lines (at least 1.6 m) with jamb ticks. The route now runs through the doors the player uses: Records door (x -9) -> corridor -> door_wing (x -2) -> Feed Prep -> door_vet (x 3) -> vet south arch -> dock roller (z -11). | 01, 06 |
| **Medium: objective banners end on a single word** | `ObjectiveGuide.NoOrphan` glues the last two words with U+00A0 for every displayed objective (known and pass-through strings; not inside tags or `{key}` tokens), used by the HUD banner and the tablet. Copy shortened: "Get to the main entrance, south end of the Lobby." and "Follow the service path to the Paddock 4 gate." The tablet's NO DATA hint also keeps "Records Annex" together. | 12 (one line), 01 (3-line card ends "Staff Corridor."), 06 |
| **Medium: new map text under the size bar** | Floor plan rooms 20 px (18 px for rooms under 11 m, shrunk only as far as needed to stay inside the walls at the tablet's 7.4 px/m, never under 16); STAFF CORRIDOR, MAIN PLAZA, SERVICE PATH 18 px. Park map: SERVICE PATH, PADDOCK 4 GATE, PADDOCK 4, RAPTOR PEN, DAWN COAST, TORMENTA BAY 18 px; scale label and N 18 px. Only `WHSE B` (inside its box) stays at 15. | 01, 06, 07 |
| **Medium: documents overflow; only the memo shown** | The archive body now uses the reader's type exactly (Courier Prime Regular 23 px, +8 line spacing; Caveat 32 for the handwritten pages) with no auto-shrink. Long documents are split into pages (TMP page overflow) with `PAGE [1] [2] ...` buttons under the page art; the manual's two scans are separate pages and the thumbnail swaps to `manual_p2` (manual = 4 pages, jeep log = 2). Pad: list -> right -> page buttons, left back. | 09, 10, 11 |
| **Medium: park map seam bands, speck towers** | The "seam" at world x ~521 is a real 18-38 m step in the terrain (heights probed: z -200: 1.2 m at x 520, 19.0 m at x 522; z 200: 2.1 -> 39.5 m) where the play area was cut into the island. Hillshade normals now come from a wide blur (13-tap box, three passes) while contours stay on the 5-tap heights; next to a step (height vs. wide blur > 0.4 m) the shading is flattened and the step is drawn as one dark cliff line, so it reads as a terrain edge, not a pale road. The z ~-109 band no longer measures (row medians 28-30 across y 480-520). Watchtowers are a 10 px ring with a 3 px dot. | 07 |
| **Medium: tablet CPU per frame, perf unproven** | Markers (player, wedge, YOU plate, pulsing beacon) live under a nested `Markers` canvas, so only it re-batches; the player marker, label fades and room highlight update only when the player moved > 0.2 m / 1 degree or the target changed. The DontSave terrain texture is destroyed by the last park view (`ParkTerrainTexture.Release`). Measured below at the base commit and on this branch. | numbers below |
| **Medium: end card horizon seam (world)** | Not UI-owned. Measured in the ending crane view: row medians 103 (sky) / 37-38 (y 351-358) / 62 (sea). Request for the lead below; the 0.46 backdrop is kept and must ship together with the seam fix (or be raised back to 0.88 in `MenuController` if the fix misses the merge). | 16 |
| Low: clean screen not clean; master-key chip permanent | Re-captured at +14 s: banner faded, light chip alpha 0, key chip alpha 0 (read back from the CanvasGroups). The master-key chip now has its own CanvasGroup: shown 6 s when the key is taken, 4 s when looking at a locked door while holding it and when the tablet closes, fades at 0.8/s. | 13 |
| Low: gamepad cannot reach FLOOR PLAN / PARK MAP after the manual | `WireNavigation()` now runs at the end of `RefreshMapTab` (after the view buttons appear) and after each document page change. Tested by eval through `Selectable.FindSelectableOn*` after `OpenJournal()` from TAB: before the manual tab0 down = none (no buttons); after `manual_read`, tab0 down = View_0, View_0 left/right = View_1, up = TabBtn_0. No physical pad was available. | eval log |
| Low: giveaways before the manual | Tab `SYSTEMS`, kicker `FIELD SYSTEMS`, the NO DATA slot 900x420; HUD hint `FIELD TABLET` until `manual_read` (re-checked twice a second), `JOURNAL & ROUTE MAP` after; route status rows close up (MASTER KEY and DOCUMENTS at rows 1-2) and the card shrinks by one row. | 01, 02, 04 |
| Low: "Copy and han..." | The finding text was cut off in the task I received; not addressed. | - |
| Coherence: archive vs reader type | Same font, size and spacing in both (see documents). | 05 vs 09 |

### Brief 5: overlays close on every story beat (all paths now captured)
`MenuController.OnCheckpointReached` now also calls `DismissStoryOverlays()` (it did not before: the round-2 hand-in
claimed it). The manual's own checkpoint is saved from its close callback after the reader root is already hidden, so it
cannot abort itself (route test still 38/38). State read back before/after each beat (`reader / docOpen / paused / journal / timeScale`):
- reader open -> cutscene starts (`GameState.SetCutscene(true)`, the ending's path): `True/True/False/False/1` -> `False/False/False/False/1` (17 shows the screen right after)
- reader open -> chapter title card (Dock chapter title): `True/True` -> `False/False`, title card on screen (14)
- pause open -> checkpoint reached: `paused=True ts=0` -> `paused=False ts=1`, no pause panel (15)
- tablet open -> title card ("23:04"): `journal=True` -> `False`
- (round 2) reader open -> `jump_chapter Intro`: reader gone, 5:42 PM card (`Screenshots/handin_r2/15`)

### Performance (common brief + Bible 2.6)
Same view (player at 200, 2, 4 in Tyrant Valley, manual read, flashlight off), **render target 1920x1080** set with
`PlayModeWindow.SetCustomRenderingResolution` (verified: GameView targetSize and `Camera.main.pixelWidth` = 1920x1080),
`perf_here` medians of 10 samples, two rounds each. Base = UI scripts of `8ed04fa` swapped in and compiled, then restored.

| | HUD only GPU / CPU | tablet open, park map GPU / CPU |
|---|---|---|
| base `8ed04fa` | 6.8 / 13.5 ms, 7.4 / 15.6 ms | 7.2 / 16.0 ms, 9.5 / 16.4 ms |
| `fix/ui` (round 2 code) | 8.8 / 14.9 ms, 7.8 / 15.5 ms | 6.8 / 16.1 ms, 7.0 / 16.4 ms |

The tablet costs about +1-2.5 ms CPU over HUD-only **at the base commit too** (full-screen backdrop and tablet canvases),
and the branch matches the base within noise. CPU 15-16.5 ms = 61-67 fps at 1080p in the **hidden batch editor**;
I could not measure a visible editor window or a player build (the clone editor is batch-only by rule). The marker
canvas change came after these runs; it only removes work.

### Files touched this round (all owned)
`Assets/Scripts/UI/ParkMapView.cs`, `JournalController.cs`, `HudController.cs`, `MenuController.cs`,
`ObjectiveGuide.cs`; helper scripts in `Tools/agent/` (`ui_fix*.sh`, `handin_fix.sh`, `perf_ab.sh`, `nav_test.*`,
`terrain_probe.cs`, `holes.cs`).

### Requests for the lead (new this round)
7. **DawnCoast horizon seam** in the ending crane view (`ending_view`): a dark band across the full width at
   y 351-358 of 720 (row median luma 37 between sky 103 and sea 62), the water's far edge against sky and fog. The end
   card's 0.46 backdrop exposes it; ship the 0.46 backdrop only with that fix, otherwise set the ending backdrop back
   to 0.88 in `MenuController` (one value).
8. The terrain step at world x ~521 (18-38 m vertical) is real geometry; if it is not meant to exist, that is a terrain
   fix (the map now draws it as a cliff line).
(Requests 1-6 from round 2 still stand.)

### Verification this round
- Compile 0 errors / 0 warnings after every step.
- Final `build_all_scenes` with NavMesh: ok, 18 doors, 5 documents, raptor and T. rex NavMeshes baked, no warnings.
- Smoke test: `jump_chapter` Intro, Night, Corridor, ServicePaths, Substation, OldYard, Escape, DawnCoast: 0 console
  errors (one engine warning, "ScriptableRuntimeReflectionSystemSettings.system is assigned more than once", from HDRP
  on Play start; not from UI code).
- `route_test.py --project <clone>`: **38 / 38** in 273 s (incl. "console: no new errors"); final Play session for the
  capture set: 0 console errors.
- Pad navigation verified by eval only (no physical pad); 1080p numbers from the hidden batch editor only.
- The round-1 capture set that was in `Screenshots/handin/` was moved to `Screenshots/handin_r1/`.
