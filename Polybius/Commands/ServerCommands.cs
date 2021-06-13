using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;

namespace Polybius.Commands {
	class ServerCommands {
		// Convenience functions for text printing.
		static StringWriter text = new ();
		static void text_clr() {
			text = new StringWriter();
		}
		static string text_out() {
			text.Flush();
			return text.ToString();
		}

		// Shorthand interpolation strings.
		const string
			c = ":white_check_mark:",
			i = ":information_source:",
			w = ":warning:";

		// Toggle a channel from the whitelist.
		public static void whitelist(string arg, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Parse the command into a channel and validate it.
			ulong? ch_arg_n = extract_channel_id(arg, msg);
			do_continue = validate_channel(ch_arg_n, msg);
			if (!do_continue)
				{ return; }
			ulong ch_arg = (ulong)ch_arg_n!;	// guaranteed by validate

			// Save previous settings to compare with.
			HashSet<ulong> whitelist =
				Program.settings[guild_id].ch_whitelist;
			string m =
				msg.Channel.Guild.GetChannel(ch_arg).Mention;

			// Update settings.
			if (whitelist.Contains(ch_arg)) {
				Program.settings[guild_id].ch_whitelist.Remove(ch_arg);
				text_clr();
				text.WriteLine($"The whitelist already contains {m}.");
				text.WriteLine($"{c} {m} has been removed from the whitelist.");
				_ = msg.RespondAsync(text_out());
				Program.log.info("  Removed channel from whitelist.");
			} else {
				Program.settings[guild_id].ch_whitelist.Add(ch_arg);
				Program.log.info("  Added channel to whitelist.");
				text_clr();
				text.WriteLine($"{c} {m} has been added to the whitelist.");
				if (Program.settings[guild_id].ch_blacklist.Contains(ch_arg)) {
					Program.settings[guild_id].ch_blacklist.Remove(ch_arg);
					Program.log.debug("  Removed channel from blacklist.");
					text.WriteLine($"{c} {m} has been removed from the blacklist.");
				}
				_ = msg.RespondAsync(text_out());
			}
			Program.settings[guild_id].save();
			Program.log.debug("  Saved settings.");
		}

		// Toggle a channel from the blacklist.
		public static void blacklist(string arg, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Parse the command into a channel and validate it.
			ulong? ch_arg_n = extract_channel_id(arg, msg);
			do_continue = validate_channel(ch_arg_n, msg);
			if (!do_continue)
				{ return; }
			ulong ch_arg = (ulong)ch_arg_n!;    // guaranteed by validate

			// Save previous settings to compare with.
			HashSet<ulong> blacklist =
				Program.settings[guild_id].ch_blacklist;
			string m =
				msg.Channel.Guild.GetChannel(ch_arg).Mention;

			// Update settings.
			if (blacklist.Contains(ch_arg)) {
				Program.settings[guild_id].ch_blacklist.Remove(ch_arg);
				text_clr();
				text.WriteLine($"The blacklist already contains {m}.");
				text.WriteLine($"{c} {m} has been removed from the blacklist.");
				_ = msg.RespondAsync(text_out());
				Program.log.info("  Removed channel from blacklist.");
			} else {
				Program.settings[guild_id].ch_blacklist.Add(ch_arg);
				Program.log.info("  Added channel to blacklist.");
				text_clr();
				text.WriteLine($"{c} {m} has been added to the blacklist.");
				if (Program.settings[guild_id].ch_whitelist.Contains(ch_arg)) {
					Program.settings[guild_id].ch_whitelist.Remove(ch_arg);
					Program.log.debug("  Removed channel from whitelist.");
					text.WriteLine($"{c} {m} has been removed from the whitelist.");
				}
				_ = msg.RespondAsync(text_out());
			}
			Program.settings[guild_id].save();
			Program.log.debug("  Saved settings.");
		}

		// Designate a bot channel to send all messages to.
		public static void bot_channel(string arg, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Parse the command into a channel and validate it.
			ulong? ch_bot_n = extract_channel_id(arg, msg);
			do_continue = validate_channel(ch_bot_n, msg);
			if (!do_continue)
				{ return; }
			ulong ch_bot = (ulong)ch_bot_n!;	// guaranteed by validate

			// Save previous settings to compare with.
			ulong? ch_bot_old =
				Program.settings[guild_id].ch_bot;
			string m =
				msg.Channel.Guild.GetChannel(ch_bot).Mention;

			// Update settings.
			if (ch_bot == ch_bot_old) {
				text_clr();
				text.WriteLine($"{c} Bot channel is already {m}.");
				text.WriteLine("No changes have been made.");
				_ = msg.RespondAsync(text_out());
				Program.log.info("  Bot channel unchanged.");
			} else {
				Program.settings[guild_id].ch_bot = ch_bot;
				Program.log.info("  Changed bot channel.");
				text_clr();
				text.WriteLine($"{c} Bot channel has been set to {m}.");
				_ = msg.RespondAsync(text_out());
			}
		}

