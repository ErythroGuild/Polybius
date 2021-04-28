using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DSharpPlus;       // C# Discord API
using DSharpPlus.Entities;
using HtmlAgilityPack;  // HTTP client + HTML parser

using Polybius.Commands;
using Polybius.Engines;

namespace Polybius {
	using CommandTable = Dictionary<string, Action<string, DiscordMessage>>;

	class Program {
		public record QueryMetaPair(string query, string meta);
		private record ChannelBotPair(ulong ch, ulong bot);

		private static DiscordClient polybius;
		private static HtmlWeb http;

		private static Dictionary<ulong, Settings> settings = new ();
		private static Dictionary<ChannelBotPair, Queue<DateTime>>
			bot_queues_short = new (),
			bot_queues_long = new ();

		private const string path_token = @"config/token.txt";
		internal const int color_embed = 0x9A61F1;

		// Rate limits on responses to bot messages.
		private static readonly TimeSpan
			ratelimit_short = TimeSpan.FromSeconds(10),
			ratelimit_long = TimeSpan.FromMinutes(1);
		private const int rate_short = 5, rate_long = 8;

		private static readonly CommandTable command_list = new () {
			{ "help", HelpCommand.main },
			{ "h"   , HelpCommand.main },
			{ "?"   , HelpCommand.main },
			{ "blacklist"      , ServerCommands.blacklist     },
			{ "whitelist"      , ServerCommands.whitelist     },
			{ "bot-channel"    , ServerCommands.bot_channel   },
			{ "botspam"        , ServerCommands.bot_channel   },
			{ "view-filters"   , ServerCommands.view_filters  },
			{ "view-blacklist" , ServerCommands.view_filters  },
			{ "view-whitelist" , ServerCommands.view_filters  },
			{ "set-token-l"    , ServerCommands.set_token_L   },
			{ "set-token-r"    , ServerCommands.set_token_R   },
			{ "set-split"      , ServerCommands.set_split     },
			{ "view-tokens"	   , ServerCommands.view_tokens   },
			{ "view-token"     , ServerCommands.view_tokens   },
			{ "stats"          , ServerCommands.stats         },
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

		static void Main() {
			const string title_ascii =
				@"     ___      _       _     _           " + "\n" +
				@"    / _ \___ | |_   _| |__ (_)_   _ ___ " + "\n" +
				@"   / /_)/ _ \| | | | | '_ \| | | | / __|" + "\n" +
				@"  / ___/ (_) | | |_| | |_) | | |_| \__ \" + "\n" +
				@"  \/    \___/|_|\__, |_.__/|_|\__,_|___/" + "\n" +
				@"                |___/                   " + "\n";
			Console.WriteLine(title_ascii);
			MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
		}

		static async Task MainAsync() {
			init_bot();

			// Connected to discord servers (but not necessarily guilds yet!).
			polybius.Ready += async (polybius, e) =>
				 await Task.Run(() => {
					DiscordActivity helptext =
						new ("@Polybius -help", ActivityType.Watching);
					polybius.UpdateStatusAsync(helptext);

					Console.WriteLine("Connected to discord servers.");
					Console.WriteLine($"Connected to {polybius.Guilds.Count} server(s).");
					Console.WriteLine("Monitoring messages...\n");
				});

			// Guild data has finished downloading.
			polybius.GuildDownloadCompleted += async (polybius, e) =>
				await Task.Run(() => {
					foreach (ulong id in e.Guilds.Keys) {
						update_guild_name(e.Guilds[id]);

						// load existing settings if possible; else set to default
						Settings settings_guild;
						if (Settings.has_save(id)) {
							settings_guild = Settings.load(id);
						} else {
							settings_guild = new (id);
							settings_guild.save();
						}
						settings.Add(id, settings_guild);
					}
				});

			// Was added to a new guild.
			polybius.GuildCreated += async (polybius, e) =>
				await Task.Run(() => {
					update_guild_name(e.Guild);
					Settings settings_guild = new (e.Guild.Id);
					settings_guild.save();
					settings.Add(e.Guild.Id, settings_guild);
				});

			// Was removed from a guild.
			polybius.GuildDeleted += async (polybius, e) =>
				await Task.Run(() => {
					// Server data: `config/guild-{guild_id}/`
					// `_server_name.txt`
					// `settings.txt`
					string path_dir = Settings.path_save_base +
						e.Guild.Id.ToString();
					string path_name = $"{path_dir}/{Settings.path_name_file}";
					string path_save = $"{path_dir}/{Settings.path_save_file}";
					if (File.Exists(path_name)) {
						File.Delete(path_name);
					}
					if (File.Exists(path_save)) {
						File.Delete(path_save);
					}
					if (Directory.Exists(path_dir)) {
						Directory.Delete(path_dir);
					}
				});

			// Any monitored guild has updated their info.
			polybius.GuildUpdated += async (polybius, e) =>
				await Task.Run(() => {
					update_guild_name(e.GuildAfter);
				});

			// Received a message from any readable channel.
			polybius.MessageCreated += async (polybius, e) => 
				await Task.Run(async () => {
					DiscordMessage msg = e.Message;

					// Never respond to self!
					if (msg.Author == polybius.CurrentUser)
						{ return; }

					// Rate-limit responses to other bots.
					if (msg.Author.IsBot) {
						ChannelBotPair ch_bot_id = new (msg.ChannelId, msg.Author.Id);

						try_init_ratelimit(ch_bot_id);
						bool is_limited = !try_process_ratelimit(ch_bot_id);
						if (is_limited)
							{ return; }
					}

					// Check that channel is legal to respond in.

					// Trim (but not trailing) leading whitespace.
					string msg_text = msg.Content.TrimStart();

					// Respond to commands (prefix is mention string).
					string mention_str = polybius.CurrentUser.Mention;
					if (msg_text.StartsWith(mention_str)) {
						msg_text = msg_text[mention_str.Length..];
						msg_text = msg_text.TrimStart();

						if (msg_text.StartsWith("-")) {
							msg_text = msg_text[1..];
							string[] msg_split = msg_text.Split(' ', 2);
							string cmd = msg_split[0].ToLower();
							string arg = msg_split[1];

							if (command_list.ContainsKey(cmd)) {
								command_list[cmd](arg, msg);
								return;
							}
						}
					}
				
					// Check for queries and exit if none are found.
					List<QueryMetaPair> queries =
						extract_queries(msg_text, msg.Channel?.GuildId ?? null);
					if (queries.Count == 0)
						{ return; }

					await msg.Channel.TriggerTypingAsync();

					foreach (QueryMetaPair query in queries) {
						Console.WriteLine($"\nQuery parsed: {query.query}, {query.meta}");

						List<SearchResult> results = new ();
						results.AddRange(EasterEggEngine.search(query));

						if (results.Count == 0) {
							Console.WriteLine("> No results found.");
							_ = msg.RespondAsync($"No results found for `{query.query}`.");
							return;
						}

						foreach (SearchResult result in results) {
							if (result.is_exact_match) {
								DiscordChannel channel =
									await find_reply_channel(msg);
								_ = result.get_display()
									.WithReply(msg.Id)
									.SendAsync(channel);
							}
						}
					}
				});

			await polybius.ConnectAsync();
			await Task.Delay(-1);
		}

		// Init discord client with token from text file.
		// This allows the token to be separated from source code.
		static void init_bot() {
			Console.WriteLine("Initializing Polybius...");
			Console.WriteLine("Reading auth token...");
			string bot_token = "";
			using (StreamReader token = File.OpenText(path_token)) {
				bot_token = token.ReadLine();
			}
			if (bot_token != "")
				Console.WriteLine("Auth token found.");

			polybius = new DiscordClient(new DiscordConfiguration {
				Token = bot_token,
				TokenType = TokenType.Bot
			});

			// Init HtmlAgilityPack parser.
			http = new HtmlWeb();
		}

		// Updates the guild name of a specific guild.
		static void update_guild_name(DiscordGuild guild) {
			// Update `config/guild-{guild_id}/_server_name.txt`.
			string file_path =
				Settings.path_save_base + guild.Id.ToString() + "/" +
				Settings.path_name_file;
			// directory must exist before creating a file there.
			Directory.CreateDirectory(Settings.path_save_base + guild.Id.ToString());
			StreamWriter file = new StreamWriter(file_path);
			file.WriteLine(guild.Name);
			file.Close();
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

		// Matches all tokens of the format `[[TOKEN]]`.
		static List<QueryMetaPair> extract_queries(string msg, ulong? guild_id) {
			Regex regex_query;
			if (guild_id is null) {
				regex_query = Settings.regex_query_default();
			} else {
				regex_query = settings[(ulong)guild_id].regex_query();
			}

			List<QueryMetaPair> queries = new ();
			MatchCollection matches = regex_query.Matches(msg);
			foreach (Match match in matches) {
				string query = match.Groups[Settings.group_query].Value;
				query = query.Trim().ToLower();
				string meta = match.Groups[Settings.group_meta].Value;
				meta = meta.Trim();
				queries.Add(new (query, meta));
			}

			return queries;
		}

		// Determine the correct channel to reply in.
		static async Task<DiscordChannel> find_reply_channel(DiscordMessage msg) {
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
