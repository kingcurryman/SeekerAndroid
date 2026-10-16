/*
 * Copyright (c) 2026 kingcurryman
 * Modified: Added progressive playback spool operations.
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

namespace Seeker.Services
{
    public interface IFileSystemService
    {
        void GetOrCreateIncompleteLocation(string username, string fullfilename, int depth,
            out string incompleteUri, out string parentUri, out long partialLength);

        Stream OpenIncompleteStream(string incompleteUri, long partialLength);

        void GetOrCreateProgressiveSpool(string username, string fullfilename,
            out string spoolPath, out string spoolUri, out string parentUri, out long partialLength);

        Stream OpenProgressiveSpoolStream(string spoolPath, long partialLength);

        string SaveProgressiveSpool(string fullfilename, string username, string spoolPath,
            int depth, bool noSubFolder, out string finalUri);

        void DeleteProgressiveSpool(string spoolPath);

        string SaveToFile(string fullfilename, string username, ArraySegment<byte> bytes,
            string uriOfIncomplete, string parentUriOfIncomplete,
            bool memoryMode, int depth, bool noSubFolder, out string finalUri);

        void SaveFileToMediaStore(string path);
    }
}
