using TaleWorlds.Library;

namespace AICompanion.Chat
{
    public sealed class ChatMessageVM : ViewModel
    {
        private string _text;
        private bool _isPlayer;

        public ChatMessageVM(ChatMessage message)
        {
            _text = message.Text;
            _isPlayer = message.Role == ChatRole.Player;
        }

        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set
            {
                if (value == _text) return;
                _text = value;
                OnPropertyChangedWithValue(value, nameof(Text));
            }
        }

        [DataSourceProperty]
        public bool IsPlayer
        {
            get => _isPlayer;
            set
            {
                if (value == _isPlayer) return;
                _isPlayer = value;
                OnPropertyChangedWithValue(value, nameof(IsPlayer));
            }
        }
    }
}
