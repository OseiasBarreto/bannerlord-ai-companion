using AICompanion.Config;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace AICompanion.Chat
{
    /// <summary>
    /// A small GauntletLayer subclass that gets ticked every frame by the engine, used to drain
    /// ChatVM's main-thread queue (the OpenRouter call's continuation posts here since it runs
    /// on a background thread, and Gauntlet view-models aren't safe to touch off the main one).
    /// </summary>
    internal sealed class ChatOverlayLayer : GauntletLayer
    {
        private readonly ChatVM _dataSource;

        public ChatOverlayLayer(ChatVM dataSource) : base("AICompanionChat", 1)
        {
            _dataSource = dataSource;
        }

        protected override void Tick(float dt)
        {
            base.Tick(dt);
            _dataSource.PumpMainThreadQueue();
        }
    }

    /// <summary>
    /// Adds the chat UI as a layer on top of whatever screen is currently active, instead of
    /// pushing a whole new ScreenBase. Pushing a full custom ScreenBase turned out to conflict
    /// with the conversation system and left the layer rendering nothing — adding straight to
    /// ScreenManager.TopScreen (the pattern TOR's own modding team documents) sidesteps that.
    /// </summary>
    public static class ChatOverlay
    {
        private static ChatOverlayLayer _layer;

        public static void Open()
        {
            if (_layer != null)
            {
                ModLog.Info("ChatOverlay.Open: already open, ignoring.");
                return;
            }

            ModLog.Info("ChatOverlay.Open: start.");

            var dataSource = new ChatVM { CloseRequested = Close };
            _layer = new ChatOverlayLayer(dataSource) { IsFocusLayer = true };
            _layer.LoadMovie("AICompanionChatScreen", dataSource);
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);

            ScreenManager.TopScreen.AddLayer(_layer);
            ScreenManager.TrySetFocus(_layer);

            ModLog.Info("ChatOverlay.Open: layer added and focused.");
        }

        public static void Close()
        {
            if (_layer == null)
            {
                return;
            }

            ScreenManager.TryLoseFocus(_layer);
            ScreenManager.TopScreen.RemoveLayer(_layer);
            _layer = null;

            ModLog.Info("ChatOverlay.Close: layer removed.");
        }
    }
}
