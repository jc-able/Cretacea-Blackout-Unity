using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Interact;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Builds Zone 3: Tyrant Valley Structures (Culvert Stream Bridge, Watchtowers, Bait Pen),
    /// Substation 3 Compound (X: 250..286, Y = 2.3), East-Only Green Escape Beacons,
    /// and Old Maintenance Yard / 1990s Workshop / E-0 Gate (X: 401..505, Y = 1.0).
    ///
    /// Mathematically eliminates EVERY Zone 3 defect from #7 & #9:
    /// - Substation 3: Clean concrete pad w/ exterior-only gravel apron; ALL machinery grounded flush on pad/cradles
    ///   (diesel_fuel_tank_pump, generator_starter_panel, power_transfer_switch — ZERO floating props, POS-1/POS-3);
    ///   Breaker #2 rotated 0 deg NORTH facing walkway inward (POS-4); wall/post-mounted bulkheads (POS-2);
    ///   ordered green AuxRoute escape lights strictly EAST of Substation 3 (X >= 288 -> 398, POS-8);
    ///   thick exterior DinoBlocker preventing T. rex leg clipping through fence!
    /// - Old Maintenance Yard & Workshop: 6 REAL cut multi-pane north glass windows (DOOR-6);
    ///   workshop_door_e hinged cleanly INSIDE opening X=462, Z=-16.8..-15.2 (DOOR-3);
    ///   partitioned Foreman Office (log_halvorsen 5/5) / Parts Archive / Electrical Closet / Tool Workshop;
    ///   pipes rooted cleanly into walls w/ flush industrial_valve_wheels (POS-5); ceiling-suspended pendant lights;
    ///   ZERO hovering concrete cube above squeeze gap; solid collider on outward bent squeeze door (COL-5);
    ///   Service Exit E-0 double gate leaves swinging OUTWARD EAST (+X) clear of sprint line (POS-16)!
    /// </summary>
    public static class Zone3_SubstationAndYardBuilder
    {
        private const float SubY = 2.3f;
        private const float YardY = 1.0f;

        public static GameObject Build(Transform worldRoot, WorldMaterialPalette palette)
        {
            var root = new GameObject("Zone_03_SubstationAndOldYard");
            if (worldRoot != null) root.transform.SetParent(worldRoot, false);

            BuildValleyStructuresAndCulvert(root.transform, palette);
            BuildSubstation3Compound(root.transform, palette);
            BuildEastOnlyAuxRouteBeacons(root.transform, palette);
            BuildOldMaintenanceYard(root.transform, palette);
            // round 2: one wire plane per fence span / gate leaf on round posts; the coast road run to the overlook
            Zone23Dressing.UpgradeChainlinkSpans(root.transform, palette);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LeafMesh") { Zone23Dressing.ThinChainlinkBox(t.gameObject); continue; }
                // chain-link door leaves of the kit (E-0): one wire plane, the box keeps its collider and moves as before
                var lr = t.GetComponent<MeshRenderer>();
                if ((t.name == "LeafA" || t.name == "LeafB") && lr != null && lr.enabled && lr.sharedMaterial == palette.ChainlinkFence)
                    Zone23Dressing.ThinChainlinkBox(t.gameObject);
            }
            Zone23Dressing.DressCoastRoad(root.transform, palette);

            return root;
        }

        // =========================================================================
        // TYRANT VALLEY LANDMARKS: PASSABLE CULVERT BRIDGE, WATCHTOWERS, BAIT PEN
        // =========================================================================

        private static void BuildValleyStructuresAndCulvert(Transform parent, WorldMaterialPalette p)
        {
            var valley = new GameObject("TyrantValley_Structures");
            valley.transform.SetParent(parent, false);

            // T. rex Stream Reveal Trigger + Drink Spawn Anchor
            WorldArchitectureKit.BuildWorldAnchor(valley.transform, "trex_drink", new Vector3(192f, 0.0f, 40f));
            WorldArchitectureKit.BuildStoryTrigger(valley.transform, "trex_reveal", new Vector3(128f, 2.0f, 0f), new Vector3(9f, 8f, 16f), oneShot: true);

            // POS-7 FIX: Real passable concrete Box-Culvert Bridge carrying Service Road 3 across the stream (X = 201, Z = +4)
            // Offers an open 1.45 m crouchable hideaway tunnel underneath!
            var bridge = new GameObject("StreamBridge_Culvert");
            bridge.transform.SetParent(valley.transform, false);
            bridge.transform.position = new Vector3(201.0f, 0.0f, 4.0f);
            GameObject deck = WorldArchitectureKit.CreateBox(bridge.transform, "BridgeDeck", new Vector3(0f, 0.15f, 0f), new Vector3(11.0f, 0.35f, 5.4f), p.ConcreteExterior, addCollider: true);
            WorldArchitectureKit.ApplySurface(deck, SurfaceType.Concrete, p);
            WorldArchitectureKit.CreateBox(bridge.transform, "CulvertPier_W", new Vector3(-4.2f, -1.1f, 0f), new Vector3(1.6f, 2.4f, 5.6f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.CreateBox(bridge.transform, "CulvertPier_E", new Vector3(+4.2f, -1.1f, 0f), new Vector3(1.6f, 2.4f, 5.6f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.CreateBox(bridge.transform, "Guardrail_N", new Vector3(0f, 0.78f, +2.55f), new Vector3(10.8f, 0.12f, 0.12f), p.RustedIron, addCollider: true);
            WorldArchitectureKit.CreateBox(bridge.transform, "Guardrail_S", new Vector3(0f, 0.78f, -2.55f), new Vector3(10.8f, 0.12f, 0.12f), p.RustedIron, addCollider: true);
            DressCulvertBridge(bridge.transform, p);

            // Broken Bait Pen (X = 150, Z = +62)
            p.InstantiatePropOrFallback("feeding_cage", valley.transform, new Vector3(150f, Zone23Dressing.Ground(150f, 62f) - 0.15f, 62f), new Vector3(0f, 34f, 12f), new Vector3(3.4f, 2.8f, 3.4f), grounded: true, addCollider: true);
            WorldArchitectureKit.BuildDecal(valley.transform, "BaitPen_Blood", new Vector3(150f, Zone23Dressing.Ground(150f, 59f) + 0.3f, 59f), new Vector3(90f, 0f, 0f), new Vector3(3.2f, 3.2f, 0.8f), p.DecalBloodSmear, 0.85f);

            // 4 Perimeter Watchtowers framing Tyrant Valley silhouette with red aircraft warning strobes
            Vector3[] towerCoords =
            {
                new(108f, 1.5f, 150f),
                new(236f, 1.5f, -150f),
                new(382f, 1.5f, 140f),
                new(140f, 1.5f, -120f)
            };
            for (int i = 0; i < towerCoords.Length; i++)
            {
                towerCoords[i].y = Zone23Dressing.GroundMin(towerCoords[i].x, towerCoords[i].z, 2.6f) - 0.05f;   // seat every leg
                p.InstantiatePropOrFallback("watchtower", valley.transform, towerCoords[i], new Vector3(0f, i * 80f, 0f), new Vector3(5.2f, 13.5f, 5.2f), grounded: true, addCollider: true);
                WorldArchitectureKit.BuildHdrpLight(valley.transform, $"Light_WatchtowerStrobe_{i}", towerCoords[i] + new Vector3(0f, 14.2f, 0f), Vector3.zero, LightType.Point, new Color(1.0f, 0.14f, 0.10f), 900f, 40f, LightGroupId.PaddockBattery, volumetricDimmer: 2.0f, pulsingEmergency: true);
            }
        }

        /// <summary>Embankments with wing walls at both deck ends (the deck stood 1.5 m above the cut banks), guardrail
        /// posts every 1.8 m and the rails carried over the embankments. Local coordinates (bridge root = (201,0,4)).</summary>
        private static void DressCulvertBridge(Transform b, WorldMaterialPalette p)
        {
            const float deckTop = 0.325f, bottom = -2.8f;
            foreach (float side in new[] { -1f, 1f })
            {
                float inner = side * 5.5f, outer = side * 11.6f, mid = (inner + outer) * 0.5f, len = Mathf.Abs(outer - inner);
                GameObject fill = WorldArchitectureKit.CreateBox(b, side < 0 ? "Embankment_W" : "Embankment_E", new Vector3(mid, (deckTop + bottom) * 0.5f, 0f), new Vector3(len, deckTop - bottom, 5.4f), p.GravelGround, addCollider: true);
                WorldArchitectureKit.ApplySurface(fill, SurfaceType.Gravel, p);
                foreach (float zs in new[] { -1f, 1f })
                {
                    WorldArchitectureKit.CreateBox(b, "WingWall", new Vector3(mid, (deckTop + 0.35f + bottom) * 0.5f, zs * 2.85f), new Vector3(len, deckTop + 0.35f - bottom, 0.3f), p.ConcreteBoardform, addCollider: true);
                    WorldArchitectureKit.CreateBox(b, "WingWallCap", new Vector3(mid, deckTop + 0.38f, zs * 2.85f), new Vector3(len + 0.05f, 0.06f, 0.36f), Zone23Dressing.Coping(p), addCollider: false);
                }
                WorldArchitectureKit.CreateBox(b, "AbutmentFace", new Vector3(side * 5.35f, (deckTop + bottom) * 0.5f - 0.2f, 0f), new Vector3(0.3f, deckTop - bottom - 0.4f, 6.0f), p.ConcreteBoardform, addCollider: false);
                // sloped gravel apron off the end face: Road 3 runs across a side slope here (terrain 0.4 m below the
                // embankment top at the SE corner, 0.9 m above it at the NE), so the step never exceeds the player's
                // 0.35 m step offset; where the terrain is higher it simply covers the apron
                const float apronRun = 2.6f, apronDrop = 0.55f, apronT = 0.6f;
                float a = Mathf.Atan2(apronDrop, apronRun);
                Vector3 up = new Vector3(side * Mathf.Sin(a), Mathf.Cos(a), 0f);
                Vector3 topMid = new Vector3(outer + side * apronRun * 0.5f, deckTop - apronDrop * 0.5f, 0f);
                GameObject apron = WorldArchitectureKit.CreateBox(b, side < 0 ? "EmbankmentApron_W" : "EmbankmentApron_E", topMid - up * (apronT * 0.5f), new Vector3(apronRun / Mathf.Cos(a) + 0.2f, apronT, 5.4f), p.GravelGround, addCollider: true, euler: new Vector3(0f, 0f, -side * a * Mathf.Rad2Deg));
                WorldArchitectureKit.ApplySurface(apron, SurfaceType.Gravel, p);
            }
            // guardrail posts on the deck and rails over the embankments (posts + rails are on the deck edges)
            for (float x = -5.4f; x <= 5.41f; x += 1.8f)
                foreach (float zs in new[] { -1f, 1f })
                    WorldArchitectureKit.CreateBox(b, "GuardrailPost", new Vector3(x, deckTop + 0.34f, zs * 2.55f), new Vector3(0.1f, 0.68f, 0.1f), p.RustedIron, addCollider: false);
            foreach (float zs in new[] { -1f, 1f })
                WorldArchitectureKit.CreateBox(b, "GuardrailMid", new Vector3(0f, deckTop + 0.2f, zs * 2.55f), new Vector3(10.8f, 0.06f, 0.06f), p.RustedIron, addCollider: false);
            Vector3 bp = b.position;
            for (int i = 0; i < 6; i++)
            {
                float x = -5.5f + i * 0.2f;
                Zone23Dressing.Plant(p, b, i % 2 == 0 ? "fern_02" : "grass_medium_02", bp + new Vector3(i < 3 ? -12.2f - i * 0.6f : 12.2f + (i - 3) * 0.6f, 0f, (i % 2 == 0 ? 3.3f : -3.3f)), i * 61f, 0.9f, 0.05f, shadows: false);
            }
            Zone23Dressing.Decal(b, "DeckGrime", bp + new Vector3(0f, deckTop + 0.2f, 0f), Vector3.up, new Vector3(10.5f, 5f, 0.5f), Zone23Dressing.Grime(p, 1), 0.5f);
        }

        // =========================================================================
        // SUBSTATION 3 COMPOUND (X: 250 .. 286, Z: -17 .. +17, Y = 2.3)
        // POS-1, POS-2, POS-3, POS-4, UX-4 ALL FIXED BY CONSTRUCTION!
        // =========================================================================

        private static void BuildSubstation3Compound(Transform parent, WorldMaterialPalette p)
        {
            var sub = new GameObject("Substation3_Compound");
            sub.transform.SetParent(parent, false);
            Vector3 c = new Vector3(268.0f, SubY, 0.0f);

            WorldArchitectureKit.BuildWorldAnchor(sub.transform, "substation_center", c);
            WorldArchitectureKit.BuildWorldAnchor(sub.transform, "sub_checkpoint", new Vector3(252.0f, SubY + 0.05f, 0.0f), yawDeg: 90f);
            WorldArchitectureKit.BuildStoryTrigger(sub.transform, "enter_substation", c + Vector3.up * 4.5f, new Vector3(38f, 10f, 36f), oneShot: true);

            // Clean Concrete Compound Pad (36 x 34 m, SurfaceType.Concrete) — ZERO gravel bleeding across pad!
            GameObject pad = WorldArchitectureKit.CreateBox(sub.transform, "Substation_ConcretePad", new Vector3(268f, SubY - 0.35f, 0f), new Vector3(36.4f, 0.70f, 34.4f), p.ConcreteCracked, addCollider: true);
            WorldArchitectureKit.ApplySurface(pad, SurfaceType.Concrete, p);
            WorldArchitectureKit.BuildDecal(sub.transform, "SubPad_Puddle1", new Vector3(264f, SubY + 0.05f, 2f), new Vector3(90f, 0f, 0f), new Vector3(8.5f, 6.5f, 0.5f), p.DecalPuddleWet, 0.80f);
            WorldArchitectureKit.BuildDecal(sub.transform, "SubPad_Puddle2", new Vector3(276f, SubY + 0.05f, -4f), new Vector3(90f, 0f, 0f), new Vector3(7.0f, 5.5f, 0.5f), p.DecalPuddleWet, 0.80f);

            // Crisp Exterior Gravel Aprons strictly OUTSIDE West (X < 250) and East (X > 286) gates!
            GameObject apronW = WorldArchitectureKit.CreateBox(sub.transform, "Substation_GravelApronW", new Vector3(246.5f, SubY - 0.37f, 0f), new Vector3(7.0f, 0.66f, 11.0f), p.GravelGround, addCollider: true);
            WorldArchitectureKit.ApplySurface(apronW, SurfaceType.Gravel, p);
            GameObject apronE = WorldArchitectureKit.CreateBox(sub.transform, "Substation_GravelApronE", new Vector3(289.5f, SubY - 0.37f, 0f), new Vector3(7.0f, 0.66f, 11.0f), p.GravelGround, addCollider: true);
            WorldArchitectureKit.ApplySurface(apronE, SurfaceType.Gravel, p);

            // Concrete Curb & Heavy Chainlink Perimeter Fence (West Gate X=250 Z=-3.2..+3.2, East Gate X=286 Z=-3.2..+3.2)
            WorldArchitectureKit.BuildChainlinkFenceSpan(sub.transform, "SubFence_North", new Vector3(250f, 0f, 17f), new Vector3(286f, 0f, 17f), SubY, 4.6f, p, addDinoBlocker: false);
            WorldArchitectureKit.BuildChainlinkFenceSpan(sub.transform, "SubFence_South", new Vector3(250f, 0f, -17f), new Vector3(286f, 0f, -17f), SubY, 4.6f, p, addDinoBlocker: false);
            WorldArchitectureKit.BuildChainlinkFenceSpan(sub.transform, "SubFence_WestN", new Vector3(250f, 0f, 3.2f), new Vector3(250f, 0f, 17f), SubY, 4.6f, p, addDinoBlocker: false);
            WorldArchitectureKit.BuildChainlinkFenceSpan(sub.transform, "SubFence_WestS", new Vector3(250f, 0f, -17f), new Vector3(250f, 0f, -3.2f), SubY, 4.6f, p, addDinoBlocker: false);
            WorldArchitectureKit.BuildChainlinkFenceSpan(sub.transform, "SubFence_EastN", new Vector3(286f, 0f, 3.2f), new Vector3(286f, 0f, 17f), SubY, 4.6f, p, addDinoBlocker: false);
            WorldArchitectureKit.BuildChainlinkFenceSpan(sub.transform, "SubFence_EastS", new Vector3(286f, 0f, -17f), new Vector3(286f, 0f, -3.2f), SubY, 4.6f, p, addDinoBlocker: false);

            // Thick Exterior DinoBlocker Ring ("sub_blocker" — Layer 10 ONLY, ignores Player, keeps T. rex legs 2.5 m outside fence until Fence_Bash!)
            var subBlocker = new GameObject("Substation_DinoBlocker");
            subBlocker.transform.SetParent(sub.transform, false);
            subBlocker.transform.position = c;
            CreateDinoBlockerSlab(subBlocker.transform, "Blk_West", new Vector3(-20.2f, 3.5f, 0f), new Vector3(2.6f, 9f, 42f));
            CreateDinoBlockerSlab(subBlocker.transform, "Blk_East", new Vector3(+20.2f, 3.5f, 0f), new Vector3(2.6f, 9f, 42f));
            CreateDinoBlockerSlab(subBlocker.transform, "Blk_North", new Vector3(0f, 3.5f, +19.2f), new Vector3(42f, 9f, 2.6f));
            CreateDinoBlockerSlab(subBlocker.transform, "Blk_South", new Vector3(0f, 3.5f, -19.2f), new Vector3(42f, 9f, 2.6f));
            AttachNamedWorldAnchor(subBlocker, "sub_blocker");

            // mimic panel on the switch cabinet: one LED per station (breakers 1-3, fuel, generator, transfer)
            Renderer[] mimic = BuildMimicPanel(sub.transform, p);

            // ---- STATION 1, 2, 3: CIRCUIT BREAKERS (Breaker #2 rotated 0 deg NORTH facing walkway, POS-4 FIX!) ----
            BuildBreakerStation(sub.transform, "breaker_1", SubstationStationType.Breaker1, new Vector3(256.0f, SubY, +13.0f), yawDeg: 180f, p, mimic[0]);
            BuildBreakerStation(sub.transform, "breaker_2", SubstationStationType.Breaker2, new Vector3(258.0f, SubY, -13.0f), yawDeg: 0f, p, mimic[1]);
            BuildBreakerStation(sub.transform, "breaker_3", SubstationStationType.Breaker3, new Vector3(282.0f, SubY, +13.0f), yawDeg: 180f, p, mimic[2]);

            // ---- STATION 4: GROUNDED DIESEL FUEL TANK & HAND PUMP (POS-3 FIX — zero floating tank!) ----
            GameObject fuelRig = p.InstantiatePropOrFallback("diesel_fuel_tank_pump", sub.transform, new Vector3(265.2f, SubY, 12.0f), new Vector3(0f, 180f, 0f), new Vector3(3.2f, 2.0f, 2.1f), grounded: true, addCollider: true, fallbackMat: p.EmissiveAmber, customName: "Substation_FuelTankAndPump");
            // Reinforce grounded concrete cradle pads underneath tank
            WorldArchitectureKit.CreateBox(sub.transform, "FuelTank_CradleW", new Vector3(264.4f, SubY + 0.14f, 12.2f), new Vector3(0.45f, 0.28f, 1.4f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.CreateBox(sub.transform, "FuelTank_CradleE", new Vector3(266.2f, SubY + 0.14f, 12.2f), new Vector3(0.45f, 0.28f, 1.4f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.BuildWorldAnchor(sub.transform, "fuel_pump", new Vector3(263.8f, SubY + 1.05f, 11.0f));
            BuildSubstationInteractStation(sub.transform, "Station_FuelPump", SubstationStationType.FuelPump, new Vector3(263.8f, SubY + 1.05f, 11.0f), new Vector3(1.1f, 1.3f, 1.1f),
                new[] { new Vector3(265.2f, SubY + 0.65f, 11.6f), new Vector3(270.4f, SubY + 0.65f, 9.5f) }, p, new[] { mimic[3] });

            // ---- STATION 5: GROUNDED DIESEL GENERATOR, METAL GRATING WALKWAY & STARTER PANEL (POS-3 FIX!) ----
            WorldArchitectureKit.CreateBox(sub.transform, "Generator_VibrationPlinth", new Vector3(272.0f, SubY + 0.15f, 9.0f), new Vector3(4.6f, 0.30f, 2.6f), p.ConcreteBoardform, addCollider: true);
            GameObject genObj = p.InstantiatePropOrFallback("generator_diesel", sub.transform, new Vector3(272.0f, SubY + 0.30f, 9.0f), Vector3.zero, new Vector3(4.1f, 2.1f, 2.1f), grounded: true, addCollider: true, fallbackMat: p.DarkPaintedMetal, customName: "Substation_DieselGenerator");
            AttachNamedWorldAnchor(genObj, "generator");

            // Steel Bar-Grating Walkway (SurfaceType.Metal — AUD-3 fix!)
            GameObject grating = WorldArchitectureKit.CreateBox(sub.transform, "Generator_MetalGrating", new Vector3(272.0f, SubY + 0.03f, 6.6f), new Vector3(5.6f, 0.06f, 1.8f), p.BrushedSteel, addCollider: true);
            WorldArchitectureKit.ApplySurface(grating, SurfaceType.Metal, p);

            // Grounded Generator Starter Console on steel legs (ZERO floating box!)
            p.InstantiatePropOrFallback("generator_starter_panel", sub.transform, new Vector3(273.5f, SubY, 7.1f), new Vector3(0f, 180f, 0f), new Vector3(0.95f, 1.48f, 0.55f), grounded: true, addCollider: true, fallbackMat: p.DarkPaintedMetal, customName: "Substation_StarterPanel");
            WorldArchitectureKit.BuildWorldAnchor(sub.transform, "generator_starter", new Vector3(273.5f, SubY + 1.15f, 7.1f));
            BuildSubstationInteractStation(sub.transform, "Station_GeneratorStarter", SubstationStationType.GeneratorStarter, new Vector3(273.5f, SubY + 1.15f, 7.1f), new Vector3(1.1f, 1.1f, 0.9f),
                new[] { new Vector3(273.5f, SubY + 0.35f, 7.4f), new Vector3(273.5f, SubY + 0.35f, 8.6f) }, p, new[] { mimic[4] });

            // ---- STATION 6: GROUNDED SWITCH CABINET & ATTACHED TRANSFER LEVER PEDESTAL (POS-1 FIX!) ----
            p.InstantiatePropOrFallback("switch_cabinet", sub.transform, new Vector3(279.5f, SubY, -7.0f), new Vector3(0f, -90f, 0f), new Vector3(1.4f, 2.25f, 0.9f), grounded: true, addCollider: true, fallbackMat: p.DarkPaintedMetal, customName: "Substation_SwitchCabinet");
            p.InstantiatePropOrFallback("power_transfer_switch", sub.transform, new Vector3(277.8f, SubY, -7.0f), new Vector3(0f, -90f, 0f), new Vector3(0.85f, 1.75f, 0.70f), grounded: true, addCollider: true, fallbackMat: p.DarkPaintedMetal, customName: "Substation_TransferPedestal");
            // Heavy steel conduit directly linking transfer pedestal to switch cabinet
            WorldArchitectureKit.BuildConduitRun(sub.transform, "TransferToCabinet_Conduit", new[] { new Vector3(277.8f, SubY + 1.25f, -7.0f), new Vector3(279.4f, SubY + 1.25f, -7.0f) }, 0.055f, p.BrushedSteel);
            WorldArchitectureKit.BuildWorldAnchor(sub.transform, "transfer_lever", new Vector3(277.6f, SubY + 1.25f, -7.0f));
            BuildSubstationInteractStation(sub.transform, "Station_TransferLever", SubstationStationType.TransferLever, new Vector3(277.6f, SubY + 1.25f, -7.0f), new Vector3(1.1f, 1.2f, 1.1f),
                new[] { new Vector3(278.6f, SubY + TroughConduitY, -7.75f), new Vector3(285.3f, SubY + TroughConduitY, -7.75f), new Vector3(285.3f, SubY + TroughConduitY, -3.6f) }, p, TransferExtras(sub.transform, mimic[5], p), clampLeds: false);

            // POS-2 FIX: Grounded steel light posts mounting caged bulkhead lamps (ZERO mid-air floating bulkheads!)
            Vector3[] bulkheadPosts = { new(279.5f, SubY, -5.6f), new(253.5f, SubY, +4.2f), new(284.5f, SubY, +4.2f) };
            for (int i = 0; i < bulkheadPosts.Length; i++)
            {
                GameObject bpost = WorldArchitectureKit.CreateBox(sub.transform, $"BulkheadPost_{i}", bulkheadPosts[i] + Vector3.up * 1.45f, new Vector3(0.14f, 2.9f, 0.14f), p.DarkPaintedMetal, addCollider: true);
                Zone23Dressing.RoundPost(sub.transform, bpost, 0.055f, p.DarkPaintedMetal, null);   // round dark painted pole, collider kept (fix round: galvanised read as a hot tube under the lamp)
                Zone23Dressing.Box(sub.transform, $"BulkheadPostBase_{i}", bulkheadPosts[i] + Vector3.up * 0.01f, new Vector3(0.24f, 0.02f, 0.24f), Zone23Dressing.GalvSteel(p), Quaternion.identity);
                // round 3 (review 2: a wall lantern stood on the pole top with its plate and arm in mid-air, the glow a
                // separate ring round the pole): the lantern hangs off the pole side on its own wall plate, clamped to the
                // pole by two U-bolt bands, cage facing the compound. The glow is a frosted sleeve that fills the cage
                // between the glass (r <= 7 cm) and the bars (r 8-9 cm), so the cage's own glass reads lit; no ring.
                // The model: plate at local z -0.146, cage centre (0, 0.18, 0.056), glass y 0.10-0.26, 0.328 m long.
                Vector3 outDir = c - bulkheadPosts[i]; outDir.y = 0f; outDir.Normalize();
                Quaternion lr = Quaternion.LookRotation(outDir, Vector3.up);
                const float ls = 0.32f / 0.328f;                                         // the uniform scale the prop gets
                Vector3 origin = bulkheadPosts[i] + Vector3.up * 2.42f + outDir * (0.055f + 0.146f * ls + 0.004f);
                // fix round: scaled at yaw 0 (the scale comes from the world AABB: a prop at yaw -64 got ~0.92 instead of
                // ls and its plate stood 1.3 cm off the pole), then turned to face the compound
                GameObject housing = p.InstantiatePropOrFallback("lamp_bulkhead", sub.transform, origin, Vector3.zero, new Vector3(0.32f, 0.22f, 0.22f), grounded: false, addCollider: false, customName: $"SubPostLamp_Housing_{i}");
                if (housing != null) housing.transform.SetPositionAndRotation(origin, lr);
                if (housing != null) foreach (MeshRenderer hr in housing.GetComponentsInChildren<MeshRenderer>()) hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Material galv = Zone23Dressing.GalvSteel(p);
                for (int k = 0; k < 2; k++)
                {
                    float by = 2.42f + (0.235f + k * 0.09f) * ls;
                    Zone23Dressing.Cyl(sub.transform, $"SubPostLamp_Clamp_{i}", bulkheadPosts[i] + Vector3.up * (by - 0.012f), bulkheadPosts[i] + Vector3.up * (by + 0.012f), 0.064f, galv);
                }
                Zone23Dressing.Cyl(sub.transform, $"SubPostLamp_PoleCap_{i}", bulkheadPosts[i] + Vector3.up * 2.9f, bulkheadPosts[i] + Vector3.up * 2.935f, 0.07f, galv);
                // a conduit from the plate down the pole to a junction box at hand height
                Zone23Dressing.Cyl(sub.transform, $"SubPostLamp_Conduit_{i}", bulkheadPosts[i] + outDir * 0.075f + Vector3.up * 1.25f, bulkheadPosts[i] + outDir * 0.075f + Vector3.up * 2.62f, 0.014f, galv);
                Zone23Dressing.Box(sub.transform, $"SubPostLamp_JBox_{i}", bulkheadPosts[i] + outDir * 0.1f + Vector3.up * 1.15f, new Vector3(0.12f, 0.16f, 0.08f), p.DarkPaintedMetal, lr);
                Vector3 lp = origin + Vector3.up * (0.18f * ls) + outDir * (0.056f * ls);   // cage centre
                GameObject lens = Zone23Dressing.Cyl(sub.transform, $"SubPostLamp_Lens_{i}", lp - Vector3.up * (0.075f * ls), lp + Vector3.up * (0.075f * ls), 0.072f * ls, Zone23Dressing.BulkheadLens(p));
                if (lens != null) lens.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // fix round: a 150 deg spot aimed out and 20 deg down (a point light 0.22 m from the metal pole lit the pole
                // below into a glowing tube); spot angle from the lamp rule
                Vector3 lampAim = (outDir - 0.36f * Vector3.up).normalized;
                WorldArchitectureKit.BuildHdrpLight(sub.transform, $"Light_SubPostLamp_{i}", lp + outDir * 0.02f, Quaternion.LookRotation(lampAim).eulerAngles, LightType.Spot, new Color(1.0f, 0.62f, 0.26f), 420f, 11f, LightGroupId.SubBattery, volumetricDimmer: 1.2f, spotOuterAngle: 150f,
                    pairedEmissives: lens != null ? new Renderer[] { lens.GetComponent<MeshRenderer>() } : null);
            }

            // 4 Towering 8 m Auxiliary Floodlight Masts (LightGroupId.AuxSub — blaze on when Circuit B restored!)
            Vector3[] mastCoords = { new(252f, SubY, 15f), new(284f, SubY, 15f), new(252f, SubY, -15f), new(284f, SubY, -15f) };
            for (int m = 0; m < mastCoords.Length; m++)
            {
                GameObject mast = WorldArchitectureKit.CreateBox(sub.transform, $"FloodMast_{m}", mastCoords[m] + Vector3.up * 4.0f, new Vector3(0.24f, 8.0f, 0.24f), p.DarkPaintedMetal, addCollider: true);
                Zone23Dressing.RoundPost(sub.transform, mast, 0.1f, Zone23Dressing.GalvSteel(p), null);
                Zone23Dressing.Box(sub.transform, $"FloodMastBase_{m}", mastCoords[m] + Vector3.up * 0.015f, new Vector3(0.42f, 0.03f, 0.42f), Zone23Dressing.GalvSteel(p), Quaternion.identity);
                Zone23Dressing.Cyl(sub.transform, $"FloodMastDoor_{m}", mastCoords[m] + new Vector3(0f, 0.6f, 0f) + (c - mastCoords[m]).normalized * 0.1f, mastCoords[m] + new Vector3(0f, 1.0f, 0f) + (c - mastCoords[m]).normalized * 0.1f, 0.03f, p.DarkPaintedMetal);
                // round 3 (review 2: the head was a blown-out white card): two LED flood heads on a crossarm, built here
                // (Light_SubMastFlood is not a LookFixtures name, so the fixture pass leaves them alone), lens cells at
                // ~9 nits behind a dark reflector grid, fins and yokes; one lamp between the heads drives both lenses
                Vector3 top = mastCoords[m] + Vector3.up * 7.95f;
                Vector3 aimDir = (c - top).normalized;
                Vector3 fwd = new Vector3(aimDir.x, 0f, aimDir.z).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                Zone23Dressing.Cyl(sub.transform, $"FloodCrossarm_{m}", top - side * 0.62f + fwd * 0.12f, top + side * 0.62f + fwd * 0.12f, 0.035f, Zone23Dressing.GalvSteel(p));
                Zone23Dressing.Box(sub.transform, $"FloodArmClamp_{m}", top + fwd * 0.06f, new Vector3(0.26f, 0.14f, 0.16f), p.DarkPaintedMetal, Quaternion.LookRotation(fwd));
                Zone23Dressing.Cyl(sub.transform, $"FloodMastCap_{m}", mastCoords[m] + Vector3.up * 8.0f, mastCoords[m] + Vector3.up * 8.03f, 0.12f, Zone23Dressing.GalvSteel(p));
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 hp = top + side * (k * 0.42f) + fwd * 0.42f - Vector3.up * 0.16f;
                    Vector3 aim = ((c + side * (k * 4f)) - hp).normalized;   // both heads on the pad, not on the fences (review 2 check: a lit fence turned into a grey screen)
                    List<Renderer> cells = Zone23Dressing.FloodHead(sub.transform, $"SubMastFloodHead_{m}_{(k < 0 ? "L" : "R")}", hp, aim, p);
                    // drop from the crossarm to the yoke stem behind the head
                    Quaternion hr = Quaternion.LookRotation(aim, Vector3.up);
                    Vector3 stemEnd = hp + hr * new Vector3(0f, -0.17f, -0.245f);
                    Zone23Dressing.Cyl(sub.transform, $"FloodDrop_{m}", top + side * (k * 0.42f) + fwd * 0.12f, stemEnd, 0.022f, Zone23Dressing.GalvSteel(p));
                    // one lamp in each lens (7000 cd each, LookLights.Zone23); only the left head of a mast casts shadows
                    WorldArchitectureKit.BuildHdrpLight(sub.transform, k < 0 ? $"Light_SubMastFlood_{m}" : $"Light_SubMastFloodB_{m}", hp + aim * 0.06f, Quaternion.LookRotation(aim).eulerAngles,
                        LightType.Spot, new Color(1.0f, 0.88f, 0.68f), 18000f, 45f, LightGroupId.AuxSub, volumetricDimmer: 0.9f, castShadows: k < 0 && m < 2, spotOuterAngle: 82f,
                        pairedEmissives: cells.ToArray());
                }
            }

            DressSubstation(sub.transform, p);
        }

        // =========================================================================
        // SUBSTATION 3 DRESSING (look pass): transformers, bus gantry, control hut, gravel beds, cable trench,
        // open gates in both fence gaps, HV signage, pad joints. The six interact stations are untouched.
        // =========================================================================

        private static void DressSubstation(Transform parent, WorldMaterialPalette p)
        {
            var d = new GameObject("Substation3_Dressing");
            d.transform.SetParent(parent, false);
            const float y0 = SubY;

            // pad joints every 6 m (flush strips, no collider) and oil/grime stains
            for (float x = 252f; x < 286f; x += 6f)
                Zone23Dressing.Box(d.transform, "PadJointX", new Vector3(x, y0 + 0.003f, 0f), new Vector3(0.03f, 0.006f, 34.2f), Zone23Dressing.DrainVoid(p), Quaternion.identity);
            for (float z = -15f; z < 17f; z += 6f)
                Zone23Dressing.Box(d.transform, "PadJointZ", new Vector3(268f, y0 + 0.004f, z), new Vector3(36.2f, 0.006f, 0.03f), Zone23Dressing.DrainVoid(p), Quaternion.identity);
            Vector3[] stains = { new(271.5f, y0, 6.2f), new(266f, y0, 9.8f), new(279f, y0, -8.6f), new(257f, y0, -1f) };
            for (int i = 0; i < stains.Length; i++)
                Zone23Dressing.Decal(d.transform, $"PadStain_{i}", stains[i] + Vector3.up * 0.05f, Vector3.up, new Vector3(2.4f, 3.0f, 0.4f), Zone23Dressing.Grime(p, i), 0.7f, rollDeg: 50f * i);

            // gravel bed for the transformer yard + a gravel strip inside the north and south fences
            Zone23Dressing.Box(d.transform, "TransformerGravelBed", new Vector3(266.5f, y0 + 0.02f, -11.2f), new Vector3(15.5f, 0.04f, 10.6f), p.GravelGround, Quaternion.identity);
            Zone23Dressing.Box(d.transform, "TransformerBedKerb_N", new Vector3(266.5f, y0 + 0.06f, -5.85f), new Vector3(15.7f, 0.12f, 0.15f), Zone23Dressing.Kerb(p), Quaternion.identity);
            Zone23Dressing.Box(d.transform, "TransformerBedKerb_W", new Vector3(258.7f, y0 + 0.06f, -11.2f), new Vector3(0.15f, 0.12f, 10.6f), Zone23Dressing.Kerb(p), Quaternion.identity);
            Zone23Dressing.Box(d.transform, "TransformerBedKerb_E", new Vector3(274.3f, y0 + 0.06f, -11.2f), new Vector3(0.15f, 0.12f, 10.6f), Zone23Dressing.Kerb(p), Quaternion.identity);
            Zone23Dressing.Box(d.transform, "GravelStrip_N", new Vector3(268f, y0 + 0.015f, 16.2f), new Vector3(35.8f, 0.03f, 1.4f), p.GravelGround, Quaternion.identity);

            BuildTransformer(d.transform, "Transformer_T1", new Vector3(263.6f, y0 + 0.04f, -11.0f), p);
            BuildTransformer(d.transform, "Transformer_T2", new Vector3(269.6f, y0 + 0.04f, -11.0f), p);
            BuildBusGantry(d.transform, p);
            BuildControlHut(d.transform, new Vector3(277.6f, y0, 14.6f), p);

            // covered cable trench from the transformers to the switch cabinet: flush grating over a dark channel
            Vector3[] trench = { new(266.6f, y0, -7.4f), new(276.2f, y0, -7.4f), new(276.2f, y0, -5.9f) };
            for (int i = 0; i < trench.Length - 1; i++)
            {
                Vector3 a = trench[i], b = trench[i + 1], m = (a + b) * 0.5f; Vector3 dir = (b - a); float len = dir.magnitude; dir /= len;
                Quaternion r = Quaternion.LookRotation(dir, Vector3.up);
                Zone23Dressing.Box(d.transform, $"CableTrench_{i}", m + Vector3.up * 0.008f, new Vector3(0.62f, 0.016f, len + 0.62f), Zone23Dressing.DrainVoid(p), r);
                Zone23Dressing.Box(d.transform, $"CableTrenchEdge_{i}a", m + r * new Vector3(0.34f, 0.012f, 0f), new Vector3(0.06f, 0.024f, len + 0.7f), p.BrushedSteel, r);
                Zone23Dressing.Box(d.transform, $"CableTrenchEdge_{i}b", m + r * new Vector3(-0.34f, 0.012f, 0f), new Vector3(0.06f, 0.024f, len + 0.7f), p.BrushedSteel, r);
                var g = Zone23Dressing.Decal(d.transform, $"CableTrenchGrating_{i}", m + Vector3.up * 0.06f, Vector3.up, new Vector3(0.62f, len + 0.62f, 0.2f), Zone23Dressing.Grating(p), 1f);
                g.transform.rotation = Quaternion.LookRotation(Vector3.down, dir);
                g.uvScale = new Vector2(1f, Mathf.Max(1f, (len + 0.62f) / 0.62f));
            }
            // open troughs carrying the three breaker conduits along Z = 9 and up from each breaker
            BuildConduitTrough(d.transform, new Vector3(256f, y0, 12.4f), new Vector3(256f, y0, 9f), p);
            BuildConduitTrough(d.transform, new Vector3(258f, y0, -12.4f), new Vector3(258f, y0, 9f), p, 6.4f, 21.4f);   // lid across the walkway (z -6 .. 9)
            BuildConduitTrough(d.transform, new Vector3(282f, y0, 12.4f), new Vector3(282f, y0, 9f), p);
            BuildConduitTrough(d.transform, new Vector3(255.8f, y0, 9f), new Vector3(269.6f, y0, 9f), p);
            BuildConduitTrough(d.transform, new Vector3(274.4f, y0, 9f), new Vector3(282.2f, y0, 9f), p);
            // the transfer conduit from the switch cabinet to the east gate (was a diagonal rod across the pad)
            BuildConduitTrough(d.transform, new Vector3(278.5f, y0, -7.75f), new Vector3(285.4f, y0, -7.75f), p);
            BuildConduitTrough(d.transform, new Vector3(285.3f, y0, -7.85f), new Vector3(285.3f, y0, -3.5f), p, 0f, 4.35f);

            // gates: heavy posts at both gaps and chainlink leaves swung back against the outside of the fence
            BuildOpenSubGate(d.transform, 250f, -1f, p);
            BuildOpenSubGate(d.transform, 286f, +1f, p);

            // signage
            Material hv = Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage");
            Material s3 = Zone23Dressing.SignMat(p, "MAT_Z23_SignSubstation3", "substation_3");
            for (float x = 256f; x < 284f; x += 12f)
            {
                Zone23Dressing.SignPlate(d.transform, "Sign_HV_N", new Vector3(x, y0 + 1.7f, 17.08f), Vector3.forward, 0.5f, 0.68f, hv, p.DarkPaintedMetal);
                Zone23Dressing.SignPlate(d.transform, "Sign_HV_S", new Vector3(x, y0 + 1.7f, -17.08f), Vector3.back, 0.5f, 0.68f, hv, p.DarkPaintedMetal);
            }
            Zone23Dressing.SignPlate(d.transform, "Sign_HV_WN", new Vector3(249.92f, y0 + 1.7f, 9f), Vector3.left, 0.5f, 0.68f, hv, p.DarkPaintedMetal);
            Zone23Dressing.SignPlate(d.transform, "Sign_HV_ES", new Vector3(286.08f, y0 + 1.7f, -9f), Vector3.right, 0.5f, 0.68f, hv, p.DarkPaintedMetal);
            Zone23Dressing.SignPlate(d.transform, "Sign_Substation3_W", new Vector3(249.9f, y0 + 2.2f, 5.6f), Vector3.left, 1.6f, 0.88f, s3, p.DarkPaintedMetal);

            // fuel corner and service clutter against the north fence (no colliders)
            p.InstantiatePropOrFallback("pallet", d.transform, new Vector3(261.2f, y0, 15.4f), new Vector3(0f, 8f, 0f), new Vector3(1.2f, 1.2f, 1.2f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("oil_drum", d.transform, new Vector3(260.9f, y0 + 0.15f, 15.2f), new Vector3(0f, 20f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("oil_drum", d.transform, new Vector3(261.6f, y0 + 0.15f, 15.6f), new Vector3(0f, 80f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("oil_drum", d.transform, new Vector3(262.6f, y0, 15.9f), new Vector3(0f, 140f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("jerry_can", d.transform, new Vector3(263.4f, y0, 14.6f), new Vector3(0f, 200f, 0f), new Vector3(0.47f, 0.47f, 0.47f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("jerry_can", d.transform, new Vector3(263.8f, y0, 14.9f), new Vector3(0f, 170f, 0f), new Vector3(0.47f, 0.47f, 0.47f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("fire_extinguisher", d.transform, new Vector3(268.2f, y0, 16.3f), new Vector3(0f, 180f, 0f), new Vector3(0.6f, 0.6f, 0.6f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("utility_truck", d.transform, new Vector3(242.5f, Zone23Dressing.GroundMin(242.5f, 10.5f, 2.6f), 10.5f), new Vector3(0f, 168f, 0f), new Vector3(2.4f, 2.4f, 5.6f), grounded: true, addCollider: true, customName: "Substation_ServiceTruck");

            // jungle pressed against the outside of the fence on three sides (the T. rex comes out of it)
            for (float x = 251f; x < 286f; x += 6.2f)
            {
                float h = Mathf.Repeat(Mathf.Sin(x * 2.3f) * 517.1f, 1f);
                Zone23Dressing.Plant(p, d.transform, h > 0.6f ? "palm_tree" : (h > 0.3f ? "tree_fern" : "banana_plant"), new Vector3(x, 0f, 19.6f + h * 3f), 360f * h, h > 0.6f ? 10f : 3.4f, 0.02f, shadows: h > 0.6f);
                Zone23Dressing.Plant(p, d.transform, h > 0.5f ? "jungle_tree" : "banana_plant", new Vector3(x + 1.7f, 0f, h > 0.5f ? -24.5f - h * 2f : -19.6f - h), 360f * h, h > 0.5f ? 14f : 2.8f, 0.02f, shadows: h > 0.5f);
                Zone23Dressing.Plant(p, d.transform, h > 0.5f ? "monstera_clump" : "fern_02", new Vector3(x + 0.8f, 0f, 18.2f), 77f * x, 1.4f, 0.03f, shadows: false);
            }
            Zone23Dressing.MergeStatic(d.transform);   // fix round: pad joints, kerbs, troughs, hut and sign boxes
        }

        private static void BuildTransformer(Transform parent, string name, Vector3 c, WorldMaterialPalette p)
        {
            var t = new GameObject(name);
            t.transform.SetParent(parent, false);
            t.transform.position = c;
            Material paint = Zone23Dressing.TransformerGrey(p);
            Material porc = Zone23Dressing.Porcelain(p);
            Transform tr = t.transform;
            // concrete plinth, tank, lid, radiator banks, conservator, bushings
            WorldArchitectureKit.CreateBox(tr, "Plinth", new Vector3(0f, 0.15f, 0f), new Vector3(3.4f, 0.3f, 2.8f), Zone23Dressing.Plinth(p), addCollider: true);
            WorldArchitectureKit.CreateBox(tr, "Tank", new Vector3(0f, 1.45f, 0f), new Vector3(2.4f, 2.3f, 1.6f), paint, addCollider: true);
            WorldArchitectureKit.CreateBox(tr, "Lid", new Vector3(0f, 2.65f, 0f), new Vector3(2.55f, 0.1f, 1.75f), paint, addCollider: false);
            WorldArchitectureKit.CreateBox(tr, "BaseSkid", new Vector3(0f, 0.36f, 0f), new Vector3(2.6f, 0.12f, 1.8f), p.DarkPaintedMetal, addCollider: false);
            for (int side = -1; side <= 1; side += 2)
                for (int f = 0; f < 9; f++)
                    WorldArchitectureKit.CreateBox(tr, "RadiatorFin", new Vector3(-1.0f + f * 0.25f, 1.35f, side * 1.02f), new Vector3(0.05f, 1.7f, 0.42f), paint, addCollider: false);
            for (int side = -1; side <= 1; side += 2)
            {
                WorldArchitectureKit.CreateBox(tr, "RadiatorHeader", new Vector3(0f, 2.25f, side * 1.02f), new Vector3(2.2f, 0.1f, 0.1f), paint, addCollider: false);
                WorldArchitectureKit.CreateBox(tr, "RadiatorFooter", new Vector3(0f, 0.5f, side * 1.02f), new Vector3(2.2f, 0.1f, 0.1f), paint, addCollider: false);
            }
            Zone23Dressing.Cyl(tr, "Conservator", c + new Vector3(-1.1f, 3.25f, -0.55f), c + new Vector3(1.1f, 3.25f, -0.55f), 0.26f, paint);
            Zone23Dressing.Cyl(tr, "ConservatorPipe", c + new Vector3(0.6f, 2.7f, -0.45f), c + new Vector3(0.6f, 3.1f, -0.5f), 0.04f, paint);
            WorldArchitectureKit.CreateBox(tr, "ConservatorSaddleA", new Vector3(-0.8f, 2.92f, -0.55f), new Vector3(0.1f, 0.45f, 0.2f), paint, addCollider: false);
            WorldArchitectureKit.CreateBox(tr, "ConservatorSaddleB", new Vector3(0.8f, 2.92f, -0.55f), new Vector3(0.1f, 0.45f, 0.2f), paint, addCollider: false);
            for (int b = -1; b <= 1; b++)
            {
                Vector3 bb = c + new Vector3(b * 0.7f, 2.7f, 0.35f);
                for (int k = 0; k < 6; k++)    // stacked insulator sheds
                    Zone23Dressing.Cyl(tr, "BushingShed", bb + Vector3.up * (k * 0.13f), bb + Vector3.up * (k * 0.13f + 0.06f), 0.09f - k * 0.004f, porc);
                Zone23Dressing.Cyl(tr, "BushingCore", bb, bb + Vector3.up * 0.95f, 0.045f, porc);
                Zone23Dressing.Cyl(tr, "BushingCap", bb + Vector3.up * 0.95f, bb + Vector3.up * 1.02f, 0.05f, p.BrushedSteel);
            }
            // rating plate + HV plate on the north face (towards the pad)
            Zone23Dressing.SignPlate(tr, "Sign_HV_Tank", c + new Vector3(0.6f, 1.6f, 0.81f), Vector3.forward, 0.36f, 0.49f, Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"));
            WorldArchitectureKit.CreateBox(tr, "RatingPlate", new Vector3(-0.6f, 1.6f, 0.805f), new Vector3(0.3f, 0.2f, 0.01f), p.BrushedSteel, addCollider: false);
            Zone23Dressing.Decal(tr, "TankGrime", c + new Vector3(0f, 1.2f, 0.9f), Vector3.forward, new Vector3(2.4f, 2.0f, 0.4f), Zone23Dressing.Grime(p, 2), 0.6f);
        }

        private static void BuildBusGantry(Transform parent, WorldMaterialPalette p)
        {
            var g = new GameObject("BusGantry");
            g.transform.SetParent(parent, false);
            const float y0 = SubY, top = 7.2f;
            Material steel = Zone23Dressing.GalvSteel(p);
            Material porc = Zone23Dressing.Porcelain(p);
            float[] portalX = { 260.2f, 273.0f };
            float[] postZ = { -16.0f, -6.6f };
            foreach (float x in portalX)
            {
                foreach (float z in postZ)
                {
                    // lattice post: four angle legs + horizontal ties
                    Vector3 b = new Vector3(x, y0, z);
                    WorldArchitectureKit.CreateBox(g.transform, "PostFooting", b + new Vector3(0f, 0.12f, 0f) - g.transform.position, new Vector3(0.8f, 0.24f, 0.8f), Zone23Dressing.Plinth(p), addCollider: true);
                    for (int lx = -1; lx <= 1; lx += 2)
                        for (int lz = -1; lz <= 1; lz += 2)
                            Zone23Dressing.Cyl(g.transform, "Leg", b + new Vector3(lx * 0.2f, 0.24f, lz * 0.2f), b + new Vector3(lx * 0.12f, top, lz * 0.12f), 0.035f, steel);
                    for (float h = 0.9f; h < top; h += 0.9f)
                    {
                        float w = Mathf.Lerp(0.2f, 0.12f, h / top);
                        Zone23Dressing.Cyl(g.transform, "Tie", b + new Vector3(-w, h, -w), b + new Vector3(w, h + 0.45f, w), 0.015f, steel);
                        Zone23Dressing.Cyl(g.transform, "Tie", b + new Vector3(w, h, -w), b + new Vector3(-w, h + 0.45f, w), 0.015f, steel);
                    }
                    WorldArchitectureKit.CreateBox(g.transform, "PostCollider", b + new Vector3(0f, top * 0.5f, 0f) - g.transform.position, new Vector3(0.45f, top, 0.45f), null, addCollider: true).GetComponent<MeshRenderer>().enabled = false;
                }
                // lattice beam between the two posts
                Vector3 b0 = new Vector3(x, y0 + top, postZ[0]), b1 = new Vector3(x, y0 + top, postZ[1]);
                Zone23Dressing.Cyl(g.transform, "BeamTop", b0 + new Vector3(0f, 0.2f, 0f), b1 + new Vector3(0f, 0.2f, 0f), 0.05f, steel);
                Zone23Dressing.Cyl(g.transform, "BeamBot", b0 - new Vector3(0f, 0.2f, 0f), b1 - new Vector3(0f, 0.2f, 0f), 0.05f, steel);
                for (float z = postZ[0]; z < postZ[1] - 0.4f; z += 0.8f)
                    Zone23Dressing.Cyl(g.transform, "BeamWeb", new Vector3(x, y0 + top - 0.2f, z), new Vector3(x, y0 + top + 0.2f, z + 0.4f), 0.02f, steel);
            }
            // three bus conductors on suspension insulators, with droppers to the transformer bushings
            float[] phaseZ = { -12.8f, -11.0f, -9.2f };
            for (int ph = 0; ph < 3; ph++)
            {
                Vector3 ia = new Vector3(portalX[0], y0 + top - 0.25f, phaseZ[ph]);
                Vector3 ib = new Vector3(portalX[1], y0 + top - 0.25f, phaseZ[ph]);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 hang = k == 0 ? ia : ib;
                    for (int sh = 0; sh < 5; sh++)
                        Zone23Dressing.Cyl(g.transform, "SuspInsulator", hang - Vector3.up * (0.1f + sh * 0.12f), hang - Vector3.up * (0.16f + sh * 0.12f), 0.1f, porc);
                }
                Vector3 ca = ia - Vector3.up * 0.75f, cb = ib - Vector3.up * 0.75f;
                Vector3 prev = ca;
                for (int sgm = 1; sgm <= 8; sgm++)                 // a gentle catenary sag
                {
                    float t = sgm / 8f;
                    Vector3 pt = Vector3.Lerp(ca, cb, t) - Vector3.up * (0.55f * 4f * t * (1f - t));
                    Zone23Dressing.Cyl(g.transform, "BusConductor", prev, pt, 0.018f, steel);
                    prev = pt;
                }
                for (int tx = 0; tx < 2; tx++)
                {
                    float bx = (tx == 0 ? 263.6f : 269.6f) + (ph - 1) * 0.7f;
                    Vector3 top3 = new Vector3(bx, y0 + 0.04f + 3.72f, -10.65f);
                    float t = (bx - portalX[0]) / (portalX[1] - portalX[0]);
                    Vector3 on = Vector3.Lerp(ca, cb, t) - Vector3.up * (0.55f * 4f * t * (1f - t));
                    on.z = phaseZ[ph];
                    Zone23Dressing.Cyl(g.transform, "Dropper", on, Vector3.Lerp(on, top3, 0.5f) + Vector3.up * 0.25f, 0.014f, steel);
                    Zone23Dressing.Cyl(g.transform, "Dropper", Vector3.Lerp(on, top3, 0.5f) + Vector3.up * 0.25f, top3, 0.014f, steel);
                }
                // incoming line from the south: the three phases drop over the fence onto a terminal pole's crossarm
                Vector3 outP = new Vector3(TermPoleX + (ph - 1) * 1.15f, TermPoleTop(y0) - 0.12f, TermPoleZ);
                Vector3 mid = Vector3.Lerp(ca, outP, 0.5f) - Vector3.up * 0.35f;     // a little sag
                Zone23Dressing.Cyl(g.transform, "IncomingLine", ca, mid, 0.016f, steel);
                Zone23Dressing.Cyl(g.transform, "IncomingLine", mid, outP, 0.016f, steel);
            }
            BuildTerminalPole(g.transform, p);
            Zone23Dressing.SignPlate(g.transform, "Sign_HV_Gantry", new Vector3(273.0f, SubY + 1.6f, -6.36f), Vector3.forward, 0.44f, 0.6f, Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"));
            Zone23Dressing.MergeStatic(g.transform);
        }

        private const float TermPoleX = 257f, TermPoleZ = -21f;
        private static float TermPoleTop(float y0) => y0 + 7.2f + 1.6f;

        /// <summary>Terminal pole outside the south fence where the incoming line ends: tapered pole on a footing,
        /// crossarm with three pin insulators, a cable riser down the pole into the ground, and a stay wire.</summary>
        private static void BuildTerminalPole(Transform parent, WorldMaterialPalette p)
        {
            float gy = Zone23Dressing.Ground(TermPoleX, TermPoleZ);
            float top = TermPoleTop(SubY);
            Vector3 b = new Vector3(TermPoleX, gy - 0.3f, TermPoleZ);
            Material wood = p.Surface("MAT_Z23_PoleTimber", "wood_planks_weathered", "BaseColor", "MaskMap", 1.4f, new Color(0.62f, 0.55f, 0.48f), 0.1f, 0.4f, 0f, 1f);
            Zone23Dressing.Cyl(parent, "TermPole", b, new Vector3(TermPoleX, top + 0.25f, TermPoleZ), 0.15f, wood);
            Zone23Dressing.Box(parent, "TermPoleFooting", new Vector3(TermPoleX, gy + 0.05f, TermPoleZ), new Vector3(0.7f, 0.25f, 0.7f), Zone23Dressing.Plinth(p), Quaternion.identity);
            Zone23Dressing.Box(parent, "TermPoleCrossarm", new Vector3(TermPoleX, top - 0.3f, TermPoleZ), new Vector3(2.8f, 0.12f, 0.12f), Zone23Dressing.GalvSteel(p), Quaternion.identity);
            Zone23Dressing.Cyl(parent, "TermPoleBraceA", new Vector3(TermPoleX - 0.9f, top - 0.34f, TermPoleZ), new Vector3(TermPoleX, top - 1.1f, TermPoleZ - 0.16f), 0.025f, Zone23Dressing.GalvSteel(p));
            Zone23Dressing.Cyl(parent, "TermPoleBraceB", new Vector3(TermPoleX + 0.9f, top - 0.34f, TermPoleZ), new Vector3(TermPoleX, top - 1.1f, TermPoleZ - 0.16f), 0.025f, Zone23Dressing.GalvSteel(p));
            Material porc = Zone23Dressing.Porcelain(p);
            for (int ph = 0; ph < 3; ph++)
            {
                float x = TermPoleX + (ph - 1) * 1.15f;
                for (int k = 0; k < 3; k++)
                    Zone23Dressing.Cyl(parent, "TermPoleInsulator", new Vector3(x, top - 0.24f + k * 0.07f, TermPoleZ), new Vector3(x, top - 0.2f + k * 0.07f, TermPoleZ), 0.06f - k * 0.008f, porc);
                // jumpers from each insulator down into the riser
                Zone23Dressing.Cyl(parent, "TermPoleJumper", new Vector3(x, top - 0.12f, TermPoleZ), new Vector3(TermPoleX, top - 1.5f, TermPoleZ - 0.2f), 0.012f, Zone23Dressing.GalvSteel(p));
            }
            Zone23Dressing.Cyl(parent, "TermPoleRiser", new Vector3(TermPoleX, gy - 0.1f, TermPoleZ - 0.2f), new Vector3(TermPoleX, top - 1.5f, TermPoleZ - 0.2f), 0.05f, p.DarkPaintedMetal);
            Zone23Dressing.Box(parent, "TermPoleRiserHead", new Vector3(TermPoleX, top - 1.5f, TermPoleZ - 0.2f), new Vector3(0.22f, 0.3f, 0.18f), p.DarkPaintedMetal, Quaternion.identity);
            // stay wire to an anchor block on the far side from the gantry pull
            Vector3 anchor = new Vector3(TermPoleX, 0f, TermPoleZ - 4.5f); anchor.y = Zone23Dressing.Ground(anchor.x, anchor.z);
            Zone23Dressing.Cyl(parent, "TermPoleStay", new Vector3(TermPoleX, top - 0.6f, TermPoleZ), anchor + Vector3.up * 0.1f, 0.01f, Zone23Dressing.GalvSteel(p));
            Zone23Dressing.Box(parent, "TermPoleStayAnchor", anchor + Vector3.up * 0.05f, new Vector3(0.35f, 0.2f, 0.35f), Zone23Dressing.Plinth(p), Quaternion.identity);
            Zone23Dressing.SignPlate(parent, "Sign_HV_TermPole", new Vector3(TermPoleX, gy + 2.2f, TermPoleZ + 0.17f), Vector3.forward, 0.36f, 0.49f, Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"));
        }

        private static void BuildControlHut(Transform parent, Vector3 c, WorldMaterialPalette p)
        {
            var h = new GameObject("Substation_ControlHut");
            h.transform.SetParent(parent, false);
            h.transform.position = c;
            Transform tr = h.transform;
            const float w = 4.4f, dz = 3.0f, ht = 2.9f;
            WorldArchitectureKit.CreateBox(tr, "Footing", new Vector3(0f, 0.08f, 0f), new Vector3(w + 0.3f, 0.16f, dz + 0.3f), Zone23Dressing.Plinth(p), addCollider: true);
            WorldArchitectureKit.CreateBox(tr, "Body", new Vector3(0f, 0.16f + ht * 0.5f, 0f), new Vector3(w, ht, dz), p.BrickPainted, addCollider: true);
            // mono-pitch corrugated roof with eaves, fascia and a gutter
            WorldArchitectureKit.CreateBox(tr, "Roof", new Vector3(0f, 0.16f + ht + 0.22f, -0.05f), new Vector3(w + 0.6f, 0.08f, dz + 0.8f), p.CorrugatedMetal, addCollider: false, euler: new Vector3(-6f, 0f, 0f));
            WorldArchitectureKit.CreateBox(tr, "Fascia", new Vector3(0f, 0.16f + ht + 0.06f, dz * 0.5f + 0.38f), new Vector3(w + 0.6f, 0.2f, 0.04f), p.DarkPaintedMetal, addCollider: false);
            Zone23Dressing.Cyl(tr, "Gutter", c + new Vector3(-w * 0.5f - 0.3f, 0.16f + ht - 0.05f, -dz * 0.5f - 0.45f), c + new Vector3(w * 0.5f + 0.3f, 0.16f + ht - 0.05f, -dz * 0.5f - 0.45f), 0.06f, p.DarkPaintedMetal);
            Zone23Dressing.Cyl(tr, "Downpipe", c + new Vector3(w * 0.5f + 0.15f, 0.2f, -dz * 0.5f - 0.45f), c + new Vector3(w * 0.5f + 0.15f, 0.16f + ht - 0.05f, -dz * 0.5f - 0.45f), 0.045f, p.DarkPaintedMetal);
            // steel door in a frame on the south face, louvre, AC unit, HV + substation plates
            float fz = -dz * 0.5f - 0.02f;
            WorldArchitectureKit.CreateBox(tr, "DoorFrame", new Vector3(-0.8f, 0.16f + 1.08f, fz), new Vector3(1.1f, 2.16f, 0.06f), p.DarkPaintedMetal, addCollider: false);
            WorldArchitectureKit.CreateBox(tr, "Door", new Vector3(-0.8f, 0.16f + 1.05f, fz - 0.02f), new Vector3(0.96f, 2.04f, 0.04f), Zone23Dressing.TransformerGrey(p), addCollider: false);
            WorldArchitectureKit.CreateBox(tr, "DoorHandle", new Vector3(-0.42f, 0.16f + 1.05f, fz - 0.06f), new Vector3(0.12f, 0.03f, 0.04f), p.BrushedSteel, addCollider: false);
            WorldArchitectureKit.CreateBox(tr, "Louvre", new Vector3(1.1f, 0.16f + 2.1f, fz), new Vector3(0.7f, 0.45f, 0.05f), p.DarkPaintedMetal, addCollider: false);
            for (int i = 0; i < 5; i++)
                WorldArchitectureKit.CreateBox(tr, "LouvreSlat", new Vector3(1.1f, 0.16f + 1.92f + i * 0.09f, fz - 0.04f), new Vector3(0.66f, 0.02f, 0.05f), p.BrushedSteel, addCollider: false, euler: new Vector3(35f, 0f, 0f));
            WorldArchitectureKit.CreateBox(tr, "ACUnit", new Vector3(w * 0.5f + 0.32f, 0.16f + 1.6f, 0.3f), new Vector3(0.6f, 0.7f, 0.9f), Zone23Dressing.TransformerGrey(p), addCollider: false);
            Zone23Dressing.SignPlate(tr, "Sign_HV_HutDoor", c + new Vector3(-0.8f, 0.16f + 1.5f, fz - 0.05f), Vector3.back, 0.36f, 0.49f, Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"));
            Zone23Dressing.SignPlate(tr, "Sign_Substation3_Hut", c + new Vector3(0.95f, 0.16f + 1.25f, fz - 0.03f), Vector3.back, 1.0f, 0.55f, Zone23Dressing.SignMat(p, "MAT_Z23_SignSubstation3", "substation_3"));
            Zone23Dressing.Decal(tr, "HutGrime", c + new Vector3(0f, 1.6f, fz - 0.2f), Vector3.back, new Vector3(4.4f, 2.6f, 0.5f), Zone23Dressing.Grime(p, 1), 0.7f);
            // battery bulkhead over the door (a warm pool the player can find the hut by)
            WorldArchitectureKit.BuildHdrpLight(tr, "Light_SubHutBulkhead_Batt", c + new Vector3(-0.8f, 0.16f + 2.45f, -dz * 0.5f - 0.25f), new Vector3(60f, 180f, 0f), LightType.Spot, new Color(1f, 0.7f, 0.4f), 300f, 9f, LightGroupId.SubBattery, volumetricDimmer: 0.8f, spotOuterAngle: 120f);
        }

        /// <summary>Circuit state on the bus gantry: a small indicator box with one LED on each of the four lattice posts
        /// (north face, 1.7 m), green once the transfer lever has put the aux circuit on. Returned with the mimic LED.</summary>
        private static Renderer[] TransferExtras(Transform parent, Renderer mimicLed, WorldMaterialPalette p)
        {
            var g = new GameObject("BusGantry_StateLeds");
            g.transform.SetParent(parent, false);
            var list = new List<Renderer>();
            if (mimicLed != null) list.Add(mimicLed);
            foreach (float x in new[] { 260.2f, 273.0f })
                foreach (float z in new[] { -16.0f, -6.6f })
                {
                    Vector3 b = new Vector3(x, SubY + 1.7f, z + 0.26f);
                    Zone23Dressing.Box(g.transform, "GantryStateBox", b, new Vector3(0.16f, 0.2f, 0.08f), p.DarkPaintedMetal, Quaternion.identity).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Zone23Dressing.Box(g.transform, "GantryStateHood", b + new Vector3(0f, 0.11f, 0.03f), new Vector3(0.18f, 0.02f, 0.12f), p.DarkPaintedMetal, Quaternion.identity).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    GameObject led = Zone23Dressing.Box(g.transform, "GantryStateLed", b + new Vector3(0f, 0.03f, 0.042f), new Vector3(0.035f, 0.035f, 0.006f), p.EmissiveRed, Quaternion.identity);
                    var mr = led.GetComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    list.Add(mr);
                }
            return list.ToArray();
        }

        /// <summary>A small mimic panel on the switch cabinet's north face: six round LEDs (breakers 1-3, fuel, generator,
        /// transfer). No label strips: blank white plates read as unprinted signs (fix round). Each LED is driven by its station with the conduit LEDs.</summary>
        private static Renderer[] BuildMimicPanel(Transform parent, WorldMaterialPalette p)
        {
            var g = new GameObject("Substation_MimicPanel");
            g.transform.SetParent(parent, false);
            Quaternion r = Quaternion.LookRotation(Vector3.back, Vector3.up);   // panel normal +Z: its local -Z faces the walkway
            Vector3 c = new Vector3(279.5f, SubY + 1.42f, -6.26f);
            Zone23Dressing.Box(g.transform, "MimicPlate", c, new Vector3(0.66f, 0.40f, 0.03f), p.DarkPaintedMetal, r);
            Zone23Dressing.Box(g.transform, "MimicBezel", c + new Vector3(0f, 0.215f, 0.005f), new Vector3(0.70f, 0.03f, 0.04f), p.BrushedSteel, r);
            Zone23Dressing.Box(g.transform, "MimicBusbar", c + new Vector3(0f, 0.06f, 0.018f), new Vector3(0.56f, 0.012f, 0.004f), Zone23Dressing.Coping(p), r);
            Material white = Zone23Dressing.Coping(p);
            var leds = new Renderer[6];
            for (int i = 0; i < 6; i++)
            {
                float x = -0.25f + i * 0.1f;
                Zone23Dressing.Box(g.transform, $"MimicDrop_{i}", c + new Vector3(x, 0.025f, 0.018f), new Vector3(0.008f, 0.07f, 0.004f), white, r);
                GameObject led = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                led.name = $"MimicLed_{i}";
                led.layer = GameLayers.Environment;
                led.transform.SetParent(g.transform, false);
                led.transform.position = c + new Vector3(x, -0.04f, 0.022f);
                led.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                led.transform.localScale = new Vector3(0.038f, 0.008f, 0.038f);
                Object.DestroyImmediate(led.GetComponent<Collider>());
                var mr = led.GetComponent<MeshRenderer>();
                mr.sharedMaterial = p.EmissiveRed;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                leds[i] = mr;
            }
            return leds;
        }

        /// <summary>Conduit height inside a trough (fix round: the conduits sat ON a 8.6 cm trough and still read as a
        /// tripwire across the walkway; now they lie inside 12 cm sides on a 3 cm bed).</summary>
        private const float TroughConduitY = 0.065f;

        private static void BuildConduitTrough(Transform parent, Vector3 a, Vector3 b, WorldMaterialPalette p, float lidFrom = -1f, float lidTo = -1f)
        {
            Vector3 dir = b - a; float len = dir.magnitude; if (len < 0.1f) return; dir /= len;
            Quaternion r = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 m = (a + b) * 0.5f;
            Zone23Dressing.Box(parent, "TroughBed", m + Vector3.up * 0.015f, new Vector3(0.2f, 0.03f, len), Zone23Dressing.Plinth(p), r);
            Zone23Dressing.Box(parent, "TroughSideA", m + r * new Vector3(0.13f, 0.06f, 0f), new Vector3(0.06f, 0.12f, len), Zone23Dressing.Kerb(p), r);
            Zone23Dressing.Box(parent, "TroughSideB", m + r * new Vector3(-0.13f, 0.06f, 0f), new Vector3(0.06f, 0.12f, len), Zone23Dressing.Kerb(p), r);
            // a chequer-plate lid where the trough crosses the walkway (distances along a->b)
            if (lidTo > lidFrom && lidFrom >= 0f)
            {
                float l0 = Mathf.Clamp(lidFrom, 0f, len), l1 = Mathf.Clamp(lidTo, 0f, len);
                for (float t = l0; t < l1 - 0.05f; t += 1.2f)
                {
                    float seg = Mathf.Min(1.2f, l1 - t) - 0.01f;
                    Zone23Dressing.Box(parent, "TroughLid", a + dir * (t + seg * 0.5f) + Vector3.up * 0.126f, new Vector3(0.26f, 0.012f, seg), p.BrushedSteel, r).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        private static void BuildOpenSubGate(Transform parent, float x, float outward, WorldMaterialPalette p)
        {
            var g = new GameObject($"SubGate_{(outward < 0 ? "West" : "East")}");
            g.transform.SetParent(parent, false);
            const float y0 = SubY, hgt = 4.4f, leafW = 3.1f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 post = new Vector3(x, y0, s * 3.32f);
                Zone23Dressing.Box(g.transform, "GatePost", post + Vector3.up * (hgt * 0.5f + 0.1f), new Vector3(0.22f, hgt + 0.2f, 0.22f), p.DarkPaintedMetal, Quaternion.identity, collider: true);
                Zone23Dressing.Box(g.transform, "GatePostCap", post + Vector3.up * (hgt + 0.22f), new Vector3(0.28f, 0.04f, 0.28f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Decal(g.transform, "GatePostHazard", post + new Vector3(-outward * 0.0f, 0.8f, 0f) + Vector3.right * outward * 0.2f, Vector3.right * outward, new Vector3(0.3f, 1.4f, 0.5f), p.DecalCautionStripes, 0.95f);
                // the leaf, hinged on this post, swung ~165 deg outward so it lies against the outside of the fence
                // closed it would point at the other post; open it points away from the gap along the fence, 15 deg out
                Vector3 openDir = new Vector3(outward * 0.906f, 0f, s * 0.423f);   // swung ~115 deg: clear of the fence (two overlapping wire layers read as a solid board)
                Quaternion lr = Quaternion.LookRotation(openDir, Vector3.up);
                Vector3 lc = post + openDir * (leafW * 0.5f + 0.12f) + Vector3.right * outward * 0.18f;
                GameObject leafMesh = Zone23Dressing.Box(g.transform, "LeafMesh", lc + Vector3.up * (hgt * 0.5f + 0.08f), new Vector3(0.03f, hgt - 0.2f, leafW - 0.1f), p.ChainlinkFence, lr);
                Zone23Dressing.ThinChainlinkBox(leafMesh);   // one wire plane (a 3 cm box drew two layers: a grey sheet)
                Zone23Dressing.Box(g.transform, "LeafTop", lc + Vector3.up * (hgt - 0.02f), new Vector3(0.06f, 0.06f, leafW), p.DarkPaintedMetal, lr);
                Zone23Dressing.Box(g.transform, "LeafBottom", lc + Vector3.up * 0.14f, new Vector3(0.06f, 0.06f, leafW), p.DarkPaintedMetal, lr);
                Zone23Dressing.Box(g.transform, "LeafMid", lc + Vector3.up * (hgt * 0.5f), new Vector3(0.05f, 0.05f, leafW), p.DarkPaintedMetal, lr);
                Zone23Dressing.Box(g.transform, "LeafStileOuter", lc + openDir * (leafW * 0.5f) + Vector3.up * (hgt * 0.5f + 0.06f), new Vector3(0.06f, hgt - 0.1f, 0.06f), p.DarkPaintedMetal, lr);
                Zone23Dressing.Box(g.transform, "LeafStileInner", lc - openDir * (leafW * 0.5f) + Vector3.up * (hgt * 0.5f + 0.06f), new Vector3(0.06f, hgt - 0.1f, 0.06f), p.DarkPaintedMetal, lr);
                Zone23Dressing.SignPlate(g.transform, "Sign_HV_Leaf", lc + Vector3.up * 1.7f + Vector3.right * outward * 0.03f, Vector3.right * outward, 0.4f, 0.55f, Zone23Dressing.SignMat(p, "MAT_Z23_SignHighVoltage", "high_voltage"));
            }
            // drop-bolt socket and wheel ruts in the apron
            Zone23Dressing.Decal(g.transform, "ApronRuts", new Vector3(x + outward * 3.0f, y0 - 0.0f, 0f), Vector3.up, new Vector3(5.0f, 6.0f, 1.0f), Zone23Dressing.Grime(p, 0), 0.6f);
        }

        private static void BuildBreakerStation(Transform parent, string anchorId, SubstationStationType type, Vector3 pos, float yawDeg, WorldMaterialPalette p, Renderer mimicLed = null)
        {
            // P1-9 FIX: Heavy grounded concrete footing + galvanized steel H-frame pedestal + backboard supporting each breaker enclosure flush to the pad!
            var rack = new GameObject($"BreakerPedestal_{anchorId}");
            rack.transform.SetParent(parent, false);
            rack.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));

            WorldArchitectureKit.CreateBox(rack.transform, "ConcreteFooting", new Vector3(0f, 0.08f, 0f), new Vector3(1.22f, 0.16f, 0.78f), p.ConcreteBoardform, addCollider: true);
            WorldArchitectureKit.CreateBox(rack.transform, "SteelLeg_L", new Vector3(-0.46f, 0.98f, -0.14f), new Vector3(0.11f, 1.96f, 0.11f), p.DarkPaintedMetal, addCollider: true);
            WorldArchitectureKit.CreateBox(rack.transform, "SteelLeg_R", new Vector3(+0.46f, 0.98f, -0.14f), new Vector3(0.11f, 1.96f, 0.11f), p.DarkPaintedMetal, addCollider: true);
            WorldArchitectureKit.CreateBox(rack.transform, "SupportShelf", new Vector3(0f, 0.56f, 0.02f), new Vector3(1.04f, 0.08f, 0.58f), p.BrushedSteel, addCollider: true);
            WorldArchitectureKit.CreateBox(rack.transform, "BackboardPlate", new Vector3(0f, 1.24f, -0.18f), new Vector3(1.04f, 1.38f, 0.08f), p.DarkPaintedMetal, addCollider: true);
            WorldArchitectureKit.CreateBox(rack.transform, "ConduitRiser_L", new Vector3(-0.22f, 0.36f, 0.02f), new Vector3(0.07f, 0.62f, 0.07f), p.BrushedSteel, addCollider: false);
            WorldArchitectureKit.CreateBox(rack.transform, "ConduitRiser_R", new Vector3(+0.22f, 0.36f, 0.02f), new Vector3(0.07f, 0.62f, 0.07f), p.BrushedSteel, addCollider: false);

            p.InstantiatePropOrFallback("breaker_box", rack.transform, pos + Vector3.up * 0.60f, new Vector3(0f, yawDeg, 0f), new Vector3(0.85f, 1.32f, 0.48f), grounded: true, addCollider: true, fallbackMat: p.DarkPaintedMetal, customName: $"Prop_{anchorId}");
            WorldArchitectureKit.BuildWorldAnchor(parent, anchorId, pos + Vector3.up * 1.15f);
            Vector3 conduitEnd = new Vector3(272.0f, SubY + TroughConduitY, 9.0f);
            // the conduit runs inside its trough; its state shows on a small LED box on the exposed riser (eye can see it)
            Vector3 fwd = rack.transform.forward;
            Vector3 riserTop = rack.transform.TransformPoint(new Vector3(-0.22f, 0.5f, 0.06f));
            Zone23Dressing.Box(rack.transform, "RiserLedBox", riserTop, new Vector3(0.09f, 0.11f, 0.05f), p.DarkPaintedMetal, rack.transform.rotation).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject riserLed = Zone23Dressing.Box(rack.transform, "RiserLed", riserTop + fwd * 0.028f + Vector3.up * 0.02f, new Vector3(0.026f, 0.026f, 0.008f), p.EmissiveRed, rack.transform.rotation);
            riserLed.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var extra = new List<Renderer> { riserLed.GetComponent<MeshRenderer>() };
            if (mimicLed != null) extra.Add(mimicLed);
            BuildSubstationInteractStation(parent, $"Station_{anchorId}", type, pos + Vector3.up * 1.15f, new Vector3(1.1f, 1.2f, 1.1f),
                new[] { pos + Vector3.up * TroughConduitY, new Vector3(pos.x, SubY + TroughConduitY, 9.0f), conduitEnd }, p, extra.ToArray(), clampLeds: false);
        }

        private static void BuildSubstationInteractStation(
            Transform parent,
            string name,
            SubstationStationType type,
            Vector3 interactPos,
            Vector3 colliderSize,
            Vector3[] conduitPath,
            WorldMaterialPalette p,
            Renderer[] extraPowerRenderers = null,
            bool clampLeds = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = interactPos;
            go.layer = GameLayers.Interactable;

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = colliderSize;

            // Animated handle/lever arm pivot
            var pivot = new GameObject("AnimatedHandlePivot");
            pivot.transform.SetParent(go.transform, false);
            if (type == SubstationStationType.TransferLever)
            {
                // round 4 (review 3, 20 vs 21: 'the lever has exactly the same pose'): the animated arm sat inside the
                // transfer pedestal. It is now a knife-switch lever on the pedestal's west face (the side the player
                // works from) that swings 90 degrees across the face from A (north) to B (south), with an A/B plate.
                Vector3 face = new Vector3(277.8f - 0.37f, SubY + 1.32f, -6.86f);
                pivot.transform.position = face + Vector3.left * 0.05f;
                Zone23Dressing.Box(go.transform, "LeverPlate", face + Vector3.left * 0.01f, new Vector3(0.02f, 0.5f, 0.46f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Box(go.transform, "LeverPlateA", face + new Vector3(-0.022f, 0.19f, 0.17f), new Vector3(0.006f, 0.07f, 0.07f), Zone23Dressing.PaintYellow(p), Quaternion.identity);
                Zone23Dressing.Box(go.transform, "LeverPlateB", face + new Vector3(-0.022f, 0.19f, -0.17f), new Vector3(0.006f, 0.07f, 0.07f), p.PaintedMetalGreen, Quaternion.identity);
                Zone23Dressing.Cyl(go.transform, "LeverBoss", face + Vector3.left * 0.02f, face + Vector3.left * 0.08f, 0.04f, p.BrushedSteel);
                WorldArchitectureKit.CreateBox(pivot.transform, "HandleArm", new Vector3(0f, 0.2f, 0f), new Vector3(0.04f, 0.42f, 0.05f), Zone23Dressing.PaintRed(p), addCollider: false);
                WorldArchitectureKit.CreateBox(pivot.transform, "HandleGrip", new Vector3(-0.03f, 0.43f, 0f), new Vector3(0.1f, 0.11f, 0.06f), Zone23Dressing.Rubber(p), addCollider: false);
            }
            else
                WorldArchitectureKit.CreateBox(pivot.transform, "HandleArm", new Vector3(0f, 0.16f, 0f), new Vector3(0.05f, 0.34f, 0.05f), Zone23Dressing.PaintRed(p), addCollider: false);

            // Diegetic status LED bulb + emissive housing
            // a 4 cm domed LED in a dark bezel on top of the box (was a bare 14 cm glowing cube)
            GameObject bezel = Zone23Dressing.Cyl(go.transform, "StatusLedBezel", interactPos + Vector3.up * 0.35f, interactPos + Vector3.up * 0.372f, 0.034f, p.DarkPaintedMetal);
            if (bezel != null) bezel.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject ledMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ledMesh.name = "StatusLED";
            ledMesh.layer = GameLayers.Environment;
            Object.DestroyImmediate(ledMesh.GetComponent<Collider>());
            ledMesh.transform.SetParent(go.transform, false);
            ledMesh.transform.localPosition = new Vector3(0f, 0.372f, 0f);
            ledMesh.transform.localScale = new Vector3(0.04f, 0.022f, 0.04f);
            ledMesh.GetComponent<MeshRenderer>().sharedMaterial = p.EmissiveRed;
            ledMesh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lightGo = new GameObject("StatusLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.56f, 0f);
            Light statusLight = lightGo.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.color = new Color(1f, 0.16f, 0.12f);
            statusLight.intensity = 18f;
            statusLight.range = 3.2f;
            lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();

            // Power conduit along the compound floor: plain galvanised steel (review 1: a glowing rod read as a laser
            // tripwire). Its state shows on small LEDs riding on clamps along it and on the mimic panel at the switch
            // cabinet (fault red -> powered green, driven by SubstationStation).
            var powerRenderers = new List<Renderer>();
            if (conduitPath != null && conduitPath.Length >= 2)
            {
                WorldArchitectureKit.BuildConduitRun(parent, $"Conduit_{name}", conduitPath, 0.032f, Zone23Dressing.ConduitGrey(p));
                if (clampLeds) powerRenderers.AddRange(Zone23Dressing.ConduitLeds(parent, $"ConduitLeds_{name}", conduitPath, 0.032f, p));
            }
            if (extraPowerRenderers != null) powerRenderers.AddRange(extraPowerRenderers);
            Renderer[] conduitRenderers = powerRenderers.ToArray();

            SubstationStation st = go.AddComponent<SubstationStation>();
            var so = new SerializedObject(st);
            so.FindProperty("stationType").enumValueIndex = (int)type;
            so.FindProperty("animatedPivot").objectReferenceValue = pivot.transform;
            so.FindProperty("statusLight").objectReferenceValue = statusLight;

            SerializedProperty ledProp = so.FindProperty("statusLedRenderers");
            ledProp.arraySize = 1;
            ledProp.GetArrayElementAtIndex(0).objectReferenceValue = ledMesh.GetComponent<MeshRenderer>();

            if (conduitRenderers != null && conduitRenderers.Length > 0)
            {
                SerializedProperty cp = so.FindProperty("powerConduitRenderers");
                cp.arraySize = conduitRenderers.Length;
                for (int i = 0; i < conduitRenderers.Length; i++)
                    cp.GetArrayElementAtIndex(i).objectReferenceValue = conduitRenderers[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =========================================================================
        // EAST-ONLY GREEN AUXILIARY ESCAPE ROUTE BEACONS (X >= 288 -> 398, POS-8 FIX!)
        // =========================================================================

        private static void BuildEastOnlyAuxRouteBeacons(Transform parent, WorldMaterialPalette p)
        {
            var route = new GameObject("AuxRoute_EastEscapeBeacons");
            route.transform.SetParent(parent, false);

            // Strictly EAST of Substation 3 (X = 288 .. 398) guiding fleeing players straight to Old Yard squeeze gap (401, 1, -22.8)
            Vector3[] beaconCoords =
            {
                new(288f, 2.25f, -2.0f),
                new(304f, 1.90f, -6.2f),
                new(320f, 1.60f, -10.5f),
                new(336f, 1.40f, -14.2f),
                new(352f, 1.25f, -17.6f),
                new(368f, 1.15f, -20.2f),
                new(384f, 1.05f, -21.8f),
                new(397.5f, 1.00f, -22.8f)
            };

            // Road 3 centre line east of the substation (Zone4 ServiceRoad3Points); bollards stand 3.4 m off it,
            // alternating sides, seated on the terrain (the old Y guesses floated up to 0.8 m).
            Vector2[] road = { new(286f, 0f), new(310f, -8f), new(360f, -20f), new(401f, -22.8f) };
            for (int i = 0; i < beaconCoords.Length; i++)
            {
                Vector2 q = new Vector2(beaconCoords[i].x, beaconCoords[i].z);
                Vector2 best = q, tan = Vector2.right; float bestD = float.MaxValue;
                for (int k = 0; k < road.Length - 1; k++)
                {
                    Vector2 ab = road[k + 1] - road[k];
                    float t = Mathf.Clamp01(Vector2.Dot(q - road[k], ab) / ab.sqrMagnitude);
                    Vector2 c = road[k] + ab * t;
                    if ((c - q).sqrMagnitude < bestD) { bestD = (c - q).sqrMagnitude; best = c; tan = ab.normalized; }
                }
                Vector2 nrm = new Vector2(-tan.y, tan.x);
                float side = i == beaconCoords.Length - 1 ? -1f : (i % 2 == 0 ? 1f : -1f);   // the last one stands south of the squeeze gap
                Vector2 o = best + nrm * side * (i == beaconCoords.Length - 1 ? 2.6f : 3.4f);
                float gy = Zone23Dressing.Ground(o.x, o.y);
                if (o.x > 285.8f && o.x < 293f && Mathf.Abs(o.y) < 5.5f) gy = Mathf.Max(gy, SubY - 0.04f);   // on the east gravel apron
                Vector3 pos = new Vector3(o.x, gy, o.y);
                p.InstantiatePropOrFallback("bollard_light", route.transform, pos, new Vector3(0f, Mathf.Atan2(tan.x, tan.y) * Mathf.Rad2Deg, 0f), new Vector3(0.32f, 1.15f, 0.32f), grounded: true, addCollider: true, customName: $"AuxRouteBollard_{i}");
                // one lens per bollard: LookFixtures.BollardCollar adds the glowing collar (review 1: a cap here as well
                // gave two stacked green discs)
                Zone23Dressing.Plant(p, route.transform, i % 2 == 0 ? "grass_medium_02" : "fern_02", pos + new Vector3(0.35f, 0f, 0.2f), i * 77f, 0.7f, 0.05f, shadows: false);
                WorldArchitectureKit.BuildHdrpLight(route.transform, $"Light_AuxRouteGreen_{i}", pos + Vector3.up * 1.35f, Vector3.zero, LightType.Point,
                    new Color(0.18f, 1.0f, 0.36f), 380f, 12f, LightGroupId.AuxRoute, volumetricDimmer: 0.75f, sequenceOrder: i);
            }
            // T. rex prints down Road 3 from the substation's east gate (the chase starts here)
            Material trexPrint = Zone23Dressing.DecalMat(p, "MAT_Z23_DecalTRexPrint", "footprint_trex_albedo", "footprint_trex_normal", new Color(0.1f, 0.08f, 0.06f, 0.85f));
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                Vector2 c = Vector2.Lerp(new Vector2(296f, 5.5f), new Vector2(322f, -6.5f), t) + new Vector2(0f, i % 2 == 0 ? 0.9f : -0.9f);
                Vector3 pos = new Vector3(c.x, Zone23Dressing.Ground(c.x, c.y) + 0.3f, c.y);
                Zone23Dressing.Decal(route.transform, $"TRexPrint_{i}", pos, Vector3.up, new Vector3(1.3f, 1.6f, 1.2f), trexPrint, 0.9f, rollDeg: 245f);
            }
        }

        // =========================================================================
        // OLD MAINTENANCE YARD (1990s), WAREHOUSE B, WORKSHOP & OUTWARD E-0 GATE
        // (X: 401 .. 505, Z: -95 .. +50, Y = 1.0)
        // =========================================================================

        private static void BuildOldMaintenanceYard(Transform parent, WorldMaterialPalette p)
        {
            var yard = new GameObject("OldMaintenanceYard_1990s");
            yard.transform.SetParent(parent, false);

            // Concrete yard slab (X: 397..507, Z: -95.5..+50.5, top Y = 1.0), cut around the Warehouse B and workshop
            // footprints so their floors (also topping out at 1.0) never share a plane with it.
            (string n, float x0, float x1, float z0, float z1)[] slabParts =
            {
                ("Yard_ConcreteSlab", 397f, 507f, -95.5f, -22f),
                ("Yard_ConcreteSlab_WsWest", 397f, 412f, -22f, -10f),
                ("Yard_ConcreteSlab_WsEast", 462f, 507f, -22f, -10f),
                ("Yard_ConcreteSlab_Mid", 397f, 507f, -10f, 4f),
                ("Yard_ConcreteSlab_WhWest", 397f, 414f, 4f, 36f),
                ("Yard_ConcreteSlab_WhEast", 456f, 507f, 4f, 36f),
                ("Yard_ConcreteSlab_North", 397f, 507f, 36f, 50.5f),
            };
            Material yardConcrete = Zone23Dressing.SlabConcrete(p);
            foreach (var sp in slabParts)
            {
                GameObject slab = WorldArchitectureKit.CreateBox(yard.transform, sp.n, new Vector3((sp.x0 + sp.x1) * 0.5f, YardY - 0.30f, (sp.z0 + sp.z1) * 0.5f), new Vector3(sp.x1 - sp.x0, 0.60f, sp.z1 - sp.z0), yardConcrete, addCollider: true);
                WorldArchitectureKit.ApplySurface(slab, SurfaceType.Concrete, p);
            }
            DressYardGround(yard.transform, p);
            _p = p;

            // Perimeter Walls (5.0 m weathered concrete)
            // West Wall (X = 401, Z = -95 -> +50, len = 145) — Squeeze Gap at Z = -22.8 (CenterAlong = 72.2 m, W = 1.6, H = 3.2)!
            // ZERO hovering concrete block above gap! Clean integrated wall lintel!
            WorldArchitectureKit.BuildWallWithOpenings(
                yard.transform,
                new Vector3(401f, 0f, -95f),
                new Vector3(401f, 0f, 50f),
                YardY, 5.0f, 0.45f, Zone23Dressing.PanelB(p),
                new[] { OpeningSpec.Arch(72.2f, 1.6f, 3.2f) },
                palette: p, wallName: "Wall_Yard_WestPerimeter");

            // COL-5/COL-8 FIX: Solid outward-hinged bent rusty door placed cleanly OUTSIDE the clear 1.45 m squeeze lane!
            WorldArchitectureKit.CreateBox(yard.transform, "SqueezeGap_BentRustyDoor", new Vector3(401.95f, YardY + 1.15f, -21.35f), new Vector3(1.15f, 2.30f, 0.08f), p.RustedIron, addCollider: true, euler: new Vector3(0f, -24f, 6f));
            // DinoBlocker slab across squeeze gap so T. rex slams the wall and NEVER clips into Old Yard!
            CreateDinoBlockerSlab(yard.transform, "SqueezeGap_DinoBlocker", new Vector3(400.6f, YardY + 2.5f, -22.8f), new Vector3(1.2f, 6.0f, 4.0f));

            WorldArchitectureKit.BuildWorldAnchor(yard.transform, "yard_gap", new Vector3(401.0f, YardY, -22.8f));
            WorldArchitectureKit.BuildWorldAnchor(yard.transform, "yard_gate_outside", new Vector3(397.0f, YardY, -22.8f));
            WorldArchitectureKit.BuildStoryTrigger(yard.transform, "yard_early_warn", new Vector3(398.0f, YardY + 1.5f, -22.8f), new Vector3(4.0f, 4.0f, 6.0f), oneShot: true);
            WorldArchitectureKit.BuildStoryTrigger(yard.transform, "reach_yard", new Vector3(403.0f, YardY + 1.5f, -22.8f), new Vector3(2.2f, 4.0f, 3.4f), oneShot: true);

            // North & South Perimeter Walls
            WorldArchitectureKit.BuildWallWithOpenings(yard.transform, new Vector3(401f, 0f, 50f), new Vector3(505f, 0f, 50f), YardY, 5.0f, 0.45f, Zone23Dressing.PanelB(p), null, palette: p, wallName: "Wall_Yard_NorthPerimeter");
            WorldArchitectureKit.BuildWallWithOpenings(yard.transform, new Vector3(401f, 0f, -95f), new Vector3(505f, 0f, -95f), YardY, 5.0f, 0.45f, Zone23Dressing.PanelB(p), null, palette: p, wallName: "Wall_Yard_SouthPerimeter");

            // East Perimeter Wall (X = 505, Z = -95 -> +50, len = 145) — Service Exit E-0 opening at Z = -46.0 (CenterAlong = 49.0 m, W = 8.0, H = 4.2)!
            WorldArchitectureKit.BuildWallWithOpenings(
                yard.transform,
                new Vector3(505f, 0f, -95f),
                new Vector3(505f, 0f, 50f),
                YardY, 5.0f, 0.45f, Zone23Dressing.PanelB(p),
                new[] { OpeningSpec.Arch(49.0f, 8.0f, 4.2f, frameMat: p.DarkPaintedMetal) },
                palette: p, wallName: "Wall_Yard_EastPerimeter");

            // Perimeter dressing: coping, outside pilasters, panel joints, plinth, grime, moss, weeds, jungle beyond
            DressPerimeterWall(yard.transform, "West", new Vector3(401f, 0f, -95f), new Vector3(401f, 0f, 50f), new Vector2(71.0f, 73.4f), 1);
            DressPerimeterWall(yard.transform, "North", new Vector3(401f, 0f, 50f), new Vector3(505f, 0f, 50f), Vector2.zero, 2);
            DressPerimeterWall(yard.transform, "South", new Vector3(401f, 0f, -95f), new Vector3(505f, 0f, -95f), Vector2.zero, 3);
            DressPerimeterWall(yard.transform, "East", new Vector3(505f, 0f, -95f), new Vector3(505f, 0f, 50f), new Vector2(44.6f, 53.4f), 4);

            // Build Warehouse B, 1990s Workshop/Office Block, and Outward-Swinging Service Exit E-0
            BuildWarehouseB(yard.transform, p);
            BuildWorkshopAndOfficeBlock(yard.transform, p);
            BuildExitCourtAndE0Gate(yard.transform, p);

            DressYardClutter(yard.transform, p);
            DressExitCourt(yard.transform, p);

            // Yard Raptor Spawns
            WorldArchitectureKit.BuildWorldAnchor(yard.transform, "yard_raptor_a", new Vector3(445f, YardY, 20f));
            WorldArchitectureKit.BuildWorldAnchor(yard.transform, "yard_raptor_b", new Vector3(470f, YardY, -30f));
        }

        // =========================================================================
        // OLD YARD GROUND (look pass): asphalt drive lanes over the cracked concrete, joints, paint, stencils,
        // puddles and stains. Overlays are 12 mm thick and collider-free: the slab underneath stays the floor.
        // =========================================================================

        private static void DressYardGround(Transform parent, WorldMaterialPalette p)
        {
            var g = new GameObject("Yard_Ground");
            g.transform.SetParent(parent, false);
            const float y = YardY;
            // panel joints every 6 m (under the asphalt where they cross it)
            for (float x = 403f; x < 506f; x += 6f)
                Zone23Dressing.Box(g.transform, "YardJointX", new Vector3(x, y + 0.002f, -22.5f), new Vector3(0.035f, 0.004f, 145.6f), Zone23Dressing.DrainVoid(p), Quaternion.identity);
            for (float z = -91f; z < 50f; z += 6f)
                Zone23Dressing.Box(g.transform, "YardJointZ", new Vector3(452f, y + 0.0025f, z), new Vector3(109.6f, 0.004f, 0.035f), Zone23Dressing.DrainVoid(p), Quaternion.identity);

            // cast bays: every 6 m bay between the joints gets one of three concrete tones (3 mm overlays, no collider;
            // the joints stay on top), and about one bay in four a big oil/grime stain, so the slab stops reading as
            // one tiled texture. Building footprints are skipped (their floors share the slab's plane).
            Material[] bayMats = { Zone23Dressing.SlabPavingDark(p), Zone23Dressing.SlabPavingLight(p) };
            for (float bx = 403f; bx < 505f; bx += 6f)
                for (float bz = -91f; bz < 50f; bz += 6f)
                {
                    float x0 = Mathf.Max(bx, 401.3f), x1 = Mathf.Min(bx + 6f, 504.7f), z0 = Mathf.Max(bz, -94.7f), z1 = Mathf.Min(bz + 6f, 49.7f);
                    float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
                    bool inWh = x1 > 413.8f && x0 < 456.2f && z1 > 3.8f && z0 < 36.2f;
                    bool inWs = x1 > 411.8f && x0 < 462.2f && z1 > -22.2f && z0 < -9.8f;
                    bool inDock = x1 > 466.8f && x0 < 501.2f && z1 > -67.2f && z0 < -57.4f;
                    if (inWh || inWs || inDock) continue;
                    float hv = Mathf.Repeat(Mathf.Sin(bx * 3.17f + bz * 7.31f) * 4375.85f, 1f);
                    if (hv < 0.28f) Zone23Dressing.Box(g.transform, "YardBayDark", new Vector3(cx, y + 0.0015f, cz), new Vector3(x1 - x0 - 0.035f, 0.003f, z1 - z0 - 0.035f), bayMats[0], Quaternion.identity);
                    else if (hv > 0.74f) Zone23Dressing.Box(g.transform, "YardBayLight", new Vector3(cx, y + 0.0015f, cz), new Vector3(x1 - x0 - 0.035f, 0.003f, z1 - z0 - 0.035f), bayMats[1], Quaternion.identity);
                    float hs = Mathf.Repeat(Mathf.Sin(bx * 1.93f + bz * 4.07f) * 2531.7f, 1f);
                    if (hs > 0.76f)
                        Zone23Dressing.Decal(g.transform, "YardBayStain", new Vector3(cx + (hs - 0.88f) * 8f, y + 0.05f, cz + (hv - 0.5f) * 2f), Vector3.up, new Vector3(3.2f + 2.4f * hv, 3.6f + 2f * hs, 0.4f), Zone23Dressing.Grime(p, (int)(bx + bz)), 0.65f, rollDeg: 360f * hv);
                }

            // round 3 (review 2, 23_ch6_yard_entry: the slab in front of the player read bone-dry next to the mirroring
            // asphalt): standing water on the entry slab where it mirrors the workshop's west wall lamp and the drive lamp
            (float x, float z, float sx, float sz, float yaw)[] entryPuddles =
            {
                (406.4f, -21.0f, 3.2f, 2.0f, 25f), (407.6f, -25.9f, 2.8f, 1.7f, -10f), (410.6f, -23.4f, 2.2f, 1.4f, 60f), (404.6f, -18.6f, 1.8f, 1.2f, 80f),
            };
            for (int i = 0; i < entryPuddles.Length; i++)
            {
                var ep = entryPuddles[i];
                WorldArchitectureKit.BuildDecal(g.transform, $"YardEntryPuddle_{i}", new Vector3(ep.x, y + 0.05f, ep.z), new Vector3(90f, ep.yaw, 0f), new Vector3(ep.sx, ep.sz, 0.5f), p.DecalPuddleWet, 0.62f);
            }

            // asphalt drives: squeeze gap -> south of the workshop -> exit court -> E-0; north drive to Warehouse B
            (string n, float x0, float x1, float z0, float z1)[] lanes =
            {
                ("Asphalt_DriveSouth", 401.6f, 466f, -31.5f, -24.5f),
                ("Asphalt_ExitCourt", 466f, 504.6f, -58f, -22.5f),
                ("Asphalt_DriveNorth", 462.5f, 470f, -22.5f, 30f),
                ("Asphalt_WhEastSpur", 456.2f, 462.5f, 16f, 26f),
                ("Asphalt_WhSouthApron", 427f, 438f, -10f, 3.85f),
            };
            for (int i = 0; i < lanes.Length; i++)
            {
                var l = lanes[i];
                Zone23Dressing.Box(g.transform, l.n, new Vector3((l.x0 + l.x1) * 0.5f, y + 0.006f + i * 0.0007f, (l.z0 + l.z1) * 0.5f), new Vector3(l.x1 - l.x0, 0.012f, l.z1 - l.z0), p.AsphaltWet, Quaternion.identity);
            }
            // paint: yellow centre dashes on the south drive, white edge lines, a stop bar before the court
            Material yellow = p.Surface("MAT_Z23_RoadPaintYellow", "concrete_polished", "BaseColor", "MaskMap", 1.5f, new Color(1.9f, 1.45f, 0.35f), 0.25f, 0.7f, 0f, 0.5f);
            Material white = p.Surface("MAT_Z23_RoadPaintWhite", "concrete_polished", "BaseColor", "MaskMap", 1.5f, new Color(2.1f, 2.1f, 2.05f), 0.25f, 0.7f, 0f, 0.5f);
            for (float x = 405f; x < 462f; x += 4f)
                Zone23Dressing.Box(g.transform, "PaintDash", new Vector3(x + 1f, y + 0.0145f, -28f), new Vector3(2f, 0.003f, 0.12f), yellow, Quaternion.identity);
            Zone23Dressing.Box(g.transform, "PaintEdgeN", new Vector3(433.8f, y + 0.0145f, -24.85f), new Vector3(64.4f, 0.003f, 0.1f), white, Quaternion.identity);
            Zone23Dressing.Box(g.transform, "PaintEdgeS", new Vector3(433.8f, y + 0.0145f, -31.15f), new Vector3(64.4f, 0.003f, 0.1f), white, Quaternion.identity);
            Zone23Dressing.Box(g.transform, "PaintStopBar", new Vector3(465.4f, y + 0.0145f, -28f), new Vector3(0.35f, 0.003f, 6.6f), white, Quaternion.identity);
            // fix round: patched asphalt (fresher, darker repairs and trench reinstatements) so the court and the drive
            // stop reading as one empty slab from the dawn crane shot; 1.5 mm between the asphalt and the paint
            Material patch = p.Surface("MAT_Z23_AsphaltPatch", "asphalt_wet", "BaseColor", "MaskMap", 2.5f, new Color(0.8f, 0.8f, 0.82f), 0.2f, 0.62f, 0f, 1f);
            (float x, float z, float sx, float sz, float yaw)[] patches =
            {
                (473.5f, -31.0f, 5.5f, 3.2f, 0f), (481.0f, -36.5f, 3.0f, 7.5f, 0f), (478.5f, -47.5f, 6.5f, 2.4f, 0f), (472.0f, -57.0f, 4.0f, 2.8f, 0f),
                (484.5f, -27.5f, 2.6f, 2.6f, 0f), (466.8f, -40.0f, 1.2f, 17.0f, 0f), (455.0f, -28.4f, 7.0f, 2.2f, 0f), (436.0f, -27.0f, 3.6f, 2.8f, 0f),
                (420.0f, -29.6f, 9.0f, 1.1f, 0f), (466.2f, -8.0f, 2.8f, 5.5f, 0f),
            };
            foreach (var pt in patches)
                Zone23Dressing.Box(g.transform, "AsphaltPatch", new Vector3(pt.x, y + 0.0128f, pt.z), new Vector3(pt.sx, 0.0015f, pt.sz), patch, Quaternion.Euler(0f, pt.yaw, 0f));
            for (float z = -55f; z < -24f; z += 3.2f)    // parking bays along the court's west side
                Zone23Dressing.Box(g.transform, "PaintBay", new Vector3(469f, y + 0.0145f, z), new Vector3(5f, 0.003f, 0.1f), white, Quaternion.identity);

            // E-0 stencil on the court asphalt + hazard paint across the gate threshold
            var e0 = Zone23Dressing.Decal(g.transform, "Stencil_E0", new Vector3(497.5f, y + 0.1f, -46f), Vector3.up, new Vector3(5.6f, 2.8f, 0.4f),
                Zone23Dressing.DecalMat(p, "MAT_Z23_DecalE0Stencil", "e0_exit_decal", null, new Color(0.9f, 0.9f, 0.85f, 0.8f)), 0.85f, drawDistance: 70f);
            e0.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.right);
            Zone23Dressing.Decal(g.transform, "Hazard_E0Threshold", new Vector3(503.9f, y + 0.1f, -46f), Vector3.up, new Vector3(1.0f, 8.0f, 0.4f), p.DecalCautionStripes, 0.9f);

            // standing water and oil
            (float x, float z, float sx, float sz)[] puddles =
            {
                (404.5f, -27f, 3.2f, 2.4f), (420f, -30.4f, 5.0f, 2.2f), (441f, -25.6f, 3.6f, 2.0f), (458f, -29.6f, 4.4f, 2.6f),
                (476f, -34f, 2.6f, 1.6f), (490f, -50f, 3.2f, 2.0f), (486f, -27f, 3.0f, 2.2f), (466f, 6f, 3.4f, 5.2f),
                (432f, -4f, 4.2f, 3.0f), (480f, 20f, 6.0f, 4.0f), (430f, -60f, 7.0f, 4.0f), (452f, -82f, 5.0f, 3.0f),
            };
            for (int i = 0; i < puddles.Length; i++)
                WorldArchitectureKit.BuildDecal(g.transform, $"YardPuddle_{i}", new Vector3(puddles[i].x, y + 0.05f, puddles[i].z), new Vector3(90f, 23f * i, 0f), new Vector3(puddles[i].sx, puddles[i].sz, 0.5f), p.DecalPuddleWet, 0.82f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 c = new Vector3(412f + i * 9.3f, y + 0.05f, -27.5f + Mathf.Sin(i * 1.7f) * 2.5f);
                if (i >= 6) c = new Vector3(472f + (i - 6) * 8f, y + 0.05f, -40f - (i % 2) * 9f);
                Zone23Dressing.Decal(g.transform, $"YardOil_{i}", c, Vector3.up, new Vector3(1.5f, 2.1f, 0.4f), Zone23Dressing.Grime(p, i), 0.6f, rollDeg: 41f * i);
            }
            for (int i = 0; i < 9; i++)
            {
                Vector3 c = new Vector3(406f + (i * 37f) % 96f, y + 0.05f, -88f + (i * 53f) % 132f);
                Zone23Dressing.Decal(g.transform, $"YardCracks_{i}", c, Vector3.up, new Vector3(3.2f, 3.2f, 0.4f), i % 2 == 0 ? p.DecalCracks : Zone23Dressing.Cracks2(p), 0.85f, rollDeg: 67f * i);
            }
            // round 3: the cast-bay overlays stay separate renderers. Merged, their one mesh spanned the whole yard and the
            // wetness driver's single roof probe at its centre landed under the workshop roof, so every bay stayed dry and
            // matte in the storm (review 2, 23_ch6_yard_entry). Each bay now gets its own probe and the wet-floor clone.
            Zone23Dressing.MergeStatic(g.transform, t => !t.name.StartsWith("YardBay"));
        }

        private static WorldMaterialPalette _p;

        /// <summary>Detail on a kit perimeter wall (centre line a->b, 0.45 m thick, 5 m high on the slab). The kit wall
        /// keeps its colliders and openings; everything added here is collider-free. gap = along-wall range to skip.</summary>
        private static void DressPerimeterWall(Transform parent, string id, Vector3 a, Vector3 b, Vector2 gap, int seed)
        {
            WorldMaterialPalette p = _p;
            var root = new GameObject($"YardWall_{id}_Dressing");
            root.transform.SetParent(parent, false);
            const float thick = 0.45f, h = 5.0f, y0 = YardY;
            Vector3 d = b - a; d.y = 0f; float len = d.magnitude; d /= len;
            Vector3 n = new Vector3(-d.z, 0f, d.x);
            Vector3 yardCentre = new Vector3(452f, 0f, -22f);
            if (Vector3.Dot(yardCentre - (a + b) * 0.5f, n) < 0f) n = -n;   // n points into the yard
            Quaternion rot = Quaternion.LookRotation(d, Vector3.up);
            bool InGap(float s, float pad) => gap != Vector2.zero && s > gap.x - pad && s < gap.y + pad;

            float eps = seed * 0.002f;
            Zone23Dressing.Box(root.transform, "Coping", (a + b) * 0.5f + Vector3.up * (y0 + h + 0.085f + eps), new Vector3(thick + 0.16f, 0.17f, len + 0.62f), Zone23Dressing.Coping(p), rot);
            // plinth and joints, interrupted at the opening
            float s0 = 0f;
            foreach (float s1 in gap == Vector2.zero ? new[] { len } : new[] { gap.x, len })
            {
                float from = s0, to = s1;
                if (gap != Vector2.zero && Mathf.Approximately(s1, len)) from = gap.y;
                Vector3 pa = a + d * from, pb = a + d * to;
                if (to - from > 0.3f)
                    Zone23Dressing.Box(root.transform, "Plinth", (pa + pb) * 0.5f + Vector3.up * (y0 + 0.17f + eps), new Vector3(thick + 0.1f, 0.34f, to - from), Zone23Dressing.Plinth(p), rot);
                s0 = s1;
            }
            for (float s = 4f; s < len - 0.5f; s += 4f)
            {
                if (InGap(s, 0.4f)) continue;
                Vector3 c = a + d * s + Vector3.up * (y0 + h * 0.5f);
                Zone23Dressing.Box(root.transform, "JointIn", c + n * (thick * 0.5f + 0.003f), new Vector3(0.008f, h - 0.02f, 0.04f), Zone23Dressing.DrainVoid(p), rot);
                Zone23Dressing.Box(root.transform, "JointOut", c - n * (thick * 0.5f + 0.003f), new Vector3(0.008f, h - 0.02f, 0.04f), Zone23Dressing.DrainVoid(p), rot);
                // fix round: a pilaster at every joint (was every second), so the outside reads as precast bays
                Zone23Dressing.Box(root.transform, "Pilaster", c - n * (thick * 0.5f + 0.12f), new Vector3(0.24f, h, 0.55f), Zone23Dressing.Plinth(p), rot);
                Zone23Dressing.Box(root.transform, "PilasterCap", c - n * (thick * 0.5f + 0.13f) + Vector3.up * (h * 0.5f + 0.04f), new Vector3(0.3f, 0.08f, 0.65f), Zone23Dressing.Coping(p), rot);
            }
            // the outside faces the dawn crane shot looks down on (east, south): every 4 m bay a slightly different cast
            // tone (2 cm skin), a horizontal joint where two precast panels meet at 2.5 m, and a dark weathered foot
            if (id == "East" || id == "South")
            {
                Material[] tones = { Zone23Dressing.SlabConcreteDark(p), Zone23Dressing.PanelB(p), Zone23Dressing.SlabConcreteLight(p), Zone23Dressing.SlabConcreteDark(p) };
                for (float s = 0f; s < len - 0.5f; s += 4f)
                {
                    float sEnd = Mathf.Min(s + 4f, len);
                    float mid = (s + sEnd) * 0.5f;
                    if (InGap(mid, 2.2f)) continue;
                    int k = Mathf.Abs(Mathf.RoundToInt(Mathf.Sin(mid * 1.37f + seed) * 1000f)) % tones.Length;
                    Vector3 c = a + d * mid - n * (thick * 0.5f + 0.01f);
                    if (k != 1) Zone23Dressing.Box(root.transform, "PanelToneOut", c + Vector3.up * (y0 + 2.6f), new Vector3(0.02f, 4.6f, sEnd - s - 0.3f), tones[k], rot);
                    Zone23Dressing.Box(root.transform, "JointOutH", c - n * 0.012f + Vector3.up * (y0 + 2.5f), new Vector3(0.012f, 0.035f, sEnd - s - 0.3f), Zone23Dressing.DrainVoid(p), rot);
                }
            }
            // round 4 (review 3, 28_ending_2: 'the perimeter wall is a plain tan band'): the inside faces the dawn crane
            // shot looks across (west, north) get a cast tone per 4 m bay, a shallow pier under the coping every 8 m, an
            // algae band and runoff streaks drawn to 200 m, and a security fence on the coping (posts with cranked arms,
            // a top rail, three strands of barbed wire)
            if (id == "West" || id == "North")
            {
                Material[] tonesIn = { Zone23Dressing.PanelA(p), Zone23Dressing.PanelB(p), Zone23Dressing.PanelC(p), Zone23Dressing.PanelB(p) };
                Material algae = Zone23Dressing.AlgaeStrong(p), runoff = Zone23Dressing.RunoffStrong(p), galvF = Zone23Dressing.GalvSteel(p);
                for (float s = 0f; s < len - 0.5f; s += 4f)
                {
                    float sEnd = Mathf.Min(s + 4f, len), mid = (s + sEnd) * 0.5f;
                    if (InGap(mid, 2.2f)) continue;
                    float hb = Mathf.Repeat(Mathf.Sin(mid * 2.37f + seed * 4.1f) * 2791.7f, 1f);
                    int k = (int)(hb * 4f) % 4;
                    Vector3 c = a + d * mid + n * (thick * 0.5f + 0.01f);
                    if (k != 1) Zone23Dressing.Box(root.transform, "PanelToneIn", c + Vector3.up * (y0 + 2.6f), new Vector3(0.02f, 4.6f, sEnd - s - 0.3f), tonesIn[k], rot);
                    Zone23Dressing.Decal(root.transform, "AlgaeBandIn", c + n * 0.14f + Vector3.up * (y0 + h - 0.38f), n, new Vector3(sEnd - s - 0.1f, 0.75f, 0.4f), algae, 0.9f, drawDistance: 200f);
                    int streaks = hb > 0.6f ? 2 : 1;
                    for (int j = 0; j < streaks; j++)
                    {
                        float hs = Mathf.Repeat(Mathf.Sin((mid + j * 1.7f) * 5.31f + seed) * 1931.3f, 1f);
                        float sh = 1.8f + 2.2f * hs;
                        Zone23Dressing.Decal(root.transform, "RunoffIn", a + d * (s + 0.6f + 2.8f * hs) + n * (thick * 0.5f + 0.2f) + Vector3.up * (y0 + h - sh * 0.5f - 0.05f), n,
                            new Vector3(0.6f + 0.7f * hs, sh, 0.45f), runoff, 0.85f, drawDistance: 200f);
                    }
                }
                for (float s = 8f; s < len - 1f; s += 8f)
                {
                    if (InGap(s, 1.2f)) continue;
                    Zone23Dressing.Box(root.transform, "PierIn", a + d * s + n * (thick * 0.5f + 0.08f) + Vector3.up * (y0 + h * 0.5f + 0.17f), new Vector3(0.16f, h - 0.34f, 0.42f), Zone23Dressing.Plinth(p), rot);
                }
                // fence on the coping
                float fy = y0 + h + 0.17f;
                Vector3 lean = (-n * 0.7f + Vector3.up).normalized;
                float fs0 = 0.3f;
                foreach (float fs1 in gap == Vector2.zero ? new[] { len - 0.3f } : new[] { gap.x - 0.6f, len - 0.3f })
                {
                    float from = fs0, to = fs1;
                    if (gap != Vector2.zero && Mathf.Approximately(fs1, len - 0.3f)) from = gap.y + 0.6f;
                    if (to - from > 1f)
                    {
                        Zone23Dressing.Cyl(root.transform, "FenceTopRail", a + d * from + Vector3.up * (fy + 1.5f), a + d * to + Vector3.up * (fy + 1.5f), 0.03f, galvF);
                        Zone23Dressing.Cyl(root.transform, "FenceMidWire", a + d * from + Vector3.up * (fy + 0.75f), a + d * to + Vector3.up * (fy + 0.75f), 0.01f, galvF);
                        for (int w = 0; w < 3; w++)
                        {
                            Vector3 o = Vector3.up * (fy + 1.5f) + lean * (0.15f + 0.14f * w);
                            Zone23Dressing.Cyl(root.transform, "FenceBarbWire", a + d * from + o, a + d * to + o, 0.012f, p.DarkPaintedMetal);
                        }
                        int posts = Mathf.Max(1, Mathf.RoundToInt((to - from) / 3f));
                        for (int pi = 0; pi <= posts; pi++)
                        {
                            Vector3 pb = a + d * Mathf.Lerp(from, to, pi / (float)posts) + Vector3.up * fy;
                            Zone23Dressing.Cyl(root.transform, "FencePost", pb, pb + Vector3.up * 1.52f, 0.035f, galvF);
                            Zone23Dressing.Cyl(root.transform, "FenceArm", pb + Vector3.up * 1.5f, pb + Vector3.up * 1.5f + lean * 0.48f, 0.025f, galvF);
                        }
                    }
                    fs0 = fs1;
                }
            }
            // grime, moss, weeds, jungle
            string[] weeds = { "grass_medium_02", "fern_02", "grass_medium_02", "fern_02", "grass_medium_02", "fern_02" };
            float[] wsize = { 0.6f, 0.8f, 0.5f, 1.0f, 0.45f, 0.7f };
            for (float s = 1.5f; s < len - 1f; s += 5.2f)
            {
                float hh = Mathf.Repeat(Mathf.Sin(s * 4.13f + seed * 9.7f) * 3571.3f, 1f);
                if (InGap(s, 2.2f)) continue;
                Vector3 basePt = a + d * s;
                if (hh > 0.35f)
                    Zone23Dressing.Decal(root.transform, "Grime", basePt + n * (thick * 0.5f + 0.2f) + Vector3.up * (y0 + h - 1.4f), n, new Vector3(1.2f + hh, 2.8f, 0.5f), Zone23Dressing.Grime(p, (int)(s * 7f)), 0.7f + 0.25f * hh);
                if (hh < 0.6f)
                {
                    Zone23Dressing.Decal(root.transform, "MossIn", basePt + n * (thick * 0.5f + 0.15f) + Vector3.up * (y0 + 0.45f), n, new Vector3(2.4f, 1.0f, 0.9f), Zone23Dressing.Moss(p), 0.85f);
                    Zone23Dressing.Decal(root.transform, "MossOut", basePt - n * (thick * 0.5f + 0.2f) + Vector3.up * (y0 + 0.6f), -n, new Vector3(3.0f, 1.6f, 1.2f), Zone23Dressing.Moss(p), 0.95f);
                }
                int w = (int)(hh * weeds.Length) % weeds.Length;
                Vector3 wp = basePt + n * (thick * 0.5f + 0.25f + 0.3f * hh) + d * (hh - 0.5f);
                Zone23Dressing.Plant(p, root.transform, weeds[w], new Vector3(wp.x, y0, wp.z), 360f * hh, wsize[w], 0.05f, seatOnTerrain: false, shadows: false);
                if (hh > 0.55f)
                    Zone23Dressing.Plant(p, root.transform, weeds[(w + 1) % weeds.Length], new Vector3(wp.x, y0, wp.z) + d * 0.6f, 200f * hh, wsize[(w + 1) % weeds.Length] * 0.8f, 0.05f, seatOnTerrain: false, shadows: false);
            }
            // outside face (what the dawn crane shot sees of the east and south walls): rain streaks under the coping,
            // rust runs, a moss band at the foot and vines hanging over the top
            float wyaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            for (float s = 2.2f; s < len - 1f; s += 3.4f)
            {
                if (InGap(s, 1.2f)) continue;
                float hh = Mathf.Repeat(Mathf.Sin(s * 5.17f + seed * 3.1f) * 2731.9f, 1f);
                Vector3 basePt = a + d * s;
                // (fix round: the 133 GrimeOut decals drawn to 90 m added no measurable detail and were removed; the
                //  per-bay tones and the pilasters carry the outside face now)
                if (hh > 0.45f && (id == "East" || id == "South"))
                {
                    float vh = 2.2f + 1.6f * hh;
                    Vector3 vp = basePt - n * (thick * 0.5f + 0.12f);
                    vp.y = y0 + h + 0.12f - vh;
                    Zone23Dressing.Plant(p, root.transform, "hanging_vines", vp, wyaw + 180f, vh, 0.03f, seatOnTerrain: false, shadows: false).name = "VinesOut";
                }
            }

            // the island takes the outside back: understory against the wall, trees 3-7 m out
            string[] under = { "banana_plant", "tree_fern", "monstera_clump", "fern_02", "banana_plant", "tree_fern" };
            float[] usz = { 2.8f, 3.6f, 1.5f, 1.0f, 2.4f, 3.0f };
            for (float s = 2f; s < len - 1f; s += 6.0f)
            {
                float hh = Mathf.Repeat(Mathf.Sin(s * 2.71f + seed * 5.3f) * 1733.1f, 1f);
                int u0 = (int)(hh * under.Length) % under.Length;
                Vector3 op = a + d * s - n * (thick * 0.5f + usz[u0] * 0.5f + 0.2f + 0.6f * hh);
                if (id == "West" && Mathf.Abs(op.z + 22.8f) < 6f) continue;          // keep the squeeze-gap approach open
                if (id == "East" && op.z > -54f && op.z < -38f) continue;            // coast road
                int u = (int)(hh * under.Length) % under.Length;
                Zone23Dressing.Plant(p, root.transform, under[u], op, 360f * hh, usz[u], 0.03f, shadows: false);
                if (Mathf.Repeat(s, 12f) < 6f)
                {
                    string stem = hh < 0.4f ? "palm_tree" : (hh < 0.7f ? "jungle_tree" : "palm_tree_b");
                    Vector3 tp = a + d * s - n * (thick * 0.5f + (stem == "jungle_tree" ? 7.5f : 3.2f) + 3f * hh);
                    Zone23Dressing.Plant(p, root.transform, stem, tp, 360f * hh, stem == "jungle_tree" ? 14f + 3f * hh : 10f + 2f * hh, 0.01f);
                }
            }
            Zone23Dressing.MergeStatic(root.transform);
        }

        private static void BuildWarehouseB(Transform parent, WorldMaterialPalette p)
        {
            var wh = new GameObject("WarehouseB");
            wh.transform.SetParent(parent, false);

            WorldArchitectureKit.BuildBoxRoomShell(wh.transform, "WarehouseB_Floor", new Vector3(435f, 0f, 20f), YardY, 42f, 32f, 8.0f, p.ConcreteFloor, p.CorrugatedMetal, SurfaceType.Concrete, p, buildCeiling: true, addRainBlocker: true);
            WorldArchitectureKit.BuildWallWithOpenings(wh.transform, new Vector3(414f, 0f, 4f), new Vector3(414f, 0f, 36f), YardY, 8.0f, 0.30f, p.CorrugatedMetal, null, palette: p, wallName: "WH_West");
            WorldArchitectureKit.BuildWallWithOpenings(wh.transform, new Vector3(414f, 0f, 36f), new Vector3(456f, 0f, 36f), YardY, 8.0f, 0.30f, p.CorrugatedMetal, null, palette: p, wallName: "WH_North");
            // South doorway X = 429..436 (CenterAlong = 18.5 m, W = 7 m)
            WorldArchitectureKit.BuildWallWithOpenings(wh.transform, new Vector3(414f, 0f, 4f), new Vector3(456f, 0f, 4f), YardY, 8.0f, 0.30f, p.CorrugatedMetal,
                new[] { OpeningSpec.Arch(18.5f, 7.0f, 4.4f) }, palette: p, wallName: "WH_South");
            // East doorway Z = +18..+24 (CenterAlong = 17.0 m, W = 6 m)
            WorldArchitectureKit.BuildWallWithOpenings(wh.transform, new Vector3(456f, 0f, 4f), new Vector3(456f, 0f, 36f), YardY, 8.0f, 0.30f, p.CorrugatedMetal,
                new[] { OpeningSpec.Arch(17.0f, 6.0f, 4.4f) }, palette: p, wallName: "WH_East");

            p.InstantiatePropOrFallback("utility_truck", wh.transform, new Vector3(426f, YardY, 22f), new Vector3(0f, 18f, 0f), new Vector3(2.4f, 2.4f, 5.6f));
            for (int i = 0; i < 4; i++)
            {
                p.InstantiatePropOrFallback("crate", wh.transform, new Vector3(440f + (i % 2) * 2.5f, YardY, 12f + i * 4.5f), new Vector3(0f, i * 15f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
                p.InstantiatePropOrFallback("oil_drum", wh.transform, new Vector3(418f, YardY, 10f + i * 3.5f), Vector3.zero, new Vector3(0.65f, 0.95f, 0.65f));
            }
            WorldArchitectureKit.BuildHdrpLight(wh.transform, "Light_WH_Aux", new Vector3(435f, YardY + 7.2f, 20f), Vector3.zero, LightType.Point, new Color(1.0f, 0.86f, 0.62f), 8500f, 32f, LightGroupId.AuxYard, volumetricDimmer: 1.5f);
            WorldArchitectureKit.BuildRoomReflectionProbe(wh.transform, "WarehouseB", new Vector3(435f, YardY + 3.8f, 20f), new Vector3(41.5f, 7.6f, 31.5f), "yard");
        }

        private static void BuildWorkshopAndOfficeBlock(Transform parent, WorldMaterialPalette p)
        {
            var ws = new GameObject("WorkshopAndOfficeBlock");
            ws.transform.SetParent(parent, false);
            const float wsH = 3.5f;

            // Main Workshop Floor/Ceiling Shell (X: 412..462, Z: -22..-10, 50 x 12 m)
            WorldArchitectureKit.BuildBoxRoomShell(ws.transform, "Workshop_Shell", new Vector3(437f, 0f, -16f), YardY, 50f, 12f, wsH, p.ConcreteFloor, p.CeilingTiles, SurfaceType.Concrete, p);

            // DOOR-6 FIX: North Exterior Wall (Z = -10, X = 412 -> 462, len = 50) with 6 REAL cut multi-pane glass windows!
            WorldArchitectureKit.BuildWallWithOpenings(
                ws.transform,
                new Vector3(412f, 0f, -10f),
                new Vector3(462f, 0f, -10f),
                YardY, wsH, 0.26f, p.BrickPainted,
                new[]
                {
                    OpeningSpec.Window(4.0f, 2.6f, 1.15f, 1.45f, mullions: 2, glassMat: p.GlassArchitectural),
                    OpeningSpec.Window(12.0f, 2.6f, 1.15f, 1.45f, mullions: 2, glassMat: p.GlassArchitectural),
                    OpeningSpec.Window(20.0f, 2.6f, 1.15f, 1.45f, mullions: 2, glassMat: p.GlassArchitectural),
                    OpeningSpec.Window(28.0f, 2.6f, 1.15f, 1.45f, mullions: 2, glassMat: p.GlassArchitectural),
                    OpeningSpec.Window(36.0f, 2.6f, 1.15f, 1.45f, mullions: 2, glassMat: p.GlassArchitectural),
                    OpeningSpec.Window(44.0f, 2.6f, 1.15f, 1.45f, mullions: 2, glassMat: p.GlassArchitectural)
                },
                addSkirting: true, palette: p, wallName: "Wall_Workshop_NorthWindows", includeInOcclusion: false);

            // South Exterior Wall (Z = -22, X = 412 -> 462)
            WorldArchitectureKit.BuildWallWithOpenings(ws.transform, new Vector3(412f, 0f, -22f), new Vector3(462f, 0f, -22f), YardY, wsH, 0.26f, p.BrickPainted, null, addSkirting: true, palette: p, wallName: "Wall_Workshop_South");

            // West Entry Wall (X = 412, Z = -22 -> -10, len = 12) — Open West Entry at Z = -16.0 (CenterAlong = 6.0 m, W = 1.6, H = 2.3)
            WorldArchitectureKit.BuildWallWithOpenings(ws.transform, new Vector3(412f, 0f, -22f), new Vector3(412f, 0f, -10f), YardY, wsH, 0.26f, p.BrickPainted,
                new[] { OpeningSpec.Arch(6.0f, 1.6f, 2.3f) }, addSkirting: true, palette: p, wallName: "Wall_Workshop_West");

            // DOOR-3 FIX: East Exit Wall (X = 462, Z = -22 -> -10, len = 12) — Exact doorway at Z = -16.0 (CenterAlong = 6.0 m, W = 1.6, H = 2.25)!
            WorldArchitectureKit.BuildWallWithOpenings(ws.transform, new Vector3(462f, 0f, -22f), new Vector3(462f, 0f, -10f), YardY, wsH, 0.26f, p.BrickPainted,
                new[] { OpeningSpec.Door(6.0f, 1.6f, 2.25f) }, addSkirting: true, palette: p, wallName: "Wall_Workshop_East");

            // Mount workshop_door_e cleanly INSIDE exact opening (462, YardY, -16.0) — NEVER buried in wall!
            WorldArchitectureKit.BuildFramedDoor(ws.transform, "workshop_door_e", new Vector3(462f, YardY, -16.0f), 90f, 1.6f, 2.25f, DoorMechanism.SingleHinged, p, soundPrefix: "door_old", overheadSignText: "EXIT COURT", leafMaterial: p.RustedIron);

            // Central Pipe Corridor Walls (Z = -14 & Z = -18, X = 412 -> 462, len = 50)
            WorldArchitectureKit.BuildWallWithOpenings(ws.transform, new Vector3(412f, 0f, -14f), new Vector3(462f, 0f, -14f), YardY, wsH, 0.20f, p.PlasterPeeling90s,
                new[] { OpeningSpec.Door(10.5f, 1.0f, 2.2f), OpeningSpec.Arch(30.5f, 1.2f, 2.25f) }, addSkirting: true, palette: p, wallName: "Wall_Workshop_CorrN");
            WorldArchitectureKit.BuildWallWithOpenings(ws.transform, new Vector3(412f, 0f, -18f), new Vector3(462f, 0f, -18f), YardY, wsH, 0.20f, p.PlasterPeeling90s,
                new[] { OpeningSpec.Door(20.5f, 1.0f, 2.2f), OpeningSpec.Arch(40.5f, 1.2f, 2.25f) }, addSkirting: true, palette: p, wallName: "Wall_Workshop_CorrS");

            // Partition North & South Wings into real distinct rooms (eliminates empty 50 m corridors!)
            WorldArchitectureKit.BuildWallWithOpenings(ws.transform, new Vector3(432f, 0f, -14f), new Vector3(432f, 0f, -10f), YardY, wsH, 0.20f, p.PlasterPeeling90s, null, addSkirting: true, palette: p, wallName: "Partition_Foreman_Parts");
            WorldArchitectureKit.BuildWallWithOpenings(ws.transform, new Vector3(438f, 0f, -22f), new Vector3(438f, 0f, -18f), YardY, wsH, 0.20f, p.PlasterPeeling90s, null, addSkirting: true, palette: p, wallName: "Partition_Elec_Tools");

            WorldArchitectureKit.BuildFramedDoor(ws.transform, "office_door", new Vector3(422.5f, YardY, -14f), 0f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, soundPrefix: "door_old", leafMaterial: p.WoodPlanks);
            WorldArchitectureKit.BuildFramedDoor(ws.transform, "tool_door", new Vector3(432.5f, YardY, -18f), 180f, 1.0f, 2.2f, DoorMechanism.SingleHinged, p, soundPrefix: "door_old", leafMaterial: p.WoodPlanks);

            // POS-5 FIX: Industrial Steam Pipe Manifold rooted cleanly into corridor walls + flush inline industrial_valve_wheels!
            WorldArchitectureKit.BuildConduitRun(ws.transform, "Workshop_OverheadSteamPipe", new[] { new Vector3(412.2f, YardY + 2.75f, -14.35f), new Vector3(461.8f, YardY + 2.75f, -14.35f) }, 0.09f, p.RustedIron);
            for (int v = 0; v < 5; v++)
            {
                float vx = 417f + v * 9.5f;
                // Vertical drop pipe connecting overhead steam pipe cleanly into wall & floor flange
                WorldArchitectureKit.BuildConduitRun(ws.transform, $"Workshop_VerticalPipe_{v}", new[] { new Vector3(vx, YardY, -14.35f), new Vector3(vx, YardY + 2.75f, -14.35f) }, 0.065f, p.RustedIron);
                // Flush pipe-mounted valve wheel directly on the vertical riser!
                p.InstantiatePropOrFallback("industrial_valve_wheel", ws.transform, new Vector3(vx, YardY + 1.45f, -14.35f), new Vector3(0f, 180f, 0f), new Vector3(0.42f, 0.42f, 0.34f), grounded: false, addCollider: false, fallbackMat: p.EmissiveRed, customName: $"WorkshopValve_{v}");
            }

            // POS-2 FIX: 5 Ceiling-Suspended Industrial Pendant Lights on steel hanger rods (ZERO sideways wall bulkheads on ceiling!)
            for (int l = 0; l < 5; l++)
            {
                float lx = 417f + l * 10.0f;
                WorldArchitectureKit.CreateBox(ws.transform, $"PendantRod_{l}", new Vector3(lx, YardY + wsH - 0.28f, -16f), new Vector3(0.03f, 0.56f, 0.03f), p.DarkPaintedMetal, addCollider: false);
                GameObject shade = WorldArchitectureKit.CreateBox(ws.transform, $"PendantShade_{l}", new Vector3(lx, YardY + wsH - 0.58f, -16f), new Vector3(0.45f, 0.14f, 0.45f), p.DarkPaintedMetal, addCollider: false);
                WorldArchitectureKit.BuildHdrpLight(ws.transform, $"Light_WorkshopPendant_{l}", new Vector3(lx, YardY + wsH - 0.70f, -16f), new Vector3(90f, 0f, 0f), LightType.Spot,
                    new Color(1.0f, 0.78f, 0.48f), 850f, 10f, l % 2 == 0 ? LightGroupId.YardBattery : LightGroupId.AuxYard, volumetricDimmer: 1.3f, castShadows: l == 2, spotOuterAngle: 88f);
            }

            // 1996 Site Plan Map on south corridor wall + Vance VO Trigger
            Material map1996Mat = p.CreateTextureQuadMaterial("MAT_MapPreserve1996", "map_preserve_1996", emissive: false, fallbackTint: new Color(0.75f, 0.70f, 0.58f));
            WorldArchitectureKit.CreateBox(ws.transform, "Map_Preserve1996", new Vector3(442f, YardY + 1.65f, -17.86f), new Vector3(2.2f, 1.45f, 0.04f), map1996Mat, addCollider: false);
            WorldArchitectureKit.BuildWorldAnchor(ws.transform, "site_map", new Vector3(442f, YardY + 1.65f, -17.85f));
            WorldArchitectureKit.BuildStoryTrigger(ws.transform, "yard_site_map", new Vector3(442f, YardY + 1.5f, -16.0f), new Vector3(4.5f, 3.0f, 3.6f), oneShot: true);

            // Foreman's Office Furnishings + Document 5/5 (Halvorsen's 1997 Maintenance Log!)
            BuildDesk(ws.transform, "ForemanDesk", new Vector3(420f, YardY, -11.4f), 1.8f, 0.95f, 0.80f, p);
            p.InstantiatePropOrFallback("office_chair", ws.transform, new Vector3(420.2f, YardY, -12.35f), new Vector3(0f, 165f, 0f), new Vector3(0.65f, 1.0f, 0.65f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("lockers", ws.transform, new Vector3(426.5f, YardY, -10.45f), new Vector3(0f, 180f, 0f), new Vector3(1.8f, 1.9f, 0.5f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("crt_monitor", ws.transform, new Vector3(419.5f, YardY + 0.80f, -11.2f), new Vector3(0f, 160f, 0f), new Vector3(0.45f, 0.44f, 0.44f));
            p.InstantiatePropOrFallback("filing_cabinet", ws.transform, new Vector3(413.2f, YardY, -11.2f), new Vector3(0f, 90f, 0f), new Vector3(0.55f, 1.38f, 0.65f));
            WorldArchitectureKit.BuildWorldAnchor(ws.transform, "halvorsen_log", new Vector3(420.4f, YardY + 0.83f, -11.4f));
            Zone1_VisitorCenterBuilder.BuildDocumentProp(
                ws.transform, new Vector3(420.4f, YardY + 0.83f, -11.4f), "log_halvorsen", "Halvorsen's 1997 Maintenance Log",
                "Read Halvorsen's maintenance log {interact}",
                new[] { "log_halvorsen" },
                new[] { "MAINTENANCE LOG (1997) — E-0 gate motor screams loud enough to draw predators, but the ratchet relay saves your open progress if you let go to hide." }, p);

            // Tool Workshop Furnishings (South-East room)
            BuildWorkbench(ws.transform, "ToolWorkbench", new Vector3(452f, YardY, -21.2f), 3.2f, 0.9f, 0.90f, p);
            p.InstantiatePropOrFallback("toolbox_case", ws.transform, new Vector3(451.5f, YardY + 0.90f, -21.2f), new Vector3(0f, 14f, 0f), new Vector3(0.58f, 0.32f, 0.36f));
            p.InstantiatePropOrFallback("archive_shelf", ws.transform, new Vector3(446f, YardY, -11.0f), Vector3.zero, new Vector3(2.2f, 2.2f, 0.55f));

            WorldArchitectureKit.BuildRoomReflectionProbe(ws.transform, "OldWorkshop", new Vector3(437f, YardY + 1.7f, -16f), new Vector3(49.5f, 3.4f, 11.6f), "yard");

            DressWorkshopExterior(ws.transform, p);
            DressElectricalCloset(ws.transform, p);
        }

        private static void BuildDesk(Transform parent, string name, Vector3 floorCentre, float w, float dpt, float ht, WorldMaterialPalette p)
        {
            var d = new GameObject(name);
            d.transform.SetParent(parent, false);
            d.transform.position = floorCentre;
            WorldArchitectureKit.CreateBox(d.transform, "Top", new Vector3(0f, ht - 0.02f, 0f), new Vector3(w, 0.04f, dpt), p.WoodPlanks, addCollider: true);
            WorldArchitectureKit.CreateBox(d.transform, "Pedestal", new Vector3(w * 0.5f - 0.24f, (ht - 0.04f) * 0.5f, 0f), new Vector3(0.44f, ht - 0.04f, dpt - 0.06f), p.DarkPaintedMetal, addCollider: true);
            for (int i = 0; i < 3; i++)
                WorldArchitectureKit.CreateBox(d.transform, "Drawer", new Vector3(w * 0.5f - 0.24f, 0.14f + i * 0.24f, -dpt * 0.5f + 0.02f), new Vector3(0.4f, 0.2f, 0.02f), Zone23Dressing.TransformerGrey(p), addCollider: false);
            WorldArchitectureKit.CreateBox(d.transform, "LegL1", new Vector3(-w * 0.5f + 0.05f, (ht - 0.04f) * 0.5f, -dpt * 0.5f + 0.05f), new Vector3(0.05f, ht - 0.04f, 0.05f), p.DarkPaintedMetal, addCollider: false);
            WorldArchitectureKit.CreateBox(d.transform, "LegL2", new Vector3(-w * 0.5f + 0.05f, (ht - 0.04f) * 0.5f, dpt * 0.5f - 0.05f), new Vector3(0.05f, ht - 0.04f, 0.05f), p.DarkPaintedMetal, addCollider: false);
            WorldArchitectureKit.CreateBox(d.transform, "ModestyPanel", new Vector3(-0.2f, ht * 0.55f, dpt * 0.5f - 0.03f), new Vector3(w - 0.6f, ht * 0.6f, 0.02f), p.WoodPlanks, addCollider: false);
        }

        private static void BuildWorkbench(Transform parent, string name, Vector3 floorCentre, float w, float dpt, float ht, WorldMaterialPalette p)
        {
            var b = new GameObject(name);
            b.transform.SetParent(parent, false);
            b.transform.position = floorCentre;
            WorldArchitectureKit.CreateBox(b.transform, "Top", new Vector3(0f, ht - 0.03f, 0f), new Vector3(w, 0.06f, dpt), p.WoodPlanks, addCollider: true);
            WorldArchitectureKit.CreateBox(b.transform, "Shelf", new Vector3(0f, 0.22f, 0f), new Vector3(w - 0.1f, 0.03f, dpt - 0.1f), p.WoodPlanks, addCollider: true);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    WorldArchitectureKit.CreateBox(b.transform, "Leg", new Vector3(i * (w * 0.5f - 0.06f), (ht - 0.06f) * 0.5f, j * (dpt * 0.5f - 0.06f)), new Vector3(0.07f, ht - 0.06f, 0.07f), p.DarkPaintedMetal, addCollider: false);
            WorldArchitectureKit.CreateBox(b.transform, "Pegboard", new Vector3(0f, ht + 0.6f, dpt * 0.5f - 0.02f), new Vector3(w - 0.2f, 0.9f, 0.02f), p.WoodPlanks, addCollider: false);
            WorldArchitectureKit.CreateBox(b.transform, "Vise", new Vector3(-w * 0.5f + 0.3f, ht + 0.08f, -dpt * 0.5f + 0.12f), new Vector3(0.18f, 0.14f, 0.22f), p.DarkPaintedMetal, addCollider: false);
            p.InstantiatePropOrFallback("oil_drum", b.transform, floorCentre + new Vector3(w * 0.5f + 0.55f, 0f, 0.05f), new Vector3(0f, 40f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: true);
        }

        /// <summary>The empty electrical closet becomes one: panelboards, a switch cabinet, cable trays.</summary>
        private static void DressElectricalCloset(Transform parent, WorldMaterialPalette p)
        {
            var c = new GameObject("ElectricalCloset_Dressing");
            c.transform.SetParent(parent, false);
            for (int i = 0; i < 4; i++)
                p.InstantiatePropOrFallback("breaker_box", c.transform, new Vector3(415f + i * 1.3f, YardY + 0.9f, -21.6f), Vector3.zero, new Vector3(0.7f, 1.05f, 0.4f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("switch_cabinet", c.transform, new Vector3(424.5f, YardY, -21.45f), Vector3.zero, new Vector3(1.4f, 2.2f, 0.8f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("server_rack", c.transform, new Vector3(427.4f, YardY, -21.4f), Vector3.zero, new Vector3(0.7f, 2.0f, 0.9f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("fire_extinguisher", c.transform, new Vector3(433.5f, YardY, -21.7f), Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("filing_cabinet", c.transform, new Vector3(436.8f, YardY, -21.5f), Vector3.zero, new Vector3(0.55f, 1.38f, 0.65f), grounded: true, addCollider: true);
            // cable tray along the south wall and drops into each panel
            WorldArchitectureKit.CreateBox(c.transform, "CableTray", new Vector3(425f, YardY + 2.95f, -21.65f), new Vector3(24f, 0.08f, 0.35f), Zone23Dressing.GalvSteel(p), addCollider: false);
            for (int i = 0; i < 4; i++)
                Zone23Dressing.Cyl(c.transform, "CableDrop", new Vector3(415f + i * 1.3f, YardY + 2.9f, -21.7f), new Vector3(415f + i * 1.3f, YardY + 1.95f, -21.7f), 0.03f, p.DarkPaintedMetal);
        }

        /// <summary>Workshop exterior: pitched corrugated roof with eaves, gables, frieze, gutters and downpipes; the
        /// canopy on posts over workshop_door_e; battery bulkheads at both entries; a plate over the west entry.</summary>
        private static void DressWorkshopExterior(Transform parent, WorldMaterialPalette p)
        {
            var r = new GameObject("Workshop_Exterior");
            r.transform.SetParent(parent, false);
            const float x0 = 412f, x1 = 462f, z0 = -22f, z1 = -10f, wallTop = YardY + 3.5f;
            const float eaveY = YardY + 4.15f, overhang = 0.6f, pitch = 14f;
            float half = (z1 - z0) * 0.5f + overhang, zc = (z0 + z1) * 0.5f;
            float rise = half * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float slope = half / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float lenX = (x1 - x0) + 2f * overhang;
            Zone23Dressing.Box(r.transform, "Roof_S", new Vector3((x0 + x1) * 0.5f, eaveY + rise * 0.5f, zc - half * 0.5f), new Vector3(lenX, 0.07f, slope), p.CorrugatedMetal, Quaternion.Euler(-pitch, 0f, 0f));
            Zone23Dressing.Box(r.transform, "Roof_N", new Vector3((x0 + x1) * 0.5f, eaveY + rise * 0.5f, zc + half * 0.5f), new Vector3(lenX, 0.07f, slope), p.CorrugatedMetal, Quaternion.Euler(pitch, 0f, 0f));
            Zone23Dressing.Box(r.transform, "RidgeCap", new Vector3((x0 + x1) * 0.5f, eaveY + rise + 0.05f, zc), new Vector3(lenX + 0.05f, 0.08f, 0.4f), p.DarkPaintedMetal, Quaternion.identity);
            // frieze closing the gap between the wall heads and the eaves (hides any roof cap on the shell)
            foreach (float z in new[] { z0, z1 })
            {
                float side = z < zc ? -1f : 1f;
                Zone23Dressing.Box(r.transform, "Frieze", new Vector3((x0 + x1) * 0.5f, (wallTop + eaveY + 0.12f) * 0.5f - 0.05f, z + side * 0.16f), new Vector3(x1 - x0 + 0.4f, eaveY + 0.12f - wallTop + 0.1f, 0.06f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Box(r.transform, "Fascia", new Vector3((x0 + x1) * 0.5f, eaveY - 0.08f, z + side * (overhang + 0.02f)), new Vector3(lenX, 0.22f, 0.04f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Cyl(r.transform, "Gutter", new Vector3(x0 - overhang, eaveY - 0.12f, z + side * (overhang + 0.1f)), new Vector3(x1 + overhang, eaveY - 0.12f, z + side * (overhang + 0.1f)), 0.07f, p.DarkPaintedMetal);
                foreach (float dx in new[] { x0 + 0.5f, (x0 + x1) * 0.5f, x1 - 0.5f })
                {
                    if (z > zc && dx > x1 - 1f) continue;
                    Zone23Dressing.Cyl(r.transform, "Downpipe", new Vector3(dx, YardY + 0.1f, z + side * 0.2f), new Vector3(dx, eaveY - 0.12f, z + side * 0.2f), 0.05f, p.DarkPaintedMetal);
                    Zone23Dressing.Cyl(r.transform, "DownpipeOffset", new Vector3(dx, eaveY - 0.12f, z + side * 0.2f), new Vector3(dx, eaveY - 0.12f, z + side * (overhang + 0.1f)), 0.05f, p.DarkPaintedMetal);
                }
            }
            // gables: a triangular brick prism standing on the wall head (base = wall top, apex under the ridge); it
            // has nothing below the wall top, so the west entry and workshop_door_e (both at z = -16) stay clear
            foreach (float x in new[] { x0, x1 })
            {
                float halfSpan = (z1 - z0) * 0.5f;
                float topY = eaveY + rise - overhang * Mathf.Tan(pitch * Mathf.Deg2Rad) - 0.04f;
                Zone23Dressing.Prism(r.transform, "Gable", new Vector3(x, wallTop, zc), 0.22f, -halfSpan, halfSpan, 0f, topY - wallTop, p.BrickPainted);
                Zone23Dressing.Box(r.transform, "GableVent", new Vector3(x + (x < 437f ? -0.12f : 0.12f), wallTop + (topY - wallTop) * 0.45f, zc), new Vector3(0.04f, 0.45f, 0.7f), p.DarkPaintedMetal, Quaternion.identity);
            }

            // canopy on two posts over workshop_door_e (462, -16)
            const float cy = YardY + 3.0f;
            Zone23Dressing.Box(r.transform, "DoorCanopy", new Vector3(463.8f, cy, -16f), new Vector3(3.4f, 0.08f, 4.6f), p.CorrugatedMetal, Quaternion.Euler(0f, 0f, -5f));
            Zone23Dressing.Box(r.transform, "DoorCanopyFascia", new Vector3(465.5f, cy - 0.18f, -16f), new Vector3(0.05f, 0.24f, 4.7f), p.DarkPaintedMetal, Quaternion.identity);
            Zone23Dressing.Box(r.transform, "DoorCanopyBeam", new Vector3(465.35f, cy - 0.12f, -16f), new Vector3(0.12f, 0.12f, 4.6f), p.DarkPaintedMetal, Quaternion.identity);
            foreach (float z in new[] { -18.1f, -13.9f })
            {
                Zone23Dressing.Box(r.transform, "DoorCanopyPost", new Vector3(465.35f, (YardY + cy - 0.12f) * 0.5f, z), new Vector3(0.12f, cy - 0.12f - YardY, 0.12f), p.DarkPaintedMetal, Quaternion.identity, collider: true);
                Zone23Dressing.Box(r.transform, "DoorCanopyPostFoot", new Vector3(465.35f, YardY + 0.05f, z), new Vector3(0.28f, 0.1f, 0.28f), Zone23Dressing.Plinth(p), Quaternion.identity);
                Zone23Dressing.Decal(r.transform, "DoorCanopyPostHazard", new Vector3(465.35f, YardY + 0.6f, z), Vector3.right, new Vector3(0.3f, 1.0f, 0.4f), p.DecalCautionStripes, 0.9f);
            }
            WorldArchitectureKit.BuildRainBlockerSlab(r.transform, "RainBlock_DoorCanopy", new Vector3(463.8f, cy + 0.12f, -16f), new Vector3(3.5f, 0.1f, 4.7f));

            // battery bulkheads: over the squeeze gap (inside face), the workshop west entry and the east door
            WorldArchitectureKit.BuildHdrpLight(r.transform, "Light_YardGapBulkhead_Batt", new Vector3(401.225f + 0.25f, YardY + 3.55f, -21.6f), new Vector3(55f, 90f, 0f), LightType.Spot, new Color(1f, 0.72f, 0.42f), 420f, 13f, LightGroupId.YardBattery, volumetricDimmer: 0.9f, spotOuterAngle: 120f);
            WorldArchitectureKit.BuildHdrpLight(r.transform, "Light_WorkshopWestBulkhead_Batt", new Vector3(411.87f - 0.25f, YardY + 2.8f, -17.3f), new Vector3(55f, -90f, 0f), LightType.Spot, new Color(1f, 0.72f, 0.42f), 360f, 11f, LightGroupId.YardBattery, volumetricDimmer: 0.9f, spotOuterAngle: 120f);
            WorldArchitectureKit.BuildHdrpLight(r.transform, "Light_WorkshopDoorEBulkhead_Batt", new Vector3(462.13f + 0.25f, YardY + 2.75f, -17.25f), new Vector3(60f, 90f, 0f), LightType.Spot, new Color(1f, 0.72f, 0.42f), 300f, 10f, LightGroupId.YardBattery, volumetricDimmer: 0.9f, spotOuterAngle: 120f);
            // the long south face: high wired-glass windows (exterior skin only), a meter box, conduit and vents
            for (float x = x0 + 5f; x < x1 - 2f; x += 9f)
            {
                Vector3 wc = new Vector3(x, YardY + 2.55f, z0 - 0.135f);
                Zone23Dressing.Box(r.transform, "HighWindowFrame", wc, new Vector3(1.5f, 0.75f, 0.03f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Box(r.transform, "HighWindowGlass", wc + new Vector3(0f, 0f, -0.012f), new Vector3(1.36f, 0.61f, 0.012f), p.GlassArchitectural, Quaternion.identity);
                Zone23Dressing.Box(r.transform, "HighWindowMullion", wc + new Vector3(0f, 0f, -0.02f), new Vector3(0.04f, 0.61f, 0.02f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Box(r.transform, "HighWindowSill", wc + new Vector3(0f, -0.42f, -0.05f), new Vector3(1.6f, 0.07f, 0.1f), Zone23Dressing.Coping(p), Quaternion.identity);
            }
            Zone23Dressing.Box(r.transform, "MeterBox", new Vector3(x0 + 9.5f, YardY + 1.45f, z0 - 0.25f), new Vector3(0.6f, 0.8f, 0.22f), Zone23Dressing.TransformerGrey(p), Quaternion.identity);
            Zone23Dressing.Cyl(r.transform, "MeterConduit", new Vector3(x0 + 9.5f, YardY + 1.85f, z0 - 0.2f), new Vector3(x0 + 9.5f, wallTop - 0.1f, z0 - 0.2f), 0.03f, p.DarkPaintedMetal);
            Zone23Dressing.Cyl(r.transform, "WallConduit", new Vector3(x0 + 9.5f, wallTop - 0.1f, z0 - 0.2f), new Vector3(x1 - 6f, wallTop - 0.1f, z0 - 0.2f), 0.03f, p.DarkPaintedMetal);
            foreach (float x in new[] { x0 + 18f, x0 + 33f })
            {
                Zone23Dressing.Box(r.transform, "WallVent", new Vector3(x, YardY + 3.0f, z0 - 0.15f), new Vector3(0.6f, 0.4f, 0.05f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Box(r.transform, "ACUnit", new Vector3(x + 1.6f, YardY + 0.45f, z0 - 0.55f), new Vector3(1.0f, 0.9f, 0.7f), Zone23Dressing.TransformerGrey(p), Quaternion.identity, collider: true);
                Zone23Dressing.Box(r.transform, "ACUnitPad", new Vector3(x + 1.6f, YardY + 0.04f, z0 - 0.55f), new Vector3(1.2f, 0.08f, 0.9f), Zone23Dressing.Plinth(p), Quaternion.identity);
                Zone23Dressing.Cyl(r.transform, "ACLine", new Vector3(x + 1.2f, YardY + 0.7f, z0 - 0.2f), new Vector3(x + 1.2f, YardY + 2.6f, z0 - 0.2f), 0.025f, p.BrushedSteel);
            }

            // grime under the eaves + weathering on the brick
            for (float x = x0 + 3f; x < x1; x += 7.5f)
            {
                Zone23Dressing.Decal(r.transform, "WsGrimeS", new Vector3(x, YardY + 2.6f, z0 - 0.3f), Vector3.back, new Vector3(2.0f, 2.0f, 0.5f), Zone23Dressing.Grime(p, (int)x), 0.65f);
                Zone23Dressing.Decal(r.transform, "WsMossS", new Vector3(x + 2f, YardY + 0.4f, z0 - 0.25f), Vector3.back, new Vector3(2.6f, 0.9f, 0.6f), Zone23Dressing.Moss(p), 0.8f);
            }
        }

        private static void BuildExitCourtAndE0Gate(Transform parent, WorldMaterialPalette p)
        {
            var court = new GameObject("ExitCourt_And_E0Gate");
            court.transform.SetParent(parent, false);

            // Covered Porch along Workshop East & Loading Dock on South
            // (the old free-floating Porch_Canopy is now the canopy on posts over workshop_door_e, see DressWorkshopExterior)
            BuildLoadingDock(court.transform, p);

            // POS-16 FIX: Service Exit E-0 Double Gate swinging OUTWARD EAST (+X) clear of the gate_control_box sprint line!
            WorldArchitectureKit.BuildFramedDoor(
                court.transform, "e0_gate_leaves", new Vector3(505.0f, YardY, -46.0f), 90f, 8.0f, 4.0f,
                DoorMechanism.DoubleHingedOutward, p, startsOpen: false, startsLocked: true,
                lockedHint: "Operate Service Exit E-0 from the control box beside the gate.",
                soundPrefix: "gate_motor", primaryOpenAngleDeg: -95f, secondaryOpenAngleDeg: 95f, bashableByRaptor: false);

            // Review 3: with see-through mesh alone the closed gate read as an open portal; round 4: the lead's 3 cm bars
            // vanished from the yard at 30-60 m. Each leaf is now a welded palisade gate: no chain-link (the wire leaf keeps
            // its collider but draws nothing), 5 cm bars every 21.5 cm, 10 cm stiles, a rail over a solid striped kick plate,
            // a brace, and a signal head on the meeting stile whose red lens faces the yard while closed; the green lens
            // faces the leaf's own hinge, which is the yard side once the leaf has swung out 95 degrees (no script needed).
            Transform e0 = court.transform.Find("Door_e0_gate_leaves");
            if (e0 != null)
            {
                Material bar = p.DarkPaintedMetal, galv = Zone23Dressing.GalvSteel(p);
                Material red = p.GetOrBuildEmissive("MAT_Z23_GateSignalRed", new Color(1.0f, 0.12f, 0.06f), 18f);
                Material green = p.GetOrBuildEmissive("MAT_Z23_GateSignalGreen", new Color(0.25f, 1.0f, 0.45f), 14f);
                foreach (string side in new[] { "A", "B" })
                {
                    Transform hinge = e0.Find($"Leaf{side}_Hinge");
                    if (hinge == null) continue;
                    Transform wire = hinge.Find($"Leaf{side}");
                    if (wire != null && wire.TryGetComponent(out MeshRenderer wr)) { wr.enabled = false; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                    float dir = side == "A" ? 1f : -1f;          // leaf A runs local +x from its hinge, leaf B -x; local -z = the yard
                    for (int i = 0; i < 18; i++)
                        WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_Bar_{i:00}", new Vector3(dir * (0.16f + i * 0.215f), 2.5f, 0f), new Vector3(0.05f, 2.76f, 0.05f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_StileHinge", new Vector3(dir * 0.05f, 2.0f, 0f), new Vector3(0.10f, 3.96f, 0.12f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_StileMeet", new Vector3(dir * 3.92f, 2.0f, 0f), new Vector3(0.10f, 3.96f, 0.12f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_KickRail", new Vector3(dir * 1.985f, 1.16f, 0f), new Vector3(3.84f, 0.08f, 0.09f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_KickPlate", new Vector3(dir * 1.985f, 0.64f, 0f), new Vector3(3.80f, 0.98f, 0.03f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_Brace", new Vector3(dir * 1.985f, 2.55f, 0.05f), new Vector3(4.28f, 0.07f, 0.05f), bar, false, new Vector3(0f, 0f, dir * 35.5f));
                    // hazard stripes on both faces of the kick plate (children of the hinge: they swing with the leaf)
                    foreach (float fz in new[] { -1f, 1f })
                    {
                        Vector3 wpos = hinge.TransformPoint(new Vector3(dir * 1.985f, 0.64f, fz * 0.03f));
                        Vector3 wn = hinge.TransformDirection(new Vector3(0f, 0f, fz));
                        DecalProjector kd = Zone23Dressing.Decal(hinge, $"Leaf{side}_KickStripes", wpos, wn, new Vector3(3.7f, 0.86f, 0.08f), p.DecalCautionStripes, 0.95f, drawDistance: 90f);
                        kd.transform.SetParent(hinge, true);
                    }
                    // signal head on the yard face of the leaf, outboard of the E-0 lightbox (which hides the meeting stiles)
                    float hx = dir * 1.2f;
                    const float hy = 3.62f, hz = -0.17f;
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalHead", new Vector3(hx, hy, hz), new Vector3(0.22f, 0.26f, 0.2f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalBracket", new Vector3(hx, hy, -0.05f), new Vector3(0.08f, 0.08f, 0.06f), galv, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalRed", new Vector3(hx, hy, hz - 0.104f), new Vector3(0.15f, 0.15f, 0.01f), red, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalHoodRed", new Vector3(hx, hy + 0.1f, hz - 0.15f), new Vector3(0.21f, 0.02f, 0.1f), bar, false, null);
                    // a deep cheek on the red lens's hinge side: once the leaf has swung out, the red lens faces along the
                    // wall line and the yard sees it only from that side, so the cheek hides it (closed, it faces the yard)
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalCheekRed", new Vector3(hx - dir * 0.086f, hy, hz - 0.21f), new Vector3(0.02f, 0.2f, 0.22f), bar, false, null);
                    // green lens recessed in a deep visor that faces this leaf's hinge (seen edge-on from the yard while closed)
                    float gx = hx - dir * 0.114f;
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalGreen", new Vector3(gx, hy, hz), new Vector3(0.01f, 0.15f, 0.15f), green, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalVisorTop", new Vector3(gx - dir * 0.08f, hy + 0.1f, hz), new Vector3(0.16f, 0.02f, 0.2f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalVisorL", new Vector3(gx - dir * 0.08f, hy, hz - 0.1f), new Vector3(0.16f, 0.2f, 0.02f), bar, false, null);
                    WorldArchitectureKit.CreateBox(hinge, $"Leaf{side}_SignalVisorR", new Vector3(gx - dir * 0.08f, hy, hz + 0.1f), new Vector3(0.16f, 0.2f, 0.02f), bar, false, null);
                }
                // swing-gate operators: a motor housing on the yard face of the wall beside each hinge post, a round hinge
                // post in the jamb, a cable conduit down into the slab
                Material housing = Zone23Dressing.TransformerGrey(p), voidM = Zone23Dressing.DrainVoid(p);
                foreach (float hz in new[] { -50f, -42f })
                {
                    float outZ = hz < -46f ? -1f : 1f;           // away from the opening
                    Vector3 post = new Vector3(504.9f, YardY, hz);
                    Zone23Dressing.Cyl(court.transform, "E0_HingePost", post, post + Vector3.up * 4.25f, 0.085f, galv);
                    Zone23Dressing.Cyl(court.transform, "E0_HingePostCap", post + Vector3.up * 4.25f, post + Vector3.up * 4.3f, 0.11f, bar);
                    Vector3 hc = new Vector3(504.775f - 0.17f, YardY + 1.25f, hz + outZ * 0.42f);
                    Zone23Dressing.Box(court.transform, "E0_MotorHousing", hc, new Vector3(0.34f, 0.62f, 0.5f), housing, Quaternion.identity);
                    Zone23Dressing.Box(court.transform, "E0_MotorHood", hc + new Vector3(-0.02f, 0.33f, 0f), new Vector3(0.4f, 0.04f, 0.56f), bar, Quaternion.identity);
                    Zone23Dressing.Box(court.transform, "E0_MotorVent", hc + new Vector3(-0.172f, 0.08f, 0f), new Vector3(0.006f, 0.22f, 0.32f), voidM, Quaternion.identity);
                    Zone23Dressing.Box(court.transform, "E0_MotorLabel", hc + new Vector3(-0.172f, -0.17f, 0f), new Vector3(0.006f, 0.1f, 0.22f), Zone23Dressing.PaintYellow(p), Quaternion.identity);
                    Zone23Dressing.Box(court.transform, "E0_MotorArmBracket", hc + new Vector3(0.0f, 0.12f, -outZ * 0.3f), new Vector3(0.12f, 0.1f, 0.12f), galv, Quaternion.identity);
                    Zone23Dressing.Cyl(court.transform, "E0_MotorConduit", hc + new Vector3(0.05f, -0.31f, 0f), new Vector3(hc.x + 0.05f, YardY + 0.02f, hc.z), 0.025f, galv);
                    Zone23Dressing.Decal(court.transform, "E0_MotorStain", new Vector3(504.775f - 0.05f, YardY + 0.6f, hc.z), Vector3.left, new Vector3(0.6f, 1.2f, 0.2f), Zone23Dressing.RunoffStrong(p), 0.7f);
                }
            }

            WorldArchitectureKit.BuildWorldAnchor(court.transform, "e0_gate", new Vector3(505.0f, YardY, -46.0f));
            WorldArchitectureKit.BuildStoryTrigger(court.transform, "through_e0_gate", new Vector3(515.0f, YardY + 2.0f, -46.0f), new Vector3(3.2f, 4.5f, 9.2f), oneShot: true);

            // Bollard-Mounted Illuminated E-0 Gate Control Box (9.0 s saved-progress hold!)
            p.InstantiatePropOrFallback("gate_control_box", court.transform, new Vector3(503.8f, YardY, -52.6f), new Vector3(0f, -90f, 0f), new Vector3(0.55f, 1.55f, 0.45f), grounded: true, addCollider: true, customName: "E0_GateControlBox");
            WorldArchitectureKit.BuildWorldAnchor(court.transform, "gate_box", new Vector3(503.8f, YardY + 1.15f, -52.6f));
            Zone1_VisitorCenterBuilder.BuildSimpleInteractableBox(
                court.transform, "Interact_E0GateBox", new Vector3(503.8f, YardY + 1.15f, -52.6f), new Vector3(1.1f, 1.5f, 1.1f),
                "gate_box", "Hold {interact} OPEN Service Exit E-0", holdSec: 9.0f, preserveProgress: true, singleUse: true,
                startsEnabled: true, startsLocked: true, lockedHint: "E-0 gate control is dead — restore Auxiliary Circuit B at Substation 3 first.");

            // E-0 Overhead Illuminated Signage, Green Gate Beacon, Asphalt Stencil & Floodlights (P1-11 & P2-4 calibrated!)
            Material e0SignMat = p.CreateTextureQuadMaterial("MAT_SignE0", Look.LookSigns.E0Lightbox, emissive: true, emissiveIntensity: 1.5f, fallbackTint: new Color(0.22f, 0.95f, 0.36f));
            WorldArchitectureKit.BuildIlluminatedSign(court.transform, "Sign_ServiceExitE0", new Vector3(504.4f, YardY + 4.35f, -46.0f), new Vector3(0f, -90f, 0f), new Vector2(4.2f, 0.72f), e0SignMat, LightGroupId.AuxYard);
            WorldArchitectureKit.BuildHdrpLight(court.transform, "Light_E0ControlBox_Beacon", new Vector3(503.6f, YardY + 2.35f, -52.6f), Vector3.zero, LightType.Point, new Color(1.0f, 0.62f, 0.16f), 320f, 9f, LightGroupId.YardBattery, volumetricDimmer: 0.9f, pulsingEmergency: true);
            // P2-4 FIX: Offset green overhead spot 1.2 m West & aim downward/east at 1400 lm so SERVICE EXIT E-0 text stays sharp and glare-free!
            WorldArchitectureKit.BuildHdrpLight(court.transform, "Light_E0Gate_GreenOverhead", new Vector3(502.6f, YardY + 5.2f, -46.0f), new Vector3(76f, 90f, 0f), LightType.Spot, new Color(0.20f, 1.0f, 0.38f), 1400f, 18f, LightGroupId.AuxYard, volumetricDimmer: 0.95f, castShadows: true, spotOuterAngle: 78f);

            // 3 Exit Court Auxiliary Floodlight Poles (P1-11 FIX: 9500 lm prevents whiteout during storm lightning flashes!)
            Vector3[] courtFloods = { new(474f, YardY, -28f), new(492f, YardY, -34f), new(482f, YardY, -54f) };
            for (int f = 0; f < courtFloods.Length; f++)
            {
                WorldArchitectureKit.CreateBox(court.transform, $"CourtFloodPole_{f}", courtFloods[f] + Vector3.up * 3.8f, new Vector3(0.22f, 7.6f, 0.22f), p.DarkPaintedMetal, addCollider: true);
                WorldArchitectureKit.BuildHdrpLight(court.transform, $"Light_CourtAuxFlood_{f}", courtFloods[f] + Vector3.up * 7.5f, new Vector3(68f, 45f, 0f), LightType.Spot, new Color(1.0f, 0.88f, 0.68f), 9500f, 34f, LightGroupId.AuxYard, volumetricDimmer: 0.85f, spotOuterAngle: 82f);
            }
        }

        /// <summary>The south dock: a corrugated shed (same footprint and collider as the old box) with three roller doors,
        /// a raised concrete dock with rubber bumpers, steps, a ramp and a cantilevered canopy.</summary>
        private static void BuildLoadingDock(Transform parent, WorldMaterialPalette p)
        {
            var d = new GameObject("SouthDockBuilding");
            d.transform.SetParent(parent, false);
            const float y = YardY, x0 = 467f, x1 = 501f, zBack = -67f, zFront = -60.2f, h = 5.4f;
            float xc = (x0 + x1) * 0.5f;
            GameObject body = Zone23Dressing.Box(d.transform, "ShedBody", new Vector3(xc, y + h * 0.5f, (zBack + zFront) * 0.5f), new Vector3(x1 - x0, h, zFront - zBack), p.CorrugatedMetal, Quaternion.identity, collider: true);
            Zone23Dressing.Box(d.transform, "ShedPlinth", new Vector3(xc, y + 0.3f, (zBack + zFront) * 0.5f), new Vector3(x1 - x0 + 0.08f, 0.6f, zFront - zBack + 0.08f), Zone23Dressing.Plinth(p), Quaternion.identity);
            Zone23Dressing.Box(d.transform, "ShedRoof", new Vector3(xc, y + h + 0.06f, (zBack + zFront) * 0.5f), new Vector3(x1 - x0 + 0.5f, 0.12f, zFront - zBack + 0.5f), p.RoofBitumen, Quaternion.identity);
            Zone23Dressing.Box(d.transform, "ShedFlashing", new Vector3(xc, y + h + 0.15f, zFront + 0.26f), new Vector3(x1 - x0 + 0.5f, 0.18f, 0.04f), p.DarkPaintedMetal, Quaternion.identity);
            for (float x = x0 + 0.05f; x <= x1; x += (x1 - x0 - 0.1f) / 4f)
                Zone23Dressing.Box(d.transform, "ShedColumn", new Vector3(x, y + h * 0.5f, zFront - 0.04f), new Vector3(0.22f, h, 0.14f), p.DarkPaintedMetal, Quaternion.identity);

            // raised dock (1.15 m) along the front
            const float dockTop = 1.15f, dockDepth = 2.6f;
            float dz = zFront + dockDepth * 0.5f;
            GameObject dock = Zone23Dressing.Box(d.transform, "DockPlatform", new Vector3(xc, y + dockTop * 0.5f, dz), new Vector3(28f, dockTop, dockDepth), Zone23Dressing.SlabConcreteDark(p), Quaternion.identity, collider: true);   // round 3: a floor slab, not the board-form wall material
            WorldArchitectureKit.ApplySurface(dock, SurfaceType.Concrete, p);
            Zone23Dressing.Box(d.transform, "DockEdgeAngle", new Vector3(xc, y + dockTop - 0.04f, zFront + dockDepth + 0.01f), new Vector3(28f, 0.08f, 0.04f), p.BrushedSteel, Quaternion.identity);
            Zone23Dressing.Decal(d.transform, "DockEdgeHazard", new Vector3(xc, y + dockTop - 0.12f, zFront + dockDepth + 0.15f), Vector3.forward, new Vector3(28f, 0.22f, 0.4f), p.DecalCautionStripes, 0.9f);
            // the dock top's front edge strip too
            Zone23Dressing.Decal(d.transform, "DockTopHazard", new Vector3(xc, y + dockTop + 0.1f, zFront + dockDepth - 0.15f), Vector3.up, new Vector3(28f, 0.25f, 0.4f), p.DecalCautionStripes, 0.85f);
            for (int i = 0; i < 3; i++)
            {
                float x = xc - 10f + i * 10f;
                // roller door: frame, slatted curtain (door 1 jammed half open), bumpers below
                Zone23Dressing.Box(d.transform, "DoorFrame", new Vector3(x, y + dockTop + 1.85f, zFront + 0.03f), new Vector3(3.9f, 3.7f, 0.06f), p.DarkPaintedMetal, Quaternion.identity);
                float open = i == 1 ? 1.4f : 0f;
                float curtainH = 3.5f - open;
                Zone23Dressing.Box(d.transform, "DoorCurtain", new Vector3(x, y + dockTop + open + curtainH * 0.5f, zFront + 0.07f), new Vector3(3.6f, curtainH, 0.04f), Zone23Dressing.TransformerGrey(p), Quaternion.identity);
                for (float sy = 0.12f; sy < curtainH; sy += 0.22f)
                    Zone23Dressing.Box(d.transform, "DoorSlat", new Vector3(x, y + dockTop + open + sy, zFront + 0.095f), new Vector3(3.6f, 0.025f, 0.012f), p.DarkPaintedMetal, Quaternion.identity);
                Zone23Dressing.Box(d.transform, "DoorHood", new Vector3(x, y + dockTop + 3.75f, zFront + 0.2f), new Vector3(4.0f, 0.42f, 0.4f), p.DarkPaintedMetal, Quaternion.identity);
                if (open > 0f)
                    Zone23Dressing.Box(d.transform, "DoorDarkInterior", new Vector3(x, y + dockTop + open * 0.5f, zFront + 0.02f), new Vector3(3.6f, open, 0.02f), Zone23Dressing.DrainVoid(p), Quaternion.identity);
                foreach (float bx in new[] { x - 1.6f, x + 1.6f })
                    Zone23Dressing.Box(d.transform, "DockBumper", new Vector3(bx, y + dockTop - 0.35f, zFront + dockDepth + 0.08f), new Vector3(0.28f, 0.5f, 0.14f), Zone23Dressing.DrainVoid(p), Quaternion.identity);
                Zone23Dressing.Decal(d.transform, "DockDoorGrime", new Vector3(x, y + dockTop + 0.5f, zFront + 0.3f), Vector3.forward, new Vector3(3.6f, 1.2f, 0.5f), Zone23Dressing.Grime(p, i), 0.6f);
            }
            // steps at the west end, a ramp at the east end (both with colliders: they are walkable)
            for (int st = 0; st < 5; st++)
            {
                float sh = dockTop * (st + 1) / 5f;
                Zone23Dressing.Box(d.transform, $"DockStep_{st}", new Vector3(xc - 14.15f - (4 - st) * 0.3f, y + sh * 0.5f, dz + 0.3f), new Vector3(0.3f, sh, 1.4f), p.ConcreteBoardform, Quaternion.identity, collider: true);
            }
            Zone23Dressing.Box(d.transform, "DockStepRail", new Vector3(xc - 14.9f, y + dockTop * 0.6f + 0.9f, dz - 0.38f), new Vector3(1.9f, 0.05f, 0.05f), p.PaintedMetalYellow, Quaternion.Euler(0f, 0f, 35f));
            float rampLen = 6.5f, ang = Mathf.Asin(dockTop / rampLen) * Mathf.Rad2Deg;
            float rampRun = Mathf.Sqrt(rampLen * rampLen - dockTop * dockTop);
            GameObject ramp = Zone23Dressing.Box(d.transform, "DockRamp", new Vector3(xc + 14f + rampRun * 0.5f, y + dockTop * 0.5f - 0.1f, dz), new Vector3(rampLen, 0.25f, dockDepth), p.ConcreteBoardform, Quaternion.Euler(0f, 0f, -ang), collider: true);
            WorldArchitectureKit.ApplySurface(ramp, SurfaceType.Concrete, p);
            // cantilevered canopy over the dock
            Zone23Dressing.Box(d.transform, "DockCanopy", new Vector3(xc, y + h - 0.35f, zFront + 1.7f), new Vector3(x1 - x0, 0.08f, 3.4f), p.CorrugatedMetal, Quaternion.Euler(-6f, 0f, 0f));
            for (float x = x0 + 1f; x < x1; x += 8f)
                Zone23Dressing.Cyl(d.transform, "DockCanopyTie", new Vector3(x, y + h + 0.1f, zFront + 0.05f), new Vector3(x, y + h - 0.2f, zFront + 3.2f), 0.025f, p.DarkPaintedMetal);
            WorldArchitectureKit.BuildRainBlockerSlab(d.transform, "RainBlock_DockCanopy", new Vector3(xc, y + h - 0.2f, zFront + 1.7f), new Vector3(x1 - x0, 0.1f, 3.4f));
            p.InstantiatePropOrFallback("pallet", d.transform, new Vector3(xc - 6f, y + dockTop, dz - 0.3f), new Vector3(0f, 4f, 0f), new Vector3(1.2f, 1.2f, 1.2f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("crate", d.transform, new Vector3(xc - 6f, y + dockTop + 0.14f, dz - 0.3f), new Vector3(0f, 12f, 0f), new Vector3(0.8f, 0.8f, 0.8f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("utility_cart", d.transform, new Vector3(xc + 7.5f, y + dockTop, dz - 0.4f), new Vector3(0f, 80f, 0f), new Vector3(1.0f, 1.0f, 1.0f), grounded: true, addCollider: false);
        }

        /// <summary>A kit-built 20 ft container: corrugated body, corner posts and castings, door end with locking bars.</summary>
        private static void BuildContainer(Transform parent, string name, Vector3 floorCentre, float yaw, Material body, WorldMaterialPalette p)
        {
            var c = new GameObject(name);
            c.transform.SetParent(parent, false);
            c.transform.SetPositionAndRotation(floorCentre, Quaternion.Euler(0f, yaw, 0f));
            const float L = 6.06f, W = 2.44f, H = 2.59f;
            // round 3: the gameplay box keeps its collider but is not drawn; a slimmer core carries the roof and the doors,
            // and trapezoid-corrugated sheets (not box ribs: review 2 read those as planks) make the walls
            GameObject bodyBox = WorldArchitectureKit.CreateBox(c.transform, "Body", new Vector3(0f, H * 0.5f, 0f), new Vector3(W - 0.06f, H - 0.12f, L - 0.1f), body, addCollider: true);
            bodyBox.GetComponent<MeshRenderer>().enabled = false;
            WorldArchitectureKit.CreateBox(c.transform, "BodyCore", new Vector3(0f, (0.06f + H - 0.04f) * 0.5f, 0f), new Vector3(W - 0.14f, H - 0.10f, L - 0.1f), body, addCollider: false);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                {
                    WorldArchitectureKit.CreateBox(c.transform, "CornerPost", new Vector3(i * (W * 0.5f - 0.06f), H * 0.5f, j * (L * 0.5f - 0.06f)), new Vector3(0.12f, H, 0.12f), body, addCollider: false);
                    WorldArchitectureKit.CreateBox(c.transform, "CastingTop", new Vector3(i * (W * 0.5f - 0.08f), H - 0.06f, j * (L * 0.5f - 0.09f)), new Vector3(0.17f, 0.12f, 0.18f), p.DarkPaintedMetal, addCollider: false);
                    WorldArchitectureKit.CreateBox(c.transform, "CastingBot", new Vector3(i * (W * 0.5f - 0.08f), 0.06f, j * (L * 0.5f - 0.09f)), new Vector3(0.17f, 0.12f, 0.18f), p.DarkPaintedMetal, addCollider: false);
                }
            WorldArchitectureKit.CreateBox(c.transform, "TopRailL", new Vector3(-W * 0.5f + 0.05f, H - 0.05f, 0f), new Vector3(0.1f, 0.1f, L - 0.2f), body, addCollider: false);
            WorldArchitectureKit.CreateBox(c.transform, "TopRailR", new Vector3(W * 0.5f - 0.05f, H - 0.05f, 0f), new Vector3(0.1f, 0.1f, L - 0.2f), body, addCollider: false);
            WorldArchitectureKit.CreateBox(c.transform, "BottomRailL", new Vector3(-W * 0.5f + 0.05f, 0.08f, 0f), new Vector3(0.1f, 0.16f, L - 0.2f), p.DarkPaintedMetal, addCollider: false);
            WorldArchitectureKit.CreateBox(c.transform, "BottomRailR", new Vector3(W * 0.5f - 0.05f, 0.08f, 0f), new Vector3(0.1f, 0.16f, L - 0.2f), p.DarkPaintedMetal, addCollider: false);
            for (int k = 0; k < 4; k++)
            {
                float x = -0.85f + k * 0.567f;
                Zone23Dressing.Cyl(c.transform, "LockBar", c.transform.TransformPoint(new Vector3(x, 0.15f, L * 0.5f + 0.02f)), c.transform.TransformPoint(new Vector3(x, H - 0.15f, L * 0.5f + 0.02f)), 0.02f, p.BrushedSteel);
                WorldArchitectureKit.CreateBox(c.transform, "LockHandle", new Vector3(x + 0.08f, 1.2f, L * 0.5f + 0.05f), new Vector3(0.18f, 0.04f, 0.03f), p.BrushedSteel, addCollider: false);
            }
            WorldArchitectureKit.CreateBox(c.transform, "DoorSplit", new Vector3(0f, H * 0.5f, L * 0.5f - 0.03f), new Vector3(0.02f, H - 0.25f, 0.04f), Zone23Dressing.DrainVoid(p), addCollider: false);
            // corrugated walls between the corner posts and the top / bottom rails (both long sides, the blind end)
            float sy0 = 0.16f, sy1 = H - 0.10f;
            for (int i = -1; i <= 1; i += 2)
                Zone23Dressing.CorrugatedPanel(c.transform, i < 0 ? "SideSheetL" : "SideSheetR", new Vector3(i * (W * 0.5f - 0.015f), (sy0 + sy1) * 0.5f, 0f), new Vector3(i, 0f, 0f), L - 0.24f, sy1 - sy0, body);
            Zone23Dressing.CorrugatedPanel(c.transform, "EndSheet", new Vector3(0f, (sy0 + sy1) * 0.5f, -L * 0.5f + 0.015f), Vector3.back, W - 0.24f, sy1 - sy0, body);
            // door end: two leaves with four vertical pressings each, the hinge blocks on the corner posts
            for (int i = -1; i <= 1; i += 2)
            {
                Zone23Dressing.CorrugatedPanel(c.transform, i < 0 ? "DoorSheetL" : "DoorSheetR", new Vector3(i * (W * 0.25f - 0.055f), (sy0 + sy1) * 0.5f, L * 0.5f - 0.02f), Vector3.forward, W * 0.5f - 0.14f, sy1 - sy0, body, 0.022f);
                for (int k = 0; k < 4; k++)
                    WorldArchitectureKit.CreateBox(c.transform, "Hinge", new Vector3(i * (W * 0.5f - 0.13f), 0.35f + k * (H - 0.7f) / 3f, L * 0.5f + 0.005f), new Vector3(0.06f, 0.09f, 0.05f), p.DarkPaintedMetal, addCollider: false);
            }
            Zone23Dressing.Decal(c.transform, "ContainerGrime", c.transform.TransformPoint(new Vector3(W * 0.5f + 0.2f, 1.2f, 0f)), c.transform.right, new Vector3(4.5f, 2.4f, 0.5f), Zone23Dressing.Grime(p, (int)(yaw + floorCentre.x)), 0.55f);
            // fix round: rust on the roof and the door end, seen from the dawn crane shot (drawn to 130 m)
            int sd = (int)(floorCentre.x * 3f + floorCentre.z);
            // round 3: shallow (0.2 m) and angle-faded, so the roof rust no longer runs down the sides as a smeared band
            DecalProjector roofRust = Zone23Dressing.Decal(c.transform, "ContainerRoofRust", c.transform.TransformPoint(new Vector3(0f, H + 0.08f, (sd % 3 - 1) * 1.2f)), Vector3.up, new Vector3(2.0f, 4.0f, 0.2f), Zone23Dressing.Grime(p, sd), 0.85f, rollDeg: yaw + 90f, drawDistance: 130f);
            roofRust.startAngleFade = 25f; roofRust.endAngleFade = 50f;
            Zone23Dressing.Decal(c.transform, "ContainerDoorRust", c.transform.TransformPoint(new Vector3(0f, 0.9f, L * 0.5f + 0.15f)), c.transform.forward, new Vector3(2.3f, 1.6f, 0.5f), Zone23Dressing.Grime(p, sd + 1), 0.8f, drawDistance: 130f);
            Zone23Dressing.MergeStatic(c.transform);   // fix round: ~70 ribs/rails/castings -> one mesh per material
        }

        /// <summary>1990s yard clutter in the empty quarters (cover and sightline breaks, never on the route lines):
        /// containers, pallet stacks, drums, a second truck, an upright jeep, debris, drive lamps.</summary>
        private static void DressYardClutter(Transform parent, WorldMaterialPalette p)
        {
            var c = new GameObject("Yard_Clutter");
            c.transform.SetParent(parent, false);
            const float y = YardY;
            BuildContainer(c.transform, "Container_NE1", new Vector3(482f, y, 31f), 90f, Zone23Dressing.ContainerBlue(p), p);
            BuildContainer(c.transform, "Container_NE2", new Vector3(482.3f, y + 2.59f, 31.1f), 88f, Zone23Dressing.ContainerRed(p), p);
            BuildContainer(c.transform, "Container_NE3", new Vector3(497f, y, 41f), 0f, Zone23Dressing.ContainerGreen(p), p);
            BuildContainer(c.transform, "Container_S1", new Vector3(430f, y, -91.5f), 90f, Zone23Dressing.ContainerRed(p), p);
            BuildContainer(c.transform, "Container_S2", new Vector3(437f, y, -91.5f), 90f, Zone23Dressing.ContainerGreen(p), p);
            BuildContainer(c.transform, "Container_S3", new Vector3(433.5f, y + 2.59f, -91.4f), 91f, Zone23Dressing.ContainerBlue(p), p);
            BuildContainer(c.transform, "Container_SW", new Vector3(405.5f, y, -62f), 0f, Zone23Dressing.ContainerBlue(p), p);

            // pallet stacks (collider on the stack) with drums and crates
            (Vector3 pos, float yaw, int layers)[] stacks =
            {
                (new Vector3(474f, y, 14f), 10f, 4), (new Vector3(476f, y, 15.5f), 80f, 2), (new Vector3(489f, y, 22f), 35f, 5),
                (new Vector3(498f, y, 12f), 0f, 3), (new Vector3(420f, y, -70f), 20f, 4), (new Vector3(458f, y, -76f), 75f, 3),
                (new Vector3(409f, y, 30f), 0f, 3), (new Vector3(475f, y, -86f), 15f, 2),
            };
            foreach (var st in stacks)
            {
                for (int l = 0; l < st.layers; l++)
                    p.InstantiatePropOrFallback("pallet", c.transform, st.pos + Vector3.up * (l * 0.15f), new Vector3(0f, st.yaw + (l % 2) * 4f, 0f), new Vector3(1.2f, 1.2f, 1.2f), grounded: true, addCollider: false);
                GameObject col = WorldArchitectureKit.CreateBox(c.transform, "PalletStackCollider", Vector3.zero, new Vector3(1.2f, st.layers * 0.15f, 0.82f), null, addCollider: true);
                col.transform.SetPositionAndRotation(st.pos + Vector3.up * (st.layers * 0.075f), Quaternion.Euler(0f, st.yaw, 0f));
                col.GetComponent<MeshRenderer>().enabled = false;
            }
            (Vector3 pos, float yaw)[] drums =
            {
                (new Vector3(486.5f, y, 18.5f), 0f), (new Vector3(487.3f, y, 18.9f), 40f), (new Vector3(486.9f, y, 19.7f), 90f), (new Vector3(500f, y, 30f), 10f),
                (new Vector3(500.7f, y, 30.6f), 70f), (new Vector3(423f, y, -72f), 0f), (new Vector3(423.7f, y, -72.5f), 50f), (new Vector3(408f, y, -45f), 20f),
                (new Vector3(503.5f, y, -88f), 0f), (new Vector3(502.8f, y, -88.6f), 60f), (new Vector3(470.5f, y, -61f), 30f),
            };
            foreach (var dr in drums)
                p.InstantiatePropOrFallback("oil_drum", c.transform, dr.pos, new Vector3(0f, dr.yaw, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("oil_drum", c.transform, new Vector3(409.2f, y, -44.2f), new Vector3(90f, 30f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: false, customName: "oil_drum_tipped");
            (Vector3 pos, float yaw)[] crates = { (new Vector3(478f, y, 22f), 15f), (new Vector3(478.9f, y, 22.3f), 40f), (new Vector3(478.4f, y + 0.47f, 22.1f), 25f), (new Vector3(415f, y, -78f), 0f), (new Vector3(415.6f, y, -77.2f), 70f) };
            foreach (var cr in crates)
                p.InstantiatePropOrFallback("crate", c.transform, cr.pos, new Vector3(0f, cr.yaw, 0f), new Vector3(0.8f, 0.8f, 0.8f), grounded: true, addCollider: true);
            for (int i = 0; i < 6; i++)
                p.InstantiatePropOrFallback("jerry_can", c.transform, new Vector3(487.8f + (i % 3) * 0.35f, y, 20.6f + (i / 3) * 0.4f), new Vector3(0f, i * 23f, 0f), new Vector3(0.47f, 0.47f, 0.47f), grounded: true, addCollider: false);

            // vehicles and debris
            p.InstantiatePropOrFallback("utility_truck", c.transform, new Vector3(490f, y, 2f), new Vector3(0f, -32f, 0f), new Vector3(2.4f, 2.4f, 5.6f), grounded: true, addCollider: true, customName: "Yard_UtilityTruck2");
            p.InstantiatePropOrFallback("jeep", c.transform, new Vector3(447f, y, -79f), new Vector3(0f, 68f, 0f), new Vector3(2.1f, 1.9f, 4.4f), grounded: true, addCollider: true, customName: "Yard_ParkedJeep");
            p.InstantiatePropOrFallback("debris_pile", c.transform, new Vector3(412f, y, -84f), new Vector3(0f, 20f, 0f), new Vector3(3f, 3f, 3f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("debris_pile", c.transform, new Vector3(500f, y, -82f), new Vector3(0f, 140f, 0f), new Vector3(2.6f, 2.6f, 2.6f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("debris_pile", c.transform, new Vector3(470f, y, 45f), new Vector3(0f, 260f, 0f), new Vector3(3f, 3f, 3f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("utility_cart", c.transform, new Vector3(458.5f, y, 13f), new Vector3(0f, 100f, 0f), new Vector3(1.0f, 1.0f, 1.0f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("tree_stump_01", c.transform, new Vector3(404f, y, 44f), new Vector3(0f, 30f, 0f), new Vector3(1.4f, 1.4f, 1.4f), grounded: true, addCollider: false);

            // ---- the empty SW / central quarters (off the route lines: the drive is z -31.5..-24.5) ----
            // a container row with one stacked on top, south of the drive
            BuildContainer(c.transform, "Container_C1", new Vector3(427.5f, y, -48f), 90f, Zone23Dressing.ContainerGreen(p), p);
            BuildContainer(c.transform, "Container_C2", new Vector3(434.0f, y, -48.2f), 90f, Zone23Dressing.ContainerBlue(p), p);
            BuildContainer(c.transform, "Container_C3", new Vector3(441.0f, y, -47.6f), 84f, Zone23Dressing.ContainerRed(p), p);
            BuildContainer(c.transform, "Container_C4", new Vector3(430.6f, y + 2.59f, -48.1f), 92f, Zone23Dressing.ContainerRed(p), p);
            BuildContainer(c.transform, "Container_C5", new Vector3(452.5f, y, -40.5f), 18f, Zone23Dressing.ContainerGreen(p), p);
            // fix round: the lone C5 read as a grey box from the crane: green, with a blue one stacked across it
            BuildContainer(c.transform, "Container_C6", new Vector3(452.3f, y + 2.59f, -40.8f), 14f, Zone23Dressing.ContainerBlue(p), p);
            // a scrap and vehicle pile: a dead truck, a stripped jeep, debris, logs, drums and tyres of pallets
            p.InstantiatePropOrFallback("utility_truck", c.transform, new Vector3(446f, y, -64f), new Vector3(0f, 122f, 3f), new Vector3(2.4f, 2.4f, 5.6f), grounded: true, addCollider: true, customName: "Yard_DeadTruck");
            p.InstantiatePropOrFallback("jeep", c.transform, new Vector3(455.5f, y, -61f), new Vector3(0f, -20f, 0f), new Vector3(2.1f, 1.9f, 4.4f), grounded: true, addCollider: true, customName: "Yard_StrippedJeep");
            p.InstantiatePropOrFallback("debris_pile", c.transform, new Vector3(451f, y, -67.5f), new Vector3(0f, 75f, 0f), new Vector3(3.4f, 3.4f, 3.4f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("debris_pile", c.transform, new Vector3(441.5f, y, -59f), new Vector3(0f, 200f, 0f), new Vector3(2.4f, 2.4f, 2.4f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("fallen_log", c.transform, new Vector3(458f, y, -67f), new Vector3(0f, 64f, 0f), new Vector3(0.6f, 0.6f, 4.2f), grounded: true, addCollider: false, fallbackMat: p.WoodPlanks);
            p.InstantiatePropOrFallback("dead_tree_trunk", c.transform, new Vector3(443f, y, -70f), new Vector3(0f, 12f, 88f), new Vector3(0.9f, 0.9f, 5f), grounded: true, addCollider: false);
            (Vector3 pos, float yaw)[] scrapDrums = { (new Vector3(449.2f, y, -59.2f), 0f), (new Vector3(449.9f, y, -58.6f), 60f), (new Vector3(459.5f, y, -63.5f), 20f) };
            foreach (var dr in scrapDrums)
                p.InstantiatePropOrFallback("oil_drum", c.transform, dr.pos, new Vector3(0f, dr.yaw, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("oil_drum", c.transform, new Vector3(452.6f, y, -58.4f), new Vector3(90f, 115f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: false, customName: "oil_drum_tipped");
            BuildScrapPile(c.transform, new Vector3(437.5f, y, -64.5f), 30f, p);
            // pallet and drum rows along the drive's south edge (two clusters, gaps for the lamps at x 419 / 441)
            for (int i = 0; i < 6; i++)
            {
                float x = (i < 3 ? 423.5f : 446.5f) + (i % 3) * 1.45f;
                int layers = 2 + (i * 7) % 4;
                for (int l = 0; l < layers; l++)
                    p.InstantiatePropOrFallback("pallet", c.transform, new Vector3(x, y + l * 0.15f, -35.2f), new Vector3(0f, 90f + (l % 2) * 5f, 0f), new Vector3(1.2f, 1.2f, 1.2f), grounded: true, addCollider: false);
                GameObject col = WorldArchitectureKit.CreateBox(c.transform, "PalletStackCollider", new Vector3(x, y + layers * 0.075f, -35.2f), new Vector3(0.82f, layers * 0.15f, 1.2f), null, addCollider: true);
                col.GetComponent<MeshRenderer>().enabled = false;
            }
            for (int i = 0; i < 5; i++)
                p.InstantiatePropOrFallback("oil_drum", c.transform, new Vector3(429f + i * 0.72f + (i % 2) * 0.1f, y, -34.6f - (i % 2) * 0.7f), new Vector3(0f, i * 47f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: true);
            for (int i = 0; i < 4; i++)
                p.InstantiatePropOrFallback("crate", c.transform, new Vector3(452f + (i % 2) * 0.85f, y + (i / 2) * 0.8f, -35.3f), new Vector3(0f, 8f + i * 21f, 0f), new Vector3(0.8f, 0.8f, 0.8f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("trash_bin", c.transform, new Vector3(458.6f, y, -34.4f), new Vector3(0f, 200f, 0f), new Vector3(0.7f, 1.1f, 0.7f), grounded: true, addCollider: true);
            // a flatbed trailer parked between the workshop and Warehouse B
            BuildTrailer(c.transform, new Vector3(447.5f, y, -3.0f), 90f, p);
            for (int l = 0; l < 3; l++)
                p.InstantiatePropOrFallback("pallet", c.transform, new Vector3(446.2f, y + 1.05f + l * 0.15f, -3.0f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 1.2f, 1.2f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("crate", c.transform, new Vector3(449.4f, y + 1.05f, -3.1f), new Vector3(0f, 7f, 0f), new Vector3(0.8f, 0.8f, 0.8f), grounded: true, addCollider: false);
            p.InstantiatePropOrFallback("utility_cart", c.transform, new Vector3(417f, y, -6.5f), new Vector3(0f, 30f, 0f), new Vector3(1.0f, 1.0f, 1.0f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("oil_drum", c.transform, new Vector3(415.2f, y, -8.6f), new Vector3(0f, 10f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: true);

            // warehouse B plate over its south door + the stencil on its east wall
            Zone23Dressing.SignPlate(c.transform, "Sign_WarehouseB", new Vector3(432.5f, y + 5.4f, 3.82f), Vector3.back, 3.2f, 1.25f, Zone23Dressing.SignMat(p, "MAT_Z23_SignWarehouseB", "warehouse_b"), p.DarkPaintedMetal);
            Zone23Dressing.Decal(c.transform, "Stencil_WarehouseB_East", new Vector3(456.3f, y + 5.6f, 11f), Vector3.right, new Vector3(5.0f, 1.95f, 0.5f),
                Zone23Dressing.DecalMat(p, "MAT_Z23_DecalWarehouseB", "warehouse_b_decal", null, new Color(0.9f, 0.9f, 0.85f, 0.8f)), 0.85f, drawDistance: 80f);

            // drive lamps (AuxYard): lamp_post + head light, along the south drive and the north drive
            (Vector3 pos, float yaw)[] lamps = { (new Vector3(419f, y, -32.6f), 0f), (new Vector3(441f, y, -32.6f), 0f), (new Vector3(471f, y, -8f), 270f), (new Vector3(471f, y, 14f), 270f) };
            for (int i = 0; i < lamps.Length; i++)
            {
                Zone23Dressing.LampPost(p, c.transform, lamps[i].pos, lamps[i].yaw);
                WorldArchitectureKit.BuildHdrpLight(c.transform, $"Light_PathLamp_Yard_{i}", lamps[i].pos + new Vector3(0f, 4.1f, 0f), new Vector3(90f, 0f, 0f), LightType.Spot, new Color(1.0f, 0.86f, 0.62f), 2200f, 16f, LightGroupId.AuxYard, volumetricDimmer: 1.0f, spotOuterAngle: 100f);
            }
        }

        /// <summary>Round 2: the exit court read as an empty slab from the dawn crane shot. Parked vehicles in the painted
        /// bays (south end, clear of the drive at z -31..-24), a skip and pallets against the walls, a fuel bowser trailer.
        /// The sprint line drive -> E-0 (466,-28) -> (505,-46) and the control box (503.8,-52.6) stay clear.</summary>
        private static void DressExitCourt(Transform parent, WorldMaterialPalette p)
        {
            var c = new GameObject("ExitCourt_Dressing");
            c.transform.SetParent(parent, false);
            const float y = YardY;
            p.InstantiatePropOrFallback("utility_truck", c.transform, new Vector3(469.2f, y, -51.2f), new Vector3(0f, 92f, 0f), new Vector3(2.4f, 2.4f, 5.6f), grounded: true, addCollider: true, customName: "Court_ParkedTruck");
            p.InstantiatePropOrFallback("jeep", c.transform, new Vector3(469.0f, y, -44.8f), new Vector3(0f, 86f, 0f), new Vector3(2.1f, 1.9f, 4.4f), grounded: true, addCollider: true, customName: "Court_ParkedJeep");
            Zone23Dressing.BuildSkip(c.transform, new Vector3(498.5f, y, -24.2f), Quaternion.Euler(0f, 0f, 0f), p);
            GameObject skipCol = WorldArchitectureKit.CreateBox(c.transform, "Court_SkipCollider", new Vector3(498.5f, y + 0.6f, -24.2f), new Vector3(1.9f, 1.2f, 1.1f), null, addCollider: true);
            skipCol.GetComponent<MeshRenderer>().enabled = false;
            Zone23Dressing.BuildSkip(c.transform, new Vector3(501.0f, y, -24.3f), Quaternion.Euler(0f, 4f, 0f), p);
            GameObject skipCol2 = WorldArchitectureKit.CreateBox(c.transform, "Court_SkipCollider2", new Vector3(501.0f, y + 0.6f, -24.3f), new Vector3(1.9f, 1.2f, 1.1f), null, addCollider: true);
            skipCol2.GetComponent<MeshRenderer>().enabled = false;
            for (int k = 0; k < 3; k++)
            {
                Vector3 sp = new Vector3(492.5f + k * 1.5f, y, -57.0f);
                int layers = 3 + k % 2 * 2;
                for (int l = 0; l < layers; l++)
                    p.InstantiatePropOrFallback("pallet", c.transform, sp + Vector3.up * (l * 0.15f), new Vector3(0f, (l % 2) * 5f, 0f), new Vector3(1.2f, 1.2f, 1.2f), grounded: true, addCollider: false);
                GameObject col = WorldArchitectureKit.CreateBox(c.transform, "Court_PalletCollider", sp + Vector3.up * (layers * 0.075f), new Vector3(0.82f, layers * 0.15f, 1.2f), null, addCollider: true);
                col.GetComponent<MeshRenderer>().enabled = false;
            }
            p.InstantiatePropOrFallback("oil_drum", c.transform, new Vector3(496.9f, y, -57.4f), new Vector3(0f, 20f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: true);
            p.InstantiatePropOrFallback("oil_drum", c.transform, new Vector3(497.6f, y, -56.8f), new Vector3(0f, 70f, 0f), new Vector3(0.9f, 0.9f, 0.9f), grounded: true, addCollider: true);
            BuildTrailer(c.transform, new Vector3(486.5f, y, -20.5f), 92f, p);
            p.InstantiatePropOrFallback("diesel_fuel_tank_pump", c.transform, new Vector3(486.3f, y + 1.0f, -20.6f), new Vector3(0f, 2f, 0f), new Vector3(3.0f, 1.6f, 1.8f), grounded: true, addCollider: false, customName: "Court_FuelBowserTank");
            Zone23Dressing.Decal(c.transform, "Court_OilUnderTruck", new Vector3(469.2f, y + 0.05f, -51.2f), Vector3.up, new Vector3(2.4f, 4.0f, 0.4f), Zone23Dressing.Grime(p, 4), 0.7f, rollDeg: 90f);
            Zone23Dressing.Decal(c.transform, "Court_TyreMarks", new Vector3(486f, y + 0.05f, -38f), Vector3.up, new Vector3(4.0f, 9.0f, 0.4f), Zone23Dressing.Grime(p, 5), 0.5f, rollDeg: 62f);

            // ---- fix round: what the dawn crane shot actually sees of the court (x < ~484: closer to the east wall the
            // ground is hidden by it). A forklift in a painted loading box, cable drums, tyre stacks, a scrap bin, oil
            // and tyre marks over the bare asphalt. Everything is off the sprint line (466,-28) -> (505,-46) by > 6 m.
            Material yellowPaint = p.Surface("MAT_Z23_RoadPaintYellow", "concrete_polished", "BaseColor", "MaskMap", 1.5f, new Color(1.9f, 1.45f, 0.35f), 0.25f, 0.7f, 0f, 0.5f);
            // painted loading box (yellow outline + hatching), x 471.5 .. 481.5, z -44.5 .. -39.5
            const float bx0 = 471.5f, bx1 = 481.5f, bz0 = -44.5f, bz1 = -39.5f, py = y + 0.0145f;
            Zone23Dressing.Box(c.transform, "LoadBoxEdgeN", new Vector3((bx0 + bx1) * 0.5f, py, bz1), new Vector3(bx1 - bx0, 0.003f, 0.12f), yellowPaint, Quaternion.identity);
            Zone23Dressing.Box(c.transform, "LoadBoxEdgeS", new Vector3((bx0 + bx1) * 0.5f, py, bz0), new Vector3(bx1 - bx0, 0.003f, 0.12f), yellowPaint, Quaternion.identity);
            Zone23Dressing.Box(c.transform, "LoadBoxEdgeW", new Vector3(bx0, py, (bz0 + bz1) * 0.5f), new Vector3(0.12f, 0.003f, bz1 - bz0), yellowPaint, Quaternion.identity);
            Zone23Dressing.Box(c.transform, "LoadBoxEdgeE", new Vector3(bx1, py, (bz0 + bz1) * 0.5f), new Vector3(0.12f, 0.003f, bz1 - bz0), yellowPaint, Quaternion.identity);
            for (float hx = bx0 + 1.2f; hx < bx1 - 0.4f; hx += 1.6f)
                Zone23Dressing.Box(c.transform, "LoadBoxHatch", new Vector3(hx, py, (bz0 + bz1) * 0.5f), new Vector3(0.12f, 0.003f, (bz1 - bz0) * 1.3f), yellowPaint, Quaternion.Euler(0f, 38f, 0f));
            BuildForklift(c.transform, new Vector3(476.0f, y, -42.0f), 200f, p);
            GameObject flCol = WorldArchitectureKit.CreateBox(c.transform, "Court_ForkliftCollider", new Vector3(476.0f, y + 1.0f, -42.0f), new Vector3(1.3f, 2.0f, 2.6f), null, addCollider: true, euler: new Vector3(0f, 200f, 0f));
            flCol.GetComponent<MeshRenderer>().enabled = false;
            // cable drums (one on its side) and tyre stacks south of the loading box, by the parked truck
            CableDrum(c.transform, new Vector3(477.6f, y, -53.0f), 0f, 0.62f, 0.95f, p, upright: true);
            CableDrum(c.transform, new Vector3(479.4f, y, -53.6f), 25f, 0.55f, 0.85f, p, upright: true);
            CableDrum(c.transform, new Vector3(478.4f, y, -55.6f), 80f, 0.5f, 0.7f, p, upright: false);
            TyreStack(c.transform, new Vector3(474.2f, y, -56.2f), 4, p);
            TyreStack(c.transform, new Vector3(475.3f, y, -57.0f), 3, p);
            TyreStack(c.transform, new Vector3(473.6f, y, -57.3f), 2, p);
            GameObject drumCol = WorldArchitectureKit.CreateBox(c.transform, "Court_DrumsCollider", new Vector3(476.8f, y + 0.6f, -55.2f), new Vector3(6.2f, 1.2f, 3.6f), null, addCollider: true);
            drumCol.GetComponent<MeshRenderer>().enabled = false;
            // north of the drive mouth: a pair of drums and tyres against the trailer
            CableDrum(c.transform, new Vector3(477.0f, y, -21.6f), 10f, 0.62f, 0.95f, p, upright: true);
            TyreStack(c.transform, new Vector3(479.2f, y, -21.2f), 3, p);
            GameObject nCol = WorldArchitectureKit.CreateBox(c.transform, "Court_NorthDrumsCollider", new Vector3(478.1f, y + 0.5f, -21.4f), new Vector3(3.4f, 1.0f, 1.6f), null, addCollider: true);
            nCol.GetComponent<MeshRenderer>().enabled = false;
            Zone23Dressing.Decal(c.transform, "Court_OilLoadBox", new Vector3(476.6f, y + 0.05f, -41.6f), Vector3.up, new Vector3(3.0f, 2.2f, 0.4f), Zone23Dressing.Grime(p, 7), 0.75f, rollDeg: 20f, drawDistance: 120f);
            Zone23Dressing.Decal(c.transform, "Court_TyreMarksW", new Vector3(476f, y + 0.05f, -33f), Vector3.up, new Vector3(3.6f, 10.0f, 0.4f), Zone23Dressing.Grime(p, 6), 0.55f, rollDeg: 105f, drawDistance: 120f);
            Zone23Dressing.Decal(c.transform, "Court_StainS", new Vector3(481f, y + 0.05f, -49f), Vector3.up, new Vector3(5.0f, 4.0f, 0.4f), Zone23Dressing.Grime(p, 2), 0.6f, rollDeg: 70f, drawDistance: 120f);
            Zone23Dressing.MergeStatic(c.transform);
        }

        /// <summary>A kit-built counterbalance forklift (~2.6 m long, 2.1 m to the guard): yellow body and counterweight,
        /// overhead guard, mast with forks lowered, four tyres. Collider-free (the caller adds one block).</summary>
        private static void BuildForklift(Transform parent, Vector3 floor, float yaw, WorldMaterialPalette p)
        {
            var g = new GameObject("Court_Forklift");
            g.transform.SetParent(parent, false);
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Material y = Zone23Dressing.PaintYellow(p), dark = p.DarkPaintedMetal, rub = Zone23Dressing.Rubber(p);
            Vector3 A(float x, float yy, float z) => floor + r * new Vector3(x, yy, z);
            Zone23Dressing.Box(g.transform, "Chassis", A(0f, 0.55f, -0.1f), new Vector3(1.05f, 0.5f, 1.9f), y, r);
            Zone23Dressing.Box(g.transform, "Counterweight", A(0f, 0.7f, -1.0f), new Vector3(1.1f, 0.8f, 0.5f), y, r);
            Zone23Dressing.Box(g.transform, "Seat", A(0f, 0.95f, -0.45f), new Vector3(0.5f, 0.35f, 0.45f), dark, r);
            Zone23Dressing.Box(g.transform, "Dash", A(0f, 1.0f, 0.35f), new Vector3(0.9f, 0.3f, 0.3f), dark, r);
            Zone23Dressing.Cyl(g.transform, "SteerColumn", A(0f, 1.05f, 0.25f), A(0f, 1.3f, 0.05f), 0.025f, dark);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Zone23Dressing.Cyl(g.transform, "GuardPostF", A(sx * 0.48f, 0.8f, 0.45f), A(sx * 0.48f, 2.1f, 0.35f), 0.035f, dark);
                Zone23Dressing.Cyl(g.transform, "GuardPostR", A(sx * 0.48f, 0.8f, -0.85f), A(sx * 0.48f, 2.1f, -0.85f), 0.035f, dark);
                Zone23Dressing.Box(g.transform, "MastRail", A(sx * 0.32f, 1.25f, 0.88f), new Vector3(0.08f, 2.4f, 0.1f), dark, r);
                Zone23Dressing.Box(g.transform, "Fork", A(sx * 0.25f, 0.05f, 1.45f), new Vector3(0.11f, 0.05f, 1.05f), dark, r);
                Zone23Dressing.Box(g.transform, "ForkShank", A(sx * 0.25f, 0.4f, 0.95f), new Vector3(0.11f, 0.7f, 0.05f), dark, r);
                Zone23Dressing.Cyl(g.transform, "TyreF", A(sx * 0.48f, 0.3f, 0.45f), A(sx * 0.62f, 0.3f, 0.45f), 0.3f, rub);
                Zone23Dressing.Cyl(g.transform, "TyreR", A(sx * 0.46f, 0.24f, -0.75f), A(sx * 0.6f, 0.24f, -0.75f), 0.24f, rub);
            }
            Zone23Dressing.Box(g.transform, "GuardRoof", A(0f, 2.12f, -0.25f), new Vector3(1.02f, 0.05f, 1.35f), dark, r);
            for (int k = 0; k < 4; k++) Zone23Dressing.Box(g.transform, "GuardSlat", A(0f, 2.15f, -0.8f + k * 0.37f), new Vector3(1.0f, 0.03f, 0.05f), dark, r);
            Zone23Dressing.Box(g.transform, "MastCross", A(0f, 2.4f, 0.88f), new Vector3(0.72f, 0.08f, 0.1f), dark, r);
            Zone23Dressing.Box(g.transform, "Carriage", A(0f, 0.55f, 0.95f), new Vector3(0.75f, 0.35f, 0.06f), dark, r);
            GameObject beacon = Zone23Dressing.Box(g.transform, "Beacon", A(0f, 2.2f, -0.85f), new Vector3(0.1f, 0.1f, 0.1f), Zone23Dressing.PaintYellow(p), r);
            Zone23Dressing.Decal(g.transform, "ForkliftGrime", A(0f, 0.7f, -1.26f), r * Vector3.back, new Vector3(1.1f, 0.8f, 0.3f), Zone23Dressing.Grime(p, 3), 0.75f);
            Zone23Dressing.MergeStatic(g.transform);
        }

        /// <summary>A wooden cable drum (two flanges on a core, a few turns of black cable), upright on a flange or on
        /// its side.</summary>
        private static void CableDrum(Transform parent, Vector3 floor, float yaw, float radius, float width, WorldMaterialPalette p, bool upright)
        {
            var g = new GameObject("Court_CableDrum");
            g.transform.SetParent(parent, false);
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Vector3 axis = upright ? Vector3.up : r * Vector3.right;
            Vector3 c0 = upright ? floor : floor + Vector3.up * radius - axis * (width * 0.5f);
            Vector3 c1 = c0 + axis * width;
            Zone23Dressing.Cyl(g.transform, "Flange", c0, c0 + axis * 0.05f, radius, p.WoodPlanks);
            Zone23Dressing.Cyl(g.transform, "Flange", c1 - axis * 0.05f, c1, radius, p.WoodPlanks);
            Zone23Dressing.Cyl(g.transform, "Cable", c0 + axis * 0.05f, c1 - axis * 0.05f, radius * 0.78f, Zone23Dressing.Rubber(p));
            Zone23Dressing.Cyl(g.transform, "Hub", c1, c1 + axis * 0.02f, radius * 0.22f, p.BrushedSteel);
        }

        /// <summary>A stack of n truck tyres (rubber ring with a dark void in the middle).</summary>
        private static void TyreStack(Transform parent, Vector3 floor, int n, WorldMaterialPalette p)
        {
            var g = new GameObject("Court_TyreStack");
            g.transform.SetParent(parent, false);
            for (int i = 0; i < n; i++)
            {
                Vector3 b = floor + new Vector3(Mathf.Sin(i * 2.1f) * 0.04f, i * 0.3f, Mathf.Cos(i * 1.7f) * 0.04f);
                Zone23Dressing.Cyl(g.transform, "Tyre", b, b + Vector3.up * 0.29f, 0.5f, Zone23Dressing.Rubber(p));
                Zone23Dressing.Cyl(g.transform, "TyreVoid", b + Vector3.up * 0.288f, b + Vector3.up * 0.292f, 0.27f, Zone23Dressing.DrainVoid(p));
            }
        }

        /// <summary>A kit-built flatbed trailer: steel deck on two side beams, tandem axle with wheels, A-frame tongue on a
        /// jack stand. Collider on the deck block only.</summary>
        private static void BuildTrailer(Transform parent, Vector3 floorCentre, float yaw, WorldMaterialPalette p)
        {
            var t = new GameObject("Yard_FlatbedTrailer");
            t.transform.SetParent(parent, false);
            t.transform.SetPositionAndRotation(floorCentre, Quaternion.Euler(0f, yaw, 0f));
            Transform tr = t.transform;
            const float L = 6.2f, W = 2.3f, deck = 1.0f;
            Material paint = Zone23Dressing.ContainerRed(p);
            WorldArchitectureKit.CreateBox(tr, "Deck", new Vector3(0f, deck - 0.03f, 0f), new Vector3(W, 0.06f, L), Zone23Dressing.TransformerGrey(p), addCollider: true);
            for (int i = -1; i <= 1; i += 2)
            {
                WorldArchitectureKit.CreateBox(tr, "SideBeam", new Vector3(i * (W * 0.5f - 0.1f), deck - 0.18f, 0f), new Vector3(0.12f, 0.24f, L), paint, addCollider: false);
                WorldArchitectureKit.CreateBox(tr, "Fender", new Vector3(i * (W * 0.5f - 0.05f), 0.86f, -0.9f), new Vector3(0.3f, 0.05f, 1.9f), p.DarkPaintedMetal, addCollider: false);
            }
            for (float z = -L * 0.5f + 0.4f; z < L * 0.5f; z += 0.9f)
                WorldArchitectureKit.CreateBox(tr, "CrossMember", new Vector3(0f, deck - 0.13f, z), new Vector3(W - 0.2f, 0.1f, 0.08f), p.DarkPaintedMetal, addCollider: false);
            foreach (float az in new[] { -1.35f, -0.45f })
            {
                Zone23Dressing.Cyl(tr, "Axle", t.transform.TransformPoint(new Vector3(-W * 0.5f, 0.38f, az)), t.transform.TransformPoint(new Vector3(W * 0.5f, 0.38f, az)), 0.05f, p.DarkPaintedMetal);
                for (int i = -1; i <= 1; i += 2)
                {
                    Zone23Dressing.Cyl(tr, "Tyre", t.transform.TransformPoint(new Vector3(i * (W * 0.5f - 0.18f), 0.38f, az)), t.transform.TransformPoint(new Vector3(i * (W * 0.5f + 0.06f), 0.38f, az)), 0.38f, Zone23Dressing.DrainVoid(p));
                    Zone23Dressing.Cyl(tr, "Hub", t.transform.TransformPoint(new Vector3(i * (W * 0.5f + 0.05f), 0.38f, az)), t.transform.TransformPoint(new Vector3(i * (W * 0.5f + 0.08f), 0.38f, az)), 0.2f, p.BrushedSteel);
                }
            }
            Vector3 hitch = t.transform.TransformPoint(new Vector3(0f, 0.62f, L * 0.5f + 1.3f));
            Zone23Dressing.Cyl(tr, "TongueA", t.transform.TransformPoint(new Vector3(-W * 0.5f + 0.1f, deck - 0.2f, L * 0.5f)), hitch, 0.06f, paint);
            Zone23Dressing.Cyl(tr, "TongueB", t.transform.TransformPoint(new Vector3(W * 0.5f - 0.1f, deck - 0.2f, L * 0.5f)), hitch, 0.06f, paint);
            Zone23Dressing.Cyl(tr, "JackStand", hitch - t.transform.forward * 0.3f, hitch - t.transform.forward * 0.3f - Vector3.up * 0.62f, 0.04f, p.BrushedSteel);
            Zone23Dressing.Box(tr, "JackFoot", hitch - t.transform.forward * 0.3f - Vector3.up * 0.6f, new Vector3(0.2f, 0.03f, 0.2f), p.BrushedSteel, t.transform.rotation);
            WorldArchitectureKit.CreateBox(tr, "TailLightL", new Vector3(-W * 0.5f + 0.2f, deck - 0.18f, -L * 0.5f - 0.01f), new Vector3(0.18f, 0.08f, 0.02f), p.EmissiveRed, addCollider: false);
            WorldArchitectureKit.CreateBox(tr, "TailLightR", new Vector3(W * 0.5f - 0.2f, deck - 0.18f, -L * 0.5f - 0.01f), new Vector3(0.18f, 0.08f, 0.02f), p.EmissiveRed, addCollider: false);
        }

        /// <summary>A scrap heap: bent panels, beams and pipe offcuts leaning on each other (one rough collider).</summary>
        private static void BuildScrapPile(Transform parent, Vector3 c, float yaw, WorldMaterialPalette p)
        {
            var s = new GameObject("Yard_ScrapPile");
            s.transform.SetParent(parent, false);
            s.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, yaw, 0f));
            Transform tr = s.transform;
            Material[] mats = { p.CorrugatedMetal, p.RustedIron, Zone23Dressing.ContainerBlue(p), p.DarkPaintedMetal, Zone23Dressing.TransformerGrey(p) };
            for (int i = 0; i < 14; i++)
            {
                float h1 = Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5453f, 1f), h2 = Mathf.Repeat(Mathf.Sin(i * 78.233f) * 12543.1f, 1f);
                Vector3 lp = new Vector3((h1 - 0.5f) * 3.2f, 0.12f + 0.12f * (i % 4), (h2 - 0.5f) * 2.4f);
                Vector3 size = i % 3 == 0 ? new Vector3(1.8f + h2, 0.04f, 1.0f + h1) : (i % 3 == 1 ? new Vector3(0.14f, 0.14f, 2.2f + h1 * 1.5f) : new Vector3(0.9f, 0.6f, 0.03f));
                WorldArchitectureKit.CreateBox(tr, "Scrap", lp, size, mats[i % mats.Length], addCollider: false, euler: new Vector3((h1 - 0.5f) * 50f, h2 * 360f, (h2 - 0.5f) * 40f));
            }
            for (int i = 0; i < 4; i++)
                Zone23Dressing.Cyl(tr, "ScrapPipe", s.transform.TransformPoint(new Vector3(-1.2f + i * 0.5f, 0.1f + 0.1f * i, -0.8f)), s.transform.TransformPoint(new Vector3(-0.2f + i * 0.4f, 0.25f + 0.15f * i, 1.1f)), 0.07f + 0.02f * i, p.RustedIron);
            GameObject col = WorldArchitectureKit.CreateBox(tr, "ScrapCollider", new Vector3(0f, 0.4f, 0f), new Vector3(3.2f, 0.8f, 2.4f), null, addCollider: true);
            col.GetComponent<MeshRenderer>().enabled = false;
        }

        private static void AttachNamedWorldAnchor(GameObject target, string anchorId)
        {
            if (target == null || string.IsNullOrWhiteSpace(anchorId)) return;
            WorldAnchor wa = target.GetComponent<WorldAnchor>() ?? target.AddComponent<WorldAnchor>();
            var so = new SerializedObject(wa);
            so.FindProperty("anchorId").stringValue = anchorId;
            so.FindProperty("registerAsNamedObject").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateDinoBlockerSlab(Transform parent, string name, Vector3 localPos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.layer = GameLayers.DinoBlocker;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;

            // Carve T. rex NavMesh cleanly around blocked barriers!
            NavMeshObstacle obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.size = size;
            obs.carving = true;
        }
    }
}
