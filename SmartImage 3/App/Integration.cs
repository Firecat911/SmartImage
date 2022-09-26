﻿using Kantan.Cli;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Novus.OS;
using Kantan.Diagnostics;

#nullable disable

namespace SmartImage.App;

/// <summary>
/// Program OS integrations
/// </summary>
public static class Integration
{
	/*
	 * HKEY_CLASSES_ROOT is an alias, a merging, of two other locations:
	 *		HKEY_CURRENT_USER\Software\Classes
	 *		HKEY_LOCAL_MACHINE\Software\Classes
	 */

	/// <returns><c>true</c> if operation succeeded; <c>false</c> otherwise</returns>
	public static bool HandleContextMenu(bool option)
	{
		/*
		 * New context menu
		 */
		if (OperatingSystem.IsWindows()) {
			switch (option) {
				case true:

					RegistryKey regMenu = null;
					RegistryKey regCmd  = null;

					string fullPath = ExeLocation;

					try {
						regMenu = Registry.CurrentUser.CreateSubKey(REG_SHELL);
						regMenu?.SetValue(String.Empty, Resources.Name);
						regMenu?.SetValue("Icon", $"\"{fullPath}\"");

						regCmd = Registry.CurrentUser.CreateSubKey(REG_SHELL_CMD);
						regCmd?.SetValue(String.Empty, $"\"{fullPath}\" -q \"%1\"");
					}
					catch (Exception ex) {
						Trace.WriteLine($"{ex.Message}");
						ConsoleManager.WaitForInput();
						return false;
					}
					finally {
						regMenu?.Close();
						regCmd?.Close();
					}

					break;
				case false:

					try {
						var reg = Registry.CurrentUser.OpenSubKey(REG_SHELL_CMD);

						if (reg != null) {
							reg.Close();
							Registry.CurrentUser.DeleteSubKey(REG_SHELL_CMD);
						}

						reg = Registry.CurrentUser.OpenSubKey(REG_SHELL);

						if (reg != null) {
							reg.Close();
							Registry.CurrentUser.DeleteSubKey(REG_SHELL);
						}
					}
					catch (Exception ex) {
						Trace.WriteLine($"{ex.Message}", C_ERROR);

						return false;
					}

					break;

			}

		}

		return false;

	}

	public static string ExeLocation
	{
		get
		{
			var module = Process.GetCurrentProcess().MainModule;

			// Require.NotNull(module);
			Trace.Assert(module != null);

			return module.FileName;
		}
	}

	public static string CurrentAppFolder  => Path.GetDirectoryName(ExeLocation);
	public static bool   IsAppFolderInPath => FileSystem.IsFolderInPath(CurrentAppFolder);

	public static void HandlePath(bool option)
	{
		switch (option) {
			case true:
			{
				string oldValue  = FileSystem.GetEnvironmentPath();
				string appFolder = CurrentAppFolder;

				if (IsAppFolderInPath) {
					return;
				}

				bool appFolderInPath = oldValue
				                       .Split(FileSystem.PATH_DELIM)
				                       .Any(p => p == appFolder);

				string cd  = Environment.CurrentDirectory;
				string exe = Path.Combine(cd, Resources.NameExe);

				if (!appFolderInPath) {
					string newValue = oldValue + FileSystem.PATH_DELIM + cd;
					FileSystem.SetEnvironmentPath(newValue);
				}

				break;
			}
			case false:
				FileSystem.RemoveFromPath(CurrentAppFolder);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(option), option, null);
		}
	}

	public static void ResetIntegrations()
	{
		// Computer\HKEY_CLASSES_ROOT\*\shell\SmartImage

		if (IsContextMenuAdded) {
			if (OperatingSystem.IsWindows()) {
				HandleContextMenu(false);

			}
		}

		Trace.WriteLine("Reset config");
	}

	[DoesNotReturn]
	public static void Uninstall()
	{
		// autonomous uninstall routine

		// self destruct

		string exeFileName = ExeLocation;

		const string DEL_BAT_NAME = "SmartImage_Delete.bat";

		string[] commands =
		{
			"@echo off",

			/* Wait approximately 4 seconds (so that the process is already terminated) */
			"ping 127.0.0.1 > nul",

			/* Delete executable */
			$"echo y | del /F {exeFileName}",

			/* Delete this bat file */
			$"echo y | del {DEL_BAT_NAME}"
		};

		// Runs in background
		var proc = Command.Batch(commands, DEL_BAT_NAME);
		proc.Start();

	}

	private const string REG_SHELL = "SOFTWARE\\Classes\\*\\shell\\SmartImage";

	private const string REG_SHELL_CMD = "SOFTWARE\\Classes\\*\\shell\\SmartImage\\command";

	public static bool IsContextMenuAdded
	{
		get
		{

			if (OperatingSystem.IsWindows()) {
				var reg = Registry.CurrentUser.OpenSubKey(REG_SHELL_CMD);
				return reg != null;

			}

			return false;
		}
	}
}