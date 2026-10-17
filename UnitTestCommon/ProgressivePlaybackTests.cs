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

using NUnit.Framework;
using Seeker.Playback;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestCommon
{
    [TestFixture]
    public sealed class ProgressivePlaybackTests
    {
        private string testDirectory = null!;
        private string spoolPath = null!;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(), "seeker-progressive-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            spoolPath = Path.Combine(testDirectory, "track.mp3");
            File.WriteAllBytes(spoolPath, Array.Empty<byte>());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }

        [Test]
        public async Task GrowingFileRead_BlocksThenResumesWhenBytesArrive()
        {
            var coordinator = new ProgressiveDownloadCoordinator(spoolPath, expectedBytes: 4);
            using var reader = new ProgressiveFileReader(coordinator, position: 0);
            byte[] readBuffer = new byte[4];

            Task<int> readTask = Task.Run(() => reader.Read(readBuffer, 0, readBuffer.Length));
            Assert.That(await Task.WhenAny(readTask, Task.Delay(100)), Is.Not.SameAs(readTask));

            using (var output = new ProgressiveWriteStream(OpenWriter(), coordinator))
            {
                output.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                output.Flush();
            }

            Assert.That(await readTask.WaitAsync(TimeSpan.FromSeconds(2)), Is.EqualTo(4));
            Assert.That(readBuffer, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void CompletedFile_ReturnsEofAtExpectedLength()
        {
            File.WriteAllBytes(spoolPath, new byte[] { 1, 2, 3 });
            var coordinator = new ProgressiveDownloadCoordinator(spoolPath, expectedBytes: 3, availableBytes: 3);
            coordinator.MarkCompleted("file:///complete.mp3");

            using var reader = new ProgressiveFileReader(coordinator, position: 3);
            Assert.That(reader.Read(new byte[1], 0, 1), Is.Zero);
        }

        [Test]
        public void CancelledDownload_UnblocksReaderWithCancellation()
        {
            var coordinator = new ProgressiveDownloadCoordinator(spoolPath, expectedBytes: 10);
            using var reader = new ProgressiveFileReader(coordinator, position: 0);

            Task readTask = Task.Run(() => reader.Read(new byte[1], 0, 1));
            coordinator.MarkCancelled();

            Assert.That(async () => await readTask, Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void FailedDownload_UnblocksReaderWithIoError()
        {
            var coordinator = new ProgressiveDownloadCoordinator(spoolPath, expectedBytes: 10);
            using var reader = new ProgressiveFileReader(coordinator, position: 0);

            Task readTask = Task.Run(() => reader.Read(new byte[1], 0, 1));
            coordinator.MarkError(new InvalidOperationException("peer failed"));

            Assert.That(async () => await readTask, Throws.InstanceOf<IOException>());
        }

        [Test]
        public async Task RetryState_KeepsReaderWaitingUntilDownloadContinues()
        {
            var coordinator = new ProgressiveDownloadCoordinator(spoolPath, expectedBytes: 2);
            using var reader = new ProgressiveFileReader(coordinator, position: 0);
            byte[] readBuffer = new byte[2];

            coordinator.MarkCancelled();
            coordinator.MarkRetrying();
            Task<int> readTask = Task.Run(() => reader.Read(readBuffer, 0, readBuffer.Length));
            Assert.That(await Task.WhenAny(readTask, Task.Delay(100)), Is.Not.SameAs(readTask));

            using (var output = new ProgressiveWriteStream(OpenWriter(), coordinator))
            {
                await output.WriteAsync(new byte[] { 7, 8 }, 0, 2, CancellationToken.None);
            }

            Assert.That(await readTask.WaitAsync(TimeSpan.FromSeconds(2)), Is.EqualTo(2));
            Assert.That(coordinator.State, Is.EqualTo(ProgressivePlaybackState.Downloading));
        }

        private FileStream OpenWriter()
        {
            return new FileStream(spoolPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        }
    }
}
