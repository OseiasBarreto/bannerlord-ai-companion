using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace AICompanion.Chat
{
    /// <summary>
    /// Persists the running conversation with the companion across save/load, so Cláudio
    /// actually remembers past conversations instead of resetting every session — the whole
    /// point of the mod is to feel like real, ongoing company, not a stateless chatbot.
    /// </summary>
    public sealed class ChatHistoryBehavior : CampaignBehaviorBase
    {
        public static ChatHistoryBehavior Instance { get; private set; }

        [SaveableField(1)]
        private List<string> _serializedHistory = new List<string>();

        private readonly List<ChatMessage> _history = new List<ChatMessage>();

        public ChatHistoryBehavior() => Instance = this;

        public IReadOnlyList<ChatMessage> History => _history;

        public void Append(ChatMessage message)
        {
            _history.Add(message);
            _serializedHistory.Add(Serialize(message));

            // Cap stored history so the save file and the context sent to Claude don't grow
            // unbounded over a long campaign.
            const int maxStored = 200;
            if (_history.Count > maxStored)
            {
                _history.RemoveRange(0, _history.Count - maxStored);
                _serializedHistory.RemoveRange(0, _serializedHistory.Count - maxStored);
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AICompanion_ChatHistory", ref _serializedHistory);
            _serializedHistory ??= new List<string>();
        }

        private void RebuildFromSaved()
        {
            _history.Clear();
            _history.AddRange(_serializedHistory.Select(Deserialize).Where(m => m != null));
        }

        private static string Serialize(ChatMessage message) => $"{message.Role}|{message.Text}";

        private static ChatMessage Deserialize(string raw)
        {
            var separatorIndex = raw.IndexOf('|');
            if (separatorIndex < 0)
            {
                return null;
            }

            var rolePart = raw.Substring(0, separatorIndex);
            var textPart = raw.Substring(separatorIndex + 1);
            return System.Enum.TryParse<ChatRole>(rolePart, out var role)
                ? new ChatMessage(role, textPart)
                : null;
        }
    }
}
