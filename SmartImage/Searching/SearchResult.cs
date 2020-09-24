#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using SimpleCore.Utilities;
using SimpleCore.Win32.Cli;
using SmartImage.Shell;
using SmartImage.Utilities;

namespace SmartImage.Searching
{
	public sealed class SearchResult : ConsoleOption
	{
		public SearchResult(ISearchEngine engine, string url, float? similarity = null) : this(engine.Color,
			engine.Name, url, similarity) { }

		public SearchResult(ConsoleColor color, string name, string url, float? similarity = null)
		{
			Url = url;
			Name = name;
			Color = color;

			Similarity = similarity;
			ExtendedInfo = new List<string>();
			ExpandedMatchResults = new List<string>();

		}

		public override ConsoleColor Color { get; }

		/// <summary>
		/// Best match
		/// </summary>
		public string Url { get; }

		public override string? Data => Format();

		public override string Name { get; }


		public float? Similarity { get; internal set; }

		public bool Success => Url != null;

		public List<string> ExtendedInfo { get; }

		/// <summary>
		/// Direct source matches, other extended results
		/// </summary>
		public List<string> ExpandedMatchResults { get; }

		public override Func<object> Function
		{
			get
			{
				return () =>
				{
					NetworkUtilities.OpenUrl(Url);
					return null;
				};
			}
		}

		public override Func<object>? AltFunction { get; internal set; }


		public override string ToString()
		{
			return String.Format("{0}: {1}", Name, Url);
		}


		private string Format()
		{
			var sb = new StringBuilder();

			char success = Success ? CliOutput.RAD_SIGN : CliOutput.MUL_SIGN;
			string altStr = AltFunction != null ? Commands.ALT_DENOTE : string.Empty;

			sb.AppendFormat("{0} {1}\n", success, altStr);

			if (Success) {

				sb.AppendFormat("\tResult: {0}\n", Url);
			}

			if (Similarity.HasValue) {
				sb.AppendFormat("\tSimilarity: {0:P}\n", Similarity/100);
			}

			

			foreach (string s in ExtendedInfo) {
				sb.AppendFormat("\t{0}\n", s);
			}

			for (int i = 0; i < ExpandedMatchResults.Count; i++) {
				string extraResult = ExpandedMatchResults[i];
				sb.AppendFormat("\tMatch result #{0}: {1}\n", i, extraResult);
			}

			return sb.ToString();
		}
	}
}