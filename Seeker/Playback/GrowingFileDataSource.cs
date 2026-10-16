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

using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using System;
using System.Threading;

namespace Seeker.Playback
{
    public sealed class GrowingFileDataSource : BaseDataSource
    {
        private readonly ProgressiveDownloadCoordinator coordinator;
        private readonly Action waitingForData;
        private readonly Android.Net.Uri uri;
        private ProgressiveFileReader reader;
        private CancellationTokenSource closeCancellation;
        private bool opened;

        public GrowingFileDataSource(ProgressiveDownloadCoordinator coordinator, Action waitingForData)
            : base(false)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.waitingForData = waitingForData;
            uri = Android.Net.Uri.Parse("seeker-progressive://current-track");
        }

        public override Android.Net.Uri Uri => opened ? uri : null;

        public override long Open(DataSpec dataSpec)
        {
            if (dataSpec == null)
            {
                throw new ArgumentNullException(nameof(dataSpec));
            }
            if (dataSpec.Position < 0 || dataSpec.Position > coordinator.ExpectedBytes)
            {
                throw new Java.IO.IOException("Requested position is outside the progressive file.");
            }

            Close();
            TransferInitializing(dataSpec);
            closeCancellation = new CancellationTokenSource();
            reader = new ProgressiveFileReader(coordinator, dataSpec.Position);
            opened = true;
            TransferStarted(dataSpec);

            long remaining = coordinator.ExpectedBytes - dataSpec.Position;
            return dataSpec.Length >= 0 ? Math.Min(dataSpec.Length, remaining) : remaining;
        }

        public override int Read(byte[] buffer, int offset, int length)
        {
            if (!opened || reader == null)
            {
                throw new Java.IO.IOException("The progressive data source is not open.");
            }
            if (length == 0)
            {
                return 0;
            }

            try
            {
                if (reader.Position >= coordinator.AvailableBytes && !coordinator.IsCompleted)
                {
                    waitingForData?.Invoke();
                }
                int bytesRead = reader.Read(buffer, offset, length, closeCancellation.Token);
                if (bytesRead == 0)
                {
                    return C.ResultEndOfInput;
                }
                BytesTransferred(bytesRead);
                return bytesRead;
            }
            catch (OperationCanceledException exception)
            {
                throw new Java.IO.InterruptedIOException(exception.Message);
            }
            catch (System.IO.IOException exception)
            {
                throw new Java.IO.IOException(exception.Message);
            }
        }

        public override void Close()
        {
            bool wasOpened = opened;
            opened = false;
            closeCancellation?.Cancel();
            closeCancellation?.Dispose();
            closeCancellation = null;
            reader?.Dispose();
            reader = null;
            if (wasOpened)
            {
                TransferEnded();
            }
        }
    }

    public sealed class GrowingFileDataSourceFactory : Java.Lang.Object, IDataSourceFactory
    {
        private readonly ProgressiveDownloadCoordinator coordinator;
        private readonly Action waitingForData;

        public GrowingFileDataSourceFactory(ProgressiveDownloadCoordinator coordinator, Action waitingForData)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.waitingForData = waitingForData;
        }

        public IDataSource CreateDataSource()
        {
            return new GrowingFileDataSource(coordinator, waitingForData);
        }
    }
}
