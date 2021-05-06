using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;
using HtmlAgilityPack;

using Type = Polybius.Engines.WowheadEngine.WowheadSearchResult.Type;

namespace Polybius.Engines {
	class WowheadEngine : IEngine {
		private static HtmlWeb http = new ();

		private const string url_search = @"https://www.wowhead.com/search?q=";

		public static List<SearchResult> search(Program.QueryMetaPair token) {
			HtmlDocument doc_html = http.Load(url_search + token.query);
			HtmlNode doc = doc_html.DocumentNode;

			// If we were immediately redirected to a non-search page,
			// this means Wowhead found a sole, exact match.
			// Check for and parse this scenario.
			string url = http.ResponseUri.ToString();
			string url_search_frag = @"wowhead.com/search?q=";
			if (!url.Contains(url_search_frag)) {
				return result_from_redirect(doc, url);
			}

			string xpath_data =
				@"//div[@id='search-listview']" +
				@"/following-sibling::script";
			HtmlNode node_data = doc.SelectSingleNode(xpath_data);
			string data = node_data.InnerText;

			List<SearchResult> results = new ();

			Regex regex_tabs = new (
				@"new Listview\((?<tab>.*)\);",
				RegexOptions.Compiled );
			MatchCollection matches_tabs = regex_tabs.Matches(data);

			List<string> tabs = new ();
			foreach (Match match_tab in matches_tabs) {
				string tab = match_tab.Groups["tab"].Value;

				Regex regex_id_entries = new (
					@"id: '(?<id>.+?)'.+data: \[(?<entries>.+)\]",
					RegexOptions.Compiled);
				Match match_id_entries = regex_id_entries.Match(tab);
				string id = match_id_entries.Groups["id"].Value;
				string entries = match_id_entries.Groups["entries"].Value;

				Dictionary<string, Type> id_to_type = new () {
					{ "abilities"         , Type.Spell         },
					{ "specializations"   , Type.Spell         },
					{ "covenant-abilities", Type.CovenantSpell },
					
					{ "talents"    , Type.Talent    },
					{ "pvp-talents", Type.PvpTalent },

					{ "runecarving-powers", Type.Memory         },
					{ "soulbind-conduits" , Type.Conduit        },
					{ "soulbind-abilities", Type.SoulbindTalent },
					{ "anima-powers"      , Type.AnimaPower     },
					{ "azerite-essence"   , Type.Essence        },

					{ "affixes", Type.Affix },
					{ "mounts" , Type.Mount },

					{ "battle-pets"         , Type.BattlePet      },
					{ "battle-pet-abilities", Type.BattlePetSpell },

					{ "items"       , Type.Item        },
					{ "achievements", Type.Achievement },
					{ "quests"      , Type.Quest       },
					{ "currencies"  , Type.Currency    },
					{ "factions"    , Type.Faction     },
					{ "titles"      , Type.Title       },
					{ "professions" , Type.Profession  },
				};

				if (!id_to_type.ContainsKey(id)) {
					continue;
				}
				Type type = id_to_type[id];
				Regex regex_entries = new (@"{(?<entry>.+?)},?", RegexOptions.Compiled);
				MatchCollection matches_entries = regex_entries.Matches(entries);
				List<string> entries_str = new ();
				foreach (Match match_entry in matches_entries) {
					entries_str.Add(match_entry.Groups["entry"].Value);
				}
				results.AddRange(get_results(type, entries_str, token));
			}

			return results;
		}

		private static List<SearchResult> get_results(Type type, List<string> entries, Program.QueryMetaPair token) {
			List<SearchResult> results = new ();

			switch (type) {
			case Type.Spell:
			case Type.CovenantSpell:
				Regex regex_spell = new (@"""id"":(?<id>\d+).*""name"":""(?<name>.+?)""", RegexOptions.Compiled);
				foreach (string entry in entries) {
					Match match_spell = regex_spell.Match(entry);
					string name = match_spell.Groups["name"].Value;
					if (name.ToLower() == token.query.ToLower()) {
						string id = match_spell.Groups["id"].Value;
						string url = $@"https://www.wowhead.com/spell={id}";
						results.Add(new WowheadSearchResult() {
							is_exact_match = true,
							similarity = 1.0F,
							name = name,
							data = url,
							type = type
						});
					}
				}
				break;
			}

			return results;
		}

