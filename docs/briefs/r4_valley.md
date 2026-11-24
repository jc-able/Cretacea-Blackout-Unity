# Round 4 package: Tyrant Valley at night (the T. rex reveal, something bright, a wet road)

Clone: `~/CretaceaUnityClones/creatures`. Branch `r4/valley`.
You own: a NEW file `Assets/Editor/WorldBuilder/ValleyNightDressing.cs` for anything you add to the valley (hook it
with ONE line in `CretaceaWorldAssembler.cs` directly after the Zone 2 build; touch nothing else there) and, if you
add lamps, a NEW table `Assets/Editor/LookPass/LookLights.Valley.cs` (follow `LookLights.Zone23.cs`); in
`Assets/Scripts/Story/StoryDirector.cs` only `TrexStreamRevealRoutine`, the T. rex patrol set-up directly above it
(`TrexPatrolPointsXZ`, the `trex_drink` spawn) and `DelayedTrexInvestigateRoar`; `Assets/Scripts/AI/**` for the T. rex
(and small new components of your own under `Assets/Scripts/Rendering/` or `Assets/Scripts/AI/`); in
`Assets/Editor/LookPass/LookWeather.cs` only the puddle placement on the valley's terrain road; in
`Tools/lookdev/route_test.py` only the timing of shots 15, 17 and 18. Do not edit `Zone2_*.cs` or `Zone3_*.cs`
(another package owns them) and do not change the patrol the stealth section depends on without re-running the route.

Read `docs/review3/night_a.md` and `night_a_check.md`. Use Play-mode game views from the Paddock checkpoint.
1. **Major — the T. rex reveal shows no T. rex** (`18_ch4_trex_reveal`, `extra/valley_road_flashlight`,
   `extra/valley_strike`). The animal is alive but 75-93 m away, about 30 px tall, in haze, behind a tree. This is the
   scare of the chapter. Stage it so a player cannot miss it: the animal crosses or stands in the open gap straight
   down the road, 40-60 m from the trigger point, lit by a lightning strike timed to the reveal (see
   `AtmosphereController.TriggerLightningFlash`), readable between strikes as a silhouette against the sky or the
   stream, with a faint moon-side rim light that follows it and eyes that shine in the torch. The line says 'Something
   massive is moving by the stream.': make the stream read too. Then capture `18` on the strike and keep the patrol
   and the stealth section working (route test 45/45).
2. **Major — between strikes the valley is one dark band with nothing bright in it** (`18`: 99th percentile 72/255,
   0.01 % of pixels above 100). Add highlights instead of lifting the whole band: battery-backed things that are
   plausible in a blackout and that guide the player down Service Road 3. Ideas: a red obstruction beacon on a mast
   toward Substation 3 visible from the valley entry (lens 14-22 nits, a little volumetric glow), amber battery road
   flashers at the paddock crossing, reflective delineator posts and cat's-eyes that light up in the torch (a small
   component that scales an emissive by the flashlight's direction and distance is cheap and reads very well), the
   feeder station's status LED. No shadow-casting lamps. Target in `18`: at least 0.3 % of pixels above 150/255
   between strikes, and a clear depth cue down the road.
3. **Major — in a downpour the valley road looks dry** (`17`, `18`, both valley presets: flat matte gravel at
   20/255, no glint even in the torch pool). Place puddles along the Service Road 3 polyline instead of by noise: one
   every 15-25 m in the ruts and low points, each 2-5 m long, from the gate to the stream, plus two dark glossy rut
   strips (smoothness about 0.8); keep the pebble field. Prove it in `extra/valley_road_flashlight` (pitch +10) and
   under a strike.
4. Minors: time shot 15 inside the raptor's one-second crossing (`route_test.py`); a flat, near-white mist band with a
   straight top lies across the valley (`NightCalm_valley_eye` y 385-410; `18` x 850-1280): find what it is and report
   it (the mist driver is the lead's) or fix it if it is water of yours; under lightning the grass turns teal-mint
   (report only).
If plants or trees block the reveal sight line, do not edit the vegetation files: list the lane (two XZ points and a
width) under "Requests for the lead". Report `perf_here.sh` at the valley entry before and after.
