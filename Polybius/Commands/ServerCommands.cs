using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;

namespace Polybius.Commands {
	class ServerCommands {
		public static void whitelist(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			ulong? ch_to_add = extract_channel_id(arg, msg);
			if (ch_to_add is null) {
				_ = msg.RespondAsync(":warning: Could not parse channel name.\nNo changes have been made.");
				return;
			}
			HashSet<ulong> whitelist = Program.settings[guild_id].ch_whitelist;
			string mention =
				msg.Channel.Guild.GetChannel((ulong)ch_to_add).Mention;

			if (whitelist.Contains((ulong)ch_to_add)) {
				Program.settings[guild_id].ch_whitelist.Remove((ulong)ch_to_add);
				_ = msg.RespondAsync($"The whitelist already contains {mention}.\n:white_check_mark: {mention} has been removed from the whitelist.");
			} else {
				Program.settings[guild_id].ch_whitelist.Add((ulong)ch_to_add);
				string response = $":white_check_mark: {mention} has been added to the whitelist.";
				if (Program.settings[guild_id].ch_blacklist.Contains((ulong)ch_to_add)) {
					Program.settings[guild_id].ch_blacklist.Remove((ulong)ch_to_add);
					response += $"\n:white_check_mark: {mention} has been removed from the blacklist.";
				}
				_ = msg.RespondAsync(response);
			}
			Program.settings[guild_id].save();
		}

		public static void blacklist(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			ulong? ch_to_add = extract_channel_id(arg, msg);
			if (ch_to_add is null) {
				_ = msg.RespondAsync(":warning: Could not parse channel name.\nNo changes have been made.");
				return;
			}
			HashSet<ulong> blacklist = Program.settings[guild_id].ch_blacklist;
			string mention =
				msg.Channel.Guild.GetChannel((ulong)ch_to_add).Mention;

			if (blacklist.Contains((ulong)ch_to_add)) {
				Program.settings[guild_id].ch_blacklist.Remove((ulong)ch_to_add);
				_ = msg.RespondAsync($"The blacklist already contains {mention}.\n:white_check_mark: {mention} has been removed from the blacklist.");
			} else {
				Program.settings[guild_id].ch_blacklist.Add((ulong)ch_to_add);
				string response = $":white_check_mark: {mention} has been added to the blacklist.";
				if (Program.settings[guild_id].ch_whitelist.Contains((ulong)ch_to_add)) {
					Program.settings[guild_id].ch_whitelist.Remove((ulong)ch_to_add);
					response += $"\n:white_check_mark: {mention} has been removed from the whitelist.";
				}
				_ = msg.RespondAsync(response);
			}
			Program.settings[guild_id].save();
		}

		public static void bot_channel(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			ulong? ch_bot = extract_channel_id(arg, msg);
			if (ch_bot is null) {
				_ = msg.RespondAsync(":warning: Could not parse channel name.\nNo changes have been made.");
				return;
			}
			ulong? ch_bot_old = Program.settings[guild_id].ch_bot;
			string mention =
				msg.Channel.Guild.GetChannel((ulong)ch_bot).Mention;

			if (ch_bot == ch_bot_old) {
				_ = msg.RespondAsync($":white_check_mark: Bot channel is already {mention}.\nNo changes have been made.");
			} else {
				Program.settings[guild_id].ch_bot = ch_bot;
				_ = msg.RespondAsync($":white_check_mark: Bot channel has been set to {mention}.");
			}
		}

		public static void bot_channel_clear(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			ulong? ch_bot_old = Program.settings[guild_id].ch_bot;

			if (ch_bot_old is not null) {
				Program.settings[guild_id].ch_bot = null;
				string mention =
					msg.Channel.Guild.GetChannel((ulong)ch_bot_old).Mention;
				_ = msg.RespondAsync($":white_check_mark: Bot channel {mention} has been cleared.");
			} else {
				_ = msg.RespondAsync(":white_check_mark: No bot channel exists yet.\nNo changes have been made.");
			}
		}

		public static void view_filters(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);
			Settings settings = Program.settings[guild_id];

			string response = "";
			if (settings.ch_bot is null) {
				response += ":information_source: No bot channel has been set.";
			} else {
				ulong ch_bot = (ulong)settings.ch_bot;
				string mention = msg.Channel.Guild.GetChannel(ch_bot).Mention;
				response += $":information_source: Bot channel: {mention}.";
			}

