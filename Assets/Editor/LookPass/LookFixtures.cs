using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Editor.WorldBuilder;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Kit-level look step "fixtures": every lamp gets a body and every body glows with its lamp.
    ///
    /// Walks the scene's lights by name family (the same names as the lamp table in LookLights.cs) and builds a
    /// fixture next to each light that has none: recessed troffers, battery twin-head emergency units, high-bay
    /// shades, floor cans, a bare bulb on a cord, lamp-post heads, bollard collars, flood heads on crossarms,
    /// pendant shades and beacon domes. Each fixture's emissive lens is added to the light's LightGroupMember
    /// (emissiveRenderers) so it dims, flickers and dies with the light group. Fixture parts never cast shadows (the
    /// lamps use cached shadow maps with a 0.12 m near plane, a body must not block its own lamp).
    ///
    /// Also runs the other kit-level passes: signs (LookSigns), roofs + hidden rain blockers (LookRoofs), truthful
    /// reflection probes (LookProbes) and static flags. Idempotent: generated objects are named Fixture_* and are
    /// rebuilt from scratch on every run; lights moved into a fixture head land on the same spot again.
    /// </summary>
    public static class LookFixtures
    {
        public const string Prefix = "Fixture_";

        private static WorldMaterialPalette s_p;
        private static readonly Dictionary<string, Material> s_lens = new Dictionary<string, Material>();
        private static int s_mask;

        public static string Apply()
        {
            if (GameObject.Find("World") == null) return "fixtures: no World root (menu scene) - skipped";
            s_p = new WorldMaterialPalette();
            s_lens.Clear();
            s_enamel = null;
            s_battHousing = null;
            s_battLed = null;
            s_mask = ~((1 << GameLayers.RainBlocker) | (1 << GameLayers.Viewmodel) | (1 << GameLayers.Trigger) | (1 << 2) | (1 << GameLayers.Player));

            var log = new List<string>();
            log.Add(BuildFixtures());
            log.Add(LookSigns.Apply());
            log.Add(LookRoofs.Apply(s_p));
            log.Add(LookProbes.Apply());
            log.Add(MarkStatic());
            AssetDatabase.SaveAssets();
            return string.Join("; ", log);
        }

        // ============================================================================================================
        // FIXTURES
        // ============================================================================================================
        private static readonly Regex Skip = new Regex(@"^(Flashlight|Light_Evac|StatusLight|ChargingLed|Light_JeepRadioGlow|Light_Dock_StormBacklight|Light_Lobby_SkylightDayFill|Light_Lobby_MoonFill|Bounce$)|_Fill$|_Wash(_\d)?$|_CeilFill(_\d+)?$");   // + bounce and fill stand-ins: light without a lamp

        private static string BuildFixtures()
        {
            ClearPrevious();
            s_troffers.Clear();
            Physics.SyncTransforms();
            var counts = new SortedDictionary<string, int>();
            int unmatched = 0;
            var misses = new List<string>();
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional || Skip.IsMatch(l.name)) continue;
                if (!l.transform.root.name.Equals("World")) continue;
                string family = Build(l);
                if (family == null) { unmatched++; if (misses.Count < 10) misses.Add(l.name); continue; }
                NoContactShadows(l);
                counts.TryGetValue(family, out int c);
                counts[family] = c + 1;
            }
            counts["troffer grid companions"] = TrofferGrid();
            SharpenSecurityCompanions();
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add($"{kv.Key}={kv.Value}");
            return "fixtures: " + string.Join(", ", parts) + (unmatched > 0 ? $"; no family: {unmatched} [{string.Join(", ", misses)}]" : "");
        }

        /// <summary>Round 4: the security office is the game's first room. Its companion troffers cast a cached 256 shadow over a
        /// 176 deg cone, which drew a stair-stepped edge down the back-right corner (review 3 office_on x 720-735, intro_20):
        /// 512 there only (cached, so the cost is atlas space, not frame time).</summary>
        private static void SharpenSecurityCompanions()
        {
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.shadows == LightShadows.None || !Regex.IsMatch(l.name, "^Fixture_Lamp_Light_Security_.*_Grid\\d+$")) continue;
                HDAdditionalLightData hd = l.GetComponent<HDAdditionalLightData>();
                if (hd == null) continue;
                hd.shadowResolution.useOverride = true;
                hd.shadowResolution.@override = 512;
                EditorUtility.SetDirty(hd);
            }
        }

        private static string Build(Light l)
        {
            string n = l.name;
            if (Regex.IsMatch(n, "^Light_Corr_Main")) return ExistingTroffer(l);
            if (n.Contains("Emerg")) return Emergency(l);
            // Visitor Center battery units: a twin-head emergency unit with glowing lenses at each pool's apex
            if (Regex.IsMatch(n, "_Batt(_\\d+)?$") && !n.Contains("Bulkhead")) return Emergency(l);
            if (Regex.IsMatch(n, "^Light_(Canopy|Gallery)_Down")) return Downlight(l);
            if (Regex.IsMatch(n, "^Light_(Lobby_Main|WH_Aux|Dock_Main)")) return HighBay(l);   // round 4: the dock canopy too (it had four flat grey panels)
            if (Regex.IsMatch(n, "^Light_(GiftShop_Pendant|Lobby_ReceptionTask)")) return DeskPendant(l);
            if (Regex.IsMatch(n, "^Light_Lobby_(Grazer|Accent)|^Light_FacadeUplight|^Light_SecWest_WallPack")) return SpotHead(l);
            if (Regex.IsMatch(n, "^Light_Lobby_MezzDown")) return Downlight(l);
            if (Regex.IsMatch(n, "^Light_Kiosk_Interior")) return Bulb(l);
            if (Regex.IsMatch(n, "^Light_(Server|Cold)_")) return CeilingPanel(l, 0.6f, 0.6f, "panel");
            if (Regex.IsMatch(n, "_Main|^Light_GiftShop")) return CeilingPanel(l, 1.22f, 0.62f, "troffer");
            if (Regex.IsMatch(n, "^Light_Storage_Bulb")) return Bulb(l);
            if (Regex.IsMatch(n, "^Light_(FossilUplight|TreeUp)")) return FloorCan(l);
            if (Regex.IsMatch(n, "^Light_(PathLamp|PlazaLamp)")) return PathLamp(l);
            if (Regex.IsMatch(n, "^Light_(PlazaBollard|AuxRouteGreen)")) return BollardCollar(l);
            if (Regex.IsMatch(n, "^Light_SubBulkhead")) return Bulkhead(l);
            if (n.Contains("Bulkhead")) return Bulkhead(l);              // battery bulkheads on walls (zones 2-3)
            if (Regex.IsMatch(n, "^Light_(Talon_Flood|AuxSubFlood|CourtAuxFlood|E0Gate_GreenOverhead)")) return Flood(l);
            if (Regex.IsMatch(n, "^Light_WorkshopPendant")) return WorkshopPendant(l);
            if (Regex.IsMatch(n, "^Light_Security_DeskTask")) return DeskPendant(l);
            if (Regex.IsMatch(n, "^Light_Records_ManualBeacon")) return DeskBeacon(l);
            if (Regex.IsMatch(n, "Strobe|Beacon|Hazard|Kiosk")) return Beacon(l);
            return null;
        }

        private static void ClearPrevious()
        {
            var old = new List<GameObject>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name.StartsWith(Prefix)) old.Add(t.gameObject);
            foreach (GameObject g in old) if (g != null) Object.DestroyImmediate(g);
            // drop dangling emissive references left by the deleted lenses
            foreach (LightGroupMember m in Object.FindObjectsByType<LightGroupMember>(FindObjectsInactive.Include))
                PruneEmissives(m);
        }

        // ---- families -------------------------------------------------------------------------------------------

        /// <summary>Staff corridor: the builder already made housing + diffuser and paired them. The diffuser gets the
        /// same lens luminance as every other fixture (see <see cref="DiffuserNits"/>).</summary>
        private static string ExistingTroffer(Light l)
        {
            LightGroupMember m = MemberOf(l);
            if (m == null) return "corridor(existing)";
            var so = new SerializedObject(m);
            SerializedProperty ep = so.FindProperty("emissiveRenderers");
            Material lens = LensFor(l);
            for (int i = 0; i < ep.arraySize; i++)
            {
                if (ep.GetArrayElementAtIndex(i).objectReferenceValue is Renderer r && r.sharedMaterial != null
                    && (r.sharedMaterial == s_p.EmissiveCoolWhite || r.sharedMaterial == s_p.EmissiveWarmWhite
                        || r.sharedMaterial.name.StartsWith("MAT_Z1_CorridorTube") || r.sharedMaterial.name.StartsWith("MAT_Lens_")))
                {
                    r.sharedMaterial = lens;
                    EditorUtility.SetDirty(r);
                }
            }
            return "corridor(existing, lens matched)";
        }

        /// <summary>Recessed ceiling fixture: dark frame lip, diffuser panel and a louvre grid, flush under the ceiling.
        /// Room mains also get companion troffers in a grid over the rest of the ceiling (see <see cref="TrofferGrid"/>).</summary>
        private static string CeilingPanel(Light l, float sx, float sz, string family)
        {
            Vector3 p = l.transform.position;
            float ceilY = CeilingAbove(p, 1.6f, out Collider ceiling) ?? (p.y + 0.06f);
            Renderer lens = PanelAt(l, new Vector3(p.x, ceilY, p.z), sx, sz, "");
            // Visitor Center: the lamp just under the diffuser (3.5 cm), so its cone horizon sits at the cornice, not 7 cm down
            MoveLight(l, new Vector3(p.x, ceilY - (InZone1(l.transform) ? 0.035f : 0.07f), p.z));
            Pair(l, lens);
            if (family == "troffer" && ceiling != null) s_troffers.Add((l, ceiling, ceilY));
            return family;
        }

        private static Renderer PanelAt(Light l, Vector3 ceilPoint, float sx, float sz, string suffix)
        {
            Transform root = Root(l, ceilPoint, Quaternion.identity);
            if (suffix.Length > 0) root.name += suffix;
            // white enamel frame and louvres: dark metal read as a black wire grid on every dead ceiling (review 1)
            bool z1 = InZone1(l.transform);
            Material frame = z1 ? Enamel() : s_p.DarkPaintedMetal, bar = z1 ? Enamel() : s_p.BrushedSteel;
            Part(root, "Frame", PrimitiveType.Cube, new Vector3(0f, -0.012f, 0f), new Vector3(sx + 0.04f, 0.024f, sz + 0.04f), frame);
            Renderer lens = Part(root, "Diffuser", PrimitiveType.Cube, new Vector3(0f, -0.026f, 0f), new Vector3(sx - 0.04f, 0.008f, sz - 0.04f), LensFor(l));
            int bars = sx > 1f ? 3 : 1;
            for (int i = 1; i <= bars; i++)
            {
                float x = -sx * 0.5f + sx * i / (bars + 1);
                Part(root, $"Louvre_{i}", PrimitiveType.Cube, new Vector3(x, -0.034f, 0f), new Vector3(0.018f, 0.012f, sz - 0.04f), bar);
            }
            Part(root, "Spine", PrimitiveType.Cube, new Vector3(0f, -0.034f, 0f), new Vector3(sx - 0.04f, 0.012f, 0.018f), bar);
            return lens;
        }

        private static readonly List<(Light light, Collider ceiling, float y)> s_troffers = new List<(Light, Collider, float)>();

        /// <summary>
        /// A real office ceiling has a troffer every few metres, not one in the middle. For each ceiling slab that
        /// carries room mains, fill a 3.0 x 2.4 m grid (anchored on the first lamp, 0.9 m clear of the edges) with
        /// companion troffers; each is paired with its nearest lamp, so the whole ceiling dims and dies together.
        /// </summary>
        private static int TrofferGrid()
        {
            int built = 0;
            var byCeiling = new Dictionary<Collider, List<(Light light, float y)>>();
            foreach (var (light, ceiling, y) in s_troffers)
            {
                if (!byCeiling.TryGetValue(ceiling, out var list)) byCeiling[ceiling] = list = new List<(Light, float)>();
                list.Add((light, y));
            }
            foreach (var kv in byCeiling)
            {
                Bounds b = kv.Key.bounds;
                var lamps = kv.Value;
                if (b.size.x * b.size.z > 400f) continue;
                bool zone1 = InZone1(lamps[0].light.transform);
                // Visitor Center: anchor the grid on the room's first lamp by name, so the cell count (and whether the room
                // gets companions at all) does not depend on scene order (the break room flipped between 14 and 15 cells)
                if (zone1) lamps.Sort((u, v) => string.CompareOrdinal(u.light.name, v.light.name));
                Vector3 a = lamps[0].light.transform.position;
                float y = lamps[0].y;
                // Visitor Center: 3.6 x 3.0 m (each companion is a lamp + Bounce + wall fill; at 3.0 x 2.4 m the security
                // wing overflowed HDRP's 24 lights per cluster cell: stair-step patches in opening shot 6)
                float sx = zone1 ? 3.6f : 3.0f, sz = zone1 ? 3.0f : 2.4f;
                const float margin = 0.9f;
                var cells = new List<Vector3>();
                for (int i = -12; i <= 12; i++)
                for (int k = -12; k <= 12; k++)
                {
                    float x = a.x + i * sx, z = a.z + k * sz;
                    if (x < b.min.x + margin || x > b.max.x - margin || z < b.min.z + margin || z > b.max.z - margin) continue;
                    Vector3 c = new Vector3(x, y, z);
                    bool nearLamp = false;
                    foreach (var lp in lamps) if (Vector3.Distance(Flatten(lp.light.transform.position), Flatten(c)) < 1.6f) { nearLamp = true; break; }
                    if (nearLamp) continue;
                    // the slab must really be there at the same height (insets, holes, wall tops)
                    float? cy = CeilingAbove(new Vector3(x, y - 0.25f, z), 0.5f, out Collider hitC);
                    if (!cy.HasValue || hitC != kv.Key || Mathf.Abs(cy.Value - y) > 0.03f) continue;
                    // nothing tall in the way below (a wall top under the ceiling)
                    if (Physics.Raycast(new Vector3(x, y - 0.02f, z), Vector3.down, 0.6f, s_mask, QueryTriggerInteraction.Ignore)) continue;
                    cells.Add(c);
                }
                if (cells.Count > (zone1 ? 18 : 14)) continue;     // a hall, not an office: leave it to the lamp's own fixture
                int idx = 0;
                foreach (Vector3 c in cells)
                {
                    Light nearest = lamps[0].light;
                    float best = float.MaxValue;
                    foreach (var lp in lamps)
                    {
                        float d = Vector3.Distance(Flatten(lp.light.transform.position), Flatten(c));
                        if (d < best) { best = d; nearest = lp.light; }
                    }
                    int gi = idx++;
                    Renderer lens = PanelAt(nearest, c, 1.22f, 0.62f, $"_Grid{gi}");
                    if (CompanionLamp(nearest, lens, c, gi)) built++;
                    else Object.DestroyImmediate(lens.transform.parent.gameObject);
                }
            }
            SplitMains();
            StripZone1SecondaryBounces();
            s_troffers.Clear();
            s_companions.Clear();
            return built;
        }

        private static readonly Dictionary<Light, int> s_companions = new Dictionary<Light, int>();

        /// <summary>
        /// "No fixture without a lamp": every companion troffer gets its own downward lamp (lamp table rule
        /// ^Fixture_Lamp_.*_Grid, small cached shadow so it cannot leak through a wall) in the main's light group,
        /// and its lens is paired with that lamp. The room's light is then shared out by <see cref="SplitMains"/>.
        /// </summary>
        private static bool CompanionLamp(Light main, Renderer lens, Vector3 ceilPoint, int index)
        {
            LightGroupMember mainMember = MemberOf(main);
            if (mainMember == null) return false;
            var go = new GameObject($"{Prefix}Lamp_{main.name}_Grid{index}");
            go.layer = main.gameObject.layer;
            go.transform.SetParent(lens.transform.parent, true);
            go.transform.SetPositionAndRotation(ceilPoint - Vector3.up * (InZone1(main.transform) ? 0.035f : 0.07f), Quaternion.Euler(90f, 0f, 0f));
            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.useColorTemperature = main.useColorTemperature;
            l.colorTemperature = main.colorTemperature;
            l.color = main.color;
            l.spotAngle = main.spotAngle;
            l.innerSpotAngle = main.innerSpotAngle;
            go.AddComponent<HDAdditionalLightData>();
            LookLights.ApplyRuleTo(l);
            // same colour as the room's main (the rule's kelvin is only a default)
            l.useColorTemperature = main.useColorTemperature;
            l.colorTemperature = main.colorTemperature;
            l.color = main.color;
            l.range = main.range;
            NoContactShadows(l);
            var mso = new SerializedObject(mainMember);
            WorldArchitectureKit.AttachLightGroupMember(go, mainMember.GroupId, new[] { l }, new[] { lens },
                0, false, false, mso.FindProperty("blackoutBackupFactor").floatValue);
            s_companions.TryGetValue(main, out int n);
            s_companions[main] = n + 1;
            return true;
        }

        /// <summary>A main with n companion lamps keeps 1/(n+1) of its table candela and each companion gets the
        /// same, so the room keeps its calibrated light output. Uses the table value, so re-runs never divide twice.</summary>
        private static void SplitMains()
        {
            foreach (var kv in s_companions)
            {
                Light main = kv.Key;
                float table = LookLights.RuleCandela(main.name);
                if (table <= 0f) table = main.intensity;
                float each = table / (kv.Value + 1);
                main.intensity = each;
                EditorUtility.SetDirty(main);
                Transform holder = main.transform.parent;
                if (holder == null) continue;
                bool vc = InZone1(main.transform);
                int ci = 0;
                foreach (Light c in holder.GetComponentsInChildren<Light>(true))
                {
                    if (c == null) continue;     // a bounce destroyed by ThinCompanionBounce earlier in this loop
                    if (!c.name.StartsWith($"{Prefix}Lamp_{main.name}_Grid")) continue;
                    c.intensity = each;
                    EditorUtility.SetDirty(c);
                    if (vc) ThinCompanionBounce(c, ci++);
                }
            }
        }

        /// <summary>Visitor Center light budget: every companion troffer carried its own Bounce and Bounce_Fill (3 lights
        /// per fixture, ~57 in the break room) and HDRP's 24-lights-per-cluster-cell list overflowed (stair-step patches in
        /// opening shot 6). Companions drop the wall fill (the room's perimeter washes do that job) and every other one
        /// drops its bounce; the kept bounces carry 1.5x the share (2x put a hot patch on the tiles round each one).</summary>
        private static void ThinCompanionBounce(Light c, int index)
        {
            Transform fill = c.transform.Find("Bounce_Fill");
            if (fill != null) Object.DestroyImmediate(fill.gameObject);
            Transform b = c.transform.Find("Bounce");
            // fix round: every companion drops its bounce (the ceiling fills carry the ceiling; one oval per kept bounce was
            // the review's halo), whatever its shadow mode
            if (b != null) Object.DestroyImmediate(b.gameObject);
        }

        /// <summary>Fix round: the secondary mains of the break room and gift shop (Light_Break_Main_n, Light_GiftShop_n) keep
        /// their cached shadow (range, no light through walls) but lose the bounce and wall fill EnsureBounce gave them: the
        /// even ceiling comes from the ceiling fills, so each would only add an oval on the tiles.</summary>
        private static void StripZone1SecondaryBounces()
        {
            GameObject z1 = GameObject.Find("Zone_01_VisitorCenter");
            if (z1 == null) return;
            foreach (Light l in z1.GetComponentsInChildren<Light>(true))
            {
                if (l == null || !Regex.IsMatch(l.name, "^Light_(GiftShop|Break_Main)_\\d+$")) continue;
                foreach (string child in new[] { "Bounce", "Bounce_Fill" })
                {
                    Transform c = l.transform.Find(child);
                    if (c != null) Object.DestroyImmediate(c.gameObject);
                }
            }
        }

        /// <summary>
        /// A fixture body sits right next to its lamp. Shadow maps ignore it (no part casts shadows), but HDRP's
        /// screen-space contact shadows do not: they march from each lit pixel towards the lamp and hit the body,
        /// cutting a dark disc into the lamp's own pool. Lamps with a body therefore never use contact shadows
        /// (this also frees HDRP's limited contact-shadow slots).
        /// </summary>
        private static void NoContactShadows(Light l)
        {
            HDAdditionalLightData hd = l.GetComponent<HDAdditionalLightData>();
            if (hd == null) return;
            hd.useContactShadow.useOverride = true;
            hd.useContactShadow.@override = false;
            EditorUtility.SetDirty(hd);
        }

        private static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>True for lamps built by the Visitor Center builder (under Zone_01_VisitorCenter): the zone1 package's
        /// fixture choices (enamel, kelvin lenses, grid spacing, battery-head aim) stay inside its own zone.</summary>
        private static bool InZone1(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "Zone_01_VisitorCenter") return true;
            return false;
        }

        /// <summary>Battery emergency unit: wall-mounted twin heads if a wall is near, otherwise a ceiling unit.
        /// A builder-placed beacon body next to the lamp (corridor) is made to glow instead.</summary>
        private static string Emergency(Light l)
        {
            Renderer existing = NearbyBody(l, 0.6f, "beacon|Beacon");
            if (existing != null)
            {
                // round 4: 12 nits (the bible's signal colours are 14-22): at 70 the red lens clipped to pink-white (review 3)
                GlowModel(existing, LensColor(l), EmergencyGlowNits, "_EmergGlow");
                Pair(l, existing);
                return "emergency(beacon glow)";
            }

            Vector3 p = l.transform.position;
            float? ceil = CeilingAbove(p, 2.5f, out _);
            if (NearestWall(p, 1.3f, out RaycastHit wall))
            {
                Vector3 nrm = Flat(wall.normal);
                // Round 3: a Visitor Center room or corridor unit (ceiling under 3.6 m) hangs at exit-sign height, 2.45 m,
                // and throws its 120 deg cone down and out from the wall (60 deg under the horizon): the top edge of the
                // cone is level, so nothing reaches the ceiling junction; the back edge draws a scallop down the wall.
                bool roomBatt = InZone1(l.transform) && Regex.IsMatch(l.name, "_Batt(_\\d+)?$") && ceil.HasValue && ceil.Value < 3.6f;
                if (roomBatt)
                {
                    Vector3 rm = new Vector3(wall.point.x, 2.45f, wall.point.z);
                    rm = FaceAt(rm, nrm);
                    Transform rr = Root(l, rm, Quaternion.LookRotation(nrm, Vector3.up));
                    TwinHeadBody(rr, l, out Renderer ra, out Renderer rb);
                    MoveLight(l, rm + nrm * 0.35f - Vector3.up * 0.1f);
                    Quaternion aimDown = Quaternion.LookRotation(nrm * 0.5f - Vector3.up * 0.866f, nrm);
                    if (Quaternion.Angle(l.transform.rotation, aimDown) > 0.01f) { l.transform.rotation = aimDown; EditorUtility.SetDirty(l.transform); }
                    Pair(l, ra, rb);
                    return "emergency(wall twin-head, aimed down the wall)";
                }
                float y = ceil.HasValue ? Mathf.Min(p.y, ceil.Value - 0.32f) : p.y;
                Vector3 mount = FaceAt(new Vector3(wall.point.x, y, wall.point.z), nrm);
                Transform root = Root(l, mount, Quaternion.LookRotation(nrm, Vector3.up));
                TwinHeadBody(root, l, out Renderer a, out Renderer b);
                // Visitor Center battery units keep their downward 150 deg cone (and its floor bounce) but throw it from
                // 1.2 m out of the wall, where the two heads aim: at 0.2 m the wall under the unit got 400-4000 lux and
                // clipped (review 2, office_dark), against the bible's ~35 lux emergency pool
                bool vcBatt = InZone1(l.transform) && Regex.IsMatch(l.name, "_Batt(_\\d+)?$");
                MoveLight(l, mount + nrm * (vcBatt ? 1.2f : 0.2f) - Vector3.up * 0.06f);
                Pair(l, a, b);
                return "emergency(wall twin-head)";
            }
            float cy = ceil ?? (p.y + 0.1f);
            Transform r2 = Root(l, new Vector3(p.x, cy - 0.12f, p.z), Quaternion.identity);
            TwinHeadBody(r2, l, out Renderer a2, out Renderer b2);
            Part(r2, "Hanger", PrimitiveType.Cube, new Vector3(0f, 0.09f, 0.0f), new Vector3(0.08f, 0.06f, 0.05f), s_p.DarkPaintedMetal);
            MoveLight(l, new Vector3(p.x, cy - 0.3f, p.z));
            Pair(l, a2, b2);
            return "emergency(ceiling twin-head)";
        }

        /// <summary>Red emergency beacon lens luminance (round 4: 70 nits clipped to pink-white at the blackout exposure).</summary>
        private const float EmergencyGlowNits = 12f;
        /// <summary>Visitor Center battery-unit lenses: 7 nits, under the blackout white (~12.7 nits at EV 3.4), so the two heads
        /// read as warm lenses in dark cans and not as two clipped orange discs (review 3 office_dark_exit_wall).</summary>
        private const float BatteryLensNits = 7f;

        private static Material s_battHousing, s_battLed;

        /// <summary>Round 4: the wall face at the unit's own height. The nearest-wall probe runs at the lamp's height, so a unit
        /// lowered onto a pilaster (security office, Pier_1 11 cm proud of the wall) was mounted on the wall behind it, and
        /// only its heads stuck out of the pier.</summary>
        private static Vector3 FaceAt(Vector3 mount, Vector3 nrm)
        {
            if (Physics.Raycast(mount + nrm * 0.6f, -nrm, out RaycastHit h, 0.75f, s_mask, QueryTriggerInteraction.Ignore))
                return new Vector3(h.point.x, mount.y, h.point.z);
            return mount;
        }

        private static void TwinHeadBody(Transform root, Light l, out Renderer lensA, out Renderer lensB)
        {
            // Round 4: a light-grey ABS housing with a dark front band and dark head cans (the off-white plaster box vanished on
            // beige drywall and the unit read as two orange eyes), a 4 cm charge LED and heads aimed well down
            if (s_battHousing == null)
                s_battHousing = s_p.Surface("MAT_Z1_BatteryHousing", "metal_painted_worn", "grey_BaseColor", "MaskMap", 0.8f, new Color(1.30f, 1.32f, 1.36f), 0.30f, 0.55f, 0f, 0.35f);
            if (s_battLed == null)
                s_battLed = s_p.GetOrBuildEmissive("MAT_Z1_BatteryChargeLED", new Color(0.30f, 1.0f, 0.42f), 10f);
            bool vc = InZone1(l.transform);
            Material housing = vc ? s_battHousing : s_p.PlasterClean;
            // local +Z points out of the wall
            Part(root, "BatteryBox", PrimitiveType.Cube, new Vector3(0f, 0f, 0.045f), new Vector3(0.36f, 0.13f, 0.09f), housing);
            Part(root, "FrontBand", PrimitiveType.Cube, new Vector3(0f, -0.038f, 0.0915f), new Vector3(0.362f, 0.04f, 0.004f), s_p.DarkPaintedMetal);
            Part(root, "Grille", PrimitiveType.Cube, new Vector3(-0.04f, 0.025f, 0.091f), new Vector3(0.14f, 0.045f, 0.004f), s_p.DarkPaintedMetal);
            Renderer led = Part(root, "ChargeLED", PrimitiveType.Cube, new Vector3(0.11f, 0.03f, 0.092f), new Vector3(0.04f, 0.014f, 0.006f), vc ? s_battLed : s_p.EmissiveGreen);
            led.name = "ChargeLED";
            Part(root, "TestButton", PrimitiveType.Cylinder, new Vector3(0.06f, 0.03f, 0.092f), new Vector3(0.016f, 0.003f, 0.016f), s_p.DarkPaintedMetal, Quaternion.Euler(90f, 0f, 0f));
            Renderer[] lenses = new Renderer[2];
            Material lensMat = vc ? BatteryLens(l) : LensFor(l);
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? -0.13f : 0.13f;
                Quaternion aim = Quaternion.Euler(vc ? 58f : 35f, s == 0 ? -20f : 20f, 0f);    // heads look out and down
                Transform head = new GameObject($"Head_{s}").transform;
                head.SetParent(root, false);
                head.localPosition = new Vector3(x, -0.075f, 0.1f);
                head.localRotation = aim;
                head.gameObject.layer = GameLayers.Environment;
                Part(head, "Swivel", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), s_p.DarkPaintedMetal);
                Part(head, "Can", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.035f), new Vector3(0.075f, 0.035f, 0.075f), vc ? s_p.DarkPaintedMetal : s_p.PlasterClean, Quaternion.Euler(90f, 0f, 0f));
                if (vc) Part(head, "Bezel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.069f), new Vector3(0.08f, 0.004f, 0.08f), s_p.BrushedSteel, Quaternion.Euler(90f, 0f, 0f));
                lenses[s] = Part(head, "Lens", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.072f), new Vector3(0.064f, 0.004f, 0.064f), lensMat, Quaternion.Euler(90f, 0f, 0f));
            }
            lensA = lenses[0];
            lensB = lenses[1];
            if (vc) BatterySpill(root, l);
        }

        private static Material BatteryLens(Light l)
        {
            int k = Mathf.RoundToInt(Mathf.Clamp(l.useColorTemperature ? l.colorTemperature : 3300f, 1800f, 9000f) / 100f) * 100;
            return CustomLens($"MAT_Z1_BattLens_K{k}", l.useColorTemperature ? LensColor(l) : new Color(1f, 0.8f, 0.6f), BatteryLensNits);
        }

        /// <summary>Round 4: the faint light of the two heads on their own housing and the wall round it, so the unit's body
        /// reads in the blackout (the box sat above its own cone and got no light). A tiny shadowless point 28 cm in front of
        /// the box (lamp table rule "^BattSpill$"), following its unit on and off (LampBounce).</summary>
        private static void BatterySpill(Transform root, Light l)
        {
            var go = new GameObject("BattSpill");
            go.layer = l.gameObject.layer;
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, -0.12f, 0.28f);   // 0.55 cd: LampBounce turns a follower under 0.5 cd off
            Light spill = go.AddComponent<Light>();
            spill.type = LightType.Point;
            go.AddComponent<HDAdditionalLightData>();
            LookLights.ApplyRuleTo(spill);
            spill.cullingMask = l.cullingMask;
            spill.renderingLayerMask = l.renderingLayerMask;
            HDAdditionalLightData hd = go.GetComponent<HDAdditionalLightData>();
            hd.affectSpecular = false;
            var follow = go.AddComponent<Cretacea.Rendering.LampBounce>();
            follow.Bind(l, spill, spill.intensity / Mathf.Max(0.01f, l.intensity));
            spill.enabled = l.enabled;
        }

        /// <summary>Lobby / warehouse high-bay: drop rod to the ceiling or truss, bell shade, glowing lens.</summary>
        private static string HighBay(Light l)
        {
            Vector3 p = l.transform.position;
            float top = CeilingAbove(p, 3f, out _) ?? (p.y + 0.6f);
            Transform root = Root(l, p, Quaternion.identity);
            float shadeBottom = 0.04f;
            Part(root, "Bell", PrimitiveType.Cylinder, new Vector3(0f, shadeBottom + 0.16f, 0f), new Vector3(0.62f, 0.16f, 0.62f), s_p.DarkPaintedMetal);
            Part(root, "Shoulder", PrimitiveType.Cylinder, new Vector3(0f, shadeBottom + 0.36f, 0f), new Vector3(0.42f, 0.05f, 0.42f), s_p.DarkPaintedMetal);
            Part(root, "Driver", PrimitiveType.Cylinder, new Vector3(0f, shadeBottom + 0.47f, 0f), new Vector3(0.24f, 0.07f, 0.24f), s_p.BrushedSteel);
            Renderer lens = Part(root, "Lens", PrimitiveType.Cylinder, new Vector3(0f, shadeBottom + 0.005f, 0f), new Vector3(0.54f, 0.005f, 0.54f), LensFor(l));
            float rodBottom = p.y + shadeBottom + 0.54f;
            if (top - rodBottom > 0.05f)
                Part(root, "Rod", PrimitiveType.Cylinder, new Vector3(0f, (rodBottom + top) * 0.5f - p.y, 0f), new Vector3(0.03f, (top - rodBottom) * 0.5f, 0.03f), s_p.DarkPaintedMetal);
            Pair(l, lens);
            return "high-bay";
        }

        /// <summary>Storage closet: bare bulb in a socket on a cord from the ceiling.</summary>
        private static string Bulb(Light l)
        {
            Vector3 p = l.transform.position;
            float ceilY = CeilingAbove(p, 2f, out _) ?? (p.y + 0.4f);
            float bulbY = Mathf.Min(p.y, ceilY - 0.45f);
            Transform root = Root(l, new Vector3(p.x, bulbY, p.z), Quaternion.identity);
            Renderer bulb = Part(root, "Bulb", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.07f, 0.085f, 0.07f), LensFor(l));
            Part(root, "Socket", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.04f, 0.03f, 0.04f), s_p.DarkPaintedMetal);
            float cordLen = ceilY - (bulbY + 0.1f);
            Part(root, "Cord", PrimitiveType.Cylinder, new Vector3(0f, 0.1f + cordLen * 0.5f, 0f), new Vector3(0.008f, cordLen * 0.5f, 0.008f), s_p.DarkPaintedMetal);
            Part(root, "Rose", PrimitiveType.Cylinder, new Vector3(0f, ceilY - bulbY - 0.01f, 0f), new Vector3(0.09f, 0.01f, 0.09f), s_p.PlasterClean);
            MoveLight(l, new Vector3(p.x, bulbY - 0.06f, p.z));
            Pair(l, bulb);
            return "bulb";
        }

        /// <summary>Fossil uplights: a floor can on the plinth with the lamp just above its lens.</summary>
        private static string FloorCan(Light l)
        {
            Vector3 p = l.transform.position;
            float floorY = p.y - 0.15f;
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, 1.5f, s_mask, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            float h = Mathf.Max(0.06f, p.y - 0.015f - floorY);
            Transform root = Root(l, new Vector3(p.x, floorY, p.z), Quaternion.identity);
            Part(root, "Can", PrimitiveType.Cylinder, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.2f, h * 0.5f, 0.2f), s_p.DarkPaintedMetal);
            Part(root, "Bezel", PrimitiveType.Cylinder, new Vector3(0f, h, 0f), new Vector3(0.22f, 0.008f, 0.22f), s_p.BrushedSteel);
            Renderer lens = Part(root, "Lens", PrimitiveType.Cylinder, new Vector3(0f, h + 0.009f, 0f), new Vector3(0.15f, 0.003f, 0.15f), LensFor(l));
            Pair(l, lens);
            return "floor can";
        }

        /// <summary>Path lamps: the lamp_post FBX head gets a glowing lens and the light moves under the head.</summary>
        private static string PathLamp(Light l)
        {
            Renderer post = NearbyBody(l, 2.2f, "^lamp_post$", horizontalOnly: true);
            if (post == null) return Beacon(l);
            // head underside in the FBX's mesh space (measured from the mesh: arm out along +Z)
            Transform mt = post.transform;
            // Round 4: the housing spans x -0.31..0.46, z 0.20..1.04 and its underside rises ~5.7 deg towards the front
            // (measured from the mesh); a flat 0.30 x 0.48 lens at the back's height hid in the lip at the back and hung out
            // at the front (review 3 DuskTransition_plaza_eye: a terracotta rectangle offset in its housing). The lens now
            // fills the housing's opening and follows its slope.
            Vector3 head = mt.TransformPoint(new Vector3(0.08f, 3.574f, 0.64f));
            Transform root = Root(l, head, Quaternion.Euler(0f, mt.eulerAngles.y, 0f));
            Renderer lens = Part(root, "Lens", PrimitiveType.Cube, new Vector3(0f, -0.003f, 0f), new Vector3(0.54f, 0.012f, 0.60f), LensFor(l), Quaternion.Euler(-5.7f, 0f, 0f));
            MoveLight(l, head - Vector3.up * 0.09f);
            Pair(l, lens);
            return "lamp post head";
        }

        /// <summary>Bollards: a glowing collar near the top (plaza warm, AuxRoute green); the AuxRoute cube cap
        /// becomes a domed lens.</summary>
        private static string BollardCollar(Light l)
        {
            Renderer bollard = NearbyBody(l, 1.2f, "bollard|Bollard", horizontalOnly: true);
            if (bollard == null) return Beacon(l);
            Bounds b = bollard.bounds;
            float width = Mathf.Min(b.size.x, b.size.z);
            float y = b.min.y + b.size.y * 0.80f;
            Transform root = Root(l, new Vector3(b.center.x, y, b.center.z), Quaternion.identity);
            Renderer collar = Part(root, "Collar", PrimitiveType.Cylinder, Vector3.zero, new Vector3(width * 1.03f, 0.05f, width * 1.03f), LensFor(l));
            var lenses = new List<Renderer> { collar };

            Renderer cap = NearbyBody(l, 0.8f, "GreenCap|Cap_", horizontalOnly: true);
            if (cap != null)
            {
                cap.enabled = false;
                EditorUtility.SetDirty(cap);
                RemoveEmissive(l, cap);
                float topY = b.max.y;
                Part(root, "Bezel", PrimitiveType.Cylinder, new Vector3(0f, topY - y + 0.01f, 0f), new Vector3(width * 0.8f, 0.01f, width * 0.8f), s_p.DarkPaintedMetal);
                lenses.Add(Part(root, "Dome", PrimitiveType.Sphere, new Vector3(0f, topY - y + 0.03f, 0f), new Vector3(width * 0.62f, 0.09f, width * 0.62f), LensFor(l)));
            }
            MoveLight(l, new Vector3(b.center.x, y, b.center.z));
            Pair(l, lenses.ToArray());
            return "bollard collar";
        }

        /// <summary>Substation bulkheads: the lamp_bulkhead FBX glows (its pale glass) with the lamp.</summary>
        private static string Bulkhead(Light l)
        {
            Renderer body = NearbyBody(l, 0.6f, "lamp_bulkhead");
            if (body == null) return Beacon(l);
            GlowModel(body, LensColor(l), DiffuserNits);   // like every other lens: 60 nits bloomed
            Pair(l, body);
            return "bulkhead glow";
        }

        /// <summary>Floods: a flood head (housing, yoke, lens) on a crossarm from the bare mast, the lamp in the lens.</summary>
        private static string Flood(Light l)
        {
            Vector3 p = l.transform.position;
            Vector3 fwd = l.transform.forward;
            Vector3 fh = Flat(fwd);
            if (fh.sqrMagnitude < 0.01f) fh = Vector3.forward;
            Renderer mast = FindMast(p);
            Vector3 mount;          // point on the mast surface facing the beam
            bool wallMount = false;
            if (mast != null)
            {
                Bounds mb = mast.bounds;
                Vector3 axis = new Vector3(mb.center.x, p.y, mb.center.z);
                float half = Mathf.Min(mb.extents.x, mb.extents.z);
                float along = Vector3.Dot(p - axis, fh);
                mount = axis + fh * Mathf.Max(half, Mathf.Min(along, half + 0.05f));
                if (along > half + 0.6f) mount = p - fh * 0.45f;        // lamp already well clear of the mast
            }
            else if (WallBelow(p, 2.5f, 1.5f, out RaycastHit wallHit, out float wallY))
            {
                p.y = wallY;
                // no mast: a wall bracket from the nearest wall, the head aimed away from it
                Vector3 wn = Flat(wallHit.normal);                  // out of the wall; the head keeps its aim
                if (wn.sqrMagnitude > 0.5f) fh = wn;
                mount = new Vector3(wallHit.point.x, p.y, wallHit.point.z);
                wallMount = true;
            }
            else mount = p - fh * 0.45f;

            Vector3 headPos = mount + fh * 0.5f;
            Transform root = Root(l, headPos, Quaternion.LookRotation(fwd, Vector3.up));
            bool signal = l.name.Contains("Green");
            float w = signal ? 0.42f : 0.56f, h = signal ? 0.30f : 0.42f;
            Part(root, "Housing", PrimitiveType.Cube, new Vector3(0f, 0f, -0.13f), new Vector3(w, h, 0.22f), s_p.DarkPaintedMetal);
            Part(root, "Fins", PrimitiveType.Cube, new Vector3(0f, 0f, -0.26f), new Vector3(w * 0.85f, h * 0.8f, 0.05f), s_p.BrushedSteel);
            Part(root, "Visor", PrimitiveType.Cube, new Vector3(0f, h * 0.5f + 0.01f, 0.02f), new Vector3(w + 0.02f, 0.018f, 0.12f), s_p.DarkPaintedMetal);
            Renderer lens = Part(root, "Lens", PrimitiveType.Cube, new Vector3(0f, 0f, -0.015f), new Vector3(w - 0.06f, h - 0.06f, 0.012f), LensFor(l));
            Part(root, "Yoke_L", PrimitiveType.Cube, new Vector3(-w * 0.5f - 0.025f, 0f, -0.13f), new Vector3(0.03f, h * 0.7f, 0.1f), s_p.DarkPaintedMetal);
            Part(root, "Yoke_R", PrimitiveType.Cube, new Vector3(w * 0.5f + 0.025f, 0f, -0.13f), new Vector3(0.03f, h * 0.7f, 0.1f), s_p.DarkPaintedMetal);

            // crossarm (world space): from the mast surface to the back of the head, slightly above it
            Vector3 back = headPos - fwd * 0.3f;
            Vector3 armA = new Vector3(mount.x, back.y + h * 0.35f, mount.z) - fh * 0.04f;
            Vector3 armB = new Vector3(back.x, back.y + h * 0.35f, back.z);
            Beam(root, "Crossarm", armA, armB, 0.08f, s_p.DarkPaintedMetal);
            Beam(root, "Drop", armB, back, 0.05f, s_p.DarkPaintedMetal);
            if (wallMount)
                Beam(root, "WallPlate", armA + fh * 0.04f, armA + fh * 0.055f, 0.22f, s_p.DarkPaintedMetal);
            else if (mast == null && Physics.Raycast(back, Vector3.down, out RaycastHit ground, 15f, s_mask, QueryTriggerInteraction.Ignore))
                Beam(root, "Post", ground.point, armA + Vector3.up * 0.05f, 0.1f, s_p.DarkPaintedMetal);
            if (mast != null) Beam(root, "MastCap", new Vector3(mast.bounds.center.x, mast.bounds.max.y, mast.bounds.center.z), new Vector3(mast.bounds.center.x, mast.bounds.max.y + 0.03f, mast.bounds.center.z), Mathf.Max(mast.bounds.size.x, mast.bounds.size.z) * 1.15f, s_p.DarkPaintedMetal);

            MoveLight(l, headPos + fwd * 0.02f);
            Pair(l, lens);
            return signal ? "signal head" : "flood head";
        }

        /// <summary>Workshop pendants: the builder's flat shade becomes an enamel bell with a glowing lens.</summary>
        private static string WorkshopPendant(Light l)
        {
            Vector3 p = l.transform.position;
            Renderer shade = NearbyBody(l, 0.5f, "PendantShade");
            float shadeBottom = shade != null ? shade.bounds.min.y : p.y + 0.04f;
            if (shade != null) { shade.enabled = false; EditorUtility.SetDirty(shade); }
            Transform root = Root(l, new Vector3(p.x, shadeBottom, p.z), Quaternion.identity);
            Part(root, "Bell", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.46f, 0.07f, 0.46f), s_p.PaintedMetalGreen);
            Part(root, "Crown", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f), new Vector3(0.2f, 0.04f, 0.2f), s_p.PaintedMetalGreen);
            Renderer lens = Part(root, "Lens", PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 0f), new Vector3(0.38f, 0.004f, 0.38f), LensFor(l));
            MoveLight(l, new Vector3(p.x, shadeBottom - 0.04f, p.z));
            Pair(l, lens);
            return "workshop pendant";
        }

        /// <summary>Security desk task light: a green enamel pendant on a cord over the CCTV desk.</summary>
        private static string DeskPendant(Light l)
        {
            Vector3 p = l.transform.position;
            float ceilY = CeilingAbove(p, 2.5f, out _) ?? (p.y + 1f);
            Transform root = Root(l, p + Vector3.up * 0.05f, Quaternion.identity);
            Part(root, "Shade", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.34f, 0.08f, 0.34f), s_p.PaintedMetalGreen);
            Part(root, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.17f, 0f), new Vector3(0.16f, 0.08f, 0.16f), s_p.PaintedMetalGreen);
            Renderer lens = Part(root, "Lens", PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 0f), new Vector3(0.28f, 0.004f, 0.28f), LensFor(l));
            float cordBottom = p.y + 0.27f;
            if (ceilY - cordBottom > 0.05f)
                Part(root, "Cord", PrimitiveType.Cylinder, new Vector3(0f, (cordBottom + ceilY) * 0.5f - p.y - 0.05f, 0f), new Vector3(0.01f, (ceilY - cordBottom) * 0.5f, 0.01f), s_p.DarkPaintedMetal);
            Pair(l, lens);
            return "desk pendant";
        }

        /// <summary>Lobby grazers and banner accents, facade uplights: a small spot can on a yoke, aimed with its lamp, on a
        /// bracket to the nearest wall (or a stalk to the floor). The lamp sits in the can's lens.</summary>
        private static string SpotHead(Light l)
        {
            Vector3 p = l.transform.position;
            Quaternion aim = l.transform.rotation;
            Transform root = Root(l, p, aim);
            Part(root, "Can", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.11f), new Vector3(0.16f, 0.11f, 0.16f), s_p.DarkPaintedMetal, Quaternion.Euler(90f, 0f, 0f));
            Part(root, "Bezel", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.002f), new Vector3(0.175f, 0.008f, 0.175f), s_p.BrushedSteel, Quaternion.Euler(90f, 0f, 0f));
            Renderer lens = Part(root, "Lens", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.004f), new Vector3(0.13f, 0.003f, 0.13f), LensFor(l), Quaternion.Euler(90f, 0f, 0f));
            Part(root, "Yoke", PrimitiveType.Cube, new Vector3(0f, -0.1f, -0.11f), new Vector3(0.2f, 0.02f, 0.05f), s_p.DarkPaintedMetal);
            Vector3 basePos = root.TransformPoint(new Vector3(0f, -0.1f, -0.11f));
            float? roof = CeilingAbove(root.TransformPoint(new Vector3(0f, 0f, -0.11f)), 1.6f, out _);
            if (roof.HasValue)
            {
                // hung from the roof truss or ceiling on a drop rod
                Vector3 top = root.TransformPoint(new Vector3(0f, 0f, -0.11f));
                Beam(root, "DropRod", new Vector3(top.x, roof.Value, top.z), top, 0.03f, s_p.DarkPaintedMetal);
            }
            else if (NearestWall(basePos, 1.2f, out RaycastHit wall))
            {
                Vector3 w = new Vector3(wall.point.x, basePos.y, wall.point.z);
                Beam(root, "Bracket", w, basePos, 0.04f, s_p.DarkPaintedMetal);
                Beam(root, "WallPlate", w, w + Flat(wall.normal) * 0.012f, 0.12f, s_p.DarkPaintedMetal);
            }
            else Support(root, basePos, 1.2f);
            MoveLight(l, p + l.transform.forward * 0.02f);
            Pair(l, lens);
            return "spot head";
        }

        /// <summary>Mezzanine soffit downlight: a round recessed can with a glowing lens flush under the slab.</summary>
        private static string Downlight(Light l)
        {
            Vector3 p = l.transform.position;
            float ceilY = CeilingAbove(p, 1.2f, out _) ?? (p.y + 0.05f);
            Transform root = Root(l, new Vector3(p.x, ceilY, p.z), Quaternion.identity);
            Part(root, "Trim", PrimitiveType.Cylinder, new Vector3(0f, -0.006f, 0f), new Vector3(0.24f, 0.006f, 0.24f), Enamel());
            Renderer lens = Part(root, "Lens", PrimitiveType.Cylinder, new Vector3(0f, -0.013f, 0f), new Vector3(0.17f, 0.002f, 0.17f), LensFor(l));
            MoveLight(l, new Vector3(p.x, ceilY - 0.05f, p.z));
            Pair(l, lens);
            return "downlight";
        }

        /// <summary>Records: a portable amber beacon standing on the desk, the lamp just above it.</summary>
        private static string DeskBeacon(Light l)
        {
            Vector3 p = l.transform.position;
            if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 2.5f, s_mask, QueryTriggerInteraction.Ignore)) return Beacon(l);
            Transform root = Root(l, hit.point, Quaternion.identity);
            Renderer dome = DomeBody(root, l, 0f);
            MoveLight(l, hit.point + Vector3.up * 0.26f);
            Pair(l, dome);
            return "desk beacon";
        }

        /// <summary>Strobes and beacons: beacon_red FBX for red, a coloured dome for amber/green, on a stalk down to
        /// what is below, or a bracket to the nearest wall/post.</summary>
        private static string Beacon(Light l)
        {
            Vector3 p = l.transform.position;
            Color c = LensColor(l);
            bool red = c.r > 0.8f && c.g < 0.3f;
            float baseY = p.y - (red ? 0.15f : 0.1f);
            Transform root = Root(l, new Vector3(p.x, baseY, p.z), Quaternion.identity);
            Renderer lens;
            if (red && Model("beacon_red") is GameObject fbx)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root);
                inst.name = "BeaconRed";
                inst.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GameLayers.Environment;
                lens = inst.GetComponentInChildren<MeshRenderer>();
                lens.shadowCastingMode = ShadowCastingMode.Off;
                GlowModel(lens, c, 70f);
                Part(root, "Plinth", PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.01f, 0.3f), s_p.DarkPaintedMetal);
            }
            else
            {
                lens = DomeBody(root, l, 0f);
            }
            Support(root, new Vector3(p.x, baseY, p.z), 1.4f);
            Pair(l, lens);
            return red ? "beacon (red)" : "beacon (dome)";
        }

        private static Renderer DomeBody(Transform root, Light l, float y)
        {
            Part(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, y + 0.035f, 0f), new Vector3(0.17f, 0.035f, 0.17f), s_p.DarkPaintedMetal);
            Part(root, "Collar", PrimitiveType.Cylinder, new Vector3(0f, y + 0.075f, 0f), new Vector3(0.15f, 0.008f, 0.15f), s_p.BrushedSteel);
            Renderer dome = Part(root, "Dome", PrimitiveType.Capsule, new Vector3(0f, y + 0.13f, 0f), new Vector3(0.13f, 0.08f, 0.13f), LensFor(l));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                Part(root, $"CageBar_{i}", PrimitiveType.Cube, Quaternion.Euler(0f, a, 0f) * new Vector3(0.072f, 0f, 0f) + new Vector3(0f, y + 0.135f, 0f), new Vector3(0.008f, 0.13f, 0.008f), s_p.DarkPaintedMetal);
            }
            return dome;
        }

        /// <summary>Something to stand on: a stalk down to the surface below (up to <paramref name="maxDrop"/>), else a
        /// bracket arm to the nearest wall or post.</summary>
        private static void Support(Transform root, Vector3 basePos, float maxDrop)
        {
            if (Physics.Raycast(basePos + Vector3.up * 0.02f, Vector3.down, out RaycastHit down, maxDrop, s_mask, QueryTriggerInteraction.Ignore))
            {
                if (down.distance > 0.06f) Beam(root, "Stalk", down.point, basePos, 0.045f, s_p.DarkPaintedMetal);
                return;
            }
            if (NearestWall(basePos, 1.6f, out RaycastHit wall))
            {
                Vector3 w = new Vector3(wall.point.x, basePos.y - 0.02f, wall.point.z);
                Beam(root, "Bracket", w, basePos - Vector3.up * 0.02f, 0.05f, s_p.DarkPaintedMetal);
                Beam(root, "WallPlate", w - Flat(wall.normal) * 0.0f, w + Flat(wall.normal) * 0.015f, 0.14f, s_p.DarkPaintedMetal);
                return;
            }
            if (Physics.Raycast(basePos, Vector3.down, out RaycastHit ground, 12f, s_mask, QueryTriggerInteraction.Ignore))
                Beam(root, "Post", ground.point, basePos, 0.08f, s_p.DarkPaintedMetal);
        }

        // ============================================================================================================
        // helpers
        // ============================================================================================================
        private static Transform Root(Light l, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(Prefix + l.name);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(l.transform.parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go.transform;
        }

        private static Renderer Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat, Quaternion? localRot = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.staticShadowCaster = false;
            return mr;
        }

        /// <summary>A square-section bar between two world points.</summary>
        private static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.005f) return;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, true);
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(d / len, Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up);
            go.transform.localScale = Vector3.one;
            Vector3 lossy = go.transform.parent != null ? go.transform.parent.lossyScale : Vector3.one;
            go.transform.localScale = new Vector3(thickness / lossy.x, thickness / lossy.y, len / lossy.z);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void MoveLight(Light l, Vector3 pos)
        {
            if ((l.transform.position - pos).sqrMagnitude < 1e-6f) return;
            l.transform.position = pos;
            EditorUtility.SetDirty(l.transform);
        }

        private static float? CeilingAbove(Vector3 p, float max, out Collider hitCollider)
        {
            hitCollider = null;
            if (Physics.Raycast(p - Vector3.up * 0.3f, Vector3.up, out RaycastHit hit, max + 0.3f, s_mask, QueryTriggerInteraction.Ignore))
            {
                hitCollider = hit.collider;
                return hit.point.y;
            }
            return null;
        }

        /// <summary>A wall (never a door leaf or an interactable) within <paramref name="max"/> metres, searched at the
        /// point's height and up to <paramref name="maxDrop"/> metres lower (a lamp above a low wall or lintel).</summary>
        private static bool WallBelow(Vector3 p, float max, float maxDrop, out RaycastHit hit, out float y)
        {
            int mask = s_mask & ~((1 << GameLayers.Door) | (1 << GameLayers.Interactable));
            for (float dy = 0f; dy <= maxDrop + 1e-3f; dy += 0.3f)
            {
                y = p.y - dy;
                if (NearestWall(new Vector3(p.x, y, p.z), max, out hit, mask)) return true;
            }
            hit = default;
            y = p.y;
            return false;
        }

        private static bool NearestWall(Vector3 p, float max, out RaycastHit best, int mask = 0)
        {
            if (mask == 0) mask = s_mask;
            best = default;
            float bestD = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (Physics.Raycast(p, d, out RaycastHit hit, max, mask, QueryTriggerInteraction.Ignore)
                    && Mathf.Abs(hit.normal.y) < 0.3f && hit.distance < bestD)
                {
                    bestD = hit.distance;
                    best = hit;
                }
            }
            return bestD < float.MaxValue;
        }

        private static Renderer NearbyBody(Light l, float radius, string namePattern, bool horizontalOnly = false)
        {
            var rx = new Regex(namePattern);
            Vector3 p = l.transform.position;
            Renderer best = null;
            float bestD = float.MaxValue;
            foreach (MeshRenderer r in l.transform.parent != null ? l.transform.parent.GetComponentsInChildren<MeshRenderer>(true) : new MeshRenderer[0])
            {
                if (r.name.StartsWith(Prefix) || r.transform.parent != null && r.transform.parent.name.StartsWith(Prefix)) continue;
                if (!rx.IsMatch(r.name)) continue;
                Bounds b = r.bounds;
                float d;
                if (horizontalOnly)
                {
                    d = new Vector2(b.center.x - p.x, b.center.z - p.z).magnitude;
                    if (p.y < b.min.y - 1f || p.y > b.max.y + 1.5f) continue;
                }
                else d = Mathf.Sqrt(b.SqrDistance(p));
                if (d <= radius && d < bestD) { bestD = d; best = r; }
            }
            return best;
        }

        private static Renderer FindMast(Vector3 p)
        {
            Renderer best = null;
            float bestD = 2.5f;
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r.gameObject.layer == GameLayers.RainBlocker || r.name.StartsWith(Prefix)) continue;
                Bounds b = r.bounds;
                if (b.size.y < 2f || b.size.x > 1.6f || b.size.z > 1.6f) continue;
                if (b.max.y < p.y - 0.6f || b.min.y > p.y) continue;
                float d = new Vector2(b.center.x - p.x, b.center.z - p.z).magnitude;
                if (d < bestD) { bestD = d; best = r; }
            }
            return best;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        private static GameObject Model(string stem) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Props/{stem}/{stem}.fbx");

        // ---- lenses -----------------------------------------------------------------------------------------------

        private static Color LensColor(Light l)
        {
            // CorrelatedColorTemperatureToRGB is linear (HDRP multiplies it into the light as is); the callers treat
            // the result as an sRGB colour and linearise it, so hand them the sRGB encoding of the lamp's colour
            if (l.useColorTemperature)
                return Mathf.CorrelatedColorTemperatureToRGB(Mathf.Clamp(l.colorTemperature, 1500f, 20000f)).gamma;
            return l.color;
        }

        /// <summary>
        /// Lens luminance. The Look Bible's palette diffusers are 170 nits, but with the current night bloom (threshold 0,
        /// scatter 0.68, intensity 0.2) any lens far above the night scene white (~19 nits at EV 4) blooms into a soft
        /// blob that hides its frame and louvres. Measured in Play mode (security office, lockers): 60 nits = blob, 40 =
        /// blob, 25 = frame and louvre grid read; the lead's proposed bloom (threshold 0.9, scatter 0.5, intensity 0.1)
        /// alone still blurs a 60-nit lens. One constant to raise if the bloom is retuned. Coloured lenses keep the
        /// palette (45-70 nits): they are small and colour-limited.
        /// </summary>
        public const float DiffuserNits = 25f;
        /// <summary>Kelvin-matched white lenses: 18 nits (the 25-nit lens clipped 81-99 % of its pixels with a halo).</summary>
        private const float LensNits = 18f;

        private static Material s_enamel;
        /// <summary>White stove enamel for troffer frames and louvres (dielectric, albedo ~0.75): reads pale in the
        /// flashlight when the lamp is dead, instead of black.</summary>
        private static Material Enamel()
        {
            if (s_enamel != null) return s_enamel;
            // the same material as the Visitor Center builder's fixture enamel (one asset, Zone 1 only)
            s_enamel = s_p.Surface("MAT_Z1_FixtureEnamel", "drywall_paint", "BaseColor", "MaskMap", 1.0f, new Color(1.42f, 1.52f, 1.66f), 0.45f, 0.62f, 0f, 0.2f);
            return s_enamel;
        }

        private static Material LensFor(Light l)
        {
            if (l.useColorTemperature && InZone1(l.transform))
            {
                // Visitor Center: the lens is the lamp's own colour (review 1: a fixed 8000 K tint gave cyan halos round 4000 K troffers),
                // one material per 100 K, at a luminance whose louvres survive the bloom
                int k = Mathf.RoundToInt(Mathf.Clamp(l.colorTemperature, 1800f, 9000f) / 100f) * 100;
                return CustomLens($"MAT_Z1_Lens_K{k}", LensColor(l), LensNits);
            }
            if (l.useColorTemperature)
            {
                // other zones keep the shared lenses (their owners calibrated them)
                float kk = l.colorTemperature;
                if (kk >= 4000f) return CustomLens("MAT_Lens_CoolWhite", new Color(0.86f, 0.93f, 1.0f), DiffuserNits);
                if (kk < 2800f) return CustomLens("MAT_Lens_Tungsten", new Color(1.0f, 0.72f, 0.45f), DiffuserNits);
                return CustomLens("MAT_Lens_WarmWhite", new Color(1.0f, 0.86f, 0.66f), DiffuserNits);
            }
            Color c = l.color;
            if (c.r > 0.8f && c.g < 0.3f) return s_p.EmissiveRed;
            if (c.r > 0.8f && c.g < 0.75f) return s_p.EmissiveAmber;
            if (c.g > 0.8f && c.r < 0.5f) return s_p.EmissiveGreen;
            if (c.b > 0.8f && c.r < 0.5f) return CustomLens("MAT_Lens_Blue", new Color(0.36f, 0.66f, 1.0f), 24f);
            return CustomLens("MAT_Lens_WarmWhite", new Color(1.0f, 0.86f, 0.66f), DiffuserNits);
        }

        private static Material CustomLens(string name, Color c, float nits)
        {
            if (s_lens.TryGetValue(name, out Material m) && m != null) return m;
            m = s_p.GetOrBuildEmissive(name, c, nits);
            if (name != "MAT_Lens_Blue")
            {
                // an opal diffuser is near-white when its lamp is off (blackout ceilings in the flashlight beam),
                // not the palette's 25 % tint; the emission is unchanged
                m.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.82f, 1f));
                EditorUtility.SetDirty(m);
            }
            s_lens[name] = m;
            return m;
        }

        /// <summary>An imported fixture model glows with its lamp: a copy of its material whose base colour is also
        /// its emission map (pale glass and domes glow, dark metal stays dark).</summary>
        private static void GlowModel(Renderer r, Color c, float nits, string variant = "_Glow")
        {
            // variant: the copy's suffix. Round 4: the red emergency heads use their own copy ("_EmergGlow"): they share the
            // beacon_red model with the zone 2-3 beacons, and one shared "_Glow" asset took whichever luminance was set last.
            Material src = r.sharedMaterial;
            if (src == null) return;
            string baseName = src.name;
            foreach (string v in new[] { "_EmergGlow", "_Glow" })
                if (baseName.EndsWith(v)) { baseName = baseName.Substring(0, baseName.Length - v.Length); break; }
            string folder = "Assets/Art/Materials/World/Fixtures";
            LookSigns.EnsureFolder(folder);
            string path = $"{folder}/{baseName}{variant}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (src.name == baseName + variant) { r.sharedMaterial = m ?? src; return; }
            Material plain = src.name == baseName ? src : AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(src).Replace(src.name + ".mat", baseName + ".mat"));
            if (plain == null) plain = src;
            if (m == null)
            {
                m = new Material(plain) { name = baseName + variant };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(plain);
            m.name = baseName + variant;
            src = plain;
            Texture baseMap = src.HasProperty("_BaseColorMap") ? src.GetTexture("_BaseColorMap") : null;
            m.SetTexture("_EmissiveColorMap", baseMap);
            Color e = c.linear * nits;
            m.SetColor("_EmissiveColor", e);
            m.SetColor("_EmissiveColorLDR", c);
            m.SetFloat("_EmissiveIntensity", nits);
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetFloat("_EmissiveExposureWeight", 1f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            r.sharedMaterial = m;
            EditorUtility.SetDirty(r);
        }

        // ---- light group pairing -----------------------------------------------------------------------------------

        private static LightGroupMember MemberOf(Light l) => l.GetComponent<LightGroupMember>() ?? l.GetComponentInParent<LightGroupMember>();

        private static void Pair(Light l, params Renderer[] lenses)
        {
            LightGroupMember m = MemberOf(l);
            if (m == null) return;
            var so = new SerializedObject(m);
            SerializedProperty ep = so.FindProperty("emissiveRenderers");
            foreach (Renderer r in lenses)
            {
                if (r == null) continue;
                bool present = false;
                for (int i = 0; i < ep.arraySize; i++) if (ep.GetArrayElementAtIndex(i).objectReferenceValue == r) { present = true; break; }
                if (present) continue;
                ep.arraySize++;
                ep.GetArrayElementAtIndex(ep.arraySize - 1).objectReferenceValue = r;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveEmissive(Light l, Renderer r)
        {
            LightGroupMember m = MemberOf(l);
            if (m == null) return;
            var so = new SerializedObject(m);
            SerializedProperty ep = so.FindProperty("emissiveRenderers");
            for (int i = ep.arraySize - 1; i >= 0; i--)
                if (ep.GetArrayElementAtIndex(i).objectReferenceValue == r) DeleteAt(ep, i);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PruneEmissives(LightGroupMember m)
        {
            var so = new SerializedObject(m);
            SerializedProperty ep = so.FindProperty("emissiveRenderers");
            bool changed = false;
            for (int i = ep.arraySize - 1; i >= 0; i--)
            {
                Object o = ep.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o == null) { DeleteAt(ep, i); changed = true; }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DeleteAt(SerializedProperty array, int i)
        {
            int before = array.arraySize;
            array.DeleteArrayElementAtIndex(i);
            if (array.arraySize == before) array.DeleteArrayElementAtIndex(i);
        }

        // ============================================================================================================
        // STATIC FLAGS
        // ============================================================================================================
        private static readonly HashSet<string> StaticSafeBehaviours = new HashSet<string>
        {
            "SurfaceTag", "LightGroupMember", "InteriorZoneVolume", "WorldAnchor", "ReflectionProbeRefresh",
        };

        /// <summary>Kit geometry that never moves gets BatchingStatic + OccludeeStatic (+ static shadow caster via the
        /// lights step). Doors, interactables, animated/physics objects, scripted objects and anything whose
        /// material is driven by a light group (emissive MPB) stay dynamic.</summary>
        private static string MarkStatic()
        {
            GameObject world = GameObject.Find("World");
            var driven = new HashSet<Renderer>();
            foreach (LightGroupMember m in world.GetComponentsInChildren<LightGroupMember>(true))
            {
                var so = new SerializedObject(m);
                SerializedProperty ep = so.FindProperty("emissiveRenderers");
                for (int i = 0; i < ep.arraySize; i++) if (ep.GetArrayElementAtIndex(i).objectReferenceValue is Renderer r) driven.Add(r);
            }
            int flagged = 0, dynamicCount = 0;
            foreach (MeshRenderer mr in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObject go = mr.gameObject;
                if (go.layer == GameLayers.RainBlocker) continue;
                if (driven.Contains(mr) || Moves(mr.transform))
                {
                    StaticEditorFlags f = GameObjectUtility.GetStaticEditorFlags(go);
                    StaticEditorFlags nf = f & ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
                    if (nf != f) GameObjectUtility.SetStaticEditorFlags(go, nf);
                    dynamicCount++;
                    continue;
                }
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                StaticEditorFlags want = flags | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                if (want != flags) GameObjectUtility.SetStaticEditorFlags(go, want);
                flagged++;
            }
            return $"static flags: static={flagged} dynamic={dynamicCount}";
        }

        private static bool Moves(Transform t)
        {
            int layer = t.gameObject.layer;
            if (layer == GameLayers.Door || layer == GameLayers.Interactable || layer == GameLayers.Dinosaur || layer == GameLayers.Player) return true;
            for (Transform a = t; a != null; a = a.parent)
            {
                if (a.GetComponent<Animator>() != null || a.GetComponent<Rigidbody>() != null || a.GetComponent<ParticleSystem>() != null) return true;
                foreach (MonoBehaviour mb in a.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string ns = mb.GetType().Namespace ?? "";
                    if (!ns.StartsWith("Cretacea")) continue;
                    if (!StaticSafeBehaviours.Contains(mb.GetType().Name)) return true;
                }
            }
            return false;
        }
    }
}
