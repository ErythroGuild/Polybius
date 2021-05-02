using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;

namespace Polybius.Commands {
	class ServerCommands {
		public static void whitelist(string arg, DiscordMessage msg) {
		}

		public static void blacklist(string arg, DiscordMessage msg) {
		}

		public static void bot_channel(string arg, DiscordMessage msg) {
			bool guild_exists = check_guild_exists(msg);
			if (!guild_exists)
				{ return; }
			ulong guild_id = (ulong)msg.Channel.GuildId;
			try_init_settings(guild_id);

			ulong? ch_bot = extract_channel_id(arg, msg);
			if (ch_bot is null) {
				_ = msg.RespondAsync(":no_entry: Could not parse channel name.\nNo changes have been made.");
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
		}

		public static void set_token_L(string arg, DiscordMessage msg) {
		}

		public static void set_token_R(string arg, DiscordMessage msg) {
		}

		public static void set_split(string arg, DiscordMessage msg) {
		}

		public static void view_tokens(string arg, DiscordMessage msg) {
		}

		public static void reset_server_settings(string arg, DiscordMessage msg) {
			_ = new Settings((ulong)msg.Channel.GuildId);
			msg.RespondAsync(":white_check_mark: All server settings have been reset to their defaults.\n:information_source: Server statistics have not been reset.");
		}

		public static void stats(string arg, DiscordMessage msg) {
		}

		// If `msg.Channel.Guild` is `null`, return false, and reply to
		// the original message explaining.
		private static bool check_guild_exists(DiscordMessage msg) {
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
		private static bool try_init_settings(ulong guild_id) {
			if (Program.settings[guild_id] is null) {
				Program.settings.Add(guild_id, new (guild_id));
				return true;
			} else {
				return false;
			}
		}

		private static ulong? extract_channel_id(string text, DiscordMessage msg) {
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

		private static IEnumerable<DiscordChannel> get_guild_channels(DiscordMessage msg) {
			return msg.Channel.Guild.Channels.Values;
		}
	}
}
