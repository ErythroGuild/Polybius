using System.Text.RegularExpressions;

namespace Polybius {
	class Sanitizer {
		static public string SanitizeTooltip(string tooltip) {
			RegexOptions regex_options = RegexOptions.Compiled | RegexOptions.IgnoreCase;

			void replace(string regex, string replacement) =>
				tooltip = Regex.Replace(tooltip, regex, replacement, regex_options);
			void extract(string regex) => replace(regex, "$1\n");
			void erase(string regex) => replace(regex, "");

			replace(@"&nbsp;", " ");        // no-break space
			replace(@"&quot;|&#39;|&#x27;", "'");   // apostrophe
			replace(@">\s*?<", "><");       // spaces outside of tags
			replace(@"<br\s?>", "\n");      // line breaks

			// item price <span>s need to be formatted before other <span>s are stripped
			replace(@"<span class=""money-\w+?"" alt=""([gsc])"">(.+?)<\/span>", "$2$1 ");
			// specifically remove secondary stat scaling info
			erase(@"<span class=""rating-conversion"">(.+?)<\/span>");

			erase(@"<\/?b>");               // not worth formatting with **markdown**
			erase(@"<\/?span.*?>");         // <span>s are usually(?) formatting
			erase(@"<img.*?>");             // talent modifications add <img> icons
			erase(@"<\/?a.*?>");            // don't need inline links

			// Item tooltips require special handling:
			// remove socket icons
			// N.B.: must happen before stripping padding <div>s, or those will match on the
			// closing </div> of the socket icon <div>.
			replace(@"<div class=""s-wow s-wow-socket-.\w+?""><\/div>", "\u2002\u29C8");
			// remove item title (redundant)
			erase(@"<dt class=""db-title q\d"">(?:.|\s)+?<\/dt>");
			// all glyphs are minor glyphs now
			erase(@"<dd class=""db-glyph-type"">(?:.|\s)+?<\/dd>");
			// cleanup item category
			extract(@"<dd style="".+?"">(.+?)<\/dd>");
			// extract use text
			extract(@"<dd class=""green margin-top"">((?:.|\s)*?)<\/dd>");
			// extract item level
			replace(@"<dd class=""yellow"">((?:.|\s)+?)\n?<\/dd>", "*$1*\n");
			// extract text from padding <div>s (appends newline)
			extract(@"<div class=""(?:|padding-block)"">((?:.|\s)*?)<\/div>");
			// replace plain <dd> list items (non stat)
			extract(@"<dd>([^\+](?:.*?))<\/dd>");
			// extract (weapon?) dps info
			extract(@"<dd class=""j-dps-info"".*?>(.+?)<\/dd>");
			// condense pairs of <dd class="db-left db-right"> tags
			replace(@"<dd class=""db-left"">(.+?)<\/dd>\s*?<dd class=""db-right"">(.+?)<\/dd>", "$1 \u2022 $2\n");
			// clean up any unpaired  <dd class="db-left"> items
			extract(@"<dd class=""db-left"">(.+?)<\/dd>");
			// condense stat info
			replace(@"<dd class=""(?:|q\d)"">\n?(\+[\d,]+?)\n?(.+?)\n*?<\/dd>", "\u2002$1 $2\n");
			// single-line stat info
			replace(@"<dd>(\+(?:.+?))<\/dd>", "\u2002$1\n");
			// "item descriptor" stat info (e.g. artifact relic)
			replace(@"<dd class=""(?:|q\d)"">\n?(.+?)\n*?<\/dd>", "\u2002$1\n");
			// condense socket info
			extract(@"<dd class=""socket"">(.+?)<\/dd>");
			// sell price (g/s/c) <span>s are formatted earlier
			// extract sell price
			replace(@"<dd class=""db-sell-price"">(.+?)\s?<\/dd>", "*$1*\n");
			// extract item source
			extract(@"<dd class=""item-extra"">(.+?)<\/dd>");

			tooltip = tooltip.Trim();   // cleanup any extra newlines at the start/end
			return tooltip;
		}
	}
}
