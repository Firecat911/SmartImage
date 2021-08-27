﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using JetBrains.Annotations;
using Novus.Win32;
using Kantan.Net;
using Kantan.Utilities;
using RestSharp;
using StringTask= System.Threading.Tasks.Task<string>;
using static Kantan.Diagnostics.LogCategories;
// ReSharper disable ConvertIfStatementToReturnStatement

// ReSharper disable CognitiveComplexity
// ReSharper disable PossibleNullReferenceException
// ReSharper disable UnusedParameter.Local
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SmartImage.Lib.Utilities
{
	public static class ImageHelper
	{
		/*
		 * https://stackoverflow.com/questions/35151067/algorithm-to-compare-two-images-in-c-sharp
		 * https://stackoverflow.com/questions/23931/algorithm-to-compare-two-images
		 * https://github.com/aetilius/pHash
		 * http://hackerfactor.com/blog/index.php%3F/archives/432-Looks-Like-It.html
		 * https://github.com/ishanjain28/perceptualhash
		 * https://github.com/Tom64b/dHash
		 * https://github.com/Rayraegah/dhash
		 * https://tineye.com/faq#how
		 * https://github.com/CrackedP0t/Tidder/
		 * https://github.com/taurenshaman/imagehash
		 */

		/*
		 * https://github.com/mikf/gallery-dl
		 * https://github.com/regosen/gallery_get
		 */


		#region Cli

		private const string MAGICK_EXE     = "magick.exe";
		private const string GALLERY_DL_EXE = "gallery-dl.exe";
		private const string YOUTUBE_DL_EXE = "youtube-dl.exe";
		private const string FFPROBE_EXE    = "ffprobe.exe";
		private const string FFMPEG_EXE     = "ffmpeg.exe";

		public static Dictionary<string, string> UtilitiesMap
		{
			get
			{
				var rg = new Dictionary<string, string>();

				foreach (string exe in Utilities) {
					string path = FileSystem.SearchInPath(exe);

					rg.Add(exe, path);
				}

				return rg;

			}
		}

		public static readonly List<string> Utilities = new()
		{
			FFMPEG_EXE, FFPROBE_EXE, MAGICK_EXE, YOUTUBE_DL_EXE, GALLERY_DL_EXE
		};

		#endregion

		private const int TimeoutMS = 1000;

		public static string Download(Uri direct, string path)
		{
			string filename = Path.GetFileName(direct.AbsolutePath);

			if (!Path.HasExtension(filename)) {

				// If no format is specified/found, just append a jpg extension
				string ext = ".jpg";

				// For Pixiv (?)
				var kv = HttpUtility.ParseQueryString(direct.Query);

				var t = kv["format"];

				if (t != null) {
					ext = $".{t}";
				}

				filename += ext;

				Debug.WriteLine("Fixed file", C_DEBUG);
			}


			string combine = Path.Combine(path, filename);

			using var wc = new WebClient();

			wc.DownloadFile(direct, combine);

			return combine;
		}


		public static Image GetImage(string s)
		{
			using var wc = new WebClient();

			//byte[] buf = wc.DownloadData(s);

			//using var stream = new MemoryStream(buf);
			//Debug.WriteLine($"Alloc {buf.Length}");

			using var stream = wc.OpenRead(s);

			var image = Image.FromStream(stream);

			return image;

		}


		/// <summary>
		/// Scans for direct images within a webpage.
		/// </summary>
		/// <param name="url">Url to search</param>
		/// <param name="count">Number of direct images to return</param>
		/// <param name="timeoutMS"></param>
		public static async Task<List<string>> FindDirectImages(string url, int count = 10, long timeoutMS = TimeoutMS)
		{
			/*
			 * TODO: WIP
			 */

			var images = new List<string>();

			IHtmlDocument document;

			var t1 = Stopwatch.GetTimestamp();

			try {
				string html   = WebUtilities.GetString(url);
				var    parser = new HtmlParser();

				document = parser.ParseDocument(html);
			}
			catch (Exception e) {
				Debug.WriteLine($"{e.Message}", C_ERROR);

				return null;
			}

			var d = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - t1);

			Debug.WriteLine($"HTML: {d.TotalSeconds:F}", C_DEBUG);

			var t2   = Stopwatch.GetTimestamp();
			var cts  = new CancellationTokenSource();
			var flat = new List<string>();

			flat.AddRange(document.QuerySelectorAttributes("a", "href"));
			flat.AddRange(document.QuerySelectorAttributes("img", "src"));

			flat = flat.Distinct().ToList();


			var tasks = new List<Task<string>>();

			for (int i = 0; i < flat.Count; i++) {
				int iCopy = i;

				tasks.Add(Task<string>.Factory.StartNew(() =>
				{
					string s = flat[iCopy];

					if (IsImage(s, (int) timeoutMS)) {
						return s;
					}

					return null;

				}, cts.Token));
			}

			while (tasks.Any() && count !=0) {
				var r = await Task.WhenAny(tasks);
				tasks.Remove(r);

				if (r.Result is { } && count > 0) {
					images.Add(r.Result);
					count--;
				}
			}


			var d2 = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - t2);

			Debug.WriteLine($"Parsing: {d2.TotalSeconds:F}", C_DEBUG);

			return images;
		}

		public static bool IsImage(string url, long timeout = TimeoutMS)
		{
			if (!Network.IsUri(url, out var u)) {
				return false;
			}

			var response = Network.GetResponse(u.ToString(), (int) timeout, Method.HEAD);

			if (!response.IsSuccessful) {
				return false;
			}

			var a = response.ContentType.StartsWith("image") && response.ContentType != "image/svg+xml";
			var b = response.ContentLength >= 50_000;

			return a && b;
		}

		internal static string AsPercent(this float n)
		{
			/*
			 * Some engines may return the similarity value as 0f < n < 1f
			 * Therefore, similarity is normalized (100f * n) and stored as 0f < n < 100f
			 */

			return $"{n / 100:P}";
		}

		public static DisplayResolutionType GetDisplayResolution(int w, int h)
		{
			/*
			 *	Other			W < 1280
			 *	[HD, FHD)		[1280, 1920)	1280 <= W < 1920	W: >= 1280 < 1920
			 *	[FHD, QHD)		[1920, 2560)	1920 <= W < 2560	W: >= 1920 < 2560
			 *	[QHD, UHD)		[2560, 3840)	2560 <= W < 3840	W: >= 2560 < 3840
			 *	[UHD, ∞)											W: >= 3840
			 */

			return (w, h) switch
			{
				/*
				 * Specific resolutions
				 */

				(640, 360) => DisplayResolutionType.nHD,


				/*
				 * General resolutions
				 */

				_ => w switch
				{
					>= 1280 and < 1920 => DisplayResolutionType.HD,
					>= 1920 and < 2560 => DisplayResolutionType.FHD,
					>= 2560 and < 3840 => DisplayResolutionType.QHD,
					>= 3840            => DisplayResolutionType.UHD,
					_                  => DisplayResolutionType.Unknown,
				}
			};

		}


		/*
		 * Direct images are URIs that point to a binary image file
		 */


		/// <summary>
		/// Determines whether <paramref name="url"/> is a direct image link
		/// </summary>
		/// <remarks>A direct image link is a link which points to a binary image file</remarks>
		public static bool IsDirect(string url, DirectImageType directType = DirectImageType.Regex)
		{
			return directType switch
			{
				DirectImageType.Binary => IsImage(url),
				DirectImageType.Regex =>
					/*
					 * https://github.com/PactInteractive/image-downloader
					 */
					Regex.IsMatch(
						url,
						@"(?:([^:\/?#]+):)?(?:\/\/([^\/?#]*))?([^?#]*\.(?:bmp|gif|ico|jfif|jpe?g|png|svg|tiff?|webp))(?:\?([^#]*))?(?:#(.*))?",
						RegexOptions.IgnoreCase),
				_ => throw new ArgumentOutOfRangeException(nameof(directType), directType, null)
			};

		}
	}


	public enum DirectImageType
	{
		Binary,
		Regex
	}

	public enum DisplayResolutionType
	{
		Unknown,

		nHD,
		HD,
		FHD,
		QHD,
		UHD,
	}
}