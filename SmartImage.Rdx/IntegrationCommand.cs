﻿// Author: Deci | Project: SmartImage.Rdx | Name: IntegrationCommand.cs
// Date: 2024/05/22 @ 16:05:51

using SmartImage.Lib.Utilities;
using Spectre.Console.Cli;

namespace SmartImage.Rdx;

public class IntegrationCommand : Command<IntegrationCommandSettings>
{

	public override int Execute(CommandContext context, IntegrationCommandSettings settings)
	{
		try {
			// AConsole.WriteLine($"{AppUtil.IsContextMenuAdded}");

			if (settings.ContextMenu.HasValue) {
				var rv = AppUtil.HandleContextMenu(settings.ContextMenu.Value);
				AConsole.WriteLine($"Context menu change: {rv}");
			}

			AConsole.WriteLine($"Context menu enabled: {AppUtil.IsContextMenuAdded}");
		}
		catch (Exception e) {
			AConsole.WriteException(e);
		}

		return SearchCommand.EC_OK;
	}

}