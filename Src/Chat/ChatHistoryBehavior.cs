using System.Collections.Generic;
using System.Linq;
using AICompanion.Companion;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace AICompanion.Chat
{
    /// <summary>
    /// Persists conversation history per hero (keyed by StringId), not globally — whoever holds
    /// "Minha Mão" only ever sees their own past conversations. If a hero dies or is dismissed
    /// and a different one is promoted later, the new holder starts with a clean slate; the old
    /// hero's history just sits unused in the save rather than being deleted (harmless, and
    /// avoids losing it if the player re-promotes the same hero later).
    /// </summary>
    public sealed class ChatHistoryBehavior : CampaignBehaviorBase
    {
        public static ChatHistoryBehavior Instance { get; private set; }

        [SaveableField(1)]
        private Dictionary<string, List<string>> _serializedHistoryByHero =
            new Dictionary<string, List<string>>();

        private readonly Dictionary<string, List<ChatMessage>> _historyByHero =
            new Dictionary<string, List<ChatMessage>>();

        public ChatHistoryBehavior() => Instance = this;

        private static string CurrentHeroId => AICompanionRoleBehavior.Instance?.CurrentHolder?.StringId;

        public IReadOnlyList<ChatMessage> History
        {
            get
            {
                var heroId = CurrentHeroId;
                if (heroId == null)
                {
                    return System.Array.Empty<ChatMessage>();
                }

                return _historyByHero.TryGetValue(heroId, out var list)
                    ? list
                    : (IReadOnlyList<ChatMessage>)System.Array.Empty<ChatMessage>();
            }
        }

        public void Append(ChatMessage message)
        {
            var heroId = CurrentHeroId;
            if (heroId == null)
            {
                return;
            }

            if (!_historyByHero.TryGetValue(heroId, out var list))
            {
                list = new List<ChatMessage>();
                _historyByHero[heroId] = list;
            }

            if (!_serializedHistoryByHero.TryGetValue(heroId, out var serializedList))
            {
                serializedList = new List<string>();
                _serializedHistoryByHero[heroId] = serializedList;
            }

            list.Add(message);
            serializedList.Add(Serialize(message));

            // Cap stored history per hero so the save file and the context sent to the API
            // don't grow unbounded over a long campaign.
            const int maxStored = 200;
            if (list.Count > maxStored)
            {
                list.RemoveRange(0, list.Count - maxStored);
                serializedList.RemoveRange(0, serializedList.Count - maxStored);
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AICompanion_ChatHistoryByHero", ref _serializedHistoryByHero);
            _serializedHistoryByHero ??= new Dictionary<string, List<string>>();
        }

        private void RebuildFromSaved()
        {
            _historyByHero.Clear();
            foreach (var pair in _serializedHistoryByHero)
            {
                _historyByHero[pair.Key] = pair.Value.Select(Deserialize).Where(m => m != null).ToList();
            }
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
