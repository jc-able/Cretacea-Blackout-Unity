#!/bin/bash
# Hidden (batch-mode) Unity editor for a project CLONE, for agents working in parallel.
#   clone_editor.sh start <clone_dir>    launch, wait until ready, sandbox saves, 1280x720 game view, open MainWorld
#   clone_editor.sh stop  <clone_dir>    save nothing, quit the clone's editor (only the PID this script started)
#   clone_editor.sh status <clone_dir>
# Never point this at the owner's project (~/CretaceaBlackoutUnity).
set -u
cmd="${1:-}"; clone="${2:-}"
[ -z "$cmd" ] || [ -z "$clone" ] && { echo "usage: clone_editor.sh start|stop|status <clone_dir>"; exit 2; }
clone="$(cd "$clone" 2>/dev/null && pwd)" || { echo "no such folder"; exit 2; }
case "$clone" in
  $HOME/CretaceaBlackoutUnity|$HOME/CretaceaBlackoutUnity/*) echo "REFUSED: that is the owner's project"; exit 3;;
  $HOME/CretaceaUnityClones/*) ;;
  *) echo "REFUSED: clones live under ~/CretaceaUnityClones"; exit 3;;
esac
export PATH="$HOME/.unity/bin:$PATH"; export UNITY_NO_BANNER=1; export UNITY_PROJECT_PATH="$clone"
UNITY=/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity
pidfile="$clone/.clone_editor.pid"
alive() { [ -f "$pidfile" ] && ps -p "$(cat "$pidfile")" -o command= 2>/dev/null | grep -q -- "-projectPath $clone"; }
case "$cmd" in
  start)
    if alive; then echo "already running pid $(cat "$pidfile")"; else
      mkdir -p "$clone/Logs" "$clone/Screenshots" "$clone/SaveSandbox"
      CRETACEA_SAVE_DIR="$clone/SaveSandbox" nohup "$UNITY" -batchmode -projectPath "$clone" -logFile "$clone/Logs/editor-batch.log" >/dev/null 2>&1 &
      echo $! > "$pidfile"; echo "launched pid $(cat "$pidfile")"
    fi
    unity status --until-ready --project-path "$clone" --timeout 600 2>&1 | tail -1
    unity command script eval --code 'UnityEditor.SessionState.SetString("Cretacea.SaveDir", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "SaveSandbox")); UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280, 720, "agent720"); return Cretacea.Core.SaveManager.SavePath;' 2>&1 | grep -v claude-code-hint | tail -1
    open_scene=$(unity command scene list_open 2>&1 | grep -c "MainWorld")
    [ "$open_scene" = "0" ] && unity command scene open --path Assets/Scenes/MainWorld.unity 2>&1 | grep -v claude-code-hint | tail -1
    unity command editor set_auto_tick 2>&1 | grep -v claude-code-hint | tail -1
    ;;
  stop)
    if alive; then pid="$(cat "$pidfile")"; unity command editor quit --discard true 2>/dev/null | tail -1; for i in 1 2 3 4 5 6 7 8 9 10; do ps -p "$pid" >/dev/null 2>&1 || break; /bin/sleep 1; done; ps -p "$pid" >/dev/null 2>&1 && kill "$pid"; echo "stopped $pid"; else echo "not running"; fi
    [ -f "$pidfile" ] && mv "$pidfile" "$pidfile.last"
    ;;
  status)
    if alive; then echo "running pid $(cat "$pidfile")"; unity status 2>&1 | grep "$clone"; else echo "not running"; fi
    ;;
  *) echo "unknown command"; exit 2;;
esac
