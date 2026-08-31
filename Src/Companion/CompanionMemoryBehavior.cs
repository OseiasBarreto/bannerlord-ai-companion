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
    /// Long-term memory Cláudio writes to himself during conversations (see the
    /// "[MEMORIA: ...]" convention parsed out in ClaudeApiClient) and reads back before
    /// replying, so he accumulates an actual understanding of the player over the campaign
    /// instead of only ever seeing raw chat history. Each entry is stored as JSON — a shape a
    /// language model reads and writes cleanly, which is the whole point.
    /// </summary>
    public sealed class CompanionMemoryBehavior : CampaignBehaviorBase
    {
        private const int MaxEntries = 60;

        public static CompanionMemoryBehavior Instance { get; private set; }

        [SaveableField(1)]
        private List<string> _serializedMemories = new List<string>();

        private readonly List<CompanionMemoryEntry> _memories = new List<CompanionMemoryEntry>();

        public CompanionMemoryBehavior() => Instance = this;

        public IReadOnlyList<CompanionMemoryEntry> Memories => _memories;

        public void AddMemory(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            var entry = new CompanionMemoryEntry
            {
                Day = (int)CampaignTime.Now.ElapsedDaysUntilNow,
                Note = note.Trim()
            };

            _memories.Add(entry);
            _serializedMemories.Add(JsonConvert.SerializeObject(entry));

            if (_memories.Count > MaxEntries)
            {
                _memories.RemoveAt(0);
                _serializedMemories.RemoveAt(0);
            }

            ModLog.Info($"New memory recorded (day {entry.Day}): {entry.Note}");
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, (_) => RebuildFromSaved());
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AICompanion_Memories", ref _serializedMemories);
            _serializedMemories ??= new List<string>();
        }

        private void RebuildFromSaved()
        {
            _memories.Clear();
            foreach (var raw in _serializedMemories)
            {
                try
                {
                    var entry = JsonConvert.DeserializeObject<CompanionMemoryEntry>(raw);
                    if (entry != null)
                    {
                        _memories.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // Skip a corrupt entry rather than losing the whole memory list over it.
                }
            }
        }

        /// <summary>
        /// Recent memories, folded into the chat system prompt so Cláudio actually consults
        /// what he's learned before responding.
        /// </summary>
        public string DescribeForPrompt()
        {
            if (_memories.Count == 0)
            {
                return string.Empty;
            }

            var recent = _memories.Skip(Math.Max(0, _memories.Count - 10));
            return "Coisas que você mesmo guardou na memória ao longo dessa jornada: " +
                   string.Join(" | ", recent.Select(m => m.Note));
        }
    }
}