		// Parse a page (redirected immediately from the search page)
		// into a `WowheadSearchResult`.
		private static List<SearchResult> result_from_redirect(HtmlNode doc, string url) {
			// Find and parse the `g_pageInfo` string.
			string pageinfo = parse_pageinfo(doc);
			Regex regex = new(
				@"""type"":(?<type>\d+),""typeId"":\d+,""name"":""(?<name>.+)""",
				RegexOptions.Compiled);
			GroupCollection match = regex.Match(pageinfo).Groups;
			int type_int = Convert.ToInt32(match["type"].Value);
			string name = match["name"].Value;

			// Do not return any results if the result type isn't one
			// of the explicitly supported ones.
			Type? type = parse_type(type_int, doc);
			if (type is null) {
				return new List<SearchResult>();
			}

			// Construct and return a list of results consisting of
			// the sole matching result.
			WowheadSearchResult result = new() {
				is_exact_match = true,
				similarity = 1.0F,
				name = name,
				data = url,
				type = (Type)type,
			};
			return new List<SearchResult>() { result };
		}

		// Extract the `var g_pageInfo` variable from the <script> CDATA
		// embedded within the HTML document.
		private static string parse_pageinfo(HtmlNode doc) {
			string xpath_data =
				@"//div[@id='infobox-original-position']" +
				@"/following-sibling::script";
			HtmlNodeCollection nodes_data = doc.SelectNodes(xpath_data);

			Regex regex_pageinfo = new(
				@"g_pageInfo = {(?<data>.+)};",
				RegexOptions.Compiled);
			foreach (HtmlNode node in nodes_data) {
				string data = node.InnerText;
				if (regex_pageinfo.IsMatch(data)) {
					return regex_pageinfo.Match(data).Groups["data"].Value;
				}
			}

			return null;
		}

		// Use the `g_pageInfo.type` value and pattern matching of the
		// page itself to infer the `WowheadSearchResult.Type` of the page.
		// Returns `null` if the inferred type isn't a supported type.
		private static Type? parse_type(int type_int, HtmlNode doc) {
			// From basic.js, `WH.Types` definition.
			Dictionary<int, Type> dict = new() {
				{ 6, Type.Spell },
				//this.NPC = 1;
				//this.OBJECT = 2;
				//this.ITEM = 3;
				//this.ITEM_SET = 4;
				//this.QUEST = 5;
				//this.SPELL = 6;
				//this.ZONE = 7;
				//this.FACTION = 8;
				//this.PET = 9;
				//this.ACHIEVEMENT = 10;
				//this.TITLE = 11;
				//this.EVENT = 12;
				//this.CLASS = 13;
				//this.RACE = 14;
				//this.SKILL = 15;
				//this.CURRENCY = 17;
				//this.PROJECT = 18;
				//this.SOUND = 19;
				//this.BUILDING = 20;
				//this.FOLLOWER = 21;
				//this.MISSION_ABILITY = 22;
				//this.MISSION = 23;
				//this.SHIP = 25;
				//this.THREAT = 26;
				//this.RESOURCE = 27;
				//this.CHAMPION = 28;
				//this.ICON = 29;
				//this.ORDER_ADVANCEMENT = 30;
				//this.FOLLOWER_A = 31;
				//this.FOLLOWER_H = 32;
				//this.SHIP_A = 33;
				//this.SHIP_H = 34;
				//this.CHAMPION_A = 35;
				//this.CHAMPION_H = 36;
				//this.TRANSMOG_ITEM = 37;
				//this.BFA_CHAMPION = 38;
				//this.BFA_CHAMPION_A = 39;
				//this.AFFIX = 40;
				//this.BFA_CHAMPION_H = 41;
				//this.AZERITE_ESSENCE_POWER = 42;
				//this.AZERITE_ESSENCE = 43;
				//this.STORYLINE = 44;
				//this.ADVENTURE_COMBATANT_ABILITY = 46;
				//this.ENCOUNTER = 47;
				//this.COVENANT = 48;
				//this.SOULBIND = 49;
				//this.PET_ABILITY = 200;
				//this.SCREENSHOT = 91;
				//this.GUIDE_IMAGE = 98;
				//this.GUIDE = 100;
				//this.TRANSMOG_SET = 101;
				//this.OUTFIT = 110;
				//this.GEAR_SET = 111;
				//this.LISTVIEW = 158;
				//this.SURVEY_COVENANTS = 161;
				//this.NEWS_POST = 162;
			};

			Type? type;
			if (!dict.ContainsKey(type_int)) {
				return null;
			} else {
				type = dict[type_int];
			}

			// Further disambiguate the types of results based on the HTML
			// document itself.
			string tooltip = get_tooltip(doc);
			switch (type) {
			case Type.Spell:
				if (tooltip.Contains("Covenant Ability")) {
					type = Type.CovenantSpell;
					break;
				}
				break;
			}

			return type;
		}

