using System;
using System.Collections.Generic;
using System.Linq;
using AICompanion.Config;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace AICompanion.Companion
{
    public sealed class CompanionMemoryEntry
    {
        [JsonProperty("day")]
        public int Day { get; set; }

        [JsonProperty("note")]
        public string Note { get; set; }
    }

    /// <summary>
    /// Long-term memory the AI writes to itself during conversations (see the
    /// "[MEMORIA: ...]" convention parsed out in ClaudeApiClient) and reads back before
    /// replying — kept per hero (StringId), not globally, so memory belongs to whoever
    /// actually lived through those conversations. A newly-promoted "Minha Mão" starts with
    /// nothing remembered, even if a previous holder had a long history.
    /// </summary>
    public sealed class CompanionMemoryBehavior : CampaignBehaviorBase
    {
        private const int MaxEntries = 60;

        public static CompanionMemoryBehavior Instance { get; private set; }

        [SaveableField(1)]
        private Dictionary<string, List<string>> _serializedMemoriesByHero =
            new Dictionary<string, List<string>>();

        private readonly Dictionary<string, List<CompanionMemoryEntry>> _memoriesByHero =
            new Dictionary<string, List<CompanionMemoryEntry>>();

        public CompanionMemoryBehavior() => Instance = this;

        private static string CurrentHeroId => AICompanionRoleBehavior.Instance?.CurrentHolder?.StringId;

        public void AddMemory(string note)
        {
            var heroId = CurrentHeroId;
            if (string.IsNullOrWhiteSpace(note) || heroId == null)
            {
                return;
            }

            var entry = new CompanionMemoryEntry
            {
                Day = (int)CampaignTime.Now.ElapsedDaysUntilNow,
                Note = note.Trim()
            };

            if (!_memoriesByHero.TryGetValue(heroId, out var list))
            {
                list = new List<CompanionMemoryEntry>();
                _memoriesByHero[heroId] = list;
            }

            if (!_serializedMemoriesByHero.TryGetValue(heroId, out var serializedList))
            {
                serializedList = new List<string>();
                _serializedMemoriesByHero[heroId] = serializedList;
            }

            list.Add(entry);
            serializedList.Add(JsonConvert.SerializeObject(entry));

            if (list.Count > MaxEntries)
            {
                list.RemoveAt(0);
                serializedList.RemoveAt(0);
            }

            ModLog.Info($"New memory recorded for hero {heroId} (day {entry.Day}): {entry.Note}");
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AICompanion_MemoriesByHero", ref _serializedMemoriesByHero);
            _serializedMemoriesByHero ??= new Dictionary<string, List<string>>();
        }

        private void RebuildFromSaved()
        {
            _memoriesByHero.Clear();
            foreach (var pair in _serializedMemoriesByHero)
            {
                var entries = new List<CompanionMemoryEntry>();
                foreach (var raw in pair.Value)
                {
                    try
                    {
                        var entry = JsonConvert.DeserializeObject<CompanionMemoryEntry>(raw);
                        if (entry != null)
                        {
                            entries.Add(entry);
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip a corrupt entry rather than losing the whole memory list over it.
                    }
                }

                _memoriesByHero[pair.Key] = entries;
            }
        }

        /// <summary>
        /// Recent memories for whoever currently holds the role, folded into the chat system
        /// prompt so the AI actually consults what it's learned before replying.
        /// </summary>
        public string DescribeForPrompt()
        {
            var heroId = CurrentHeroId;
            if (heroId == null || !_memoriesByHero.TryGetValue(heroId, out var memories) ||
                memories.Count == 0)
            {
                return string.Empty;
            }

            var recent = memories.Skip(Math.Max(0, memories.Count - 10));
            return "Coisas que você mesmo guardou na memória ao longo dessa jornada: " +
                   string.Join(" | ", recent.Select(m => m.Note));
        }
    }
}
