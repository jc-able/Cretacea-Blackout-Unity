using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cretacea.Core;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// The Look Pass: everything that decides how the generated world LOOKS, applied on top of what the zone
    /// builders place. Idempotent; safe to run again on the open scene, and run automatically after a world build.
    ///
    ///   unity command cretacea look_pass                       (all steps on the open scene)
    ///   unity command cretacea look_pass --steps lights        (one step: render, profiles, materials, lights, shadows, weather, albedo, preview)
    ///   unity command cretacea look_preview --preset NightStormBlackout
    /// </summary>
    public static class LookPass
    {
        public static readonly string[] AllSteps = { "render", "profiles", "materials", "lights", "shadows", "preview" };

        [MenuItem("Cretacea/Look/Run Look Pass On Open Scene")]
        public static void RunMenu()
        {
            Debug.Log("[Look] " + Run("all", AtmospherePreset.NightStormBlackout, save: false));
        }

        public static string Run(string steps, AtmospherePreset previewPreset, bool save)
        {
            var wanted = new HashSet<string>(
                (string.IsNullOrWhiteSpace(steps) || steps.Trim().ToLowerInvariant() == "all" ? string.Join(",", AllSteps) : steps)
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()));
            var log = new List<string>();

            if (wanted.Contains("render")) log.Add(LookRenderSettings.Apply());
            if (wanted.Contains("profiles")) { LookAtmosphereProfiles.GenerateAll(); log.Add("profiles regenerated"); }
            if (wanted.Contains("materials")) log.Add(LookMaterials.Apply());
            if (wanted.Contains("lights")) log.Add(LookLights.Calibrate());
            if (wanted.Contains("fixtures") || wanted.Contains("lights")) log.Add(LookFixtures.Apply());   // fixtures move lights and split companion lamps: always after calibration
            if (wanted.Contains("shadows")) log.Add(LookLights.MarkStaticShadowCasters());
            if (wanted.Contains("weather")) log.Add(LookWeather.Apply());
            if (wanted.Contains("albedo")) log.Add(LookAlbedoMap.Apply());
            if (wanted.Contains("preview")) log.Add(Preview(previewPreset));

            Scene scene = SceneManager.GetActiveScene();
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (save) { EditorSceneManager.SaveScene(scene); log.Add("scene saved"); }
            }
            return string.Join("; ", log);
        }

        /// <summary>Called by the world assembler right before it saves a freshly built scene.</summary>
        public static string RunOnBuild(bool isMainMenu)
        {
            var log = new List<string>();
            if (!System.IO.File.Exists(LookAtmosphereProfiles.PathFor(AtmospherePreset.GoldenDay))) { LookAtmosphereProfiles.GenerateAll(); log.Add("profiles generated"); }
            log.Add(LookMaterials.Apply());
            log.Add(LookLights.Calibrate());
            log.Add(LookFixtures.Apply());
            log.Add(LookLights.MarkStaticShadowCasters());
            if (!isMainMenu) log.Add(LookWeather.Apply());
            if (!isMainMenu) log.Add(LookAlbedoMap.Apply());   // last: after everything that places or merges geometry
            log.Add(Preview(isMainMenu ? AtmospherePreset.NightStormBlackout : AtmospherePreset.NightCalm));
            return string.Join("; ", log);
        }

        public static string Preview(AtmospherePreset preset)
        {
            AtmosphereController atmo = UnityEngine.Object.FindAnyObjectByType<AtmosphereController>();
            if (atmo == null) return "preview: no AtmosphereController in scene";
            if (Application.isPlaying) { atmo.SetPreset(preset, 0f); return "preview (play): " + preset; }
            atmo.PreviewInEditor(preset);
            EditorUtility.SetDirty(atmo);
            return "preview: " + preset;
        }

        [CliCommand("cretacea", "look_pass",
            "Apply the Look Pass to the open scene: render settings, atmosphere profiles, light calibration, static shadow casters, edit-mode preview.",
            MainThreadRequired = true)]
        public static string LookPassCli(
            [CliArg("steps", "Comma list of steps (render,profiles,materials,lights,shadows,preview) or 'all'")] string steps = "all",
            [CliArg("preset", "Atmosphere preset shown in edit mode afterwards")] string preset = "NightStormBlackout",
            [CliArg("save", "Save the scene afterwards (edit mode only)")] bool save = false)
        {
            if (!Enum.TryParse(preset, true, out AtmospherePreset p)) p = AtmospherePreset.NightStormBlackout;
            return Run(steps, p, save);
        }

        [CliCommand("cretacea", "look_preview",
            "Show one atmosphere preset (GoldenDay, DuskTransition, NightCalm, NightStormBlackout, DawnCoast) in edit mode or Play mode.",
            MainThreadRequired = true)]
        public static string LookPreviewCli(
            [CliArg("preset", "GoldenDay | DuskTransition | NightCalm | NightStormBlackout | DawnCoast")] string preset = "NightStormBlackout")
        {
            if (!Enum.TryParse(preset, true, out AtmospherePreset p)) return "unknown preset " + preset;
            return Preview(p);
        }
    }
}
