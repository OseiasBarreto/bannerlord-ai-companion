using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AICompanion.Companion;
using AICompanion.Config;
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

        private MBBindingList<ChatMessageVM> _messages = new MBBindingList<ChatMessageVM>();
        private string _inputText = string.Empty;
        private bool _isWaitingForReply;
        private string _title = CompanionDefinition.FullTitle;

        public Action CloseRequested;

        public ChatVM()
        {
            if (!AICompanionConfig.Instance.IsConfigured)
            {
                Messages.Add(new ChatMessageVM(new ChatMessage(ChatRole.System,
                    "Nenhuma chave de API configurada. Crie " +
                    "Modules/AICompanion/ai-companion.config.json com sua chave da Anthropic " +
                    "para conversar com Cláudio.")));
                return;
            }

            var previous = ChatHistoryBehavior.Instance?.History;
            if (previous != null && previous.Count > 0)
            {
                foreach (var message in previous)
                {
                    Messages.Add(new ChatMessageVM(message));
                }
            }
            else
            {
                AddMessage(ChatRole.Companion,
                    "Fala, viajante. Sobre o que quer conversar hoje?");
            }
        }

        [DataSourceProperty]
        public MBBindingList<ChatMessageVM> Messages
        {
            get => _messages;
            set
            {
                if (value == _messages) return;
                _messages = value;
                OnPropertyChangedWithValue(value, nameof(Messages));
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
            }
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
            IsWaitingForReply = true;
            ModLog.Info("ChatVM.ExecuteSend: player message added, calling SendAsync.");

            var fullHistory = ChatHistoryBehavior.Instance?.History ?? (IReadOnlyList<ChatMessage>)new List<ChatMessage>();
            var historySnapshot = fullHistory
                .Skip(Math.Max(0, fullHistory.Count - ContextWindowSize))
                .ToList();

            _client.SendAsync(historySnapshot).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    var error = task.Exception?.InnerException?.Message ?? "erro desconhecido";
                    ModLog.Error($"ChatVM.ExecuteSend: task faulted: {error}");
                    _mainThreadQueue.Enqueue(() =>
                    {
                        AddMessage(ChatRole.System, $"(Cláudio não respondeu: {error})");
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
                        IsWaitingForReply = false;
                        ModLog.Info($"ChatVM.ExecuteSend: reply applied on main thread. " +
                                    $"Messages.Count={Messages.Count}, IsWaitingForReply={IsWaitingForReply}.");
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

        private void AddMessage(ChatRole role, string text)
        {
            var message = new ChatMessage(role, text);
            if (role != ChatRole.System)
            {
                ChatHistoryBehavior.Instance?.Append(message);
            }
            Messages.Add(new ChatMessageVM(message));

            // Belt-and-suspenders: MBBindingList.Add is supposed to notify the bound ListPanel
            // on its own, but a new reply wasn't visibly appearing on a live test even though it
            // was genuinely added (confirmed in the log). Forcing a property-changed on the list
            // itself makes sure the panel re-reads it regardless of whether the Add-notification
            // path works in this GauntletLayer/MissionScreen hosting context.
            OnPropertyChangedWithValue(Messages, nameof(Messages));
        }
    }
}
