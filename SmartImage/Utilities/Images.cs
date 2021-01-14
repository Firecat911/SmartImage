﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Novus.Win32;
using SimpleCore.Console.CommandLine;

namespace SmartImage.Utilities
{
	/// <summary>
	/// Image utilities
	/// </summary>
	internal static class Images
	{
		internal static (int Width, int Height) GetDimensions(string img)
		{
			var bmp = new Bitmap(img);

			return (bmp.Width, bmp.Height);
		}

		internal static bool IsFileValid(string img)
		{
			if (String.IsNullOrWhiteSpace(img)) {
				return false;
			}

			if (!File.Exists(img)) {
				NConsole.WriteError($"File does not exist: {img}");
				return false;
			}

			bool isImageType = FileSystem.ResolveFileType(img).Type == FileType.Image;

			if (!isImageType) {
				return NConsoleIO.ReadConfirmation("File format is not recognized as a common image format. Continue?");
			}

			return true;
		}
	}
}