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

namespace Seeker.Playback
{
    public enum ProgressivePlaybackState
    {
        Loading,
        WaitingForPeer,
        Downloading,
        Retrying,
        Buffering,
        Playing,
        Completed,
        Cancelled,
        Error
    }
}
