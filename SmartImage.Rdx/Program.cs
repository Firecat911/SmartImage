﻿using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Flurl.Http;
using Flurl.Http.Configuration;
using Kantan.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novus.Streams;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SmartImage.Lib;
using SmartImage.Lib.Images;
using SmartImage.Lib.Images.Uni;
using Spectre.Console;
using Spectre.Console.Cli;
using SmartImage.Rdx.Shell;
using SmartImage.Rdx.Utilities;

namespace SmartImage.Rdx;

/*
 * cd /mnt/c/Users/Deci/RiderProjects/SmartImage/
 * dotnet run --project SmartImage.Rdx/ "$HOME/1654086015521.png"
 * dotnet run -c 'DEBUG' --project SmartImage.Rdx "$HOME/1654086015521.png"
 * dotnet run -lp 'SmartImage.Rdx' -c 'WSL' --project SmartImage.Rdx "$HOME/1654086015521.png"
 * dotnet SmartImage.Rdx/bin/Debug/net8.0/SmartImage.Rdx.dll "/home/neorenegade/1654086015521.png"
 * dotnet run -c Test --project SmartImage.Rdx --  "/home/neorenegade/0c4c80957134d4304538c27499d84dbe.jpeg" -e All -p Auto
 * ./SmartImage.Rdx/bin/Release/net8.0/publish/linux-x64/SmartImage "/home/neorenegade/0c4c80957134d4304538c27499d84dbe.jpeg"
 * dotnet run --project SmartImage.Rdx -- --help
 * dotnet run --project SmartImage.Rdx/ "C:\Users\Deci\Pictures\Epic anime\Kallen_FINAL_1-3.png" --search-engines All --output-format "Delimited" --output-file "output.csv" --read-cookies
 * echo -nE $cx1 | dotnet run -c WSL --project SmartImage.Rdx --
 * "C:\Users\Deci\Pictures\Art\Makima 1-3.png" | dotnet run -c Debug --project SmartImage.Rdx --
 * $cx2=[System.IO.File]::ReadAllBytes($(Resolve-Path "..\..\Pictures\Art\fucking_epic.jpg"))
 * cd /mnt/c/Users/Deci/RiderProjects/SmartImage/
 * ./SmartImage.Rdx/bin/Debug/net8.0/SmartImage
 * dotnet run -c Test --project SmartImage.Rdx/ "C:\Users\Deci\Pictures\Epic anime\Kallen_FINAL_1-3.png" --search-engines All
 */

public static class Program
{

	public static async Task<int> Main(string[] args)
	{
		Debug.WriteLine(AConsole.Profile.Height);
		Debug.WriteLine(Console.BufferHeight);

#if DEBUG
		Debugger.Launch();
#endif
		/*if (args.Length == 0) {
			var prompt = new TextPrompt<string>("Input")
			{
				Converter = s =>
				{
					/*
					var task = SearchQuery.TryCreateAsync(s);
					task.Wait();
					var res = task.Result;
					#1#

					if (UniImage.IsValidSourceType(s)) {
						// var sq = SearchQuery.TryCreateAsync(s).Result;

						return s;
					}

					else {
						return null;
					}
				}
			};
			var sz = AConsole.Prompt(prompt);

			args = [sz];
		}*/

		if (Console.IsInputRedirected) {
			Trace.WriteLine("Input redirected");
			var pipeInput = ConsoleUtil.ParseInputStream();

			var newArgs = new string[args.Length + 1];
			newArgs[0] = pipeInput;
			args.CopyTo(newArgs, 1);

			args = newArgs;

			AConsole.WriteLine($"Received input from stdin");
		}

		var ff = ConsoleFormat.LoadFigletFontFromResource(nameof(R2.Fg_larry3d), out var ms);

		// ms?.Dispose();

		var fg = new FigletText(ff, R1.Name)
			.LeftJustified()
			.Color(ConsoleFormat.Clr_Misc1);

		AConsole.Write(fg);

#if DEBUG
		Trace.WriteLine(args.QuickJoin());
#endif

		Grid grd = ConsoleFormat.CreateInfoGrid();

		AConsole.Write(grd);

		// var env = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);

		var app = new CommandApp<SearchCommand>();

		app.Configure(c =>
		{
			c.PropagateExceptions();
			var helpProvider = new CustomHelpProvider(c.Settings);
			c.SetHelpProvider(helpProvider);

			c.AddCommand<IntegrationCommand>("integrate")
				.WithDescription("Configure system integration such as context menu");

		});
		int x = SearchCommand.EC_OK;

		try {
			x = await app.RunAsync(args);

		}
		catch (Exception e) {
			AConsole.WriteException(e);
			x = SearchCommand.EC_ERROR;
		}
		finally {

			if (x != SearchCommand.EC_OK) {
				AConsole.Confirm("Press any key to continue");
			}
		}

		return x;
	}

}