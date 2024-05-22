using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using AggroRangeVisualizer.Windows;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.Diagnostics;
using Dalamud.Logging;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace AggroRangeVisualizer
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Aggro Range Visualizer";

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        [PluginService] static internal IFramework Framework { get; private set; }
        public IGameGui GameGui { get; private set; } = null!;
        public IClientState clientState { get; private set; }
        [PluginService] static internal ICondition Condition { get; private set; }
        public readonly IObjectTable _objectTable;
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("Overlay");
        public IPluginLog Log { get; private set; }

        private ConfigWindow ConfigWindow { get; init; }
        private Overlay overlay { get; init; }

        private Vector3 lastPosition;
        public static float movementSpeed = 0;
        private List<float> movementSpeedHistory = new List<float>();
        private int historyCount = 5;
        Stopwatch sw = new Stopwatch();

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            IGameGui gameGui,
            IClientState clientState,
            IObjectTable objectTable,
            IPluginLog pluginLog)
        {
            this.PluginInterface = pluginInterface;
            this.GameGui = gameGui;
            this.clientState = clientState;
            this._objectTable = objectTable;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            this.Log = pluginLog;

            ConfigWindow = new ConfigWindow(this);
            overlay = new Overlay(this, pluginLog);
            
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(overlay);
            overlay.IsOpen = true;

            CommandManager = commandManager;
            commandManager.AddHandler("/aggroconfig", new CommandInfo((string command, string args) =>
            {
                this.ConfigWindow.Toggle();
            })
            {
                HelpMessage = "Opens the settings window."
            });
            commandManager.AddHandler("/aggro", new CommandInfo((string command, string args) =>
            {
                this.Configuration.isEnabled = !this.Configuration.isEnabled;
                this.Configuration.Save();
            })
            {
                HelpMessage = "Toggles aggro overlay."
            });

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += () => { ConfigWindow.IsOpen = true; };
            
            Framework.Update += OnFrameworkUpdate;
            sw.Start();
        }

        public void OnFrameworkUpdate(IFramework framework)
        {
            if (clientState.LocalPlayer != null)
            {
                var curPosition = clientState.LocalPlayer.Position;
                curPosition.Y = 0;
                movementSpeed = Vector3.Distance(curPosition, lastPosition) / (sw.ElapsedMilliseconds / 1000f);
                movementSpeedHistory.Add(movementSpeed);
                if (movementSpeedHistory.Count > historyCount)
                    movementSpeedHistory.RemoveAt(0);
                movementSpeed = movementSpeedHistory.Average();
                lastPosition = curPosition;
            }
            sw.Restart();
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            
            ConfigWindow.Dispose();
            overlay.Dispose();
            Framework.Update -= OnFrameworkUpdate;
        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }
    }
}
