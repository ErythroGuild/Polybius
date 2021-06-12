using System.Collections.Generic;

namespace Polybius {
	class SearchToken {
		public enum Type {
			Unspecified,
			Wowhead, Petopia, Wowpedia, Undermine, RaiderIO,
			Minecraft,
			Zelda,
			Pokemon,
			Wikipedia,
		}

		public const string delim = "-";
		static readonly Dictionary<string, Type> dict_types = new () {
			{ "wh"       , Type.Wowhead   },
			{ "wowhead"  , Type.Wowhead   },
			{ "pet"      , Type.Petopia   },
			{ "petopia"  , Type.Petopia   },
			{ "wp"       , Type.Wowpedia  },
			{ "wowpedia" , Type.Wowpedia  },
			{ "uj"       , Type.Undermine },
			{ "undermine", Type.Undermine },
			{ "underminejournal", Type.Undermine },
			{ "rio"      , Type.RaiderIO  },
			{ "raiderio" , Type.RaiderIO  },

			{ "mc"       , Type.Minecraft },
			{ "minecraft", Type.Minecraft },

			{ "z"    , Type.Zelda },
			{ "zelda", Type.Zelda },

			{ "poke"      , Type.Pokemon },
			{ "pokemon"   , Type.Pokemon },
			{ "bulbapedia", Type.Pokemon },

			{ "wiki"     , Type.Wikipedia },
			{ "wikipedia", Type.Wikipedia },
		};

		public Type type;
		public string text;
		public string? meta;

		// Initialize a search token from a standard query.
		public SearchToken(string text, string meta="") {
			// assign text
			this.text = text;

			// assign type
			string type_str = "";
			if (meta.Contains(delim)) {
				string[] split = meta.Split(delim, 2);
				type_str = split[0];
				meta = split[1];
			} else {
				type_str = meta;
				meta = "";
			}
			type = get_type(type_str);

			// assign meta
			if (meta == "")
				{ this.meta = null; }
			else
				{ this.meta = meta; }
		}

		// A translation function for the type of the query.
		// `public` to allow access to the list of identifiers.
		public static Type get_type(string key) {
			key = key.ToLower();
			if (!dict_types.ContainsKey(key)) {
				return Type.Unspecified;
			} else {
				return dict_types[key];
			}
		}
	}
}
