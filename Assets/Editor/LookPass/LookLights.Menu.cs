using UnityEngine;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Lamp rules for the main menu scene. Checked BEFORE the core table in LookLights.cs, so a rule here can refine a core rule.
    /// One line per lamp family: name regex, candela, kelvin (0 = keep colour / use filter), range, volumetric dimmer,
    /// shadow (None/Low/Medium/High), spot angle (0 = keep), colour filter, throw (Throw.Down = ceiling fixture).
    /// </summary>
    public static partial class LookLights
    {
        private static readonly Rule[] MenuRules =
        {
            // Main menu atrium: the fossil is the hero, storm light rakes in through the skylight
            new Rule("^Menu_Uplight_SkullKey",     5200f, 3000f, 16f, 0.80f, Shadow.Medium, 46f),
            new Rule("^Menu_Uplight_RibsWarm",     3600f, 2800f, 16f, 0.80f, Shadow.Medium, 64f),
            new Rule("^Menu_SkylightStormRim",     2600f, 0f,    30f, 1.00f, Shadow.Medium, 0f, new Color(0.52f, 0.72f, 1.0f)),
            new Rule("^Menu_StormExteriorRim",     1800f, 0f,    34f, 1.00f, Shadow.None, 0f, new Color(0.46f, 0.66f, 1.0f)),
        };
    }
}
