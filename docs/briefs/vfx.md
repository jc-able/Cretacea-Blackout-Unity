# Package: WEATHER AND EFFECTS  (branch `look/vfx`)

**Problem.** The storm is the game's atmosphere and it is the weakest effect in it: rain streaks are unlit,
fixed-brightness scratches that ignore light and fog; the roof occlusion never worked under HDRP (the lead has put
in a stop-gap: no streaks are spawned while the camera is under a roof); "wet" materials do nothing; no surface
reacts to rain; there is no ground mist, no dust in a flashlight beam, lightning is only a light.
Read `docs/maps_2026-11-20/digest_rendering_player.md`, `notes_rendering_player.md` (rain, wetness, viewmodel,
probes) and `digest_materials.md` (custom shaders, wetness).

**You own:** `Assets/Scripts/Rendering/StormRainOcclusionController.cs`, the shaders
`Assets/Shaders/CretaceaRainParticle.shader`, `CretaceaRainOcclusionDepth.shader`, `CretaceaWetSurface.shader`,
`Assets/VFX/*`, `Assets/Settings/Rendering/MAT_Rain*.mat`, `MAT_WetPuddle*.mat`, `Assets/Art/VFX/*` (new textures you
generate locally), new runtime scripts under `Assets/Scripts/Rendering/` (wetness driver, mist, lightning bolt, dust
motes), new `Assets/Editor/LookPass/LookWeather*.cs` (placement of fog volumes, puddles; call it from
`LookPass` with two lines in `LookPassCli.cs`: a `weather` step in `Run` and in `RunOnBuild`).
Weather values come from the atmosphere controller through shader globals: `_AtmoRainTarget` (0–1),
`_AtmoWetnessTarget` (0–1), `_LightningFlashIntensity` (0–1 during a strike), plus the legacy `_RainIntensity`,
`_GlobalWetness`. Do not edit `AtmosphereController.cs`; if you need another global or a public property, write it
in your hand-in as a request.

## Goals, in priority order
1. **Rain that lives in the light.** Streaks must be visible where there is light and vanish where there is none:
   bright in the flashlight cone and around lamps, a silver sheet against the sky during lightning
   (`_LightningFlashIntensity`), faint blue elsewhere, fading into the fog. An HDRP-compatible way of your choice
   (HDRP Lit/Unlit particles with exposure handled correctly, or sampling the flashlight cone in the shader from
   globals you set: position, direction, angle, intensity). Rain direction and density follow wind and
   `_AtmoRainTarget`. No streak may ever be brighter than the lamp that lights it.
2. **Never indoors, always outside the window.** Replace the dead `RenderWithShader` roof map with something that
   works in HDRP (e.g. a custom pass rendering the RainBlocker layer's depth from above into the RT the shader
   already expects, or `CustomPassUtils.RenderDepthFromCamera`), so rain stops at roofs but is still seen falling
   outside from inside a room. Remove the stop-gap `_coverBlend` only when the real occlusion works.
3. **Surfaces answer the rain.** Splashes and ripple rings where drops land, drips from roof edges and canopies,
   puddles in the low spots of roads and slabs (decals or a puddle layer driven by wetness), and a wetness driver:
   at runtime, materials used outdoors get wetter (smoothness up, albedo slightly down) as `_AtmoWetnessTarget`
   rises and dry out for the dawn — without ever modifying the material ASSETS (clone at runtime, or property
   blocks) and without touching indoor rooms.
4. **Air.** Low ground mist in the valley and along the stream, thin haze in the big interiors (lobby, warehouse),
   dust motes drifting in the flashlight beam indoors (few, small, lit by the beam). `LocalVolumetricFog` volumes
   and tiny particle systems, placed by your `LookWeather` step. Keep it subtle: mist must never white out a
   flashlight beam.
5. **Lightning you can see.** A brief sky flash and, some of the time, a visible forked bolt in the distance
   (procedural line/mesh or flipbook) synchronised with `_LightningFlashIntensity`.
6. Sparks at the broken shutter and lamp: lit, with a short glow on nearby surfaces.

## Verify
Play mode at night: `jump_chapter ServicePaths` / `Substation` (outdoors, flashlight on and off, during a strike:
`Cretacea.Core.AtmosphereController.Instance.TriggerLightningFlash(1f, false)` then capture within 0.1 s, or set
`Shader.SetGlobalFloat("_LightningFlashIntensity", 1f)` for a still), `Corridor` and `Night` (indoors: no rain,
dust motes), a window view from inside. Day and dawn: no rain, drying surfaces. Performance: report
`unity command diagnostics get_performance` before and after in the same view; the weather may cost at most ~1.5 ms.
A hunting dinosaur will kill the test player and put a red death screen over your captures: call
`Cretacea.AI.DinoSpawner.Instance.DespawnAll()` after each jump, or use free-camera captures.
