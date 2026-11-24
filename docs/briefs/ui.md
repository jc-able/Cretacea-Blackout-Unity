# Package: UI  (branch `look/ui`)

**Problem.** Every string is LiberationSans faux-bold (no SDF font assets exist for the six bundled fonts), every
panel is a flat hard-edged slab, secondary text is 13–17 px at 1080p, the palette is a cool sci-fi teal that clashes
with the warm world, documents render crunchy, there is no brightness setting.
Read `docs/maps_2026-11-20/digest_ui_dinos.md` (UI part) and `notes_ui_dinos.md`, and look at the Godot original's
theme for the intended warmth: `~/CretaceaBlackout/scripts/ui/ui_theme.gd` (read-only).

**You own:** `Assets/Scripts/UI/*`, `Assets/Resources/UI/*`, `Assets/Art/UI/*` (new sprites/fonts only), in
`Assets/Editor/CretaceaPbrImporter.cs` ONLY the UI/docs/maps texture rules, in `Assets/Scripts/Core/SaveManager.cs`
ONLY new fields in `SettingsData`, and a new editor script for font-asset generation
(`Assets/Editor/CretaceaFontAssets.cs`).

## Goals, in priority order
1. **Real fonts.** Generate TextMeshPro SDF font assets from the bundled TTFs
   (`TMP_FontAsset.CreateFontAsset`, saved with their atlas textures and materials as sub-assets) where the UI
   factory's loaders look for them (`Assets/Resources/UI/Fonts/…`, see the digest): Barlow Condensed (headings,
   labels), Barlow (body), Courier Prime (terminals, documents), Caveat/Kalam (handwritten notes), Stardos Stencil
   (stencilled titles). Real bold weights instead of faux bold; LiberationSans as fallback; dynamic atlas or a
   character set that covers every string in the game.
2. **One UI style** in `CretaceaUiFactory`: warm near-black panels with a 9-sliced rounded sprite (3–4 px radius,
   1 px warm border), a soft shadow, warm off-white primary text, amber accent, coral for danger, green only for
   the escape route; a text underlay/outline so text holds over bright lamps. Minimum sizes at the 1080p reference:
   labels 18–20 px, body 22 px, subtitles 26–30 px. Fade prompts and subtitles in and out. Focus/hover that you can
   see (accent outline). Use the art that already exists (`Assets/Art/UI/HUD/*`, `park_logo`).
3. **Screens:** main menu (logo lockup, tidy list, footer), pause, settings (2 columns, sliders with values),
   chapter select, journal (map, systems, documents), document reader (page ~980 px tall at 1080p, crisp: fix the
   import rules for docs/maps — mips, 4096, no BC1), death and end screens, chapter title card with a soft plate,
   objective banner, stealth indicator, interaction prompt with key icons, damage vignette.
4. **Brightness setting** (−1.5…+1.5 EV, default 0) in Settings, saved in `SettingsData`, applied with
   `Cretacea.Core.AtmosphereController.UserBrightnessEV = value` on load and on change.
5. **Field of view:** the stored value is vertical degrees; default 78 is fisheye in corridors. Default to 68,
   range 55–90, and label it clearly.

## Verify
Play mode at 1280×720 and 1920×1080 (set with `UnityEditor.PlayModeWindow.SetCustomRenderingResolution`):
HUD in `Night` and `Corridor`, pause (`unity command runtime simulate_key --key Escape`), settings, journal
(`Tab`), a document, the main menu scene (`unity command scene open --path Assets/Scenes/MainMenu.unity` then Play),
death screen if you can trigger it. Text must be readable in the 1280×720 captures at 100 %.
