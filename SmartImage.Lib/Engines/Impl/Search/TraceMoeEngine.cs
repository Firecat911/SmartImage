using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using Flurl;
using Flurl.Http;
using JetBrains.Annotations;
using Kantan.Collections;
using Kantan.Text;
using SmartImage.Lib.Clients;
using SmartImage.Lib.Model;
using SmartImage.Lib.Results;

// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006, IDE0051
namespace SmartImage.Lib.Engines.Impl.Search;

/// <summary>
/// 
/// </summary>
/// <a href="https://soruly.github.io/trace.moe/#/">Documentation</a>
public sealed class TraceMoeEngine : BaseSearchEngine, IDisposable
{

	public TraceMoeEngine() : base(URL_QUERY, URL_API)
	{
		Timeout = TimeSpan.FromSeconds(25);
	}

	/// <summary>
	/// Used to retrieve more information about results
	/// </summary>
	private readonly AnilistClient m_anilistClient = new();

	public override string Name => "trace.moe";

	public override SearchEngineOptions EngineOption => SearchEngineOptions.TraceMoe;

	public override async Task<SearchResult> GetResultAsync(SearchQuery query, CancellationToken token = default)
	{

		// https://soruly.github.io/trace.moe/#/

		TraceMoeRootObject tm = null;

		var r = await base.GetResultAsync(query, token);

		try {
			IFlurlRequest request = Client.Request((EndpointUrl.AppendPathSegment("/search")))
				.WithTimeout(Timeout)
				.SetQueryParam("url", query.Upload, true);

			var response = await request.GetAsync(cancellationToken: token);

			var json = await response.GetStringAsync();

			/*
			var settings = new JsonSerializerOptions()
			{
				Error = (sender, args) =>
				{
					if (Equals(args.ErrorContext.Member, nameof(TraceMoeDoc.episode)) /*&&
						args.ErrorContext.OriginalObject.GetType() == typeof(TraceMoeRootObject)#1#) {
						args.ErrorContext.Handled = true;
					}

					Debug.WriteLine($"{Name} :: {args.ErrorContext}", nameof(GetResultAsync));
				}
			};
			*/

			tm = JsonSerializer.Deserialize<TraceMoeRootObject>(json);
		}
		catch (Exception e) {
			Debug.WriteLine($"{Name} :: {nameof(Process)}: {e.Message}", nameof(GetResultAsync));
			r.ErrorMessage = e.Message;
			r.Status       = SearchResultStatus.Failure;
			goto ret;
		}

		if (tm != null) {
			if (tm.result != null) {
				// Most similar to least similar

				try {
					var results = await ConvertResultsAsync(tm, r);

					r.RawUrl = new Url(BaseUrl + query.Upload);
					r.Results.AddRange(results);
				}
				catch (Exception e) {
					r.ErrorMessage = e.Message;
					r.Status       = SearchResultStatus.Failure;
				}

			}
			else if (tm.error != null) {
				Debug.WriteLine($"{Name} :: API error: {tm.error}", nameof(GetResultAsync));
				r.ErrorMessage = tm.error;
				r.Status       = SearchResultStatus.IllegalInput;

				if (r.ErrorMessage.Contains("Search queue is full")) {
					r.Status = SearchResultStatus.Unavailable;
				}
			}
		}

		ret:
		r.Update();

		return r;
	}

	private async Task<IEnumerable<SearchResultItem>> ConvertResultsAsync(TraceMoeRootObject obj, SearchResult sr)
	{
		var results = obj.result;
		var items   = new SearchResultItem[results.Count];

		for (int i = 0; i < items.Length; i++) {
			var doc    = results[i];
			var result = doc.Convert(sr, out var ch);

			try {
				string anilistUrl = ANILIST_URL.AppendPathSegment(doc.anilist);
				string name       = await m_anilistClient.GetTitleAsync((int) doc.anilist);
				result.Source   = name;
				result.Url      = new Url(anilistUrl);
				result.Metadata = doc;
			}
			catch (Exception e) {
				Debug.WriteLine($"{this} :: {e.Message}", nameof(ConvertResultsAsync));
			}

			items[i] = result;
		}

		return items;

	}

	/// <summary>
	/// https://anilist.co/anime/{id}/
	/// </summary>
	private const string ANILIST_URL = "https://anilist.co/anime/";

	/// <summary>
	/// Threshold at which results become inaccurate
	/// </summary>
	public const double FILTER_THRESHOLD = 87.00;

	private const string URL_API   = "https://api.trace.moe";
	private const string URL_QUERY = "https://trace.moe/?url=";

	public override void Dispose()
	{
		m_anilistClient.Dispose();
	}

	public async Task<TraceMoeQuotaObject> GetQuotaAsync()
	{
		return await EndpointUrl.AppendPathSegment("me")
			       .GetJsonAsync<TraceMoeQuotaObject>();
	}

}

#region API Objects

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TraceMoeRootObject
{

	public long frameCount { get; set; }

	public string error { get; set; }

	public List<TraceMoeDoc> result { get; set; }

}

public class TraceMoeQuotaObject
{

	public string Id { get; set; }

	public long Priority { get; set; }

	public long Concurrency { get; set; }

	public long Quota { get; set; }

	public long QuotaUsed { get; set; }

}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TraceMoeDoc : IResultConvertable
{

	public double from { get; set; }

	public double to { get; set; }

	public long anilist { get; set; }

	public string filename { get; set; }

	/// <remarks>Episode may be a JSON array (edge case) or a normal integer</remarks>
	public object episode { get; set; }

	public double similarity { get; set; }

	public string video { get; set; }

	public string image { get; set; }

	public string EpisodeString
	{
		get
		{
			string epStr = episode is { } ? episode is string s ? s : episode.ToString() : string.Empty;

			if (episode is IEnumerable e && e is not string) {
				var epList = e.CastToList()
					.Select(x =>
					{
						var s1 = x.ToString();

						if (s1.Contains('|')) {
							s1 = s1.Split('|')[0];
						}

						return long.Parse(s1 ?? string.Empty);
					});

				epStr = epList.QuickJoin();
			}

			return epStr;
		}
	}

	public SearchResultItem Convert(SearchResult sr, out SearchResultItem[] children)
	{
		children = [];
		var sim = Math.Round(similarity * 100.0f, 2);

		string epStr = EpisodeString;

		var result = new SearchResultItem(sr)
		{
			Similarity = sim,
			// Metadata   = new[] { doc.video, doc.image },
			Title = filename,

			Description = $"Episode #{epStr} @ " +
			              $"[{TimeSpan.FromSeconds(from):g} - {TimeSpan.FromSeconds(to):g}]",
		};

		// result.Metadata.video = video;
		// result.Metadata.image = image;

		if (result.Similarity < TraceMoeEngine.FILTER_THRESHOLD) {
			/*result.OtherMetadata.Add("Note", $"Result may be inaccurate " +
												 $"({result.Similarity.Value / 100:P} " +
												 $"< {FILTER_THRESHOLD / 100:P})");*/
			//todo

			// result.Metadata.Warning = $"Similarity below threshold {FILTER_THRESHOLD:P}";
		}

		return result;
	}

}

#endregion