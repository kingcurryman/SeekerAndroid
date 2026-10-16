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

namespace Seeker.Playback
{
    public sealed class ProgressiveFileReader : IDisposable
    {
        private readonly ProgressiveDownloadCoordinator coordinator;
        private readonly FileStream stream;

        public ProgressiveFileReader(ProgressiveDownloadCoordinator coordinator, long position)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (position < 0 || position > coordinator.ExpectedBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            stream = new FileStream(
                coordinator.SpoolPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            stream.Position = position;
        }

        public long Position => stream.Position;

        public int Read(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (count == 0)
            {
                return 0;
            }
            if (!coordinator.WaitForReadableByte(stream.Position, cancellationToken))
            {
                return 0;
            }

            int readable = (int)Math.Min(count, coordinator.AvailableBytes - stream.Position);
            return stream.Read(buffer, offset, readable);
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }
}
