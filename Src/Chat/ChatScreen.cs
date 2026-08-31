using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace AICompanion.Chat
{
    /// <summary>
    /// Full-screen chat window pushed on top of the current screen when the player picks the
    /// "Conversar" dialog option. Closes itself and pops back to whatever was underneath.
    /// </summary>
    public sealed class ChatScreen : ScreenBase
    {
        private GauntletLayer _layer;
        private ChatVM _dataSource;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _dataSource = new ChatVM { CloseRequested = CloseScreen };

            _layer = new GauntletLayer("AICompanionChat", 1);
            _layer.LoadMovie("AICompanionChatScreen", _dataSource);
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);

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
