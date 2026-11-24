#!/usr/bin/env python3
"""Full-route play-test for Cretacea: Blackout (Unity), driven through the Unity CLI.

Plays every story beat from the security office to the dawn coast in the editor's Play mode by calling the same
interactables and story triggers the player would, checks the game state after each beat, takes a screenshot per
beat and watches the console for new errors. Dinosaurs are despawned after the beat that needs them (there is no
god mode, and a dead test player proves nothing about the route).

    python3 Tools/lookdev/route_test.py [--project <dir>] [--out Screenshots/route] [--keep-dinos]

Exit code 0 = every check passed. Saves go to <project>/SaveSandbox (never the owner's save).
"""
import argparse
import json
import os
import subprocess
import sys
import time

ap = argparse.ArgumentParser()
ap.add_argument("--project", default=os.environ.get("UNITY_PROJECT_PATH") or os.path.expanduser("~/CretaceaBlackoutUnity"))
ap.add_argument("--out", default="Screenshots/route")
ap.add_argument("--width", type=int, default=1280)
ap.add_argument("--height", type=int, default=720)
ap.add_argument("--keep-dinos", action="store_true")
args = ap.parse_args()

ENV = dict(os.environ)
ENV["PATH"] = os.path.expanduser("~/.unity/bin") + ":" + ENV.get("PATH", "")
ENV["UNITY_NO_BANNER"] = "1"
ENV["UNITY_PROJECT_PATH"] = args.project

results = []          # (ok, name, detail)
t_start = time.time()


def unity(*a, timeout=120):
    p = subprocess.run(["unity", *a], env=ENV, capture_output=True, text=True, timeout=timeout)
    return "\n".join(l for l in (p.stdout + p.stderr).splitlines() if "claude-code-hint" not in l).strip()


def ev(code):
    out = unity("command", "script", "eval", "--code", code)
    return out.splitlines()[-1].strip() if out else ""


def wait(seconds):
    time.sleep(seconds)


def shot(name):
    path = f"{args.out}/{name}.png"
    unity("command", "capture", "game_view", "--source", "screen", "--width", str(args.width), "--height", str(args.height),
          "--output-path", path)
    return path


def check(name, ok, detail=""):
    results.append((bool(ok), name, detail))
    print(("PASS " if ok else "FAIL ") + name + (("  [" + detail + "]") if detail else ""), flush=True)


def flag(key):
    return ev(f'return Cretacea.Core.GameState.Instance.GetFlag("{key}") ? "1" : "0";') == "1"


def state():
    s = ev('var g = Cretacea.Core.GameState.Instance; var p = Cretacea.Player.PlayerController.Instance; '
           'return "cp=" + g.CurrentCheckpoint + ";fl=" + g.HasFlashlight + ";key=" + g.HasMasterKey + ";cut=" + g.InCutscene + '
           '";over=" + g.IsGameOver + ";dead=" + (p != null && p.IsDead) + ";preset=" + Cretacea.Core.AtmosphereController.Instance.CurrentPreset + '
           '";pos=" + (p != null ? p.transform.position.ToString("F1") : "none");')
    return dict(kv.split("=", 1) for kv in s.split(";") if "=" in kv)


def alive(step):
    st = state()
    check(f"{step}: player alive", st.get("dead") == "False", str(st))
    # a player who fell through the world is "alive" too: the height must be near the ground
    try:
        y = float(st.get("pos", "(0, 0, 0)").strip("()").split(",")[1])
    except (ValueError, IndexError):
        y = -999.0
    check(f"{step}: player on the ground (y {y:.1f})", -6.0 < y < 60.0, str(st))
    return st


def interact(iid):
    return ev(f'var i = Cretacea.Core.WorldRegistry.Instance.GetInteractable("{iid}"); if (i == null) return "MISSING"; '
              f'i.OnInteract(null); return "ok";')


def trigger(tid):
    return ev(f'Cretacea.Core.GameEvents.RaiseStoryTriggerEntered("{tid}"); return "ok";')


def teleport(x, y, z, yaw=0.0, pitch=0.0):
    return ev(f'Cretacea.Player.PlayerController.Instance.Teleport(new UnityEngine.Vector3({x}f, {y}f, {z}f), {yaw}f, {pitch}f); return "ok";')


def despawn():
    if args.keep_dinos:
        return
    ev('if (Cretacea.AI.DinoSpawner.Instance != null) Cretacea.AI.DinoSpawner.Instance.DespawnAll(); return "ok";')


def console_cursor():
    out = unity("command", "console", "status", "--format", "json")
    try:
        d = json.loads(out)
        d = d.get("data", d)
        return int(d.get("cursor", -1))
    except Exception:
        return -1


