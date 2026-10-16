/*
 * Copyright (c) 2026 kingcurryman
 *
 * This file is part of Seeker
 *
 * Seeker is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Seeker is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Seeker. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Seeker.Playback
{
    public sealed class ProgressiveWriteStream : Stream
    {
        private readonly Stream inner;
        private readonly ProgressiveDownloadCoordinator coordinator;

        public ProgressiveWriteStream(Stream inner, ProgressiveDownloadCoordinator coordinator)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (!inner.CanWrite || !inner.CanSeek)
            {
                throw new ArgumentException("The progressive output stream must be writable and seekable.", nameof(inner));
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush()
        {
            inner.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return inner.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            coordinator.ReportCommittedBytes(inner.Position);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            coordinator.ReportCommittedBytes(inner.Position);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
