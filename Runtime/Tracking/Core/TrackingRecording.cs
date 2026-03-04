using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Unity.Mathematics;

namespace Star67.Tracking
{
    public sealed class TrackingRecordingHeader
    {
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public uint SessionToken { get; set; }
        public TrackingSessionInfo SessionInfo { get; set; } = new TrackingSessionInfo();
    }

    public sealed class TrackingRecordingWriter : ITrackingPacketSink, IDisposable
    {
        private sealed class QueueSlot
        {
            public byte[] Buffer;
            public int Length;
            public long CaptureDeltaUs;
        }

        private readonly object _sync = new object();
        private readonly QueueSlot[] _slots;
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);

        private FileStream _stream;
        private Thread _thread;
        private volatile bool _isRunning;
        private int _readIndex;
        private int _writeIndex;
        private int _count;
        private long? _firstCaptureTimestampUs;

        public TrackingRecordingWriter(int queueCapacity = TrackingProtocol.RecordingQueueCapacity)
        {
            _slots = new QueueSlot[queueCapacity];
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new QueueSlot();
            }
        }

        public int DroppedFrameCount { get; private set; }

        public void Start(string path, TrackingRecordingHeader header)
        {
            Stop();
            _firstCaptureTimestampUs = null;
            DroppedFrameCount = 0;
            _readIndex = 0;
            _writeIndex = 0;
            _count = 0;

            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            WriteHeader(_stream, header);

            _isRunning = true;
            _thread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "Star67TrackingRecordingWriter"
            };
            _thread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _signal.Set();

            if (_thread != null && _thread.IsAlive)
            {
                _thread.Join(1000);
            }

            _thread = null;
            _stream?.Dispose();
            _stream = null;

            for (int i = 0; i < _slots.Length; i++)
            {
                ReturnSlotBuffer(_slots[i]);
            }
        }

        public void OnFramePacket(ReadOnlySpan<byte> packet, long captureTimestampUs)
        {
            if (!_isRunning || packet.Length == 0)
            {
                return;
            }

            lock (_sync)
            {
                if (_count == _slots.Length)
                {
                    DroppedFrameCount++;
                    return;
                }

                QueueSlot slot = _slots[_writeIndex];
                if (slot.Buffer == null || slot.Buffer.Length < packet.Length)
                {
                    ReturnSlotBuffer(slot);
                    slot.Buffer = ArrayPool<byte>.Shared.Rent(packet.Length);
                }

                packet.CopyTo(slot.Buffer);
                slot.Length = packet.Length;
                if (!_firstCaptureTimestampUs.HasValue)
                {
                    _firstCaptureTimestampUs = captureTimestampUs;
                }

                slot.CaptureDeltaUs = captureTimestampUs - _firstCaptureTimestampUs.GetValueOrDefault();
                _writeIndex = (_writeIndex + 1) % _slots.Length;
                _count++;
            }

            _signal.Set();
        }

        public void Dispose()
        {
            Stop();
            _signal.Dispose();
        }

        private void WriterLoop()
        {
            while (_isRunning || HasPendingItems())
            {
                _signal.WaitOne(250);
                while (TryDequeue(out QueueSlot slot))
                {
                    WriteRecord(_stream, slot.CaptureDeltaUs, slot.Buffer, slot.Length);
                    ReturnSlotBuffer(slot);
                }
            }

            _stream?.Flush(true);
        }

        private bool HasPendingItems()
        {
            lock (_sync)
            {
                return _count > 0;
            }
        }

        private bool TryDequeue(out QueueSlot slot)
        {
            lock (_sync)
            {
                if (_count == 0)
                {
                    slot = null;
                    return false;
                }

                slot = _slots[_readIndex];
                _readIndex = (_readIndex + 1) % _slots.Length;
                _count--;
                return true;
            }
        }

        private static void WriteHeader(Stream stream, TrackingRecordingHeader header)
        {
            WriteAscii(stream, TrackingProtocol.RecordingMagic);
            WriteInt32(stream, TrackingProtocol.RecordingFormatVersion);
            WriteInt64(stream, header.CreatedUtc.ToBinary());
            WriteUInt32(stream, header.SessionToken);
            WriteByte(stream, header.SessionInfo?.ProtocolVersion ?? TrackingProtocol.ProtocolVersion);
            WriteString(stream, header.SessionInfo?.AppVersion ?? string.Empty);
            WriteString(stream, header.SessionInfo?.DeviceName ?? string.Empty);
            WriteUInt32(stream, (uint)(header.SessionInfo?.AvailableFeatures ?? TrackingFeatureFlags.None));
            WriteSingle(stream, header.SessionInfo?.NominalFps ?? 0f);
        }

        private static void WriteRecord(Stream stream, long captureDeltaUs, byte[] buffer, int length)
        {
            WriteInt64(stream, captureDeltaUs);
            WriteInt32(stream, length);
            stream.Write(buffer, 0, length);
        }

        private static void ReturnSlotBuffer(QueueSlot slot)
        {
            if (slot.Buffer != null)
            {
                ArrayPool<byte>.Shared.Return(slot.Buffer);
                slot.Buffer = null;
            }

            slot.Length = 0;
            slot.CaptureDeltaUs = 0;
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteString(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt32(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt32(Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteInt64(Stream stream, long value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteByte(Stream stream, byte value)
        {
            stream.WriteByte(value);
        }

        private static void WriteSingle(Stream stream, float value)
        {
            WriteInt32(stream, BitConverter.SingleToInt32Bits(value));
        }
    }

    public sealed class TrackingRecordingReader : IDisposable
    {
        private struct RecordIndex
        {
            public long PacketOffset;
            public int PacketLength;
            public long CaptureDeltaUs;
        }

        private readonly List<RecordIndex> _records = new List<RecordIndex>();
        private readonly byte[] _int32Buffer = new byte[4];
        private readonly byte[] _int64Buffer = new byte[8];
        private FileStream _stream;

        public TrackingRecordingReader(string path)
        {
            Open(path);
        }

        public TrackingRecordingHeader Header { get; private set; }
        public int FrameCount => _records.Count;
        public long DurationUs => _records.Count == 0 ? 0 : _records[_records.Count - 1].CaptureDeltaUs;

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }

        public long GetFrameTimeUs(int frameIndex)
        {
            return _records[frameIndex].CaptureDeltaUs;
        }

        public int ReadPacket(int frameIndex, byte[] destination, out long captureDeltaUs)
        {
            RecordIndex record = _records[frameIndex];
            captureDeltaUs = record.CaptureDeltaUs;
            _stream.Position = record.PacketOffset;
            return ReadExact(_stream, destination, 0, record.PacketLength);
        }

        private void Open(string path)
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Header = ReadHeader(_stream);

            while (_stream.Position < _stream.Length)
            {
                long captureDeltaUs = ReadInt64(_stream, _int64Buffer);
                int packetLength = ReadInt32(_stream, _int32Buffer);
                long packetOffset = _stream.Position;
                _records.Add(new RecordIndex
                {
                    PacketOffset = packetOffset,
                    PacketLength = packetLength,
                    CaptureDeltaUs = captureDeltaUs
                });
                _stream.Position += packetLength;
            }
        }

        private static TrackingRecordingHeader ReadHeader(Stream stream)
        {
            string magic = ReadAscii(stream, 4);
            if (!string.Equals(magic, TrackingProtocol.RecordingMagic, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Invalid Star67 tracking recording magic.");
            }

            int formatVersion = ReadInt32(stream, new byte[4]);
            if (formatVersion != TrackingProtocol.RecordingFormatVersion)
            {
                throw new InvalidDataException($"Unsupported Star67 tracking recording version: {formatVersion}.");
            }

            var header = new TrackingRecordingHeader
            {
                CreatedUtc = DateTime.FromBinary(ReadInt64(stream, new byte[8])),
                SessionToken = ReadUInt32(stream, new byte[4]),
                SessionInfo = new TrackingSessionInfo()
            };
            header.SessionInfo.ProtocolVersion = ReadByte(stream);
            header.SessionInfo.AppVersion = ReadString(stream);
            header.SessionInfo.DeviceName = ReadString(stream);
            header.SessionInfo.AvailableFeatures = (TrackingFeatureFlags)ReadUInt32(stream, new byte[4]);
            header.SessionInfo.NominalFps = BitConverter.Int32BitsToSingle(ReadInt32(stream, new byte[4]));
            return header;
        }

        private static int ReadExact(Stream stream, byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = stream.Read(buffer, offset + totalRead, length - totalRead);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                totalRead += read;
            }

            return totalRead;
        }

        private static string ReadAscii(Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            ReadExact(stream, buffer, 0, length);
            return Encoding.ASCII.GetString(buffer);
        }

        private static string ReadString(Stream stream)
        {
            int length = ReadInt32(stream, new byte[4]);
            byte[] buffer = new byte[length];
            ReadExact(stream, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        private static int ReadInt32(Stream stream, byte[] buffer)
        {
            ReadExact(stream, buffer, 0, 4);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        private static uint ReadUInt32(Stream stream, byte[] buffer)
        {
            ReadExact(stream, buffer, 0, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        private static long ReadInt64(Stream stream, byte[] buffer)
        {
            ReadExact(stream, buffer, 0, 8);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        private static byte ReadByte(Stream stream)
        {
            int value = stream.ReadByte();
            if (value < 0)
            {
                throw new EndOfStreamException();
            }

            return (byte)value;
        }
    }

    public static class TrackingFrameInterpolator
    {
        public static void Interpolate(TrackingFrameBuffer from, TrackingFrameBuffer to, float t, TrackingFrameBuffer destination)
        {
            if (from == null)
            {
                destination.Clear();
                return;
            }

            if (to == null || t <= 0f)
            {
                destination.CopyFrom(from);
                return;
            }

            if (t >= 1f)
            {
                destination.CopyFrom(to);
                return;
            }

            destination.CopyFrom(from);
            destination.Sequence = from.Sequence;
            destination.CaptureTimestampUs = from.CaptureTimestampUs + (long)((to.CaptureTimestampUs - from.CaptureTimestampUs) * t);
            destination.Features = from.Features | to.Features;
            destination.CameraWorldPose = LerpPose(from.CameraWorldPose, to.CameraWorldPose, t);
            destination.HeadPoseCameraSpace = LerpPose(from.HeadPoseCameraSpace, to.HeadPoseCameraSpace, t);

            for (int i = 0; i < TrackingProtocol.FaceBlendshapeCount; i++)
            {
                destination.FaceBlendshapes[i].weight = math.lerp(from.FaceBlendshapes[i].weight, to.FaceBlendshapes[i].weight, t);
            }

            InterpolateHand(from.LeftHand, to.LeftHand, t, destination.LeftHand);
            InterpolateHand(from.RightHand, to.RightHand, t, destination.RightHand);
        }

        private static TrackingPose LerpPose(TrackingPose from, TrackingPose to, float t)
        {
            return new TrackingPose
            {
                Position = math.lerp(from.Position, to.Position, t),
                Rotation = math.slerp(from.Rotation, to.Rotation, t)
            };
        }

        private static void InterpolateHand(TrackedHandData from, TrackedHandData to, float t, TrackedHandData destination)
        {
            if (from == null || !from.IsTracked)
            {
                if (to != null && to.IsTracked)
                {
                    destination.CopyFrom(to);
                }
                else
                {
                    destination.Clear();
                }

                return;
            }

            if (to == null || !to.IsTracked)
            {
                destination.CopyFrom(from);
                return;
            }

            destination.IsTracked = true;
            destination.Confidence = math.lerp(from.Confidence, to.Confidence, t);
            for (int i = 0; i < TrackingProtocol.HandJointCount; i++)
            {
                destination.JointPositions[i] = math.lerp(from.JointPositions[i], to.JointPositions[i], t);
            }
        }
    }

    /// <summary>
    /// Playback source that reads recorded frame packets from a <c>.s67trk</c> file.
    /// </summary>
    public sealed class TrackingRecordingPlayer : ITrackingFrameSource
    {
        private readonly object _sync = new object();
        private readonly TrackingRecordingReader _reader;
        private readonly TrackingFrameBuffer _lowerFrame = new TrackingFrameBuffer();
        private readonly TrackingFrameBuffer _upperFrame = new TrackingFrameBuffer();
        private readonly TrackingFrameBuffer _currentFrame = new TrackingFrameBuffer();
        private readonly byte[] _packetBuffer = new byte[TrackingProtocol.MaxPacketSize];
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private double _baseTimeSeconds;
        private int _lowerIndex = -1;
        private int _upperIndex = -1;
        private bool _hasFrame;

        /// <summary>
        /// Opens a recording file and prepares playback buffers.
        /// </summary>
        /// <param name="path">Path to a <c>.s67trk</c> recording.</param>
        public TrackingRecordingPlayer(string path)
        {
            _reader = new TrackingRecordingReader(path);
            SessionInfo = _reader.Header.SessionInfo.Clone();
            State = TrackingConnectionState.Playback;
            Seek(0f);
        }

        /// <inheritdoc />
        public TrackingConnectionState State { get; private set; }

        /// <inheritdoc />
        public TrackingSessionInfo SessionInfo { get; }

        /// <summary>
        /// Gets whether playback is actively advancing.
        /// </summary>
        public bool IsPlaying => _stopwatch.IsRunning;

        /// <summary>
        /// Gets or sets whether playback wraps to the beginning after reaching the end.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Gets total recording duration in seconds.
        /// </summary>
        public float DurationSeconds => _reader.DurationUs / 1_000_000f;

        /// <summary>
        /// Gets the current playback time in seconds.
        /// </summary>
        public float CurrentTimeSeconds { get; private set; }

        /// <summary>
        /// Starts playback from the current time.
        /// </summary>
        public void Play()
        {
            if (_reader.FrameCount == 0 || _stopwatch.IsRunning)
            {
                return;
            }

            _stopwatch.Restart();
        }

        /// <summary>
        /// Pauses playback and preserves current playback time.
        /// </summary>
        public void Pause()
        {
            if (!_stopwatch.IsRunning)
            {
                return;
            }

            _baseTimeSeconds = CurrentTimeSeconds;
            _stopwatch.Stop();
        }

        /// <summary>
        /// Seeks to a normalized recording position.
        /// </summary>
        /// <param name="normalizedTime">Normalized time in [0, 1].</param>
        public void Seek(float normalizedTime)
        {
            Pause();
            float duration = DurationSeconds;
            if (duration <= 0f)
            {
                CurrentTimeSeconds = 0f;
                _baseTimeSeconds = 0d;
                DecodeAtTime(0f);
                return;
            }

            CurrentTimeSeconds = math.clamp(normalizedTime, 0f, 1f) * duration;
            _baseTimeSeconds = CurrentTimeSeconds;
            DecodeAtTime(CurrentTimeSeconds);
        }

        /// <inheritdoc />
        public void Update()
        {
            if (_reader.FrameCount == 0)
            {
                return;
            }

            if (_stopwatch.IsRunning)
            {
                CurrentTimeSeconds = (float)(_baseTimeSeconds + _stopwatch.Elapsed.TotalSeconds);
                if (DurationSeconds > 0f && CurrentTimeSeconds > DurationSeconds)
                {
                    if (Loop)
                    {
                        CurrentTimeSeconds %= DurationSeconds;
                        _baseTimeSeconds = CurrentTimeSeconds;
                        _stopwatch.Restart();
                    }
                    else
                    {
                        CurrentTimeSeconds = DurationSeconds;
                        Pause();
                    }
                }
            }

            DecodeAtTime(CurrentTimeSeconds);
        }

        /// <inheritdoc />
        public bool TryCopyLatestFrame(TrackingFrameBuffer destination)
        {
            if (destination == null)
            {
                return false;
            }

            lock (_sync)
            {
                if (!_hasFrame)
                {
                    return false;
                }

                destination.CopyFrom(_currentFrame);
                return true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _reader.Dispose();
        }

        private void DecodeAtTime(float timeSeconds)
        {
            long targetTimeUs = (long)(math.max(0f, timeSeconds) * 1_000_000f);
            int index = FindLowerFrameIndex(targetTimeUs);
            if (index < 0)
            {
                return;
            }

            if (index != _lowerIndex)
            {
                LoadFrame(index, _lowerFrame);
                _lowerIndex = index;
            }

            int nextIndex = math.min(index + 1, _reader.FrameCount - 1);
            if (nextIndex != _upperIndex)
            {
                LoadFrame(nextIndex, _upperFrame);
                _upperIndex = nextIndex;
            }

            float t = 0f;
            if (_upperIndex > _lowerIndex)
            {
                long lowerTime = _reader.GetFrameTimeUs(_lowerIndex);
                long upperTime = _reader.GetFrameTimeUs(_upperIndex);
                if (upperTime > lowerTime)
                {
                    t = math.clamp((targetTimeUs - lowerTime) / (float)(upperTime - lowerTime), 0f, 1f);
                }
            }

            lock (_sync)
            {
                TrackingFrameInterpolator.Interpolate(_lowerFrame, _upperFrame, t, _currentFrame);
                _hasFrame = true;
            }
        }

        private void LoadFrame(int index, TrackingFrameBuffer destination)
        {
            int packetLength = _reader.ReadPacket(index, _packetBuffer, out _);
            if (!TrackingPacketCodec.TryReadFramePacket(new ReadOnlySpan<byte>(_packetBuffer, 0, packetLength), destination))
            {
                throw new InvalidDataException($"Failed to decode frame packet {index} from recording.");
            }
        }

        private int FindLowerFrameIndex(long targetTimeUs)
        {
            if (_reader.FrameCount == 0)
            {
                return -1;
            }

            int low = 0;
            int high = _reader.FrameCount - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                long midTime = _reader.GetFrameTimeUs(mid);
                if (midTime == targetTimeUs)
                {
                    return mid;
                }

                if (midTime < targetTimeUs)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return math.max(0, high);
        }
    }
}
