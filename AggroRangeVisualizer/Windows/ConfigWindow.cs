using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AggroRangeVisualizer.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin plugin;

    public ConfigWindow(Plugin plugin) : base(
        $"{plugin.Name} Config",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(380, 220);
        this.SizeCondition = ImGuiCond.Always;

        this.Configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var isEnabled = this.Configuration.isEnabled;
        var showLowLevel = this.Configuration.showLowLevel;
        var simpleDrawMode = this.Configuration.fancyDrawMode;
        var drawDistance = this.Configuration.drawDistance;
        var maxEnemies = this.Configuration.maxEnemies;
        var maxEnemyAction = this.Configuration.maxEnemyAction;

        if (ImGui.Checkbox("Enabled", ref isEnabled))
        {
            this.Configuration.isEnabled = isEnabled;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
        ImGui.Separator();
        if (ImGui.Checkbox("Show Low Level Targets", ref showLowLevel))
        {
            this.Configuration.showLowLevel = showLowLevel;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
        if (ImGui.Checkbox("Fancy Drawing Mode", ref simpleDrawMode))
        {
            this.Configuration.fancyDrawMode = simpleDrawMode;
            this.Configuration.Save();
        }
        if (ImGui.SliderInt("Draw Distance", ref drawDistance, 5, 100))
        {
            this.Configuration.drawDistance = drawDistance;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            this.Configuration.Save();
        }
        if (ImGui.SliderInt("Max Enemies", ref maxEnemies, 2, 50))
        {
            this.Configuration.maxEnemies = maxEnemies;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            this.Configuration.Save();
        }
        if (ImGui.BeginCombo("Max Enemies Action", maxEnemyAction))
        {
            for (int i = 0; i < this.Configuration.maxEnemyActions.Length; i++)
            {
                bool is_selected = maxEnemyAction == this.Configuration.maxEnemyActions[i];
                if (ImGui.Selectable(this.Configuration.maxEnemyActions[i], is_selected))
                {
                    maxEnemyAction = this.Configuration.maxEnemyActions[i];
                    this.Configuration.maxEnemyAction = maxEnemyAction;
                    this.Configuration.Save();
                }
                if (is_selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
    }
}
