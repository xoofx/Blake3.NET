// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Blake3
{
    /// <summary>
    /// A stream that allows to calculate a hash while reading/writing from a backend stream.
    ///
    /// Use the <see cref="ComputeHash()"/> or <see cref="ComputeHash(System.Span{byte})"/> methods to calculate the hash before disposing the stream
    /// </summary>
    public class Blake3Stream : Stream
    {
        private readonly Stream _stream;
        private readonly bool _dispose;
        private Hasher _hasher;

        /// <summary>
        /// Creates an instance of <see cref="Blake3Stream"/> using the specified backend stream.
        /// </summary>
        /// <param name="backendStream"></param>
        /// <param name="dispose">A boolean that indicates if this stream will dispose the backend stream. Default is true.</param>
        public Blake3Stream(Stream backendStream, bool dispose = true)
        {
            _stream = backendStream ?? throw new ArgumentNullException(nameof(backendStream));
            _dispose = dispose;
            _hasher = Hasher.New();
        }

        protected override void Dispose(bool disposing)
        {
            _hasher.Dispose();
            if (_dispose)
            {
                _stream.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            _hasher.Dispose();
            if (_dispose)
            {
                await _stream.DisposeAsync();
            }
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _stream.FlushAsync(cancellationToken);
        }

        public void ResetHash()
        {
            _hasher.Reset();
        }

        public Hash ComputeHash()
        {
            return _hasher.Finalize();
        }

        public void ComputeHash(Span<byte> hash)
        {
            _hasher.Finalize(hash);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = _stream.Read(buffer, offset, count);
            if (length > 0)
            {
                var span = new Span<byte>(buffer, offset, length);
                _hasher.Update(span);
            }
            return length;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var length = await _stream.ReadAsync(buffer, offset, count, cancellationToken);
            if (length > 0)
            {
                _hasher.Update(new Span<byte>(buffer, offset, length));
            }
            return length;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            var length = await _stream.ReadAsync(buffer, cancellationToken);
            if (length > 0)
            {
                _hasher.Update(buffer.Span);
            }
            return length;
        }

        public override int Read(Span<byte> buffer)
        {
            var length = _stream.Read(buffer);
            if (length > 0)
            {
                _hasher.Update(buffer);
            }
            return length;
        }

        public override unsafe int ReadByte()
        {
            var value = _stream.ReadByte();
            if (value < 0) return value;
            var bValue = (byte) value;
            var span = new ReadOnlySpan<byte>(&bValue, 1);
            _hasher.Update(span);
            return value;
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            if (count > 0)
            {
                var span = new Span<byte>(buffer, offset, count);
                _hasher.Update(span);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        { 
            await _stream.WriteAsync(buffer, offset, count, cancellationToken);
            if (count > 0)
            {
                _hasher.Update(new Span<byte>(buffer, offset, count));
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _stream.Write(buffer);
            _hasher.Update(buffer);
        }

        public override unsafe void WriteByte(byte value)
        {
            _stream.WriteByte(value);
            var span = new ReadOnlySpan<byte>(&value, 1);
            _hasher.Update(span);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }
        
        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }
    }
}
