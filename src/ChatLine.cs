namespace HAL9000
{
    internal sealed class ChatLine
    {
        public string Role;
        public string Content;

        public ChatLine(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
