using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Serilog;

using Polybius.Commands;
using Polybius.Engines;

namespace Polybius {
	using CommandFunc = Action<string, DiscordMessage>;
	using CommandTable = Dictionary<string, Action<string, DiscordMessage>>;
	using PermissionTable = Dictionary<Action<string, DiscordMessage>, Permissions>;

	class Program {
		record ChannelBotPair(ulong ch, ulong bot);

		// Discord client objects.
		internal static readonly DiscordClient polybius;
		internal static readonly Dictionary<ulong, Settings> settings = new ();
		internal static readonly Logger log;
		static readonly Stopwatch stopwatch_connect;

		// File paths for config files.
		internal const string path_build = @"config/commit.txt";
		internal const string path_version = @"config/tag.txt";
		internal const string path_serilog = @"logs_D#+/serilog.txt";
		internal const string dir_logs = @"logs";
#if RELEASE
		const string path_token = @"config/token.txt";
#else
		const string path_token = @"config/token_debug.txt";
#endif

		// User ID of the account to accept admin commands from.
		internal const ulong id_user_admin = 165557736287764483;

		// Rate limits on responses to bot messages.
		static readonly Dictionary<ChannelBotPair, Queue<DateTime>>
			bot_queues_short = new (),
			bot_queues_long = new ();
		internal static readonly TimeSpan
			ratelimit_short = TimeSpan.FromSeconds(10),
			ratelimit_long = TimeSpan.FromMinutes(1);
		internal const int
			rate_short = 5,
			rate_long = 8;

		// Per message caps for queries and results.
		internal const int cap_tokens = 5;
		internal const int cap_results = 3;

		internal static readonly CommandTable command_list = new () {
			{ "help", HelpCommand.main },
			{ "h"   , HelpCommand.main },
			{ "?"   , HelpCommand.main },
			{ "blacklist"        , ServerCommands.blacklist         },
			{ "whitelist"        , ServerCommands.whitelist         },
			{ "bot-channel"      , ServerCommands.bot_channel       },
			{ "bot-channel-set"  , ServerCommands.bot_channel       },
			{ "set-bot-channel"  , ServerCommands.bot_channel       },
			{ "clear-bot-channel", ServerCommands.bot_channel_clear },
			{ "bot-channel-clear", ServerCommands.bot_channel_clear },
			{ "unset-bot-channel", ServerCommands.bot_channel_clear },
			{ "bot-channel-unset", ServerCommands.bot_channel_clear },
			{ "reset-bot-channel", ServerCommands.bot_channel_clear },
			{ "bot-channel-reset", ServerCommands.bot_channel_clear },
			{ "view-filters"     , ServerCommands.view_filters      },
			{ "view-blacklist"   , ServerCommands.view_filters      },
			{ "view-whitelist"   , ServerCommands.view_filters      },
			{ "set-token-l"      , ServerCommands.set_token_L       },
			{ "set-token-r"      , ServerCommands.set_token_R       },
			{ "set-split"        , ServerCommands.set_split         },
			{ "view-tokens"	     , ServerCommands.view_tokens       },
			{ "view-token"       , ServerCommands.view_tokens       },
			{ "reset-server-settings", ServerCommands.reset_server_settings },
			{ "stats"            , ServerCommands.stats             },
			{ "version"          , ServerCommands.version           },
			{ "build"            , ServerCommands.version           },
			{ "exit"           , AdminCommands.exit           },
			{ "end"            , AdminCommands.exit           },
			{ "kill"           , AdminCommands.exit           },
			{ "terminate"      , AdminCommands.exit           },
			{ "restart"        , AdminCommands.restart        },
			{ "reboot"         , AdminCommands.restart        },
			{ "suspend_db"     , AdminCommands.suspend_db     },
			{ "resume_db"      , AdminCommands.resume_db      },
			{ "refresh_servers", AdminCommands.refresh_guilds },
			{ "refresh_guilds" , AdminCommands.refresh_guilds },
			{ "global_stats"   , AdminCommands.global_stats   },
		};

