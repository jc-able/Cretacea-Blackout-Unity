# Hand-in: UI package (branch `look/ui`)

One warm UI style across every screen, real fonts, crisp documents, a brightness setting and a saner FOV.
Captures: `Screenshots/handin/` (see `INDEX.md` there; before/after pairs for HUD, settings and the document reader).

## What changed and why

### 1. Real fonts (goal 1)
- New editor script `Assets/Editor/CretaceaFontAssets.cs` (`unity command cretacea build_ui_fonts`, menu
  *Cretacea/UI/Build Font Assets*) generates TextMeshPro SDF assets from the bundled OFL TTFs into
  `Assets/Resources/UI/Fonts/`: Barlow Condensed Bold / SemiBold (headings, labels, buttons), Barlow Regular / Medium /
  SemiBold / Bold / Italic (body), Courier Prime Regular / Bold (documents, terminal lines), Caveat (handwriting),
  Kalam (handwriting, available), Stardos Stencil Bold (stencilled "BLACKOUT").
- Static atlases (SDFAA; 84 pt in 2048x1024 for the two display faces, 56-64 pt in 1024x1024 for the rest) with
  ASCII + Latin-1 + typographic punctuation (dashes, quotes, ellipsis, bullet, middle dot, degree, multiplication sign).
  Static means the atlases never grow at runtime, so they never dirty the repo. Only the arrows (U+2190-2193) and №
  are missing from the faces; they fall back to LiberationSans SDF (set as fallback on every asset).
- Real bold and italic: the weight tables link Barlow Regular to Barlow Bold / Barlow Italic, Barlow Condensed SemiBold
  to Bold and Courier Prime Regular to Bold, so `<b>` and `FontStyles.Bold/Italic` use the real cuts. No faux bold is
  used anywhere any more (`CreateText` no longer sets `FontStyles.Bold`).
- One `<Name> Shadow.mat` preset per font (soft black underlay, 72 %) that every UI text uses by default, so text holds
  over bright lamps and the flashlight hot spot (see `02_after_hud_corridor`).
- The em dash / en dash / middle dot are now real glyphs, so the old string sanitising that turned "—" into "-" is gone
  (only arrows are still replaced).

### 2. One UI style (goal 2) — `CretaceaUiFactory`
- Warm palette (sRGB): panels near-black warm (0.035, 0.031, 0.027, 84-96 %), warm off-white text (0.93, 0.90, 0.84),
  warm grey secondary (0.68, 0.64, 0.57), 1 px warm border (0.62, 0.58, 0.48, 30 %), amber accent and focus
  (0.96, 0.69, 0.28), coral danger (0.95, 0.42, 0.30), green only for the E-0 escape route, a muted cool blue only for
  "CONCEALED" (moonlight). The old cool teal is gone from every bar and accent.
- New `CretaceaPanelGraphic` (an `Image` subclass, so all existing `GetComponent<Image>()` code keeps working): draws a
  panel as up to three nine-sliced layers from tiny procedural textures made at the exact radius: soft drop shadow
  spilling outside the rect, the rounded fill (4-6 px radius) and a 1 px warm border ring. Assigning a sprite or using
  the Flat style makes it a plain Image again. Helpers: `StyleCard`, `StyleRounded`.
- `CretaceaButtonFx`: hover / keyboard-gamepad focus / press feedback that you can see: border turns amber (2 px on
  focus), the fill warms up, the label brightens, the button grows 3 %, `ui_hover` sound on pointer enter. Menus now give
  focus to their first control when they open (`CretaceaUiFactory.Focus`). `RowFocusFx` does the same for settings rows
  (warm plate + amber outline on the slider handle / toggle).
- Sliders: rounded 6 px track, amber fill, round knob, value label in condensed amber. Toggles: pill switch with knob
  plus ON/OFF text.