def new_errors(since):
    out = unity("command", "console", "get", "--level", "error", "--since", str(since), "--tail", "50", "--format", "json")
    try:
        d = json.loads(out)
        d = d.get("data", d)
        return [e.get("message", "")[:160].replace("\n", " | ") for e in d.get("entries", [])]
    except Exception:
        return ["(could not read console)"]


# ---------------------------------------------------------------------------------------------------------------------
os.makedirs(os.path.join(args.project, args.out), exist_ok=True)
print("project:", args.project)
status = unity("command", "editor", "status", "--format", "json")
if '"playMode":"playing"' not in status:
    ev(f'UnityEditor.SessionState.SetString("Cretacea.SaveDir", "{args.project}/SaveSandbox"); return "ok";')
    unity("command", "editor.playmode", "start")
    wait(4)
    unity("status", "--until-ready", "--timeout", "180", timeout=200)
    wait(3)
else:
    ev(f'UnityEditor.SessionState.SetString("Cretacea.SaveDir", "{args.project}/SaveSandbox"); return "ok";')
cursor = console_cursor()
print("save path:", ev("return Cretacea.Core.SaveManager.SavePath;"))

# ---- Chapter 1: Night Shift ----------------------------------------------------------------------------------------
ev('Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.Night, true); return "ok";')
wait(5)
st = alive("ch1 start")
check("ch1 start: no flashlight in hand", st.get("fl") == "False", str(st))
check("ch1 start: atmosphere NightCalm", st.get("preset") == "NightCalm", st.get("preset", ""))
shot("01_ch1_office_start")

check("ch1: desk flashlight present", interact("pickup_flashlight") == "ok")
wait(2)
check("ch1: flashlight picked up", state().get("fl") == "True")
shot("02_ch1_flashlight_in_hand")

check("ch1: terminal usable", interact("terminal") == "ok")
wait(7)
teleport(-37.0, 0.06, 28.6, 0, -4)
wait(1)
shot("03_ch1_control_room_lit")
trigger("control_switchover")
wait(10.5)
shot("04_ch1_blackout_lights_dying")
wait(5.5)
shot("05_ch1_blackout_emergency_on")
wait(8)
shot("06_ch1_blackout_talon_breakout")
wait(5)
st = alive("ch1 blackout")
check("ch1: blackout flag set", flag("blackout"))
check("ch1: atmosphere NightStormBlackout", st.get("preset") == "NightStormBlackout", st.get("preset", ""))
despawn()

teleport(-31.0, 0.06, -2.0, 180, 0)
wait(1)
trigger("lobby_beat")
wait(4)
shot("07_ch1_lobby_blackout")
wait(6)
despawn()
alive("ch1 lobby")

teleport(-53.5, 0.06, -10.0, 270, 0)
wait(1)
trigger("staff_exit_collapse")
wait(8)
check("ch1: collapse flag set", flag("collapse"))
shot("08_ch1_staff_exit_collapse")
despawn()

# ---- Chapter 2: Records Annex ----------------------------------------------------------------------------------------
teleport(-12.0, 0.06, 20.5, 60, 0)
wait(1.5)
shot("09_ch2_corridor_east")
ev('var d = Cretacea.Core.WorldRegistry.Instance.GetDoor("door_records"); if (d != null) d.Open(null, true); return "ok";')
teleport(-5.5, 0.06, 26.0, 70, -15)
wait(1.5)
shot("10_ch2_records_annex")
check("ch2: manual present", interact("doc_manual") == "ok")
wait(1.5)
shot("11_ch2_manual_open")
for _ in range(5):
    ev('Cretacea.UI.MenuController.Instance.CloseDocumentModal(); return "ok";')
    wait(0.8)
wait(2)
check("ch2: manual_read flag", flag("manual_read"))
check("ch2: master key", state().get("key") == "True")
check("ch2: checkpoint Records", state().get("cp") == "Records", state().get("cp", ""))
despawn()

# ---- Chapter 3: Service Paths ----------------------------------------------------------------------------------------
ev('var d = Cretacea.Core.WorldRegistry.Instance.GetDoor("door_wing"); if (d != null) { d.SetLocked(false); d.Open(null, true); } return "ok";')
teleport(-2.0, 0.06, 12.0, 180, 0)
wait(1.5)
shot("12_ch3_feed_prep")
despawn()
teleport(10.4, 0.06, -11.0, 90, 0)
wait(1)
trigger("reach_dock")
wait(3)
check("ch3: checkpoint Dock", state().get("cp") == "Dock", state().get("cp", ""))
shot("13_ch3_loading_dock")
teleport(40.0, 0.06, 6.5, 0, -2)
wait(2)
shot("14_ch3_service_path_start")
teleport(55.0, 0.06, 14.0, 90, 0)
wait(1)
trigger("paths_corner2")
wait(0.1)   # the raptor crosses the junction in under a second (straight line since round 4): shoot at once
shot("15_ch3_path_raptor_crossing")
wait(5.4)
despawn()
teleport(71.0, 0.06, 6.0, 110, 0)
trigger("paths_mid")
wait(2)
teleport(85.0, 0.06, -3.5, 100, 0)
trigger("paths_end")
wait(2.5)
shot("16_ch3_service_gate")
alive("ch3")

