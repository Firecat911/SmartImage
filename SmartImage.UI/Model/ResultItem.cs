﻿// $User.Name $File.ProjectName $File.FileName
// $File.CreatedYear-$File.CreatedMonth-$File.CreatedDay @ $File.CreatedHour:$File.CreatedMinute

global using CBN = JetBrains.Annotations.CanBeNullAttribute;
global using USI = JetBrains.Annotations.UsedImplicitlyAttribute;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Cache;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using AngleSharp.Css;
using Flurl;
using Flurl.Http;
using JetBrains.Annotations;
using Kantan.Net.Utilities;
using Kantan.Utilities;
using Novus.FileTypes;
using Novus.OS;
using Novus.Streams;
using Novus.Win32;
using SmartImage.Lib.Clients;
using SmartImage.Lib.Model;
using SmartImage.Lib.Results;
using SmartImage.Lib.Utilities;
using SmartImage.UI.Controls;

namespace SmartImage.UI.Model;

#pragma warning disable CS8618

public class ResultItem : INotifyPropertyChanged, IBitmapImageSource, INamed, IItemSize, IDisposable
{

	private string m_previewText;
	private string m_label;

	public string PreviewText
	{
		get => m_previewText;
		set
		{
			if (value == m_previewText) return;

			m_previewText = value;
			OnPropertyChanged();
		}
	}

	public string Label
	{
		get => m_label;
		set
		{
			if (value == m_label) return;

			m_label = value;
			OnPropertyChanged();
		}
	}

	#region

	public bool CanDownload { get; internal set; }

	private BitmapImage m_statusImage;

	public string Name { get; set; }

	public SearchResultItem Result { get; }

	public BitmapImage StatusImage
	{
		get => m_statusImage;
		internal set
		{
			if (Equals(value, m_statusImage)) return;

			m_statusImage = value;
			OnPropertyChanged();
		}
	}

	// public Url? Url => Uni != null ? Uni.Value.ToString() : Result.Url;

	/// <summary>
	///     <see cref="SearchResultItem.Url" /> of <see cref="Result" />
	/// </summary>
	public Url? Url { get; protected set; }

	public bool CanScan { get; internal set; }

	public bool CanOpen { get; internal set; }

	public bool IsThumbnail { get; protected set; }

	public int? Width { get; internal set; }

	public int? Height { get; internal set; }

	public string StatusMessage { get; internal set; }

	public bool IsLowQuality => !Url.IsValid(Url) || Result.Root.Status.IsError() || Result.IsRaw;

	public BitmapSource? Image { get; /*protected*/ set; }

	[MNNW(true, nameof(Image))]
	public bool HasImage => Image is not null;

	public virtual bool CanLoadImage => !HasImage && Url.IsValid(Result.Thumbnail);

	public virtual string? Download { get; set; }

	public virtual bool IsDownloaded
	{
		get => Download != null;
		set { }
	}

	public bool IsSister { get; internal init; }

	public virtual long Size => Native.ERROR_SV;

	private double m_previewProgress;

	public double PreviewProgress
	{
		get => m_previewProgress;
		set
		{
			if (value.Equals(m_previewProgress)) return;

			m_previewProgress = value;
			OnPropertyChanged();
		}
	}

	#endregion