		static readonly PermissionTable dict_permission = new () {
			{ ServerCommands.blacklist        , Permissions.ManageGuild    },
			{ ServerCommands.whitelist        , Permissions.ManageGuild    },
			{ ServerCommands.bot_channel      , Permissions.ManageGuild    },
			{ ServerCommands.bot_channel_clear, Permissions.ManageGuild    },
			{ ServerCommands.view_filters     , Permissions.AccessChannels },
			{ ServerCommands.set_token_L      , Permissions.ManageGuild    },
			{ ServerCommands.set_token_R      , Permissions.ManageGuild    },
			{ ServerCommands.set_split        , Permissions.ManageGuild    },
			{ ServerCommands.view_tokens      , Permissions.AccessChannels },
			{ ServerCommands.reset_server_settings, Permissions.ManageGuild },
			{ ServerCommands.stats            , Permissions.ViewAuditLog   },
		};

		static readonly List<CommandFunc> dict_admin = new () {
			AdminCommands.exit,
			AdminCommands.restart,
			AdminCommands.suspend_db,
			AdminCommands.resume_db,
			AdminCommands.refresh_guilds,
			AdminCommands.global_stats,
		};

		static Program() {
			log = new Logger(dir_logs, TimeSpan.FromDays(1));
			log.info("Initializing Polybius...");

			// Parse authentication token from file.
			log.info("  Reading auth token...");
			string bot_token = "";
			using (StreamReader token = File.OpenText(path_token)) {
				bot_token = token.ReadLine() ?? "";
			}
			if (bot_token != "") {
				log.info("  Auth token found.");
				int disp_size = 8;
				string token_disp =
					bot_token[..disp_size] +
					new string('*', bot_token.Length - 2*disp_size) +
					bot_token[^disp_size..];
				log.debug($"    {token_disp}");
			} else {
				log.error("  Could not find auth token.");
				log.debug($"    Path: {path_token}");
				throw new FormatException($"Could not find auth token at {path_token}.");
			}

			// Initialize Serilog and connect it to Logger.
			log.debug("  Setting up Serilog...");
			Log.Logger = new LoggerConfiguration()
				.WriteTo.File(
					path_serilog,
					outputTemplate: "{Timestamp:yyyy-MM-dd H:mm:ss.ff} > [{Level:u3}] {Message}{NewLine}",
					rollingInterval: RollingInterval.Day)
				.CreateLogger();
			var serilog = new LoggerFactory().AddSerilog();
			log.debug("  Serilog has been set up.");

			// Initialize `DiscordClient`.
			stopwatch_connect = Stopwatch.StartNew();
			log.info("  Logging in to Discord.");
			polybius = new DiscordClient(new DiscordConfiguration {
				LoggerFactory = serilog,
				Token = bot_token,
				TokenType = TokenType.Bot
			});

			log.info("Polybius initialized.");
		}

		static void Main() {
			const string title_ascii =
				@"     ___      _       _     _           " + "\n" +
				@"    / _ \___ | |_   _| |__ (_)_   _ ___ " + "\n" +
				@"   / /_)/ _ \| | | | | '_ \| | | | / __|" + "\n" +
				@"  / ___/ (_) | | |_| | |_) | | |_| \__ \" + "\n" +
				@"  \/    \___/|_|\__, |_.__/|_|\__,_|___/" + "\n" +
				@"                |___/                   " + "\n";
			Console.ForegroundColor = ConsoleColor.DarkRed;
			Console.WriteLine();
			Console.WriteLine(title_ascii);
			Console.ForegroundColor = ConsoleColor.Gray;
			MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
		}

