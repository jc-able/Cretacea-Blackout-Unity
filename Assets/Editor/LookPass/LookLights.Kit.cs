using UnityEngine;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Lamp rules for kit-level fixtures. Checked BEFORE the core table in LookLights.cs, so a rule here can refine a core rule.
    /// One line per lamp family: name regex, candela, kelvin (0 = keep colour / use filter), range, volumetric dimmer,
    /// shadow (None/Low/Medium/High), spot angle (0 = keep), colour filter, throw (Throw.Down = ceiling fixture).
    /// </summary>
    public static partial class LookLights
    {
        private static readonly Rule[] KitRules =
        {
            // ---- companion troffers (LookFixtures.TrofferGrid): each companion panel is a real lamp. LookFixtures
            // splits the room main's candela evenly over the main and its companions after calibration; this rule
            // gives the rest (small cached shadow so a lamp near a wall does not leak into the next room, low volumetric) if the lights step runs on its own.
            new Rule("^Fixture_Lamp_.*_Grid\\d+$",    400f, 3600f, 11f, 0.10f, Shadow.Low, Wide, null, Throw.Down),

            // ---- interior mains: the same lamps as the core table, but with a low volumetric dimmer. Since the
            // fixtures step put each lamp 4-7 cm under its own diffuser, the in-room fog lit by a lamp that close
            // turned every lit fixture into a large glowing halo that hid its frame and louvres.
            new Rule("^Light_Security_Main",         1500f, 3500f, 12f, 0.10f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Control_Main",          1700f, 4500f, 15f, 0.10f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Break_Main",            1500f, 3600f, 14f, 0.10f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_GiftShop",              1700f, 3400f, 16f, 0.12f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Lockers_Main",          1400f, 4300f, 11f, 0.10f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Records_Main",          1400f, 3200f, 13f, 0.10f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_Corr_Main",             1150f, 4300f, 11f, 0.15f, Shadow.Medium, Wide, null, Throw.Down),
            new Rule("^Light_WorkshopPendant",       1250f, 2800f, 10f, 0.30f, Shadow.Medium, 104f),

            // ---- server / cold room: the lamp sits 7 cm under its 0.6 m ceiling panel now, so it throws down like
            // every other ceiling fixture (a bare point light there burns a hot spot into the ceiling around the panel)
            new Rule("^Light_Server_Blue",            420f, 0f,    9f,  0.30f, Shadow.Low, Wide, new Color(0.36f, 0.66f, 1.0f), Throw.Down),
            new Rule("^Light_Cold_Blue",              420f, 0f,    11f, 0.40f, Shadow.Low, Wide, new Color(0.36f, 0.66f, 1.0f), Throw.Down),
        };

        /// <summary>Candela the lamp table gives a light of this name (first matching rule, kit rules included),
        /// or -1 when no rule matches. Used by LookFixtures to split a room main over its companion lamps without
        /// depending on the light's current (possibly already split) intensity.</summary>
        internal static float RuleCandela(string lightName)
        {
            foreach (Rule r in AllRules())
                if (r.rx.IsMatch(lightName)) return r.candela;
            return -1f;
        }

        /// <summary>Calibrate one light with the lamp table (used for lamps the fixtures step creates after the
        /// lights step ran). Returns false when no rule matches.</summary>
        internal static bool ApplyRuleTo(Light l)
        {
            foreach (Rule r in AllRules())
            {
                if (!r.rx.IsMatch(l.name)) continue;
                Apply(l, r);
                return true;
            }
            return false;
        }
    }
}
