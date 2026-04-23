using System;
using Unity.Mathematics;

namespace Star67.Tracking
{
    public enum SemanticHandFinger
    {
        Thumb = 0,
        Index = 1,
        Middle = 2,
        Ring = 3,
        Little = 4
    }

    [Serializable]
    public struct SemanticAngleRange
    {
        public float MinRadians;
        public float MaxRadians;

        public SemanticAngleRange(float minRadians, float maxRadians)
        {
            MinRadians = minRadians;
            MaxRadians = maxRadians;
        }

        public float Normalize01(float radians)
        {
            float span = MaxRadians - MinRadians;
            if (math.abs(span) < 1e-5f)
            {
                return 0f;
            }

            return math.clamp((radians - MinRadians) / span, 0f, 1f);
        }

        public float Denormalize01(float value)
        {
            return math.lerp(MinRadians, MaxRadians, math.clamp(value, 0f, 1f));
        }
    }

    [Serializable]
    public struct SemanticSignedAngleRange
    {
        public float NegativeRadians;
        public float PositiveRadians;

        public SemanticSignedAngleRange(float negativeRadians, float positiveRadians)
        {
            NegativeRadians = negativeRadians;
            PositiveRadians = positiveRadians;
        }

        public float NormalizeSigned(float radians)
        {
            if (radians >= 0f)
            {
                return PositiveRadians > 1e-5f ? math.clamp(radians / PositiveRadians, 0f, 1f) : 0f;
            }

            float magnitude = math.abs(NegativeRadians);
            return magnitude > 1e-5f ? -math.clamp(math.abs(radians) / magnitude, 0f, 1f) : 0f;
        }

        public float DenormalizeSigned(float value)
        {
            value = math.clamp(value, -1f, 1f);
            return value >= 0f ? value * PositiveRadians : -value * NegativeRadians;
        }
    }

    [Serializable]
    public struct SemanticFingerPose
    {
        public float McpCurl;
        public float PipCurl;
        public float DipCurl;
        public float McpSplay;

        public void Clamp()
        {
            McpCurl = math.clamp(McpCurl, 0f, 1f);
            PipCurl = math.clamp(PipCurl, 0f, 1f);
            DipCurl = math.clamp(DipCurl, 0f, 1f);
            McpSplay = math.clamp(McpSplay, -1f, 1f);
        }

        public static SemanticFingerPose Lerp(SemanticFingerPose from, SemanticFingerPose to, float t)
        {
            return new SemanticFingerPose
            {
                McpCurl = math.lerp(from.McpCurl, to.McpCurl, t),
                PipCurl = math.lerp(from.PipCurl, to.PipCurl, t),
                DipCurl = math.lerp(from.DipCurl, to.DipCurl, t),
                McpSplay = math.lerp(from.McpSplay, to.McpSplay, t)
            };
        }
    }

    [Serializable]
    public struct SemanticThumbPose
    {
        /// <summary>
        /// Normalized CMC-to-tip thumb direction in palm space: +X toward thumb side, +Y out of palm, +Z toward fingers.
        /// </summary>
        public float3 AimPalmSpace;

        /// <summary>
        /// Normalized CMC-to-MCP thumb segment direction in palm space.
        /// </summary>
        public float3 ProximalPalmSpace;

        /// <summary>
        /// Normalized MCP-to-IP thumb segment direction in palm space.
        /// </summary>
        public float3 IntermediatePalmSpace;

        /// <summary>
        /// Normalized IP-to-tip thumb segment direction in palm space.
        /// </summary>
        public float3 DistalPalmSpace;

        public float BaseCurl;
        public float TipCurl;

        public void Clamp()
        {
            AimPalmSpace = NormalizeDirection(AimPalmSpace);
            ProximalPalmSpace = NormalizeDirection(ProximalPalmSpace);
            IntermediatePalmSpace = NormalizeDirection(IntermediatePalmSpace);
            DistalPalmSpace = NormalizeDirection(DistalPalmSpace);
            BaseCurl = math.clamp(BaseCurl, 0f, 1f);
            TipCurl = math.clamp(TipCurl, 0f, 1f);
        }

        public static SemanticThumbPose Lerp(SemanticThumbPose from, SemanticThumbPose to, float t)
        {
            var pose = new SemanticThumbPose
            {
                AimPalmSpace = math.lerp(from.AimPalmSpace, to.AimPalmSpace, t),
                ProximalPalmSpace = math.lerp(from.ProximalPalmSpace, to.ProximalPalmSpace, t),
                IntermediatePalmSpace = math.lerp(from.IntermediatePalmSpace, to.IntermediatePalmSpace, t),
                DistalPalmSpace = math.lerp(from.DistalPalmSpace, to.DistalPalmSpace, t),
                BaseCurl = math.lerp(from.BaseCurl, to.BaseCurl, t),
                TipCurl = math.lerp(from.TipCurl, to.TipCurl, t)
            };
            pose.Clamp();
            return pose;
        }

        private static float3 NormalizeDirection(float3 value)
        {
            if (!math.all(math.isfinite(value)))
            {
                return float3.zero;
            }

            float lengthSq = math.lengthsq(value);
            return lengthSq > 1e-8f ? value * math.rsqrt(lengthSq) : float3.zero;
        }
    }

    [Serializable]
    public struct SemanticHandPoseSettings
    {
        public SemanticAngleRange FingerMcpCurlRange;
        public SemanticAngleRange FingerPipCurlRange;
        public SemanticAngleRange FingerDipCurlRange;
        public SemanticSignedAngleRange FingerMcpSplayRange;
        public float IndexNeutralSplayRadians;
        public float MiddleNeutralSplayRadians;
        public float RingNeutralSplayRadians;
        public float LittleNeutralSplayRadians;
        public SemanticAngleRange ThumbBaseCurlRange;
        public SemanticAngleRange ThumbTipCurlRange;

        public float GetNeutralSplayRadians(SemanticHandFinger finger)
        {
            switch (finger)
            {
                case SemanticHandFinger.Index:
                    return IndexNeutralSplayRadians;
                case SemanticHandFinger.Middle:
                    return MiddleNeutralSplayRadians;
                case SemanticHandFinger.Ring:
                    return RingNeutralSplayRadians;
                case SemanticHandFinger.Little:
                    return LittleNeutralSplayRadians;
                default:
                    return 0f;
            }
        }

        public static SemanticHandPoseSettings Default => new SemanticHandPoseSettings
        {
            FingerMcpCurlRange = new SemanticAngleRange(0f, 1.35f),
            FingerPipCurlRange = new SemanticAngleRange(0f, 1.75f),
            FingerDipCurlRange = new SemanticAngleRange(0f, 1.25f),
            FingerMcpSplayRange = new SemanticSignedAngleRange(-0.55f, 0.55f),
            IndexNeutralSplayRadians = 0.22f,
            MiddleNeutralSplayRadians = 0.04f,
            RingNeutralSplayRadians = -0.12f,
            LittleNeutralSplayRadians = -0.28f,
            ThumbBaseCurlRange = new SemanticAngleRange(0f, 1.15f),
            ThumbTipCurlRange = new SemanticAngleRange(0f, 1.35f)
        };
    }
}
