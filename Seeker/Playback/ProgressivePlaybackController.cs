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

using Android.OS;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using Seeker.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Seeker.Playback
{
    public sealed class ProgressivePlaybackController
    {
        private const long InitialBufferBytes = 1024 * 1024;
        private readonly Handler mainHandler = new Handler(Looper.MainLooper);
        private readonly object sync = new object();
        private IExoPlayer player;
        private GrowingFileDataSourceFactory dataSourceFactory;
        private DefaultMediaSourceFactory mediaSourceFactory;
        private ProgressiveDownloadCoordinator coordinator;
        private CancellationTokenSource startupCancellation;
        private Timer playerStateTimer;
        private ProgressivePlaybackState state = ProgressivePlaybackState.Loading;

        private ProgressivePlaybackController()
        {
        }

        public static ProgressivePlaybackController Instance { get; } = new ProgressivePlaybackController();

        public event EventHandler<ProgressivePlaybackState> StateChanged;

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

        public async Task PlayNowAsync(FullFileInfo file, string username)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            StopCurrentPlayback();
            SetState(ProgressivePlaybackState.Loading);

            var localStartupCancellation = new CancellationTokenSource();
            lock (sync)
            {
                startupCancellation = localStartupCancellation;
            }

            try
            {
                ProgressiveDownloadCoordinator progressiveDownload = await DownloadService.Instance
                    .EnqueueFileForPlaybackAsync(file, username)
                    .ConfigureAwait(false);
                coordinator = progressiveDownload;
                coordinator.StateChanged += Coordinator_StateChanged;
                SetState(coordinator.State);

                await coordinator.WaitForMinimumBytesAsync(InitialBufferBytes, localStartupCancellation.Token).ConfigureAwait(false);
                localStartupCancellation.Token.ThrowIfCancellationRequested();

                var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                mainHandler.Post(() =>
                {
                    try
                    {
                        if (localStartupCancellation.IsCancellationRequested)
                        {
                            ready.TrySetCanceled(localStartupCancellation.Token);
                            return;
                        }
                        StartPlayer(coordinator);
                        ready.TrySetResult(true);
                    }
                    catch (Exception exception)
                    {
                        ready.TrySetException(exception);
                    }
                });
                await ready.Task.ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                if (!localStartupCancellation.IsCancellationRequested)
                {
                    SetState(ProgressivePlaybackState.Cancelled);
                }
            }
            catch (Exception)
            {
                SetState(ProgressivePlaybackState.Error);
                throw;
            }
        }

        public void StopCurrentPlayback()
        {
            ProgressiveDownloadCoordinator previousCoordinator;
            lock (sync)
            {
                startupCancellation?.Cancel();
                startupCancellation?.Dispose();
                startupCancellation = null;
                previousCoordinator = coordinator;
                coordinator = null;
            }
            if (previousCoordinator != null)
            {
                previousCoordinator.StateChanged -= Coordinator_StateChanged;
            }

            playerStateTimer?.Dispose();
            playerStateTimer = null;
            mainHandler.Post(() =>
            {
                player?.Release();
                player = null;
                mediaSourceFactory?.Dispose();
                mediaSourceFactory = null;
                dataSourceFactory?.Dispose();
                dataSourceFactory = null;
            });
        }

        private void StartPlayer(ProgressiveDownloadCoordinator progressiveDownload)
        {
            dataSourceFactory = new GrowingFileDataSourceFactory(
                progressiveDownload,
                () => SetState(ProgressivePlaybackState.Buffering));
            mediaSourceFactory = new DefaultMediaSourceFactory(dataSourceFactory);
            player = new ExoPlayerBuilder(SeekerApplication.ApplicationContext)
                .SetMediaSourceFactory(mediaSourceFactory)
                .Build();
            player.SetMediaItem(MediaItem.FromUri("seeker-progressive://current-track"));
            player.PlayWhenReady = true;
            player.Prepare();
            SetState(ProgressivePlaybackState.Buffering);

            playerStateTimer = new Timer(_ => mainHandler.Post(PollPlayerState), null, 250, 250);
        }

        private void PollPlayerState()
        {
            if (player == null)
            {
                return;
            }
            if (player.PlayerError != null)
            {
                SetState(ProgressivePlaybackState.Error);
            }
            else if (player.IsPlaying)
            {
                SetState(ProgressivePlaybackState.Playing);
            }
            else if (player.PlayWhenReady)
            {
                SetState(ProgressivePlaybackState.Buffering);
            }
        }

        private void Coordinator_StateChanged(object sender, ProgressivePlaybackState newState)
        {
            if (newState == ProgressivePlaybackState.Completed && State == ProgressivePlaybackState.Playing)
            {
                return;
            }
            if (newState == ProgressivePlaybackState.Downloading && player != null)
            {
                return;
            }
            SetState(newState);
        }

        private void SetState(ProgressivePlaybackState newState)
        {
            lock (sync)
            {
                if (state == newState)
                {
                    return;
                }
                state = newState;
            }
            mainHandler.Post(() => StateChanged?.Invoke(this, newState));
        }
    }
}
