﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using AngleSharp.Dom;
using Flurl.Http;
using Kantan.Collections;
using Kantan.Net.Utilities;
using Kantan.Text;
using Kantan.Utilities;
using Microsoft.Extensions.Logging;
using Novus.FileTypes;
using Novus.OS;
using Novus.Win32;
using SmartImage.Lib;
using SmartImage.Lib.Engines;
using SmartImage.Lib.Results;
using SmartImage.Lib.Utilities;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Path = System.IO.Path;
using Timer = System.Timers.Timer;
using Url = Flurl.Url;

namespace SmartImage.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IDisposable
{
	private static readonly string[] Args;

	static MainWindow()
	{
		Args = Environment.GetCommandLineArgs();

	}

	public MainWindow()
	{

		Client    = new SearchClient(new SearchConfig());
		m_queries = new ConcurrentDictionary<string, SearchQuery>();

		InitializeComponent();

		foreach (var arg in Args) {
			Tb_Log.Text += $"{arg}\n";
		}

		DataContext = this;
		Results     = new();

		Query      = SearchQuery.Null;
		Queue      = new();
		m_queuePos = 0;
		m_cts      = new CancellationTokenSource();

		Engines1                = new(Engines);
		Engines2                = new(Engines);
		Lb_Engines.ItemsSource  = Engines1;
		Lb_Engines2.ItemsSource = Engines2;
		Lb_Engines.HandleEnumList(Engines1, Config.SearchEngines);
		Lb_Engines2.HandleEnumList(Engines2, Config.PriorityEngines);

		Lv_Results.ItemsSource = Results;
		Lv_Queue.ItemsSource   = Queue;

		Client.OnResult   += OnResult;
		Client.OnComplete += OnComplete;

		m_cbDispatch = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1)
		};
		m_cbDispatch.Tick += ClipboardListenAsync;

		m_uni                    = new();
		m_clipboard              = new();
		m_are                    = new AutoResetEvent(false);
		Cb_ContextMenu.IsChecked = AppUtil.IsContextMenuAdded;

		var e = Args.GetEnumerator();

		while (e.MoveNext()) {
			var c = e.Current.ToString();

			if (c == R2.Arg_Input) {
				var inp = e.MoveAndGet();
				InputText = inp.ToString();
				continue;
			}

			if (c == R2.Arg_AutoSearch) {

				e.MoveNext();
				Config.AutoSearch = true;
				continue;
			}
		}

		BindingOperations.EnableCollectionSynchronization(Results, m_lock);
	}

	#region

	private static readonly ILogger Logger = LogUtil.Factory.CreateLogger(nameof(MainWindow));

	public static SearchEngineOptions[] Engines { get; } = Enum.GetValues<SearchEngineOptions>();

	private readonly object m_lock = new();

	#endregion

	#region

	/// <summary>
	/// <see cref="Lb_Engines"/>
	/// <see cref="SearchConfig.SearchEngines"/>
	/// </summary>
	public List<SearchEngineOptions> Engines1 { get; }

	/// <summary>
	/// <see cref="Lb_Engines2"/>
	/// <see cref="SearchConfig.PriorityEngines"/>
	/// </summary>
	public List<SearchEngineOptions> Engines2 { get; }

	public SearchClient Client { get; }

	public SearchConfig Config => Client.Config;

	public SearchQuery Query { get; internal set; }

	public ObservableCollection<ResultItem> Results { get; set; }

	public ObservableCollection<string> Queue { get; }

	public string InputText
	{
		get => Tb_Input.Text;
		set => Tb_Input.Text = value;
	}

	#endregion

	#region

	private readonly ConcurrentDictionary<ResultItem, UniSource[]> m_uni;

	private readonly List<string> m_clipboard;

	private readonly DispatcherTimer m_cbDispatch;

	private readonly ConcurrentDictionary<string, SearchQuery> m_queries;

	private int m_queuePos;

	private BitmapImage             m_image;
	private CancellationTokenSource m_cts;
	private AutoResetEvent          m_are;

	private static int Status = S_OK;

	private const int S_NO = 0;
	private const int S_OK = 1;

	#endregion

	#region

	private async Task RunAsync()
	{
		Clear();
		var r = await Client.RunSearchAsync(Query, token: m_cts.Token);
	}

	private bool IsInputReady()
	{
		return !string.IsNullOrWhiteSpace(InputText);
	}

	private async void ClipboardListenAsync(object? s, EventArgs e)
	{
		var cImg  = Clipboard.ContainsImage();
		var cText = Clipboard.ContainsText();
		var cFile = Clipboard.ContainsFileDropList();

		if (cImg) {
			if (IsInputReady() || Query != SearchQuery.Null) {
				return;
			}

			var bmp = Clipboard.GetImage();
			// var df=DataFormats.GetDataFormat((int) ClipboardFormat.PNG);
			var fn = Path.GetTempFileName().Split('.')[0] + ".png";
			var ms = File.Open(fn, FileMode.OpenOrCreate);
			InputText = fn;
			BitmapEncoder enc = new PngBitmapEncoder();
			enc.Frames.Add(BitmapFrame.Create(bmp));
			enc.Save(ms);
			ms.Dispose();
			await SetQueryAsync(fn);
		}

		if (cText) {
			var txt = (string) Clipboard.GetData(DataFormats.Text);

			if (SearchQuery.IsValidSourceType(txt)) {

				if (!IsInputReady() && !m_clipboard.Contains(txt)) {
					m_clipboard.Add(txt);
					InputText = txt;
					await SetQueryAsync(txt);
				}
			}

			return;
		}

		if (cFile) {
			var files = Clipboard.GetFileDropList();
			var rg    = new string[files.Count];
			files.CopyTo(rg, 0);
			rg = rg.Where(x => !m_clipboard.Contains(x)).ToArray();
			EnqueueAsync(rg);
			m_clipboard.AddRange(rg);

			return;
		}

		// Thread.Sleep(1000);
	}

	private async Task SetQueryAsync(string q)
	{
		Interlocked.Exchange(ref Status, S_NO);

		Btn_Run.IsEnabled = false;
		bool b;

		if (m_queries.TryGetValue(q, out var qq)) {
			Query = qq;
			b     = true;
		}

		else {
			Query                     = await SearchQuery.TryCreateAsync(q);
			Pb_Status.IsIndeterminate = true;
			b                         = Query != SearchQuery.Null;
		}

		if (b) {
			var u = await Query.UploadAsync();

			Tb_Input2.Text            = u;
			Pb_Status.IsIndeterminate = false;
			Img_Preview.Source        = m_image = new BitmapImage(new Uri(Query.Uni.Value.ToString()));
			Tb_Input3.Text            = $"{Query.Uni.SourceType} {Query.Uni.FileTypes[0]}";

			m_queries.TryAdd(q, Query);

			if (Config.AutoSearch) {
				Dispatcher.InvokeAsync(RunAsync);

			}
		}
		else { }

		Btn_Run.IsEnabled = b;
		Interlocked.Exchange(ref Status, S_OK);

	}

	private void OnComplete(object sender, SearchResult[] e) { }

	private void OnResult(object o, SearchResult result)
	{
		Pb_Status.Value += (Results.Count / (double) Client.Engines.Length) * 10;

		lock (m_lock) {
			int i = 0;

			var allResults = result.AllResults;

			var sri1 = new SearchResultItem(result)
			{
				Url = result.RawUrl,
			};

			Results.Add(new ResultItem(sri1, $"{sri1.Root.Engine.Name} (Raw)"));

			foreach (SearchResultItem sri in allResults) {
				Results.Add(new ResultItem(sri, $"{sri.Root.Engine.Name} #{++i}"));

			}
		}
	}

	private async void EnqueueAsync(string[] files)
	{
		if (!files.Any()) {
			return;
		}

		if (!IsInputReady()) {
			var ff = files[0];
			InputText = ff;

			if (files.Length > 1) {
				files = files[1..];

			}

		}

		foreach (var s in files) {

			if (!Queue.Contains(s)) {
				Queue.Add(s);
			}
		}
	}

	private static string[] GetFilesFromDrop(DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.FileDrop)) {

			if (e.Data.GetData(DataFormats.FileDrop, true) is string[] files
			    && files.Any()) {

				return files;

			}
		}

		return Array.Empty<string>();
	}

	private void Restart()
	{
		Clear();
		Dispose(false);
		m_cts = new();
		m_clipboard.Clear();
	}

	private void Clear()
	{
		Results.Clear();
		// Btn_Run.IsEnabled = false;
		// InputText         = string.Empty;
		// Query.Dispose();
		Pb_Status.Value = 0;
	}

	private void Next()
	{
		Restart();

		if (!Queue.Any()) {
			return;
		}

		var next = Queue[m_queuePos++ % Queue.Count];
		InputText = next;
		Lv_Queue.SelectedItems.Clear();
		Lv_Queue.SelectedItems.Add(next);

		if (m_queuePos < Queue.Count && m_queuePos >= 0) {
			// await SetQueryAsync(next);
		}
	}

	public void Dispose()
	{
		Dispose(true);
	}

	public void Dispose(bool full)
	{
		if (full) {
			Client.Dispose();
			Query.Dispose();

			Queue.Clear();
			m_queuePos = 0;

			foreach (var kv in m_queries) {
				kv.Value.Dispose();
			}

			m_queries.Clear();

			foreach (var kv in m_uni) {
				kv.Key.Dispose();
			}

			m_uni.Clear();

		}

		foreach (var r1 in Results) {
			r1.Dispose();
		}

		Results.Clear();
		m_cts.Dispose();
	}

	#endregion

	#region

	#region

	private async void Tb_Input_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (Interlocked.CompareExchange(ref Status, S_OK, S_NO) == S_NO) {
			return;
		}

		var txt = InputText;
		var ok  = SearchQuery.IsValidSourceType(txt);

		if (ok /*&& !IsInputReady()*/) {
			await SetQueryAsync(txt);
		}

		Btn_Run.IsEnabled = ok;
	}

	private void Tb_Input_TextInput(object sender, TextCompositionEventArgs e) { }

	private void Tb_Input_DragOver(object sender, DragEventArgs e)
	{
		e.Handled = true;
	}

	private void Tb_Input_PreviewDragOver(object sender, DragEventArgs e)
	{
		e.Handled = true;
	}

	private void Tb_Input_Drop(object sender, DragEventArgs e)
	{
		var files1 = GetFilesFromDrop(e);

		EnqueueAsync(files1);
		var files = files1;
		var f1    = files.FirstOrDefault();
		InputText = f1;
		e.Handled = true;

	}

	private void Tb_Input2_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		HttpUtilities.TryOpenUrl(Query.Upload);
	}

	#endregion

	#region

	private void Lv_Queue_Drop(object sender, DragEventArgs e)
	{
		var files = GetFilesFromDrop(e);

		EnqueueAsync(files);
		string[] temp = files;
		e.Handled = true;
	}

	private void Lv_Queue_DragOver(object sender, DragEventArgs e)
	{
		e.Handled = true;
	}

	private void Lv_Queue_PreviewDragOver(object sender, DragEventArgs e)
	{
		e.Handled = true;
	}

	private async void Lv_Queue_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count > 0) {
			var i = e.AddedItems[0] as string;
			InputText = i;

		}
	}

	#endregion

	private void Lb_Engines_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		Lb_Engines.HandleEnumOption(e, (ai, ri) =>
		{
			Config.SearchEngines |= (ai);
			Config.SearchEngines &= ~ri;
		});
	}

	private void Lb_Engines2_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		Lb_Engines2.HandleEnumOption(e, (ai, ri) =>
		{
			Config.PriorityEngines |= (ai);
			Config.PriorityEngines &= ~ri;
		});
	}

	private void Btn_Run_Click(object sender, RoutedEventArgs e)
	{
		// await SetQueryAsync(InputText);
		Btn_Run.IsEnabled = false;

		Dispatcher.InvokeAsync(RunAsync);
	}

	private async void Btn_Clear_Click(object sender, RoutedEventArgs e)
	{
		Clear();
	}

	private void Btn_Restart_Click(object sender, RoutedEventArgs e)
	{
		Restart();
		Queue.Clear();
	}

	private async void Btn_Next_Click(object sender, RoutedEventArgs e)
	{
		Next();
	}

	private void Btn_Cancel_Click(object sender, RoutedEventArgs e)
	{
		m_cts.Cancel();
	}

	private void Btn_Run_Loaded(object sender, RoutedEventArgs e)
	{
		// Btn_Run.IsEnabled = false;
	}

	#region

	private void Lv_Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (Lv_Results.SelectedItem is ResultItem si) {
			HttpUtilities.TryOpenUrl(si.Result.Url);
		}
	}

	private void Lv_Results_MouseRightButtonDown(object sender, MouseButtonEventArgs e) { }

	private void Lv_Results_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

	private void Lv_Results_KeyDown(object sender, KeyEventArgs e)
	{

		var ctrl  = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
		var alt   = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
		var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

		var key = e.Key;

		switch (key) {
			case Key.D when ctrl:
				Application.Current.Dispatcher.InvokeAsync(async () =>
				{
					var    ri = ((ResultItem) Lv_Results.SelectedItem);
					var    u  = ri.Uni;
					var    v  = (Url) u.Value.ToString();
					string path;

					if (v.PathSegments is { Count: >= 1 }) {
						path = $"{v.PathSegments[^1]}";

					}
					else path = v.Path;

					path = HttpUtility.HtmlDecode(path);
					path = FileSystem.SanitizeFilename(path);
					var path2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), path);

					var f = File.OpenWrite(path2);

					if (u.Stream.CanSeek) {
						u.Stream.Position = 0;

					}

					await u.Stream.CopyToAsync(f);
					FileSystem.ExploreFile(path2);
					f.Dispose();
					// u.Dispose();
				});

				break;
			case Key.S when ctrl:

				Application.Current.Dispatcher.InvokeAsync(async () =>
				{
					var ri = ((ResultItem) Lv_Results.SelectedItem);

					if (m_uni.ContainsKey(ri)) {
						return;
					}

					Pb_Status.IsIndeterminate = true;
					var d = await ri.Result.LoadUniAsync();

					if (d) {
						Debug.WriteLine($"{ri}");
						var resultUni = ri.Result.Uni;
						m_uni.TryAdd(ri, resultUni);
						var resultItems = new ResultItem[resultUni.Length];

						for (int i = 0; i < resultUni.Length; i++) {
							var rii = new ResultItem(ri.Result, $"{ri.Name} {i} 🖼", i);
							resultItems[i] = rii;
							Results.Insert(Results.IndexOf(ri) + 1 + i, rii);
						}
					}
					Pb_Status.IsIndeterminate = false;

				});

				break;
			default:
				break;
		}
	}

	#endregion

	#region

	private void Cb_Clipboard_Checked(object sender, RoutedEventArgs e)
	{
		// Config.Clipboard = !Config.Clipboard;

	}

	private void Cb_AutoSearch_Checked(object sender, RoutedEventArgs e)
	{
		// Config.AutoSearch = !Config.AutoSearch;
	}

	private void Cb_OpenRaw_Checked(object sender, RoutedEventArgs e)
	{
		// Config.OpenRaw = !Config.OpenRaw;
	}

	private void Cb_ContextMenu_Checked(object sender, RoutedEventArgs e)
	{
		if (!((FrameworkElement) e.Source).IsLoaded) {
			return;
		}

		AppUtil.HandleContextMenu(!AppUtil.IsContextMenuAdded);

	}

	#endregion

	#region

	private void Wnd_Main_Loaded(object sender, RoutedEventArgs e)
	{
		m_cbDispatch.Start();

	}

	private void Wnd_Main_Closed(object sender, EventArgs e) { }

	private void Wnd_Main_Closing(object sender, CancelEventArgs e) { }

	#endregion

	#endregion
}