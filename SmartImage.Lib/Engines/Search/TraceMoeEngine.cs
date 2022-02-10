using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Flurl.Http;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp.Serialization.Json;
using SmartImage.Lib.Clients;
using SmartImage.Lib.Engines.Search.Base;
using SmartImage.Lib.Searching;
using static Kantan.Diagnostics.LogCategories;

// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006, IDE0051
namespace SmartImage.Lib.Engines.Search;

/// <summary>
/// 
/// </summary>
/// <a href="https://soruly.github.io/trace.moe/#/">Documentation</a>
public sealed class TraceMoeEngine : ClientSearchEngine
{
	public TraceMoeEngine() : base("https://trace.moe/?url=", "https://api.trace.moe") { }

	//public override TimeSpan Timeout => TimeSpan.FromSeconds(4);

	/// <summary>
	/// Used to retrieve more information about results
	/// </summary>
	private readonly AnilistClient m_anilistClient = new();


	public override string Name => "trace.moe";

	public override EngineSearchType SearchType => EngineSearchType.External | EngineSearchType.Metadata;

	public override SearchEngineOptions EngineOption => SearchEngineOptions.TraceMoe;

	protected override SearchResult Process(object obj, SearchResult r)
	{

		//var r = base.GetResult(url);
		var query = (ImageQuery) obj;
		// https://soruly.github.io/trace.moe/#/


		TraceMoeRootObject tm = null;

		try {
			IFlurlRequest request = (EndpointUrl + "/search")
			                        .AllowAnyHttpStatus()
			                        .SetQueryParam("url",
			                                       query.UploadUri.ToString(),
			                                       true);

			var task = request.GetStringAsync();

			task.Wait(Timeout);
			var result = task.Result;

			/*JObject jo;

			jo = JObject.Parse(result);
			var token = jo["result"];

			Debug.WriteLine(result);
			var rg    = JsonConvert.DeserializeObject<List<TraceMoeDoc>>(token.ToString());

			tm            = new();
			tm.result     = rg.ToList();
			tm.error      = jo["error"].ToString();
			tm.frameCount = long.Parse(jo["frameCount"].ToString());*/


			// tm = JsonConvert.DeserializeObject<TraceMoeRootObject>(result);
			
			var settings = new JsonSerializerSettings
			{
				Error = (sender, args) =>
				{
					if (object.Equals(args.ErrorContext.Member, nameof(TraceMoeDoc.episode)) &&
					    args.ErrorContext.OriginalObject.GetType() == typeof(TraceMoeDoc))
					{
						args.ErrorContext.Handled = true;
					}
				}
			};
			tm = JsonConvert.DeserializeObject<TraceMoeRootObject>(result, settings);


		}
		catch (Exception e) {
			Debug.WriteLine($"{e.Message}");

			goto ret;
		}

		if (tm != null) {
			if (tm.result != null) {
				// Most similar to least similar

				try {
					var results = ConvertResults(tm, r).ToList();
					var best    = results[0];

					r.PrimaryResult = best;
					r.RawUri        = new Uri(BaseUrl + query.UploadUri);
					r.OtherResults.AddRange(results);
				}
				catch (Exception e) {
					r.ErrorMessage = e.Message;
					r.Status       = ResultStatus.Failure;
				}

			}
			else if (tm.error != null) {
				Debug.WriteLine($"{Name}: API error: {tm.error}", C_ERROR);
				r.ErrorMessage = tm.error;

				if (r.ErrorMessage.Contains("Search queue is full")) {
					r.Status = ResultStatus.Unavailable;
				}
			}
		}

		ret:

		r.PrimaryResult.Quality = r.PrimaryResult.Similarity switch
		{
			>= FILTER_THRESHOLD => ResultQuality.High,
			_ or null           => ResultQuality.NA,
		};

		return r;
	}

	private IEnumerable<ImageResult> ConvertResults(TraceMoeRootObject obj, SearchResult r)
	{
		var docs    = obj.result;
		var results = new ImageResult[docs.Count];

		for (int i = 0; i < results.Length; i++) {
			var   doc = docs[i];
			float sim = MathF.Round((float) (doc.similarity * 100.0f), 2);

			var result = new ImageResult(r)
			{
				Similarity  = sim,
				Description = $"Episode #{doc.episode} @ {TimeSpan.FromSeconds(doc.from)}"
			};

			try {
				string anilistUrl = ANILIST_URL + doc.anilist;
				string name       = m_anilistClient.GetTitle((int) doc.anilist);
				result.Source = name;
				result.Url    = new Uri(anilistUrl);
			}
			catch (Exception e) {
				Debug.WriteLine($"{e.Message}");
			}

			if (result.Similarity < FILTER_THRESHOLD) {
				result.OtherMetadata.Add("Note", $"Result may be inaccurate " +
				                                 $"({result.Similarity.Value / 100:P} " +
				                                 $"< {FILTER_THRESHOLD / 100:P})");
			}

			results[i] = result;
		}

		return results;
	}

	/// <summary>
	/// https://anilist.co/anime/{id}/
	/// </summary>
	private const string ANILIST_URL = "https://anilist.co/anime/";

	/// <summary>
	/// Threshold at which results become inaccurate
	/// </summary>
	private const float FILTER_THRESHOLD = 87.00F;


	#region API Objects

	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	private class TraceMoeDoc
	{
		public double from { get; set; }

		public double to { get; set; }

		public long anilist { get; set; }

		public string filename { get; set; }


		/// <remarks>Episode field may contain multiple possible results delimited by <c>|</c></remarks>
		[JsonIgnore]
		public string episode { get; set; }

		public double similarity { get; set; }

		public string video { get; set; }

		public string image { get; set; }
	}

	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	private class TraceMoeRootObject
	{
		public long frameCount { get; set; }

		public string error { get; set; }

		public List<TraceMoeDoc> result { get; set; }
	}

	#endregion
}