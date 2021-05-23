using System;
using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

namespace Polybius.Commands {
	using CommandFunc = Action<string, DiscordMessage>;
	using HelpTable = Dictionary<Action<string, DiscordMessage>, Func<DiscordMessage, string>>;

	class HelpCommand {
		private static readonly string m = $"@{Program.polybius.CurrentUser.Username}";

		private static readonly HelpTable dict_help = new () {
			{ HelpCommand.main, help_general },
			{ ServerCommands.blacklist        , help_filterlist  },
			{ ServerCommands.whitelist        , help_filterlist  },
			{ ServerCommands.bot_channel      , help_botchannel  },
			{ ServerCommands.bot_channel_clear, help_botchannel  },
			{ ServerCommands.view_filters     , help_viewfilters },
			{ ServerCommands.set_token_L      , help_settoken    },
			{ ServerCommands.set_token_R      , help_settoken    },
			{ ServerCommands.set_split        , help_settoken    },
			{ ServerCommands.view_tokens      , help_viewtokens  },
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

		// The general help command, also given when an invalid argument
		// is given. The only discoverable entry point to the bot.
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

			text.WriteLine($"Surround anything you want to search for in your message with `{tL}` and `{tR}`. E.g.:");
			text.WriteLine($"> I would have done better if you had given me `{tL}innervate{tR}`.");
			//text.WriteLine();
			//text.WriteLine($"You can also add `{tS}` to use a specific search engine or specify which results you want.");
			//text.WriteLine($"> Check out the new `{tL}Dreamrunner{tS}pet{tR}` model they added!");
			text.WriteLine();
			text.WriteLine("Use the command name to get more help on commands, e.g.:");
			text.WriteLine($"> `{m} -help view-tokens`");

			text.Flush();
			return text.ToString();
		}

		// The help command for configuring the blacklist & whitelist.
		private static string help_filterlist(DiscordMessage msg) {
			StringWriter text = new ();

			text.WriteLine($"Use `{m} -blacklist` & `{m} -whitelist` to set up channel filters.");
			text.WriteLine("Entering a channel already on a list will remove it from that list.");
			text.WriteLine("You can specify channels with channel IDs / mention strings / channel names.");
			text.WriteLine();
			text.WriteLine("Some examples:");
			text.WriteLine($"> `{m} -whitelist 834981452453249054`");
			text.WriteLine($"> `{m} -blacklist #announcements`");
			text.WriteLine($"> `{m} -whitelist bot-spam`");
			text.WriteLine();
			text.WriteLine("Polybius will not respond to searches from channels on the blacklist.");
			text.WriteLine("If a whitelist exists, Polybius will only respond to searches from those channels.");
			text.WriteLine("If a whitelist does not exist, Polybius will respond to searches from all channels, *except* blacklisted ones.");
			text.WriteLine();
			text.WriteLine($"Use `{m} -view-filters` to view the server's whitelist and blacklist.");
			text.WriteLine($"Also see: `{m} -help bot-channel`.");

			text.Flush();
			return text.ToString();
		}

		// The help command for configuring a bot channel for the server.
		private static string help_botchannel(DiscordMessage msg) {
			StringWriter text = new ();

			text.WriteLine($"Use `{m} -bot-channel` to set a bot channel where Polybius will respond to searches.");
			text.WriteLine($"`{m} -clear-bot-channel` will let Polybius respond anywhere.");
			text.WriteLine();
			text.WriteLine("You can specify channels with channel IDs / mention strings / channel names.");
			text.WriteLine($"> `{m} -bot-channel 834981452453249054`");
			text.WriteLine($"> `{m} -bot-channel #bots`");
			text.WriteLine($"> `{m} -bot-channel bot-spam`");
			text.WriteLine();
			text.WriteLine($"Use `{m} -view-filters` to view the current bot channel.");
			text.WriteLine($"Also see: `{m} -help blacklist` / `{m} -help whitelist`.");

			text.Flush();
			return text.ToString();
		}

		// The help command for viewing the blacklist & whitelist &
		// bot channel.
		private static string help_viewfilters(DiscordMessage msg) {
			StringWriter text = new ();

			text.WriteLine($"Use `{m} -view-filters` to view the current blacklist / whitelist / bot channel.");
			text.WriteLine($"Also see: `{m} -help blacklist`, `{m} -help whitelist`, and `{m} -help bot-channel`.");

			text.Flush();
			return text.ToString();
		}

		// The help command for modifying the search token format.
		private static string help_settoken(DiscordMessage msg) {
			StringWriter text = new ();
			string tL = Settings.token_L_default;
			string tR = Settings.token_R_default;
			string tS = Settings.split_default;

			text.WriteLine($"Use `{m} -set-token-L`, `{m} -set-token-R`, and `{m} -set-split` to change the search format.");
			text.WriteLine($"You can use any (non-empty) string. The default settings are `{tL}query{tS}meta{tR}`.");
			text.WriteLine();
			text.WriteLine("Some examples:");
			text.WriteLine($"> `{m} -set-token-L <<!`");
			text.WriteLine($"> `{m} -set-token-R {tR}`");
			text.WriteLine($"> `{m} -set-split &&`");
			text.WriteLine();
			text.WriteLine($"Use `{m} -view-tokens` to check the current search format.");

			text.Flush();
			return text.ToString();
		}

		// The help command for checking the current format for
		// search tokens.
		private static string help_viewtokens(DiscordMessage msg) {
			StringWriter text = new ();
			string tL = Settings.token_L_default;
			string tR = Settings.token_R_default;
			string tS = Settings.split_default;

			text.WriteLine($"Use `{m} -view-tokens` to check the current search format.");
			text.WriteLine($"The default settings are `{tL}query{tS}meta{tR}`.");
			text.WriteLine();
			text.WriteLine($"Use `{m} -set-token-L`, `{m} -set-token-R`, and `{m} -set-split` to change the current search format.");

			text.Flush();
			return text.ToString();
		}
	}
}
