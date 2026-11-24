using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Audio
{
    /// <summary>
    /// Plays surface-accurate footsteps across ALL 7 surfaces (Concrete, Tile, Gravel, Metal, Mud, Grass, Water)
    /// plus crouch/stand/grass-rustle foley, fixing Godot's AUD-3 bug where tile/gravel/metal steps stayed silent.
    /// </summary>
    public sealed class SurfaceAudioDetector : MonoBehaviour
    {
        [SerializeField] private float crouchVolumeScale = 0.42f;
        [SerializeField] private float walkVolumeScale = 0.78f;
        [SerializeField] private float sprintVolumeScale = 1.05f;

        private void OnEnable()
        {
            GameEvents.OnPlayerFootstep += OnPlayerFootstep;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerFootstep -= OnPlayerFootstep;
        }

        private void OnPlayerFootstep(SurfaceType surface, Vector3 worldPos, bool sprinting, bool crouching)
        {
            PlaySurfaceFootstep(surface, worldPos, sprinting, crouching);
        }

        public void PlaySurfaceFootstep(SurfaceType surface, Vector3 worldPos, bool sprinting, bool crouching)
        {
            string baseCue = GetCueIdForSurface(surface);
            string postureCue = crouching ? $"{baseCue}_crouch" : (sprinting ? $"{baseCue}_run" : $"{baseCue}_walk");
            float vol = crouching ? crouchVolumeScale : (sprinting ? sprintVolumeScale : walkVolumeScale);

            AudioManager audioMgr = AudioManager.Instance;
            if (audioMgr != null)
            {
                string chosen = audioMgr.Bank != null && audioMgr.Bank.TryGetEntry(postureCue, out SoundCueEntry e) && e.HasAnyClip()
                    ? postureCue
                    : baseCue;
                audioMgr.PlayOneShot(chosen, worldPos, vol, force2D: true);
            }
            else
            {
                GameEvents.RaisePlaySound(baseCue, worldPos, vol);
            }
        }

        public static string GetCueIdForSurface(SurfaceType surface)
        {
            return surface switch
            {
                SurfaceType.Tile => "foley_step_tile",
                SurfaceType.Gravel => "foley_step_gravel",
                SurfaceType.Metal => "foley_step_metal",
                SurfaceType.Mud => "foley_step_mud",
                SurfaceType.Grass => "foley_step_grass",
                SurfaceType.Water => "foley_step_water",
                _ => "foley_step_concrete"
            };
        }
    }
}
