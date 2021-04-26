using DSharpPlus.Entities;

using System.Collections.Generic;
using System.IO;

namespace Polybius.Engines {
	class EasterEggEngine : IEngine {
		private const string path_db = @"db/easter_eggs.txt";
		private const string delim = @"=";

		public static List<SearchResult> search(Program.QueryMetaPair token) {
			List<SearchResult> results = new ();

			StreamReader db = new (path_db);
			while (!db.EndOfStream) {
				string line = db.ReadLine();
				string[] split = line.Split(delim, 2);
				string name = split[0], data = split[1];
				if (name == token.query) {
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

			return results;
		}

		public class EasterEggSearchResult : SearchResult {
			public override DiscordMessageBuilder get_display() {
				return new DiscordMessageBuilder().WithContent(data);
			}
		}
	}
}
