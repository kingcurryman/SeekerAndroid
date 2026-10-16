/*
 * Copyright (c) 2026 kingcurryman
 * Modified: Added optional progressive playback transfer context.
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

using Seeker.Playback;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Seeker
{
    public class DownloadInfo
    {
        public string username;
        public string fullFilename;
        public long Size;
        public int QueueLength;
        public CancellationTokenSource CancellationTokenSource;
        public int RetryCount;
        public Exception PreviousFailureException;
        public TransferItem TransferItemReference = null;
        public int Depth = 1;
        /// <summary>
        /// For memory-backed downloads, holds the MemoryStream reference so the
        /// continuation can read the downloaded bytes via ToArray().
        /// </summary>
        public MemoryStream OutputMemoryStream = null;
        public ProgressiveDownloadCoordinator ProgressiveDownload = null;
        // library raised its first state change for this request, and so we know it will handle the rest (and so set state failed on failure)
        public bool HandedToLibrary;
        public DownloadInfo(string usr, string file, long size, Task task, CancellationTokenSource token, int queueLength, int retryCount, int depth)
        {
            username = usr; fullFilename = file; Size = size; CancellationTokenSource = token; QueueLength = queueLength; RetryCount = retryCount; Depth = depth;
        }
    }
}