# ---- Chapter 4: Tyrant Valley ----------------------------------------------------------------------------------------
teleport(93.6, 0.06, -2.2, 90, 0)
wait(1)
trigger("enter_paddock")
wait(3)
check("ch4: checkpoint Paddock", state().get("cp") == "Paddock", state().get("cp", ""))
shot("17_ch4_valley_entry")
# the ground under a point: a ray from above (Terrain.activeTerrain is an arbitrary tile now that the island has nine)
ty = ev('UnityEngine.RaycastHit h; return UnityEngine.Physics.Raycast(new UnityEngine.Vector3(128f, 120f, 0f), UnityEngine.Vector3.down, out h, 400f, Cretacea.Core.GameLayers.EnvironmentAndDoorMask, UnityEngine.QueryTriggerInteraction.Ignore) ? h.point.y.ToString("F2") : "none";')
try:
    ty = float(ty)
except ValueError:
    ty = 0.5
teleport(128.0, ty + 0.1, 0.0, 70, 4)
wait(1)
trigger("trex_reveal")
wait(1.9)   # r4: aims at the third strike (2.05 s; capture latency varies) while the T. rex crosses the open gap ~45 m down the road
shot("18_ch4_trex_reveal")
wait(3.3)
despawn()
alive("ch4")

# ---- Chapter 5: Substation 3 -----------------------------------------------------------------------------------------
teleport(252.5, 2.4, 0.0, 90, -3)
wait(1)
trigger("enter_substation")
wait(3)
check("ch5: checkpoint Substation", state().get("cp") == "Substation", state().get("cp", ""))
shot("19_ch5_substation_dark")
despawn()
for sid in ["substation_Breaker1", "substation_Breaker2", "substation_Breaker3", "substation_FuelPump"]:
    check(f"ch5: {sid}", interact(sid) == "ok")
    wait(1.2)
for n in range(3):
    check(f"ch5: generator starter press {n + 1}", interact("substation_GeneratorStarter") == "ok")
    wait(3.0)
    despawn()
teleport(276.0, 2.4, -5.0, 120, -5)
wait(1)
shot("20_ch5_generator_running")
check("ch5: transfer lever", interact("substation_TransferLever") == "ok")
wait(2.6)
shot("21_ch5_aux_power_floods")
despawn()
wait(2)
check("ch5: aux_power flag", flag("aux_power"))
teleport(300.0, 3.3, -5.0, 100, 0)
wait(2)
shot("22_ch5_green_route")
despawn()
alive("ch5")

# ---- Chapter 6: Old Maintenance Yard ---------------------------------------------------------------------------------
teleport(403.2, 1.1, -22.8, 90, 0)
wait(1)
trigger("reach_yard")
wait(5)
check("ch6: checkpoint Yard", state().get("cp") == "Yard", state().get("cp", ""))
despawn()
shot("23_ch6_yard_entry")
teleport(445.0, 1.1, -30.0, 110, 0)
wait(2)
shot("24_ch6_yard_open")
despawn()
teleport(500.0, 1.1, -50.0, 60, 0)
wait(1.5)
shot("25_ch6_e0_gate_closed")
check("ch6: gate box", interact("gate_box") == "ok")
wait(3)
check("ch6: e0_open flag", flag("e0_open"))
shot("26_ch6_e0_gate_open")
despawn()
teleport(515.0, 1.1, -46.0, 90, 0)
wait(1)
trigger("through_e0_gate")
wait(3.5)
shot("27_escape_coast_road")
despawn()
alive("ch6")

# ---- Ending ------------------------------------------------------------------------------------------------------------
teleport(536.5, 1.1, -46.0, 90, 0)
wait(1)
trigger("coast_ending")
for i, t in enumerate([7, 4, 4, 4]):      # 2.1 s in black, then the morning fades in over 2.4 s
    wait(t)
    shot(f"28_ending_{i + 1}")
st = state()
check("ending: atmosphere DawnCoast", st.get("preset") == "DawnCoast", st.get("preset", ""))
wait(10)
shot("29_end_screen")
st = state()
check("ending: cutscene finished", st.get("cut") == "False", str(st))
check("ending: player never died", st.get("dead") == "False", str(st))

errs = new_errors(cursor)
check("console: no new errors during the run", len(errs) == 0, "; ".join(errs[:4]))

failed = [r for r in results if not r[0]]
print(f"\n{len(results) - len(failed)} / {len(results)} checks passed in {time.time() - t_start:.0f} s")
for _, name, detail in failed:
    print("  FAILED:", name, detail)
sys.exit(1 if failed else 0)
