﻿using System;
using System.Collections.Generic;
using System.Text;

#nullable enable
namespace SmartImage.Lib.Searching
{
	/// <summary>
	/// Describes an image search result
	/// </summary>
	public sealed class ImageResult
	{
		/// <summary>
		/// Url
		/// </summary>
		public Uri? Url { get; set; }

		/// <summary>
		/// Similarity
		/// </summary>
		public float? Similarity { get; set; }

		/// <summary>
		/// Width
		/// </summary>
		public int? Width { get; set; }

		/// <summary>
		/// Height
		/// </summary>
		public int? Height { get; set; }

		/// <summary>
		/// Description, caption
		/// </summary>
		public string? Description { get; set; }

		/// <summary>
		/// Artist, author, creator
		/// </summary>
		public string? Artist { get; set; }

		/// <summary>
		/// Source
		/// </summary>
		public string? Source { get; set; }

		/// <summary>
		/// Character(s) present in image
		/// </summary>
		public string? Characters { get; set; }

		/// <summary>
		/// Site name
		/// </summary>
		public string? Site { get; set; }


		/// <summary>
		/// Date of image
		/// </summary>
		public DateTime? Date { get; set; }


		/// <summary>
		///     Result name
		/// </summary>
		public string? Name { get; set; }

		public Dictionary<string, object> OtherMetadata { get; }

		public ImageResult()
		{
			OtherMetadata = new();
		}

		public void UpdateFrom(ImageResult result)
		{
			Url         = result.Url;
			Similarity  = result.Similarity;
			Width       = result.Width;
			Height      = result.Height;
			Source      = result.Source;
			Characters  = result.Characters;
			Artist      = result.Artist;
			Site        = result.Site;
			Description = result.Description;
			Date        = result.Date;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append($"{Url}\n");

			if (Similarity.HasValue) {
				sb.Append($"{Similarity.Value/100:P}\n");
			}

			if (Width.HasValue && Height.HasValue) {
				sb.Append($"{Width}x{Height}\n");

			}

			if (Description != null) {
				sb.Append($"{nameof(Description)}: {Description}\n");

			}

			if (Artist != null) {
				sb.Append($"{Artist}\n");

			}

			if (Site != null) {
				sb.Append($"{Site}\n");

			}

			return sb.ToString();
		}
	}
}