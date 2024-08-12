﻿// Author: Deci | Project: SmartImage.Lib | Name: UniImageFile.cs
// Date: 2024/07/17 @ 02:07:16

using SixLabors.ImageSharp.Formats;

namespace SmartImage.Lib.Images.Uni;

public class UniImageFile : UniImage
{	

	internal UniImageFile(object value, FileInfo fi, IImageFormat format = null)
		: base(value, UniImageType.File, format)
	{
		FileInfo = fi;
	}

	public FileInfo FileInfo { get; }

	

	public override async ValueTask<bool> Alloc(CancellationToken ct = default)
	{
		Stream = File.OpenRead(FileInfo.FullName);
		return HasStream;
	}

	public static bool IsFileType(object o, out FileInfo f)
	{
		f = null;

		if (o is string { } s && File.Exists(s)) {
			f = new FileInfo(s);
		}

		return f != null;
	}

}