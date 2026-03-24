using System;
using System.Buffers.Binary;
using System.Text;
using Unity.Mathematics;

namespace Star67.Tracking
{
    public static class TrackingPacketCodec
    {
        public static bool TryReadPacketHeader(ReadOnlySpan<byte> source, out TrackingPacketType packetType, out uint sessionToken, out int offset)
        {
            packetType = default;
            sessionToken = 0;
            offset = 0;
            if (source.Length < 6 || source[0] != TrackingProtocol.ProtocolVersion)
            {
                return false;
            }

            packetType = (TrackingPacketType)source[1];
            offset = 2;
            sessionToken = ReadUInt32(source, ref offset);
            offset = 6;
            return true;
        }

        public static bool TryWriteConnectRequest(uint sessionToken, Span<byte> destination, out int written)
        {
            return TryWriteHeaderOnly(TrackingPacketType.ConnectRequest, sessionToken, destination, out written);
        }

        public static bool TryReadConnectRequest(ReadOnlySpan<byte> source, out uint sessionToken)
        {
            return TryReadHeaderOnly(source, TrackingPacketType.ConnectRequest, out sessionToken);
        }

        public static bool TryWriteConnectAck(uint sessionToken, Span<byte> destination, out int written)
        {
            return TryWriteHeaderOnly(TrackingPacketType.ConnectAck, sessionToken, destination, out written);
        }

        public static bool TryReadConnectAck(ReadOnlySpan<byte> source, out uint sessionToken)
        {
            return TryReadHeaderOnly(source, TrackingPacketType.ConnectAck, out sessionToken);
        }

        public static bool TryWriteRegisterReceiver(uint sessionToken, int receiverDataPort, Span<byte> destination, out int written)
        {
            written = 0;
            int offset = 0;
            if (!TryWriteHeader(TrackingPacketType.RegisterReceiver, sessionToken, destination, ref offset)
                || !TryWriteInt32(receiverDataPort, destination, ref offset))
            {
                return false;
            }

            written = offset;
            return true;
        }

        public static bool TryReadRegisterReceiver(ReadOnlySpan<byte> source, out uint sessionToken, out int receiverDataPort)
        {
            receiverDataPort = 0;
            if (!TryReadHeaderOnly(source, TrackingPacketType.RegisterReceiver, out sessionToken, out int offset)
                || !TryReadInt32(source, ref offset, out receiverDataPort))
            {
                return false;
            }

            return offset == source.Length;
        }

        public static bool TryWriteHelloPacket(uint sessionToken, TrackingSessionInfo sessionInfo, Span<byte> destination, out int written)
        {
            written = 0;
            int offset = 0;
            if (!TryWriteHeader(TrackingPacketType.Hello, sessionToken, destination, ref offset))
            {
                return false;
            }

            if (!TryWriteByte(sessionInfo?.ProtocolVersion ?? TrackingProtocol.ProtocolVersion, destination, ref offset)
                || !TryWriteString(sessionInfo?.AppVersion ?? string.Empty, destination, ref offset)
                || !TryWriteString(sessionInfo?.DeviceName ?? string.Empty, destination, ref offset)
                || !TryWriteUInt32((uint)(sessionInfo?.AvailableFeatures ?? TrackingFeatureFlags.None), destination, ref offset)
                || !TryWriteSingle(sessionInfo?.NominalFps ?? 0f, destination, ref offset))
            {
                return false;
            }

            written = offset;
            return true;
        }

        public static bool TryReadHelloPacket(ReadOnlySpan<byte> source, out uint sessionToken, TrackingSessionInfo sessionInfo)
        {
            sessionToken = 0;
            if (!TryReadHeaderOnly(source, TrackingPacketType.Hello, out sessionToken, out int offset))
            {
                return false;
            }

            if (!TryReadByte(source, ref offset, out byte protocolVersion)
                || !TryReadString(source, ref offset, out string appVersion)
                || !TryReadString(source, ref offset, out string deviceName)
                || !TryReadUInt32(source, ref offset, out uint features)
                || !TryReadSingle(source, ref offset, out float nominalFps))
            {
                return false;
            }

            sessionInfo.ProtocolVersion = protocolVersion;
            sessionInfo.AppVersion = appVersion;
            sessionInfo.DeviceName = deviceName;
            sessionInfo.AvailableFeatures = (TrackingFeatureFlags)features;
            sessionInfo.NominalFps = nominalFps;
            return true;
        }

