using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Polybius {
	class Settings {
		// Regex group names.
		public const string
			group_query = "query",
			group_meta = "meta";

		// Default regex tokens.
		public const string
			token_L_default = "[[",
			token_R_default = "]]",
			split_default = "|";

		// `config/guild-{guild_id}/settings.txt`
		public const string
			path_save_base = "config/guild-",
			path_save_file = "settings.txt",
			path_name_file = "_server_name.txt";

		// Private backing fields for all settings.
		bool _do_log_stats;
		string _token_L, _token_R, _split;
		ulong? _ch_bot;
		HashSet<ulong> _ch_whitelist, _ch_blacklist;

		// Hiding setter since guild_id should be immutable;
		// all other property setters also save the entire object.
		public ulong id { get; private set; }
		public bool do_log_stats {
			get => _do_log_stats;
			set { _do_log_stats = value; save(); }
		}
		public string token_L {
			get => _token_L;
			set { _token_L = value; save(); }
		}
		public string token_R {
			get => _token_R;
			set { _token_R = value; save(); }
		}
		public string split {
			get => _split;
			set { _split = value; save(); }
		}
		public ulong? ch_bot {
			get => _ch_bot;
			set { _ch_bot = value; save(); }
		}
		public HashSet<ulong> ch_whitelist {
			get => _ch_whitelist;
			set { _ch_whitelist = value; save(); }
		}
		public HashSet<ulong> ch_blacklist {
			get => _ch_blacklist;
			set { _ch_blacklist = value; save(); }
		}

		// Default constructor:
		// stat logging, [[query|meta]] tokens, no bot channel
		// Directly accesses the private backing fields to avoid saving
		// the entire file on every access.
		public Settings(ulong id) {
			this.id = id;
			_do_log_stats = true;
			_token_L = token_L_default;
			_token_R = token_R_default;
			_split = split_default;
			_ch_bot = null;
			_ch_whitelist = new ();
			_ch_blacklist = new ();
		}

		public static Regex regex_query_default() {
			string regex =
				$@"{Regex.Escape(token_L_default)}(?<{group_query}>.+?)" +
				$@"(?:{Regex.Escape(split_default)}(?<{group_meta}>.+?))?" +
				$@"{Regex.Escape(token_R_default)}";
			return new Regex(regex, RegexOptions.IgnoreCase);
		}

		public Regex regex_query() {
			// e.g.:
			// \Q[[\E(?<query>.+?)(?:\Q|\E(?<meta>.+?))?\Q]]\E
			string regex =
				$@"{Regex.Escape(token_L)}(?<{group_query}>.+?)" +
				$@"(?:{Regex.Escape(split)}(?<{group_meta}>.+?))?" +
				$@"{Regex.Escape(token_R)}";
			return new Regex(regex, RegexOptions.IgnoreCase);
		}

		// Returns whether or not the bot is allowed to post in a channel,
		// but does not take into account the bot channel (`ch_bot`).
		public bool is_ch_safe(ulong id) {
			if (ch_whitelist.Count > 0) {
				if (!ch_whitelist.Contains(id)) {
					// a whitelist exists and the channel is not on it
					return false;
				} else if (ch_blacklist.Contains(id)) {
					// a whitelist exists and the channel is on it, &&
					// the channel is also on the blacklist
					return false;
				} else {
					// a whitelist exists and the channel is on it, &&
					// the channel is not on the blacklist
					return true;
				}
			} else if (!ch_blacklist.Contains(id)) {
				// a whitelist does not exist, &&
				// the channel is not on the blacklist
				return true;
			} else {
				// a whitelist does not exist, &&
				// the channel is on the blacklist
				return false;
			}
		}

		// These variables are only used to save/load settings.
		const string delim_key = "=", delim_entry = ",";
		const string str_null = "null";
		const string
			key_log_stats    = "do_log_stats",
			key_token_L      = "token_L",
			key_token_R      = "token_R",
			key_split	     = "split",
			key_ch_bot       = "ch_bot",
			key_ch_whitelist = "ch_whitelist",
			key_ch_blacklist = "ch_blacklist";

		static string get_path_save(ulong id) {
			return $"{path_save_base}{id}/{path_save_file}";
		}

		string get_path_save() {
			return get_path_save(id);
		}

		public static bool has_save(ulong id) {
			return File.Exists(get_path_save(id));
		}

		public void save() {
			try {
				// Directory must exist before attempting to create a file there.
				Directory.CreateDirectory($"{path_save_base}{id}");
				StreamWriter file_save = new (get_path_save());

				// Convenience functions for writing to the file.
				void SaveVal(string key, string val) {
					file_save.WriteLine(key + delim_key + val);
				}
				void SaveVals(string key, List<string> vals) {
					string val = "";
					foreach (string entry in vals)
						{ val += entry + delim_entry; }
					// trim the trailing delimiter
					if (val.EndsWith(delim_entry))
						{ val = val[..^delim_entry.Length]; }
					SaveVal(key, val);
				}

				SaveVal(key_log_stats, do_log_stats.ToString());
				SaveVal(key_token_L, token_L);
				SaveVal(key_token_R, token_R);
				SaveVal(key_split, split);

				// `null` is a special case that is easily disambiguated on read,
				// since otherwise a `ulong` will only have digits after conversion.
				string str_ch_bot = ch_bot?.ToString() ?? str_null;
				SaveVal(key_ch_bot, str_ch_bot);

				List<string> vals_whitelist = new ();
				foreach (ulong ch in ch_whitelist)
					{ vals_whitelist.Add(ch.ToString()); }
				SaveVals(key_ch_whitelist, vals_whitelist);

				List<string> vals_blacklist = new ();
				foreach (ulong ch in ch_blacklist)
					{ vals_blacklist.Add(ch.ToString()); }
				SaveVals(key_ch_blacklist, vals_blacklist);

				// Flush/finalize the save file.
				file_save.Close();

			} catch {
				Console.WriteLine("Could not create save file.");
				Console.WriteLine("> " + get_path_save());
			}
		}

		public static Settings load(ulong id) {
			Settings settings = new (id);
			StreamReader file_save = new (settings.get_path_save());

			// Read in the file line-by-line.
			while (!file_save.EndOfStream) {
				// not at EndOfStream, line must be non-null
				string line = file_save.ReadLine()!;
				string[] line_split = line.Split(delim_key, 2);
				string key = line_split[0];
				string val = line_split[1];

				try {
					switch (key) {
					case key_log_stats:
						settings._do_log_stats = Convert.ToBoolean(val);
						break;
					case key_token_L:
						settings._token_L = val;
						break;
					case key_token_R:
						settings._token_R = val;
						break;
					case key_split:
						settings._split = val;
						break;

					case key_ch_bot:
						if (val == str_null)
							{ settings._ch_bot = null; }
						else
							{ settings._ch_bot = Convert.ToUInt64(val); }
						break;

					case key_ch_whitelist:
						string[] vals_whitelist = val.Split(delim_entry);
						if (vals_whitelist[0] != "") {
							foreach (string entry in vals_whitelist) {
								settings._ch_whitelist.Add(Convert.ToUInt64(entry));
							}
						}
						break;
					case key_ch_blacklist:
						string[] vals_blacklist = val.Split(delim_entry);
						if (vals_blacklist[0] != "") {
							foreach (string entry in vals_blacklist) {
								settings._ch_blacklist.Add(Convert.ToUInt64(entry));
							}
						}
						break;
					}
				} catch (FormatException) {
					Console.WriteLine($"Could not convert key value: {val}");
				} catch (OverflowException) {
					Console.WriteLine($"Could not convert key value: {val}");
				}
			}

			file_save.Close();
			return settings;
		}
	}
}
