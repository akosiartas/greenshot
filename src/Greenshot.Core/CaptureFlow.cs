﻿#region Greenshot GNU General Public License

// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2018 Thomas Braun, Jens Klingen, Robin Krom
// 
// For more information see: http://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 1 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Greenshot.Core.Interfaces;

namespace Greenshot.Core
{
    /// <summary>
    /// This describes a capture flow, from source via processor to destination
    /// </summary>
    public class CaptureFlow
    {
        /// <summary>
        /// The ISource this capture flow contains
        /// </summary>
        public IList<ISource> Sources
        {
            get;
        } = new List<ISource>();

        /// <summary>
        /// The IProcessor this capture flow contains
        /// </summary>
        public IList<IProcessor> Processors
        {
            get;
        } = new List<IProcessor>();

        /// <summary>
        /// The IDestination this capture flow contains
        /// </summary>
        public IList<IDestination> Destinations
        {
            get;
        } = new List<IDestination>();

        /// <summary>
        /// Execute this capture flow, to create a capture
        /// </summary>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>ICapture</returns>
        public async Task<ICapture> Execute(CancellationToken cancellationToken = default)
        {
            var capture = new Capture();

            // Import the capture from the sources
            foreach (var source in Sources)
            {
                var captureElement = await source.Import(cancellationToken);
                if (captureElement == null)
                {
                    continue;
                }
                capture.CaptureElements.Add(captureElement);
            }

            // Process by processors
            foreach (var processor in Processors)
            {
                await processor.Process(capture, cancellationToken);
            }

            // Export to destination
            foreach (var destination in Destinations)
            {
                await destination.Export(capture, cancellationToken);
            }

            return capture;
        }
    }
}
