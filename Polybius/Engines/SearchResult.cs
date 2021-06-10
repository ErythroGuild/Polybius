using DSharpPlus.Entities;

namespace Polybius.Engines {
	abstract class SearchResult {
		public bool is_exact_match;
		public float similarity;
		public string name = "";
		public string data = "";

		public abstract DiscordMessageBuilder get_display();
	}
}
