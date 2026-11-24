# Notes: UI (A) + Dinosaurs/Characters (B) — Cretacea: Blackout Unity port

Read-only map. Paths relative to ~/CretaceaBlackoutUnity. "not verified" = not confirmed in code/assets.

## A1. How the UI is built (canvas, scaler, render mode)

- 100 % code-built uGUI + TextMeshPro (TMP 3.x style API, `TextMeshProUGUI`). No UI prefabs, no UXML/UI Toolkit, no scene-authored widgets. Factory = `Assets/Scripts/UI/CretaceaUiFactory.cs` (static class, line 16).
- `CreateRootCanvas(name, sortOrder)` CretaceaUiFactory.cs:45-61: `RenderMode.ScreenSpaceOverlay`, `pixelPerfect=false`, CanvasScaler `ScaleWithScreenSize`, reference **1920x1080**, `MatchWidthOrHeight`, match **0.5**. EventSystem + `InputSystemUIInputModule`, DontDestroyOnLoad (63-68).
- Three canvases, each built in the controller's `Awake`: `[HUDCanvas]` sortOrder 10 (HudController.cs:274, built from Awake 101-110), `[JournalCanvas]` 25 (JournalController.cs:190), `[MenuCanvas]` 40 (MenuController.cs:337, BuildAllMenus 335-348).
- Every panel/background is an `Image` with a procedural **2x2 white sprite** (CretaceaUiFactory.cs:389-397) via `CreatePanel` (126-149) / `CreateAnchoredPanel` (151-175) => hard-edged flat rectangles, no 9-slice, no rounded corners, no 1 px border, no drop shadow. Only decoration = 2-4 px accent stripes.
- Procedural sprites (Texture2D built at runtime, no mips): circle 64 px (399-417), ring 128 px (419-441), vignette 128 px (443-466, stretched full-screen => low-res gradient), stealth eye 128x64 (468-514). Ring/vignette first try `Resources.Load("UI/HUD/interact_ring" | "UI/HUD/damage_red" | "UI/HUD/vignette")` (422, 446) but **there is no `Assets/Resources/UI/HUD` folder** (Resources/UI has only `Docs/` and `Maps/`), so those loads always return null.
- Builder wiring (only scene-baked UI data): `CretaceaWorldAssembler.cs:945-958` BuildHudController sets `customHoldRingSprite`=`interact_ring`, `customDamageVignetteSprite`=`damage_red` through `FindSpriteAsset` (2306-2318, searches `Assets/Art/UI` then `Assets/Art`, `t:Sprite`); 960-970 BuildJournalController sets `routeMapSprite`=`hud_minimap_route`; 972-981 BuildMenuController sets `isMainMenuScene`, `gameplaySceneName="MainWorld"`, `mainMenuSceneName="MainMenu"`.
- `Assets/Scenes/MainWorld.unity` is **binary-serialized** (6.07 MB, `file` says "data") => GUID greps on it return nothing; whether the 3 sprite overrides are actually set in the built scene is **not verified**. `MainMenu.unity` is text (579 KB) and holds MenuController only (no HUD/Journal).
- Consequence for the look pass: UI restyle = edit C# only; `cretacea build_all_scenes` does not overwrite UI layout (it only re-adds the 3 managers + 3 sprite refs).

## A2. Palette constants + central restyle points

Palette, CretaceaUiFactory.cs:19-31 (hex = sRGB of the float values):

| const | float RGBA | hex / alpha | where used |
|---|---|---|---|
| PanelBg | .045 .058 .072 .86 | #0B0F12 @86% | HUD boxes (objective, prompt, chips, toast, skip) |
| PanelBgSolid | .038 .048 .060 .96 | #0A0C0F @96% | pause/settings/chapter/doc cards, journal tablet |
| PanelCard | .085 .105 .128 .88 | #161B21 @88% | journal sub-cards |
| BorderSubtle | .28 .36 .42 .42 | #475C6B @42% | dividers, non-accent button stripe |
| AccentAmber | .95 .67 .22 | #F2AB38 | headers, hints, slider fill, hold ring |
| AccentTeal | .32 .82 .78 | #52D1C7 | prompt bar, stamina, "CONCEALED", player dot |
| AccentGreen | .26 .88 .44 | #42E070 | master key chip, E-0 fill |
| AccentDanger | .92 .22 .18 | #EB382E | "SPOTTED", death title, exhausted stamina |
| TextPrimary | .93 .95 .96 | #EDF2F5 (cool white) | body text |
| TextMuted | .64 .70 .74 | #A3B3BD (cool grey) | secondary text |
| ButtonNormal | .10 .13 .16 .90 | #1A2129 @90% | button bg |
| ButtonHover / ButtonPressed | #2E3B47 / #42331C | — | **dead constants, 0 references** |