		// Reset the bot channel (to `null`).
		public static void bot_channel_clear(string _1, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Save previous settings to compare with.
			ulong? ch_bot_old = Program.settings[guild_id].ch_bot;

			// Update settings.
			if (ch_bot_old is not null) {
				Program.settings[guild_id].ch_bot = null;
				string m =
					msg.Channel.Guild.GetChannel((ulong)ch_bot_old).Mention;
				text_clr();
				text.WriteLine($"{c} Bot channel {m} has been cleared.");
				_ = msg.RespondAsync(text_out());
				Program.log.info("  Bot channel cleared (set to null).");
			} else {
				text_clr();
				text.WriteLine($"{c} No bot channel exists yet.");
				text.WriteLine("No changes have been made.");
				_ = msg.RespondAsync(text_out());
				Program.log.info("  No bot channel set. No changes made.");
			}
		}

		// View all of the current channel filters.
		public static void view_filters(string _1, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Cache settings data.
			Settings settings = Program.settings[guild_id];
			text_clr();

			// Display bot channel info.
			if (settings.ch_bot is null) {
				text.WriteLine($"{i} No bot channel has been set.");
			} else {
				ulong ch_bot = (ulong)settings.ch_bot;
				string m = msg.Channel.Guild.GetChannel(ch_bot).Mention;
				text.WriteLine($"{i} Bot channel: {m}");
			}

			// Display whitelist info.
			if (settings.ch_whitelist.Count == 0) {
				text.WriteLine($"{i} No channels have been whitelisted.");
			} else {
				text.Write($"{i} Whitelist:");
				foreach (ulong ch in settings.ch_whitelist) {
					string m = msg.Channel.Guild.GetChannel(ch).Mention;
					text.Write($" {m}");
				}
				text.Write("\n");
			}

			// Display blacklist info.
			if (settings.ch_blacklist.Count == 0) {
				text.WriteLine($"{i} No channels have been blacklisted.");
			} else {
				text.Write($"{i} Blacklist:");
				foreach (ulong ch in settings.ch_blacklist) {
					string m = msg.Channel.Guild.GetChannel(ch).Mention;
					text.Write($" {m}");
				}
				text.Write("\n");
			}

			_ = msg.RespondAsync(text_out());
			Program.log.info("  Filters printed.");
		}

		// Configure the left-hand token.
		public static void set_token_L(string arg, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Prevent empty strings being set.
			do_continue = validate_token(arg, msg);
			if (!do_continue)
				{ return; }

			// Write token value to settings.
			string t = Program.settings[guild_id].token_L;
			Program.settings[guild_id].token_L = arg;
			text_clr();
			text.WriteLine($"{c} Left-hand token changed from `{t}` to `{arg}`.");
			_ = msg.RespondAsync(text_out());
			Program.log.info($"  Left-hand token changed to {arg}.");
		}

		// Configure the right-hand token.
		public static void set_token_R(string arg, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Prevent empty strings being set.
			do_continue = validate_token(arg, msg);
			if (!do_continue)
				{ return; }

			// Write token value to settings.
			string t = Program.settings[guild_id].token_R;
			Program.settings[guild_id].token_R = arg;
			text_clr();
			text.WriteLine($"{c} Right-hand token changed from `{t}` to `{arg}`.");
			_ = msg.RespondAsync(text_out());
			Program.log.info($"  Right-hand token changed to {arg}.");
		}

		// Configure the splitter token.
		public static void set_split(string arg, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Prevent empty strings being set.
			do_continue = validate_token(arg, msg);
			if (!do_continue)
				{ return; }

			// Write token value to settings.
			string t = Program.settings[guild_id].split;
			Program.settings[guild_id].split = arg;
			text_clr();
			text.WriteLine($"{c} Splitter token changed from `{t}` to `{arg}`.");
			_ = msg.RespondAsync(text_out());
			Program.log.info($"  Splitter token changed to {arg}.");
		}