- Keycaps: `CretaceaUiFactory.CreateKeycap` uses the project's key art (`key_e/f/c/shift.png`, copied to
  `Assets/Resources/UI/HUD/`) and draws a matching cap for any other key (Tab, Space, gamepad X/Y...).
  `FormatKeyTokens` turns `[Tab]`-style tokens in running text into amber bold keys; `ExtractKey` splits a prompt into
  key + action.
- Minimum sizes at the 1080p reference: labels 18-21 px, body 22-24 px, subtitles 28 px (speaker 20 px), prompt 26 px.
- Prompts, subtitles, toasts, sound captions, the skip prompt and stamina fade in/out over 0.15 s (unscaled time).

### 3. Screens (goal 3)
- **HUD**: objective banner on the project's `objective_bar` art (idle alpha 0.86 instead of 0.65), stealth pill,
  crosshair from `crosshair_dot`, hold ring with a faint track, interaction prompt laid out as
  `[key] Action  HOLD 1.5S` and sized to its text (locked: muted title + coral reason on a second line), flashlight chip
  with keycap and lamp dot, master-key chip, rounded stamina bar, toast sized to its text with an amber edge, italic
  sound caption pill, subtitle box sized to its text (1-2 lines), chapter title card with a soft elliptical plate, an
  amber rule and a Courier timestamp line. The gameplay HUD fades out under the field tablet instead of showing through.
- **Main menu**: logo lockup (park logo + "CRETACEA" condensed + "BLACKOUT" stencil), tagline, list-style menu (no boxes
  until hovered / focused), footer with key hints and the version, soft left gradient over the live 3D scene instead
  of a slab.
- **Pause** (with the current chapter), **settings** (2 columns, 13 rows, values shown), **chapter select** (kicker +
  title per chapter), **death** (dark with a blood-red vignette instead of a full red flood, last checkpoint line),
  **ending** (logo, title, stats card): all modal cards share one header (amber kicker, condensed title, warm rule).