		static async Task MainAsync() {
			// Connected to discord servers (but not necessarily guilds yet!).
			polybius.Ready += (polybius, e) => {
				_ = Task.Run(() => {
					DiscordActivity helptext =
						new ("@Polybius -help", ActivityType.Watching);
					polybius.UpdateStatusAsync(helptext);

					stopwatch_connect.Stop();

					log.info("  Logged in to Discord servers.");
					log.debug($"    Took {stopwatch_connect.ElapsedMilliseconds} msec.");
					log.debug($"    Connected to {polybius.Guilds.Count} server{(polybius.Guilds.Count==1 ? "" : "s")}.");
					log.endl();
					log.info("Monitoring messages...");
					log.endl();
				});
				return Task.CompletedTask;
			};

			// Guild data has finished downloading.
			polybius.GuildDownloadCompleted += (polybius, e) => {
				_ = Task.Run(() => {
					log.info("Server data downloaded.");
					log.info("Reading and updating saved settings...");
					Stopwatch stopwatch = Stopwatch.StartNew();
					foreach (ulong id in e.Guilds.Keys) {
						update_guild_name(e.Guilds[id]);
						log.debug($"  Server: {e.Guilds[id].Name}");

						// load existing settings if possible; else set to default
						Settings settings_guild;
						if (Settings.has_save(id)) {
							log.debug("    Loading existing settings...");
							settings_guild = Settings.load(id);
							log.debug("    Settings loaded.");
						} else {
							log.debug("    No existing settings found.");
							log.debug("    Initializing with default settings...");
							settings_guild = new (id);
							settings_guild.save();
							log.debug("    Settings initialized to default.");
						}
						settings.Add(id, settings_guild);
					}

					stopwatch.Stop();
					log.info("Server settings updated.");
					log.debug($"  Took {stopwatch.ElapsedMilliseconds} msec.");
					log.endl();
				});
				return Task.CompletedTask;
			};

			// Was added to a new guild.
			polybius.GuildCreated += (polybius, e) => {
				_ = Task.Run(() => {
					log.info($"Added to new guild: {e.Guild.Name}");
					log.debug("  Initializing to default settings...");
					update_guild_name(e.Guild);
					Settings settings_guild = new (e.Guild.Id);
					settings_guild.save();
					settings.Add(e.Guild.Id, settings_guild);
					log.debug("  Settings initialized.");
					log.endl();
				});
				return Task.CompletedTask;
			};

			// Was removed from a guild.
			polybius.GuildDeleted += (polybius, e) => {
				_ = Task.Run(() => {
					log.info($"Removed from server: {e.Guild.Name}");
					log.info("  Deleting saved settings...");

					// Server data: `config/guild-{guild_id}/`
					// `_server_name.txt`
					// `settings.txt`
					string path_dir = Settings.path_save_base +
						e.Guild.Id.ToString();
					string path_name = $"{path_dir}/{Settings.path_name_file}";
					string path_save = $"{path_dir}/{Settings.path_save_file}";

					try {
						if (File.Exists(path_name)) {
							File.Delete(path_name);
						}
						if (File.Exists(path_save)) {
							File.Delete(path_save);
						}
						if (Directory.Exists(path_dir)) {
							Directory.Delete(path_dir);
						}
					} catch (IOException) {
						log.error($"  Could not delete settings for server: {e.Guild.Id}");
					}

					log.info("  Settings deleted.");
					log.endl();
				});
				return Task.CompletedTask;
			};

			// Any monitored guild has updated their info.
			polybius.GuildUpdated += (polybius, e) => {
				_ = Task.Run(() => {
					log.debug($"Updating server info for: {e.GuildBefore.Name}");
					update_guild_name(e.GuildAfter);
					log.debug($"  Server info updated for: {e.GuildAfter.Name}");
					log.endl();
				});
				return Task.CompletedTask;
			};

			// Received a message from any readable channel.
			polybius.MessageCreated += (polybius, e) => {
				_ = Task.Run(async () => {
					DiscordMessage msg = e.Message;

					// Never respond to self!
					if (msg.Author == polybius.CurrentUser)
						{ return; }

					// Never respond to DMs!
					if (e.Guild is null)
						{ return; }

					// Rate-limit responses to other bots.
					if (msg.Author.IsBot) {
						ChannelBotPair ch_bot_id = new (msg.ChannelId, msg.Author.Id);

						try_init_ratelimit(ch_bot_id);
						bool is_limited = !try_process_ratelimit(ch_bot_id);
						string bot_name = $"{msg.Author.Username}#{msg.Author.Discriminator}";
						if (is_limited) {
							log.info($"Message received from bot: {bot_name}");
							log.warning($"  {bot_name} is currently being ratelimited!");
							log.info("  Message will be silently discarded.");
							log.endl();
							return;
						}
					}

					// Check if channel is illegal to respond in.
					if (!is_channel_tracked(msg.Channel))
						{ return; }

					// Trim leading whitespace.
					string msg_text = msg.Content.TrimStart();

					// Respond to commands (prefix is mention string).
					string mention_str = polybius.CurrentUser.Mention;
					if (msg_text.StartsWith(mention_str)) {
						msg_text = msg_text[mention_str.Length..];
						msg_text = msg_text.TrimStart();
						log.info("Command received.");
						log.debug($"  {msg_text}");
						await process_commands(msg_text, msg);
						log.endl();
					}

					// Check for queries and exit if none are found.
					// Discard blank queries.
					Stopwatch stopwatch = Stopwatch.StartNew();
					List<SearchToken> tokens =
						extract_tokens(msg_text, msg.Channel?.GuildId ?? null);
					bool did_discard_token = false;
					foreach (SearchToken token in tokens) {
						if (token.text.Trim() == "") {
							tokens.Remove(token);
							log.debug("  Blank query discarded.");
							did_discard_token = true;
						}
					}
					if (did_discard_token)
						{ log.endl(); }
					if (tokens.Count == 0)
						{ return; }

					// Log message.
					log.info("Queries found in message.");
					log.debug($"  Took {stopwatch.ElapsedMilliseconds} msec to parse.");
					log.debug($"  {msg.Content}");

					// Cap the number of queries accepted per message.
					if (tokens.Count > cap_tokens) {
						log.warning("  Query cap (per message) exceeded. Discarding excess queries.");
						log.debug($"    {tokens.Count} quer{(tokens.Count==1 ? "y" : "ies" )} found.");
						log.debug($"    Keeping first {cap_tokens} quer{(tokens.Count==1 ? "y" : "ies")}.");
						tokens = tokens.GetRange(0, cap_tokens);
					}

					// Indicate to the user that their query has been received
					// and is currently being processed.
					stopwatch.Restart();
					if (msg.Channel is not null)
						{ await msg.Channel.TriggerTypingAsync(); }

					foreach (SearchToken token in tokens) {
						log.info($@"  Query: text - {token.text}, meta - {token.meta}");

						List<SearchResult> results = new ();
						results.AddRange(WowheadEngine.search(token));
						results.AddRange(EasterEggEngine.search(token));

						// Handle case where no results were found.
						if (results.Count == 0) {
							_ = msg.RespondAsync($"No results found for `{token.text}`.");
							log.info("    No results found.");
							log.endl();
							return;
						}

						// Cap the results returned per query.
						log.info($"    {results.Count} result{(results.Count==1 ? "" : "s")} found for query.");
						if (results.Count > cap_results) {
							log.info($"    Only displaying the first {cap_results} result{(cap_results==1 ? "" : "s")}.");
							results = results.GetRange(0, cap_results);
						}

						// Display results.
						foreach (SearchResult result in results) {
							if (result.is_exact_match) {
								DiscordChannel channel =
									await find_reply_channel(msg);
								_ = result.get_display()
									.WithReply(msg.Id)
									.SendAsync(channel);
								log.info($"  Result: {result.name}");
								log.debug($"    {result.data}");
							}
						}
					}

					stopwatch.Stop();
					log.debug($"  Searches took {stopwatch.ElapsedMilliseconds} msec total.");
					log.endl();
				});
				return Task.CompletedTask;
			};

			await polybius.ConnectAsync();
			await Task.Delay(-1);
		}

