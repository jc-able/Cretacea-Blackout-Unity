# source this: Unity CLI helpers for the Cretacea Unity project
export PATH="$HOME/.unity/bin:$PATH"
# keep a project path that is already exported (agents work in clones); the owner's project is only the default
export UNITY_PROJECT_PATH="${UNITY_PROJECT_PATH:-$HOME/CretaceaBlackoutUnity}" UNITY_NO_BANNER=1
P="$UNITY_PROJECT_PATH"
ev() { unity command script eval --code "$1" 2>&1 | grep -v "claude-code-hint"; }          # eval C# (one statement block)
evf() { unity command script eval --file "$1" 2>&1 | grep -v "claude-code-hint"; }         # eval a .cs snippet file
gv() { unity command capture game_view --source screen --width ${3:-1920} --height ${4:-1080} --output-path "$1" 2>&1 | grep -v "claude-code-hint" | tail -${2:-1}; }
waitt() { /bin/sleep "$1"; }
# keep Play-mode tests away from the owner's save file (SessionState lasts until the editor restarts)
sandbox_saves() { unity command script eval --code "UnityEditor.SessionState.SetString(\"Cretacea.SaveDir\", \"$UNITY_PROJECT_PATH/SaveSandbox\"); return Cretacea.Core.SaveManager.SavePath;" 2>&1 | grep -v claude-code-hint | tail -1; }
