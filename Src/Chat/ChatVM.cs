using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AICompanion.Companion;
using AICompanion.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace AICompanion.Chat
{
    /// <summary>
    /// Backing view-model for the chat screen. The Claude API call runs on a background
    /// task; its continuation is queued and drained on the main thread via
    /// <see cref="PumpMainThreadQueue"/>, called every frame from ChatScreen — Gauntlet
    /// view-models are not thread-safe to mutate off the main thread.
    /// </summary>
    public sealed class ChatVM : ViewModel
    {
        // How many past turns get sent to Claude as context on every message — keeps the
        // request bounded even though far more history is kept in the save file.
        private const int ContextWindowSize = 30;

        private readonly ClaudeApiClient _client = new ClaudeApiClient();
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private string _companionText = string.Empty;
        private string _playerText = string.Empty;
        private string _inputText = string.Empty;
        private bool _isWaitingForReply;
        private string _title = string.Empty;

        public Action CloseRequested;

        // Same clan banner shown on the vanilla conversation nameplate (SPConversation.xml's
        // "Conversed Hero Banner") — whoever holds "Minha Mão" wears the player's own clan
        // colors, since they're a member of that clan, not a separate faction.
        [DataSourceProperty]
        public ImageIdentifierVM CompanionBanner { get; }

        public ChatVM()
        {
            CompanionBanner = new BannerImageIdentifierVM(Hero.MainHero?.Clan?.Banner, false);
            Title = AICompanionRoleBehavior.Instance?.CurrentHolder?.Name?.ToString() ?? string.Empty;

            if (!AICompanionConfig.Instance.IsConfigured)
            {
                CompanionText = "Nenhuma chave de API configurada. Crie " +
                    "Modules/AICompanion/ai-companion.config.json com sua chave da API " +
                    "pra poder conversar.";
                return;
            }

            var previous = ChatHistoryBehavior.Instance?.History;
            var lastCompanionLine = previous?.LastOrDefault(m => m.Role == ChatRole.Companion);
            CompanionText = lastCompanionLine?.Text ?? string.Empty;
        }

        // A vanilla-style single-turn display, not a scrolling log: each reply replaces what was
        // shown before, same as how the game's own conversation box works — the full back and
        // forth still gets persisted to the save via ChatHistoryBehavior, just not all rendered
        // on screen at once.
        [DataSourceProperty]
        public string CompanionText
        {
            get => _companionText;
            set
            {
                if (value == _companionText) return;
                _companionText = value;
                OnPropertyChangedWithValue(value, nameof(CompanionText));
                PushToVanillaBox();
            }
        }

        [DataSourceProperty]
        public string PlayerText
        {
            get => _playerText;
            set
            {
                if (value == _playerText) return;
                _playerText = value;
                OnPropertyChangedWithValue(value, nameof(PlayerText));
            }
        }

        [DataSourceProperty]
        public string InputText
        {
            get => _inputText;
            set
            {
                if (value == _inputText) return;
                _inputText = value;
                OnPropertyChangedWithValue(value, nameof(InputText));
            }
        }

        [DataSourceProperty]
        public bool IsWaitingForReply
        {
            get => _isWaitingForReply;
            set
            {
                if (value == _isWaitingForReply) return;
                _isWaitingForReply = value;
                OnPropertyChangedWithValue(value, nameof(IsWaitingForReply));
                OnPropertyChangedWithValue(CanSend, nameof(CanSend));
                PushToVanillaBox();
            }
        }

        // Gauntlet XML bindings only support direct property references (no negation/ternary
        // in the attribute string), so the "enabled when not waiting" polarity has to live here
        // instead of in the prefab.
        [DataSourceProperty]
        public bool CanSend => !IsWaitingForReply;

        private bool _isOpen;

        // Layer is created once at conversation start and kept around inert (see
        // ChatConversationView) — this is what actually shows/hides it.
        [DataSourceProperty]
        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (value == _isOpen) return;
                _isOpen = value;
                OnPropertyChangedWithValue(value, nameof(IsOpen));

                // Scoped strictly to our own conversation: while true, the vanilla nameplate box
                // ("Claro. Diga o que pensa.") shows Cláudio's real reply instead — every other
                // NPC's conversation elsewhere in the game is left completely untouched.
                ConversationVmBridge.IsActive = value;
                if (value)
                {
                    PushToVanillaBox();
                }
            }
        }

        private void PushToVanillaBox()
        {
            ConversationVmBridge.Push(IsWaitingForReply ? "..." : CompanionText);
        }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value == _title) return;
                _title = value;
                OnPropertyChangedWithValue(value, nameof(Title));
            }
        }

        public void ExecuteSend()
        {
            ModLog.Info($"ChatVM.ExecuteSend called. InputText='{InputText}', IsWaitingForReply={IsWaitingForReply}.");

            var text = InputText?.Trim();
            if (string.IsNullOrEmpty(text) || IsWaitingForReply)
            {
                ModLog.Info("ChatVM.ExecuteSend: aborted (empty text or already waiting).");
                return;
            }

            if (!AICompanionConfig.Instance.IsConfigured)
            {
                ModLog.Error("ChatVM.ExecuteSend: aborted, config not set.");
                return;
            }

            InputText = string.Empty;
            AddMessage(ChatRole.Player, text);
            PlayerText = text;
            IsWaitingForReply = true;
            ModLog.Info("ChatVM.ExecuteSend: player message added, calling SendAsync.");

            var fullHistory = ChatHistoryBehavior.Instance?.History ?? (IReadOnlyList<ChatMessage>)new List<ChatMessage>();
            var historySnapshot = fullHistory
                .Skip(Math.Max(0, fullHistory.Count - ContextWindowSize))
                .ToList();

            _client.SendAsync(historySnapshot).ContinueWith(task =>
            {
                // A timed-out HttpClient request throws OperationCanceledException, which puts
                // the task in the Canceled state, NOT Faulted — task.IsFaulted alone missed this.
                // The old code then fell into the "success" branch and read task.Result on a
                // canceled task, which throws inside this very continuation with nothing to
                // observe it, so the queued UI update (and IsWaitingForReply = false) never ran.
                // That's the exact "stuck on 'Cláudio está pensando...' forever" bug.
                if (task.IsFaulted || task.IsCanceled)
                {
                    var error = task.IsCanceled
                        ? "sem resposta a tempo (tempo esgotado)"
                        : task.Exception?.InnerException?.Message ?? "erro desconhecido";
                    ModLog.Error($"ChatVM.ExecuteSend: task faulted/canceled: {error}");
                    _mainThreadQueue.Enqueue(() =>
                    {
                        CompanionText = $"(sem resposta: {error})";
                        IsWaitingForReply = false;
                    });
                }
                else
                {
                    var reply = task.Result;
                    ModLog.Info($"ChatVM.ExecuteSend: reply received, length={reply?.Length}. Queuing UI update.");
                    _mainThreadQueue.Enqueue(() =>
                    {
                        AddMessage(ChatRole.Companion, reply);
                        CompanionText = reply;
                        IsWaitingForReply = false;
                        ModLog.Info($"ChatVM.ExecuteSend: reply applied on main thread. " +
                                    $"IsWaitingForReply={IsWaitingForReply}.");
                    });
                }
            });
        }

        public void ExecuteClose() => CloseRequested?.Invoke();

        private bool _loggedFirstPump;

        /// <summary>Must be called once per frame from the owning screen.</summary>
        public void PumpMainThreadQueue()
        {
            if (!_loggedFirstPump)
            {
                _loggedFirstPump = true;
                ModLog.Info("ChatVM.PumpMainThreadQueue: first tick confirmed running.");
            }

            while (_mainThreadQueue.TryDequeue(out var action))
            {
                action();
            }
        }

        // Persists to the save (so the model still has real history for context on the next
        // message, and so past turns survive save/load) without necessarily being shown —
        // CompanionText/PlayerText handle the single-turn on-screen display separately.
        private void AddMessage(ChatRole role, string text)
        {
            if (role == ChatRole.System)
            {
                return;
            }

            ChatHistoryBehavior.Instance?.Append(new ChatMessage(role, text));
        }
    }
}
