If (Test-Path Releases) {
remove-item Releases -recurse
}
new-item -Name Releases -ItemType directory

dotnet restore

Add-Type -A System.IO.Compression.FileSystem

dotnet publish -c release -r win10-x64

[IO.Compression.ZipFile]::CreateFromDirectory('PokemonGoRaidBot\bin\Release\netcoreapp2.0\win10-x64\publish', 'Releases\PokemonGoDiscordRaidBot_win10-x64.zip')

dotnet publish -c release -r win8-x64

[IO.Compression.ZipFile]::CreateFromDirectory('PokemonGoRaidBot\bin\Release\netcoreapp2.0\win8-x64\publish', 'Releases\PokemonGoDiscordRaidBot_win8-x64.zip')

dotnet publish -c release -r win7-x64

[IO.Compression.ZipFile]::CreateFromDirectory('PokemonGoRaidBot\bin\Release\netcoreapp2.0\win7-x64\publish', 'Releases\PokemonGoDiscordRaidBot_win7-x64.zip')

dotnet publish -c release -r ubuntu.16.10-x64

[IO.Compression.ZipFile]::CreateFromDirectory('PokemonGoRaidBot\bin\Release\netcoreapp2.0\ubuntu.16.10-x64\publish', 'Releases\PokemonGoDiscordRaidBot_ubuntu.16.10-x64.zip')

dotnet publish -c release -r osx.10.11-x64

[IO.Compression.ZipFile]::CreateFromDirectory('PokemonGoRaidBot\bin\Release\netcoreapp2.0\osx.10.11-x64\publish', 'Releases\PokemonGoDiscordRaidBot_osx.10.11-x64.zip')

pause
