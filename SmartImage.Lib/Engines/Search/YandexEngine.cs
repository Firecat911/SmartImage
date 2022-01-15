using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.XPath;
using Kantan.Net;
using Kantan.Text;
using SmartImage.Lib.Engines.Search.Base;
using SmartImage.Lib.Searching;

// ReSharper disable SuggestVarOrType_SimpleTypes

#pragma warning disable 8602

#nullable enable

namespace SmartImage.Lib.Engines.Search;

public sealed class YandexEngine : WebSearchEngine
{
	public YandexEngine() : base("https://yandex.com/images/search?rpt=imageview&url=") { }

	public override SearchEngineOptions EngineOption => SearchEngineOptions.Yandex;

	public override string Name => EngineOption.ToString();

	public override TimeSpan Timeout => TimeSpan.FromSeconds(6.5);

	public override EngineSearchType SearchType => EngineSearchType.Image;

	private static string? GetAnalysis(IDocument doc)
	{
		var nodes = doc.Body.SelectNodes("//a[contains(@class, 'Tags-Item') and " +
		                                 "../../../../div[contains(@class,'CbirTags')]]/*");

		var nodes2 = doc.Body.QuerySelectorAll(".CbirTags > .Tags > " +
		                                       ".Tags-Wrapper > .Tags-Item");

		nodes.AddRange(nodes2);

		if (!nodes.Any()) {
			return null;
		}

		string? appearsToContain = nodes.Select(n => n.TextContent).QuickJoin();

		return appearsToContain;
	}

	private static List<ImageResult>? GetOtherImages(IDocument doc, SearchResult r)
	{
		var tagsItem = doc.Body.SelectNodes("//li[@class='other-sites__item']");

		if (tagsItem == null) {
			return null;
		}

		 ImageResult Parse(INode siz)
		{
			string link    = siz.FirstChild.TryGetAttribute("href");
			string resText = siz.FirstChild.ChildNodes[1].FirstChild.TextContent;

			//other-sites__snippet

			var snippet = siz.ChildNodes[1];
			var title   = snippet.FirstChild;
			var site    = snippet.ChildNodes[1];
			var desc    = snippet.ChildNodes[2];

			var (w, h) = ParseResolution(resText);

			return new ImageResult(r)
			{
				Url         = new Uri(link),
				Site        = site.TextContent,
				Description = title.TextContent,
				Width       = w,
				Height      = h,
			};
		}

		return tagsItem.AsParallel().Select(Parse).ToList();
	}

	private static (int? w, int? h) ParseResolution(string resText)
	{
		string[] resFull = resText.Split(Strings.Constants.MUL_SIGN);

		int? w = null, h = null;

		if (resFull.Length == 1 && resFull[0] == resText) {
			const string TIMES_DELIM = "&times;";

			if (resText.Contains(TIMES_DELIM)) {
				resFull = resText.Split(TIMES_DELIM);
			}
		}

		if (resFull.Length == 2) {
			w = Int32.Parse(resFull[0]);
			h = Int32.Parse(resFull[1]);
		}

		return (w, h);
	}

	private static List<ImageResult> GetImages(IDocument doc,SearchResult r)
	{
		var tagsItem = doc.Body.SelectNodes("//a[contains(@class, 'Tags-Item')]");
		var images   = new List<ImageResult>();

		if (tagsItem.Count == 0) {
			return images;
		}

		var sizeTags = tagsItem.Where(sx => !sx.Parent.Parent.TryGetAttribute("class")
		                                       .Contains("CbirItem"));

		 ImageResult Parse(INode siz)
		{
			string? link = siz.TryGetAttribute("href");

			string? resText = siz.FirstChild.GetExclusiveText();

			(int? w, int? h) = ParseResolution(resText!);

			if (!w.HasValue || !h.HasValue) {
				w = null;
				h = null;
				//link = null;
			}

			if (UriUtilities.IsUri(link, out var link2)) { }
			else {
				link2 = null;
			}

			var yi = new ImageResult(r)
			{
				Url    = link2,
				Width  = w,
				Height = h,
			};

			return yi;
		}

		images.AddRange(sizeTags.AsParallel().Select(Parse));

		return images;
	}

	protected override SearchResult Process(object obj, SearchResult sr)
	{
		var doc = (IDocument) obj;

		// Automation detected
		const string AUTOMATION_ERROR_MSG = "Please confirm that you and not a robot are sending requests";

		if (doc.Body.TextContent.Contains(AUTOMATION_ERROR_MSG)) {
			sr.Status = ResultStatus.Cooldown;
			return sr;
		}
			
		/*
		 * Parse what the image looks like
		 */

		string? looksLike = GetAnalysis(doc);

		/*
		 * Find and sort through high resolution image matches
		 */

		var images = GetImages(doc, sr);

		var otherImages = GetOtherImages(doc, sr);

		if (otherImages != null) {
			images.AddRange(otherImages);
		}

		images = images.OrderByDescending(r => r.PixelResolution).ToList();

		//

		if (images.Count > 0) {
			var best = images[0];
			sr.PrimaryResult.UpdateFrom(best);

			if (looksLike != null) {
				sr.PrimaryResult.Description = looksLike;
			}

			sr.OtherResults.AddRange(images);
		}

		const string NO_MATCHING = "No matching images found";

		if (doc.Body.TextContent.Contains(NO_MATCHING)) {

			sr.ErrorMessage = NO_MATCHING;
			sr.Status       = ResultStatus.Extraneous;
		}

		sr.PrimaryResult.Quality = sr.PrimaryResult.MegapixelResolution switch
		{
			null => ResultQuality.Indeterminate,
			>= 1 => ResultQuality.High,
			_    => ResultQuality.Low,
		};
		

		return sr;
	}
}