﻿using System;

namespace SmartImage.Lib.Engines
{
	/// <summary>
	/// Search engine options
	/// </summary>
	[Flags]
	public enum SearchEngineOptions
	{
		/// <summary>
		/// No engines
		/// </summary>
		None = 0,

		/// <summary>
		/// Automatic (use best result)
		/// </summary>
		Auto = 1,

		SauceNao = 1 << 1,

		Iqdb = 1 << 2,

		ImgOps = 1 << 3,

		//todo

		/// <summary>
		/// All engines
		/// </summary>
		All = SauceNao | Iqdb | ImgOps
	}
}