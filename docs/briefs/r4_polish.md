# Round 4 package: interface and cinematic polish (HUD, menus, tablet, captions, intro staging)

Clone: `~/CretaceaUnityClones/ui`. Branch `r4/polish`.
You own: `Assets/Scripts/UI/**`; in `Assets/Scripts/Story/StoryDirector.cs` only `OpeningCutsceneRoutine` with the
helpers it calls for the intro actors and cameras, and the caption calls inside `DawnCoastEndingRoutine` (another
package edits the T. rex reveal routine in the same file: stay out of everything else there);
`Assets/Scripts/AI/ParkVisitor*.cs`, `Assets/Prefabs/Characters/**`.

Read `docs/review3/ui.md` and `ui_check.md`, then `intro.md` and `intro_check.md`, then the two caption items in
`night_b_check.md` (28_ending_1).
Interface:
1. Controls page (`ui/controls`): the SHIFT legend is 7 px tall at 720p and E, F, C are 9 px against 11-12 px for the
   other keys: one legible size for every legend; the alternating row bands are solid tan-grey and read as a
   spreadsheet: make them about a quarter as strong.
2. HUD flashlight chip (`ui/hud_corridor`, `02_ch1_flashlight_in_hand`): the 'beam on' lamp looks like an off-centre
   eclipse: centre the dot in its glow. The TAB / FIELD TABLET hint hugs the objective banner's bottom edge (2 px
   under it, 13 px above): even padding.
3. Document reader (`ui/document_reader`, `11_ch2_manual_open`): the numbered items 7.1-7.4 of the transcript run
   together: paragraph spacing.
4. Tablet (`ui/journal_tab0`, `tab1`, `tab2`): the YOU dot and its heading wedge cover the room label under it; the
   LOADING DOCK label touches the room's top wall; the documents list uses 'NOT FOUND' as the title of unfound
   documents and words the same state three ways: one wording, and name the evacuation manual the objective asks for.
5. Art direction (`ui/pause` and the other cards): pause, settings, controls and the tablet are the same flat
   near-black cards, closer to a modern app than a 1996 park-security kit. Bring the park badge, the stencil face and
   some worn hardware character into these screens without hurting legibility. Keep it restrained.
6. Ending captions (`28_ending_1`): the caption box sits inside the picture, on the rail, directly under the hero
   boat. Keep captions in the lower safe area, clear of the subject.
Cinematic:
7. The rail shot (`intro/intro_08`, `intro_09` in `_refs/final`): the trikes still overlap into one long animal.
   Stage three clearly separate animals at different depths, none hidden behind another, none touching the
   brachiosaurus's tail, and check the whole shot, not one frame.
8. During the opening glide the rail brachiosaurus, waiting by the plaza, lines up with the shot-1 animal so the two
   read as one two-headed animal (`intro_03` x 880-990): keep the rail animal out of sight until its shot.
9. The '[THUNDER CRACKS OVERHEAD]' plate covers the flashlight the objective asks for in the first gameplay frame
   (`intro_20`), and no intro frame shows lightning although the radio and the caption sell a storm: fire the flash
   and its caption during the first night shot, not as the cutscene ends. The first gameplay view gives its top third
   to the ceiling and three troffer lenses: start the player looking slightly down at the desk and the flashlight.
10. The closing glide (`intro_14`-`16`) turns day into night without the sky ever appearing: let the camera show the
    storm front and the dusk sky before it settles on the plaza.
Not yours (report under "Requests for the lead" if you see more of them): light colour, exposure, weather, the
viewmodel arm, room props.
Run `route_test.py` before hand-in (it plays the intro and the ending and reads the HUD).
