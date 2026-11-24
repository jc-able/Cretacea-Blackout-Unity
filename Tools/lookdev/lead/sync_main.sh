#!/bin/bash
# Bring JC's open project (~/CretaceaBlackoutUnity, editor on port 7800) to the verified branch of the clone.
# Run ONLY when his editor answers commands (no modal dialog, no build running). Never closes or restarts the editor.
#   sync_main.sh [check]      check = only report what would happen
MAIN=$HOME/CretaceaBlackoutUnity; SRC=$HOME/CretaceaUnityClones/vfx; BR=lead/verify
cd "$MAIN" || exit 1
unset UNITY_PROJECT_PATH; source Tools/lookdev/ucli.sh          # default project = the owner's
say() { echo "== $(date +%T) $*"; }
ST=$(unity command build status 2>/dev/null | grep -E '^status:' | head -1)
say "build status: ${ST:-none}"
case "$ST" in *building*|*queued*) echo "editor busy (build or dialog): stop"; exit 2;; esac
PLAY=$(perl -e 'alarm 25; exec @ARGV' unity command script eval --code 'return UnityEditor.EditorApplication.isPlaying + "|" + UnityEditor.EditorApplication.isCompiling + "|" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;' 2>/dev/null | grep -v hint | tail -1)
say "editor: playing|compiling|scene = ${PLAY:-no answer}"
case "$PLAY" in False\|False\|*) ;; *) echo "editor not idle in edit mode: stop"; exit 3;; esac
TARGET=$(git -C "$SRC" rev-parse "$BR"); HEAD0=$(git rev-parse HEAD)
say "main at $(git rev-parse --short HEAD) ($(git rev-parse --abbrev-ref HEAD)), target $(git -C "$SRC" rev-parse --short "$BR")"
git merge-base --is-ancestor "$HEAD0" "$TARGET" 2>/dev/null || { git fetch -q "$SRC" "$BR:$BR" && git merge-base --is-ancestor "$HEAD0" "$BR" || { echo "target does not descend from main's HEAD: stop"; exit 4; }; }
[ "$1" = "check" ] && { git status --short | grep -v '^??' | wc -l | xargs echo "tracked files with local changes:"; exit 0; }

# 1. park the editor on an empty scene: files change under it, and an open dirty scene would raise a dialog
say "park editor on an empty scene"
ev 'UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); return "empty scene";' | tail -1
# 2. git: safety tag, drop regenerated files, fast-forward
git tag -f pre-sync-$(date +%m%d-%H%M) >/dev/null
git fetch -q "$SRC" "$BR:$BR"
git stash push -q -m "generated files before sync $(date +%H:%M)" 2>/dev/null; git stash list | head -2
# untracked files that the branch brings would block the fast-forward: move them aside (they are older copies)
ASIDE="${TMPDIR:-/tmp}/cretacea_main_untracked_$(date +%H%M)"; mkdir -p "$ASIDE"
git ls-files --others --exclude-standard | while read -r f; do if git cat-file -e "$BR:$f" 2>/dev/null; then mkdir -p "$ASIDE/$(dirname "$f")"; mv "$f" "$ASIDE/$f"; echo "moved aside: $f"; fi; done
git merge -q --ff-only "$BR" && say "main now at $(git rev-parse --short HEAD)" || { echo "fast-forward failed: stop"; exit 5; }
# 3. import + compile
say "recompile"; unity recompile "$MAIN" --format json 2>&1 | grep -v hint | grep -o '"summary":{[^}]*}' | head -1
unity status --until-ready --timeout 900 2>&1 | grep -v hint | grep "CretaceaBlackoutUnity" | cut -c1-70
# 4. rebuild the generated scenes (also regenerates the git-ignored generated assets); retry while the editor reloads
for try in 1 2 3 4 5 6; do R=$(unity command cretacea look_pass --steps profiles 2>&1 | grep -v hint | tail -1 | cut -c1-60); case "$R" in *regenerated*) break;; esac; /bin/sleep 5; done
say "build_all_scenes ($R)"; unity command cretacea build_all_scenes --timeout 2400 2>&1 | grep -v hint | tail -2
# 5. leave MainWorld open in edit mode, nothing dirty
ev 'var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (s.name != "MainWorld") UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainWorld.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); s = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); return s.name + " dirty=" + s.isDirty + " playing=" + UnityEditor.EditorApplication.isPlaying;' | tail -1
say "doors (save file sandboxed for the test)"; sandbox_saves >/dev/null
unity command cretacea verify_playmode_doors --timeout 600 2>&1 | grep -v hint | grep -E "failedDoors|documentsFound|navMesh" | head -4
/bin/sleep 3
# give the save folder back to the owner's own Play sessions, and reopen the scene from disk so nothing is left dirty
ev 'UnityEditor.SessionState.EraseString("Cretacea.SaveDir"); if (!UnityEditor.EditorApplication.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainWorld.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); return s.name + " dirty=" + s.isDirty + " playing=" + UnityEditor.EditorApplication.isPlaying + " save=" + Cretacea.Core.SaveManager.SavePath;' | tail -1
git status --short | grep -v '^??' | wc -l | xargs echo "tracked files changed after the rebuild:"
say "sync done"
