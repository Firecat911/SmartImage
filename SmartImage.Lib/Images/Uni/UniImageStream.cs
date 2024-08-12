﻿// Author: Deci | Project: SmartImage.Lib | Name: UniImageStream.cs
// Date: 2024/07/17 @ 02:07:31

using SixLabors.ImageSharp.Formats;

namespace SmartImage.Lib.Images.Uni;

public class UniImageStream : UniImage
{

	internal UniImageStream(object value, Stream str, IImageFormat format = null)
		: base(value, str, UniImageType.Stream, format) { }


	public static bool IsStreamType(object o, out Stream t2)
	{
		t2 = Stream.Null;

		if (o is Stream sz) {
			t2 = sz;
		}

		return t2 != Stream.Null;
	}

	public override async ValueTask<bool> Alloc(CancellationToken ct = default)
	{
		return HasStream;
	}

}