- Inline colours that bypass the palette: accent-button bg `(0.42,0.27,0.08,0.94)` #6B4514 (CretaceaUiFactory.cs:232); slider/toggle track #242E38 (297, 358); damage vignette `(0.68,0.04,0.04)` (HudController.cs:279); stealth pill (339); caption pill (458); subtitle bg (465); MenuController sidebar (357), pause backdrop (397), settings/chapter backdrops (430, 527), doc backdrop (581), death red `(0.14,0.012,0.015,0.88)` (609), ending (633); Journal backdrop (JournalController.cs:195), step rows (340, 355), active-tab amber-brown `(0.34,0.22,0.08,0.96)` (420).
- Button states (CretaceaUiFactory.cs:239-246): ColorTint with highlighted 1.32 / selected 1.25 multipliers on a near-black bg => hover/focus barely visible (#1A2129 -> ~#222B36); no outline/scale/sound on hover; gamepad focus state almost invisible.
- The Godot original's theme (`~/CretaceaBlackout/scripts/ui/ui_theme.gd:53-130`) was WARM: text `(0.92,0.90,0.85)` #EBE6D9, panel `(0.02,0.025,0.03,0.88)` + 1 px warm border `(0.5,0.48,0.4,0.3)`, hover/focus border coral `(0.95,0.5,0.35)` #F28059, pressed red `(0.7,0.16,0.10)`, every label with black 70 % drop shadow offset (1,2), corner radius 3-4. The port replaced it with a cool teal/amber sci-fi palette and dropped borders + text shadows.
- Central restyle points (change once, applies everywhere): palette 19-31; fonts 70-102; `CreateText` 177-210 (headers = `FontStyles.Bold` + `characterSpacing 3.5`; no outline/underlay/shadow material); `CreateButton` 212-263; `CreatePanel`/`CreateAnchoredPanel` 126-175 (swap the 2x2 white sprite for a 9-sliced rounded/bordered sprite here); `CreateSliderRow` 265-331; `CreateToggleRow` 333-378. HUD/menu sizes and positions are literal numbers inside each Build* method (see A4/A5).

## A3. Fonts: used vs available, TMP vs legacy

- All text is TMP (`TextMeshProUGUI`); no legacy `UnityEngine.UI.Text` in Scripts/UI.
- Resolution chain: header `Resources.Load<TMP_FontAsset>("UI/Fonts/BarlowCondensed-Bold SDF")` -> `"Fonts & Materials/LiberationSans SDF"` -> TMP default -> OS font (CretaceaUiFactory.cs:70-81); body `"UI/Fonts/Barlow-Regular SDF"` -> header font (83-91); mono `"UI/Fonts/CourierPrime-Regular SDF"` -> body font (93-102).
- **No TMP font asset exists for any project font**: the only `*SDF*.asset` files are `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` (+ Fallback); there is no `Resources/UI/Fonts` folder. => **every string in the game (headers, body, mono, title, subtitles) renders in LiberationSans SDF** (TMP Settings default, `Assets/TextMesh Pro/Resources/TMP Settings.asset:27`, fallback list empty :36). The "mono"/typewriter style is therefore not monospaced at all, and headers are faux-bold LiberationSans with +3.5 tracking.
- Available but unused TTFs (Assets/Art/UI/Fonts/): Barlow (Regular/Medium/SemiBold/Bold/Italic), BarlowCondensed (Medium/SemiBold/Bold/ExtraBold/Black), CourierPrime (Regular/Bold), Caveat (variable wght), Kalam (Regular/Bold), StardosStencil (Regular/Bold). Only mention in code: credits string GameState.cs:487.
- Godot original roles (ui_theme.gd:8-12): head = BarlowCondensed, body = Barlow, type(writer) = CourierPrime, hand = Caveat/Kalam (handwritten notes); Stardos Stencil was presumably for signage/titles (not verified).
- Fix: generate TMP SDF assets (Window > TextMeshPro > Font Asset Creator, or an editor script with `TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024)` saved as assets) at exactly `Assets/Resources/UI/Fonts/BarlowCondensed-Bold SDF.asset`, `Barlow-Regular SDF.asset`, `CourierPrime-Regular SDF.asset` -> the existing loader picks them up with zero code change. Add LiberationSans as fallback for missing glyphs (· — →: HudController.cs:721-725 strips em dash/arrow/middle dot precisely because of glyph gaps).

## A4. HUD elements (HudController)

All positions in 1920x1080 reference px (anchor, pivot, anchoredPos, size). Font sizes are TMP px at 1080p; at a 1280x720 window everything is x0.667.

| element | file:line | layout | text/colour | behaviour |
|---|---|---|---|---|
| DamageVignette | HudController.cs:279-282 | full-screen Image | tint (0.68,0.04,0.04,a) | a = intensity*0.85, decays 1.15/s (205-212, 698-701); sprite = builder-wired `damage_red.png` else 128 px procedural |
| GameplayHUD group | 285-294 | full-screen CanvasGroup | — | alpha 0 in menu/modal/cutscene (230-255) |
| ObjectiveBanner | 313-334 | TL (0,1) pivot (0,1) pos (38,-34) 560x92, PanelBg + 4 px amber left bar | "CURRENT OBJECTIVE" 14 bold amber; body 19 TextPrimary (2 lines max: rect 40 px, Overflow mode); "[Tab] Journal & Route Map" 14 muted | alpha 1 for 4.5 s after change then 0.65 forever (176-182, 517-533) |
| StealthEye | 336-353, 573-607 | top-centre pos (0,-34) 156x68 pill (0.03,0.045,0.06,0.76); eye 68x34 procedural | label 13 bold | only when flag `blackout`; SPOTTED! (danger, pulse sin 9t), EXPOSED (amber, vis>0.95), CONCEALED (teal, vis<=0.18), SHADOWED (muted) |
| Reticle | 357-361, 626, 632 | centre 6x6 circle, white 72 % (10x10 on target) | — | the "crosshair"; `crosshair_dot.png` unused |
| HoldRing | 363-372, 650 | centre 68x68, Filled Radial360 from top | AccentAmber | sprite = builder-wired `interact_ring.png` else procedural |
| InteractPrompt | 374-390, 618-659 | bottom-centre pos (0,224) 640x78 PanelBg, 3 px bottom bar teal (amber when locked) | title 22 bold; hint 17 amber ("Hold (1.5s)") | alpha 0/1 snap, no fade |
| E0Progress | 392-404, 653-670 | bottom-centre pos (0,314) 460x44 | label 15 "mono", green fill | only gate_box hold |
| FlashlightChip (the only light indicator) | 409-422, 535-555 | BL pos (38,76) 215x38, 10 px amber dot | "[F] LIGHT: OFF/ON" 15 "mono" | hidden until HasFlashlight; no battery meter |
| MasterKeyChip (only key indicator) | 424-431, 557-560 | BL pos (262,76) 165x38 | "MASTER KEY" 15 green | SetActive on key |
| StaminaBar | 433-441, 609-616 | BL pos (38,44) 280x14 | teal / danger fill | shown when <95 % |
| ToastBanner | 446-455, 681-688 | TR pos (-38,-34) 520x58, 4 px teal right bar | 18 right-aligned | timer, snap hide |
| SoundCaption | 457-462, 690-696 | bottom-centre pos (0,168) 560x36 (0.03,0.04,0.05,0.78) | 16 amber "mono" | setting-gated |
| SubtitleBanner | 464-476, 672-679 | bottom-centre pos (0,74) 980x82 (0.025,0.032,0.042,0.84) | speaker 15 bold amber; body 22 centred (48 px rect = 2 lines, Overflow) | snap show/hide, no fade |
| Letterbox | 479-485, 734-756 | black top/bottom bars | — | animate to 92 px |
| ChapterTitleCard | 487-502, 703-719 | centre pos (0,120) 1100x140, **no plate/backdrop** | main 48 bold; sub 22 amber "mono" | fade 0.55 s in / hold / 0.65 s out |
| CutsceneSkip | 504-511, 764-772 | BR pos (-42,38) 260x40 | 16 muted | alpha 0.9 |
| ScreenFade | 307-310, 727-732 | full-screen black | — | coroutine |

- No film-grain or HUD-vignette overlay in UI code: grain/vignette come from HDRP volume overrides (env.txt:13-14 Vignette 0.38, FilmGrain Medium3 0.24 on one profile; env.txt:32/43 zeroed on another).
- Unused HUD art (no code path reads them; scene refs unverifiable because MainWorld.unity is binary): `crosshair_dot` 16x16, `film_grain` 256x256, `interact_ring_fill` 128x128, `key_c/key_e/key_f` 64x64, `key_shift` 128x64, `objective_bar` 1024x96, `vignette` 1024x1024. Key glyphs are plain text tokens from `CretaceaInput.FormatPrompt("{flashlight}")` etc. (HudController.cs:549, 804) instead of the key_*.png icons.
- All show/hide is instant `alpha = 0/1` except objective/title/letterbox/fade => prompts and subtitles pop.

## A5. Main menu, pause, settings, journal, documents, end screens

- Main menu (MenuController.cs:350-393): left sidebar full height x 560 px, `(0.03,0.042,0.055,0.84)` + 2 px BorderSubtle right edge; title "CRETACEA:\nBLACKOUT" 56 bold TopLeft (offsetMin 56,200 / offsetMax -42,-84); tagline "ISLA TORMENTA  ·  SURVIVAL HORROR" 16 amber "mono" (367-369); VerticalLayoutGroup spacing 14 with 5 buttons 440x58 (CONTINUE = accent, NEW GAME, CHAPTER SELECT, SETTINGS, QUIT) (371-390). No logo (`Art/UI/Signs/park_logo.png` 1024x1024 exists, imported as Default texture, unused). 3D backdrop = `Menu_StormyAtriumVignette` + `Menu_StormJungleExterior` built in CretaceaWorldAssembler.cs:1282-1490 (Hero_TrexSkeleton from `Assets/Prefabs/Props/PF_TrexSkeleton.prefab` 1449-1462; Menu_* HDRP lights 1473-1489; profile `Assets/Settings/Atmosphere/MainMenu_StormAtmosphere.asset` 2132-2134).
- Pause (395-426): backdrop `(0.015,0.02,0.028,0.80)`, card 520x500 PanelBgSolid, "PAUSED" 40 bold, 4 buttons 420x56.
- Settings (428-489): card 1240x680, header "CRETACEA - SETTINGS" 34 amber; left column Master/Music/SFX/Voice volume + FOV 65-95 + Fullscreen toggle; right column Mouse/Gamepad sensitivity 0.25-3.0, Invert Y, Subtitles, Sound Captions, Rumble; "SAVE & BACK" 260x52. **No Brightness/Gamma/Exposure calibration, no graphics quality, no subtitle size/background options** (only SettingsData fields used at 457-470).
- Chapter select (525-567): card 720x760, one 620x58 button (font 20) per `CheckpointId` (7), titles from `GameState.GetChapterTitle`.
- Document modal (579-605): backdrop 88 %, frame 960x880; page Image anchors x .06-.94, y .24-.94 (~845x616 px) with preserveAspect => a 1600x2200 page is drawn at only ~448x616 px; summary 19 px; button "CONTINUE / CLOSE [E]" 300x48. Sprite = `Resources.Load<Sprite>("UI/Docs/{assetKey}")` (229); the second fallback `"Art/UI/Docs/..."` is not under Resources (dead).
- Death (607-629): full-screen blood red `(0.14,0.012,0.015,0.88)`, "YOU WERE KILLED" 64 danger, RETRY 340x58 / MAIN MENU 340x54.
- Ending (631-653): 94 % dark, "CRETACEA: BLACKOUT" 58 amber, stats 24 "mono", button 380x58.
- Journal (JournalController.cs:188-410): backdrop 76 %, tablet 1380x820, header bar 84 px PanelCard + 2 px amber underline, title "CRETACEA PARK - FIELD TABLET" 28; 3 tab buttons 248x48 font 17 (active tab bg `(0.34,0.22,0.08,0.96)` 420); footer 16 "mono". Map tab: frame 68 % width, map = `routeMapSprite` (`hud_minimap_route` 1024x1024) with AspectRatioFitter FitInParent (258-275), 6 zone labels 13 "mono" (278-285), beacon ring 22 px amber, player dot 16 px teal; side card 22/20 px. Systems tab: header 24, step rows 1240x54 with 16 px dot + 20 px text (316-368). Documents tab: list buttons 410x76 font 17, detail 24/20 "mono" (369-410).
- `map_park_modern.png` / `map_preserve_1996.png` (2400x1600) are only used as in-world props (Zone1_VisitorCenterBuilder.cs, Zone3_SubstationAndYardBuilder.cs), never in the journal.

## A6. UI art assets (Art/UI, Resources/UI)

- Counts: 11 HUD PNGs, 3 maps, 7 documents, 22 signs, 21 font files (6 families). Resources/UI/Docs (7) + Resources/UI/Maps (1) duplicate the Art/UI copies by name (runtime loads use the Resources copies).
- Import (all HUD/Maps/Documents + Resources/UI metas, e.g. `Assets/Art/UI/HUD/interact_ring.png.meta`, `Assets/Resources/UI/Docs/keeper_note.png.meta`): textureType 8 Sprite, spriteMode 2 (Multiple, single full-rect sprite `<name>_0`), **enableMipMap 0**, filterMode 1 bilinear, **maxTextureSize 2048**, **textureCompression 1** (Normal => BC1/BC3). Signs are Default textures with mips (e.g. `Signs/park_logo.png.meta`).
- Effect: documents 1600x2200 are clamped to ~1489x2048 on import, block-compressed, then minified ~3.3x with no mips in the doc modal => crunchy/aliased handwriting and typewriter text; 2400x1600 maps clamped to 2048. Procedural 128 px vignette stretched to 1920 px => soft banding.
- Fix: docs/maps/HUD textures -> Generate Mip Maps ON + Trilinear, compression None or BC7 (High Quality), max size 4096 for docs/maps; render docs bigger (page ~980 px tall) with optional zoom.

## A7. Visible UI problems in screenshots

- `shots_base/play_Corridor_5s.jpg` (1280x720 => UI x0.667): objective header/journal chip 14 px -> ~9 px, stealth label 13 -> ~8.7 px, light chip 15 -> 10 px: illegible. Objective box at idle alpha 0.65 = grey smear on dark scene. A bright white light cone runs from the top-centre straight through the stealth-eye pill to the vanishing point (lighting/volumetric, not UI). The viewmodel glove + flashlight read as flat light-grey/white, almost unshaded (viewmodel material not verified; outside my files).
- `shots_base/play_Night_5s.jpg`: subtitle banner legible (22 -> 14.7 px) but it is a flat translucent slab; speaker line 15 -> 10 px. All text is the same LiberationSans (no typographic hierarchy).
- `gemqa_small/01_mainmenu_home.jpg`: faux-bold LiberationSans title with +3.5 tracking, tagline unreadable (16 -> 10.7 px), five flat slabs, lower half of sidebar empty, no logo; the 3D backdrop reads as flat blue daylight with an empty horizon (not storm night).
- `gemqa_small/12_pause_menu_no_overlap.jpg` (older build): "23:05" title-card ghost visible under "PAUSED" despite the file name; code guards exist now (HudController.cs:214-218, 242-254) but not re-verified. Hover/selected tint 1.25-1.32x on #1A2129 is invisible, so gamepad focus is unclear.
- Root causes: (1) missing SDF font assets -> generic faux-bold font; (2) 2x2-white-sprite slabs with no border/radius/shadow; (3) 13-17 px secondary text at 1080 ref (too small below 1080p; match 0.5 shrinks further on ultrawide); (4) cool palette clashing with the warm amber lamps; (5) no TMP outline/underlay, so text over bright light has no separation.

## B1. Dinosaur assets: FBX, textures, manifest

Source: `Assets/Art/Dinosaurs/dino_manifest.json` (Blender 5.2 headless "dino-rigger" pipeline, 1 m units, +Y up, +Z fwd, 30 fps; 1 skinned mesh + 1 material per species) and per-folder `metadata.json`.

| species (folder / FBX) | tris / verts | bones | size (m) W x H x L | clips (s, loop) |
|---|---|---|---|---|
| Velociraptor (raptor/Velociraptor.fbx 4.7 MB) | 28,694 / 14,330 | 34 | 0.95 x 1.67 stalk (2.04 bind) x 3.24 | Idle_Alert 3.6 L, Stalk_Walk 0.8 L 1.9 m/s, Sprint_Run 0.47 L 6.2 m/s, Sniff_Inspect 3.2 L, Roar_Call 2.2, Bite_Lunge 1.1, Turn_L/R 0.8 L |
| Tyrannosaurus (trex/Tyrannosaurus.fbx 6.4 MB) | 49,516 / 24,756 | 33 | 4.20 x 5.52 x 11.2 | Idle_Heavy 4.0, Walk_Stomp 1.6 (2.6 m/s), Charge_Run 0.8 (7.0), Roar_Massive 3.2, Look_Scan 4.0, Bite_Snap 1.4, Fence_Bash 2.0 |
| Triceratops (triceratops/Triceratops.fbx 2.7 MB) | 23,598 / 11,801 | 26 | 2.33 x 3.61 x 7.6 | Idle_Alert 4.5, Walk_Heavy 1.8 (1.3), Snort_Threat 2.6 |
| Brachiosaurus (sauropod/Brachiosaurus.fbx 3.7 MB) | 29,568 / 14,782 | 33 | 3.49 x 16.08 x 19.0 | Idle_Graze 6.0, Walk_Majestic 3.2 (1.35), Head_Lift_Call 4.0 |
| TrexSkeleton (trex_skeleton/TrexSkeleton.fbx, static prop) | 38,120 / 25,394 | 0 | 3.44 x 7.85 x 12.0 | — |

- Textures per species (`<folder>/Textures/`): `_Albedo` (RGB), `_Normal` (OpenGL +Y), `_Metallic`, `_Roughness`, `_AO`, `_MaskMap` (RGBA: R metal, G AO, B 0, A smoothness) — all **4096x4096**. Albedo 11-16 MB PNGs.
- Measured (PIL, downsampled): **AO maps are blank** (mean 0.998-0.999, min 0.96 for every species) => MaskMap G = 1 => no cavity/specular occlusion. Smoothness (MaskMap A) is near-uniform: raptor mean 0.53 (p5-p95 0.49-0.56), T. rex 0.41 (0.30-0.50), trike 0.52 (0.33-0.58), brachio 0.22 (0.04-0.44). Metallic mostly ~0 (means 0.016-0.04) but brachio has spots at 1.0, others up to 0.22-0.27. Albedo luminance means 0.21-0.29 (fine), p95 up to 0.56.
- Manifest `deformation_audit.max_edge_stretch`: raptor Sprint_Run 76.2, Stalk_Walk 64.3, Idle 35.4; T. rex Charge_Run 43.2, Walk 30.7 (brachio <= 12.3, trike <= 18.4) => heavy skin stretching on raptor/T. rex (visible as smeared texels on the raptor throat in `gemqa_small/50_dino_raptor_roar_jaw.jpg`).
- Texture import (e.g. `raptor/Textures/Velociraptor_Albedo.png.meta`): Default platform max 4096, mips on, aniso 8, albedo compression Normal (BC1), normal + mask CompressedHQ (BC7/BC5); no mip streaming. Enforced by `Assets/Editor/CretaceaPbrImporter.cs:167-215` (`/dinosaurs/` is a 4096 "high-res" token, line 69-72).
- FBX import (`raptor/Velociraptor.fbx.meta`): Generic rig, globalScale 1, useFileScale 1, materialImportMode 2 (embedded FBX materials), normals AreaAndAngleWeighted, blend shapes on, maxBonesPerVertex 4, mesh compression off. Quality skinWeights 255 (ProjectSettings/QualitySettings.asset:21).

## B2. Prefab structure + import settings per species

- `Assets/Prefabs/Dinosaurs/PF_{Velociraptor,Tyrannosaurus,Triceratops,Brachiosaurus}.prefab` = **fully unpacked** FBX copies (no prefab-variant link to the FBX): root GO `PF_<Species>` with Animator (Generic avatar from the FBX, controller from DinoAnimatorBuilder, root motion off, AlwaysAnimate), NavMeshAgent, CapsuleCollider, 3D AudioSource, `DinoProceduralRig` + species AI (`RaptorAI` / `TrexAI` / `HerbivoreAI` for trike and brachio); children = `<Species>_Armature` bone tree + one `SkinnedMeshRenderer` GO named after the species.
- SkinnedMeshRenderer (identical on all 4, e.g. PF_Tyrannosaurus.prefab:688-740): 1 material `MAT_<Species>`, CastShadows On, ReceiveShadows On, MotionVectors per-object + skinned MV, `m_Quality 0` (auto), **UpdateWhenOffscreen 1**, RayTracingMode 3, layer `GameLayers.Dinosaur` (DinoAnimatorBuilder.cs:1008). Local bounds extents: raptor (0.59, 0.97, 1.67), T. rex (2.51, 3.33, 5.74), trike (1.44, 2.14, 3.92), brachio (2.22, 8.51, 9.58).
- **No LODGroup** on any dino (and `lodGroups=0` scene-wide, env.txt:110); no separate eye/teeth/claw meshes or materials (manifest: `fbx_mesh_count 1`).
- Scale: prefabs are 1:1 with the FBX (root scale 1); there is no visual-scale field in `DinoConfig` — species size is purely the mesh. Godot used varied sizes (sauropods 17 m and 15 m, trikes 7.5/6.9/6.3 m: `~/CretaceaBlackout/scripts/cutscene/opening.gd:60-70`); Unity spawns identical clones (19.0 m brachio, 7.6 m trike).

## B3. Dino materials + SSS profile

- `Assets/Art/Materials/Dinosaurs/MAT_{Velociraptor,Tyrannosaurus,Triceratops,Brachiosaurus}.mat` (282 lines each) are **identical except texture GUIDs** (diffed). HDRP/Lit, opaque, MaterialID 0 = Subsurface Scattering (MAT_Velociraptor.mat:28-37 keywords `_MASKMAP _NORMALMAP _NORMALMAP_TANGENT_SPACE _MATERIAL_FEATURE_SUBSURFACE_SCATTERING _MATERIAL_FEATURE_TRANSMISSION`).
- Maps bound: `_BaseColorMap` = Albedo, `_NormalMap` = Normal (scale 1), `_MaskMap` = MaskMap (verified GUID->file for all 4). **Not bound**: `_DetailMap`, `_BentNormalMap`, `_ThicknessMap`, `_SubsurfaceMaskMap`, `_EmissiveColorMap`, `_HeightMap` (MAT_Velociraptor.mat:57-146).
- Values (MAT_Velociraptor.mat:150-266): `_BaseColor` (1,1,1,1); `_SmoothnessRemapMin 0.12` / `Max 0.78` (=> effective smoothness raptor ~0.47, T. rex ~0.39, trike ~0.46, brachio ~0.26, all nearly uniform across the body); `_Metallic 0`, metallic remap 0-1 (mask R passes through, so brachio metal spots render metallic); `_AORemapMin/Max 0/1` (AO map blank); `_SubsurfaceMask 0.35`; `_TransmissionEnable 1`, `_Thickness 1`, ThicknessRemap (0,1); `_DiffusionProfileHash 2.311843` = asfloat(1075049788) = SSS_DinoSkin hash (verified); `_ReceivesSSR 1`, `_SupportDecals 1`, `_EnableGeometricSpecularAA 0`; stencil `_StencilRefGBuffer 14` (SSS bit set => SSS pass active).
- `Assets/Settings/Rendering/SSS_DinoSkin.asset`: scatteringDistance (0.55, 0.18, 0.11) x multiplier 1.15, transmissionTint (0.65, 0.22, 0.14), transmissionMode 1 (thin object), thicknessRemap (0, 5), worldScale 1, ior 1.42, hash 1075049788. Registered in `Assets/Settings/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset:162-170` DiffusionProfileList (3rd entry); runtime profile `MainWorld_RuntimeAtmosphere.asset` does not override the list (env.txt:1-15); HDRP asset SSS=True (env.txt:63), SubSurfaceScattering sampleCount 1 in default profile (env.txt:49).
- Same SSS + transmission applies to teeth, claws and eyes (single material) and eyes get the same ~0.4-0.5 smoothness as skin => no wet eye glint, waxy teeth. No emissive => no "glowing eyes" source in material (glow would only come from bloom on specular, not verified in-game).
- No wetness response for the rain chapters: smoothness is static (the "wet rain sheen" comment at DinoAnimatorBuilder.cs:888-890 only sets the remap; the uniform A channel keeps it matte-plastic).
- Hero skeleton (`TrexSkeleton.fbx`) is rendered with the **FBX-embedded material `TrexSkeleton_Mat`** (materials.txt:88: HDRP/Lit, albedo + normal, **no mask map**, smoothness 0.50, MaterialID standard); `Assets/Prefabs/Props/PF_TrexSkeleton.prefab` (4.6 KB) is an FBX instance with no material override. Its 4K `TrexSkeleton_MaskMap.png` is unused. Used in the main-menu backdrop (CretaceaWorldAssembler.cs:1449-1462) and the VC lobby (Zone1_VisitorCenterBuilder.cs:614, scale (3.4, 4.8, 11.8) via InstantiatePropOrFallback).

## B4. DinoAnimatorBuilder material/prefab/renderer code

- Entry: CLI `unity command cretacea build_dino_prefabs ... -- --overwrite true` (DinoAnimatorBuilder.cs:21, 42); menu `BuildDinoPrefabsMenu` -> `BuildAllDinosaurs(.., overwrite: true)` (~55-102): per species `BuildOrUpdateConfigAsset` -> `ConfigureModelImporter` (516+) -> `BuildAnimatorController` -> `BuildOrUpdateHdrpMaterial` (835-911) -> `BuildDinoPrefab` (982-1076). Output dirs: materials `Assets/Art/Materials/Dinosaurs` (line 28), prefabs `PrefabOutputDir` = Assets/Prefabs/Dinosaurs.
- `BuildOrUpdateHdrpMaterial`: reuses the existing .mat (keeps unrelated properties), sets shader HDRP/Lit, forces Normal map importer to NormalMap + linear and MaskMap to linear (856-857, 953-971), binds BaseColor/Normal/Mask + keywords (863-891), **resets `_SmoothnessRemapMin/Max` to 0.12/0.78** (889-890), binds SSS_DinoSkin by reflection (893-951: `_DiffusionProfileAsset` = GUID as Vector4, `_DiffusionProfileHash` = asfloat(hash)), **resets `_MaterialID` 0 and `_SubsurfaceMask` 0.35** (903-905). Anything else (detail map, thickness map, subsurface mask map, coat) survives a rebuild.
- `BuildDinoPrefab`: instantiates FBX, unpacks completely (1003-1006), sets layer Dinosaur, `updateWhenOffscreen = true` and replaces every material slot with the species material (1011-1021), adds Animator/NavMeshAgent/CapsuleCollider/AudioSource/DinoProceduralRig/AI (1023-1067), `SaveAsPrefabAsset` (1069). => **Hand edits to dino prefabs (LODGroup, extra renderers, renderer flags) are destroyed by any `--overwrite true` rebuild**; put visual changes into this function.
- Prefabs are wired into the scene by `CretaceaWorldAssembler.BuildDinoSpawner` (CretaceaWorldAssembler.cs:984-1017; paths at 58-61). Missing prefab => `DinoSpawner.CreateFallbackRuntimeDino` primitive actor (DinoSpawner.cs:90-98).

## B5. DinoConfig scale/height + DinoProceduralRig eye/jaw/visual + DinoSpawner placement

- `Assets/Scripts/AI/DinoConfig.cs` has **no visual scale**; only nav/gameplay sizes (assets in `Assets/ScriptableObjects/AI/DinoConfig_*.asset`): raptor agentHeight 1.67 / eyeHeight 1.42 / radius 0.45 (DinoConfig.cs:366-368); T. rex 4.40 / 3.85 / 1.45 (438-440; mesh is 5.52 m tall); brachio 12.0 / 11.5 / 1.65 (509-511); trike 3.10 / 2.05 / 1.25 (574-576). Jaw max open deg (rigOverrides): T. rex 42 (105), brachio 22 (130), trike 24 (155), raptor 36 (180).
- `DinoProceduralRig.cs`: look-at, foot IK, pelvis, jaw only; **no renderer/material/eye code** (grep for Renderer/Material/emiss/eye: only `GetEyeWorldPosition` 301-305, a transform helper). Jaw = breath sine (0.08 x 0.35 Hz default, 92-94) + envelope pulse (`TriggerJawPulse` 291-299) + AudioSource RMS (gain 4.2 in prefab), rotated about skull right axis (448-517). Eyes are just texels on the body mesh.
- `DinoSpawner.cs` only instantiates (Spawn 67-113; grounded via ResolveSpawnPosition 270+); placement is in `Assets/Scripts/Story/StoryDirector.cs`:
  - Opening (day) `OpeningCutsceneRoutine` 428+: brachio `meadow_sauropod_a` (-40,-6.2,-150) yaw 160 and `_b` (-95,-6.2,-185) yaw 60 (545-548); 3 trikes at `meadow_trike` (-18,-6.2,-142) + (7i,0,-4i), yaw 110+15i (550-558); intro T. rex at (64, ground, -28) yaw 180 (561); all AiEnabled=false. Positions WorldRegistry.cs:83-96.
  - Talon breakout: 2 raptors at (-36+2.5i, 0, 56+3i) (784-792). Lobby glass raptor (-58,0,-38) (827). Wing raptor at `wing_cold` (933). Service-path crossing raptor `raptor_cross_a`->`_b` (985). Valley T. rex near substation (300, ground, 55) or other start (1020-1027). Yard raptors `yard_raptor_a` (445,1,20), `_b` (470,1,-30) (1338, 1352).
- The live dump has no dino material in use (materials.txt) because dinos are runtime spawns under `[DinoSpawner]/ActiveDinosaurs` (env.txt:97); judge dino looks in Play mode at those beats.

## B6. Characters (park_guide, visitor_*): are they placed?

- Assets exist: `Assets/Art/Characters/{park_guide,visitor_man,visitor_woman,visitor_teen,visitor_child}/<name>.fbx` (7.5-9.5 MB, Tripo rigs with embedded clips idle/walk/clap/look_around/phone_standing/play_mobile_game per `strings`), 2048x2048 BaseColor/Normal/MaskMap, materials `Assets/Art/Materials/Characters/MAT_*.mat` (HDRP/Lit, MaterialID 1 standard, smoothness remap 0-1, no SSS: MAT_park_guide.mat:11-210).
- **They are never placed**: zero C# references to `park_guide`/`visitor_*`, zero prefab/text-scene references to the 5 MAT_* GUIDs, and none appears in the in-scene material dump. Only `WorldMaterialPalette.cs:37` lists `Assets/Art/Characters` as a search root.
- The Godot original spawned 16 people in the opening (8 strolling the glass facade x -92 -> 18 at z 38-41, 5 at the viewing rail z 94.6 playing idle/phone/look/cheer, 3 gallery visitors at x 52.5 z 49-55: `~/CretaceaBlackout/scripts/cutscene/opening.gd:39-58`, heights man 1.8 / woman 1.68 / child 1.25 / teen 1.74 / guide 1.8). Unity's `OpeningCutsceneRoutine` (StoryDirector.cs:428+) dropped them => the day intro is lifeless.
- Character textures cap at 2048 (`/characters/` is not in the 4096 token list, CretaceaPbrImporter.cs:69-72).

## B7. Visible dino problems + fixes

From `gemqa_small/50_dino_raptor_roar_jaw.jpg` and `55_dino_trex_roar_jaw.jpg` (older daylight test yard) + data above:
1. Plastic/flat hide: uniform smoothness (A channel spread ~0.07 on raptor) + blank AO => even sheen across scales and folds, gold-lacquer belly on the T. rex. Fix: rebuild MaskMap G from a real AO bake (or derive cavity from the normal map) and add variation to A (scales rougher 0.25-0.35, folds/wet areas 0.55-0.7); set `_AORemapMin` ~0.4 once AO exists; add an HDRP `_DetailMap` (tiling 8-16, scale-pattern normal + smoothness) for close-ups; zero mask R (metallic) or set `_MetallicRemapMax 0` (brachio has metal = 1 spots).
2. Texture smearing on raptor throat and T. rex limbs from rig stretch (audit 64-76). Fix needs re-weighting/re-topology upstream; short term keep cameras off the raptor throat in cutscenes.
3. One material for skin + teeth + claws + eyes: SSS/transmission on teeth and claws, matte eyes. Fix: author `_SubsurfaceMaskMap` (teeth/claws/eyes = 0) and boost eye UV islands in A to ~0.9; ideally split eyes into a separate submesh with an HDRP Eye or high-smoothness Lit material.
4. No wet response in a storm game. Fix: per-instance `MaterialPropertyBlock` driven by the rain/wetness global (raise `_SmoothnessRemapMax` to ~0.9 and darken `_BaseColor` ~15 % when outdoors in rain); note SSS MaterialID cannot use Coat.
5. SSS radius tiny for the creature scale (scatteringDistance (0.55,0.18,0.11) x 1.15 with worldScale 1) => SSS visually negligible at gameplay distance; transmission tint (0.65,0.22,0.14) with thickness remap 0-5 mm may glow orange on thin parts under the 85,000 cd lamps (not verified). Tune scattering x3-5 and thickness remap after exposure is fixed.
6. No LODs, `updateWhenOffscreen` true on all (CPU skinning of off-screen dinos). Visual impact low; add LODGroup in `BuildDinoPrefab` if perf is needed for the 2 brachios + 3 trikes + T. rex in the intro.
7. Hero T. rex skeleton uses the FBX material without mask map (flat 0.5 smoothness bone). Fix: create `MAT_TrexSkeleton` (albedo + normal + `TrexSkeleton_MaskMap`, smoothness remap 0.05-0.35, MaterialID standard) and assign through the FBX importer's material remap (ModelImporter `AddRemap`/`SearchAndRemapMaterials`) so both PF_TrexSkeleton and the lobby instance pick it up after rebuilds.
8. Visitors missing (B6): restore the opening crowd with the 5 character FBXs (needs Animator controllers; not built by any script yet).

## How to change safely (gotchas)

- UI is rebuilt from C# every Awake; scene rebuilds never touch layout. Restyle in CretaceaUiFactory (palette 19-31, CreateText 177-210, CreatePanel 126-175, CreateButton 212-263); per-widget numbers live in HudController/MenuController/JournalController Build* methods.
- Fonts: just create TMP SDF assets at `Assets/Resources/UI/Fonts/{BarlowCondensed-Bold SDF, Barlow-Regular SDF, CourierPrime-Regular SDF}.asset`; loaders (CretaceaUiFactory.cs:76/88/98) cache the first hit in static fields (`_cachedHeaderFont` etc.) and **Enter Play Mode Options disable domain reload** (ProjectSettings/EditorSettings.asset:29-30 = 1/1), so the cached LiberationSans survives Play restarts until a script recompile (touch a script or reload the domain). Add LiberationSans SDF as fallback to keep em dash/arrow glyphs.
- UI texture import is forced by `Assets/Editor/CretaceaPbrImporter.cs:145-165` on every (re)import (any path containing /ui/, /hud/, /documents/, /docs/, /maps/, /icons/, /fonts/): Sprite, **mips off, max 2048**. Inspector changes get reverted; edit the postprocessor instead (Assets/Editor is ignored by it, 23-30).
- Dino materials/prefabs are regenerated by `cretacea build_dino_prefabs --overwrite true` (DinoAnimatorBuilder.cs:21-101): material remap/MaterialID/SubsurfaceMask reset (889-905), prefabs rebuilt from the FBX (982-1076). Make visual changes in that code (or in properties it does not touch: detail/thickness/subsurface-mask maps survive).
- `build_all_scenes` re-wires DinoSpawner prefabs (CretaceaWorldAssembler.cs:984-1017) and the 3 HUD/Journal sprites (945-970); MainWorld.unity is binary, so verify scene refs in the editor, not by grep.
- Dinos only exist at runtime (spawned per story beat, B5), so material checks need Play mode at Intro (meadow), Lobby/Wing (raptors indoors), Valley/Substation (T. rex), OldYard (raptors).

## Top issues (summary)

UI: (1) no SDF font assets => all text LiberationSans faux-bold (CretaceaUiFactory.cs:70-102); (2) flat 2x2 white slabs, no border/radius/shadow (126-175, 389-397); (3) secondary text 13-17 px at 1080 ref, illegible at 720p (HudController.cs:323-353, 401-428; MenuController.cs:367); (4) cool teal/amber sci-fi palette, unlike the warm Godot theme (CretaceaUiFactory.cs:19-31 vs ui_theme.gd:53-130); (5) docs/maps import without mips, max 2048, BC1 + small doc modal (CretaceaPbrImporter.cs:145-165; MenuController.cs:585-591); (6) no brightness/exposure calibration in Settings (MenuController.cs:457-470); (7) unused HUD art and park logo; invisible button focus (CretaceaUiFactory.cs:239-246). Prior blueprint claim that HUD art + OFL fonts are used (prev_agent_unity_aaa_blueprint.md:379) is false.
Dinos: (1) blank AO + uniform smoothness => plastic hide; (2) one material for eyes/teeth/skin; (3) rig stretch smearing; (4) no wetness; (5) skeleton prop without mask map; (6) visitors never placed; (7) identical clone sizes, no LOD.