        public static bool TryWriteFramePacket(TrackingFrameBuffer frame, Span<byte> destination, out int written)
        {
            written = 0;
            if (frame == null)
            {
                return false;
            }

            int offset = 0;
            if (!TryWriteHeader(TrackingPacketType.Frame, frame.SessionToken, destination, ref offset)
                || !TryWriteUInt64(frame.Sequence, destination, ref offset)
                || !TryWriteInt64(frame.CaptureTimestampUs, destination, ref offset)
                || !TryWriteUInt32((uint)frame.Features, destination, ref offset)
                || !TryWritePose(frame.CameraWorldPose, destination, ref offset)
                || !TryWritePose(frame.HeadPoseCameraSpace, destination, ref offset))
            {
                return false;
            }

            for (int i = 0; i < TrackingProtocol.FaceBlendshapeCount; i++)
            {
                if (!TryWriteSingle(frame.FaceBlendshapes[i].weight, destination, ref offset))
                {
                    return false;
                }
            }

            if (HasTrackedHand(frame.Features, TrackingFeatureFlags.LeftHand, frame.LeftHand))
            {
                if (!TryWriteHand(frame.LeftHand, destination, ref offset))
                {
                    return false;
                }
            }

            if (HasTrackedHand(frame.Features, TrackingFeatureFlags.RightHand, frame.RightHand))
            {
                if (!TryWriteHand(frame.RightHand, destination, ref offset))
                {
                    return false;
                }
            }

            written = offset;
            return true;
        }

        public static bool TryReadFramePacket(ReadOnlySpan<byte> source, TrackingFrameBuffer destination)
        {
            if (destination == null || !TryReadHeaderOnly(source, TrackingPacketType.Frame, out uint sessionToken, out int offset))
            {
                return false;
            }

            destination.Clear();
            destination.SessionToken = sessionToken;

            if (!TryReadUInt64(source, ref offset, out ulong sequence)
                || !TryReadInt64(source, ref offset, out long captureTimestampUs)
                || !TryReadUInt32(source, ref offset, out uint features)
                || !TryReadPose(source, ref offset, out TrackingPose cameraWorldPose)
                || !TryReadPose(source, ref offset, out TrackingPose headPoseCameraSpace))
            {
                return false;
            }

            destination.Sequence = sequence;
            destination.CaptureTimestampUs = captureTimestampUs;
            destination.Features = (TrackingFeatureFlags)features;
            destination.CameraWorldPose = cameraWorldPose;
            destination.HeadPoseCameraSpace = headPoseCameraSpace;

            for (int i = 0; i < TrackingProtocol.FaceBlendshapeCount; i++)
            {
                if (!TryReadSingle(source, ref offset, out float weight))
                {
                    return false;
                }

                destination.FaceBlendshapes[i].weight = weight;
            }

            if ((destination.Features & TrackingFeatureFlags.LeftHand) != 0)
            {
                if (!TryReadHand(source, ref offset, destination.LeftHand))
                {
                    return false;
                }
            }

            if ((destination.Features & TrackingFeatureFlags.RightHand) != 0)
            {
                if (!TryReadHand(source, ref offset, destination.RightHand))
                {
                    return false;
                }
            }

            return offset == source.Length;
        }

        private static bool HasTrackedHand(TrackingFeatureFlags features, TrackingFeatureFlags handFlag, TrackedHandData hand)
        {
            return (features & handFlag) != 0 && hand != null && hand.IsTracked;
        }

        private static bool TryWriteHeaderOnly(TrackingPacketType packetType, uint sessionToken, Span<byte> destination, out int written)
        {
            int offset = 0;
            if (!TryWriteHeader(packetType, sessionToken, destination, ref offset))
            {
                written = 0;
                return false;
            }

            written = offset;
            return true;
        }

        private static bool TryReadHeaderOnly(ReadOnlySpan<byte> source, TrackingPacketType expectedType, out uint sessionToken)
        {
            return TryReadHeaderOnly(source, expectedType, out sessionToken, out _);
        }

        private static bool TryReadHeaderOnly(ReadOnlySpan<byte> source, TrackingPacketType expectedType, out uint sessionToken, out int offset)
        {
            sessionToken = 0;
            offset = 0;
            if (!TryReadPacketHeader(source, out TrackingPacketType packetType, out sessionToken, out offset))
            {
                return false;
            }

            return packetType == expectedType;
        }

        private static bool TryWriteHeader(TrackingPacketType packetType, uint sessionToken, Span<byte> destination, ref int offset)
        {
            return TryWriteByte(TrackingProtocol.ProtocolVersion, destination, ref offset)
                && TryWriteByte((byte)packetType, destination, ref offset)
                && TryWriteUInt32(sessionToken, destination, ref offset);
        }

        private static bool TryWritePose(TrackingPose pose, Span<byte> destination, ref int offset)
        {
            return TryWriteSingle(pose.Position.x, destination, ref offset)
                && TryWriteSingle(pose.Position.y, destination, ref offset)
                && TryWriteSingle(pose.Position.z, destination, ref offset)
                && TryWriteSingle(pose.Rotation.value.x, destination, ref offset)
                && TryWriteSingle(pose.Rotation.value.y, destination, ref offset)
                && TryWriteSingle(pose.Rotation.value.z, destination, ref offset)
                && TryWriteSingle(pose.Rotation.value.w, destination, ref offset);
        }

        private static bool TryReadPose(ReadOnlySpan<byte> source, ref int offset, out TrackingPose pose)
        {
            pose = TrackingPose.Identity;
            if (!TryReadSingle(source, ref offset, out float px)
                || !TryReadSingle(source, ref offset, out float py)
                || !TryReadSingle(source, ref offset, out float pz)
                || !TryReadSingle(source, ref offset, out float rx)
                || !TryReadSingle(source, ref offset, out float ry)
                || !TryReadSingle(source, ref offset, out float rz)
                || !TryReadSingle(source, ref offset, out float rw))
            {
                return false;
            }

            pose = new TrackingPose
            {
                Position = new float3(px, py, pz),
                Rotation = math.normalize(new quaternion(rx, ry, rz, rw))
            };
            return true;
        }

