namespace AICompanion.Chat
{
    public enum ChatRole
    {
        Player,
        Companion,
        System
    }

    public sealed class ChatMessage
    {
        public ChatRole Role { get; }
        public string Text { get; }

        public ChatMessage(ChatRole role, string text)
        {
            Role = role;
            Text = text;
        }
    }
}