- **Journal / field tablet**: 1500x900 card, condensed tabs (active tab warm), bigger map labels (18 px), objective side
  card, systems checklist rows, E-0 bar, document list with the selected entry highlighted, transcript on a paper-
  coloured card in Courier Prime (the keeper's note in Caveat handwriting).
- **Document reader**: page 980 px tall at 1080p (was ~616), fitted per document aspect, on a soft shadow; title, page
  number and summary in a reading column; Continue button with the interact keycap.
- **Import rules** (`CretaceaPbrImporter.cs`, UI/docs/maps rule only): documents and maps import at up to 4096, mip
  maps on (Kaiser), trilinear, aniso 4, CompressedHQ (BC7 on desktop, never BC1). Logo lockup textures under
  `Resources/UI/Brand/` get mips + trilinear. HUD/icon sprites unchanged. This also sharpens the in-world map posters
  (`Art/UI/Maps/map_park_modern|map_preserve_1996`, 2400x1600 now kept at full size).

### 4. Brightness (goal 4)
- `SettingsData.brightnessEV` (new field, default 0), slider "Brightness" -1.5 … +1.5 EV in steps of 0.1, shown as
  "+0.7 EV". Applied with `AtmosphereController.UserBrightnessEV` on change and before the first scene loads
  (`CretaceaSettingsBootstrap`, `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`). A one-line hint under the settings
  card tells the player how to set it.
- Verified: driving the slider to 0.74 saved `brightnessEV: 0.7` in the settings JSON and set the static to 0.7; the
  mean luma of the same Night frame measured 6.4 / 21.7 / 57.5 at -1.5 / 0 / +1.5 EV.

### 5. Field of view (goal 5)
- Slider labelled "Field of View (vertical)", 55-90°, shown as "68°".
- `SettingsData.settingsRevision` (new field). Settings saved before revision 2 (and brand-new settings) are migrated
  once to 68° by `CretaceaSettingsBootstrap` before any camera reads them; out-of-range values are clamped to 55-90.
  Verified in the sandbox: an old file with 78 came back as 68 / revision 2.

## Verification done
- `unity recompile`: 0 errors, 0 warnings after the last change.
- Final `cretacea build_all_scenes` WITH NavMesh: exit 0, no builder warnings (raptor + T. rex NavMesh baked).
- Play-mode smoke test on the rebuilt scenes (console cleared first): jumped Night, Corridor, ServicePaths, Substation,
  OldYard, Escape, DawnCoast and opened HUD states, pause, settings, document, journal (3 tabs), title card, death,
  ending: **0 errors, 0 warnings**. MainMenu scene: 0 errors, 1 warning on scene switch
  (`ScriptableRuntimeReflectionSystemSettings.system is assigned more than once`, HDRP internal, not UI).
  An earlier MainMenu run on the stale pre-rebuild scene logged one HDRP `VolumeComponent.Override` NullReference;
  it did not recur after the rebuild.
  The very last session (MainWorld reopened after MainMenu, journal recaptures) ended with 0 errors and 1 warning that I
  did not read before stopping the editor; most likely the same HDRP scene-switch warning.
- 1080p: `Screenshots/ui/a20_hud_1080.png` / `a21_settings_1080.png` checked at 100 % crops (crisp, same layout).

## Files touched
- `Assets/Scripts/UI/CretaceaUiFactory.cs` (rewritten internals; every public signature kept, new optional overloads
  and helpers added), `HudController.cs`, `MenuController.cs`, `JournalController.cs`
- New: `Assets/Scripts/UI/CretaceaPanelGraphic.cs`, `CretaceaButtonFx.cs`, `RowFocusFx.cs`, `CretaceaSettingsBootstrap.cs`
- New: `Assets/Editor/CretaceaFontAssets.cs`
- New assets: `Assets/Resources/UI/Fonts/*` (12 SDF assets + 12 Shadow materials, ~30 MB),
  `Assets/Resources/UI/HUD/` (copies of key_c/e/f/shift, objective_bar, crosshair_dot, interact_ring, damage_red so the
  runtime can load them), `Assets/Resources/UI/Brand/park_logo.png` (copy for the logo lockups)
- `Assets/Editor/CretaceaPbrImporter.cs`: UI/docs/maps texture rule only
- `Assets/Scripts/Core/SaveManager.cs`: two new `SettingsData` fields only (`brightnessEV`, `settingsRevision`)
- Re-imported metas: `Assets/Art/UI/Documents/*.meta`, `Assets/Art/UI/Maps/*.meta`, `Assets/Resources/UI/Docs/*.meta`,
  `Assets/Resources/UI/Maps/*.meta`

No lamp rules added (no lights in this package).

## Requests for the lead (files I do not own)
1. `Assets/Scripts/Player/CameraDirector.cs` clamps the FOV to 60-100 (lines ~110, 117, 194). The brief's range is
   55-90: please change the clamp to `Mathf.Clamp(fov, 55f, 90f)` (or use `Cretacea.UI.CretaceaSettingsBootstrap.FovMin/
   FovMax` if Player may reference UI; otherwise duplicate the constants). Until then 55-59 on the slider behave as 60.
2. `Tools/lookdev/clone_editor.sh start`: right after launch, the `script eval` that sets the save sandbox ran before
   my editor had registered and was answered by **another clone's editor** (it returned
   `~/CretaceaUnityClones/terrain/SaveSandbox/cretacea_save.json`); the following `scene open MainWorld` may
   have gone there too. The values it set were that editor's own (sandbox path computed from its own dataPath, 1280x720),
   so it was probably harmless, but the script should pass `--project-path "$clone"` (or `--pid`) on every call and wait
   for its own PID to report ready. Please tell the terrain agent in case its open scene was reloaded.
3. Shared scratchpad: the `kit` agent overwrote `scratchpad/env.sh` (it now points UNITY_PROJECT_PATH at the kit clone).
   I moved my files into `scratchpad/ui_pkg/`. Worth telling every agent to use a private sub-folder.