			if (settings.ch_whitelist.Count == 0) {
				response += "\n:information_source: No channels have been whitelisted.";
			} else {
				response += "\n:information_source: Whitelist:";
				foreach (ulong ch in settings.ch_whitelist) {
					string mention = msg.Channel.Guild.GetChannel(ch).Mention;
					response += $" {mention},";
				}
				response = response[..^1];
			}

			if (settings.ch_blacklist.Count == 0) {
				response += "\n:information_source: No channels have been blacklisted.";
			} else {
				response += "\n:information_source: Blacklist:";
				foreach (ulong ch in settings.ch_blacklist) {
					string mention = msg.Channel.Guild.GetChannel(ch).Mention;
					response += $" {mention},";
				}
				response = response[..^1];
			}
			_ = msg.RespondAsync(response);
		}

		public static void set_token_L(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			if (arg == "") {
				_ = msg.RespondAsync(":warning: Tokens cannot be set to empty strings.\nNo settings have been changed.");
				return;
			}

			string token_old = Program.settings[guild_id].token_L;
			Program.settings[guild_id].token_L = arg;
			_ = msg.RespondAsync($":white_check_mark: Left-hand token changed from `{token_old}` to `{arg}`.");
		}

		public static void set_token_R(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			if (arg == "") {
				_ = msg.RespondAsync(":warning: Tokens cannot be set to empty strings.\nNo settings have been changed.");
				return;
			}

			string token_old = Program.settings[guild_id].token_R;
			Program.settings[guild_id].token_R = arg;
			_ = msg.RespondAsync($":white_check_mark: Right-hand token changed from `{token_old}` to `{arg}`.");
		}

		public static void set_split(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			if (arg == "") {
				_ = msg.RespondAsync(":warning: Tokens cannot be set to empty strings.\nNo settings have been changed.");
				return;
			}

			string token_old = Program.settings[guild_id].split;
			Program.settings[guild_id].split = arg;
			_ = msg.RespondAsync($":white_check_mark: Splitter token changed from `{token_old}` to `{arg}`.");
		}

		public static void view_tokens(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);
			Settings settings = Program.settings[guild_id];

			_ = msg.RespondAsync($":information_source: Search token format:\n`{settings.token_L}`query`{settings.split}`meta`{settings.token_R}`");
		}

		public static void reset_server_settings(string arg, DiscordMessage msg) {
			_ = new Settings((ulong)msg.Channel.GuildId);
			msg.RespondAsync(":white_check_mark: All server settings have been reset to their defaults.\n:information_source: Server statistics have not been reset.");
		}

		public static void stats(string arg, DiscordMessage msg) {
		}

		public static void version(string arg, DiscordMessage msg) {
			StreamReader file;

			file = File.OpenText(Program.path_build);
			string build = file.ReadLine();
			build = build[..7];
			file.Close();

			file = File.OpenText(Program.path_version);
			string version = file.ReadLine();
			file.Close();

			msg.RespondAsync($":information_source: **Polybius {version}** build `{build}`");
		}

		// If `msg.Channel.Guild` is `null`, return false, and reply to
		// the original message explaining.
		static bool check_guild_exists(DiscordMessage msg) {
			if (msg.Channel.GuildId is null) {
				string str_no_server =
					"Could not find a Discord server to configure. " + 
					":zipper_mouth:";
				_ = msg.RespondAsync(str_no_server);
				return false;
			}
			return true;
		}

		// Creates a new default Settings file for the given server if
		// one doesn't exist already.
		// Returns true when a new file was created, false otherwise.
		static bool try_init_settings(ulong guild_id) {
			if (Program.settings[guild_id] is null) {
				Program.settings.Add(guild_id, new (guild_id));
				return true;
			} else {
				return false;
			}
		}

		static ulong? extract_channel_id(string text, DiscordMessage msg) {
			if (msg.MentionedChannels.Count > 0) {
				return msg.MentionedChannels[0].Id;
			} else if (Regex.IsMatch(text, @"\d+")) {
				return Convert.ToUInt64(text);
			} else {
				text = text.ToLower();
				foreach (DiscordChannel channel in get_guild_channels(msg)) {
					if (channel.Name == text)
						return channel.Id;
				}
			}
			return null;
		}

		static IEnumerable<DiscordChannel> get_guild_channels(DiscordMessage msg) {
			return msg.Channel.Guild.Channels.Values;
		}
	}
}
