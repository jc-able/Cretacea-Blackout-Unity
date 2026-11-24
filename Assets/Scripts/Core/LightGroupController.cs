using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Orchestrates all 13 HDRP facility Light Groups, blackout sputter-fades, emergency relay click-on,
    /// and ordered east-bound Substation -> Old Yard AuxRoute ripple activation.
    /// </summary>
    public sealed class LightGroupController : MonoBehaviour
    {
        public static LightGroupController Instance { get; private set; }

        private readonly Dictionary<LightGroupId, List<LightGroupMember>> _groups = new Dictionary<LightGroupId, List<LightGroupMember>>();
        private readonly Dictionary<LightGroupId, bool> _groupStates = new Dictionary<LightGroupId, bool>();
        private readonly Dictionary<LightGroupId, Coroutine> _activeTransitions = new Dictionary<LightGroupId, Coroutine>();

        public static LightGroupController EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<LightGroupController>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("[LightGroupController]");
            Instance = go.AddComponent<LightGroupController>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            WorldRegistry.EnsureInstance().RegisterLightGroupController(this);
        }

        private void OnEnable()
        {
            GameEvents.OnLightGroupChanged += OnLightGroupEvent;
        }

        private void OnDisable()
        {
            GameEvents.OnLightGroupChanged -= OnLightGroupEvent;
        }

        public void RegisterMember(LightGroupMember member)
        {
            if (member == null) return;
            if (!_groups.TryGetValue(member.GroupId, out List<LightGroupMember> list))
            {
                list = new List<LightGroupMember>();
                _groups[member.GroupId] = list;
            }
            if (!list.Contains(member))
            {
                list.Add(member);
            }

            if (_groupStates.TryGetValue(member.GroupId, out bool isOn))
            {
                member.SetFactor(isOn ? 1f : 0f);
            }
        }

        public void UnregisterMember(LightGroupMember member)
        {
            if (member == null) return;
            if (_groups.TryGetValue(member.GroupId, out List<LightGroupMember> list))
            {
                list.Remove(member);
            }
        }

        public bool IsGroupEnabled(LightGroupId id) => _groupStates.TryGetValue(id, out bool on) && on;

        /// <summary>Global shader float bumped every time a light group finishes changing. Reflection probes
        /// (Cretacea.Rendering.ReflectionProbeRefresh) watch it and re-render, so a blacked-out room never reflects
        /// the lit room it was before.</summary>
        public static readonly int LightEpochId = Shader.PropertyToID("_CretaceaLightEpoch");
        private static float s_lightEpoch;

        private static void NotifyGroupSettled()
        {
            s_lightEpoch += 1f;
            Shader.SetGlobalFloat(LightEpochId, s_lightEpoch);
        }

        private void OnLightGroupEvent(LightGroupId id, bool enabled, float fadeSeconds)
        {
            SetGroup(id, enabled, fadeSeconds);
        }

        public void SetGroup(LightGroupId id, bool enabled, float fadeSeconds = 0f, bool electricalFlicker = false)
        {
            _groupStates[id] = enabled;

            if (_activeTransitions.TryGetValue(id, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
                _activeTransitions.Remove(id);
            }

            if (!_groups.TryGetValue(id, out List<LightGroupMember> members) || members.Count == 0)
                return;

            if (fadeSeconds <= 0.001f && !electricalFlicker)
            {
                float target = enabled ? 1f : 0f;
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i] != null) members[i].SetFactor(target);
                }
                NotifyGroupSettled();
                return;
            }

            _activeTransitions[id] = StartCoroutine(TransitionGroupRoutine(id, enabled, Mathf.Max(0.05f, fadeSeconds), electricalFlicker));
        }

        /// <summary>
        /// Ripples the east-bound AuxRoute green strobe bollards sequentially from Substation 3 (X >= 286)
        /// toward Old Yard squeeze gap (X = 401), never west of Substation 3 (POS-8 fix).
        /// </summary>
        public Coroutine RippleGroupSequential(LightGroupId id, float stepDelaySeconds = 0.12f, bool surgeEffect = true)
        {
            _groupStates[id] = true;
            if (_activeTransitions.TryGetValue(id, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }

            Coroutine c = StartCoroutine(RippleSequentialRoutine(id, stepDelaySeconds, surgeEffect));
            _activeTransitions[id] = c;
            return c;
        }

        private IEnumerator TransitionGroupRoutine(LightGroupId id, bool targetOn, float duration, bool flicker)
        {
            float start = targetOn ? 0f : 1f;
            float end = targetOn ? 1f : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float val = Mathf.Lerp(start, end, t);

                if (flicker)
                {
                    // Realistic failing ballast / power-restore surge jitter
                    float jitter = Mathf.PerlinNoise(elapsed * 26f, (int)id * 3.7f);
                    if (!targetOn && t < 0.78f && jitter > 0.65f)
                        val *= 0.15f;
                    else if (targetOn && t < 0.45f)
                        val = Mathf.Clamp01(val * (0.6f + jitter * 0.9f));
                }

                ApplyGroupFactor(id, val);
                yield return null;
            }

            ApplyGroupFactor(id, end);
            _activeTransitions.Remove(id);
            NotifyGroupSettled();
        }

        private IEnumerator RippleSequentialRoutine(LightGroupId id, float stepDelay, bool surgeEffect)
        {
            if (!_groups.TryGetValue(id, out List<LightGroupMember> members) || members.Count == 0)
            {
                _activeTransitions.Remove(id);
                yield break;
            }

            var ordered = new List<LightGroupMember>(members);
            ordered.RemoveAll(m => m == null);
            ordered.Sort((a, b) =>
            {
                int cmp = a.SequenceOrder.CompareTo(b.SequenceOrder);
                return cmp != 0 ? cmp : a.transform.position.x.CompareTo(b.transform.position.x);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                LightGroupMember m = ordered[i];
                if (m == null) continue;
                if (surgeEffect)
                {
                    m.SetFactor(1f);
                    GameEvents.RaisePlaySound("breaker_reset_clunk", m.transform.position, 0.35f);
                }
                else
                {
                    m.SetFactor(1f);
                }
                yield return new WaitForSeconds(stepDelay);
            }

            _activeTransitions.Remove(id);
            NotifyGroupSettled();
        }

        private void ApplyGroupFactor(LightGroupId id, float factor)
        {
            if (!_groups.TryGetValue(id, out List<LightGroupMember> members)) return;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null) members[i].SetFactor(factor);
            }
        }
    }
}
