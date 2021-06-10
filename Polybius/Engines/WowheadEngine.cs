using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;
using HtmlAgilityPack;

namespace Polybius.Engines {
	using Type = WowheadEngine.WowheadSearchResult.Type;

	class WowheadEngine : IEngine {
		static readonly HtmlWeb http = new ();

		const string url_search = @"https://www.wowhead.com/search?q=";
		const int embed_color = 0xA71A19;

		public static List<SearchResult> search(Program.QueryMetaPair token) {
			HtmlDocument doc = http.Load(url_search + token.query);
			HtmlNode page = doc.DocumentNode;

			// If we were immediately redirected to a non-search page,
			// this means Wowhead found a sole, exact match.
			// Check for and parse this scenario.
			string url = http.ResponseUri.ToString();
			string url_search_frag = @"wowhead.com/search?q=";
			if (!url.Contains(url_search_frag)) {
				return result_from_redirect(page, url);
			}

			// Extract the <script> node containing the search data.
			string xpath_data =
				@"//div[@id='search-listview']" +
				@"/following-sibling::script";
			HtmlNode node_data = page.SelectSingleNode(xpath_data);
			if (node_data is null) {
				return new List<SearchResult>();
			}
			string data = node_data.InnerText;

			// Extract all the tabs and parse through their entries,
			// collating the results into a list.
			List<string> tabs = parse_tab_strings(data);
			List<SearchResult> results = new ();
			foreach (string tab in tabs) {
				// Identify the type of results contained by the tab.
				// Discard the data if the results aren't supported.
				Regex regex_tab_id = new (@"id: '(?<id>.+?)'");
				string tab_id = regex_tab_id.Match(tab).Groups["id"].Value;

				Type? type = get_tab_type(tab_id);
				if (type is null) {
					continue;
				}

				// Extract individual entries from the tab's data, parse
				// through, and add to the total list of entries.
				Regex regex_tab_data = new (@"data: \[(?<data>.+)\]");
				string tab_data = regex_tab_data.Match(tab).Groups["data"].Value;

				List<string> entries = parse_tab_entries(tab_data);
				List<SearchResult> tab_results = parse_results((Type)type, entries, token);
				results.AddRange(tab_results);
			}

			return results;
		}

