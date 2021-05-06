﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using SimpleCore.Net;

// ReSharper disable InconsistentNaming

// ReSharper disable UnusedMember.Global

namespace SmartImage.Lib.Utilities
{
	public static class ImageUtilities
	{
		//todo

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

		/*public static HtmlNode Index(this HtmlNode node, params int[] i)
		{
			if (!i.Any()) {
				return node;
			}
			if (i.First() < node.ChildNodes.Count) {
				return node.ChildNodes[i.First()].Index(i.Skip(1).ToArray());
			}

			return node;
		}*/

		public static (int w, int h) GetResolution(string s)
		{
			using var bmp = Image.FromFile(s);

			return (bmp.Width, bmp.Height);
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

		public static bool IsDirectImage(string value)
		{
			return MediaTypes.IsDirect(value, MimeType.Image);
		}
		
		public static string ResolveDirectLink(string s)
		{
			//todo: WIP
			string d = "";

			try {
				var    uri  = new Uri(s);
				string host = uri.Host;


				var doc  = new HtmlDocument();
				var html = Network.GetSimpleResponse(s);

				if (host.Contains("danbooru")) {
					Debug.WriteLine("danbooru");


					var jObject = JObject.Parse(html.Content);

					d = (string) jObject["file_url"]!;


					return d;
				}

				doc.LoadHtml(html.Content);

				string sel = "//img";

				var nodes = doc.DocumentNode.SelectNodes(sel);

				if (nodes == null) {
					return null;
				}

				Debug.WriteLine($"{nodes.Count}");
				Debug.WriteLine($"{nodes[0]}");


			}
			catch (Exception e) {
				Debug.WriteLine($"direct {e.Message}");
				return d;
			}


			return d;
		}
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