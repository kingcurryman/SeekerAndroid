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
    public sealed class ProgressiveDownloadCoordinator
    {
        private readonly object sync = new object();
        private long availableBytes;
        private long expectedBytes;
        private bool completed;
        private bool cancelled;
        private Exception? error;
        private ProgressivePlaybackState state;

        public ProgressiveDownloadCoordinator(string spoolPath, long expectedBytes, long availableBytes = 0)
        {
            if (string.IsNullOrWhiteSpace(spoolPath))
            {
                throw new ArgumentException("A spool path is required.", nameof(spoolPath));
            }
            if (expectedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedBytes));
            }
            if (availableBytes < 0 || availableBytes > expectedBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(availableBytes));
            }

            SpoolPath = spoolPath;
            this.expectedBytes = expectedBytes;
            this.availableBytes = availableBytes;
            state = ProgressivePlaybackState.Loading;
        }

        public string SpoolPath { get; }

        public string FinalUri { get; private set; } = string.Empty;

        public event EventHandler<ProgressivePlaybackState>? StateChanged;

        public ProgressivePlaybackState State
        {
            get
            {
                lock (sync)
                {
                    return state;
                }
            }
        }

        public long AvailableBytes
        {
            get
            {
                lock (sync)
                {
                    return availableBytes;
                }
            }
        }

        public long ExpectedBytes
        {
            get
            {
                lock (sync)
                {
                    return expectedBytes;
                }
            }
        }

        public bool IsCompleted
        {
            get
            {
                lock (sync)
                {
                    return completed;
                }
            }
        }

        public void MarkWaitingForPeer()
        {
            SetNonTerminalState(ProgressivePlaybackState.WaitingForPeer);
        }

        public void MarkDownloading()
        {
            SetNonTerminalState(ProgressivePlaybackState.Downloading);
        }

        public void MarkRetrying()
        {
            lock (sync)
            {
                if (completed)
                {
                    return;
                }
                cancelled = false;
                error = null;
                state = ProgressivePlaybackState.Retrying;
                Monitor.PulseAll(sync);
            }
            RaiseStateChanged(ProgressivePlaybackState.Retrying);
        }

        public void ReportCommittedBytes(long position)
        {
            ProgressivePlaybackState? changedState = null;
            lock (sync)
            {
                if (position < availableBytes)
                {
                    throw new InvalidOperationException("Progressive downloads must remain contiguous.");
                }
                if (position > expectedBytes)
                {
                    throw new IOException("The progressive download exceeded its expected size.");
                }

                availableBytes = position;
                if (!completed && !cancelled && error == null && state != ProgressivePlaybackState.Downloading)
                {
                    state = ProgressivePlaybackState.Downloading;
                    changedState = state;
                }
                Monitor.PulseAll(sync);
            }
            RaiseStateChanged(changedState);
        }

        public void ResetForRetry(long newExpectedBytes)
        {
            if (newExpectedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newExpectedBytes));
            }

            lock (sync)
            {
                expectedBytes = newExpectedBytes;
                availableBytes = 0;
                completed = false;
                cancelled = false;
                error = null;
                state = ProgressivePlaybackState.Retrying;
                Monitor.PulseAll(sync);
            }
            RaiseStateChanged(ProgressivePlaybackState.Retrying);
        }

        public void MarkCompleted(string finalUri)
        {
            lock (sync)
            {
                if (availableBytes != expectedBytes)
                {
                    throw new IOException($"Download completed with {availableBytes} of {expectedBytes} bytes available.");
                }
                completed = true;
                FinalUri = finalUri ?? string.Empty;
                state = ProgressivePlaybackState.Completed;
                Monitor.PulseAll(sync);
            }
            RaiseStateChanged(ProgressivePlaybackState.Completed);
        }

        public void MarkCancelled()
        {
            lock (sync)
            {
                if (completed)
                {
                    return;
                }
                cancelled = true;
                state = ProgressivePlaybackState.Cancelled;
                Monitor.PulseAll(sync);
            }
            RaiseStateChanged(ProgressivePlaybackState.Cancelled);
        }

        public void MarkError(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            lock (sync)
            {
                if (completed)
                {
                    return;
                }
                error = exception;
                state = ProgressivePlaybackState.Error;
                Monitor.PulseAll(sync);
            }
            RaiseStateChanged(ProgressivePlaybackState.Error);
        }

        public bool WaitForReadableByte(long position, CancellationToken cancellationToken)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            using CancellationTokenRegistration registration = cancellationToken.Register(PulseWaiters);
            lock (sync)
            {
                while (position >= availableBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (error != null)
                    {
                        throw new IOException("The progressive download failed.", error);
                    }
                    if (cancelled)
                    {
                        throw new OperationCanceledException("The progressive download was cancelled.");
                    }
                    if (completed)
                    {
                        return false;
                    }
                    Monitor.Wait(sync);
                }
                return true;
            }
        }

        public async Task WaitForMinimumBytesAsync(long minimumBytes, CancellationToken cancellationToken = default)
        {
            long target = Math.Min(Math.Max(minimumBytes, 0), ExpectedBytes);
            await Task.Run(() =>
            {
                if (target == 0)
                {
                    return;
                }
                WaitForReadableByte(target - 1, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);
        }

        private void SetNonTerminalState(ProgressivePlaybackState newState)
        {
            bool changed = false;
            lock (sync)
            {
                if (!completed && !cancelled && error == null && state != newState)
                {
                    state = newState;
                    changed = true;
                }
            }
            RaiseStateChanged(changed ? newState : null);
        }

        private void PulseWaiters()
        {
            lock (sync)
            {
                Monitor.PulseAll(sync);
            }
        }

        private void RaiseStateChanged(ProgressivePlaybackState? changedState)
        {
            if (changedState.HasValue)
            {
                StateChanged?.Invoke(this, changedState.Value);
            }
        }
    }
}