		// Parse a page (redirected immediately from the search page)
		// into a `WowheadSearchResult`.
		static List<SearchResult> result_from_redirect(HtmlNode doc, string url) {
			// Find and parse the `g_pageInfo` string.
			string? pageinfo = parse_pageinfo(doc);
			if (pageinfo is null) {
				return new List<SearchResult>();
			}
			Regex regex_id = new (@"""typeId"":(?<id>\d+),");
			Regex regex_name = new (@"""name"":""(?<name>.+)""");
			string id = regex_id.Match(pageinfo).Groups["id"].Value;
			string name = regex_name.Match(pageinfo).Groups["name"].Value;

			// Do not return any results if the result type isn't one
			// of the explicitly supported ones.
			Type? type = type_from_breadcrumb(doc);
			if (type is null) {
				return new List<SearchResult>();
			}

			// Construct and return a list of results consisting of
			// the sole matching result.
			WowheadSearchResult result = new () {
				is_exact_match = true,
				similarity = 1.0F,
				name = name,
				data = url,
				type = (Type)type,
				id = id
			};
			return new List<SearchResult>() { result };
		}

		// Extract the `var g_pageInfo` variable from the <script> CDATA
		// embedded within the HTML document.
		static string? parse_pageinfo(HtmlNode page) {
			string xpath_data =
				@"//div[@id='infobox-original-position']" +
				@"/following-sibling::script";
			HtmlNodeCollection nodes_data = page.SelectNodes(xpath_data);

			// Go through all <script> nodes until we find one that sets
			// the `g_pageInfo` variable.
			Regex regex_pageinfo = new (@"g_pageInfo = {(?<data>.+)};");
			foreach (HtmlNode node in nodes_data) {
				string data = node.InnerText;
				if (regex_pageinfo.IsMatch(data)) {
					return regex_pageinfo.Match(data).Groups["data"].Value;
				}
			}

			return null;
		}

		// Use the breadcrumb string to classify the page's Type.
		static Type? type_from_breadcrumb(HtmlNode page) {
			string xpath_data =
				@"//div[@id='infobox-original-position']" +
				@"/following-sibling::script";
			HtmlNodeCollection nodes_data = page.SelectNodes(xpath_data);

			// Go through all <script> nodes until we find one that sets
			// the `g_pageInfo` variable, then extract the breadcrumb.
			Regex regex_pageinfo = new (@"g_pageInfo = {(?<data>.+)};");
			Regex regex_breadcrumb = new (@"breadcrumb: \[(?<data>[\w\-"", ]+)\]");
			string breadcrumb = "";
			foreach (HtmlNode node in nodes_data) {
				string data = node.InnerText;
				if (regex_pageinfo.IsMatch(data)) {
					breadcrumb = regex_breadcrumb.Match(data).Groups["data"].Value;
					break;
				}
			}

			// Format breadcrumb in a easily parseable context.
			breadcrumb = breadcrumb.Replace(" ", "");
			if (!breadcrumb.EndsWith(",")) {
				breadcrumb = $"{breadcrumb},";
			}

			// Check the start of the breadcrumb and classify the page.
			return breadcrumb switch {
				var s when s.StartsWith("0,1,-4,")  => Type.Spell,   // racial
				var s when s.StartsWith("0,1,7,")   => Type.Spell,   // general
				var s when s.StartsWith("0,1,-12,") => Type.Spell,   // spec
				var s when s.StartsWith("0,1,-3,")  => Type.Spell,   // pet
				var s when s.StartsWith("0,1,-20,") => Type.CovenantSpell,
				var s when s.StartsWith("0,1,-2,")  => Type.Talent,
				var s when s.StartsWith("0,1,-16,") => Type.PvpTalent,

				var s when s.StartsWith("0,1,-22,") => Type.Memory,
				var s when s.StartsWith("0,1,-24,") => Type.Conduit,
				var s when s.StartsWith("0,1,-23,") => Type.SoulbindTalent,
				var s when s.StartsWith("0,1,-21,") => Type.AnimaPower,

				var s when s.StartsWith("0,\"azerite-essences\",") => Type.Essence,
				var s when s.StartsWith("0,1,-18,")                => Type.AzeriteTrait,

				var s when s.StartsWith("0,40,")   => Type.Affix,
				var s when s.StartsWith("0,1,-5,") => Type.Mount,

				var s when s.StartsWith("0,17,2,") => Type.BattlePet,
				var s when s.StartsWith("0,17,1,") => Type.BattlePetSpell,

				var s when s.StartsWith("0,0,")    => Type.Item,
				var s when s.StartsWith("0,9,")    => Type.Achievement,
				var s when s.StartsWith("0,3,")    => Type.Quest,
				var s when s.StartsWith("0,15,")   => Type.Currency,
				var s when s.StartsWith("0,7,")    => Type.Faction,
				var s when s.StartsWith("0,10,")   => Type.Title,
				var s when s.StartsWith("0,1,11,") => Type.Profession,  // primary
				var s when s.StartsWith("0,1,9,")  => Type.Profession,	// secondary

				_ => null,
			};
		}

		// Returns the tooltip data that is processed into HTML.
		// This is javascript, so it contains backslash escapes.
		static string? get_tooltip(HtmlNode page) {
			StringReader data = new (get_tooltip_raw(page));
			Regex regex_tooltip = new (@"g_\w+\[\d+\]\.tooltip_enus = ""(?<data>.+)"";");

			// Only one entry should have tooltip data associated
			// (the one corresponding to the current page).
			while (data.Peek() != -1) {
				string line = data.ReadLine() ?? "";
				Match match = regex_tooltip.Match(line);
				if (match != Match.Empty) {
					return match.Groups[1].Value;
				}
			}

			// If no tooltip was found, return null.
			return null;
		}
		
		// Returns the inner text of the entire <script> tag enclosing
		// the tooltip data itself.
		static string get_tooltip_raw(HtmlNode page) {
			string xpath_data =
				@"//div[@id='main-contents']" +
				@"/div[@class='text']" +
				@"/script";
			HtmlNode node_data = page.SelectSingleNode(xpath_data);
			return node_data.InnerText;
		}

		// Replace escaped quotes and backslashes with their actual
		// representations.
		static string javascript_to_html(string input) {
			input = input.Replace(@"\""", "\"");
			input = input.Replace(@"\/", "/");
			return input;
		}

		// Decode HTML entities into their display counterparts.
		static string html_decode(string input) {
			Dictionary<string, string> dict_entities = new () {
				{ @"&nbsp;" , " "      },
				{ @"&quot;" , "\""     },
				{ @"&ldquo;", "\u201C" },
				{ @"&rdquo;", "\u201D" },
				{ @"&apos;" , "'"      },
				{ @"&lsquo;", "\u2018" },
				{ @"&rsquo;", "\u2019" },
				{ @"&lt;"   , "<"      },
				{ @"&gt;"   , ">"      },
				{ @"&amp;"  , "&"      },
				{ @"&ndash;", "\u2013" },
				{ @"&mdash;", "\u2014" },
				{ @"&trade;", "\u2122" },
			};

			foreach (string entity in dict_entities.Keys) {
				input = input.Replace(entity, dict_entities[entity]);
			}

			return input;
		}

		// Parse <script> data into a list of search result tabs
		// (still formatted as javascript).
		static List<string> parse_tab_strings(string data) {
			List<string> tabs = new ();
			Regex regex_tabs = new (@"new Listview\((?<tab>.*)\);");
			MatchCollection matches = regex_tabs.Matches(data);
			foreach (Match match in matches) {
				// Regex captures are found in a 1-based array,
				// [0] contains the match itself.
				tabs.Add(match.Groups["tab"].Value);
			}
			return tabs;
		}

		// Classify the `Type` of the tab from the tab-id.
		// Returns `null` if the tab type isn't supported.
		static Type? get_tab_type(string tab_id) {
			Dictionary<string, Type> dict = new () {
				{ "abilities"         , Type.Spell         },
				{ "specializations"   , Type.Spell         },
				{ "covenant-abilities", Type.CovenantSpell },

				{ "talents"    , Type.Talent    },
				{ "pvp-talents", Type.PvpTalent },

				{ "runecarving-powers", Type.Memory         },
				{ "soulbind-conduits" , Type.Conduit        },
				{ "soulbind-abilities", Type.SoulbindTalent },
				{ "anima-powers"      , Type.AnimaPower     },

				{ "azerite-essence", Type.Essence      },
				{ "azerite-traits" , Type.AzeriteTrait },

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

			if (!dict.ContainsKey(tab_id)) {
				return null;
			} else {
				return dict[tab_id];
			}
		}

		// Parses the list of entries in a tab into individual entries.
		static List<string> parse_tab_entries(string data) {
			// Split multiple elements onto different lines.
			data = data.Replace("},{", "}\n{");
			string[] data_split = data.Split('\n');

			// For each element, remove the opening and closing braces.
			for (int i=0; i<data_split.Length; ++i) {
				data_split[i] = data_split[i][1..^1];
			}
			return new List<string>(data_split);
		}

		// Takes an entire tab worth of results (and the category of the tab),
		// and returns a parsed list of (`Wowhead`)`SearchResult`s.
		static List<SearchResult> parse_results(Type type, List<string> tab, Program.QueryMetaPair token) {
			List<SearchResult> entries = new ();
			// Capture groups are accessed from a 1-based list,
			// [0] contains the match string itself.
			Regex regex_name = new (@"""name"":""(?<name>.+?)""");
			Regex regex_id = new (@"""id"":(?<id>\d+)");

			foreach (string entry in tab) {
				string name = regex_name.Match(entry).Groups["name"].Value;

				// Titles get special handling to trim the added "<Name>".
				if (type == Type.Title) {
					name = name.Replace("%s", "");
					char[] title_trim = { ' ', ',' };
					name = name.Trim(title_trim);
				}

				// Collate matches into a list.
				if (name.ToLower() == token.query.ToLower()) {
					string id = regex_id.Match(entry).Groups["id"].Value;
					string url = create_entry_url(type, id);
					entries.Add(new WowheadSearchResult() {
						is_exact_match = true,
						similarity = 1.0F,
						name = name,
						data = url,
						type = type,
						id = id
					});
				}
			}
			return entries;
		}

		// Create the link to the actual Wowhead entry page, from
		// entries parsed from the result list.
		static string create_entry_url(Type type, string id) {
			return type switch {
				Type.Spell or
				Type.CovenantSpell or
				Type.Talent or
				Type.PvpTalent or
				Type.Memory or
				Type.Conduit or
				Type.SoulbindTalent or
				Type.AnimaPower or
				Type.AzeriteTrait or
				Type.Mount or
				Type.Profession => $@"https://www.wowhead.com/spell={id}",
				Type.Essence => $@"https://www.wowhead.com/azerite-essence/{id}",
				Type.Affix => $@"https://www.wowhead.com/affix={id}",
				Type.BattlePet => $@"https://www.wowhead.com/npc={id}",
				Type.BattlePetSpell => $@"https://www.wowhead.com/pet-ability={id}",
				Type.Item => $@"https://www.wowhead.com/item={id}",
				Type.Achievement => $@"https://www.wowhead.com/achievement={id}",
				Type.Quest => $@"https://www.wowhead.com/quest={id}",
				Type.Currency => $@"https://www.wowhead.com/currency={id}",
				Type.Faction => $@"https://www.wowhead.com/faction={id}",
				Type.Title => $@"https://www.wowhead.com/title={id}",
			};
		}

		// Return the name of the spell (as shown in the header) at
		// the provided link.
		static string get_spell_name(string url) {
			HtmlDocument doc = http.Load(url);
			HtmlNode page = doc.DocumentNode;

			string xpath_title =
				@"//div[@id='main-contents']" +
				@"/div[@class='text']" +
				@"/h1";
			HtmlNode node_title = page.SelectSingleNode(xpath_title);

			return node_title.InnerText;
		}


		public class WowheadSearchResult : SearchResult {
			public enum Type {
				Spell, CovenantSpell,
				Talent, PvpTalent,
				Memory, Conduit, SoulbindTalent, AnimaPower,
				Essence, AzeriteTrait,
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
			public string id = "";

			public override DiscordMessageBuilder get_display() {
				DiscordEmbed embed = new DiscordEmbedBuilder()
					.WithColor(embed_color)
					.WithTitle(name)
					.WithUrl(data)
					.WithFooter("powered by Wowhead", @"https://wow.zamimg.com/images/logos/favicon-standard.png");

				// Load data url into a document for later reuse.
				HtmlDocument doc = http.Load(data);
				HtmlNode page = doc.DocumentNode;

				// Parse the icon and add it to the embed if it exists.
				string? url_icon = get_icon(page);
				if (url_icon is not null) {
					embed = new DiscordEmbedBuilder(embed)
						.WithThumbnail(url_icon);
				}

				StringWriter writer = new ();
				Dictionary<Type, Func<HtmlNode, string>> tooltips = new () {
					{ Type.Spell        , text_spell },
					{ Type.CovenantSpell, text_spell },
					{ Type.Talent       , text_spell },
					{ Type.PvpTalent    , text_spell },

					{ Type.Memory        , text_spell },
					{ Type.Conduit       , text_spell },
					{ Type.SoulbindTalent, text_spell },
					{ Type.AnimaPower    , text_spell },

					{ Type.Essence     , text_essence },
					{ Type.AzeriteTrait, text_spell   },

					{ Type.Affix, text_affix },
					{ Type.Mount, text_spell },

					{ Type.BattlePet     , text_battlepet },
					{ Type.BattlePetSpell, text_spell     },

					{ Type.Item       , text_item        },
					{ Type.Achievement, text_achievement },
					{ Type.Quest      , text_quest       },
					{ Type.Currency   , text_currency    },
					{ Type.Faction    , text_faction     },
					{ Type.Title      , text_title       },
					{ Type.Profession , text_spell       },
				};

				// Fetch tooltip text from function delegates.
				string tooltip = tooltips[type](page);
				tooltip = html_decode(tooltip);
				writer.WriteLine(tooltip);
				writer.WriteLine();
				writer.WriteLine($"*More info: [Wowhead]({data}) \u2022 [comments]({data}#comments)*");
				writer.Flush();
				string description = writer.ToString();

				// Construct embed and pass it to caller.
				embed = new DiscordEmbedBuilder(embed)
					.WithDescription(description);
				return new DiscordMessageBuilder().WithEmbed(embed);
			}

			// Returns the thumbnail icon if one exists, or null otherwise.
			string? get_icon(HtmlNode page) {
				string xpath, data, name;
				Regex regex;
				HtmlNode node_data;

				switch (type) {
				case Type.Spell:
				case Type.CovenantSpell:
				case Type.Talent:
				case Type.PvpTalent:
				case Type.Memory:
				case Type.Conduit:
				case Type.SoulbindTalent:
				case Type.AnimaPower:
				case Type.AzeriteTrait:
				case Type.Mount:
				case Type.Profession:
					data = get_tooltip_raw(page);

					regex = new ($@"""{id}"".*?""icon"":""(?<name>.+?)""");
					name = regex.Match(data).Groups["name"].Value;

					return $@"https://wow.zamimg.com/images/wow/icons/large/{name}.jpg";
				case Type.Essence:
				case Type.Affix:
					xpath =
						@"//div[@id='h1titleicon']" +
						@"/following-sibling::script";
					node_data = page.SelectSingleNode(xpath);
					data = node_data.InnerText;

					regex = new (@"Icon\.create\(['""](?<name>\w+)['""]");
					name = regex.Match(data).Groups["name"].Value;

					return $@"https://wow.zamimg.com/images/wow/icons/large/{name}.jpg";
				case Type.BattlePet:
				case Type.Currency:
					xpath =
						@"//div[@id='main-contents']" +
						@"/div[@class='text']" +
						@"/script";
					node_data = page.SelectSingleNode(xpath);
					data = node_data.InnerText;

					regex = new (@"Icon\.create\(['""](?<name>\w+)['""]");
					name = regex.Match(data).Groups["name"].Value;

					return $@"https://wow.zamimg.com/images/wow/icons/large/{name}.jpg";
				case Type.BattlePetSpell:
				case Type.Item:
					xpath =
						@"//div[@id='main-contents']" +
						@"/div[@class='text']" +
						@"/script";
					node_data = page.SelectSingleNode(xpath);
					data = node_data.InnerText;

					regex = new (@"""icon"":""(?<name>\w+)""");
					name = regex.Match(data).Groups["name"].Value;

					return $@"https://wow.zamimg.com/images/wow/icons/large/{name}.jpg";
				case Type.Achievement:
					xpath =
						@"//div[@id='main-contents']" +
						@"/div[@class='text']" +
						@"/h1/following-sibling::script";
					node_data = page.SelectSingleNode(xpath);
					data = node_data.InnerText;

					regex = new (@"Icon\.create\(['""](?<name>\w+)['""]");
					name = regex.Match(data).Groups["name"].Value;

					return $@"https://wow.zamimg.com/images/wow/icons/large/{name}.jpg";
				default:
				case Type.Quest:
				case Type.Faction:
				case Type.Title:
					return null;
				}
			}

			string text_spell(HtmlNode page) {
				string? tooltip = get_tooltip(page);
				if (tooltip is null)
					{ return ""; }
				tooltip = javascript_to_html(tooltip);

				HtmlDocument dom = new ();
				dom.LoadHtml(tooltip);

				// Find the main text node, and explicitly add newlines.
				string xpath_text = @"/table[2]/tr/td";
				HtmlNode node_text = dom.DocumentNode.SelectSingleNode(xpath_text);
				HtmlNodeCollection nodes;

				// Replace <br> tags with newlines.
				nodes = node_text.SelectNodes(@"//br");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.ReplaceChild(dom.CreateTextNode("\n"), node);
					}
				}
				// Add newlines between top-level <div>s.
				nodes = node_text.SelectNodes(@"//div/following-sibling::div");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertBefore(dom.CreateTextNode("\n"), node);
					}
				}
				// Add units to "money" nodes.
				nodes = node_text.SelectNodes(@"//span[@class='moneygold']");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertAfter(dom.CreateTextNode("g "), node);
					}
				}
				nodes = node_text.SelectNodes(@"//span[@class='moneysilver']");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertAfter(dom.CreateTextNode("s "), node);
					}
				}
				nodes = node_text.SelectNodes(@"//span[@class='moneycopper']");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertAfter(dom.CreateTextNode("c "), node);
					}
				}
				tooltip = node_text.InnerText;
				
				// Remove excess newlines (no more than 2 consecutive).
				tooltip = Regex.Replace(tooltip, @"(?:\n){3,}", "\n\n");
				return tooltip.TrimEnd();
			}

			string text_essence(HtmlNode page) {
				string xpath_data =
					@"//div[@id='article-all']" +
					@"/following-sibling::script[2]";
				HtmlNode node_data = page.SelectSingleNode(xpath_data);

				// Extract the paragraph with spell info.
				string data = node_data.InnerText;
				string[] blocks = data.Split("[hr]");
				data = blocks[2];

				// Extract the blocks describing major/minor powers.
				data = data.Replace(@"\r\n", "\n");
				blocks = data.Split("\n\n");
				string text_major = blocks[2];
				string text_minor = blocks[3];

				// Extract the major/minor power spell links.
				string get_spell_link(string data) {
					Regex regex_id = new (
						@"\[spell=(?<id>\d+)\]",
						RegexOptions.Compiled);
					string id = regex_id.Match(data).Groups["id"].Value;
					string url = create_entry_url(Type.Spell, id);
					string name = get_spell_name(url);
					return $@"[{name}]({url})";
				}

				// Sanitize and format major/minor power list items.
				void print_list_items(string data, StringWriter writer) {
					Regex regex_item = new (
						@"\[li\](?<item>.+)\[\\\/li\]",
						RegexOptions.Compiled);
					MatchCollection items = regex_item.Matches(data);

					foreach (Match item in items) {
						string line = item.Groups["item"].Value;
						line = line.Replace(@"[b]", "**");
						line = line.Replace(@"[\/b]", "**");
						line = line.Replace(@"[i]", "*");
						line = line.Replace(@"[\/i]", "*");
						line = Regex.Replace(line, @"\[\\?\/?color(?:=q\d)?\]", "");
						line = $"\u2002\u25E6 {line}";
						writer.WriteLine(line);
					}
				}

				// Construct and return the extracted, formatted text.
				StringWriter writer = new ();

				string link_major = get_spell_link(text_major);
				writer.WriteLine($@"**Major Power:** {link_major}");
				print_list_items(text_major, writer);

				writer.WriteLine();

				string link_minor = get_spell_link(text_minor);
				writer.WriteLine($@"**Minor Power:** {link_minor}");
				print_list_items(text_minor, writer);

				writer.Flush();
				return writer.ToString().TrimEnd();
			}

			string text_affix(HtmlNode page) {
				string xpath_data = @"//div[@id='article-all']";
				HtmlNode node_data = page.SelectSingleNode(xpath_data);
				node_data = node_data.PreviousSibling;
				return node_data.InnerText.Trim();
			}

			string text_battlepet(HtmlNode page) {
				string xpath_data =
						@"//div[@id='main-contents']" +
						@"/div[@class='text']" +
						@"/p";
				HtmlNode node_data = page.SelectSingleNode(xpath_data);
				return node_data.InnerText.Trim();
			}

			string text_item(HtmlNode page) {
				// Find tooltip data.
				string xpath_data =
					@"//div[@id='main-contents']" +
					@"/div[@class='text']" +
					@"/preceding-sibling::script";
				HtmlNodeCollection nodes_data = page.SelectNodes(xpath_data);

				Regex regex_tooltip = new (@"g_\w+\[\d+\]\.tooltip_enus = ""(?<tooltip>.+)"";");
				string tooltip = "";
				foreach (HtmlNode node_data in nodes_data) {
					string data = node_data.InnerText;
					if (regex_tooltip.IsMatch(data)) {
						tooltip = regex_tooltip.Match(data).Groups["tooltip"].Value;
						break;
					}
				}

				// Recover and sanitize tooltip HTML.
				tooltip = javascript_to_html(tooltip);

				// Create a new HTML parser.
				HtmlDocument dom = new ();
				dom.LoadHtml(tooltip);

				// Find the main text node, and explicitly add newlines.
				string xpath_text = @"/table[2]/tr/td";
				HtmlNode node_text = dom.DocumentNode.SelectSingleNode(xpath_text);
				HtmlNodeCollection nodes;

				// Replace <br> tags with newlines.
				nodes = node_text.SelectNodes(@"//br");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.ReplaceChild(dom.CreateTextNode("\n"), node);
					}
				}
				// Add newlines between top-level <div>s.
				nodes = node_text.SelectNodes(@"//div/following-sibling::div");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertBefore(dom.CreateTextNode("\n"), node);
					}
				}
				// Add newlines between <span>/<div> interfaces.
				nodes = node_text.SelectNodes(@"//span/following-sibling::div");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertBefore(dom.CreateTextNode("\n"), node);
					}
				}
				// Add newlines between text/<div> interfaces.
				nodes = node_text.SelectNodes(@"//text()/following-sibling::div");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertBefore(dom.CreateTextNode("\n"), node);
					}
				}
				// Add units to "money" nodes.
				nodes = node_text.SelectNodes(@"//span[@class='moneygold']");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertAfter(dom.CreateTextNode("g "), node);
					}
				}
				nodes = node_text.SelectNodes(@"//span[@class='moneysilver']");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertAfter(dom.CreateTextNode("s "), node);
					}
				}
				nodes = node_text.SelectNodes(@"//span[@class='moneycopper']");
				if (nodes is not null) {
					foreach (HtmlNode node in nodes) {
						node.ParentNode.InsertAfter(dom.CreateTextNode("c "), node);
					}
				}
				tooltip = node_text.InnerText;

				// Remove excess newlines (no more than 2 consecutive).
				tooltip = Regex.Replace(tooltip, @"(?:\n){3,}", "\n\n");
				return tooltip.TrimEnd();
			}

			string text_achievement(HtmlNode page) {
				string xpath_data =
					@"//div[@id='main-contents']" +
					@"/div[@class='text']" +
					@"/text()";
				HtmlNodeCollection nodes = page.SelectNodes(xpath_data);

				foreach (HtmlNode node in nodes) {
					string text = node.InnerText.Trim();
					if (text == string.Empty) {
						continue;
					} else {
						return text;
					}
				}

				return "";
			}

			string text_quest(HtmlNode page) {
				string xpath_data =
					@"//div[@id='main-contents']" +
					@"/div[@class='text']" +
					@"/h2[1]" +
					@"/preceding-sibling::text()";
				HtmlNodeCollection nodes = page.SelectNodes(xpath_data);

				foreach (HtmlNode node in nodes) {
					string text = node.InnerText.Trim();
					if (text == string.Empty) {
						continue;
					} else {
						return text;
					}
				}

				return "";
			}

			string text_currency(HtmlNode page) {
				string xpath_data =
					@"//div[@id='main-contents']" +
					@"/div[@class='text']" +
					@"/p";
				HtmlNode node_data = page.SelectSingleNode(xpath_data);
				return node_data.InnerText.Trim();
			}

			string text_faction(HtmlNode page) {
				return "**Faction**";
			}

			string text_title(HtmlNode page) {
				return "**Title**";
			}
		}
	}
}
