using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Generates the five "look" Volume Profiles (one per AtmospherePreset) that AtmosphereController cross-fades
    /// at runtime: Assets/Resources/Atmosphere/Look_&lt;Preset&gt;.asset.
    ///
    /// One sky model for the whole game (Physically Based Sky + Volumetric Clouds), so day, dusk, night, storm and
    /// dawn blend into each other instead of switching sky types. Exposure is automatic inside a narrow per-preset
    /// range: lit rooms and a dark storm outside can both sit in the mid-tones, and nothing is exposed for moonlight
    /// while a lamp is on screen (the old fixed EV 0.6 clipped every lamp to pure red or white).
    ///
    /// The numbers here are the single source of truth; run "cretacea look_pass" (or the menu item) to regenerate.
    /// </summary>
    public static class LookAtmosphereProfiles
    {
        public const string Folder = "Assets/Resources/Atmosphere";
        public const string StarsPath = Folder + "/Look_StarsCube.asset";

        public static string PathFor(AtmospherePreset preset) => $"{Folder}/Look_{preset}.asset";

        private struct Look
        {
            public AtmospherePreset preset;

            // exposure (automatic histogram, clamped)
            public float evMin, evMax, evCompensation;
            public float adaptDarkToLight, adaptLightToDark;

            // sky
            public float skyExposure;            // EV offset on the physically based sky
            public float starsMultiplier;        // space emission (stars)
            public Color horizonTint, zenithTint;
            public float aerosolDensity;         // haze in the air (PBS)
            public Color groundTint;

            // clouds
            public VolumetricClouds.CloudPresets cloudPreset;
            public float cloudDensity;           // densityMultiplier (custom look on top of the preset)
            public float cloudShapeFactor;       // lower = more coverage
            public float cloudBottom, cloudRange;
            public bool cloudShadows;
            public float cloudShadowOpacity;
            public float cloudAmbientDimmer, cloudSunDimmer;
            public Color cloudScatterTint;
            public float windSpeedKmh, windOrientation;

            // fog
            public float fogMeanFreePath, fogBaseHeight, fogMaxHeight, fogAnisotropy;
            public Color fogAlbedo;
            public Color fogTint;
            public bool fogConstant;             // far haze takes fogColor instead of the sky's colour (dusk, night, storm)
            public Color fogColor;               // HDR, scene luminance units
            public float fogLightProbeDimmer;
            public float fogMaxDistance;
            public float fogDepthExtent;         // how far the volumetric (light-shaft) fog reaches

            // tone curve (Custom mode on every preset, so presets cross-fade without a switch in the curve)
            public float toeStrength, toeLength, shoulderStrength, shoulderLength, shoulderAngle;
            public float blackLift;              // LiftGammaGain lift offset: the darkest tone on screen (0 = true black)

            // shadows
            public float shadowDistance;

            // grade
            public float temperature, tint;
            public float contrast, saturation, postExposure;
            public Color colorFilter;
            public Vector4 smhShadows, smhMidtones, smhHighlights;   // rgb = colour, w = offset
            public Color splitShadows, splitHighlights;
            public float splitBalance;

            // lens
            public float bloomIntensity, bloomScatter, bloomThreshold;
            public float vignette, grain, chromatic, motionBlur;

            // screen-space lighting
            public float aoIntensity, aoRadius;
            public float indirectDiffuse, indirectReflection;
        }

        [MenuItem("Cretacea/Look/Regenerate Atmosphere Profiles")]
        public static void GenerateAllMenu()
        {
            GenerateAll();
            Debug.Log("[Look] Atmosphere profiles regenerated in " + Folder);
        }

        public static void GenerateAll()
        {
            EnsureFolder(Folder);
            Cubemap stars = EnsureStarsCubemap();
            foreach (Look look in AllLooks())
            {
                BuildProfile(look, stars);
            }
            BuildUserBrightnessProfile();
            BuildIndoorProfile();
            AssetDatabase.SaveAssets();
        }

        /// <summary>The player's brightness setting: one post-exposure override the runtime instantiates and drives.</summary>
        private static void BuildUserBrightnessProfile()
        {
            string path = Folder + "/Look_UserBrightness.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Look_UserBrightness";
                AssetDatabase.CreateAsset(profile, path);
            }
            var ca = Get<ColorAdjustments>(profile);
            Set(ca.postExposure, 0f);
            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// Indoor ambient: the game has no baked lighting, so the sky's ambient light and reflections reach every
        /// surface, also inside sealed rooms. The runtime raises this profile's weight while the viewer is enclosed
        /// (walls and a ceiling around the camera), which takes most of the sky light out of interiors.
        /// </summary>
        // Sky ambient left inside a room. By day big windows really do light a room; at night the sky must not tint
        // a lamp-lit ceiling blue (review 1: the first room's ceiling read cyan). The runtime blends between the two
        // by how bright the key light is (AtmosphereController.UpdateIndoor).
        // Review 2 found that the old 0.20 also switched off the screen-space bounce (lit floors sent nothing to the
        // ceilings: charcoal lids). Each room's reflection probe keeps the sky out of the fallback, so the bounce runs
        // at its real strength; the blackout gets 3x so walls between the battery pools still read.
        public const float IndoorDiffuseNight = 1.0f;
        public const float IndoorDiffuseBlackout = 2.2f;   // 3.0 until round 4: it tripled the torch pool's bounce onto the ceiling; the dim base (AtmosphereController.BlackoutBaseLux) carries the dark walls now
        public const float IndoorDiffuseDay = 0.85f;
        public const float IndoorReflection = 0.25f;
        // Review 2: when the power died the camera opened two stops (EV 5 -> 3) and the dark office came out 79 % as
        // bright as the lit one. Inside, the night exposure never goes below this, so a room without power is dark
        // and the flashlight is the brightest thing in it. (The runtime keeps the preset's own floor when it is higher.)
        public const float IndoorNightEvMin = 3.4f;

        private static void BuildIndoorProfile()
        {
            string path = Folder + "/Look_Indoor.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Look_Indoor";
                AssetDatabase.CreateAsset(profile, path);
            }
            var ilc = Get<IndirectLightingController>(profile);
            Set(ilc.indirectDiffuseLightingMultiplier, IndoorDiffuseNight);
            Set(ilc.reflectionLightingMultiplier, IndoorReflection);
            // A lit room is graded neutral: the night presets' cool white balance and blue shadows are for moonlight.
            var wb = Get<WhiteBalance>(profile);
            Set(wb.temperature, 0f);
            Set(wb.tint, 0f);
            var smh = Get<ShadowsMidtonesHighlights>(profile);
            Set(smh.shadows, new Vector4(1f, 1f, 1f, 0f));
            Set(smh.highlights, new Vector4(1f, 1f, 1f, 0f));
            // only the lower exposure limit; AtmosphereController.UpdateIndoor writes the value every frame
            var ex = Get<Exposure>(profile);
            Set(ex.limitMin, IndoorNightEvMin);
            // The ray-miss fallback stays at the look's own setting (probes, then sky). Rooms have their own
            // reflection probe, which wins over the sky inside its box. A probes-only override here was tried: an enum
            // switches as soon as this volume has any weight, and it also applies to whatever is seen from inside, so
            // the meadow through a doorway lost all sky light in its shadows.
            var gi = Get<GlobalIllumination>(profile);
            gi.rayMiss.overrideState = false;
            // Rooms are lit almost entirely by direct lamp light, and most lamps cast no shadow map: without occlusion
            // on direct light the furniture had no contact with the floor (review 2: 'tables float').
            var ao = Get<ScreenSpaceAmbientOcclusion>(profile);
            Set(ao.directLightingStrength, 0.65f);
            EditorUtility.SetDirty(profile);
        }

        // ------------------------------------------------------------------------------------------------------------
        // THE LOOK TABLE
        // ------------------------------------------------------------------------------------------------------------
        private static Look[] AllLooks()
        {
            // Shared defaults, then per-preset differences.
            Look baseLook = new Look
            {
                adaptDarkToLight = 2.4f, adaptLightToDark = 1.1f,
                horizonTint = Color.white, zenithTint = Color.white,
                aerosolDensity = 0.012f,
                groundTint = new Color(0.16f, 0.22f, 0.26f),          // below the horizon the sky model shows sea haze, not olive earth
                cloudBottom = 1100f, cloudRange = 2400f,
                cloudAmbientDimmer = 1f, cloudSunDimmer = 1f,
                cloudScatterTint = Color.black,
                fogTint = Color.white, fogLightProbeDimmer = 1f, fogMaxDistance = 5000f, fogDepthExtent = 80f,
                // Measured on Nov 20: ACES crushed everything under ~2 % to black (night ground 0.005 -> 0.0015), which
                // is what made the nights unreadable and the day look underexposed. This curve keeps the dark end
                // straight and only rolls the highlights off.
                // Review 1 (Nov 20): hot spots, lenses, the dawn sun and lit clouds clipped to flat white. The shoulder
                // now spans 2.4 stops above white, so a surface at 2-4x white keeps its texture.
                toeStrength = 0.10f, toeLength = 0.5f, shoulderStrength = 0.9f, shoulderLength = 2.4f, shoulderAngle = 0.2f,
                colorFilter = Color.white,
                smhShadows = new Vector4(1f, 1f, 1f, 0f), smhMidtones = new Vector4(1f, 1f, 1f, 0f), smhHighlights = new Vector4(1f, 1f, 1f, 0f),
                splitShadows = new Color(0.5f, 0.5f, 0.5f), splitHighlights = new Color(0.5f, 0.5f, 0.5f),
                bloomScatter = 0.6f, bloomThreshold = 0.9f,
                chromatic = 0.04f, motionBlur = 0.25f,
                aoIntensity = 0.9f, aoRadius = 1.6f,
                indirectDiffuse = 1f, indirectReflection = 1f,
            };

            // --- Prologue: late afternoon, the park open, the island at its best. Natural colour, warm key, blue shade.
            Look day = baseLook;
            day.preset = AtmospherePreset.GoldenDay;
            day.evMin = 12.0f; day.evMax = 14.2f; day.evCompensation = 0.7f;
            day.toeStrength = 0.10f;                                  // 0.28 crushed every shaded surface to black
            day.indirectDiffuse = 1.3f;                               // blue sky fill in the shade
            day.chromatic = 0f;
            day.skyExposure = 0f; day.starsMultiplier = 0f;
            day.cloudPreset = VolumetricClouds.CloudPresets.Cloudy;
            day.cloudDensity = 0.42f; day.cloudShapeFactor = 0.88f;
            day.cloudShadows = true; day.cloudShadowOpacity = 0.45f;
            day.cloudBottom = 1500f; day.cloudRange = 2200f;
            day.windSpeedKmh = 22f; day.windOrientation = 140f;
            // Thin, low haze: with a 1500 m mean free path up to 320 m the far sea and every aerial view ended in a
            // flat olive band instead of a horizon.
            day.fogMeanFreePath = 3600f; day.fogBaseHeight = -8f; day.fogMaxHeight = 140f; day.fogAnisotropy = 0.6f;
            day.fogAlbedo = new Color(0.86f, 0.90f, 0.95f);
            day.shadowDistance = 260f;
            // Neutral white balance: the low sun is warm on its own. With +4/+2 and warm highlights the grey paving
            // read orange-brown and the shade brown (the owner's "sepia").
            day.temperature = -1f; day.tint = 0f;
            day.contrast = 8f; day.saturation = 10f;
            day.smhShadows = new Vector4(0.94f, 0.99f, 1.08f, 0f);
            day.smhHighlights = new Vector4(1.01f, 1.0f, 0.99f, 0f);
            day.bloomIntensity = 0.07f; day.vignette = 0.16f; day.grain = 0.08f;

            // --- Dusk: the storm front arrives; sun under the cloud deck, teal shade, last amber light.
            Look dusk = baseLook;
            dusk.preset = AtmospherePreset.DuskTransition;
            dusk.evMin = 6.5f; dusk.evMax = 10.5f; dusk.evCompensation = 0.15f;   // +0.35 clipped the clear sky beside the storm front to a white wedge
            dusk.cloudSunDimmer = 0.6f;                               // the sunlit cloud top clipped to a flat cream blob (reviews 2 and 3)
            dusk.toeStrength = 0.04f; dusk.indirectDiffuse = 1.7f;    // the ground under a bright dusk sky must stay readable
            dusk.chromatic = 0.02f;
            dusk.fogConstant = true; dusk.fogColor = new Color(0.50f, 0.62f, 0.74f) * 190f;
            dusk.groundTint = new Color(0.10f, 0.14f, 0.18f);
            dusk.skyExposure = 0f; dusk.starsMultiplier = 0f;
            dusk.cloudPreset = VolumetricClouds.CloudPresets.Overcast;
            dusk.cloudDensity = 0.55f; dusk.cloudShapeFactor = 0.78f;
            dusk.cloudShadows = false; dusk.cloudShadowOpacity = 0.5f;
            dusk.windSpeedKmh = 55f; dusk.windOrientation = 140f;
            dusk.fogMeanFreePath = 420f; dusk.fogBaseHeight = -6f; dusk.fogMaxHeight = 180f; dusk.fogAnisotropy = 0.7f;
            dusk.fogAlbedo = new Color(0.72f, 0.78f, 0.86f);
            dusk.shadowDistance = 200f;
            dusk.temperature = -4f; dusk.tint = 0f;
            dusk.contrast = 6f; dusk.saturation = 2f;
            dusk.smhShadows = new Vector4(0.90f, 1.0f, 1.10f, 0f);
            dusk.smhHighlights = new Vector4(1.08f, 1.0f, 0.90f, 0f);
            dusk.bloomIntensity = 0.08f; dusk.vignette = 0.22f; dusk.grain = 0.14f;

            // --- Night, power on: moon through broken cloud, tungsten and sodium practicals, light rain.
            Look night = baseLook;
            night.preset = AtmospherePreset.NightCalm;
            night.evMin = 3.3f; night.evMax = 5.0f; night.evCompensation = 0f;
            night.adaptDarkToLight = 3.5f; night.adaptLightToDark = 0.8f;   // stepping from the dark into a lit room: under half a second of glare, not a second and a half
            night.skyExposure = 0f; night.starsMultiplier = 0.9f;
            night.zenithTint = new Color(0.80f, 0.87f, 1.0f); night.horizonTint = new Color(0.82f, 0.89f, 1.0f);   // blue, not cyan
            night.groundTint = new Color(0.02f, 0.03f, 0.05f);
            night.cloudPreset = VolumetricClouds.CloudPresets.Cloudy;
            night.cloudDensity = 0.5f; night.cloudShapeFactor = 0.82f;
            night.cloudShadows = false; night.cloudShadowOpacity = 0.4f;
            night.cloudAmbientDimmer = 1f; night.cloudSunDimmer = 1f;
            night.windSpeedKmh = 40f; night.windOrientation = 140f;
            night.fogMeanFreePath = 260f; night.fogBaseHeight = -6f; night.fogMaxHeight = 90f; night.fogAnisotropy = 0.40f;   // mist reads from every side, not only toward the moon
            night.fogAlbedo = new Color(0.62f, 0.72f, 0.86f);
            night.shadowDistance = 120f;
            // Blue moonlight, not teal: review 1 measured the red channel near zero outside the lamp pools (4,17,17).
            night.temperature = -1f; night.tint = 2f;
            night.contrast = 6f; night.saturation = -10f;
            night.blackLift = 0.0022f;                                // nothing sits below about 7/255
            night.toeStrength = 0f;                                   // darkness stays dark blue, never crushed black
            night.indirectDiffuse = 2.0f;                             // moonlit sky fill (interiors: Look_Indoor)
            // The far haze has its own dim blue-grey glow. Taken from the sky's colour it was a dark veil: under a
            // near-black storm sky the hill 200 m away stayed crisp and nothing stood out as a silhouette.
            night.fogConstant = true; night.fogColor = new Color(0.30f, 0.40f, 0.58f) * 2.2f; night.fogDepthExtent = 220f;
            night.smhShadows = new Vector4(0.99f, 1.0f, 1.05f, 0f);
            night.smhHighlights = new Vector4(1.03f, 1.0f, 0.97f, 0f);
            night.bloomIntensity = 0.10f; night.vignette = 0.26f; night.grain = 0.20f;
            night.aoIntensity = 1.1f;

            // --- Blackout storm: the core of the game. Heavy rain haze, lightning, almost no moon; lamps are islands.
            Look storm = night;
            storm.preset = AtmospherePreset.NightStormBlackout;
            storm.evMin = 3.0f; storm.evMax = 4.7f; storm.evCompensation = 0f;
            storm.starsMultiplier = 0f;
            storm.cloudPreset = VolumetricClouds.CloudPresets.Stormy;
            storm.cloudDensity = 0.72f; storm.cloudShapeFactor = 0.70f;
            storm.cloudBottom = 800f; storm.cloudRange = 3000f;
            storm.windSpeedKmh = 85f;
            storm.fogColor = new Color(0.34f, 0.42f, 0.56f) * 3.0f; storm.fogDepthExtent = 320f;
            storm.cloudSunDimmer = 0.4f;                              // no electric-blue cloud edges around the moon
            storm.fogMeanFreePath = 110f; storm.fogBaseHeight = -6f; storm.fogMaxHeight = 70f; storm.fogAnisotropy = 0.35f;
            storm.fogAlbedo = new Color(0.55f, 0.66f, 0.80f);
            storm.shadowDistance = 100f;
            storm.temperature = -2f; storm.tint = 2f;
            // Review 2: open ground in the storm sat in one narrow band (everything at 20-25/255, nothing above 75).
            // Less sky fill and more contrast, so ground under the canopy drops below the haze and wet highlights,
            // lamps and lightning stand out; the black lift still keeps the floor at about 7/255.
            storm.indirectDiffuse = 1.7f; storm.toeStrength = 0.02f;   // contrast: see below
            storm.contrast = 9f; storm.saturation = -14f;
            storm.smhShadows = new Vector4(0.98f, 1.0f, 1.03f, 0f);   // 1.06 until round 4: with the blue flash it turned wet grass teal-mint
            storm.smhHighlights = new Vector4(1.03f, 1.0f, 0.97f, 0f);
            storm.bloomIntensity = 0.10f; storm.vignette = 0.30f; storm.grain = 0.24f;

            // --- Dawn at the coast: storm gone, sun on the horizon, peach to pale blue sky, teal water.
            Look dawn = baseLook;
            dawn.preset = AtmospherePreset.DawnCoast;
            // Review 2: the settled dawn was a brown-amber monochrome, two stops under: with the sun in the middle of a
            // centre-weighted meter the camera exposed for the disc. The disc is no longer 30,000x white
            // (AtmosphereController.ApplyDiscLook), so the meter can be trusted and the range is narrower.
            dawn.evMin = 10.2f; dawn.evMax = 12.6f; dawn.evCompensation = 0.0f;
            dawn.cloudSunDimmer = 0.8f;
            dawn.indirectDiffuse = 1.5f; dawn.toeStrength = 0.05f;
            dawn.chromatic = 0f;
            dawn.groundTint = new Color(0.30f, 0.27f, 0.28f);
            dawn.skyExposure = 0f; dawn.starsMultiplier = 0f;
            dawn.cloudPreset = VolumetricClouds.CloudPresets.Sparse;
            dawn.cloudDensity = 0.36f; dawn.cloudShapeFactor = 0.90f;
            dawn.cloudBottom = 1500f; dawn.cloudRange = 2200f;
            dawn.cloudShadows = false; dawn.cloudShadowOpacity = 0.5f;
            dawn.windSpeedKmh = 14f; dawn.windOrientation = 110f;
            // A low morning mist in the valley (the overlook stands above it, so the sea stays clear)
            // thin over the sea: at 850 m the whole bay sat in warm haze and the water's own colour never showed
            dawn.fogMeanFreePath = 2200f; dawn.fogBaseHeight = -6f; dawn.fogMaxHeight = 60f; dawn.fogAnisotropy = 0.5f;   // review 3: at 2600 m / 24 m the far ridge was as crisp as the near canopy ('the misty island')
            dawn.fogDepthExtent = 250f;
            dawn.horizonTint = new Color(0.96f, 0.98f, 1.0f); dawn.zenithTint = new Color(0.74f, 0.86f, 1.0f);   // peach to pale blue
            dawn.fogAlbedo = new Color(0.88f, 0.92f, 0.98f);   // review 4: the sea read khaki; the physical sky already puts gold round the sun
            dawn.shadowDistance = 240f;
            // A warm sun needs a cool sky to play against: no magenta tint (it drained the greens and browned the
            // clouds), a slightly cool balance, blue in the shadows, neutral highlights.
            dawn.temperature = -6f; dawn.tint = 0f;
            dawn.contrast = 6f; dawn.saturation = 6f;
            dawn.smhShadows = new Vector4(0.93f, 0.98f, 1.10f, 0f);
            dawn.smhHighlights = new Vector4(1.0f, 1.0f, 1.0f, 0f);
            dawn.bloomIntensity = 0.06f; dawn.vignette = 0.16f; dawn.grain = 0.08f;

            return new[] { day, dusk, night, storm, dawn };
        }

        // ------------------------------------------------------------------------------------------------------------
        private static void BuildProfile(Look L, Cubemap stars)
        {
            string path = PathFor(L.preset);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(profile, path);
            }

            // --- sky ---------------------------------------------------------------------------------------------
            var env = Get<VisualEnvironment>(profile);
            Set(env.skyType, (int)SkyType.PhysicallyBased);
            Set(env.cloudType, 0);
            Set(env.skyAmbientMode, SkyAmbientMode.Dynamic);
            Set(env.windOrientation, L.windOrientation);
            Set(env.windSpeed, L.windSpeedKmh);

            var sky = Get<PhysicallyBasedSky>(profile);
            Set(sky.type, PhysicallyBasedSkyModel.EarthAdvanced);
            Set(sky.atmosphericScattering, true);
            Set(sky.aerosolDensity, L.aerosolDensity);
            Set(sky.aerosolAnisotropy, 0.76f);
            Set(sky.groundTint, L.groundTint);
            Set(sky.horizonTint, L.horizonTint);
            Set(sky.zenithTint, L.zenithTint);
            Set(sky.spaceEmissionTexture, stars);
            Set(sky.spaceEmissionMultiplier, L.starsMultiplier);
            Set(sky.exposure, L.skyExposure);
            Set(sky.updateMode, EnvironmentUpdateMode.Realtime);
            Set(sky.updatePeriod, 0.5f);

            var clouds = Get<VolumetricClouds>(profile);
            Set(clouds.enable, true);
            Set(clouds.cloudControl, VolumetricClouds.CloudControl.Simple);
            Set(clouds.cloudSimpleMode, VolumetricClouds.CloudSimpleMode.Quality);
            clouds.cloudPreset = L.cloudPreset;
            SetPrivateOverride(clouds, "m_CloudPreset");
            Set(clouds.densityMultiplier, L.cloudDensity);
            Set(clouds.shapeFactor, L.cloudShapeFactor);
            Set(clouds.bottomAltitude, L.cloudBottom);
            Set(clouds.altitudeRange, L.cloudRange);
            Set(clouds.ambientLightProbeDimmer, L.cloudAmbientDimmer);
            Set(clouds.sunLightDimmer, L.cloudSunDimmer);
            Set(clouds.scatteringTint, L.cloudScatterTint);
            Set(clouds.shadows, L.cloudShadows);
            Set(clouds.shadowOpacity, L.cloudShadowOpacity);
            Set(clouds.shadowDistance, 8000f);
            Set(clouds.shadowResolution, VolumetricClouds.CloudShadowResolution.Medium256);
            Set(clouds.numPrimarySteps, 64);
            Set(clouds.numLightSteps, 8);
            Set(clouds.temporalAccumulationFactor, 0.95f);
            Set(clouds.ghostingReduction, true);

            // --- fog ---------------------------------------------------------------------------------------------
            var fog = Get<Fog>(profile);
            Set(fog.enabled, true);
            Set(fog.colorMode, L.fogConstant ? FogColorMode.ConstantColor : FogColorMode.SkyColor);
            Set(fog.color, L.fogConstant ? L.fogColor : Color.grey);
            Set(fog.tint, L.fogTint);
            Set(fog.maxFogDistance, L.fogMaxDistance);
            Set(fog.mipFogMaxMip, 0.5f);
            Set(fog.mipFogNear, 0f);
            Set(fog.mipFogFar, 1000f);
            Set(fog.baseHeight, L.fogBaseHeight);
            Set(fog.maximumHeight, L.fogMaxHeight);
            Set(fog.meanFreePath, L.fogMeanFreePath);
            Set(fog.enableVolumetricFog, true);
            Set(fog.albedo, L.fogAlbedo);
            Set(fog.anisotropy, L.fogAnisotropy);
            Set(fog.globalLightProbeDimmer, L.fogLightProbeDimmer);
            Set(fog.depthExtent, L.fogDepthExtent);
            Set(fog.sliceDistributionUniformity, 0.7f);
            Set(fog.denoisingMode, FogDenoisingMode.Both);
            // Volumetric buffer size, set here on purpose: the project default was half the screen resolution with 96
            // slices (HDRP's own default is 1/8 and 64). Measured Nov 20 at the substation: 36 ms of GPU time with
            // the old value, 13 ms with this one, and the same picture (reprojection + Gaussian denoise hide the grid).
            Set(fog.quality, 3);                                    // Custom: use the values below, not the quality tier
            fog.fogControlMode = FogControl.Manual;
            SetPrivateOverride(fog, "m_FogControlMode");
            Set(fog.screenResolutionPercentage, 16.67f);
            Set(fog.volumeSliceCount, 64);

            // --- exposure ----------------------------------------------------------------------------------------
            var ex = Get<Exposure>(profile);
            Set(ex.mode, ExposureMode.AutomaticHistogram);
            Set(ex.meteringMode, MeteringMode.CenterWeighted);
            Set(ex.luminanceSource, LuminanceSource.ColorBuffer);
            Set(ex.limitMin, L.evMin);
            Set(ex.limitMax, L.evMax);
            Set(ex.compensation, L.evCompensation);
            Set(ex.histogramPercentages, new Vector2(40f, 88f));
            Set(ex.adaptationMode, AdaptationMode.Progressive);
            Set(ex.adaptationSpeedDarkToLight, L.adaptDarkToLight);
            Set(ex.adaptationSpeedLightToDark, L.adaptLightToDark);
            Set(ex.targetMidGray, TargetMidGray.Grey125);

            // --- shadows -----------------------------------------------------------------------------------------
            var sh = Get<HDShadowSettings>(profile);
            Set(sh.maxShadowDistance, L.shadowDistance);
            Set(sh.cascadeShadowSplitCount, 4);
            var contact = Get<ContactShadows>(profile);
            Set(contact.enable, true);
            Set(contact.length, 0.2f);
            Set(contact.opacity, 0.9f);
            Set(contact.maxDistance, 60f);
            Set(contact.fadeDistance, 8f);
            var micro = Get<MicroShadowing>(profile);
            Set(micro.enable, true);
            Set(micro.opacity, 0.8f);

            // --- screen-space lighting ---------------------------------------------------------------------------
            var ao = Get<ScreenSpaceAmbientOcclusion>(profile);
            Set(ao.intensity, L.aoIntensity);
            Set(ao.radius, L.aoRadius);
            Set(ao.directLightingStrength, 0.25f);
            var gi = Get<GlobalIllumination>(profile);
            Set(gi.enable, true);
            var ssr = Get<ScreenSpaceReflection>(profile);
            Set(ssr.enabled, true);
            Set(ssr.enabledTransparent, true);
            // wet ground must mirror lamps and sky, not only the probes
            Set(ssr.quality, 3);                                    // Custom: the two thresholds below are ours
            ssr.minSmoothness = 0.70f;
            SetPrivateOverride(ssr, "m_MinSmoothness");
            ssr.smoothnessFadeStart = 0.76f;
            SetPrivateOverride(ssr, "m_SmoothnessFadeStart");
            ssr.rayMaxIterations = 64;
            SetPrivateOverride(ssr, "m_RayMaxIterations");
            var ilc = Get<IndirectLightingController>(profile);
            Set(ilc.indirectDiffuseLightingMultiplier, L.indirectDiffuse);
            Set(ilc.reflectionLightingMultiplier, L.indirectReflection);

            // --- grade -------------------------------------------------------------------------------------------
            var tone = Get<Tonemapping>(profile);
            Set(tone.mode, TonemappingMode.Custom);
            Set(tone.toeStrength, L.toeStrength);
            Set(tone.toeLength, L.toeLength);
            Set(tone.shoulderStrength, L.shoulderStrength);
            Set(tone.shoulderLength, L.shoulderLength);
            Set(tone.shoulderAngle, L.shoulderAngle);
            Set(tone.gamma, 1f);
            var wb = Get<WhiteBalance>(profile);
            Set(wb.temperature, L.temperature);
            Set(wb.tint, L.tint);
            var ca = Get<ColorAdjustments>(profile);
            Set(ca.postExposure, L.postExposure);
            Set(ca.contrast, L.contrast);
            Set(ca.saturation, L.saturation);
            Set(ca.colorFilter, L.colorFilter);
            var smh = Get<ShadowsMidtonesHighlights>(profile);
            Set(smh.shadows, L.smhShadows);
            Set(smh.midtones, L.smhMidtones);
            Set(smh.highlights, L.smhHighlights);
            var lgg = Get<LiftGammaGain>(profile);
            Set(lgg.lift, new Vector4(1f, 1f, 1f, L.blackLift));
            var split = Get<SplitToning>(profile);
            Set(split.shadows, L.splitShadows);
            Set(split.highlights, L.splitHighlights);
            Set(split.balance, L.splitBalance);

            // --- lens --------------------------------------------------------------------------------------------
            var bloom = Get<Bloom>(profile);
            Set(bloom.intensity, L.bloomIntensity);
            Set(bloom.scatter, L.bloomScatter);
            Set(bloom.threshold, L.bloomThreshold);
            var vig = Get<Vignette>(profile);
            Set(vig.intensity, L.vignette);
            Set(vig.smoothness, 0.42f);
            var grain = Get<FilmGrain>(profile);
            Set(grain.type, FilmGrainLookup.Thin2);
            Set(grain.intensity, L.grain);
            Set(grain.response, 0.75f);
            var chroma = Get<ChromaticAberration>(profile);
            Set(chroma.intensity, L.chromatic);
            var mb = Get<MotionBlur>(profile);
            Set(mb.intensity, L.motionBlur);
            Set(mb.maximumVelocity, 120f);
            Set(mb.minimumVelocity, 4f);

            // --- subsurface / translucency profiles in use: dinosaur skin and foliage ------------------------------
            var dino = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>("Assets/Settings/Rendering/SSS_DinoSkin.asset");
            var foliage = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>("Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/FoliageDiffusionProfile.asset");
            var skin = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>("Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/SkinDiffusionProfile.asset");
            var list = new System.Collections.Generic.List<DiffusionProfileSettings>();
            if (dino != null) list.Add(dino);
            if (foliage != null) list.Add(foliage);
            if (skin != null) list.Add(skin);
            var dpl = Get<DiffusionProfileList>(profile);
            dpl.diffusionProfiles.overrideState = true;
            dpl.diffusionProfiles.value = list.ToArray();

            // --- water (the ocean and the valley stream use the HDRP water system) --------------------------------
            var water = Get<WaterRendering>(profile);
            Set(water.enable, true);

            EditorUtility.SetDirty(profile);
        }

        // ------------------------------------------------------------------------------------------------------------
        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T c) && c != null) return c;
            c = profile.Add<T>(false);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        private static void Set<T>(VolumeParameter<T> p, T value)
        {
            p.overrideState = true;
            p.value = value;
        }

        private static void SetPrivateOverride(VolumeComponent c, string field)
        {
            var f = c.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.GetValue(c) is VolumeParameter p) p.overrideState = true;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>A small procedural star field for the night presets (space emission of the physically based sky).</summary>
        private static Cubemap EnsureStarsCubemap()
        {
            Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(StarsPath);
            if (cube != null) return cube;

            const int size = 1024;
            cube = new Cubemap(size, TextureFormat.RGBAHalf, false) { name = "Look_StarsCube", filterMode = FilterMode.Bilinear };
            var rng = new System.Random(19960611);
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            foreach (CubemapFace face in faces)
            {
                var px = new Color[size * size];
                int stars = 1500;
                for (int s = 0; s < stars; s++)
                {
                    int x = rng.Next(2, size - 2);
                    int y = rng.Next(2, size - 2);
                    // few bright stars, many faint ones; slight colour variety (blue-white to warm)
                    float mag = Mathf.Pow((float)rng.NextDouble(), 6f);
                    float lum = 0.04f + mag * 2.6f;
                    float hue = (float)rng.NextDouble();
                    Color c = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.88f, 0.72f), hue) * lum;
                    px[y * size + x] += c;
                    if (mag > 0.35f)
                    {
                        Color halo = c * 0.28f;
                        px[y * size + x + 1] += halo; px[y * size + x - 1] += halo;
                        px[(y + 1) * size + x] += halo; px[(y - 1) * size + x] += halo;
                    }
                }
                cube.SetPixels(px, face);
            }
            cube.Apply(false, false);
            AssetDatabase.CreateAsset(cube, StarsPath);
            return cube;
        }
    }
}
