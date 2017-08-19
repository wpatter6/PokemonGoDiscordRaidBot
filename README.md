# PokemonGoRaidBot
Discord Bot Template based on Discord.Net 1.0.1

This discord bot will parse posts in a discord guild and provide clean output to a configured channel on the discord guild.

It attempts to identify responses to a raid post and includes the chat on the configured channel's history as well.

It determines the end time of the raid and removes it from chat after the raid has ended.

## How to build:
1. Download the full repository.
1. Run the publish.ps1 powershell script.  This will delete and re-create the `Releases` folder with zip files containing the builds for windows, ubuntu, and osx.

## How to install:
1. Get the zip file for your operating system from the `Releases` folder, either by downloading it directly or building using the above instructions.
1. Extract the package and run the executable
  1. It will ask you to enter the bot token.  Copy this from the bot you created at [here](https://discordapp.com/developers/applications/me)
  1. It will ask for the default output channel.  This should be the channel name from your discord server that the bot should post into.
  1. These values will be stored in the `configuration\config.json` file.  If you wish to change them in the future, you can do so in this file, or delete it and re-enter them the next time you run the bot.
