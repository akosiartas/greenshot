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

#region Usings

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Dapplo.Addons.Bootstrapper.Resolving;
using Dapplo.Log;
using Greenshot.Addons.Addons;
using Greenshot.Addons.Controls;
using Greenshot.Addons.Core;
using Greenshot.Addons.Interfaces;
using Greenshot.Addons.Interfaces.Plugin;
using Greenshot.Gfx;

#endregion

namespace Greenshot.Addon.Photobucket
{
    /// <summary>
    ///     Description of PhotobucketDestination.
    /// </summary>
    [Destination("Photobucket")]
    public class PhotobucketDestination : AbstractDestination
	{
	    private static readonly LogSource Log = new LogSource();
        private readonly string _albumPath;
		private readonly IPhotobucketConfiguration _photobucketConfiguration;
	    private readonly IPhotobucketLanguage _photobucketLanguage;

        /// <summary>
        ///     Create a Photobucket destination
        /// </summary>
        /// <param name="photobucketConfiguration">IPhotobucketConfiguration</param>
        /// <param name="photobucketLanguage">IPhotobucketLanguage</param>
        [ImportingConstructor]
        public PhotobucketDestination(IPhotobucketConfiguration photobucketConfiguration, IPhotobucketLanguage photobucketLanguage)
	    {
	        _photobucketConfiguration = photobucketConfiguration;
	        _photobucketLanguage = photobucketLanguage;
	    }

        /// <summary>
        ///     Create a Photobucket destination, which also has the path to the album in it
        /// </summary>
        /// <param name="photobucketConfiguration">IPhotobucketConfiguration</param>
        /// <param name="photobucketLanguage">IPhotobucketLanguage</param>
        /// <param name="albumPath">path to the album, null for default</param>
        public PhotobucketDestination(IPhotobucketConfiguration photobucketConfiguration, IPhotobucketLanguage photobucketLanguage, string albumPath) : this (photobucketConfiguration, photobucketLanguage)
		{
			_photobucketConfiguration = photobucketConfiguration;
			_albumPath = albumPath;
		}

		public override string Description
		{
			get
			{
				if (_albumPath != null)
				{
					return _albumPath;
				}
				return _photobucketLanguage.UploadMenuItem;
			}
		}

		public override Bitmap DisplayIcon
		{
			get
			{
			    // TODO: Optimize this
			    var embeddedResource = GetType().Assembly.FindEmbeddedResources(@".*photobucket-logo\.png").FirstOrDefault();
			    using (var bitmapStream = GetType().Assembly.GetEmbeddedResourceAsStream(embeddedResource))
			    {
			        return BitmapHelper.FromStream(bitmapStream);
			    }
            }
		}

		public override bool IsDynamic => true;

		public override IEnumerable<IDestination> DynamicDestinations()
		{
			IList<string> albums = null;
			try
			{
				albums = PhotobucketUtils.RetrievePhotobucketAlbums();
			}
			catch
			{
				// ignored
			}

			if (albums == null || albums.Count == 0)
			{
				yield break;
			}
			foreach (var album in albums)
			{
				yield return new PhotobucketDestination(_photobucketConfiguration, _photobucketLanguage, album);
			}
		}

		/// <summary>
		///     Export the capture to Photobucket
		/// </summary>
		/// <param name="manuallyInitiated"></param>
		/// <param name="surface"></param>
		/// <param name="captureDetails"></param>
		/// <returns></returns>
		public override ExportInformation ExportCapture(bool manuallyInitiated, ISurface surface, ICaptureDetails captureDetails)
		{
			var exportInformation = new ExportInformation(Designation, Description);
		    var uploaded = Upload(captureDetails, surface, _albumPath, out var uploadUrl);
			if (uploaded)
			{
				exportInformation.ExportMade = true;
				exportInformation.Uri = uploadUrl;
			}
			ProcessExport(exportInformation, surface);
			return exportInformation;
		}


	    /// <summary>
	    ///     Upload the capture to Photobucket
	    /// </summary>
	    /// <param name="captureDetails"></param>
	    /// <param name="surfaceToUpload">ISurface</param>
	    /// <param name="albumPath">Path to the album</param>
	    /// <param name="uploadUrl">out string for the url</param>
	    /// <returns>true if the upload succeeded</returns>
	    public bool Upload(ICaptureDetails captureDetails, ISurface surfaceToUpload, string albumPath, out string uploadUrl)
	    {
	        var outputSettings = new SurfaceOutputSettings(_photobucketConfiguration.UploadFormat, _photobucketConfiguration.UploadJpegQuality, _photobucketConfiguration.UploadReduceColors);
	        try
	        {
	            var filename = Path.GetFileName(FilenameHelper.GetFilename(_photobucketConfiguration.UploadFormat, captureDetails));
	            PhotobucketInfo photobucketInfo = null;

	            // Run upload in the background
	            new PleaseWaitForm().ShowAndWait("Photobucket", _photobucketLanguage.CommunicationWait,
	                delegate { photobucketInfo = PhotobucketUtils.UploadToPhotobucket(surfaceToUpload, outputSettings, albumPath, captureDetails.Title, filename); }
	            );
	            // This causes an exeption if the upload failed :)
	            Log.Debug().WriteLine("Uploaded to Photobucket page: " + photobucketInfo.Page);
	            uploadUrl = null;
	            try
	            {
	                if (_photobucketConfiguration.UsePageLink)
	                {
	                    uploadUrl = photobucketInfo.Page;
	                    Clipboard.SetText(photobucketInfo.Page);
	                }
	                else
	                {
	                    uploadUrl = photobucketInfo.Original;
	                    Clipboard.SetText(photobucketInfo.Original);
	                }
	            }
	            catch (Exception ex)
	            {
	                Log.Error().WriteLine(ex, "Can't write to clipboard: ");
	            }
	            return true;
	        }
	        catch (Exception e)
	        {
	            Log.Error().WriteLine(e);
	            MessageBox.Show(_photobucketLanguage.UploadFailure + " " + e.Message);
	        }
	        uploadUrl = null;
	        return false;
	    }
    }
}