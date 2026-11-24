using System;
using System.Text.RegularExpressions;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.UI
{
    /// <summary>
    /// Player-facing objective copy and where each objective is in the world. The story code raises its own
    /// objective strings; the HUD and the field tablet show them through <see cref="Display"/>, which turns each
    /// known objective into one short line that names the place, and <see cref="Target"/> gives the map beacon.
    /// Unknown strings pass through unchanged, so a new objective never disappears.
    /// </summary>
    public static class ObjectiveGuide
    {
        public readonly struct Entry
        {
            public readonly string Match;      // case-insensitive substring of the story string
            public readonly string Copy;       // what the player reads ("{0}" = the n/3 counter when there is one)
            public readonly Vector2 Target;    // world XZ of the beacon
            public readonly string Place;      // short place name for the map card

            public Entry(string match, string copy, Vector2 target, string place)
            {
                Match = match; Copy = copy; Target = target; Place = place;
            }
        }

        // World XZ of the places objectives send the player to (from the zone builders' anchors).
        public static readonly Vector2 SecurityDesk = new Vector2(-62.5f, 25.6f);
        public static readonly Vector2 SecurityTerminal = new Vector2(-61f, 25.6f);
        public static readonly Vector2 ControlRoom = new Vector2(-37f, 25.6f);
        public static readonly Vector2 LobbyEntrance = new Vector2(-31f, -29.5f);
        public static readonly Vector2 StaffExit = new Vector2(-70f, -10f);
        public static readonly Vector2 RecordsAnnex = new Vector2(-4f, 25.6f);
        public static readonly Vector2 LoadingDock = new Vector2(15f, -13.5f);   // south half of the dock: the label sits north
        public static readonly Vector2 ServiceGate = new Vector2(90.5f, -3f);
        public static readonly Vector2 Substation = new Vector2(266f, 0f);
        public static readonly Vector2 YardGap = new Vector2(401f, -22.8f);
        public static readonly Vector2 ExitE0 = new Vector2(505f, -46f);
        public static readonly Vector2 Coast = new Vector2(543f, -46f);

        private static readonly Entry[] Entries =
        {
            new Entry("tactical flashlight", "Take your flashlight from the desk in the Security Office.", SecurityDesk, "SECURITY OFFICE"),
            new Entry("clock in", "Clock in at the terminal in the Security Office.", SecurityTerminal, "SECURITY OFFICE"),
            new Entry("log the paddock feeds", "Log the paddock feeds in the Control Room, down the Staff Corridor past the Lockers (east).", ControlRoom, "CONTROL ROOM"),
            new Entry("main entrance", "Get to the main entrance doors, straight across the Lobby (south end).", LobbyEntrance, "LOBBY ATRIUM"),
            new Entry("staff exit", "Try the Staff Exit beside the Gift Shop, at the far side of the Lobby (west end).", StaffExit, "STAFF EXIT"),
            new Entry("records annex", "Find the 1996 evacuation manual in the Records Annex, at the far end of the Staff Corridor past the Control Room (east).", RecordsAnnex, "RECORDS ANNEX"),
            new Entry("restore auxiliary power", "Go through Feed Prep to the Loading Dock.", LoadingDock, "LOADING DOCK"),
            new Entry("service gate is jammed", "The Paddock 4 service gate is jammed. Crouch {crouch} under the bent north leaf.", ServiceGate, "PADDOCK 4 GATE"),
            new Entry("paddock 4 service gate", "Follow the service path to the Paddock 4 gate.", ServiceGate, "PADDOCK 4 GATE"),
            new Entry("cross tyrant valley", "Cross Tyrant Valley to Substation 3. Stay in the tall grass, light off.", Substation, "SUBSTATION 3"),
            new Entry("tripped breakers", "Reset the three tripped breakers at Substation 3 ({0}/3).", Substation, "SUBSTATION 3"),
            new Entry("fuel pump", "Prime the generator's fuel pump at Substation 3 (hold).", Substation, "SUBSTATION 3"),
            new Entry("start the diesel", "Start the diesel generator at Substation 3.", Substation, "SUBSTATION 3"),
            new Entry("transfer lever", "Throw the transfer lever to Circuit B (hold).", Substation, "SUBSTATION 3"),
            new Entry("green route lights", "RUN! Follow the green route lights east to the Old Maintenance Yard.", YardGap, "OLD MAINTENANCE YARD"),
            new Entry("find service exit e-0", "Find Service Exit E-0, the big service gate across the Old Maintenance Yard (east side).", ExitE0, "EXIT E-0"),
            new Entry("keep holding open", "Hold OPEN on the E-0 gate. Progress holds if you hide; they can hear the motor.", ExitE0, "EXIT E-0"),
            new Entry("sprint out", "GO! Sprint out through Service Exit E-0!", Coast, "EXIT E-0"),
        };

        /// <summary>
        /// Glues the last two words with a no-break space (U+00A0, in every Barlow SDF asset), so a wrapped objective
        /// never ends on a single word. Spaces inside a rich-text tag or a {key} token are left alone.
        /// </summary>
        public static string NoOrphan(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            int sp = s.TrimEnd().LastIndexOf(' ');
            if (sp <= 0) return s;
            if (s.LastIndexOf('>') > sp || s.LastIndexOf('}') > sp && s.LastIndexOf('{') < sp) return s;
            if (s.IndexOf('<', sp) >= 0 || s.IndexOf('{', sp) >= 0) return s;
            return s.Substring(0, sp) + "\u00A0" + s.Substring(sp + 1);
        }

        private static readonly Regex s_Counter = new Regex(@"\((\d+)\s*/\s*3\)");

        private static int Find(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return -1;
            for (int i = 0; i < Entries.Length; i++)
                if (raw.IndexOf(Entries[i].Match, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        /// <summary>The short, place-naming line the player reads for a story objective string.</summary>
        public static string Display(string raw) => Display(raw, new Vector2(-1000f, 0f));

        /// <summary>
        /// As <see cref="Display(string)"/>, but the long post-manual route objective is given one leg at a time from
        /// where the player is: Feed Prep and the dock, then the service path, then Tyrant Valley.
        /// </summary>
        public static string Display(string raw, Vector2 playerXZ)
        {
            int i = Find(raw);
            if (i < 0) return NoOrphan(raw ?? string.Empty);
            if (Entries[i].Target == LoadingDock && playerXZ.x > -500f)
            {
                if (playerXZ.x < 8f) return NoOrphan("Go through Feed Prep to the Loading Dock.");
                if (playerXZ.x < 92f) return NoOrphan("Follow the service path to the Paddock 4 gate.");
                return NoOrphan("Cross Tyrant Valley to Substation 3. Stay in the tall grass, light off.");
            }
            string copy = Entries[i].Copy;
            if (copy.Contains("{0}"))
            {
                Match m = s_Counter.Match(raw);
                copy = copy.Replace("{0}", m.Success ? m.Groups[1].Value : "0");
            }
            return NoOrphan(copy);
        }

        /// <summary>World XZ of the current objective (falls back to the story flags when the string is unknown).</summary>
        public static Vector2 Target(GameState gs, Vector2 playerXZ, out string place)
        {
            place = string.Empty;
            if (gs == null) return SecurityTerminal;
            int i = Find(gs.CurrentObjective);
            if (i >= 0)
            {
                place = Entries[i].Place;
                Vector2 t = Entries[i].Target;
                // The route objective after the manual covers the whole way to the substation: once the player is
                // out of the building, the beacon moves on to the next leg.
                if (t == LoadingDock && playerXZ.x > 20f)
                {
                    if (playerXZ.x < 92f) { place = "PADDOCK 4 GATE"; return ServiceGate; }
                    place = "SUBSTATION 3"; return Substation;
                }
                if (t == ServiceGate && playerXZ.x > 96f) { place = "SUBSTATION 3"; return Substation; }
                return t;
            }
            if (!gs.HasFlashlight) { place = "SECURITY OFFICE"; return SecurityDesk; }
            if (!gs.GetFlag("blackout")) { place = "CONTROL ROOM"; return ControlRoom; }
            if (!gs.GetFlag("manual_read")) { place = "RECORDS ANNEX"; return RecordsAnnex; }
            if (!gs.GetFlag("aux_power")) { place = "SUBSTATION 3"; return playerXZ.x < 20f ? LoadingDock : (playerXZ.x < 92f ? ServiceGate : Substation); }
            if (!gs.GetFlag("e0_open")) { place = "EXIT E-0"; return playerXZ.x < 401f ? YardGap : ExitE0; }
            place = "DAWN COAST";
            return Coast;
        }
    }
}
