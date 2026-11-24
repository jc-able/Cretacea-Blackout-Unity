# Package: FIXTURES, SIGNS, ROOFS, PROBES  (branch `look/kit`)

**Problem.** The kit (`WorldArchitectureKit`) builds a world of anonymous boxes: 90 lamps but almost no lamp bodies,
sign plates that are blank glowing rectangles although 20 sign textures exist, buildings whose roofs are grey slabs,
reflection probes captured once with the lights on (so a blacked-out room reflects a lit one) that also see the
invisible rain-blocker slabs. These are systemic, so fix them once, centrally, and every zone benefits.
Read `docs/maps_2026-11-20/digest_assembler_kit.md`, `notes_assembler_kit.md`, and the "fixture / sign / probe" items
in `digest_zone1.md` and `digest_zone23.md`.

**You own:** `Assets/Editor/WorldBuilder/WorldArchitectureKit.cs`, `Assets/Editor/LookPass/LookLights.Kit.cs`, new
`Assets/Editor/LookPass/LookFixtures.cs` (+ other new `LookPass/Look*.cs` files you need), new runtime scripts under
`Assets/Scripts/Rendering/`, and in `Assets/Scripts/Core/LightGroupController.cs` only what is needed to tell probes
that a light group finished changing. Other agents are editing the zone builders right now: **keep every existing
public signature in the kit working** (add optional parameters at the end only).

## Goals, in priority order
1. **Every lamp has a body, every body glows with its lamp.** Add a post-build step `LookFixtures.Apply()` and call it
   from `LookPass` (`Run`: new step name `fixtures`, and in `RunOnBuild` before the lamp calibration is fine either
   way) — you may add those two call lines in `Assets/Editor/LookPass/LookPassCli.cs`, nothing else there. It walks
   the scene's lights by name family and, where no fixture exists within ~0.5 m, builds one next to the light and
   pairs its emissive lens with the light's `LightGroupMember` (`emissiveRenderers`), so the lens dims, flickers and
   dies with the light group. Idempotent (name the generated objects `Fixture_*`, rebuild cleanly on re-run).
   Families (see the lamp table `LookLights.cs` for names):
   ceiling mains (`*_Main`, `Light_Corr_Main*`, lockers, break, gift, records, dock) → recessed troffer: dark frame
   + diffuser panel; emergency units (`*Emerg*`) → wall/ceiling battery unit with twin heads or a caged bulkhead,
   small red lens; storage bulb → cord + bulb; lobby mains → high-bay shades; fossil uplights → floor can lights;
   path lamps → use the `lamp_post` FBX head (move the light under the head if it sits on the pole axis); bollards
   (`bollard_light` FBX exists) → glowing collar; bulkheads → `lamp_bulkhead` FBX with a glowing lens; floods
   (Talon, AuxSub, CourtAux, WH) → flood head + crossarm on the bare mast; workshop pendants → emissive shade;
   strobes/beacons → `beacon_red` FBX or a beacon dome; AuxRoute green bollards → a lens instead of a cube cap;
   station status LEDs stay tiny.
   Lens brightness: palette emissives (`EmissiveWarmWhite/CoolWhite` 170 nits, colours 45–70); a lens is small and
   must not light the room by itself. Fixture bodies must not block their own lamp: lamps use cached shadow maps
   with `shadowNearPlane` 0.12 m — keep the body behind/above the light or mark its renderers
   `shadowCastingMode = Off`.
2. **Signs with words.** `BuildIlluminatedSign` (and any blank sign plate helpers) must show a real sign: map the
   sign's text/id to `Assets/Art/UI/Signs/*.png` (control_room, e0_exit, emergency_exit, feed_prep, fire_assembly,
   hazard_tape, high_voltage, lobby_welcome, paddock4_gate, park_logo, raptor_poster, records, restroom, rex_banner,
   security_office, service_road_3, staff_only, substation_3, talon_ridge_warning, warehouse_b) on a quad with
   `palette.CreateTextureQuadMaterial`; for texts without art, render the text onto the plate (a `TextMeshPro`
   world-space label on a dark plate is fine). Back-lit signs: modest emissive (2–5× the scene white), tied to the
   right light group so they go dark in the blackout unless they are battery exit signs (green running-man style
   exit signs stay lit: they are the wayfinding).
3. **Roofs and exteriors.** `BuildBoxRoomShell`: the ceiling box doubles as the roof; add a roof cap
   (`palette.RoofBitumen` or `CorrugatedMetal`) and a low parapet so buildings read from outside and above. Give the
   RainBlocker slabs a material/setting that never shows in any colour camera or probe (they are occlusion proxies
   on layer 13; the gameplay camera already excludes that layer).
4. **Reflection probes that tell the truth.** Per-room probes: realtime **OnDemand**, culling mask without the
   Viewmodel and RainBlocker layers, re-rendered when a light group finishes switching and at chapter start
   (small runtime component). Add a handful of outdoor probes (plaza, service path bends, substation pad, yard,
   coast road) so wet ground reflects its surroundings, not a lit room. Multiplier 1.
5. Static flags on kit geometry that never moves (`staticShadowCaster`, `BatchingStatic` where safe) so cached
   shadows and instancing work; leave doors and interactables dynamic.

## Verify
Play mode: `jump_chapter Night` (lit office, corridor), `Corridor` (blackout), `ServicePaths`, `Substation`,
`OldYard`, plus free-camera close-ups of each fixture family and of four different signs. Check in a capture that
a lens goes dark when its group is off (Night vs Corridor).
