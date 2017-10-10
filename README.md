# PokemonGoRaidBot

##### Try it out in a sandbox environment: https://discord.gg/9SCnYAU (message wpatter6 in discord if you want to try out admin commands)

##### The bot now supports Dutch as its first non-english language!  Using the command `!culture nl-NL` will now translate your bot's output, big thanks to sjaakbanaan for doing the translation.

## What it does
- This highly configurable discord bot will parse posts in a discord server and provide clean output to a configured channel on the discord server, and/or it can pin messages in the channel they were posted in.  

- It does not require the use of explicit commands, allowing users to post raids in chat like they normally do to create formatted raid post entries.

- Identifies the raid's location and is usually able to create a link to google maps for directions to the raid.  Cities can be applied by server and by channel to give better accuracy.

- Can tell if users are joining the raid, and how many.  Users can also use explicit commands to join or un-join a raid.  Can also determine if the user says at what time they will arrive.

- Once a user has posted or joined a raid, they will be notified by direct message of any users that join or quit the raid.  This behavior can be disabled.

- Users can join raids by giving an emoji reaction to the bot's raid post.  Any reaction except thumbs down (👎) will add one to the user's joined persons count.  Removing the reaction will subtract one from the joined persons count.

- Identifies responses to a raid post and includes the discussion on the configured output channel's raid post thread as well.  It also parses any time strings in the user's response and outputs the actual time, with configurable GMT time zone.

- Determines the end time of the raid and removes its messages from the chat after the raid has ended.

###### *Posts formatted and pinned into their original channel*
![Pinning messages posted in channel](http://i.imgur.com/1JgHR2F.png)

###### *All channels formatting and outputting to their specific channel as 'threads'*
![All output to specific channel](https://imgur.com/CDvSLCY.png)

###### *info command will give useful information about the raid with a link to boss counters*
![info command](http://i.imgur.com/7LN3Asy.png)

## How to post effectively
* Only post a single raid in a single message.
* If the channel already has a raid and you want to post a second with the same pokemon, ensure you include a different location
* Cross streets and landmark names will work best for locations.

## How to configure in discord:
*can be done without installing, requires Manage Server role permission*
1. Either [use my bot](https://discordapp.com/oauth2/authorize?&client_id=347493806695776256&scope=bot&permissions=0) or join your installed bot to the server ([help](https://stackoverflow.com/a/37743722/711674)).
1. Check the [Different Configurations](https://github.com/wpatter6/PokemonGoDiscordRaidBot/wiki/Different-Configurations) wiki to determine which configuration will work best for your server.
1. Bot requires role permission "Manage Messages" in order to pin.
1. Configure which channel is the output channel using !channel command (or none with !nochannel)
1. Configure which channels should have pin behavior !pin or !pinall commands.
1. Configure the timezone of the discord server using the !timezone commands
1. Configure the city of the discord server using the !city and !channelcity commands.  This greatly improves google maps geolocation accuracy by basically appending this value to the lat/long search each time a location is identified.
1. Configure the culture of the discord server using !culture if outside the US.  This will allow 24 hour clock and languages (if language file exists -- see bottom for how to build)

## How to install:
1. Get the zip file for your operating system from the [Releases page](https://github.com/wpatter6/PokemonGoDiscordRaidBot/releases/latest) or by building using the below Build instructions.
1. Required dependencies for .NET Core applications can be [found here](https://github.com/dotnet/core/blob/master/Documentation/prereqs.md).
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

## Future Additions:
* Move google geocode off messaging thread so it doesn't slow results.
* Store raid data and produce aggregate statistics.
* Continuously improve phrase matching and dicitionary.

<hr/>

## Bot Commands:
*Parenthesis indicate shorthand version of command, `*` means optional parameter. *
* `!(r)aid [pokemon] [time left] [location]` Creates a new raid post with the specified [pokemon] name, [time left] (formatted H:MM), and [location], which must be identifiable by google maps.  Creates a new raid post whether a matching one exists or not.
* `!(j)oin [raid*] [number*] [arrival time*]` Joins the specified number of people to the specified `raid` Id.  Adding + or - before the number will add or subtract from an existing value.  If pokemon name is used instead of Id, will join the most recently posted in channel matching the start of the pokemon name.  If no parameters, will join 1 to the most recently posted raid in that channel.
* `!(un)join [raid*]` Removes your join information from the raid.  If `raid` is blank, unjoins all.
* `!(i)nfo [name*]` Displays information about the selected raid, or all of the raids if `name` is blank.  Information was taken from https://pokemongo.gamepress.gg.
* `!(d)elete [raid]` Deletes a raid post with the corresponding Id.  Use `all` to delete all raids posted by you.  The thumbs down emoji reaction will delete the raid as well, if done by the raid poster or server admin.
* `!(m)erge [raid1] [raid2]` Merges `raid2` into `raid1`.  Can only be done by admin or original poster of `raid2`.
* `!(loc)ation [raid] [new location]` Allows the poster of the raid or admin to change the location of a post.
* `!(e)nd [raid] [new end time]` Allows the poster of the raid or admin to change the end time on a post.
* `!(s)tart [raid] [start time]` Allows a user to explicitly declare a start time for a group in the raid.  This will be added to the post's information header.
* `!(h)elp` Shows help message.

## Admin Commands:
*requires Manage Server or Admin role permission*
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
* `!culture [culture]` Will set the culture of the server for time outputs.  If a language file with the matching name exists in bot config, that language file will be used.  Default is \"en-us\".
* `!city [name]` - Will set the city of the current server.  This value gets appended to the location when using the Google geocoding API for better accuracy.
* `!channelcity [channel name] [city name]` Will set the city of the selected channel to be used in Google geocoding.
* `!cities` Displays the city configured to the server and each channel.
* `!place [location] [lat, long*]` Allows a location to be manually added to the server's list.  This allows an admin to set up locations that are commonly used in their area but google maps does not always give a good match.   Multiple places can be added with a single command by adding a carriage return between each place and its lat/long.
* `!places` Displays all locations that have been added to the server's list.
* `!deleteplace` [location] Removes specified place from the server's list.

## How to make a new language:
* Copy the `Languages/en-us.json` file, and modify the values in that file to translated versions.  This will require a basic knowledge of C# format strings and regular expressions.
* Contact me with the new language file and I'll add it to the release!
