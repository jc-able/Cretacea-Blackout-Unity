using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// One table for every lamp in the game. The zone builders place the lights; this pass decides how bright,
    /// what colour, how far and whether they cast shadows, so the whole game can be balanced in one place.
    ///
    /// Units: candela (Unity 6 stores point/spot intensity in candela whatever the inspector unit says).
    /// Reference: the night presets expose automatically between about EV 3 (dark storm outside) and EV 6 (a lit
    /// room). Mid-grey on a 50 % surface needs about 11 lux at EV 3, 22 at EV 4, 43 at EV 5, 87 at EV 6;
    /// lux = candela / distance^2. A 600 cd ceiling tube gives ~60 lux on the floor 3 m below: a normally lit
    /// corridor. A 60 cd emergency lamp gives ~15 lux at 2 m: a red pool, not a red flood.
    /// </summary>
    public static partial class LookLights
    {
        private enum Shadow { None, Low, Medium, High }

        /// <summary>How the lamp throws its light. A bare point light under a ceiling blasts the ceiling (inverse
        /// square at 20 cm) and fools the auto exposure; real fixtures throw downwards.</summary>
        private enum Throw { Keep, Down }

        private readonly struct Rule
        {
            public readonly Regex rx;
            public readonly float candela;
            public readonly float kelvin;        // 0 = keep the builder's colour (coloured signal lamps)
            public readonly float range;
            public readonly float volumetric;
            public readonly Shadow shadow;
            public readonly float spotAngle;     // 0 = keep
            public readonly Color? filter;
            public readonly Throw throwMode;

            public Rule(string pattern, float candela, float kelvin, float range, float volumetric, Shadow shadow,
                float spotAngle = 0f, Color? filter = null, Throw throwMode = Throw.Keep)
            {
                rx = new Regex(pattern, RegexOptions.Compiled);
                this.candela = candela; this.kelvin = kelvin; this.range = range; this.volumetric = volumetric;
                this.shadow = shadow; this.spotAngle = spotAngle; this.filter = filter; this.throwMode = throwMode;
            }
        }

        // Properties, not fields: the area tables live in other files of this partial class, and C# leaves the order of
        // static field initialisers between files open. A table that ran first read (0,0,0,0) for its colour (round 4).
        private static Color Red => new Color(1.0f, 0.10f, 0.06f);
        private static Color Amber => new Color(1.0f, 0.55f, 0.12f);
        private static Color Green => new Color(0.36f, 1.0f, 0.64f);      // light, not paint
        private static Color CoolBlue => new Color(0.36f, 0.66f, 1.0f);
        private const float Wide = 164f;   // downward cone of a ceiling fixture

        // First match wins. Order from specific to general.
        // The night world is balanced around EV 4 (the night presets expose between about 3.3 and 5.0) and the
        // game's dark surfaces (wood, carpet, wet concrete: about 20 % reflectance):
        //   lit room walls ~110 lux, lamp pools outside ~130 lux, emergency pools ~35 lux, moonlight ~8 lux.
        private static readonly Rule[] Rules =
        {
            // ---- Visitor Center, mains (on in "Night", off in the blackout) ------------------------------------
            new Rule("^Light_Corr_Main",             1150f, 4300f, 11f, 0.40f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Security_Main",         1500f, 3500f, 12f, 0.30f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Security_DeskTask",      420f, 2900f, 5f,  0.25f, Shadow.Low, 72f),
            new Rule("^Light_Control_Main",          1700f, 4500f, 15f, 0.35f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Break_Main",            1500f, 3600f, 14f, 0.35f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_GiftShop",              1700f, 3400f, 16f, 0.35f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Lockers_Main",          1400f, 4300f, 11f, 0.35f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Records_Main",          1400f, 3200f, 13f, 0.40f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Storage_Bulb",           420f, 2700f, 8f,  0.45f, Shadow.Low),
            new Rule("^Light_Lobby_Main",            9500f, 3600f, 30f, 0.45f, Shadow.High, 118f),
            new Rule("^Light_Lobby_SkylightDayFill",    0f, 0f,    30f, 0f,    Shadow.None),      // daylight comes from the sky now
            new Rule("^Light_FossilUplight",         4200f, 3100f, 15f, 0.60f, Shadow.Medium, 62f),
            new Rule("^Light_Dock_Main",             2600f, 3500f, 20f, 0.55f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Dock_StormBacklight",    900f, 0f,    17f, 0.90f, Shadow.Medium, 0f, CoolBlue),
            new Rule("^Light_Server_Blue",            420f, 0f,    9f,  0.45f, Shadow.Low, 0f, CoolBlue),
            new Rule("^Light_Cold_Blue",              420f, 0f,    11f, 0.60f, Shadow.Low, 0f, CoolBlue),
            new Rule("^Light_Records_ManualBeacon",   260f, 0f,    4.5f, 0.45f, Shadow.Low, 0f, Amber),      // the desk must block it

            // ---- emergency lighting (on in the blackout): pools, not floods -----------------------------------
            new Rule("^Light_Corr_Emerg",             120f, 0f,    7f,   0.60f, Shadow.Low, 118f, Red, Throw.Down),
            new Rule("^Light_Lobby_Emerg",            620f, 0f,    20f,  0.80f, Shadow.Medium, 130f, Red, Throw.Down),
            new Rule("Emerg",                         150f, 0f,    8f,   0.65f, Shadow.Low, 120f, Red, Throw.Down),

            // ---- outdoors --------------------------------------------------------------------------------------
            new Rule("^Light_PlazaBollard",           360f, 2700f, 9f,   0.65f, Shadow.None),
            new Rule("^Light_PathLamp_3$",           2300f, 4800f, 17f,  0.90f, Shadow.Medium, 100f),   // the one cool lamp on the route
            new Rule("^Light_PathLamp",              2500f, 2450f, 17f,  0.90f, Shadow.Medium, 100f),
            new Rule("^Light_Kiosk_Amber",            420f, 0f,    9f,   0.85f, Shadow.Low, 0f, Amber),
            new Rule("^Light_ServiceGate_Beacon",     520f, 0f,    11f,  0.95f, Shadow.None, 0f, Amber),
            new Rule("^Light_JeepHazard",             260f, 0f,    8f,   0.45f, Shadow.None, 0f, Amber),
            new Rule("^Light_JeepRadioGlow",           24f, 0f,    2.4f, 0.20f, Shadow.None, 0f, Green),
            new Rule("^Light_Talon_Flood",          18000f, 5600f, 50f,  1.00f, Shadow.High, 80f),
            new Rule("^Light_Talon_Strobe",           700f, 0f,    20f,  0.90f, Shadow.None, 0f, Red),
            new Rule("^Light_WatchtowerStrobe",       900f, 0f,    36f,  1.10f, Shadow.None, 0f, Red),

            // ---- Substation 3 ----------------------------------------------------------------------------------
            new Rule("^Light_AuxSubFlood",          14000f, 3300f, 48f,  1.00f, Shadow.High, 88f),
            new Rule("^Light_SubBulkhead",            520f, 2400f, 11f,  0.85f, Shadow.Low),
            new Rule("^StatusLight$",                  16f, 0f,    2.8f, 0.30f, Shadow.None),

            // ---- AuxRoute + Old Maintenance Yard ----------------------------------------------------------------
            new Rule("^Light_AuxRouteGreen_Overlook", 800f, 0f,    14f,  1.20f, Shadow.None, 0f, Green),   // the goal light at the coast
            new Rule("^Light_AuxRouteGreen",          170f, 0f,    9f,   0.50f, Shadow.None, 0f, Green),
            new Rule("^Light_WorkshopPendant",       1250f, 2800f, 10f,  0.85f, Shadow.Medium, 104f),
            new Rule("^Light_WH_Aux",                8500f, 3300f, 34f,  0.95f, Shadow.High, Wide, null, Throw.Down),
            new Rule("^Light_CourtAuxFlood",         4200f, 3300f, 34f,  0.85f, Shadow.High, 88f),
            new Rule("^Light_E0Gate_GreenOverhead",   900f, 0f,    17f,  0.85f, Shadow.Medium, 86f, Green),
            new Rule("^Light_E0ControlBox_Beacon",    420f, 0f,    9f,   0.75f, Shadow.None, 0f, Amber),

            // ---- small things ----------------------------------------------------------------------------------
            new Rule("^ChargingLedLight$",              6f, 0f,    0.9f, 0.20f, Shadow.None, 0f, Amber),
        };

        /// <summary>Core table first, then the per-area tables (LookLights.&lt;Area&gt;.cs), so each area owns one file.</summary>
        private static IEnumerable<Rule> AllRules()
        {
            // zones 2-3 first: their rules are anchored by name, zone 1 has a catch-all "_Batt" rule
            foreach (Rule r in Zone23Rules) yield return r;
            foreach (Rule r in Zone1Rules) yield return r;
            foreach (Rule r in KitRules) yield return r;
            foreach (Rule r in MenuRules) yield return r;
            foreach (Rule r in ValleyRules) yield return r;     // the valley builder applies them too; listed so look_pass knows them
            foreach (Rule r in Rules) yield return r;
        }

        public static string Calibrate()
        {
            Rule[] rules = System.Linq.Enumerable.ToArray(AllRules());
            int matched = 0, unmatched = 0;
            var misses = new List<string>();
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional) continue;
                if (l.name.StartsWith("Flashlight")) continue;          // owned by FlashlightController
                if (l.name.StartsWith("Light_Evac")) continue;           // distant boat, dawn only
                if (l.name.StartsWith(BounceName)) continue;             // made here (bounce, wall fill), follows its lamp
                bool hit = false;
                for (int i = 0; i < rules.Length; i++)
                {
                    if (!rules[i].rx.IsMatch(l.name)) continue;
                    Apply(l, rules[i]);
                    hit = true;
                    break;
                }
                if (hit) matched++;
                else { unmatched++; if (misses.Count < 12) misses.Add(l.name); }
            }
            return $"lights calibrated={matched} unmatched={unmatched}" + (misses.Count > 0 ? " [" + string.Join(", ", misses) + "]" : "");
        }

        private static void Apply(Light l, Rule r)
        {
            Undo.RecordObject(l, "Look lights");
            l.lightUnit = LightUnit.Candela;
            l.intensity = r.candela;
            // Indoor battery units: pools of 60-100/255 at the blackout exposure, clearly under the flashlight's own
            // pool (review 2: at 340 cd they matched it and the beam was not the brightest thing in the room).
            if (l.name.Contains("_Batt") && r.throwMode == Throw.Down && r.candela <= 400f) l.intensity = r.candela * BatteryIndoorScale;
            // A grid companion (ceiling lamp without shadows) only has to reach the floor and the nearest wall. With
            // 9-12 m each, 30+ lights overlapped in one office and the tile light lists overflowed (blocky patches
            // on the wall seen through the window in opening shot 6).
            l.range = (r.throwMode == Throw.Down && r.shadow == Shadow.None) ? Mathf.Min(r.range, 7f) : r.range;
            if (r.kelvin > 0f)
            {
                l.useColorTemperature = true;
                l.colorTemperature = r.kelvin;
                l.color = Color.white;
            }
            else if (r.filter.HasValue)
            {
                l.useColorTemperature = false;
                l.color = r.filter.Value;
            }
            if (r.throwMode == Throw.Down && l.type != LightType.Directional)
            {
                // ceiling fixture: a wide downward cone instead of a bare omni point
                Undo.RecordObject(l.transform, "Look lights");
                l.type = LightType.Spot;
                l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (r.spotAngle > 0f && l.type == LightType.Spot)
            {
                l.spotAngle = r.spotAngle;
                l.innerSpotAngle = r.spotAngle * (r.throwMode == Throw.Down ? 0.94f : 0.45f);
            }
            if (r.candela <= 0.001f) l.enabled = false;

            HDAdditionalLightData hd = l.GetComponent<HDAdditionalLightData>();
            if (hd == null) hd = l.gameObject.AddComponent<HDAdditionalLightData>();
            Undo.RecordObject(hd, "Look lights");
            hd.affectsVolumetric = r.volumetric > 0.01f;
            hd.volumetricDimmer = r.volumetric;
            hd.fadeDistance = 220f;
            hd.volumetricFadeDistance = 90f;

            bool rotating = HasRotatingMember(l);
            if (r.shadow == Shadow.None || rotating)
            {
                l.shadows = LightShadows.None;
            }
            else
            {
                l.shadows = LightShadows.Soft;
                int res = r.shadow == Shadow.High ? 1024 : (r.shadow == Shadow.Medium ? 512 : 256);
                // a very wide ceiling cone spreads its map thin: the shadow edge in a far corner came out as stair steps
                if (r.throwMode == Throw.Down && r.spotAngle > 140f && r.shadow != Shadow.Low) res = Mathf.Max(res, 1024);
                hd.shadowResolution.useOverride = true;
                hd.shadowResolution.@override = res;
                // Static world is rendered into the cached atlas once; doors, dinosaurs and the player are added live.
                hd.shadowUpdateMode = ShadowUpdateMode.OnEnable;
                hd.alwaysDrawDynamicShadows = false;         // LampShadowBudget turns it on for the lamps near the viewer
                hd.shadowFadeDistance = 70f;
                hd.shadowNearPlane = 0.12f;
                hd.useContactShadow.useOverride = true;
                hd.useContactShadow.@override = r.shadow != Shadow.Low;
                hd.shadowDimmer = 1f;
            }
            EditorUtility.SetDirty(l);
            EditorUtility.SetDirty(hd);

            // Signal-coloured lamps (red emergency heads) get no bounce: a red bounce floods the whole room, and the
            // rule is small red pools, never a room in one saturated colour.
            // Only a room's main lamp (the one that casts shadows) carries the bounce; its companions in a troffer grid
            // share it. One bounce per lamp gave 112 extra lights for no visible gain.
            // Battery units (the blackout's only room lights) are islands: with a floor bounce and a wall fill each,
            // a room without power was lit as evenly as one with power (review 2).
            if (r.throwMode == Throw.Down && l.type == LightType.Spot)
                EnsureBounce(l, r, wanted: !r.filter.HasValue && r.shadow != Shadow.None && !l.name.Contains("_Batt"));
        }

        // ---- ceiling fixtures: one diffuse bounce each (see Cretacea.Rendering.LampBounce) -------------------------
        /// <summary>
        /// Candela of the bounce per candela of the lamp. Review 2 measured ceilings at a tenth of the wall light at
        /// 0.45 (charcoal lids over lit rooms); a white drop ceiling over a pale floor gets back nearly all of it.
        /// </summary>
        public const float BounceShare = 0.95f;
        private const float BounceDepth = 1.4f;      // metres under the floor: a soft, wide source and no hot spot
        private const string BounceName = "Bounce";

        private static void EnsureBounce(Light l, Rule r, bool wanted = true)
        {
            Transform existing = l.transform.Find(BounceName);
            if (r.candela <= 0.001f || !wanted)
            {
                Transform oldFill = l.transform.Find(WallFillName);
                if (oldFill != null) Undo.DestroyObjectImmediate(oldFill.gameObject);
                if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
                return;
            }

            // the floor (or the table) under the fixture
            float drop = 3.0f;
            int mask = Cretacea.Core.GameLayers.EnvironmentAndDoorMask;
            Vector3 from = l.transform.position + Vector3.down * 0.25f;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 14f, mask, QueryTriggerInteraction.Ignore))
            {
                drop = hit.distance + 0.25f;
            }
            drop = Mathf.Clamp(drop, 1.6f, 12f);

            GameObject go = existing != null ? existing.gameObject : new GameObject(BounceName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Look lights");
                go.transform.SetParent(l.transform, false);
            }
            go.layer = l.gameObject.layer;
            go.transform.position = l.transform.position + Vector3.down * (drop + BounceDepth);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);      // straight up

            Light b = go.GetComponent<Light>();
            if (b == null) b = go.AddComponent<Light>();
            HDAdditionalLightData bhd = go.GetComponent<HDAdditionalLightData>();
            if (bhd == null) bhd = go.AddComponent<HDAdditionalLightData>();

            float reach = drop + BounceDepth;
            // A wide cone from under the floor: the ceiling above, and the walls from knee height up (a wall 3 m away
            // gets about half of what the ceiling gets, more low down: what a lit floor really gives back).
            b.type = LightType.Spot;
            b.lightUnit = LightUnit.Candela;
            b.spotAngle = 150f;
            b.innerSpotAngle = 105f;
            b.range = reach + 1.5f;          // short: every extra metre of range puts the light into more screen tiles
            b.intensity = r.candela * BounceShare;
            b.useColorTemperature = l.useColorTemperature;
            b.colorTemperature = l.colorTemperature;
            b.color = l.color;
            b.shadows = LightShadows.None;
            b.cullingMask = l.cullingMask;
            b.renderingLayerMask = l.renderingLayerMask;
            bhd.affectsVolumetric = false;
            bhd.volumetricDimmer = 0f;
            bhd.affectSpecular = false;                                   // a bounce has no highlight of its own
            bhd.fadeDistance = 70f;
            bhd.lightDimmer = 1f;

            var follower = go.GetComponent<Cretacea.Rendering.LampBounce>();
            if (follower == null) follower = go.AddComponent<Cretacea.Rendering.LampBounce>();
            follower.Bind(l, b, BounceShare);
            b.enabled = l.enabled;
            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(b);
            EditorUtility.SetDirty(bhd);
            EditorUtility.SetDirty(follower);

            EnsureWallFill(l, r);
        }

        /// <summary>
        /// A downward cone stops lighting a wall about half a metre under the ceiling, and the bounce from under the
        /// floor reaches the top of a wall only at a grazing angle: review 1 saw a black band under every lit ceiling.
        /// A small shadowless point light a metre under the fixture fills that band (a real diffuser throws light
        /// sideways too).
        /// </summary>
        public const float WallFillShare = 0.11f;
        public const float BatteryIndoorScale = 0.65f;
        private const string WallFillName = "Bounce_Fill";

        private static void EnsureWallFill(Light l, Rule r)
        {
            Transform existing = l.transform.Find(WallFillName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(WallFillName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Look lights");
                go.transform.SetParent(l.transform, false);
            }
            go.layer = l.gameObject.layer;
            go.transform.position = l.transform.position + Vector3.down * 1.05f;
            go.transform.rotation = Quaternion.identity;

            Light f = go.GetComponent<Light>();
            if (f == null) f = go.AddComponent<Light>();
            HDAdditionalLightData fhd = go.GetComponent<HDAdditionalLightData>();
            if (fhd == null) fhd = go.AddComponent<HDAdditionalLightData>();
            f.type = LightType.Point;
            f.lightUnit = LightUnit.Candela;
            f.range = Mathf.Clamp(r.range * 0.6f, 4f, 6.5f);
            f.intensity = r.candela * WallFillShare;
            f.useColorTemperature = l.useColorTemperature;
            f.colorTemperature = l.colorTemperature;
            f.color = l.color;
            f.shadows = LightShadows.None;
            f.cullingMask = l.cullingMask;
            f.renderingLayerMask = l.renderingLayerMask;
            fhd.affectsVolumetric = false;
            fhd.volumetricDimmer = 0f;
            fhd.affectSpecular = false;
            fhd.fadeDistance = 60f;
            fhd.lightDimmer = 1f;

            var follower = go.GetComponent<Cretacea.Rendering.LampBounce>();
            if (follower == null) follower = go.AddComponent<Cretacea.Rendering.LampBounce>();
            follower.Bind(l, f, WallFillShare);
            f.enabled = l.enabled;
            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(f);
            EditorUtility.SetDirty(fhd);
            EditorUtility.SetDirty(follower);
        }

        private static bool HasRotatingMember(Light l)
        {
            var member = l.GetComponentInParent<Cretacea.Core.LightGroupMember>();
            if (member == null) return false;
            var so = new SerializedObject(member);
            SerializedProperty p = so.FindProperty("rotatingBeacon");
            return p != null && p.boolValue;
        }

        /// <summary>
        /// Cached shadow maps only hold renderers flagged as static shadow casters. Everything under World/ that does
        /// not move is flagged; doors, dinosaurs, pickups and anything animated stay dynamic.
        /// </summary>
        public static string MarkStaticShadowCasters()
        {
            GameObject world = GameObject.Find("World");
            if (world == null) return "static casters: no World root";
            int flagged = 0, dynamic = 0;
            foreach (MeshRenderer mr in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool moves = mr.GetComponentInParent<Animator>() != null
                             || mr.GetComponentInParent<Rigidbody>() != null
                             || mr.GetComponentInParent<Cretacea.Interact.DoorController>() != null
                             || mr.gameObject.layer == Cretacea.Core.GameLayers.Interactable
                             || mr.gameObject.layer == Cretacea.Core.GameLayers.Door;
                if (mr.staticShadowCaster != !moves)
                {
                    mr.staticShadowCaster = !moves;
                    EditorUtility.SetDirty(mr);
                }
                if (moves) dynamic++; else flagged++;
            }
            return $"static shadow casters={flagged} dynamic={dynamic}";
        }
    }
}
