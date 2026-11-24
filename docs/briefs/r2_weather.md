# Round 2 package: weather (rain, wet surfaces, puddles, storm haze, lightning)

Clone: `~/CretaceaUnityClones/vfx`. Branch `fix/weather`.
You own: `Assets/Scripts/Rendering/{StormRainOcclusionController,WetnessDriver,WeatherLightSampler,LightningBoltController,DustMotesController,RoofOcclusionMap,SparkGlow,TerrainWetnessDriver}.cs`,
`Assets/Editor/LookPass/LookWeather*.cs`, the rain / ripple / puddle / mist / lightning materials, shaders and
textures those files create. (Not the sea, river or lake: the island package owns water bodies.)

Findings to fix (read `docs/review1/night_a.md` and `docs/review1/night_b.md`; yours are the ones about weather):
1. **Rain reads as snow** (major, every storm frame): short, fat, bright-white dashes and dots of one size, spread
   evenly over the frame, also in the dark sky far from any lamp. Make it rain: long thin streaks (0.5–1.0 m, a few
   millimetres wide, motion-blur look), low opacity, size and speed variation, slanted by the wind, dense near the
   camera and thinning out; a streak is only as bright as the light that reaches it (bright in a lamp cone or the
   flashlight beam, a faint blue-grey hint under the moon, invisible against a black sky). Judge it in
   `22_ch5_green_route`, `27_escape_coast_road`, `19_ch5_substation_dark`, `NightStormBlackout_*`.
2. **Flat light-grey rectangles on the ground** (major: `19_ch5_substation_dark` right of the hand, `23_ch6_yard_entry`
   on the road): puddle decals reading as primitive quads. Puddles need irregular soft-edged shapes, a dark wet look
   with sky/lamp reflection, never a lighter-than-ground rectangle.
3. **Wet ground sparkles like glitter / hoar-frost** (major: gravel in `22`, asphalt in `NightStormBlackout_coast_eye`,
   moon-side grass and gravel in `NightCalm_valley_eye` and `18`): specular aliasing on wet rough surfaces under the
   low moon. Wet should be a smooth dark sheen with broad soft highlights. Check the wetness smoothness range, normal
   strength when wet, and specular anti-aliasing on the materials you drive (terrain layers are driven by
   `TerrainWetnessDriver`).
4. **The plaza is bone dry in the rain** (major: `NightCalm_plaza_eye`, storm plaza): no sheen, no puddles, no lamp
   reflections on the paving. The hero exterior must read as wet stone (reference: the Jurassic Park storm night).
5. **The storm does not read as a storm from the plaza** (major: `NightStormBlackout_plaza_eye`): the jungle hill
   200 m away is crisp, no rain haze, no lightning. You can add distance rain haze / rain curtains / wind-driven mist
   as effects (the global fog values are the lead's: request changes), and make sure lightning strikes are visible
   and frequent enough to be seen in a 10-second look (the strike light is the lead's `KeyLook.lightningLux`).
6. Anything else in those two files with area "weather" or about wetness.
Also check the DuskTransition preset (rain 0.25, wetness 0.35) and that nothing of yours shows in GoldenDay or DawnCoast
(no rain, wetness 0.08 / 0.5).