		// Display the accepted search token format.
		public static void view_tokens(string _1, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId!;
			try_init_settings(guild_id);

			// Cache settings data.
			Settings s = Program.settings[guild_id];

			// Display token format.
			text_clr();
			text.WriteLine($"{i} Search token format:");
			text.WriteLine($"`{s.token_L}`query`{s.split}`meta`{s.token_R}`");
			_ = msg.RespondAsync(text_out());
			Program.log.info($"  `{s.token_L}`query`{s.split}`meta`{s.token_R}`");
		}

		// Reset server settings back to default values.
		public static void reset_server_settings(string _1, DiscordMessage msg) {
			// Validate message and set up preconditions.
			bool do_continue = validate_guild(msg);
			if (!do_continue)
				{ return; }
			
			// (Re-)initialize server settings.
			_ = new Settings((ulong)msg.Channel.GuildId!);
			text_clr();
			text.WriteLine($"{c} All server settings have been reset to their defaults.");
			text.WriteLine($"{i} Server statistics have not been reset.");
			msg.RespondAsync(text_out());
			Program.log.info("  Server settings have been reset.");
		}

		// Print server-specific stats.
		public static void stats(string arg, DiscordMessage msg) {
		}

		// Displays build information.
		public static void version(string _1, DiscordMessage msg) {
			StreamReader file;

			// read in build data
			file = File.OpenText(Program.path_build);
			string build = file.ReadLine() ?? "";
			if (build.Length > 7) {
				build = build[..7];
			}
			file.Close();

			file = File.OpenText(Program.path_version);
			string version = file.ReadLine() ?? "";
			file.Close();

			// display data
			text_clr();
			text.WriteLine($"{i} **Polybius {version}** build `{build}`");
			msg.RespondAsync(text_out());
			Program.log.debug($"  Polybius {version}, build {build}");
		}

		// If "false" is returned, the guild associated with the message
		// isn't usable and the caller should return immediately.
		static bool validate_guild(DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists) {
				Program.log.warning("  Message from invalid guild.");
				text_clr();
				text.Write("Could not find a Discord server to configure ");
				text.Write(":zipper_mouth:");
				_ = msg.RespondAsync(text_out());
				return false;
			} else {
				return true;
			}
		}

		// If "false" is returned, the channel that was entered isn't
		// parseable and the caller should return immediately.
		static bool validate_channel(ulong? id, DiscordMessage msg) {
			if (id is null) {
				text_clr();
				text.WriteLine($"{w} Could not parse channel name.");
				text.WriteLine($"No changes have been made.");
				Program.log.info("  Could not parse channel name.");
				_ = msg.RespondAsync(text_out());
				return false;
			} else {
				return true;
			}
		}

		// If "false" is returned, the token was empty and the caller
		// should return immediately.
		static bool validate_token(string token, DiscordMessage msg) {
			if (token == "") {
				text_clr();
				text.WriteLine($"{w} Tokens cannot be set to empty strings.");
				text.WriteLine("No settings have been changed.");
				_ = msg.RespondAsync(text_out());
				Program.log.info("  Cannot set token to empty string.");
				return false;
			} else {
				return true;
			}
		}

		// If `msg.Channel.Guild` is `null`, return false, and reply to
		// the original message explaining.
		static bool check_guild_exists(DiscordMessage msg) {
			return (msg.Channel.Guild is not null);
		}

		// Creates a new default Settings file for the given server if
		// one doesn't exist already.
		// Returns true when a new file was created, false otherwise.
		static bool try_init_settings(ulong guild_id) {
			if (Program.settings[guild_id] is null) {
				Program.settings.Add(guild_id, new (guild_id));
				Program.log.debug("    Initialized guild settings to default.");
				return true;
			} else {
				return false;
			}
		}

		// Try to convert a text string to a single id.
		// Returns null if unsuccessful.
		static ulong? extract_channel_id(string text, DiscordMessage msg) {
			if (msg.MentionedChannels.Count > 0) {
				// Check mentions.
				return msg.MentionedChannels[0].Id;
			} else if (Regex.IsMatch(text, @"\d+")) {
				// Check for IDs.
				return Convert.ToUInt64(text);
			} else {
				// Check against plaintext names.
				text = text.ToLower();
				IEnumerable<DiscordChannel> channels =
					msg.Channel.Guild.Channels.Values;
				foreach (DiscordChannel channel in channels) {
					if (channel.Name == text)
						return channel.Id;
				}
			}
			return null;
		}
	}
}
