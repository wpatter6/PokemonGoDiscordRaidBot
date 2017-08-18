namespace PokemonGoRaidBot.Objects
{
    public class PokemonMessage
    {
        public PokemonMessage(string username, string message)
        {
            Username = username;
            Content = message;
        }
        public string Username;
        public string Content;
    }
}
