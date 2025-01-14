﻿// Read S SmartImage.UI MainWindow.State.cs
// 2023-08-04 @ 1:24 PM

global using MN = System.Diagnostics.CodeAnalysis.MaybeNullAttribute;
global using ICBN = JetBrains.Annotations.ItemCanBeNullAttribute;
global using NN = System.Diagnostics.CodeAnalysis.NotNullAttribute;
using System.Windows;
using SmartImage.UI.Model;
using Kantan.Monad;
using System.Linq;
using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using SmartImage.Lib;
using SmartImage.UI.Controls;
using System.Diagnostics;

namespace SmartImage.UI;

public partial class MainWindow
{

	#region

	private ResultItem m_currentResult;

	// [MN]
	public ResultItem CurrentResult
	{
		get => m_currentResult;
		set
		{
			if (Equals(value, m_currentResult)) return;

			m_currentResult = value;
			OnPropertyChanged();
		}
	}

	#endregion

	#region

	private void OpenResultWindow(ResultItem ri)
	{
		var sw = new ResultWindow(ri)
			{ };

		if (ri is UniResultItem { HasImage: true } uri) {
			sw.Img_Preview.Source = uri.Image;
		}
		else if (ri.HasImage) {
			sw.Img_Preview.Source = ri.Image;
		}
		else {
			sw.Img_Preview.Source = Image;
		}

		sw.Show();
	}

	private ResultItem? FindResult(Predicate<ResultItem> f)
	{
		if (!HasQuerySelected) {
			return null;
		}

		return CurrentQuery.Results.FirstOrDefault(t => f(t));

	}

	private ResultItem? FindParent(ResultItem r)
	{
		foreach (ResultItem item in CurrentQuery.Results) {
			/*
			if (item.Result.Children.Contains(r.Result)) {
				return item;
			}
		*/

			if (item.Result.Parent == r.Result) {
				return item;
			}
		}

		return null;
	}

	private int FindResultIndex(Predicate<ResultItem> f)
	{
		var r = FindResult(f);

		if (r == null || !HasQuerySelected) {
			return -1;
		}

		return CurrentQuery.Results.IndexOf(r);
	}

	#endregion

	private bool m_hasResultSelected;

	[MNNW(true, nameof(CurrentResult))]
	public bool HasResultSelected
	{
		get { return m_hasResultSelected; }
		set
		{
			m_hasResultSelected = value;
			OnPropertyChanged();
		}
	}

	#region

	public QueryModel? FindQueue(string s)
	{
		var x = Queue.FirstOrDefault(x => x.Value == s);

		return x;
	}

	public void ClearQueue()
	{
		lock (Queue) {
			foreach (var kv in Queue) {
				kv.Dispose();
			}

			Lb_Queue.Dispatcher.Invoke(() =>
			{
				Lb_Queue.SelectedIndex = -1;
				Queue.Clear();
				var rm = new QueryModel();
				Queue.Add(rm);

				// Lb_Queue.SelectedIndex = 0;
				CurrentQuery = rm;
			});

			// CurrentQueueItem       = new ResultModel();

			/*var item = new ResultModel();
			Queue.Add(item);
			CurrentQueueItem = item;*/
		}
	}

	public bool SetQueue(string s, out QueryModel? qm)
	{
		qm = FindQueue(s);

		var b = qm == null;

		if (b) {
			qm = new QueryModel(s);
			Queue.Add(qm);
			CurrentQuery = qm;
		}

		return b;
	}

	#endregion

	#region

	private bool m_showMedia;

	public bool ShowMedia
	{
		get => m_showMedia;
		set
		{
			if (value == m_showMedia) return;

			m_showMedia = value;
			OnPropertyChanged();
		}
	}

	private void CheckMedia()
	{
		if (ShowMedia) {
			CloseMedia();

			// Me_Preview.Pause();
			// ShowMedia = false;
		}
		else { }
	}

	private void CloseMedia()
	{
		m_ctsMedia.Cancel();

		Me_Preview.Stop();

		// Me_Preview.Position = TimeSpan.Zero;
		Me_Preview.Close();

		Me_Preview.ClearValue(MediaElement.SourceProperty);
		Me_Preview.Source = null;

		// Me_Preview.Dispose();
		ShowMedia  = false;
		m_isPaused = false;
		m_ctsMedia = new CancellationTokenSource();
	}

	private bool m_isPaused;

	private void PlayPauseMedia()
	{
		if (ShowMedia) {

			if (m_isPaused) {
				Me_Preview.Play();
				m_isPaused = false;
			}
			else {

				Me_Preview.Pause();
				m_isPaused = true;
			}
		}

	}

	#endregion
		#region

	private void SetPreview(IBitmapImageSource igs)
	{
		// m_ctsMedia.Cancel();

		Application.Current.Dispatcher.Invoke(() =>
		{
			if (Img_Preview.Source != null) {
				if (Img_Preview.Source.Dispatcher != null) {
					if (!Img_Preview.Source.Dispatcher.CheckAccess()) {
						return;
					}

				}
			}
		});

		var load = igs.LoadImage();
		
		if (!load) {
			SetPreviewToCurrentQuery();
			return;
		}

		string name = igs is INamed n ? n.Name : ControlsHelper.STR_NA;
		string n2;

		if (igs.IsThumbnail) {
			/*
			if (igs.IsThumbnail) {
				n2 = "thumbnail";

			}
			else {
				n2 = "full res";
			}
			*/
			n2 = "thumbnail";
		}
		else {
			n2 = ControlsHelper.STR_NA;
		}

		string? name2 = null;

		if (igs is ResultItem rri) {

			if (rri.IsSister) {
				/*var grp=CurrentQueueItem.Results.GroupBy(x => x.Result.Root);

				foreach (IGrouping<SearchResult, ResultItem> items in grp) {
					// var zz=items.GroupBy(y => y.Result.Root.AllResults.Where(yy => yy.Sisters.Contains(y.Result)));

				}*/
				var p = FindParent(rri);

				if (p != null) {
					Debug.WriteLine($"couldn't find parent for {rri}/{p}");
					name  = p.Name;
					name2 = $"(child)";

				}
			}
			else {
				name2 = "(parent)";
			}
		}

		/*Tb_Preview.Dispatcher.Invoke(() =>
		{

		});*/

		Img_Preview.Dispatcher.Invoke(() =>
		{
			// igs.LoadImage();
			OnPropertyChanged(nameof(igs));
			Img_Preview.Source = igs.Image;
		});

		// Debug.WriteLine($"updated image {ri.Image}");
		// PreviewChanged?.Invoke(ri);

		/*if (ri.Image != null) {
				using var bmp1 = CurrentQueueItem.Image.BitmapImage2Bitmap();
				using var bmp2 = ri.Image.BitmapImage2Bitmap();
				mse = AppUtil.CompareImages(bmp1, bmp2,1);

			}*/

		// Tb_Preview.Text = $"Preview: {name} ({n2}) {name2}";

		// m_us2.Release();

	}

	private void SetPreviewToCurrentQuery()
	{
		SetPreview(CurrentQuery);

		// UpdatePreview(m_image);
		// Tb_Preview.Text = $"Preview: (query)";
	}

	#endregion

}