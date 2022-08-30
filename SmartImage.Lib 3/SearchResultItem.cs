﻿using Flurl;

namespace SmartImage_3.Lib;

public record SearchResultItem
{
	public SearchResult Root { get; }

	public Url Url { get; internal set; }

	public string Title { get; internal set; }

	public string Source { get; internal set; }

	public double? Width { get; internal set; }

	public double? Height { get; internal set; }

	public string Artist { get; internal set; }

	public string Description { get; internal set; }

	public string Site { get; internal set; }

	public double? Similarity { get; internal set; }

	internal SearchResultItem(SearchResult r)
	{
		Root = r;
	}

	#region Overrides of Object

	public override string ToString()
	{
		return $"{Root} :: {Url} | {Similarity} {Artist} {Source}";
	}

	#endregion
}