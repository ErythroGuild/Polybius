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
				string url_thumbnail;

				switch (type) {
				case Type.Spell:
					HtmlDocument doc_html = http.Load(data);
					HtmlNode doc = doc_html.DocumentNode;

					string id = Regex.Match(data, @"spell=(?<id>\d+)", RegexOptions.Compiled).Groups["id"].Value;

					string xpath_tooltip =
						@"//div[@id='main-contents']" +
						@"/div[@class='text']" +
						@"/script";
					HtmlNode node_tooltip = doc.SelectSingleNode(xpath_tooltip);
					string tooltip_raw = node_tooltip.InnerText;

					Regex regex_thumb_url = new ($@"""{id}"".*?""icon"":""(?<url>.+?)""", RegexOptions.Compiled);
					string thumb_url = regex_thumb_url.Match(tooltip_raw).Groups["url"].Value;
					url_thumbnail = $@"https://wow.zamimg.com/images/wow/icons/large/{thumb_url}.jpg";

					Regex regex_tooltip = new Regex(@"tooltip_enus = ""(?<tooltip>.+?)"";", RegexOptions.Compiled);
					string tooltip_str = regex_tooltip.Match(tooltip_raw).Groups["tooltip"].Value;

					Regex regex_tooltip_text = new Regex(@"<div class=\\""q\d?\\"">(?<text>.*)<\\\/div>", RegexOptions.Compiled);
					string tooltip_text = "";
					MatchCollection tooltip_parts = regex_tooltip_text.Matches(tooltip_str);
					foreach (Match match in tooltip_parts) {
						tooltip_text += match.Groups["text"].Value;
						tooltip_text += "\n";
					}
					tooltip_text = tooltip_text.Replace(@"<br \/>", "\n");
					tooltip_text = Regex.Replace(tooltip_text, @"<(?:\\\/)?span(?:.*?)>", "", RegexOptions.Compiled);
					tooltip_text += $"\n*Read more: [Wowhead comments]({data}#comments)*";

					embed = new DiscordEmbedBuilder(embed)
						.WithUrl(data)
						.WithThumbnail(url_thumbnail)
						.WithDescription(tooltip_text);
					break;
				}

				return new DiscordMessageBuilder().WithEmbed(embed);
			}
		}
	}
}
