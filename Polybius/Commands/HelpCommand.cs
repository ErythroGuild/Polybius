using System;
using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

namespace Polybius.Commands {
	using CommandFunc = Action<string, DiscordMessage>;
	using HelpTable = Dictionary<Action<string, DiscordMessage>, Func<DiscordMessage, string>>;

	class HelpCommand {
		private static readonly HelpTable dict_help = new () {
			{ HelpCommand.main, help_general },
		};

		// The general handler function called from the main program.
		public static void main(string arg, DiscordMessage msg) {
			// Parse the intended argument with the same table used
			// to parse the commands themselves.
			// Default to "general" help text.
			CommandFunc func = main;
			if (Program.command_list.ContainsKey(arg)) {
				func = Program.command_list[arg];
			}

			// Fetch the associated help text and send as a reply.
			string response = dict_help[func](msg);
			_ = msg.RespondAsync(response);
		}

		private static string help_general(DiscordMessage msg) {
			StringWriter text = new ();
			string tL, tR, tS;

			// Fetch customized tokens from guild settings.
			ulong? id = msg.Channel.GuildId;
			if ((id is not null) && Program.settings.ContainsKey((ulong)id)) {
				Settings settings = Program.settings[(ulong)id];
				tL = settings.token_L;
				tR = settings.token_R;
				tS = settings.split;
			} else {
				tL = Settings.token_L_default;
				tR = Settings.token_R_default;
				tS = Settings.split_default;
			}

			text.WriteLine($"Surround anything you want to search for in your message with `{tL}` and `{tR}`.");
			text.WriteLine($"> I would have done better if you had given me `{tL}innervate{tR}`.");
			//text.WriteLine();
			//text.WriteLine($"You can also add `{tS}` to use a specific search engine or specify which results you want.");
			//text.WriteLine($"> Check out the new `{tL}Dreamrunner{tS}pet{tR}` model they added!");
			text.WriteLine();
			text.WriteLine("Use the command name to get more help on commands, e.g.:");
			text.WriteLine("`@Polybius -help view-tokens`");

			text.Flush();
			return text.ToString();
		}
	}
}
