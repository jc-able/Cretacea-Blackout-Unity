#!/usr/bin/env python3
"""Start-to-finish playthrough of Cretacea: Blackout (Unity) by the in-game bot (Assets/Scripts/Dev/PlaythroughBot.cs).

The bot plays like a player: one start, then it only feeds the game's own move / look / interact input (no
teleports, no direct trigger or interactable calls). This script starts Play mode, starts the bot, polls it, grabs a
Game-view frame whenever the bot reports a problem (death, stuck, missing prompt, blocked door ...) and every
--frame-every seconds, and stops when the end screen shows or at --timeout.

    python3 Tools/lookdev/playthrough.py [--project <clone>] [--out Screenshots/playthrough] [--menu]
                                         [--passive-dinos] [--timeout 1500] [--frame-every 20]

--menu starts from the main menu's NEW GAME (the intro plays), otherwise from plain Play (Chapter 1, Night Shift).
Exit code 0 = the bot reached the end screen. The bot's log: <out>/bot_log.txt.
"""
import argparse
import os
import subprocess
import sys
import time

ap = argparse.ArgumentParser()
ap.add_argument("--project", default=os.environ.get("UNITY_PROJECT_PATH"))
ap.add_argument("--out", default="Screenshots/playthrough")
ap.add_argument("--menu", action="store_true")
ap.add_argument("--passive-dinos", action="store_true")
ap.add_argument("--checkpoint", default="", help="development only: start at this checkpoint (Records, Dock, Paddock, Substation, Yard)")
ap.add_argument("--timeout", type=float, default=1500)
ap.add_argument("--frame-every", type=float, default=20)
ap.add_argument("--keep-playing", action="store_true", help="leave Play mode running at the end")
args = ap.parse_args()
if not args.project or "CretaceaBlackoutUnity" in args.project:
    sys.exit("set --project / UNITY_PROJECT_PATH to your clone (never the owner's project)")

ENV = dict(os.environ)
ENV["PATH"] = os.path.expanduser("~/.unity/bin") + ":" + ENV.get("PATH", "")
ENV["UNITY_NO_BANNER"] = "1"
ENV["UNITY_PROJECT_PATH"] = args.project
OUT = os.path.join(args.project, args.out)
os.makedirs(OUT, exist_ok=True)


def unity(*a, timeout=120):
    try:
        p = subprocess.run(["unity", *a], env=ENV, capture_output=True, text=True, timeout=timeout)
    except subprocess.TimeoutExpired:
        return ""
    return "\n".join(l for l in (p.stdout + p.stderr).splitlines() if "claude-code-hint" not in l).strip()


def ev(code):
    out = unity("command", "script", "eval", "--code", code)
    return out.splitlines()[-1].strip() if out else ""


def shot(name):
    unity("command", "capture", "game_view", "--source", "screen", "--width", "1280", "--height", "720",
          "--output-path", os.path.join(args.out, name + ".png"))


def parse(status):
    d = {}
    for kv in status.split("|"):
        if "=" in kv:
            k, v = kv.split("=", 1)
            d[k] = v
    return d


status = unity("command", "editor", "status", "--format", "json")
ev(f'UnityEditor.SessionState.SetString("Cretacea.SaveDir", "{args.project}/SaveSandbox"); return "ok";')
if '"playMode":"playing"' in status:
    unity("command", "editor.playmode", "stop")
    time.sleep(4)
if args.menu:
    # the main menu scene, as a player launches the game
    ev('UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/Scenes/MainMenu.unity"); return "ok";')
for attempt in range(3):     # a start sent while the editor is still importing is dropped: check and resend
    unity("command", "editor.playmode", "start")
    time.sleep(4)
    unity("status", "--until-ready", "--timeout", "180", timeout=200)
    time.sleep(3)
    if '"playMode":"playing"' in unity("command", "editor", "status", "--format", "json"):
        break
if args.menu:
    ev('UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null; return "ok";')

if args.checkpoint:
    ev(f'Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.{args.checkpoint}, true); return "ok";')
    time.sleep(3)
log = os.path.join(OUT, "bot_log.txt")
print(ev(f'return Cretacea.Dev.PlaythroughBot.Begin("{log}", {str(args.passive_dinos).lower()}, {str(args.menu).lower()});'), flush=True)
t0 = time.time()
last_problem = 0
last_frame = 0
frame_n = 0
st = {}
while time.time() - t0 < args.timeout:
    time.sleep(2)
    s = ev("return Cretacea.Dev.PlaythroughBot.Status();")
    if not s or s == "none":
        continue
    st = parse(s)
    try:
        pnum = int(st.get("problem", "0"))
    except ValueError:
        pnum = last_problem
    if pnum > last_problem:
        last_problem = pnum
        name = f"problem_{pnum:02d}_{st.get('cp', '')}"
        shot(name)
        print(f"[{time.time() - t0:5.0f}s] PROBLEM {pnum}: {st.get('last', '')}  -> {name}.png", flush=True)
    if time.time() - last_frame > args.frame_every:
        last_frame = time.time()
        frame_n += 1
        shot(f"frame_{frame_n:03d}_{st.get('cp', '')}")
        print(f"[{time.time() - t0:5.0f}s] {st.get('cp')} | {st.get('obj', '')[:70]} | {st.get('goal')} | {st.get('pos')} | deaths {st.get('deaths')}", flush=True)
    if st.get("done") == "1":
        break

shot("final")
result = st.get("result", "timeout")
if st.get("done") != "1":
    ev('return Cretacea.Dev.PlaythroughBot.Stop();')
    result = "timeout"
errs = unity("command", "console", "get", "--level", "error", "--tail", "20", "--format", "json")
print("\nresult:", result, f"in {time.time() - t0:.0f} s, deaths {st.get('deaths')}")
print("bot log:", log)
if not args.keep_playing:
    unity("command", "editor.playmode", "stop")
sys.exit(0 if result.startswith("finished") else 1)
