﻿using System.Dynamic;
using Flurl;

namespace SmartImage.Lib;

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

	public dynamic Metadata { get; internal set; }

	internal SearchResultItem(SearchResult r)
	{
		Root     = r;
		Metadata = new ExpandoObject();
	}

	#region Overrides of Object

	public override string ToString()
	{
		return $"[link]{Url}[/] {Similarity/100:P} {Artist} {Description} {Site} {Source} {Title}";
	}

	#endregion

	public static bool Validate([CBN] SearchResultItem r)
	{
		return r switch
		{
			not { } => false,
			_       => true
		};
	}
}