		// Returns the tooltip data that is processed into HTML.
		// This is javascript, so it contains backslash escapes.
		private static string get_tooltip(HtmlNode doc) {
			string data = get_tooltip_raw(doc);

			// Only one entry should have tooltip data associated
			// (the one corresponding to the current page).
			Regex regex_tooltip = new (
				@"tooltip_enus = ""(?<tooltip>.+?)"";",
				RegexOptions.Compiled);
			return regex_tooltip.Match(data).Groups["tooltip"].Value;
		}

		// Returns the game icon to the left of the tooltip.
		private static string get_icon(HtmlNode doc, string id) {
			string data = get_tooltip_raw(doc);

			Regex regex = new (
				$@"""{id}"".*?""icon"":""(?<url>.+?)""",
				RegexOptions.Compiled);
			string name = regex.Match(data).Groups["url"].Value;

			return $@"https://wow.zamimg.com/images/wow/icons/large/{name}.jpg";
		}

		// Returns the inner text of the entire <script> tag enclosing
		// the tooltip data itself.
		private static string get_tooltip_raw(HtmlNode doc) {
			string xpath_data =
				@"//div[@id='main-contents']" +
				@"/div[@class='text']" +
				@"/script";
			HtmlNode node_data = doc.SelectSingleNode(xpath_data);
			return node_data.InnerText;
		}

		public class WowheadSearchResult : SearchResult {
			public enum Type {
				Spell, CovenantSpell,
				Talent, PvpTalent,
				Memory, Conduit, SoulbindTalent, AnimaPower,
				Essence,
				Affix,
				Mount,
				BattlePet, BattlePetSpell,
				Item,
				Achievement,
				Quest,
				Currency,
				Faction,
				Title,
				Profession,
			};

			public Type type;

			public override DiscordMessageBuilder get_display() {
				DiscordEmbed embed = new DiscordEmbedBuilder()
					.WithTitle(name)
					.WithColor(Program.color_embed)
					.WithFooter("powered by Wowhead", @"https://wow.zamimg.com/images/logos/favicon-standard.png");

				string url_icon, tooltip;
				switch (type) {
				case Type.Spell:
					HtmlDocument doc_html = http.Load(data);
					HtmlNode doc = doc_html.DocumentNode;

					string id = Regex.Match(data, @"spell=(?<id>\d+)", RegexOptions.Compiled).Groups["id"].Value;

					url_icon = get_icon(doc, id);
					tooltip = get_tooltip(doc);

					Regex regex_tooltip_text = new Regex(@"<div class=\\""q\d?\\"">(?<text>.*)<\\\/div>", RegexOptions.Compiled);
					string tooltip_text = "";
					MatchCollection tooltip_parts = regex_tooltip_text.Matches(tooltip);
					foreach (Match match in tooltip_parts) {
						tooltip_text += match.Groups["text"].Value;
						tooltip_text += "\n";
					}
					tooltip_text = tooltip_text.Replace(@"<br \/>", "\n");
					tooltip_text = Regex.Replace(tooltip_text, @"<(?:\\\/)?span(?:.*?)>", "", RegexOptions.Compiled);
					tooltip_text += $"\n*Read more: [Wowhead comments]({data}#comments)*";

					embed = new DiscordEmbedBuilder(embed)
						.WithUrl(data)
						.WithThumbnail(url_icon)
						.WithDescription(tooltip_text);
					break;
				}

				return new DiscordMessageBuilder().WithEmbed(embed);
			}
		}
	}
}