		// Updates the guild name of a specific guild.
		public static void update_guild_name(DiscordGuild guild) {
			// Update `config/guild-{guild_id}/_server_name.txt`.
			string file_path =
				$"{Settings.path_save_base}{guild.Id}/{Settings.path_name_file}";

			try {
				// directory must exist before creating a file there.
				Directory.CreateDirectory(Settings.path_save_base + guild.Id.ToString());
				StreamWriter file = new(file_path);
				file.WriteLine(guild.Name);
				file.Close();
			} catch {
				log.error($"  Could not update server name for {guild.Name}.");
				log.debug("    Could not create save file.");
			}
		}

		// Initialize rate-limiting queues if the request is from a new
		// channel/bot combo.
		static void try_init_ratelimit(ChannelBotPair id) {
			if (!bot_queues_short.ContainsKey(id)) {
				bot_queues_short.Add(id, new ());
			}
			if (!bot_queues_long.ContainsKey(id)) {
				bot_queues_long.Add(id, new ());
			}
		}

		// Advance the rate-limiting queue, but not if the rate-limit has
		// been hit.
		// Returns `true` if rate-limit is not hit; `false` if it is.
		static bool try_process_ratelimit(ChannelBotPair id) {
			DateTime now = DateTime.Now;

			if (bot_queues_short[id].Count >= rate_short) {
				if (now - bot_queues_short[id].Peek() < ratelimit_short)
					{ return false; }
				else
					{ bot_queues_short[id].Dequeue(); }
			}
			if (bot_queues_long[id].Count >= rate_long) {
				if (now - bot_queues_long[id].Peek() < ratelimit_long)
					{ return false; }
				else
					{ bot_queues_long[id].Dequeue(); }
			}

			bot_queues_short[id].Enqueue(now);
			bot_queues_long[id].Enqueue(now);
			return true;
		}

