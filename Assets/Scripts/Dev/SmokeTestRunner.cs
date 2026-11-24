using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cretacea.Core;
using Cretacea.Story;

namespace Cretacea.Dev
{
    /// <summary>
    /// Unattended check of a built player. Start the game with
    ///   -cretaceaSmoke &lt;folder&gt;
    /// and it shows the menu, loads the game, visits every chapter start, writes one screenshot per stop plus a
    /// report (frame times, errors logged) into the folder, and quits. Without the flag this does nothing.
    /// </summary>
    public sealed class SmokeTestRunner : MonoBehaviour
    {
        private string _dir;
        private int _errors;
        private readonly System.Text.StringBuilder _report = new System.Text.StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-cretaceaSmoke");
            if (at < 0 || at + 1 >= args.Length) return;
            var go = new GameObject("[SmokeTestRunner]");
            DontDestroyOnLoad(go);
            go.AddComponent<SmokeTestRunner>()._dir = args[at + 1];
        }

        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errors++;
            if (_errors <= 20) _report.AppendLine("ERROR: " + condition);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_dir);
            _report.AppendLine("Cretacea smoke test, " + Application.version + ", " + SystemInfo.graphicsDeviceName + ", " + Screen.width + "x" + Screen.height);

            yield return Shot("00_menu", 8f);

            GameState gs = GameState.EnsureInstance();
            gs.ApplyCheckpointPreset(CheckpointId.Night);
            gs.SetPaused(false);
            gs.SetMainMenuState(false);
            gs.SetFlag("launch_from_menu");
            SceneManager.LoadScene("MainWorld");

            float waited = 0f;
            float loadStart = Time.realtimeSinceStartup;
            yield return null;                                   // the load happens on the next frame
            while ((SceneManager.GetActiveScene().name != "MainWorld" || StoryDirector.Instance == null) && waited < 60f)
            {
                waited = Time.realtimeSinceStartup - loadStart;
                yield return null;
            }
            waited = Time.realtimeSinceStartup - loadStart;
            if (StoryDirector.Instance == null)
            {
                _report.AppendLine("FAIL: MainWorld did not come up within 60 s");
                Finish();
                yield break;
            }
            _report.AppendLine("MainWorld up after " + waited.ToString("F1") + " s");

            var stops = new (CheckpointId cp, string name)[]
            {
                (CheckpointId.Night, "01_night_office"), (CheckpointId.Records, "02_records_blackout"),
                (CheckpointId.Dock, "03_dock"), (CheckpointId.Paddock, "04_paddock_valley"),
                (CheckpointId.Substation, "05_substation"), (CheckpointId.Yard, "06_yard"),
            };
            foreach (var stop in stops)
            {
                StoryDirector.Instance.StartFromCheckpoint(stop.cp, true);
                yield return Shot(stop.name, 9f);
            }

            Finish();
        }

        private IEnumerator Shot(string name, float settleSeconds)
        {
            float t = 0f, worst = 0f, sum = 0f; int frames = 0;
            while (t < settleSeconds)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                if (t > settleSeconds * 0.5f) { sum += dt; frames++; if (dt > worst) worst = dt; }
                yield return null;
            }
            float avg = frames > 0 ? sum / frames : 0f;
            _report.AppendLine(string.Format("{0}: {1:F1} fps average, worst frame {2:F0} ms, scene {3}", name, avg > 0f ? 1f / avg : 0f, worst * 1000f, SceneManager.GetActiveScene().name));
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return new WaitForSecondsRealtime(0.6f);
        }

        private void Finish()
        {
            _report.AppendLine("errors logged: " + _errors);
            _report.AppendLine(_errors == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            File.WriteAllText(Path.Combine(_dir, "report.txt"), _report.ToString());
            Application.Quit();
        }
    }
}
