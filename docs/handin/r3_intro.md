# Round 3 hand-in: the opening cinematic (staging, crowd, title cards)

Branch `r3/intro` (base 5c57ee7). Captures: `Screenshots/handin_r3/` (16 PNGs + INDEX.md, before/after pairs of the
views review 2 named). All four goals of `docs/briefs/r3_intro.md` are done and verified in Play-mode game views.

## What changed and why

1. **T. rex money shot (major, intro_13)** — `StoryDirector.SpawnOpeningShowcaseDinos` / `OpeningCutsceneRoutine`.
   The rex now waits in the palms at (71,-45.5) and walks 14.6 m at 3.4 m/s to its mark (66.8,-59.5), on it at about
   4.3 s of the 6 s gallery shot, then `ScriptHoldAndLookAt` turns it square to the glass and the crowd. Shot 4 is
   re-aimed at its path (camera (45.2,1.65,-55) -> (46.6,1.7,-58.2)); shot 4b (the roar) looks straight down the middle
   pane from (49.6..50.6, 1.75, -59.5): head and chest framed between the mullions at z -61 and -58, about half the frame
   high, crowd heads in the bottom corners. The mark is clear of the gallery roof's shadow, so the sun (WSW, 27 deg,
   behind the lens) lights the head and chest. Roar and gasp sounds moved to the new positions.
