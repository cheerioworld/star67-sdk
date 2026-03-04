using System;

namespace Star67
{
    /// <summary>
    /// Tracked blendshape weight of a single ARKit-compatible face channel.
    /// </summary>
    [Serializable]
    public struct FaceBlendshape
    {
        public FaceBlendshapeLocation location;
        public float weight;
    }

    /// <summary>
    /// ARKit-compatible face blendshape channels.
    /// </summary>
    [Serializable]
    public enum FaceBlendshapeLocation
    {
        BrowDownLeft,
        BrowDownRight,
        BrowInnerUp,
        BrowOuterUpLeft,
        BrowOuterUpRight,
        CheekPuff,
        CheekSquintLeft,
        CheekSquintRight,
        EyeBlinkLeft,
        EyeBlinkRight,
        EyeLookDownLeft,
        EyeLookDownRight,
        EyeLookInLeft,
        EyeLookInRight,
        EyeLookOutLeft,
        EyeLookOutRight,
        EyeLookUpLeft,
        EyeLookUpRight,
        EyeSquintLeft,
        EyeSquintRight,
        EyeWideLeft,
        EyeWideRight,
        JawForward,
        JawLeft,
        JawOpen,
        JawRight,
        MouthClose,
        MouthDimpleLeft,
        MouthDimpleRight,
        MouthFrownLeft,
        MouthFrownRight,
        MouthFunnel,
        MouthLeft,
        MouthLowerDownLeft,
        MouthLowerDownRight,
        MouthPressLeft,
        MouthPressRight,
        MouthPucker,
        MouthRight,
        MouthRollLower,
        MouthRollUpper,
        MouthShrugLower,
        MouthShrugUpper,
        MouthSmileLeft,
        MouthSmileRight,
        MouthStretchLeft,
        MouthStretchRight,
        MouthUpperUpLeft,
        MouthUpperUpRight,
        NoseSneerLeft,
        NoseSneerRight,
        TongueOut
    }
}
