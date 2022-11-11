﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using Windows.ApplicationModel.Background;
using Flurl.Http;
using Kantan.Net;
using Kantan.Net.Utilities;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Uwp.Notifications;
using Novus.FileTypes;
using SmartImage.Lib;

namespace SmartImage.App;

// ReSharper disable PossibleNullReferenceException
internal static class AppToast
{
	internal static async Task ShowToast(object sender, List<UniFile> args)
	{
		Debug.WriteLine($"Building toast", C_DEBUG);

		var builder = new ToastContentBuilder();
		var button  = new ToastButton();
		var button2 = new ToastButton();

		button2.SetContent("Dismiss")
		       .AddArgument(ARG_KEY_ACTION, ARG_VALUE_DISMISS);

		builder.AddText("Search Complete");

		string? url    = null;
		var     result = args.First();
		builder.AddText($"Engine: {sender}");

		button.SetContent("Open")
		      .AddArgument(ARG_KEY_ACTION, $"{url}");

		builder.AddButton(button)
		       .AddButton(button2)
		       .AddAttributionText($"{url}")
		       .AddText($"Results: {args.Count}");

		await AddNotificationImage(builder, result);

		builder.SetBackgroundActivation();
		builder.Show();

	}

	private static async Task AddNotificationImage(ToastContentBuilder builder, UniFile directResults)
	{
		var uri = new Uri(directResults.Value);
		var f   = await uri.DownloadFileAsync(Path.GetTempPath());

		builder.AddHeroImage(new Uri(f));

		/*if (!directResults.Any()) {
			return;
		}

		/*var query = Program.Config.Query;

		var img = Image.FromStream(query.Stream);

		var aspectRatio = (double) img.Width / img.Height;

		for (int di = 0; di < directResults.Count; di++) {
			var ix = directResults[di];

		}#1#

		// var ar1   = (double) query.Width.Query / query.Height.Query;

		var mediaResources = directResults.ToList();

		// var directImage = directResults.First();

		var path = Path.GetTempPath();

		// string file = MediaHelper.Download(directImage.DirectImage.Url, path);

		var mediaResource = mediaResources.First();

		string file = HttpUtilities.Download(new Uri(mediaResource.Value), path);

		if (file == null) {
			int i = 0;

			do {
				// file = MediaHelper.Download(directResults[i++].DirectImage.Url, path);

				file = HttpUtilities.Download(new Uri(mediaResources[i++].Value), path);

			} while (String.IsNullOrWhiteSpace(file) && i < directResults.Count);

		}

		/*#1#

		if (file != null) {
			// NOTE: The file size limit doesn't seem to actually matter...

			Debug.WriteLine($"{nameof(AppToast)}: Downloaded {file}", C_INFO);

			builder.AddHeroImage(new Uri(file));

			AppDomain.CurrentDomain.ProcessExit += (_, _) =>
			{
				File.Delete(file);
			};
		}*/
	}

	internal static void OnToastActivated(ToastNotificationActivatedEventArgsCompat compat)
	{
		// NOTE: Does not return if invoked from background

		// Obtain the arguments from the notification

		var arguments = ToastArguments.Parse(compat.Argument);

		foreach (var argument in arguments) {
			Debug.WriteLine($"Toast argument: {argument}", C_DEBUG);

			if (argument.Key == ARG_KEY_ACTION) {

				if (argument.Value == ARG_VALUE_DISMISS) {
					break;
				}

				HttpUtilities.OpenUrl(argument.Value);
			}
		}

		if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated()) {

			// ToastNotificationManagerCompat.History.Clear();
			// Environment.Exit(0);

			// Closes toast ...

			return;
		}
	}

	private static async void RegisterBackground()
	{
		const string taskName = "ToastBackgroundTask";

		// If background task is already registered, do nothing
		if (BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals(taskName)))
			return;

		// Otherwise request access
		BackgroundAccessStatus status = await BackgroundExecutionManager.RequestAccessAsync();

		// Create the background task
		var builder = new BackgroundTaskBuilder()
		{
			Name = taskName
		};

		// Assign the toast action trigger
		builder.SetTrigger(new ToastNotificationActionTrigger());

		// And register the task
		BackgroundTaskRegistration registration = builder.Register();

		// todo
	}

	private const string ARG_KEY_ACTION    = "action";
	private const string ARG_VALUE_DISMISS = "dismiss";
}