2. **Sauropods (major, intro_09/02)** — shot 3 now looks south from just above head height (camera (-49,2.05,-90.3) ->
   (-48,2.2,-90.5), look y 8.6-9.6, FOV 50->48). A brachiosaurus starts at (-35.5,-121.5), walks to (-40.5,-119) in the
   sunlit meadow (~29 m from the lens, outside the lone tree's shadow band) and turns its head to the rail; its neck and
   head break the skyline. Three triceratops graze in the right mid-ground. Shot 1's glide now stops short of the herd
   (it used to fly through the anchor point) and a second brachiosaurus crosses the middle of the frame in profile, front-
   lit, with two trikes grazing nearer. Visitors are kept out of shot 3's frame (the "or not at all" option): heads cut
   by the bottom edge were the complaint, and the lens height needed to show the animal cannot also show whole people at
   the rail. Total intro actors: 2 brachiosaurus, 5 triceratops, 1 T. rex (all despawned at the end, as before).
3. **Crowd (major, intro_05)** — `ParkVisitorCrowd.Layout` goes from 16 to 55 people: facade 29 (a queue snaking between
   the entrance belts, a family at the map board, buyers at both ticket windows, a tour group of six around the guide,
   ten strollers on lanes that clear the kiosks, queue rails, map board and posts), rail 9 (just outside shot 3's frame),
   gallery 15 (12 pressed against the glass, three in a second row, the middle pane kept clear). Per person: height
   +/-6 % (+/-8 % children), one of seven mild sRGB tints via MaterialPropertyBlock on `_BaseColor`, random clip phase and
   a 0.88-1.12 clip rate for standing people. `ParkVisitor` scales the walk speed with the height so feet do not skate
   (`Stand` got an optional `clipRate` parameter at the end; signatures otherwise unchanged).
4. **Title cards (major, intro_19/17/14)** — `HudController` title-card code only: every card is one lower-left block
   (104 px from the left, bottom at 212 px on the 1920x1080 canvas: above the letterbox and the HUD chips) on a dark
   rounded plate with an amber edge bar, sized to its text; heading / amber rule / amber mono line stacked bottom-up. The
   text column is capped at 560 px (headings shrink, the mono line wraps), so the plate never reaches the centred
   sound-caption box (checked with "[THUNDER CRACKS OVERHEAD]" over the 23:05 card). "Closing time" is now a heading
   ("CLOSING TIME"); "23:04" stays (the second opinion dropped "two stamps" as a problem) as a heading in the same block.
   Chapter cards and the dawn card use the same block.
5. Shot 6's camera and the dusk-to-night transition are untouched.

## Files touched
- `Assets/Scripts/Story/StoryDirector.cs` — `OpeningCutsceneRoutine`, `SpawnOpeningShowcaseDinos`, new private helpers
  (`IntroGround`, `YawTowards`, staging constants and two actor fields) next to them. Nothing else in the file.
- `Assets/Scripts/AI/ParkVisitorCrowd.cs`, `Assets/Scripts/AI/ParkVisitor.cs`
- `Assets/Scripts/UI/HudController.cs` — `BuildTitleCard`, new `SetTitleRect` / `LayoutTitleCard`, `TitleCardRoutine`.
- Helpers: `Tools/agent/intro_seq.sh` (timed intro capture), `intro_pos.sh`, `intro_probe.cs`, `intro_rex.cs`,
  `intro_crowd_probe.cs`.
- No lamp rules added. No prefab or scene changes (PF_OpeningCrowd is unchanged; the layout is code).

## Verification
- Recompile: 0 errors, 0 warnings. Final `build_all_scenes` with NavMesh: OK, no warnings.
- `route_test.py`: 45 / 45 in 296 s. `verify_playmode_doors`: 18/18 doors, 5/5 documents, both NavMeshes valid.
- Fresh Play session, whole intro captured (14 frames): ends in the security office, objective "Take your tactical
  flashlight from the security desk.", cutscene flag off, 0 console errors / 0 warnings during the run.
- The only console error seen in the session is at editor start, before any of my code ran: the texture importer
  (`CretaceaPbrImporter.cs:247`, "RGBA 64 bit is not valid ... texture type Default") — pre-existing.
- Perf (`perf_here.sh`, hidden editor, during shot 4 with all 55 visitors + 8 dinos): GPU 6.4 ms, CPU 22.2 ms, 5.38 M
  tris. The "before" sample (stashed scripts, same moment) came back empty (0.0 ms, 5.94 M tris: the sampler caught a
  stall), so I have no valid before number. Not re-measured for lack of time; the added cost is 39 skinned visitors
  and 4 extra dinos, cutscene-only.

## Not done / rough edges (requests for the lead)
- Not in my files: glass reflections or smudges on the gallery glass (the critic wants the crowd visibly behind glass);
  kiosk 1's blank back (Zone1 builder); the night-aerial plaza probe white-out, rain on cutscene cameras, shot 5 sky,
  troffer shadow steps, the desk primitives (all other packages or the lead).
- "Stragglers walking to the exits in the Closing-time aerial" (second-opinion suggestion): not done; the crowd still
  leaves at the cut. The shot-3 brachiosaurus and the trikes do stay visible in that aerial until dusk, which helps.
- The shot-3 brachiosaurus is side/back-lit (the sun stands to the right of the lens): it reads by silhouette against the
  sky rather than as a fully sunlit body. Turning the camera to put the sun behind it would aim the shot at the forest.
- Two people in a group may overlap shoulders slightly in some frames (queue spacing 0.8-1.0 m); none clip props that I
  could find (bounds probe + captures), but merged static meshes make the probe coarse.

## Fix round (art director's review of 671bfa3)

Captures: `Screenshots/handin_r3/` (`NN_before_*` = base 5c57ee7, `NN_prev_*` = the reviewed hand-in, `NN_after_*` and
18-25 = this round; INDEX.md). All "after" frames come from one fresh Play session after the final NavMesh build.

**Corrections to the first hand-in's claims.** The crowd was 54 people, not 55 (facade 31, rail 9, gallery 14). The first
objective reads "Take your flashlight from the desk in the Security Office." (the notes quoted another text). The kiosk
windows were described the wrong way round (kiosk 0 opens WEST, kiosk 1 EAST). The rail group was never on screen. The
perf "after" was only shot 4, hidden editor, 1280x720, with no valid "before". The crowd is now 48 people (facade 28,
rail 5, gallery 15).

1. **[high] Shot 3: backlit head-on brachiosaurus, no trikes** — confirmed (critic's analysis of the 3 m agent stop + the
   hold toward the lens was right). Restaged on the critic's plan: the brachiosaurus walks (-22,-116) -> (-16,-102),
   starting at the beginning of shot 2 at 1 m/s so shot 3 catches it mid-stride (18) and it settles on its mark at the
   end (08). New `IntroWalkToMark` coroutine (my helper next to `IntroGround`) aims 4 m past the mark and holds the
   animal when it reaches the mark, so the agent's stopping distance no longer matters (NavMesh or direct steering).
   The hold looks straight ahead of the walk (body ~93 deg to the lens: profile, never turned to the camera). Camera
   (-49,2.8,-90.3) -> (-48,2.9,-90.5) looking (-18.5..-18, 8.6..9.2, -104). West flank in the WSW sun. Trikes moved to
   18-24 m, 17-24 deg right of the axis; one reads clearly lower right, overlapping the brachiosaurus's tail tip; the other
   two are mostly hidden behind it and the grass (rough edge, see below).
2. **[high] T. rex stops 2.6 m short; mullion through it; roars off-centre, shaded, snout up** — confirmed. The rex now
   uses the same `IntroWalkToMark` (hips on (66.8,-59.5)). Shot 4 ends square on the pane: toPos (46.6,1.7,-59.5), look
   (64.5,2.0,-59.5) (19). Probe (22): at the mark, facing the lens head-on, the chest is in its own shade (~55/255, 21);
   turned south-west (hold toward (56.8,2.5,-71), ~50 deg off the 4b axis) the flank reads 88-149/255 and the open jaw
   shows in 3/4. Final roar (12): rex inside the middle pane between the z -61/-58 mullions, jaw open, lit. The mark was
   kept (probes at x 64.5 and panes z -56.5/-62.5 were no better lit and cut the head on a mullion).
3. **[medium] Five cloned outfits, gallery lineup, unseen rail group, empty closing plaza** — fixed.
   - `Tools/agent/visitor_variants.py` keys the clothing pixels of each visitor albedo by hue/saturation/value (blue
     polo, white dress, grey hoodie, red jacket; skin, hair, shoes and khaki untouched) and writes three recolours per
     model (1024 px JPEG) into `Assets/Prefabs/Characters/Resources/VisitorVariants/`; `visitor_variants_mats.cs` makes
     a copy of each model's material with only `_BaseColorMap` swapped (12 materials). `ParkVisitorCrowd` deals the four
     looks per model in turn from a random start (neighbours differ) and no longer uses a MaterialPropertyBlock (SRP
     Batcher compatible again). The guide keeps its uniform.
   - Gallery: four groups of 3-4 people 0.6-1.6 m off the glass with 2-3 m gaps, three people turned to each other
     (teen to the woman, woman to the teen, man to the guide), two in a second row.
   - Rail: the 4 people who were never on screen are gone; 5 stand at the rail 15-19 m down the shot-3 lens, small whole
     figures in the lower left (08).
   - Closing: `BeginClosing(3.5 s)` (called from shot 5 via SendMessage, Story does not reference the AI assembly; also
     triggered by the crowd itself when the atmosphere leaves GoldenDay during the cutscene) removes everyone but the 7
     strollers, who keep walking through the first 3.5 s of the aerial (14; they are tiny at that distance). A skip or
     chapter jump still removes the crowd at once.
4. **[medium] Ticket buyers at the kiosks' blank backs** — confirmed (yaw 90 puts kiosk 0's window on the west).
   Buyers moved to the window sides: kiosk 0 (-52.4,-41.7)/(-52.9,-42.7) facing (-51.5,1.4,-42), kiosk 1 (-11.6,-41.6)/
   (-11.0,-42.6) facing (-12.5,1.4,-42). Request for the lead below (flip the yaw) to also cure the blank back seen
   from shot 2.
5. **[medium] Shot 4 ceiling 25-30 %** — look targets lowered to y 2.0 (level lens): ceiling + lintel ~13 % of the
   active band in 10 and 19. Shot 4b look lowered to y 3.4-3.8 so the adults at the bottom corners show heads and
   shoulders.
6. **[medium] Title-card layout** — `TitleCardRoutine` moves "CHAPTER n" of "CHAPTER n — NAME" headings to the mono line
   (all chapter cards now have the same shape; 23/24); `LayoutTitleCard` breaks a too-wide mono line at the em dash
   ("Night Shift" / "Cretacea Park Visitor Center", 16/25), measures the widest line, drops the fontSizeMax/fontSize
   factor, and the heading may shrink to 34 px. At the owner's 2313x1486 view the 23:05 plate ends ~586 canvas px vs the
   caption box at ~733.
7. **[medium] No valid perf pair** — measured with `Tools/agent/perf_intro.sh` (game view 1920x1080, hidden clone
   editor; it could not be focused from the shell), median of 8 samples in shot 2 (t 12.6-19.5) and 6 in shot 4
   (t 28.3-33.5). Before = the four scripts of 5c57ee7, same session and build:

   | | shot 2 GPU / CPU / tris | shot 4 GPU / CPU / tris |
   |---|---|---|
   | before (5c57ee7 scripts) | 4.3 / 14.9 ms / 3.81 M | 5.1 / 13.8 ms / 3.86 M |
   | this round, all groups always on (2 runs) | 5.8-4.4 / 14.4-16.0 ms / 5.48 M | 5.8-5.3 / 15.1-14.9 ms / 5.65 M |
   | final (per-shot crowd groups) | 6.1 / 14.0 ms / 5.48 M | 6.7 / 13.6 ms / 5.65 M |

   Shot 4 CPU grew by ~1.2 ms with every group on, so each group now draws and animates only while the camera is near
   it (facade unless the camera is in the gallery, rail within 40 m of the rail lens, gallery inside the gallery;
   renderers + animators toggled, animator state kept). Final CPU is at or under the "before" numbers. CPU 13.6-16 ms is
   the editor's own frame (it was 13.8-14.9 before any of this work). GPU +1.6 ms is the 8 dinosaurs and the crowd; the
   hidden editor's GPU numbers jitter by ~1.5 ms between runs.
8. **[low] Stroller lanes** — rebuilt: lane z -45.1 eastbound (3), z -38.3 westbound east of the queue rails (3), z -37.6
   westbound west of the cafe (1); one direction per lane, starts >= 6 m apart, the walker in front the fastest,
   segments 33-64 m (no turn before ~41 s). `Tools/agent/lanes.cs` capsule-casts (r 0.3 m) each lane against the built
   scene: 0 hits (the same cast through the kiosks hits 2, so it sees colliders; bollards and cafe tables have colliders).
   The child starts at x -46 so it no longer passes the shot-2 lens.
9. **[low] Child's head in the 4b letterbox** — the slot next to the clear pane is an adult (man at -61.5), the child is
   at (55.3,-63.4), ~40 deg off the 4b axis (outside the ~35 deg half-FOV); 12 shows only adults at the corners.
10. **[low] Hand-in claims** — corrected above.

Files touched this round: `Assets/Scripts/Story/StoryDirector.cs` (OpeningCutsceneRoutine, its staging constants,
SpawnOpeningShowcaseDinos, new IntroWalkToMark / CloseOpeningVisitors helpers), `Assets/Scripts/AI/ParkVisitorCrowd.cs`,
`Assets/Scripts/UI/HudController.cs` (title-card code only), new `Assets/Prefabs/Characters/Resources/VisitorVariants/*`
(12 JPEG albedos + 12 materials), helpers `Tools/agent/visitor_variants.py`, `visitor_variants_mats.cs`, `lanes.cs`,
`rexprobe.sh`, `perf_intro.sh`, `sheet.py`. `ParkVisitor.cs` unchanged this round.

Verification (this round): recompile 0 errors / 0 warnings; final `build_all_scenes` with NavMesh: OK, warnings [],
both NavMeshes baked; fresh Play session through the whole intro: ends in the security office with the objective above,
console 0 errors / 0 warnings; `verify_playmode_doors` 18/18 doors, 5/5 documents, both NavMeshes valid; `route_test.py`:
45 / 45 in 260 s (no new console errors during the run).

Requests for the lead: flip the ticket-kiosk yaw in `Zone1_VisitorCenterBuilder` line 2535 to `k == 0 ? -90f : 90f` so
both windows face the axis and the shot-2 lens (then move the four buyers in `ParkVisitorCrowd.Layout` back to
x -48.7/-48.2 and -15.3/-15.9). Gallery glass reflections/smudges are still the lead's (look system).

Rough edges: in shot 3 only one of the three rail trikes reads clearly (the others sit behind it and in the grass) and it
overlaps the tip of the brachiosaurus's tail; the rail group stands low in the frame (whole figures, feet near the
letterbox). The stragglers in the closing aerial are a few pixels tall. Not checked in a standalone build.
