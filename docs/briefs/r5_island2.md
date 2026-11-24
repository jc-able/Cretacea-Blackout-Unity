# Round 5 package: island, second pass (the round 4 critic's findings)

Clone: `~/CretaceaUnityClones/terrain`. Branch `r5/island2`.
You own the same files as round 4 (`docs/briefs/r4_island.md`). The lead already fixed the critic's top finding
when merging: the pond trees are now scattered like every other tree and removed after the whole scatter, so the
forest no longer re-rolls (`LookVegetation.ScatterPlayAreaTrees` / `Scatter`; keep that). Read the critic's report
in `docs/handin/r4_island_critic.md` first.
Goals, in order:
1. **Dusk hill mosaic** (`DuskTransition_meadow_eye`, `DawnCoast_valley_eye`, `DuskTransition_valley_eye`): the
   impostor caps still read as a faceted mosaic of lit plates from eye level. Make them fade by view elevation (an
   HDRP Shader Graph for the impostor is allowed, in `Assets/Prefabs/Foliage`), or the critic's no-shader fallback
   (rim 0.55-0.70, centre capY-0.10h, droop 0.30-0.40h, plus card planes). Accept only if the aerial stays green
   (`GoldenDay_aerial_sw` far canopy luminance at least 0.30, hue at least 80).
2. **Valley band from the air** (`GoldenDay_valley_air`): a soft dark ground blob under each impostor so the band
   has shadows at any range; crown clusters G/R at least 1.15 and hue at least 75; no brown trees at dawn.
3. **Tall grass**: main stand about 0.45 saturation with lighter tips, straw seed heads less saturated and shorter,
   no straight front near the road; capture dusk, dawn and a storm frame too.
4. **Ending belt**: palms and tree ferns with their own bodies and crown clearance, jungle trees capped at about
   22 m, the belt only where the crane shot looks.
5. Minors: lawn tint and noise, orange slab rock and headland shelves to dark volcanic rock, pond rim and reeds,
   texture compression, perf before and after in the valley and the meadow.
