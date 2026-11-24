# Round 6 package: the valley and the storm at night

Clone: `~/CretaceaUnityClones/kit`. Branch `r6/valley`.
You own: `Assets/Editor/WorldBuilder/ValleyNightDressing.cs`, `Assets/Editor/LookPass/LookLights.Valley.cs`, NEW
components of your own under `Assets/Scripts/Rendering/` (for example a lens halo or a rain-splash emitter),
`Assets/Scripts/Rendering/LightningBoltController.cs`, in `Assets/Editor/LookPass/LookWeather.cs` the valley-road
water and the rut/puddle decal materials (`MAT_WetRutDecal` and its siblings), in `Assets/Scripts/Core/AtmosphereController.cs`
ONLY the lightning flash code (never `GetKeyLook`), in `Assets/Editor/LookPass/LookAtmosphereProfiles.cs` ONLY the
storm profile block, in `Tools/lookdev/route_test.py` only the timing of shots 15, 17 and 18. Not the T. rex AI, not
`Assets/Scripts/Story/**` (requests for the lead).

Read `docs/review4/night_a.md` and `docs/review4/night_b.md`, then look at
`~/CretaceaUnityClones/vfx/Screenshots/review4/presets/NightStormBlackout_valley_eye.png`, `review4/route/17_*`,
`18_*`, `review4/extra/valley_strike.png`. Free-camera storm frames need the weather anchor (`capture_set.sh` does it);
Play-mode frames come from `nightlab.sh` at a blackout checkpoint (`Paddock`).

Goals, in order:
1. **Major — between strikes nothing in the valley draws the eye** (NightStormBlackout_valley_eye; target from
   review 3: something at 120/255 or more in every valley frame without lightning). Camera-facing additive halos on
   the obstruction lenses (about 1.5–2 m, deep red, reading 6–10 px at 120 m) with the lens itself at 6–8 nits so it
   stays red; the bridge flashers with a stronger pulse and a visible amber pool on the deck; the storm `fogColor`
   trimmed (about x2.3–2.4) so the near grass separates from the haze. Judge it from Play-mode frames with the torch
   off and lightning held.
2. **Major — the valley road looks dry under the torch and between strikes** (17_ch4_valley_entry): an animated
   rain-ring normal on the rut and puddle decals (like the gate puddle), and splash/ripple particles on the ground
   inside the torch cone and round the player (one GPU particle system that follows the player, no collision;
   at most 0.3 ms).
3. **A strike flattens the valley into grey-blue** (valley_strike minor): put the strike-side cloud glow low behind
   the treeline (`LightningBoltController.BuildGlow`) so the trees stand out against it, and lower the flash light's
   volumetric dimmer so the near air does not fog over.
4. If time: the chevron boards at the reveal are the brightest thing in frame and clip (18 minor); the knocked-down
   bollard reads as a dropped flashlight (17 minor).

Evidence: same-view before/after of the valley eye (torch off, lightning held), 17_ch4_valley_entry (torch on), a
6–10 s motion clip of rain rings and splashes in the torch on the road, valley_strike; perf at the valley entry.