		// Returns false if the channel should not be responded to,
		// either in the channel itself or a bot channel.
		public static bool is_channel_tracked(DiscordChannel channel) {
			// Track non-server channels.
			if (channel.GuildId is null) {
				return true;
			}

			ulong guild_id = (ulong)channel.GuildId;
			ulong ch_id = channel.Id;

			// Track the bot channel, if it exists.
			if ((settings[guild_id].ch_bot is not null) &&
				(settings[guild_id].ch_bot == ch_id)) {
				return true;
			}

			// Never track any channel on the blacklist.
			if (settings[guild_id].ch_blacklist.Contains(ch_id)) {
				return false;
			}

			// If whitelist exists, only track channels on the whitelist.
			if (settings[guild_id].ch_whitelist.Count == 0) {
				return true;
			} else if (settings[guild_id].ch_whitelist.Contains(ch_id)) {
				return false;
			} else {
				return true;
			}
		}

		// Process and call the response methods to any commands.
		static async Task process_commands(string input, DiscordMessage msg) {
			if (input.StartsWith("-")) {
				input = input[1..];
				string[] msg_split = input.Split(' ', 2);
				string cmd = msg_split[0].ToLower();
				string arg = "";
				if (msg_split.Length > 1)
					{ arg = msg_split[1]; }
				arg = arg.Trim();

				if (command_list.ContainsKey(cmd)) {
					CommandFunc command = command_list[cmd];
					// Check if server permissions are needed (and met).
					if (dict_permission.ContainsKey(command)) {
						DiscordGuild guild = msg.Channel.Guild;
						DiscordMember author = await guild.GetMemberAsync(msg.Author.Id);
						Permissions permissions = author.PermissionsIn(msg.Channel);
						Permissions permission_req = dict_permission[command];
						if (!permissions.HasPermission(permission_req)) {
							_ = msg.RespondAsync(":warning: You do not have sufficient permissions to use that command.");
							log.info($"  {msg.Author.Username}#{msg.Author.Discriminator} does not have permission to use this command.");
							return;
						}
					}
					// Check if admin permissions are needed (and met).
					if (dict_admin.Contains(command)) {
						if (msg.Author.Id != id_user_admin) {
							_ = msg.RespondAsync(":warning: Only the Polybius admin can use that command.");
							log.info($"  {msg.Author.Username}#{msg.Author.Discriminator} attempted to use an admin command.");
							return;
						}
					}
					command_list[cmd](arg, msg);
				} else {
					_ = msg.RespondAsync($":confused: Unknown command. Use `{HelpCommand.m} -help` for more info.");
					log.info("  Command not recognized.");
				}
				return;
			}
		}

		// Matches all tokens of the format `[[TOKEN]]`.
		public static List<SearchToken> extract_tokens(string msg, ulong? guild_id) {
			Regex regex_token;
			if (guild_id is null) {
				regex_token = Settings.regex_token_default();
			} else {
				regex_token = settings[(ulong)guild_id].regex_token();
			}

			List<SearchToken> tokens = new ();
			MatchCollection matches = regex_token.Matches(msg);
			foreach (Match match in matches) {
				string text = match.Groups[Settings.group_query].Value;
				text = text.Trim().ToLower();
				string meta = match.Groups[Settings.group_meta].Value;
				meta = meta.Trim();
				tokens.Add(new SearchToken(text, meta));
			}

			return tokens;
		}

		// Determine the correct channel to reply in.
		internal static async Task<DiscordChannel> find_reply_channel(DiscordMessage msg) {
			if (msg.Channel.GuildId is null)
				{ return msg.Channel; }

			ulong? ch_bot =
				settings[(ulong)msg.Channel.GuildId].ch_bot;
			if (ch_bot is null) { 
				return msg.Channel;
			} else {
				return await polybius.GetChannelAsync((ulong)ch_bot);
			}
		}
	}
}
