using UnityEngine;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Lamp rules for Zones 2 and 3 (service paths, Talon Ridge, Substation 3, Old Yard). Checked BEFORE the core table in LookLights.cs, so a rule here can refine a core rule.
    /// One line per lamp family: name regex, candela, kelvin (0 = keep colour / use filter), range, volumetric dimmer,
    /// shadow (None/Low/Medium/High), spot angle (0 = keep), colour filter, throw (Throw.Down = ceiling fixture).
    /// </summary>
    public static partial class LookLights
    {
        private static readonly Rule[] Zone23Rules =
        {
            // battery bulkheads at the dead-black gaps (alive in the blackout). Mounted 2.75-3.55 m up, 0.25 m off the
            // wall, pitched 50-60 deg down with an 80 deg cone (the cone stays off its own wall): the axis meets the
            // ground 3.2-4.6 m out, i.e. ~35-60 lux in the pool centre (Look Bible: emergency pools 35, lamps 130).
            new Rule("^Light_Paths\\w*Bulkhead_Batt$",       1000f, 2600f, 14f,  0.80f, Shadow.Low,  80f),
            new Rule("^Light_YardGapBulkhead_Batt$",          800f, 2600f, 14f,  0.80f, Shadow.Low,  80f),
            new Rule("^Light_.*Bulkhead_Batt$",               560f, 2600f, 12f,  0.75f, Shadow.Low,  80f),
            // Talon Ridge: extra cold floods on the fence masts (the pen reads as a lit cage from the Control Room)
            new Rule("^Light_Talon_FloodFence",             16000f, 6200f, 55f,  1.00f, Shadow.Low, 80f),
            // Old Yard drive lamps on lamp_posts (AuxYard): sodium-warm like the path lamps, slightly dimmer
            new Rule("^Light_PathLamp_Yard",                 2200f, 2300f, 17f,  0.85f, Shadow.Low, 100f),
            // AuxRoute bollards (review 1: pure-green pools on the gravel read as radioactive paint): fewer candela and
            // a slightly cyan, desaturated green so the pool reads as light; the collar lens stays the palette green
            new Rule("^Light_AuxRouteGreen",                  110f, 0f,    7.5f, 0.45f, Shadow.None, 0f, new Color(0.42f, 1.0f, 0.70f)),
            // E-0: the green overhead keeps the gate the brightest thing in the court, a little less saturated
            new Rule("^Light_E0Gate_GreenOverhead",           900f, 0f,    17f,  0.85f, Shadow.Medium, 86f, new Color(0.38f, 1.0f, 0.62f)),
            // E-0 control-box beacon: a small amber accent, not an orange wash over the right fifth of the frame
            new Rule("^Light_E0ControlBox_Beacon",            190f, 0f,    5f,   0.50f, Shadow.None, 0f, new Color(1.0f, 0.55f, 0.12f)),
            // coast road (Zone 3 dressing): low warm road lamps on the run to the overlook, and the evac flare at the rail
            new Rule("^Light_PathLamp_Coast",                1600f, 2400f, 15f,  0.85f, Shadow.None, 100f),
            // (the evac flare is Light_Evac*: LookLights.Calibrate skips that prefix, so its builder values apply)
            // substation floods: cool white like every flood (Bible 3: 4300-5600 K), visible cones (was the core 3300 K rule)
            // round 3: renamed from Light_AuxSubFlood (the zone builder makes the twin LED heads); volumetric 1.8 -> 0.9 so
            // the beam reads as a cone in the rain instead of a grey halo over the upper frame (review 2, 21)
            // two lamps per mast, one in each LED head: the right-hand one draws no shadow (and gets no bounce light)
            // fix round: the B lamps cast a low-res shadow too (with none they leaked under the cabinet and transformers)
            new Rule("^Light_SubMastFloodB",                 7000f, 5000f, 48f,  0.90f, Shadow.Low,  88f),
            new Rule("^Light_SubMastFlood",                  7000f, 5000f, 48f,  0.90f, Shadow.High, 88f),
            // round 3 (review 2, 16): the service-gate beacon threw a soft amber orb with no source behind the trunk
            new Rule("^Light_ServiceGate_Beacon",              520f, 0f,    11f,  0.40f, Shadow.None, 0f, new Color(1.0f, 0.55f, 0.12f)),
            // substation post lamps (renamed from Light_SubBulkhead: their own frosted lens, the housing stays dark)
            // fix round: a 150 deg spot aimed out and 20 deg down from the lantern (as a point light 0.22 m from the
            // galvanised pole it lit the pole into a glowing tube, ~10,000 lux on its face)
            new Rule("^Light_SubPostLamp",                    520f, 2400f, 11f,  0.85f, Shadow.Low, 150f),
            // fix round: the coast navigation lantern (was Light_EvacNavBeacon_Coast, outside the table). A 12 deg white
            // beam (volumetric 3: at 26000 cd / 0.8 the shaft did not show in the storm haze) that sweeps over the sea at
            // 30 deg/s; built after Calibrate, so OpenCoastRoad applies this
            // rule itself through ApplyRuleTo
            new Rule("^Light_CoastNavLantern",              60000f, 0f,  120f,  3.00f, Shadow.None, 12f),
        };

    }
}
