﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flurl.Http;
using Kantan.Net.Utilities;
using SmartImage.Lib.Results;

namespace SmartImage.Lib.Engines.Impl.Upload;

public sealed class PomfEngine : BaseUploadEngine
{

	public PomfEngine() : base("https://pomf.lain.la/upload.php") { }

	public override long? MaxSize => 1_000_000_000;

	public override string Name => "Pomf";

	public static readonly BaseUploadEngine Instance = new PomfEngine();

	public override async Task<UploadResult> UploadFileAsync(string file, CancellationToken ct = default)
	{
		Verify(file);

		var response = await Client.Request(EndpointUrl)
			               .WithSettings(r =>
			               {
				               r.Timeout = Timeout;
			               }).OnError(r =>
			               {
				               r.ExceptionHandled = true;
				               Trace.WriteLine($"{r.Exception.Message}: {file} {Name}");
			               })
			               .PostMultipartAsync(mp =>
			               {
				               mp.AddFile("files[]", file);
			               }, cancellationToken: ct);

		if (response == null) {
			Debugger.Break();
			return new UploadResult()
			{
				IsValid = false
			};
		}

		var pr = await response.GetJsonAsync<PomfResult>();

		var bur = new UploadResult()
		{
			Value    = pr,
			Size     = pr.Files[0].Size,
			Url      = pr.Files[0].Url,
			IsValid  = pr.Success,
			Response = response
		};

		return bur;
	}

}

public sealed class PomfResult
{

	public bool Success { get; set; }

	public PomfFileResult[] Files { get; set; }

}

public sealed class PomfFileResult
{

	public string Hash { get; set; }

	public string Name { get; set; }

	public string Url { get; set; }

	public long Size { get; set; }

}