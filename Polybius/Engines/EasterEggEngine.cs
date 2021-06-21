using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using DSharpPlus.Entities;

namespace Polybius.Engines {
	class EasterEggEngine : IEngine {
		readonly static Stopwatch stopwatch = new ();

		const string path_db = @"db/easter_eggs.txt";
		const string delim = @"=";

		public static List<SearchResult> search(SearchToken token) {
			stopwatch.Restart();
			Program.log.info("  Searching easter eggs...");
			List<SearchResult> results = new ();

			StreamReader db = new (path_db);
			while (!db.EndOfStream) {
				string line = db.ReadLine() ?? "";
				string[] split = line.Split(delim, 2);
				string name = split[0], data = split[1];
				if (name == token.text) {
					Program.log.info("    Easter egg result found.");
					data = decode_newlines(data);
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

			stopwatch.Stop();
			Program.log.debug("  Easter eggs searched.");
			Program.log.debug($"    Took {stopwatch.ElapsedMilliseconds} msec.");
			return results;
		}

		static string decode_newlines(string str) {
			Dictionary<string, string> dict = new () {
				{ @"\n"   , "\n"     },	// newline
				{ @"\zwsp", "\u200B" },	// zero-width space
				{ @"\qMsp", "\u2005" },	// 4-per-em space (quarter-em)
				{ @"\nbsp", "\u00A0" },	// no-break space
			};
			foreach (string key in dict.Keys) {
				str = str.Replace(key, dict[key]);
			}
			return str;
		}

		public class EasterEggSearchResult : SearchResult {
			public override DiscordMessageBuilder get_display() {
				return new DiscordMessageBuilder().WithContent(data);
			}
		}
	}
}
