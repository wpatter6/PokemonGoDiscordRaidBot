# PokemonGoRaidBot

##### Try it out first in a sandbox environment: https://discord.gg/9SCnYAU (message me in discord if you want to try out admin commands)

## What it does
- This highly configurable discord bot will parse posts in a discord server and provide clean output to a configured channel on the discord server, and/or it can pin messages in the channel they were posted in.  

- It does not require the use of explicit commands, allowing users to post raids in chat like they normally do to create formatted raid post entries.

- Identifies the raid's location and is usually able to create a link to google maps for directions to the raid.  Cities can be applied by server and by channel to give better accuracy.

- Can tell if users are joining the raid, and how many.  Users can also use explicit commands to join or un-join a raid.

- Identifies responses to a raid post and includes the discussion on the configured output channel's raid post thread as well.  It also parses any time strings in the user's response and outputs the actual time, with configurable GMT time zone.

- Determines the end time of the raid and removes its messages from the chat after the raid has ended.

###### *Posts formatted and pinned into their original channel*
![Pinning messages posted in channel](http://i.imgur.com/AkXFcPi.png)

###### *All channels formatting and outputting to their specific channel as 'threads'*
![All output to specific channel](http://i.imgur.com/csmjW5D.png)

###### *info command will give useful information about the raid with a link to boss counters*
![info command](http://i.imgur.com/qlIGKwU.png)

## How to configure in discord:
*can be done without installing, requires Manage Server role permission*
1. Either [use my bot](https://discordapp.com/oauth2/authorize?&client_id=347493806695776256&scope=bot&permissions=0) or join your installed bot to the server ([help](https://stackoverflow.com/a/37743722/711674)).
1. Bot requires role permission "Manage Messages" in order to pin.
1. Configure which channel is the output channel using !channel command (or none with !nochannel)
1. Configure which channels should have pin behavior !pin or !pinall commands.
1. Configure the timezone of the discord server using the !timezone commands
1. Configure the city of the discord server using the !city and !channelcity commands.  This greatly improves google maps geolocation accuracy by basically appending this value to the lat/long search each time a location is identified.
1. ~~Configure the language of the discord server~~ (only "en-us" currently).

## How to install:
1. Get the zip file for your operating system from the [Releases page](https://github.com/wpatter6/PokemonGoDiscordRaidBot/releases) or by building using the below Build instructions.
1. Extract the package and run the executable.  It will ask for the following values:
    1. Discord bot token.  Copy this from the bot you created [here](https://discordapp.com/developers/applications/me)
    1. Google API key to use for location geocoding.  Get this [from google](https://developers.google.com/maps/documentation/geocoding/get-api-key)
    1. Command Prefix will be the prefix for the below commands (ex: "!")
    1. Default output channel should be the channel name from your discord server that the bot should post into.
    1. These values will be stored in the `configuration\config.json` file.  If you wish to change them in the future, you can do so in this file, or delete it and re-enter them the next time you run the bot.  If you edit the json directly, you will need to close and restart the bot for the changes to take effect.

## How to Build:
1. Download the full repository.
1. Run the publish.ps1 powershell script.  This will delete and re-create the `Releases` folder with zip files containing the builds for windows, ubuntu, and osx.

<hr/>

## Bot Commands:
*Parenthesis indicate shorthand version of command*
* `!(j)oin [id] [number]` Joins the specified number of people to the specified raid Id. Overwrites any previous values.
* `!(un)join [id]` Removes your join information from the raid.
* `!(i)nfo [name]` Displays information about the selected raid, or all of the raids if [name] is blank.  Information was taken from https://pokemongo.gamepress.gg.
* `!(d)elete [id]` Deletes a raid post with the corresponding Id.
* `!(m)erge [id1] [id2]` Merges two raid posts together.
* `!(h)elp` Shows help message.

## Admin Commands:
*requires Manage Server role permission*
* `!channel [name]` Changes the bot output channel on this server to the value passed in for [name].  If blank, the override is removed and the default value is used.
* `!nochannel` Prevents bot from posting in a specific channel. !pin functionality can still be used for specific channels.
* `!alias [pokemon] [alias]` Adds an alias for a pokemon.
* `!removealias [pokemon] [alias]` Removes an alias for a pokemon.
* `!pin [channel name]` Raids posted in the specified channel will be posted and pinned in the channel itself.
* `!unpin [channel name]` Removes channel from pin channels.
* `!pinall` Adds all channels on the server to pin channels.
* `!unpinall` Removes all channels on the server from pin channels.
* `!pinlist` Lists all pin channels.
* `!mute [channel name]` Raids posted in the specified channel will not be output or pinned, and will be ignored by the bot.
* `!unmute [channel name]` Removes channel from mute channels.
* `!muteall` Adds all channels on the server to mute channels.
* `!unmuteall` Removes all channels on the server from mute channels.
* `!mutelist` Lists all muted channels.
* `!timezone [gmt offset]` Will set the GMT offset of the discord server [gmt offset] for all time output.
* `!language [language]` Will set the language of the server.  A language file with the matching name must exist in bot config.  Default is \"en-us\"
* `!city [name]` - Will set the city of the current server.  This value gets appended to the location when using the Google geocoding API for better accuracy.
* `!channelcity [channel name] [city name]` Will set the city of the selected channel to be used in Google geocoding.

## How to make a new language:
* Copy the `Languages/en-us.json` file, and modify the values in that file to translated versions.  This will require a basic knowledge of C# format strings and regular expressions.
* Contact me with the new language file and I'll add it to the release!
