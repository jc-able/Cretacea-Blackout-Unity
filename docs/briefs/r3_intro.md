# Round 3 package: the opening cinematic (staging, crowd, title cards)

Clone: `~/CretaceaUnityClones/creatures`. Branch `r3/intro`.
You own for this round: the method `OpeningCutsceneRoutine` and the helpers it calls for spawning and moving the intro
actors in `Assets/Scripts/Story/StoryDirector.cs` (nothing else in that file), `Assets/Scripts/AI/ParkVisitor*.cs`,
`Assets/Prefabs/Characters/**`, the title-card presentation used by the intro (`GameEvents.RaiseTitleCard` consumer in
`Assets/Scripts/UI/HudController.cs`: only the title-card code).

Read `docs/review2/intro.md` (and `_check.md` if present). Capture the intro with
`Tools/lookdev/capture_set.sh Screenshots/x intro` (20 frames, 2.4 s apart; use a fresh Play session) or your own
denser timing.
1. **Major — the intro's money shot misses**: through the whole 6 s gallery shot the window shows only trees, the
   T. rex is a sliver at the left edge; when it roars it sits in the left third, cropped, with a mullion through its
   chest. Stage it: the animal walks into the middle of the window and roars framed between two mullions, large in
   frame, lit by the sun.
2. **Major — "sauropods towering past the viewing rail" shows no sauropod**: only a thin dark neck 58 m away, hidden by
   grass and by heads cropped at the bottom of the frame. Bring the brachiosaurus (and the triceratops herd) into the
   frame at a readable distance and light, and frame the visitors' heads on purpose (lower third) or not at all.
3. **Major — the park looks nearly empty**: 2–4 people per shot, the same five models; a crowd gasp plays over three
   people. More visitors in groups along the facade, the rail and the gallery (variation by scale, tint, pose, offset
   animation phase; keep it cheap: no new packages, no downloads), enough that the park reads as open.
4. **Major — every title card lands on the shot's subject**: "Night Shift - Cretacea Park Visitor Center" runs across
   the wall clock and the pendant lamp, "23:04" sits on the window head, "Closing time" is a lone small line on the
   facade. Place title cards in a consistent lower-third (or upper-left) block with a legible plate, never over the
   subject.
5. Do not change shot 6's camera path (the lead just cleared it) or the dusk-to-night transition (the lead's).
Check the whole intro in a capture sequence before and after; it must still end in the security office with the
first objective, with 0 console errors, and `route_test.py` must stay 45/45.
