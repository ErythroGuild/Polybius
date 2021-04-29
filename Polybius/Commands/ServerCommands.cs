using System;
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
			if (Program.settings[guild_id] is null) {
				Program.settings.Add(guild_id, new (guild_id));
			}

			ulong? ch_bot = null;
			if (msg.MentionedChannels.Count == 1) {
				ch_bot = msg.MentionedChannels[0].Id;
			} else if (Regex.IsMatch(arg, @"\d+")) {
				ch_bot = Convert.ToUInt64(arg);
			} else {
				arg = arg.ToLower();
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
			if (!guild_exists) { return; }

			ulong guild_id = (ulong)msg.Channel.GuildId;
			if (Program.settings[guild_id] is null) {
				Program.settings.Add(guild_id, new(guild_id));
			}

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
	}
}
