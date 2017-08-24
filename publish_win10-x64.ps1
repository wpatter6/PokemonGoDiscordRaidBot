remove-item Releases\PokemonGoDiscordRaidBot_win10-x64.zip

dotnet restore

dotnet publish -c release -r win10-x64

Add-Type -A System.IO.Compression.FileSystem

[IO.Compression.ZipFile]::CreateFromDirectory('PokemonGoRaidBot\bin\Release\netcoreapp1.1\win10-x64', 'Releases\PokemonGoDiscordRaidBot_win10-x64.zip')