	public ResultItem(SearchResultItem result, string name)
	{
		Result = result;
		Name   = !result.IsRaw ? name : $"{name} (Raw)";

		Url     = result.Url;
		CanOpen = Url.IsValid(Url);
		CanScan = CanOpen;

		(Width, Height) = (Result.Width, Result.Height);

		if (Result.Root.Status.IsSuccessful()) {
			StatusImage = AppComponents.accept;
		}
		else if (Result.Root.Status.IsUnknown()) {
			StatusImage = AppComponents.help;
		}
		else if (Result.Root.Status.IsError()) {
			StatusImage = AppComponents.exclamation;
		}
		else {
			StatusImage = AppComponents.asterisk_yellow;
		}

		StatusMessage = $"[{Result.Root.Status}]";

		if (!String.IsNullOrWhiteSpace(result.Root.ErrorMessage)) {
			StatusMessage += $" :: {result.Root.ErrorMessage}";
		}

		Image = null;

		// Image = new Lazy<BitmapSource?>(LoadImage, LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public bool Open()
	{
		bool b = CanOpen;

		if (CanOpen) {
			b = FileSystem.Open(Url);

		}

		return b;
	}

	public Task<IFlurlResponse> GetResponseAsync(CancellationToken token = default)
	{
		return Url.AllowAnyHttpStatus()
			.WithAutoRedirect(true)
			.WithTimeout(TimeSpan.FromSeconds(3))
			.OnError(x =>
			{
				if (x.Exception is FlurlHttpException fx) {
					Debug.WriteLine($"{fx}");
				}

				x.ExceptionHandled = true;
			})
			.GetAsync(cancellationToken: token);
	}

	public void UpdateProperties()
	{
		OnPropertyChanged(nameof(CanOpen));
		OnPropertyChanged(nameof(IsDownloaded));
		OnPropertyChanged(nameof(IsSister));
		OnPropertyChanged(nameof(Label));
		OnPropertyChanged(nameof(Image));
	}

	protected virtual void OnImageDownloadCompleted(object? sender, EventArgs args)
	{
		PreviewText = $"Preview cache complete";

		if (Image is { CanFreeze: true }) {
			Image.Freeze();
		}

		if (HasImage) {
			CanDownload = true;
		}

		// CanDownload = HasImage;

		// OnPropertyChanged(nameof(Width));
		// OnPropertyChanged(nameof(Height));

		IsThumbnail = HasImage;
		OnPropertyChanged(nameof(IsThumbnail));

		// Properties &= ResultItemProperties.Thumbnail;

		UpdateProperties();

		if (Image is BitmapImage bmp) {
			Width  = bmp.PixelWidth;
			Height = bmp.PixelHeight;
			OnPropertyChanged(nameof(Height));
			OnPropertyChanged(nameof(Width));
			OnPropertyChanged(nameof(Size));
		}
	}

	protected virtual void OnImageDownloadProgress(object? sender, DownloadProgressEventArgs args)
	{
		PreviewProgress = ((float) args.Progress);
		PreviewText     = $"Preview cache...{PreviewProgress}";

	}

	protected virtual void OnImageDownloadFailed(object? sender, ExceptionEventArgs args)
	{
		PreviewProgress = 0;
		PreviewText     = $"Preview fetch failed: {args.ErrorException.Message}";

	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		var eventArgs = new PropertyChangedEventArgs(propertyName);
		PropertyChanged?.Invoke(this, eventArgs);

		// Debug.WriteLine($"{this} :: {eventArgs.PropertyName}");
	}

	protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;

		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public virtual bool LoadImage()
	{
		if (HasImage) {
			return true;
		}
		else if (!CanLoadImage) {
			return false;
		}

		Debug.WriteLine($"{nameof(LoadImage)} :: {Name} / {Result}");

		var img = new BitmapImage()
			{ };

		// Label = $"Loading {Name}";

		/*
		 * NOTE:
		 * BitmapCreateOptions.DelayCreation does not seem to work properly so this is a workaround.
		 *
		 */

		img.BeginInit();
		img.UriSource = new Uri(Result.Thumbnail);
		/*var task = Result.Thumbnail.GetAsync();
		task.Wait();
		var async = task.Result.GetStreamAsync();
		async.Wait();
		img.StreamSource  =  async.Result;*/
		img.CacheOption = BitmapCacheOption.OnDemand;

		// Image.CreateOptions = BitmapCreateOptions.DelayCreation;
		// Image.CreateOptions = BitmapCreateOptions.None;
		
		img.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.Reload) { };
		img.EndInit();

		img.DownloadFailed    += OnImageDownloadFailed;
		img.DownloadProgress  += OnImageDownloadProgress;
		img.DownloadCompleted += OnImageDownloadCompleted;

		Image = img;

		// UpdateProperties();
		return HasImage;
	}

	#region

	public virtual void Dispose()
	{
		Debug.WriteLine($"Disposing {Name}");
		GC.SuppressFinalize(this);
		Result.Dispose();
		Image = null;
	}

	public virtual async Task<string> DownloadAsync(string? dir = null, bool exp = true)
	{
		if (!Url.IsValid(Url) || !HasImage) {
			return null;
		}

		string path;

		path = Url.GetFileName();

		dir ??= AppUtil.MyPicturesFolder;
		var path2 = Path.Combine(dir, path);

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(Image));

		await using (var fs = new FileStream(path2, FileMode.Create)) {
			encoder.Save(fs);
		}

		StatusImage = AppComponents.picture_save;

		if (exp) {
			FileSystem.ExploreFile(path2);
		}

		CanDownload = false;
		Download    = path2;

		// u.Dispose();
		UpdateProperties();

		return path2;
	}

	// public event PropertyChangedEventHandler? PropertyChanged;

	#endregion

}