using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ClaudeApiClient _client = new ClaudeApiClient();
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
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
                AddMessage(ChatRole.System,
                    "Nenhuma chave de API configurada. Crie " +
                    "Modules/AICompanion/ai-companion.config.json com sua chave da Anthropic " +
                    "para conversar com Cláudio.");
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
            var text = InputText?.Trim();
            if (string.IsNullOrEmpty(text) || IsWaitingForReply)
            {
                return;
            }

            if (!AICompanionConfig.Instance.IsConfigured)
            {
                return;
            }

            InputText = string.Empty;
            AddMessage(ChatRole.Player, text);
            IsWaitingForReply = true;

            var historySnapshot = new List<ChatMessage>(_history);

            _client.SendAsync(historySnapshot).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    var error = task.Exception?.InnerException?.Message ?? "erro desconhecido";
                    _mainThreadQueue.Enqueue(() =>
                    {
                        AddMessage(ChatRole.System, $"(Cláudio não respondeu: {error})");
                        IsWaitingForReply = false;
                    });
                }
                else
                {
                    var reply = task.Result;
                    _mainThreadQueue.Enqueue(() =>
                    {
                        AddMessage(ChatRole.Companion, reply);
                        IsWaitingForReply = false;
                    });
                }
            });
        }

        public void ExecuteClose() => CloseRequested?.Invoke();

        /// <summary>Must be called once per frame from the owning screen.</summary>
        public void PumpMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                action();
            }
        }

        private void AddMessage(ChatRole role, string text)
        {
            var message = new ChatMessage(role, text);
            _history.Add(message);
            Messages.Add(new ChatMessageVM(message));
        }
    }
}
