namespace Star67.Tracking
{
    public static class TrackingProtocol
    {
        public const byte ProtocolVersion = 5;
        public const int DataPort = 36767;
        public const int ControlPort = 36768;
        public const int DiscoveryPort = ControlPort;
        public const int FaceBlendshapeCount = 52;
        public const int MaxPacketSize = 1200;
        public const int DiscoveryIntervalMs = 1000;
        public const int SessionTimeoutMs = 2000;
        public const int RecordingQueueCapacity = 128;
        public const string RecordingMagic = "S67R";
        public const int RecordingFormatVersion = 4;
    }
}
