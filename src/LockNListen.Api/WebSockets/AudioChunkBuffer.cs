using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace LockNListen.Api.WebSockets
{
    public class AudioChunkBuffer
    {
        private const int FrameSize = 960; // 960 samples at 16kHz = 60ms
        private readonly List<byte[]> _chunks = new();
        private int _totalBytes = 0;
        private readonly int _targetBytes;

        public AudioChunkBuffer(int targetFrameCount = 1)
        {
            _targetBytes = targetFrameCount * FrameSize * sizeof(short); // 16-bit samples
        }

        public void Append(byte[] chunk)
        {
            // Reject partial frames (chunks not exactly 1920 bytes)
            if (chunk.Length != 1920)
            {
                Reset(); // Reset buffer on partial frame
                return;
            }
            
            _chunks.Add(chunk);
            _totalBytes += chunk.Length;
        }

        public bool IsFull => _totalBytes >= _targetBytes;

        public Stream GetStream()
        {
            if (_chunks.Count == 0)
                return Stream.Null;

            var totalBytes = _totalBytes;
            var buffer = ArrayPool<byte>.Shared.Rent(totalBytes);
            var offset = 0;

            foreach (var chunk in _chunks)
            {
                Array.Copy(chunk, 0, buffer, offset, chunk.Length);
                offset += chunk.Length;
            }

            return new MemoryStream(buffer, 0, totalBytes, false, true);
        }

        public void Reset()
        {
            _chunks.Clear();
            _totalBytes = 0;
        }
    }
}