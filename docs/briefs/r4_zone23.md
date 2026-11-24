# Round 4 package: service paths, substation, yard, E-0 gate, coast road

Clone: `~/CretaceaUnityClones/zone23`. Branch `r4/zone23`.
You own: `Assets/Editor/WorldBuilder/Zone2_*.cs`, `Zone3_*.cs`, `Zone23Dressing.cs`,
`Assets/Editor/LookPass/LookLights.Zone23.cs`, `LookSigns.cs`, `LookRoofs.cs` (zones 2-3), your materials. Another
package (r4/valley) adds lights, puddles and the T. rex staging in Tyrant Valley through its own new files: do not
work in the valley except for the bollard in goal 5.

Read `docs/review3/night_b.md` and `night_b_check.md`, then `night_a_check.md` (14, 15, 16).
The lead is changing in parallel: outdoor walls get wetter in the storm (darker, more sheen), dawn haze on the far
ridge. Your part:
1. **Major — the service path: frame 15 is still three clean pale wall faces** (`15_ch3_path_raptor_crossing`: left
   wall 51/255, back wall 102, right wall 54, about 65 % of the frame), and across 14-16 the walls look dry and newly
   poured. Give each face one large form that reads from 10 m and in silhouette: a steel door with a stencil on the
   right wall, a PADDOCK 4 arrow sign under the back fence, ferns and vines spilling over the left coping, a drain
   channel, strong runoff streaks under every coping joint. Lower the panel tint that makes them pale.
2. **Major — the closed E-0 gate** (`25_ch6_e0_gate_closed`, `24_ch6_yard_open`, `extra/yard_before_strike`). The lead
   added 16 upright bars, stiles and a brace to each leaf after the captures (`_refs/final/route/25...`). Finish it:
   the leaves must read as a closed, heavy gate from the yard at 30-60 m as well (they vanished from there); a motor
   housing on the hinge post; a beacon that is red while closed and green when it opens; no chain-link that turns
   into a halftone dot grid at distance (give the leaves their own infill).
3. **Major — the ending crane shot** (`28_ending_2`): the warehouse is a flat-roofed orange box with a black roof and
   one vent, and the perimeter wall is a plain tan band. Roof detail (ridge or parapet, skylights, units, gutters,
   stains), a wall with piers, coping, staining and a fence. (Trees are the island package's; haze is the lead's.)
4. Minors: the raised foreground slab at the substation is matte orange-brown with no sheen while the slab beyond it
   mirrors the lamps (`19_ch5_substation_dark`); after the transfer lever is thrown it has the same pose and only a
   small indicator lights (`21_ch5_aux_power_floods`): move the lever and make the result visible; the nearest
   bollard's collar clips to mint-white (`22_ch5_green_route`); the run to the coast never shows the sea
   (`27_escape_coast_road`): open the view over the rails if you can do it in your files, otherwise report what
   blocks it.
5. Minor — the knocked-down bollard by the jeep is a plain black capped cylinder that reads as a giant torch
   (`17_ch4_valley_entry` x 790-870): a broken lens, a base plate and a cable.
Report `perf_here.sh` in the yard before and after. Run `route_test.py` and `verify_playmode_doors` before hand-in.
