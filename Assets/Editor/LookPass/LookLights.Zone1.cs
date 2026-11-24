using UnityEngine;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Lamp rules for Zone 1 (Visitor Center). Checked BEFORE the core table in LookLights.cs, so a rule here can refine a core rule.
    /// One line per lamp family: name regex, candela, kelvin (0 = keep colour / use filter), range, volumetric dimmer,
    /// shadow (None/Low/Medium/High), spot angle (0 = keep), colour filter, throw (Throw.Down = ceiling fixture).
    ///
    /// Night balance (EV ~4, white point ~19 nits):
    /// - offices are lit by a troffer grid (one fixture per ~40 m2): a 950 cd wide downlight 3.3 m up gives ~85 lux under it
    ///   and ~50 lux on a wall 3.5 m away, so two neighbours put ~100-110 lux on the walls. Only one fixture per room casts
    ///   shadows or lights the fog (cost: every shadowed light also redraws the moving casters each frame, every
    ///   volumetric light is evaluated per fog froxel); the extra fixtures are shadowless and fogless.
    /// - a "_Wash" is an upward 160 deg spot 1.7 m off the floor standing in for the floor bounce: ~30 lux on the ceiling, no
    ///   hot spot (the old "_Fill" points 1 m under the tiles gave an 80 lux blob and are gone).
    /// - a battery unit 3 m up gives ~35 lux on the floor (340 cd / 9.6 m^2), unshadowed except the four big lobby units.
    /// </summary>
    public static partial class LookLights
    {
        /// <summary>Visitor Center troffers: a 176 deg cone instead of the core 164 deg, with the lamp 3.5 cm under the tiles
        /// (LookFixtures). 179 deg was tried in review 2's fix round: a cached spot shadow that wide degenerates (a lit disc
        /// under every shadowed troffer, the rest of its floor shadowed), so the junction is left to the perimeter washes.
        /// The 164 deg cone stopped 0.4-0.5 m under the ceiling on a wall 3 m away, which left the black band under every
        /// lit ceiling that review 1 flagged as a blocker; at 176 deg the cone edge reaches the wall 0.1 m under the tiles.</summary>
        private const float TrofferCone = 176f;
        /// <summary>One fluorescent colour for every Visitor Center troffer (the bible allows 4300-5600 K for tubes): 4000 K
        /// made the beige-textured rooms read sepia (red/blue 1.5), 4300 K still peach under the D65 grade (mid-tone
        /// red/blue 1.36-1.45 in review 2), so the tubes are 5000 K.</summary>
        private const float TubeK = 5000f;
        private const string VcRooms = "(Security|Lockers|Control|Records|Break|GiftShop|StaffExit|Server|Prep|Vet|WingS|Cold)";

        // Round 4: the signal colours this table uses, declared in this file ahead of the table. Static initializers of a
        // partial class run file by file in an order C# leaves open: reading LookLights.cs's Red/Amber/CoolBlue from here
        // could see them still unset (black), which turned every red emergency head, its lens and the records beacon off.
        private static readonly Color Z1Red = new Color(1.0f, 0.10f, 0.06f);
        private static readonly Color Z1Amber = new Color(1.0f, 0.55f, 0.12f);
        private static readonly Color Z1CoolBlue = new Color(0.36f, 0.66f, 1.0f);

        private static readonly Rule[] Zone1Rules =
        {
            // ---- ceiling fills (Zone1_VisitorCenterBuilder.CeilingFill): an upward spot 7 m under each <= 6 m ceiling
            //      cell, on the ceiling-only light layer, shadowless: the even light a lit floor sends back to the tiles
            //      (review 2 round 3: charcoal ceilings with one oval per troffer). Inner angle set by the builder.
            // fix round: 10 m range (the 45 deg cell edge at the tiles is 9.9 m from the source; 12.5 m reached 5.5 m above the
            // ceiling into the light lists of the rooms above and beside) at 850 cd for the same ceiling level
            // the staff corridor's ceiling sat at ~17 % of its bright walls (target 35-50 %): its fills run hotter
            new Rule("^Light_Corr_CeilFill(_\\d+)?$",   1500f, TubeK, 10f, 0f, Shadow.None, 0f),
            new Rule("_CeilFill(_\\d+)?$",             850f, TubeK, 10f, 0f, Shadow.None, 0f),
            // ---- ceiling washes (upward, no hot spot): the lamp light on the ceiling, so the night sky ambient no longer
            //      decides its colour (review 1: blue ceiling between warm walls). Several per room, shadowless, no fog.
            new Rule("^Light_Dock_Wash$",             380f, 0f,    9f,   0f,    Shadow.None, 160f),
            // upward floor-bounce stand-ins in the corridor and the feed-prep wing (CeilingWash)
            new Rule("^Light_(Corr|Prep)_Wash",      210f, 0f,    4.5f, 0f,    Shadow.None, 170f),
            // perimeter washes of the rooms (Washes): 2.5 m off a wall, aimed up at the wall/ceiling junction
            // round 4: the gift shop's back-wall washes drew a bright ceiling band with no fixture in it (review 3 giftshop_on)
            new Rule("^Light_GiftShop_Wash",           240f, 0f,    6.5f, 0f,    Shadow.None, 150f),
            new Rule("_Wash(_\\d)?$",                 450f, 0f,    7.5f, 0f,    Shadow.None, 150f),

            // ---- office troffer grids: neutral fluorescent white, one shadowed fixture per room -----------------------
            new Rule("^Light_(Security|Lockers|Control|Records)_Main_\\d", 950f, TubeK, 6.5f, 0f, Shadow.None, TrofferCone, null, Throw.Down),
            // round 4: the security office's shadowed main at 150 deg (its 1024 map spread over a 176 deg cone drew stair steps
            // in the back-right corner, review 3 office_on / intro_20); its companions keep the 176 deg cone for the cornice
            new Rule("^Light_Security_Main$", 1000f, TubeK, 6.5f, 0f, Shadow.Medium, 150f, null, Throw.Down),
            new Rule("^Light_(Lockers_Main|Control_Main[LR]|Records_Main)$", 1000f, TubeK, 6.5f, 0f, Shadow.Medium, TrofferCone, null, Throw.Down),
            // companion troffers of the Visitor Center rooms (LookFixtures.TrofferGrid): same cone as their main
            // Round 3: shadowless (Shadow.None), so they carry no floor bounce of their own: one bright oval per troffer was the
            // "blotchy halo" of review 2; the ceiling now gets its light from the even ceiling fills, and only each room's
            // shadowed main troffer keeps a bounce
            // fix round: back to Shadow.Low (cached shadow: no light through walls; 6.5 m range kept); their bounces are removed in
            // LookFixtures.ThinCompanionBounce instead (Shadow.None also clamped the range and dimmed the walls 10-14 %)
            new Rule("^Fixture_Lamp_Light_" + VcRooms + "_.*_Grid\\d+$", 520f, TubeK, 6.5f, 0f, Shadow.Low, TrofferCone, null, Throw.Down),

            // ---- corridor tubes: the core 1150 cd, no volumetric (an orb 7 cm under its own lens), every other one shadowed
            new Rule("^Light_Corr_Main_[13579]$",    1150f, TubeK, 8f, 0f, Shadow.None, TrofferCone, null, Throw.Down),
            new Rule("^Light_Corr_Main_\\d+$",        1150f, TubeK, 8f, 0f, Shadow.Low,  TrofferCone, null, Throw.Down),

            // ---- blackout: warm battery emergency units (VCEmergency), pools not floods ---------------------------------
            new Rule("^Light_Lobby_Batt",            900f, 3200f, 16f,  0.70f, Shadow.Low, 150f, null, Throw.Down),
            // round 4: hung from the canopy over the walk to the Service Road 3 exit (Zone1 builder), a 100 deg pool, not a
            // 140 deg flood from the wall behind the player (review 3 13_ch3: 57 % of the frame under 16/255); a cached
            // 256 shadow so the truck and the drums stand on the floor
            new Rule("^Light_Dock_Batt",            1300f, 3200f, 12f,  0.45f, Shadow.Low, 100f, null, Throw.Down),
            // Visitor Center rooms and the staff corridor: a cached 256 shadow gives each unit its floor bounce and wall
            // fill (LookLights.EnsureBounce), so the walls round a pool are not crushed to black (corridor_dark_noflash)
            // Round 3: one unit per room, by the exit, 2.45 m up, aimed 60 deg down and out from the wall (LookFixtures.Emergency):
            // no Throw.Down, so the aim stays and the unit carries no floor bounce or wall fill (the battery pool is an island;
            // review 2: the units' bounces washed the blackout rooms almost as bright as with the power on)
            new Rule("^Light_(Corridor|" + VcRooms.Substring(1) + "_Batt", 300f, 3300f, 9f, 0.45f, Shadow.Low, 120f),
            new Rule("_Batt",                        340f, 3300f, 9f,   0.60f, Shadow.None, 150f, null, Throw.Down),
            // round 4: the faint spill of a battery unit's own heads on its housing (LookFixtures.TwinHeadBody), so the box
            // reads in the blackout; follows its unit (LampBounce), shadowless, no fog, 0.7 m reach
            new Rule("^BattSpill$",                    0.55f, 3300f, 0.8f, 0f,    Shadow.None, 0f),

            // ---- red only as small accents: corridor beacons, the staff exit, the jammed shutter control ----------------
            // round 4: 64 deg straight down (the 150 deg cone from 0.35 m off the north wall flooded the opposite wall bay
            // crimson: 20.7 % of corridor_dark red-dominant); the foot of the far wall is 37 deg off vertical, outside the cone
            new Rule("^Light_Corr_Emerg",            150f, 0f,    5f,   0.45f, Shadow.None, 64f, Z1Red, Throw.Down),
            new Rule("^Light_StaffExit_Emerg$",       60f, 0f,    4.5f, 0.60f, Shadow.None, 150f, Z1Red, Throw.Down),
            new Rule("^Light_Lobby_Emerg$",          140f, 0f,    7f,   0.80f, Shadow.None, 150f, Z1Red, Throw.Down),

            // ---- the amber work beacon on the records desk. Review 4: its cached shadow never blocked the floor through the
            //      desk top (an amber ellipse under the desk), so the light reaches only 1 m: the binder and desk, not the floor
            new Rule("^Light_Records_ManualBeacon$", 260f, 0f,    1.0f, 0.45f, Shadow.Low, 0f, Z1Amber),

            // ---- power-on fixtures added by the look pass (first of each room shadowed, the rest not) -------------------
            new Rule("^Light_Prep_Main_0$",         1300f, TubeK, 11f, 0.10f, Shadow.Medium, TrofferCone, null, Throw.Down),
            new Rule("^Light_Prep_Main",            1300f, TubeK, 11f, 0f, Shadow.None, TrofferCone, null, Throw.Down),
            new Rule("^Light_(Vet|WingS)_Main",     1300f, TubeK, 11f, 0.10f, Shadow.Low, TrofferCone, null, Throw.Down),
            new Rule("^Light_Cold_Main",            1100f, 5000f, 10f, 0.10f, Shadow.Low, TrofferCone, null, Throw.Down),
            new Rule("^Light_StaffExit_Main",       1000f, TubeK, 10f, 0.10f, Shadow.Medium, TrofferCone, null, Throw.Down),
            new Rule("^Light_GiftShop$",            1300f, TubeK, 9f, 0.10f, Shadow.Medium, TrofferCone, null, Throw.Down),
            new Rule("^Light_GiftShop_\\d",          1300f, TubeK, 9f, 0f, Shadow.Low, TrofferCone, null, Throw.Down),   // bounce removed in LookFixtures (ceiling fills)
            new Rule("^Light_Server_Main",           900f, TubeK, 10f, 0.10f, Shadow.Medium, TrofferCone, null, Throw.Down),
            // the cold room's blue was a bare 420 cd point under the ceiling: a saturated blue flood with a hot spot in the
            // blackout. Now a small cool pool from the freezer status light.
            new Rule("^Light_Cold_Blue",              45f, 0f,    5f,  0.20f, Shadow.None, 120f, Z1CoolBlue, Throw.Down),
            new Rule("^Light_Server_Blue",           110f, 0f,    5f,  0.45f, Shadow.None, 0f, Z1CoolBlue),
            new Rule("^Light_Break_Main$",          1250f, TubeK, 9f, 0.10f, Shadow.Medium, TrofferCone, null, Throw.Down),
            new Rule("^Light_Break_Main_\\d",        1250f, TubeK, 9f, 0f, Shadow.Low, TrofferCone, null, Throw.Down),   // bounce removed in LookFixtures (ceiling fills)
            // warm practicals carry the warmth instead of the troffers: checkout pendants
            new Rule("^Light_GiftShop_Pendant",      260f, 2800f, 6f,  0f,    Shadow.None, 110f),

            // ---- lobby atrium (VCMain): wall grazers, mezzanine downlights, banner accents, reception task lights ---------
            // the two atrium high-bays (core 9500 cd) carry less of the room so the auto exposure stops keying on the floor
            // pool and the walls read lit: review 2 measured the walls at luma 44-52 with the power on
            // round 4: the lit lobby read as one amber-brown (frame R/G 1.26, B/G 0.79; review 3): the high-bays are the neutral
            // key at 4300 K and the wall grazers 4800 K (the walls' own texture is warm); the 3000-3300 K accents (skeleton, banners, reception) and the moonlight
            // through the roof (Light_Lobby_MoonFill) carry the warm-cool contrast
            new Rule("^Light_Lobby_Main",           6000f, 4300f, 30f,  0.45f, Shadow.High, 118f),
            new Rule("^Light_Lobby_Grazer",        12000f, 4800f, 18f,  0.10f, Shadow.None, 150f),
            new Rule("^Light_Lobby_MoonFill$",      9000f, 7000f, 36f,  0f,    Shadow.Low, 110f),
            new Rule("^Light_Lobby_MezzDown",        700f, 3200f, 5.5f, 0f,    Shadow.None, 120f, null, Throw.Down),
            new Rule("^Light_Lobby_Accent",         3200f, 3300f, 13f,  0.20f, Shadow.None, 34f),
            new Rule("^Light_Lobby_ReceptionTask",   300f, 3000f, 4f,   0f,    Shadow.None, 100f, null, Throw.Down),

            // ---- plaza, entrance canopy, viewing gallery (Plaza group: on before the blackout); no shadows -------------
            // a lamp-post head throws a 110 deg cone from under its head; pools ~90 lux under the head, not one carpet
            // round 4: 2400 cd in a 95 deg cone: each pool ~140 lux under the head, about twice the moonlit paving seen from
            // the opening aerial (review 3 intro_16: an even mauve sheet with pinprick heads)
            new Rule("^Light_PlazaLamp",            2400f, 3000f, 14f,  0.40f, Shadow.None, 95f),
            new Rule("^Light_Canopy_Down",           800f, 3000f, 9f,   0.60f, Shadow.None, 130f, null, Throw.Down),
            new Rule("^Light_Gallery_Down",          800f, 3000f, 9f,   0.60f, Shadow.None, 130f, null, Throw.Down),
            new Rule("^Light_FacadeUplight",       1800f, 3000f, 14f,  0.10f, Shadow.None, 60f),
            // round 4: 3300 K and a thin fog halo: at 2700 K with a 0.5-0.6 volumetric the halos read red-maroon under the night
            // grade (review 3 intro_17: RGB 63-71/34-38/30-34)
            new Rule("^Light_SecWest_WallPack$",     600f, 3300f, 9f,   0.22f, Shadow.None, 100f),
            new Rule("^Light_SecWest_WallPack_\\d$", 420f, 3300f, 8f,  0.20f, Shadow.None, 100f),   // round 3: two more along the west wall
            new Rule("^Light_Kiosk_Interior",        160f, 3000f, 4f,   0f,    Shadow.None, 0f),
            // round 3: one warm can uplight in every plaza tree island (review 2 intro_15/16: planters read as black discs at
            // dusk and night): lights the trunk and the underside of the crown, shadowless, no fog
            new Rule("^Light_TreeUp",                520f, 3000f, 9f,   0f,    Shadow.None, 46f),
        };
    }
}
