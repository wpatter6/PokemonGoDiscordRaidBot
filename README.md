# PokemonGoRaidBot
- Discord Bot Template based on Discord.Net 1.0.1

- This discord bot will parse posts in a discord guild and provide clean output to a configured channel on the discord server.

- It identifies responses to a raid post and includes the discussion on the configured channel's raid post thread as well.  It also parses any time strings in the user's response and outputs the actual time (currently only in MST but will soon make it configurable)

![Pokemon Go Raid Bot in action](http://i.imgur.com/6iqfkcN.png)

- When multiple responses occur, the number of respondants will display instead of the 'posted by' user.

- Specific channels can have the raid posts pinned and can be configured using commands listed below.

- It determines the end time of the raid and removes its messages from the output chat after the raid has ended.

## How to Build:
1. Download the full repository.
1. Run the publish.ps1 powershell script.  This will delete and re-create the `Releases` folder with zip files containing the builds for windows, ubuntu, and osx.

## How to install:
1. Get the zip file for your operating system from the `Releases` folder, either by downloading it directly or building using the above  Build instructions.
1. Extract the package and run the executable.  It will ask for the following values:
  1. Discord bot token.  Copy this from the bot you created at [here](https://discordapp.com/developers/applications/me)
  1. Google API key to use for location geocoding.
  1. Command Prefix will be the prefix for the below commands (ex: "!")
  1. It will ask for the default output channel.  This should be the channel name from your discord server that the bot should post into.
  1. These values will be stored in the `configuration\config.json` file.  If you wish to change them in the future, you can do so in this file, or delete it and re-enter them the next time you run the bot.  If you edit the json directly, you will need to close and restart the bot for the changes to take effect.

## Bot Commands:
* !join [id] [number] - Joins the specified number of people to the specified raid Id. Overwrites any previous values.
* !unjoin [id] - Removes your join information from the raid.
* !info [name] - Displays information about the selected raid, or all of the raids above rank 3.  Information was taken from https://pokemongo.gamepress.gg.
* !channel [name] - *Changes the bot output channel on this server to the value passed in for [name].  If blank, the override is removed and the default value is used.
* !nochannel - *Prevents bot from posting in a specific channel. !pin functionality can still be used for specific channels.
* !alias [pokemon] [alias] - *Adds an alias for a pokemon.
* !removealias [pokemon] [alias] - *Removes an alias for a pokemon.
* !delete [id] - *Deletes a raid post with the corresponding Id.
* !merge [id1] [id2] - *Merges two raid posts together.
* !pin [channel name] - *Raids posted in the specified channel will be posted and pinned in the channel itself.
* !unpin [channel name] - *Removes channel from pin channels.
* !pinall - *Adds all channels on the server to pin channels.
* !unpinall - *Removes all channels on the server from pin channels.
* !timezone [gmt offset] - *Will set the GMT offset of the discord server [gmt offset] for all time output.
* !language [language] - *Will set the language of the server.  A language file with the matching name must exist in bot config.  Default is \"en-us\"
* !city [name] - *Will set the city of the current server.  This value gets appended to the location when using the Google geocoding API for better accuracy.
* !channelcity [channel name] [city name] - *Will set the city of the selected channel to be used in Google geocoding.
* !help - Shows this message.
