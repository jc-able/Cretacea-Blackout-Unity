using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Dust motes drifting around the camera indoors. Few and tiny; they reflect only the flashlight and the room's
    /// own lamps (Cretacea/HDRP/RainParticle in mote mode, no sky light), so they appear in the beam and vanish
    /// outside it. Off outdoors (the rain takes over there).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DustMotesController : MonoBehaviour
    {
        [SerializeField] private float boxSize = 7f;
        [SerializeField] private float ratePerSecond = 32f;     // review 1: 90/s read as snow under the dock canopy
        [SerializeField] private int maxMotes = 260;

        private ParticleSystem _ps;
        private Transform _cam;
        private float _indoorBlend;

        public int AliveCount => _ps != null ? _ps.particleCount : 0;

        private void Start()
        {
            var go = new GameObject("DustMotes");
            go.transform.SetParent(transform, false);
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.04f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.009f);   // fine dust, 1-2 px in the beam: a hint, not specks
            main.startColor = new Color(1f, 0.97f, 0.92f, 1f);
            main.gravityModifier = 0.0015f;
            main.maxParticles = maxMotes;

            var em = _ps.emission;
            em.rateOverTime = 0f;

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(boxSize, boxSize * 0.55f, boxSize);

            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.08f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.02f;
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX
            });
            Shader sh = Shader.Find("Cretacea/HDRP/RainParticle");
            if (sh != null)
            {
                var m = new Material(sh) { name = "MAT_DustMotes (runtime)" };
                m.SetFloat("_Mode", 2f);
                m.SetColor("_Color", new Color(1f, 0.96f, 0.9f, 0.5f));
                m.SetFloat("_Scatter", 0.05f);
                m.SetFloat("_SkyLight", 0f);
                m.SetFloat("_FlashLight", 0f);   // indoor motes: no moon, sky or lightning
                m.SetFloat("_IndoorLights", 1f);
                m.SetFloat("_UseRoofOcclusion", 0f);
                m.SetFloat("_Occlusion", 0f);
                m.SetFloat("_MaxNits", 24f);
                m.SetFloat("_Softness", 1.2f);
                m.SetVector("_NearFade", new Vector4(0.12f, 0.35f, 0f, 0f));
                r.sharedMaterial = m;
            }
            _ps.Play();
        }

        private void OnDestroy()
        {
            if (_ps != null)
            {
                var r = _ps.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.sharedMaterial != null) Destroy(r.sharedMaterial);
            }
        }

        private void LateUpdate()
        {
            if (_ps == null) return;
            if (_cam == null) { Camera c = Camera.main; if (c != null) _cam = c.transform; }
            if (_cam == null) return;

            bool indoors = WeatherLightSampler.CameraIndoors;
            _indoorBlend = Mathf.MoveTowards(_indoorBlend, indoors ? 1f : 0f, Time.deltaTime * 0.5f);
            // the emitter box sits a little ahead of the camera, where the beam goes
            _ps.transform.position = _cam.position + _cam.forward * (boxSize * 0.3f);
            var em = _ps.emission;
            em.rateOverTime = ratePerSecond * _indoorBlend;
        }
    }
}
