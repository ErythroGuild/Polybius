using DSharpPlus;
using DSharpPlus.Entities;

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Polybius {
	class Settings {
		public const string group_query = "query";
		public const string group_meta = "meta";
		private const string dir = "config/";

		public ulong id;
		public bool do_log_stats;
		public string token_L;
		public string token_R;
		public string split;
		public ulong? ch_bot;
		public HashSet<ulong> ch_whitelist;
		public HashSet<ulong> ch_blacklist;

		public Settings(ulong id) {
			this.id = id;
			do_log_stats = true;
			token_L = "[["; split = "|"; token_R = "]]";
			ch_bot = null;
			ch_whitelist = new HashSet<ulong>();
			ch_blacklist = new HashSet<ulong>();
		}

		public Regex regex_token() {
			// e.g.:
			// \Q[[\E(?<query>.+?)(?:\Q|\E(?<meta>.+?))?\Q]]\E
			string regex_str =
				@"\Q" + token_L + @"\E" +
				@"(?<"+ group_query + @">.+?)" +
				@"(?:\Q" + split + @"\E" +
				@"(?<" + group_meta + @">.+?))?" +
				@"\Q" + token_R + @"\E";
			return new Regex(regex_str,
				RegexOptions.Compiled | RegexOptions.IgnoreCase);
		}

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
	}
}
