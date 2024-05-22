using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AggroRangeVisualizer
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool isEnabled { get; set; } = true;
        public bool showLowLevel { get; set; } = false;
        public bool fancyDrawMode { get; set; } = false;
        public int drawDistance { get; set; } = 40;
        public int maxEnemies { get; set; } = 12;
        public string maxEnemyAction { get; set; } = "Cull";
        public string[] maxEnemyActions { get; private set; } = { "Simplify", "Cull" };

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