4. `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` is a dynamic atlas that TMP
   rewrites at runtime (it was already modified when I started). I did not commit it. Consider making it static or
   adding it to .gitignore-by-convention.

## What I could not verify
- Real keyboard / gamepad input: `runtime simulate_key` (Escape, DownArrow) does not reach the game in the hidden
  editor (Escape did not pause even before my changes), so pause / journal / settings were opened through
  `MenuController` / `JournalController` calls. Focus outlines were verified on the first focused control
  (CONTINUE in the main menu, RESUME, Brightness row, PROLOGUE) but not by moving focus with keys.
- The death and ending screens were opened by invoking `MenuController.OnPlayerDied` / `OnGameFinished` directly, not
  by dying or finishing the game.
- The ending stats text in the capture is sample text; the real summary string comes from the game.
- In the editor the document textures report RGBA32 (texture compression on import is off in this editor); BC7 applies
  in a player build. Not checked in a build.
- Hover effects with a real mouse.

## Known rough edges
- The objective bar art fades to transparent on its right side, so a long second objective line sits on the world with
  only the text underlay behind it (readable in every capture, but not boxed).
- Kalam is generated but nothing uses it yet (Caveat is the handwriting face in use).
- `CretaceaUiFactory.KeySprite` only has art for E, F, C and Shift; other keys use the drawn cap, which matches the art
  closely but not exactly.

---

## Fix round (art-director review of f0ef736)

