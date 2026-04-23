using System;
using Unity.Mathematics;
using UnityEngine;

namespace Star67.Tracking.Unity
{
    [Serializable]
    public struct SemanticHandTemporalChannelSettings
    {
        [Min(0.0001f)] public float StableHalfLife;
        [Min(0.0001f)] public float ResponsiveHalfLife;
        [Min(0.0001f)] public float LowConfidenceHalfLife;
        [Min(0f)] public float MotionLow;
        [Min(0f)] public float MotionHigh;

        public bool IsConfigured =>
            StableHalfLife > 1e-5f &&
            ResponsiveHalfLife > 1e-5f &&
            LowConfidenceHalfLife > 1e-5f &&
            MotionHigh > 1e-5f;

        public static SemanticHandTemporalChannelSettings Resolve(
            SemanticHandTemporalChannelSettings value,
            SemanticHandTemporalChannelSettings fallback)
        {
            if (!value.IsConfigured)
            {
                value = fallback;
            }

            value.StableHalfLife = Mathf.Max(0.0001f, value.StableHalfLife);
            value.ResponsiveHalfLife = Mathf.Max(0.0001f, value.ResponsiveHalfLife);
            value.LowConfidenceHalfLife = Mathf.Max(0.0001f, value.LowConfidenceHalfLife);
            value.MotionLow = Mathf.Max(0f, value.MotionLow);
            value.MotionHigh = Mathf.Max(value.MotionLow + 1e-4f, value.MotionHigh);
            return value;
        }
    }

    [Serializable]
    public struct SemanticHandTemporalFilterSettings
    {
        [Range(0f, 1f)] public float LowConfidenceThreshold;
        [Min(0f)] public float HoldDurationSeconds;
        [Min(0f)] public float RelaxDurationSeconds;
        [Min(0.001f)] public float MaxDeltaTimeSeconds;

        public SemanticHandTemporalChannelSettings WristPosition;
        public SemanticHandTemporalChannelSettings WristRotation;
        public SemanticHandTemporalChannelSettings FingerCurl;
        public SemanticHandTemporalChannelSettings FingerSplay;
        public SemanticHandTemporalChannelSettings ThumbCurl;
        public SemanticHandTemporalChannelSettings ThumbDirection;

        public SemanticThumbPose RelaxedThumb;

        public static SemanticHandTemporalFilterSettings Resolve(SemanticHandTemporalFilterSettings value)
        {
            SemanticHandTemporalFilterSettings defaults = Default;
            value.LowConfidenceThreshold = IsFinite(value.LowConfidenceThreshold)
                ? Mathf.Clamp01(value.LowConfidenceThreshold)
                : defaults.LowConfidenceThreshold;
            value.HoldDurationSeconds = IsFinite(value.HoldDurationSeconds)
                ? Mathf.Max(0f, value.HoldDurationSeconds)
                : defaults.HoldDurationSeconds;
            value.RelaxDurationSeconds = IsFinite(value.RelaxDurationSeconds)
                ? Mathf.Max(0f, value.RelaxDurationSeconds)
                : defaults.RelaxDurationSeconds;
            value.MaxDeltaTimeSeconds = IsFinite(value.MaxDeltaTimeSeconds)
                ? Mathf.Max(0.001f, value.MaxDeltaTimeSeconds)
                : defaults.MaxDeltaTimeSeconds;
            value.WristPosition = SemanticHandTemporalChannelSettings.Resolve(value.WristPosition, defaults.WristPosition);
            value.WristRotation = SemanticHandTemporalChannelSettings.Resolve(value.WristRotation, defaults.WristRotation);
            value.FingerCurl = SemanticHandTemporalChannelSettings.Resolve(value.FingerCurl, defaults.FingerCurl);
            value.FingerSplay = SemanticHandTemporalChannelSettings.Resolve(value.FingerSplay, defaults.FingerSplay);
            value.ThumbCurl = SemanticHandTemporalChannelSettings.Resolve(value.ThumbCurl, defaults.ThumbCurl);
            value.ThumbDirection = SemanticHandTemporalChannelSettings.Resolve(value.ThumbDirection, defaults.ThumbDirection);

            if (!math.all(math.isfinite(value.RelaxedThumb.AimPalmSpace))
                || !math.all(math.isfinite(value.RelaxedThumb.ProximalPalmSpace))
                || !math.all(math.isfinite(value.RelaxedThumb.IntermediatePalmSpace))
                || !math.all(math.isfinite(value.RelaxedThumb.DistalPalmSpace))
                || !IsFinite(value.RelaxedThumb.BaseCurl)
                || !IsFinite(value.RelaxedThumb.TipCurl))
            {
                value.RelaxedThumb = defaults.RelaxedThumb;
            }

            value.RelaxedThumb.Clamp();
            return value;
        }

        public static SemanticHandTemporalFilterSettings Default => new SemanticHandTemporalFilterSettings
        {
            LowConfidenceThreshold = 0.45f,
            HoldDurationSeconds = 0.12f,
            RelaxDurationSeconds = 0.22f,
            MaxDeltaTimeSeconds = 0.2f,
            WristPosition = new SemanticHandTemporalChannelSettings
            {
                StableHalfLife = 0.045f,
                ResponsiveHalfLife = 0.018f,
                LowConfidenceHalfLife = 0.14f,
                MotionLow = 0.02f,
                MotionHigh = 0.4f
            },
            WristRotation = new SemanticHandTemporalChannelSettings
            {
                StableHalfLife = 0.05f,
                ResponsiveHalfLife = 0.02f,
                LowConfidenceHalfLife = 0.15f,
                MotionLow = 20f,
                MotionHigh = 240f
            },
            FingerCurl = new SemanticHandTemporalChannelSettings
            {
                StableHalfLife = 0.08f,
                ResponsiveHalfLife = 0.03f,
                LowConfidenceHalfLife = 0.2f,
                MotionLow = 0.2f,
                MotionHigh = 2.5f
            },
            FingerSplay = new SemanticHandTemporalChannelSettings
            {
                StableHalfLife = 0.11f,
                ResponsiveHalfLife = 0.04f,
                LowConfidenceHalfLife = 0.24f,
                MotionLow = 0.15f,
                MotionHigh = 1.8f
            },
            ThumbCurl = new SemanticHandTemporalChannelSettings
            {
                StableHalfLife = 0.085f,
                ResponsiveHalfLife = 0.03f,
                LowConfidenceHalfLife = 0.22f,
                MotionLow = 0.18f,
                MotionHigh = 2.4f
            },
            ThumbDirection = new SemanticHandTemporalChannelSettings
            {
                StableHalfLife = 0.08f,
                ResponsiveHalfLife = 0.028f,
                LowConfidenceHalfLife = 0.2f,
                MotionLow = 18f,
                MotionHigh = 260f
            },
            RelaxedThumb = new SemanticThumbPose
            {
                AimPalmSpace = new float3(0.82f, 0.08f, 0.56f),
                ProximalPalmSpace = new float3(0.86f, 0.08f, 0.5f),
                IntermediatePalmSpace = new float3(0.76f, 0.05f, 0.64f),
                DistalPalmSpace = new float3(0.66f, 0.02f, 0.75f),
                BaseCurl = 0f,
                TipCurl = 0f
            }
        };

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
