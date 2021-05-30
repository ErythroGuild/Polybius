# Polybius

Polybius is a Discord bot which searches for (and displays) queries
in any message. Although primarily used to search Wowhead entries, it
is modular and extensible to use different sites (with the suitable
scraping logic).

**To look up a query** (default settings), simply surround the query
inside double-square brackets, `[[like so]]`.

## Resources & Support

* **[Invite Polybius to your server][1]**
* **[Discord server][2]** (help & support)
* `@Polybius -help` (help command)
* **[GitHub repo][3]**
* **[GitHub issue tracker][4]** (bugs & feature requests)
* **[Latest release][5]**

## Command List

`<required arguments>`, `[optional arguments]`

* `@Polybius -help [command]`:
  Displays helptext, either general or for a specific command.
* `@Polybius -blacklist <channel>`, `@Polybius -whitelist <channel>`:
  Configure which channels aren't tracked (blacklist) or, if defined,
  the only channel(s) *to* track (whitelist).
* `@Polybius -bot-channel <channel>`, `@Polybius -clear-bot-channel`:
  All results (regardless of blacklist/whitelist) can be configured
  to be posted to a specific channel.
* `@Polybius -view-filters`, `@Polybius -view-bot-channel`:
  View the current blacklist/whitelist, and the current bot channel.
* `@Polybius -set-token-L <str>`, `@Polybius -set-token-R <str>`,
  `@Polybius -set-split <str>`:
  Customize the tokens to monitor for search queries.
* `@Polybius -view-tokens`:
  View the current tokens used for search queries.
* `@Polybius -reset-server-settings`:
  Reset all settings back to default, as if the bot has just been
  added to the server.

## Required Permissions

+ **Read Message History**: Respond to queries in posted messages.
+ **Send Messages**: Reply with search results.
+ **Embed Links**: Display the search results inside embeds.
+ **Add Reactions**: Use reactions as button workarounds.
+ **Use External Emojis**: Use custom buttons from own server.

---

*See also: [License][6] and [Acknowledgements][7].*

[1]: https://discord.com/oauth2/authorize?client_id=483340619432067098&scope=bot&permissions=346176
[2]: https://discord.gg/t3Sza8vu5E
[3]: https://github.com/ErythroGuild/polybius
[4]: https://github.com/ErythroGuild/polybius/issues
[5]: https://github.com/ErythroGuild/polybius/releases/latest
[6]: https://github.com/ErythroGuild/polybius/blob/master/License.md
[7]: https://github.com/ErythroGuild/polybius/blob/master/Acknowledgements.md
