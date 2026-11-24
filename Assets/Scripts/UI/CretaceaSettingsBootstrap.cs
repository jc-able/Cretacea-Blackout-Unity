using UnityEngine;
using Cretacea.Core;

namespace Cretacea.UI
{
    /// <summary>
    /// Applies the saved player settings that the look depends on before the first scene loads:
    /// brightness (EV stops on top of every atmosphere preset) and a one-time migration of the field of view.
    /// The stored FOV is VERTICAL degrees; the old default of 78 reads as fisheye in the corridors, so settings
    /// written before revision 2 (or brand-new settings) move to 68.
    /// </summary>
    public static class CretaceaSettingsBootstrap
    {
        public const int CurrentRevision = 2;
        public const float DefaultFov = 68f;
        public const float FovMin = 55f;
        public const float FovMax = 90f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnLoad()
        {
            SettingsData s = SaveManager.LoadSettings();
            bool dirty = false;
            if (s.settingsRevision < CurrentRevision)
            {
                s.fieldOfView = DefaultFov;
                s.settingsRevision = CurrentRevision;
                dirty = true;
            }
            float fov = Mathf.Clamp(s.fieldOfView, FovMin, FovMax);
            if (!Mathf.Approximately(fov, s.fieldOfView)) { s.fieldOfView = fov; dirty = true; }
            s.brightnessEV = Mathf.Clamp(s.brightnessEV, -1.5f, 1.5f);
            if (dirty) SaveManager.SaveSettings(s);
            AtmosphereController.UserBrightnessEV = s.brightnessEV;
        }
    }
}
