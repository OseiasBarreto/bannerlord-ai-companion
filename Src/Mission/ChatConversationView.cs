using System;
using AICompanion.Chat;
using AICompanion.Config;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace AICompanion.Mission
{
    /// <summary>
    /// Hosts the chat GauntletLayer for a conversation. Primary path (ported from the real,
    /// shipped wonderingmark123/Bannerlord.ChatGPT mod): build the layer unconditionally, hidden,
    /// in OnConversationBegin, on MissionScreen — then the dialog consequence just flips
    /// visibility. Fallback: if OnConversationBegin never fired for this conversation (observed
    /// on a real test — no log line from it at all, suggesting a lightweight party-roster
    /// conversation doesn't dispatch that hook the same way a scene-based one does) or
    /// MissionScreen was never populated, build the layer lazily the moment the dialog option is
    /// actually picked, using whatever screen is on top at that exact moment instead.
    /// </summary>
    public sealed class ChatConversationView : MissionView
    {
        private GauntletLayer _layer;
        private ChatVM _dataSource;
        private ScreenBase _hostScreen;

        public override void OnConversationBegin()
        {
            base.OnConversationBegin();
            ModLog.Info("ChatConversationView.OnConversationBegin fired.");
            EnsureLayer();
        }

        /// <summary>Creates the layer if it doesn't exist yet. Safe to call repeatedly.</summary>
        public bool EnsureLayer()
        {
            if (_layer != null)
            {
                return true;
            }

            ModLog.Info("ChatConversationView.EnsureLayer: start.");
            try
            {
                _hostScreen = (ScreenBase)(object)MissionScreen ?? ScreenManager.TopScreen;
                ModLog.Info($"ChatConversationView.EnsureLayer: MissionScreen=" +
                            $"{MissionScreen?.GetType().FullName ?? "null"}, host=" +
                            $"{_hostScreen?.GetType().FullName ?? "null"}.");

                if (_hostScreen == null)
                {
                    ModLog.Error("ChatConversationView.EnsureLayer: no host screen available at all.");
                    return false;
                }

                _dataSource = new ChatVM { CloseRequested = () => SetActive(false) };
                _layer = new GauntletLayer("AICompanionChat", 9000) { IsFocusLayer = true };
                _hostScreen.AddLayer(_layer);
                _layer.LoadMovie("AICompanionChatScreen", _dataSource);
                _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.Invalid);
                ModLog.Info("ChatConversationView.EnsureLayer: layer created (inactive).");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("ChatConversationView.EnsureLayer threw", ex);
                _layer = null;
                _hostScreen = null;
                return false;
            }
        }

        public void SetActive(bool active)
        {
            if (active && !EnsureLayer())
            {
                ModLog.Error("ChatConversationView.SetActive: could not create layer, aborting.");
                return;
            }

            if (_layer == null)
            {
                return;
            }

            _dataSource.IsOpen = active;

            if (active)
            {
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                ScreenManager.TrySetFocus(_layer);
            }
            else
            {
                ScreenManager.TryLoseFocus(_layer);
                _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.Invalid);
            }

            ModLog.Info($"ChatConversationView.SetActive({active}): done.");
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            _dataSource?.PumpMainThreadQueue();

            if (_dataSource != null && _dataSource.IsOpen && Input.IsKeyPressed(InputKey.Escape))
            {
                SetActive(false);
            }
        }

        public override void OnConversationEnd()
        {
            base.OnConversationEnd();
            RemoveLayerIfAny();
        }

        private void RemoveLayerIfAny()
        {
            if (_layer == null)
            {
                return;
            }

            ScreenManager.TryLoseFocus(_layer);
            _hostScreen?.RemoveLayer(_layer);
            _layer = null;
            _dataSource = null;
            _hostScreen = null;
            ModLog.Info("ChatConversationView: layer removed.");
        }
    }
}
