﻿// Read S SmartImage.Lib IqdbEngine.cs
// 2023-01-13 @ 11:21 PM

// ReSharper disable UnusedMember.Global

using System.Diagnostics;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.XPath;
using Flurl.Http;
using Kantan.Net.Utilities;
using Kantan.Text;
using SmartImage.Lib.Results;
using SmartImage.Lib.Utilities;

// ReSharper disable StringLiteralTypo

namespace SmartImage.Lib.Engines.Impl.Search;

#nullable disable

public class IqdbEngine : BaseSearchEngine, IDisposable
{

	public override SearchEngineOptions EngineOption => SearchEngineOptions.Iqdb;

	public IqdbEngine() : this(URL_QUERY) { }

	private IqdbEngine(string s) : this(s, URL_ENDPOINT) { }

	protected IqdbEngine(string b, string e) : base(b, e)
	{
		MaxSize = MAX_FILE_SIZE; // NOTE: assuming IQDB uses kilobytes instead of kibibytes

		// Timeout = TimeSpan.FromSeconds(10);
	}

	private const int MAX_FILE_SIZE = 8_388_608;

	private const string URL_ENDPOINT = "https://iqdb.org/";
	private const string URL_QUERY    = "https://iqdb.org/?url=";

	protected override string[] ErrorBodyMessages =>
		[
			"Can't read query result!",
			"too large"
		];

	private async Task<IDocument> GetDocumentAsync(SearchQuery query, CancellationToken ct)
	{

		try {
			var response = await Client.Request(EndpointUrl)
				               .OnError(r =>
					               {
						               Debug.WriteLine($"{r.Exception}", Name);
						               r.ExceptionHandled = true;
					               }
				               )
				               .WithTimeout(Timeout)
				               .PostMultipartAsync(m =>
				               {
					               m.AddString("MAX_FILE_SIZE", MAX_FILE_SIZE.ToString());
					               m.AddString("url", query.Uni.IsUri ? query.Uni.ValueString : String.Empty);

					               if (query.Uni.IsUri) { }
					               else if (query.Uni.IsFile) {
						               m.AddFile("file", query.Uni.Value.ToString(), fileName: "image.jpg");
					               }

					               return;
				               }, cancellationToken: ct);

			if (response != null) {
				var s = await response.GetStringAsync();

				var parser = new HtmlParser();
				return await parser.ParseDocumentAsync(s, ct).ConfigureAwait(false);

			}

			return null;
		}
		catch (Exception e) {
			Debug.WriteLine($"{e.Message}!");
			return null;
		}
	}

	private SearchResultItem ParseResult(IHtmlCollection<IElement> tr, SearchResult r)
	{
		var caption = tr[0];
		var img     = tr[1];
		var src     = tr[2];

		var img2         = img.Children[0].Children[0].Children[0].Attributes["src"];
		var thumbnail    = img2 != null ? Url.Combine(BaseUrl.Root, img2.Value) : null;
		var thumbnail1   = img.Children[0].Children[0].Attributes["alt"];
		var thumbnailAlt = thumbnail1?.Value;

		string url = null;

		//img.ChildNodes[0].ChildNodes[0].TryGetAttribute("href")

		try {
			//url = src.FirstChild.ChildNodes[2].ChildNodes[0].TryGetAttribute("href");

			url = img.ChildNodes[0].ChildNodes[0].TryGetAttribute(Serialization.Atr_href);

			// Links must begin with http:// in order to work with "start"

		}
		catch {
			// ignored
		}

		int w = 0, h = 0;

		if (tr.Length >= 4) {
			var res = tr[3];

			string[] wh = res.TextContent.Split(Strings.Constants.MUL_SIGN);

			string wStr = wh[0].SelectOnlyDigits();
			w = int.Parse(wStr);

			// May have NSFW caption, so remove it

			string hStr = wh[1].SelectOnlyDigits();
			h = int.Parse(hStr);
		}

		double? sim;

		if (tr.Length >= 5) {
			var    simNode = tr[4];
			string simStr  = simNode.TextContent.Split('%')[0];
			sim = double.Parse(simStr);
			sim = Math.Round(sim.Value, 2);
		}
		else {
			sim = null;
		}

		Url uri;

		if (url != null) {
			// Url u = url;
			
			if (url.StartsWith("//")) {
				url = "https:" + url;

				// url = url[2..];
			}

			uri = url;
		}
		else {
			uri = null;
		}

		var result = new SearchResultItem(r)
		{
			Url            = uri,
			Similarity     = sim,
			Width          = w,
			Height         = h,
			Source         = src.TextContent,
			Description    = caption.TextContent,
			Thumbnail      = thumbnail,
			ThumbnailTitle = thumbnailAlt

		};
		result.Site ??= uri?.Host;

		// r.Results.Add(result);

		return result;
	}

	public override async Task<SearchResult> GetResultAsync(SearchQuery query, CancellationToken token = default)
	{
		// Don't select other results

		var sr = await base.GetResultAsync(query, token);

		if (sr.Status == SearchResultStatus.IllegalInput) {
			goto ret;
		}

		var doc = await GetDocumentAsync(query, token);

		if (doc == null || doc.Body == null) {
			sr.ErrorMessage = $"Could not retrieve data";
			sr.Status       = SearchResultStatus.Failure;
			goto ret;
		}

		foreach (string s in ErrorBodyMessages) {
			if (doc.Body.TextContent.Contains(s)) {

				sr.Status = SearchResultStatus.IllegalInput;
				goto ret;
			}

		}

		var err = doc.Body.GetElementsByClassName("err");

		if (err.Length != 0) {
			var fe = err[0];
			sr.Status       = SearchResultStatus.Failure;
			sr.ErrorMessage = $"{fe.TextContent}";
			goto ret;
		}

		var pages  = doc.Body.SelectSingleNode(Serialization.S_Iqdb_Pages);
		var tables = ((IHtmlElement) pages).SelectNodes(Serialization.S_Iqdb_DivTable);

		// No relevant results?

		var ns = doc.Body.QuerySelector(Serialization.S_Iqdb_NoMatches);

		if (ns != null) {

			sr.Status = SearchResultStatus.NoResults;
			goto ret;
		}

		var select = tables.Select(table => ((IHtmlElement) table)
			                           .QuerySelectorAll(Serialization.S_Iqdb_Table))
			.ToArray();

		for (int i = 1; i < select.Length; i++) {
			var sri = ParseResult(select[i], sr);
			sr.Results.Add(sri);
		}

		// XPATH //body/div/div/table

		// First is original image
		// images.RemoveAt(0);

		// var best = images[0];
		// sr.PrimaryResult.UpdateFrom(best);
		// sr.Results.AddRange(images);

		/*sr.Results.Quality = sr.PrimaryResult.Similarity switch
		{
			>= 75 => ResultQuality.High,
			_ or null => ResultQuality.NA,
		};*/

	ret:
		sr.Update();
		return sr;
	}

	#region

	public override void Dispose()
	{
		// base.Dispose();
		GC.SuppressFinalize(this);
	}

	#endregion

}