        private static bool TryWriteHand(TrackedHandData hand, Span<byte> destination, ref int offset)
        {
            if (!TryWriteSingle(hand.Confidence, destination, ref offset))
            {
                return false;
            }

            for (int i = 0; i < TrackingProtocol.HandJointCount; i++)
            {
                float3 joint = hand.JointPositions[i];
                if (!TryWriteSingle(joint.x, destination, ref offset)
                    || !TryWriteSingle(joint.y, destination, ref offset)
                    || !TryWriteSingle(joint.z, destination, ref offset))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadHand(ReadOnlySpan<byte> source, ref int offset, TrackedHandData hand)
        {
            hand.Clear();
            if (!TryReadSingle(source, ref offset, out float confidence))
            {
                return false;
            }

            hand.IsTracked = true;
            hand.Confidence = confidence;
            for (int i = 0; i < TrackingProtocol.HandJointCount; i++)
            {
                if (!TryReadSingle(source, ref offset, out float x)
                    || !TryReadSingle(source, ref offset, out float y)
                    || !TryReadSingle(source, ref offset, out float z))
                {
                    return false;
                }

                hand.JointPositions[i] = new float3(x, y, z);
            }

            return true;
        }

        private static bool TryWriteString(string value, Span<byte> destination, ref int offset)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > ushort.MaxValue || destination.Length - offset < bytes.Length + 2)
            {
                return false;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), (ushort)bytes.Length);
            offset += 2;
            bytes.AsSpan().CopyTo(destination.Slice(offset, bytes.Length));
            offset += bytes.Length;
            return true;
        }

        private static bool TryReadString(ReadOnlySpan<byte> source, ref int offset, out string value)
        {
            value = string.Empty;
            if (!TryReadUInt16(source, ref offset, out ushort length) || source.Length - offset < length)
            {
                return false;
            }

            value = Encoding.UTF8.GetString(source.Slice(offset, length).ToArray());
            offset += length;
            return true;
        }

        private static bool TryWriteByte(byte value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 1)
            {
                return false;
            }

            destination[offset++] = value;
            return true;
        }

        private static bool TryReadByte(ReadOnlySpan<byte> source, ref int offset, out byte value)
        {
            value = 0;
            if (source.Length - offset < 1)
            {
                return false;
            }

            value = source[offset++];
            return true;
        }

        private static bool TryWriteUInt32(uint value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 4)
            {
                return false;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
            offset += 4;
            return true;
        }

        private static bool TryReadUInt32(ReadOnlySpan<byte> source, ref int offset, out uint value)
        {
            value = 0;
            if (source.Length - offset < 4)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
            offset += 4;
            return true;
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> source, ref int offset)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
            offset += 4;
            return value;
        }

        private static bool TryWriteUInt64(ulong value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 8)
            {
                return false;
            }

            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, 8), value);
            offset += 8;
            return true;
        }

        private static bool TryReadUInt64(ReadOnlySpan<byte> source, ref int offset, out ulong value)
        {
            value = 0;
            if (source.Length - offset < 8)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, 8));
            offset += 8;
            return true;
        }

        private static bool TryWriteInt64(long value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 8)
            {
                return false;
            }

            BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, 8), value);
            offset += 8;
            return true;
        }

        private static bool TryReadInt64(ReadOnlySpan<byte> source, ref int offset, out long value)
        {
            value = 0;
            if (source.Length - offset < 8)
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt64LittleEndian(source.Slice(offset, 8));
            offset += 8;
            return true;
        }

        private static bool TryWriteUInt16(ushort value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 2)
            {
                return false;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), value);
            offset += 2;
            return true;
        }

        private static bool TryReadUInt16(ReadOnlySpan<byte> source, ref int offset, out ushort value)
        {
            value = 0;
            if (source.Length - offset < 2)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));
            offset += 2;
            return true;
        }

        private static bool TryWriteSingle(float value, Span<byte> destination, ref int offset)
        {
            return TryWriteInt32(BitConverter.SingleToInt32Bits(value), destination, ref offset);
        }

        private static bool TryReadSingle(ReadOnlySpan<byte> source, ref int offset, out float value)
        {
            value = 0f;
            if (!TryReadInt32(source, ref offset, out int bits))
            {
                return false;
            }

            value = BitConverter.Int32BitsToSingle(bits);
            return true;
        }

        private static bool TryWriteInt32(int value, Span<byte> destination, ref int offset)
        {
            if (destination.Length - offset < 4)
            {
                return false;
            }

            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), value);
            offset += 4;
            return true;
        }

        private static bool TryReadInt32(ReadOnlySpan<byte> source, ref int offset, out int value)
        {
            value = 0;
            if (source.Length - offset < 4)
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, 4));
            offset += 4;
            return true;
        }
    }
}
