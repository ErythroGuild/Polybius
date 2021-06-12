using System.Collections.Generic;
using System.IO;

using DSharpPlus.Entities;

namespace Polybius.Engines {
	class EasterEggEngine : IEngine {
		const string path_db = @"db/easter_eggs.txt";
		const string delim = @"=";

		public static List<SearchResult> search(SearchToken token) {
			Program.log.info("  Searching easter eggs...");
			List<SearchResult> results = new ();

			StreamReader db = new (path_db);
			while (!db.EndOfStream) {
				string line = db.ReadLine() ?? "";
				string[] split = line.Split(delim, 2);
				string name = split[0], data = split[1];
				data = decode_newlines(data);
				if (name == token.text) {
					Program.log.info("    Easter egg result found.");
					results.Add(new EasterEggSearchResult {
						is_exact_match = true,
						similarity = 1.0F,
						name = name,
						data = data
					});
					break;
				}
			}
			db.Close();

			Program.log.debug("  Easter eggs searched.");
			return results;
		}

		static string decode_newlines(string str) {
			return str.Replace(@"\n", "\n");
		}

		public class EasterEggSearchResult : SearchResult {
			public override DiscordMessageBuilder get_display() {
				return new DiscordMessageBuilder().WithContent(data);
			}
		}
	}
}
