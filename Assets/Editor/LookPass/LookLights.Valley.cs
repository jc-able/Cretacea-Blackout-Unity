using UnityEngine;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Lamp rules for Tyrant Valley's battery-backed night dressing (ValleyNightDressing: obstruction mast at
    /// Substation 3, amber flashers at the stream bridge). Signal lamps only: no shadows, small ranges, a little
    /// volumetric glow so the beacon reads as a light in the rain from the valley entry (~120 m).
    /// The lights are named StatusLight_Valley* so the fixtures step leaves them alone (ValleyNightDressing builds
    /// their own heads with 18-nit lenses); ValleyNightDressing applies this table itself through ApplyValleyRules.
    /// </summary>
    public static partial class LookLights
    {
        private static readonly Rule[] ValleyRules =
        {
            // aviation obstruction lamps (steady red) on the 23 m mast: a small halo in the haze 120 m away (the 0.3 m lens
            // alone is under two pixels there), nothing reaches the ground from 12 and 23 m up
            new Rule("^StatusLight_Valley_Obstruction",       900f, 0f,    16f,  1.20f, Shadow.None, 0f, new Color(1.0f, 0.12f, 0.07f)),
            // battery road flashers at the bridge (soft pulse from LightGroupMember): a small amber pool on the deck ends
            new Rule("^StatusLight_Valley_BridgeFlasher",     120f, 0f,    6.5f, 0.35f, Shadow.None, 0f, new Color(1.0f, 0.55f, 0.12f)),
        };

        /// <summary>Calibrate the valley lights under <paramref name="root"/> with <see cref="ValleyRules"/>.</summary>
        internal static int ApplyValleyRules(Transform root)
        {
            int n = 0;
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                foreach (Rule r in ValleyRules)
                {
                    if (!r.rx.IsMatch(l.name)) continue;
                    Apply(l, r);
                    n++;
                    break;
                }
            }
            return n;
        }
    }
}
