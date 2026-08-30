using TaleWorlds.Core.ViewModelCollection.Input;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace AICompanion.Chat
{
    /// <summary>
    /// Full-screen chat window pushed on top of the current screen when the player picks the
    /// "Conversar" dialog option. Closes itself and pops back to whatever was underneath.
    /// </summary>
    public sealed class ChatScreen : ScreenBase, IGauntletScreen, IApplicationScreen
    {
        private GauntletLayer _layer;
        private ChatVM _dataSource;

        public InputContext InputContext { get; private set; }
        public string SceneName => null;
        public string ApplicationTitle => AICompanion.Companion.CompanionDefinition.FullTitle;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _dataSource = new ChatVM { CloseRequested = CloseScreen };

            _layer = new GauntletLayer(1);
            _layer.LoadMovie("AICompanionChatScreen", _dataSource);
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);

            InputContext = new InputContext("AICompanionChat");
            AddLayer(_layer);
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            _dataSource?.PumpMainThreadQueue();
        }

        private void CloseScreen()
        {
            if (ScreenManager.TopScreen == this)
            {
                ScreenManager.PopScreen();
            }
        }

        protected override void OnFinalize()
        {
            base.OnFinalize();
            if (_layer != null)
            {
                RemoveLayer(_layer);
                _layer = null;
            }
            _dataSource = null;
        }
    }
}