Captures: `Screenshots/handin/` (new set, `INDEX.md` there; `01` is the previous hand-in's HUD as the "before").
Verification: `unity recompile` 0 errors / 0 warnings; final `build_all_scenes` **with** NavMesh exit 0, builder
`warnings: []`, raptor + T. rex NavMesh baked; Play-mode smoke test on the rebuilt scenes: Intro (opening skipped),
Night, Corridor, ServicePaths, Substation, OldYard, Escape, DawnCoast, journal, document, pause, settings, title card,
ending, MainMenu + chapter select: **0 errors**. Warnings: only HDRP's
`ScriptableRuntimeReflectionSystemSettings.system is assigned more than once`, logged on entering Play right after a
scene open (same as last round, not UI).

### Findings and what I did
1. **Lower-third collision (high)** — fixed. `HudController`: master-key chip moved to (34,132), stacked on the
   flashlight chip (x 34-246); subtitle wraps at 988 and the box is at most 1060 wide (left edge >= 430 at 16:9);
   stack re-spaced as constants (`SubtitleY 62`, `CaptionY 214`, `PromptY 276`, `E0BoxY 380`). Measured at 1080p
   (`Screenshots/final/a20_hud_1080_bottomleft_crop.png`): chips end at x 245, subtitle starts at x 448; caption to
   subtitle and prompt to caption 20 px apart. 02/05 show the worst case (2-line subtitle + caption + prompt + E-0 bar).
2. **Flashlight chip lies (high)** — fixed. `HudController` keeps `_beamOn` from `OnFlashlightToggled` and uses it in
   `OnFlashlightAcquiredChanged` / `OnInputDeviceChanged`. In `Assets/Scripts/Player/FlashlightController.cs` (lead's
   file) I added exactly the two allowed call lines: `GameEvents.RaiseFlashlightToggled(IsBeamOn);` at the end of
   `SetCutsceneSuppressed` and after the visibility-change `ApplyVisibilityAndBeamImmediate()` in `Update`. Checked in
   Play: Corridor start, beam on, chip "LIGHT ON" (03, and by reading the chip text: `beamOn=True chip=LIGHT ON`).
3. **Journal map (medium)** — fixed without new art: the map sprite is cropped at runtime to its route area
   (`JournalController.CropRouteMap`, rect 16,136,992,656 of the 1024 px art, aspect 1.51 = the frame), the viewport is
   (0.02,0.02)-(0.98,0.90), so the map draws ~915 px wide at 1080p (was ~525): baked labels ~24 px at 1080p / 16 px at
   720p (were ~5-6 px at 720p). The ZoneLbl strip is deleted (the art has its own labels); the map's title and
   "Service Road 3" footer moved to the frame header. Player dot (+ YOU) and objective ring now ride the drawn route
   (piecewise world X -> route points; the substation objective points at its icon). The side card has a ROUTE STATUS
   block (Substation 3, E-0 gate % with a green bar, master key, documents). A 2048 px redrawn map was not needed.
4. **Tablet hides subtitles / captions / danger eye (medium)** — fixed. Subtitle box, caption pill and stealth eye
   are built on a second canvas `[HUDOverlay]` (sort 30, above the journal's 25). Its group hides for menus, pause,
   settings, documents and the ending, not for the tablet. While the tablet is open the caption moves to the free band
   above it (top left) and the subtitle box turns 94 % opaque (07-09). Behaviour change: subtitles and captions now
   also stay up during cutscenes (they were hidden with the gameplay HUD), lifted above the letterbox bar; the stealth
   eye still hides itself in cutscenes.
5. **Document reader (medium)** — fixed. `MenuController.BuildDocumentModal`: the page floats on the backdrop with its
   shadow (714x980 max), only the 760x980 reading column is framed, the pair is centred (~50 px margins at 1080p). Under
   the summary a TRANSCRIPT block (Courier Prime 22 px, line spacing 8, auto-size down to 18; Caveat for the keeper's
   note) fed by the new `public static string JournalController.GetTranscript(string docId)` (reads `ArchiveDocs`).
   Importer (UI docs rule only): `mipMapBias = -0.5` for documents (not maps). 1080p checked: 11 is a 100 % crop of a
   true 1920x1080 frame (game view switched to 1920x1080), text crisp.
6. **Systems checklist colours (medium)** — fixed. Done = off-white dot, muted text, "DONE"; next = amber dot, warm
   row tint, "NEXT"; locked = hollow muted ring, "LOCKED" (Label 18 px, right, -24). Green only on the E-0 bar (08).
7. **Keys drawn two ways (medium)** — fixed with one key art everywhere. New atlas
   `Assets/Resources/UI/Keys/key_atlas.png` + `.json` (made by `docs/handin/ui_tools/make_key_atlas.py`: the
   project's own E/F/C/Shift art pasted in, Tab/Esc/Enter/Space/LB/RB/L3/View/Menu/Q/R/M/J/P drawn in the same style,
   round gamepad A/B/X/Y). `CretaceaUiFactory.KeySpriteAsset()` builds a TMP sprite asset over it at runtime (material
   asset `key_atlas_mat.mat` in Resources keeps the TMP sprite shader in builds) and every factory text gets it, so
   `FormatKeyTokens` now emits `<sprite name="tab">` ("[Hold Space]" -> HOLD + key, "[LB / L3]" -> both keys; unknown
   tokens keep the amber text). `Keycap.SetKey` uses the same atlas. So the HUD journal hint, toasts, objectives, pause
   hint, main-menu footer, tablet footer and skip prompt all show the same keycaps as the prompt (02, 04, 06, 15). I did
   not add a separate `CreateKeyHint` because the inline sprites cover every one of those places.
8. **Focus / hover (medium)** — fixed. `CretaceaButtonFx.OnPointerEnter` and `RowFocusFx.OnPointerEnter` move the
   EventSystem selection to the hovered control (only one hot control); slider tracks are raycast targets now.
   `JournalController.OpenJournal` focuses the active tab; explicit navigation (tabs left/right, down into the document
   list, list up/down, up from the first document back to the tab). Checked in Play:
   `focus=TabBtn_2 right=TabBtn_0 down=DocBtn_0 down2=DocBtn_1 upFromDoc0=TabBtn_2`. Buttons wider than 300 px no
   longer scale (16: titles at the same x in every row).
9. **Uncaptured HUD states (medium)** — fixed and captured: E-0 box at y 380 and the E-0 prompt hint is just "HOLD"
   (no second PROGRESS SAVED; other preserve-progress holds say "PROGRESS KEPT IF YOU LET GO"); objective banner has a
   soft plate sized to its text under the bar art (04, over the GoldenDay sky); damage vignette now tinted white so the
   art's blood red shows (it was multiplied to near-black), peak alpha 0.6, decay 0.9/s (06); gamepad prompts (06);
   daylight HUD (04); E-0 hold (05).
10. **Slider knobs (low)** — fixed: handle area anchored to the centre line (0 px tall), fill area inset ±10 (12).
11. **Composition (low)** — main-menu lockup made compact (logo 156, CRETACEA 80 px, BLACKOUT 50 px) so it ends left
    of the T. rex skull (15); title card plate denser (0.92) and larger, title single-line with auto-size 50-68 on a
    1500 px card (13); the ending shows a headline, three stat tiles from GameState and the credits from the real
    summary string (14); the amber rule hides when the title card has no title; version label 18 px.

### FOV goal
The UI side is done (slider 55-90, "Field of View (vertical)", migration to 68 verified again in the sandbox file:
`fieldOfView 68.0, settingsRevision 2`). Still needed from the lead (files I do not own):
- `Assets/Scripts/Player/CameraDirector.cs` lines 110, 117 and 194: `Mathf.Clamp(..., 60f, 100f)` ->
  `Mathf.Clamp(..., 55f, 90f)`; until then 55-59 behave as 60.
- `Assets/Scripts/Core/SaveManager.cs` line 27: `public float fieldOfView = 78f;` -> `68f`. Every load already goes
  through `CretaceaSettingsBootstrap`, which migrates new and old files to 68, so this is for consistency only; my
  brief allows only new fields in `SettingsData`, so I did not change it.

### Files touched this round
`Assets/Scripts/UI/{HudController,JournalController,MenuController,CretaceaUiFactory,CretaceaButtonFx,RowFocusFx}.cs`,
`Assets/Editor/CretaceaPbrImporter.cs` (UI rule: docs mip bias, keys atlas rule), new `Assets/Resources/UI/Keys/`
(key_atlas.png/.json, key_atlas_mat.mat), new `docs/handin/ui_tools/make_key_atlas.py`, re-imported doc metas
(`Assets/Art/UI/Documents/*.meta`, `Assets/Resources/UI/Docs/*.meta`: mipBias only), and the two call lines in
`Assets/Scripts/Player/FlashlightController.cs`. Not committed: scenes, `Assets/Settings/Atmosphere/*.meta` (guid
churn from the builder, already modified before this round), the TMP LiberationSans fallback atlas.

### Requests for the lead
1. The two FOV lines above.
2. Sign-off on the map-poster metas from last round (`Art/UI/Maps/map_park_modern|map_preserve_1996`: 2400x1600,
   CompressedHQ, mips): unchanged this round.
3. `FlashlightController.cs`: the two event lines are in this commit; please keep them when merging.

### Not verified
- Real mouse hover and real key / gamepad presses: the hidden editor gets no input. Hover was tested by sending
  `pointerEnter` through `ExecuteEvents` (16); navigation by querying `FindSelectableOn*`; the gamepad prompt set by
  switching `CretaceaInput`'s device by reflection.
- HUD states are raised by script (not by playing to that point); the daylight HUD (04) lifts the cutscene
  suppression during the opening shot; the damage vignette (06) is held by re-raising the flash for 3 s.
- The runtime TMP sprite asset and the key material in a player build (checked in the editor only).
- The "YOU" dot position is schematic (world X along the corridor), not a true map projection.

### Known rough edges
- While the tablet is open, the subtitle box covers the bottom ~150 px of the tablet (map footer, E-0 box, last
  document row) for as long as a line is up.
- The objective side card has some empty space between the objective text and ROUTE STATUS on short objectives.
