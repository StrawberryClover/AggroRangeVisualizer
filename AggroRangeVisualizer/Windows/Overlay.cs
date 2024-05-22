using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using ImGuiNET;
using Lumina.Data.Structs;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using GameObject = Dalamud.Game.ClientState.Objects.Types.GameObject;

namespace AggroRangeVisualizer.Windows
{
    internal class Overlay : Window, IDisposable
    {
        public static Window window;
        private Plugin Plugin;
        private IPluginLog pluginLog;
        private Vector3 lastPosition;
        private List<float> speedHistory = new List<float>();
        private int historyCount = 60;
        private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();


        //                           v (int max)                     | ???
        //                            v                              | ???
        //                             v                             | ???
        //                              v                            | ???
        //                               v                           | ???
        //                                v                          | ???
        //                                 v                         | ???
        //                                  v                        | ???
        //                                   v                       | ???
        //                                    v                      | ???
        //                                     v                     | ???
        //                                      v                    | ???
        //                                       v                   | ???
        //                                        v                  | ???
        //                                         v                 | ???
        //                                          v                | ???
        //                                           v               | ???
        //                                            v (ushort max) | Objects, but not all objects, and maybe terrain?
        //                                             v             | Invisible Walls + Terrain? Maybe some objects?
        //                                              v            | Objects in Gridania??
        //                                               v           | ???
        //                                                v          | ???
        //                                                 v         | ???
        //                                                  v        | ???
        //                                                   v       | ???
        //                                                    v      | ???
        //                                                     v     | Invisible Ceiling?
        //                                                      v    | Some objects?
        //                                                       x   | Objects, maybe terrain?
        //                                                        x  | Only Terrain?
        //                                                         v | Invisible Ceiling
        private int raycastFlags = 0b0000000000000000001000000000010;

        private float maxYChange = 0.5f;

        private string[] blindMobs =
        {
            "Roselet",
            "Fluturini",
            "Slug",
            "Yarzon",
            "Mantis",
            "Corpse Flower",
            "Worm",
            "Mirrorknight",
            "Aurelia",
            "Sankchinni",
            "Ameretat",
            "Walking Sapling",
            "Clipper",
            "Red Eye",
            "Karlabos of Pyros",
            "Pyros Piranu",
            "Pyros Slime",
            "Pyros Hawk",
            "Pyros Wood Golem",
            "Nanka",
            "Islandhander",
            "Bat",
            "Wamouracampa"
        };
        private string[] deafMobs =
        {
            "Ruszor",
            "Wolf",
            "Mole",
            "Banemite",
            " Ant",
            "Dodo",
            "Deepeye",
            "Galago",
            "Puk",
            "Colibri",
            "Tursus",
            "Bear",
            "Uragnite",
            "Gelato",
            "Vindthurs",
            "Matamata",
            "Wildebeest",
            "Wyvern",
            "Croc",
            " Eyes",
            " Guardian",
            "Geshunpest",
            "Zoblyn",
            "Coeurl",
            "Spriggan",
            "Val Yeti",
            "Northern Ray"
        };
        private string[] ashkin =
        {
            "Demon of the Incunable"
        };

        private int[] deepDungeonTerritories =
        {
            561, 562, 563, 564, 565, 593, 594, 595, 596, 597, 598, 599, 600, 601, 602, 603, 604, 605, 606, 607, // PotD
            770, 771, 772, 773, 774, 775, 782, 783, 784, 785, // HoH
            1099, 1100, 1101, 1102, 1103, 1104, 1105, 1106, 1107, 1108 // EO
        };


        public Overlay(Plugin plugin, IPluginLog pluginLog) : base(
        "Aggro Overlay", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar
                        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysUseWindowPadding)
        {
            window = this;
            unsafe
            {
                float windowWidth = Device.Instance()->Width;
                float windowHeight = Device.Instance()->Height;
                this.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(windowWidth, windowHeight),
                    MaximumSize = new Vector2(windowWidth, windowHeight)
                };
                this.SizeCondition = ImGuiCond.Always;
            }

            this.Plugin = plugin;
            this.pluginLog = pluginLog;
            this.Position = new Vector2(0, 0);
            this.PositionCondition = ImGuiCond.Always;
        }

        // shamelessly stole from BetterTargetingSystem, which in turn shamelessly stole from elsewhere
        [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3")]
        private CanAttackDelegate? CanAttackFunction = null!;
        private delegate nint CanAttackDelegate(nint a1, nint objectAddress);

        public override void Draw()
        {
            if (Plugin.clientState.LocalPlayer != null && Plugin.Configuration.isEnabled)
            {
                /*
                Vector2 drawPos;
                Plugin.GameGui.WorldToScreen(Plugin.clientState.LocalPlayer.Position, out drawPos);
                ImGui.SetCursorScreenPos(drawPos);
                ImGui.Text($"Speed: {Plugin.movementSpeed}");
                */

                int e = 0;
                if (Plugin.Configuration.maxEnemyAction == "Simplify")
                {
                    if (Plugin._objectTable.Count(x => Vector3.Distance(Plugin.clientState.LocalPlayer.Position, x.Position) <= Plugin.Configuration.drawDistance) > Plugin.Configuration.maxEnemies)
                    {
                        e = Plugin.Configuration.maxEnemies + 1;
                    }
                }

                float healthPerc = (float)Plugin.clientState.LocalPlayer.CurrentHp / (float)Plugin.clientState.LocalPlayer.MaxHp;
                foreach (var gameObject in Plugin._objectTable.OrderBy(x => Vector3.Distance(Plugin.clientState.LocalPlayer.Position, x.Position)))
                {
                    #region Deep Dungeon Objects
                    if (deepDungeonTerritories.Contains(Plugin.clientState.TerritoryType))
                    {
                        Vector2 objectPos;
                        Plugin.GameGui.WorldToScreen(gameObject.Position, out objectPos);
                        string objectName = gameObject.Name.ToString();
                        switch (gameObject.DataId)
                        {
                            case 802:
                            case 803:
                                objectName = "Bronze Coffer";
                                break;
                            case 2007358:
                                objectName = "Gold Coffer";
                                break;
                            case 2007357:
                                objectName = "Silver Coffer";
                                break;
                            case 2007542:
                                objectName = "Accursed Hoard";
                                break;
                            case 2007543:
                                objectName = "Banded Coffer";
                                break;
                            case 2007182:
                                objectName = "Landmine Trap";
                                break;
                            case 2007183:
                                objectName = "Luring Trap";
                                break;
                            case 2007184:
                                objectName = "Enfeebling Trap";
                                break;
                            case 2007185:
                                objectName = "Impeding Trap";
                                break;
                            case 2007186:
                                objectName = "Toading Trap";
                                break;
                            case 6360:
                                objectName = "Mimic";
                                break;
                            case 2007187:
                                objectName = "Cairn of Return";
                                break;
                            case 2007188:
                                objectName = "Cairn of Passage";
                                break;
                            default:
                                objectName = "";
                                break;
                        }
                        //string label = objectName + " (" + gameObject.DataId.ToString() + ")";
                        if (objectName != "")
                        {
                            string label = objectName;
                            Vector2 labelSize = ImGui.CalcTextSize(label);
                            objectPos.X -= labelSize.X / 2;
                            objectPos.Y -= labelSize.Y / 2;
                            ImGui.GetWindowDrawList().AddText(objectPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1f)), label);
                        }
                        // Bronze Treasure Coffer: 2007358
                        // Silver Treasure Coffer: 2007357
                        // Trap ???: 6388
                        // Cairn of Return: 2007187
                        // Cairn of Passage: 2007188
                        // Mimic?: 6360
                    }
                    #endregion

                    if (gameObject.ObjectKind.ToString() == "BattleNpc" && Vector3.Distance(Plugin.clientState.LocalPlayer.Position, gameObject.Position) <= Plugin.Configuration.drawDistance)
                    {
                        unsafe
                        {
                            var o = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
                            var c = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)gameObject.Address;
                            //var enemyList = GetEnemyList();
                            //pluginLog.Debug(c->IsHostile.ToString());
                            if (o->GetIsTargetable() && !gameObject.IsDead && c->CharacterData.Battalion == 4 && c->IsHostile && !c->InCombat /*&& (c->StatusFlags & 0b0110) == 0 && c->StatusFlags != 0*/)
                            {
                                e++;
                                if (e > Plugin.Configuration.maxEnemies && Plugin.Configuration.maxEnemyAction == "Cull")
                                {
                                    break;
                                }

                                Vector2 pos;
                                bool inView;
                                Plugin.GameGui.WorldToScreen(gameObject.Position, out pos, out inView);

                                #region Deep Dungeon
                                if (deepDungeonTerritories.Contains(Plugin.clientState.TerritoryType))
                                {
                                    float touchingRadius = 0.75f;
                                    float soundRadius = 10f;
                                    float sightRadius = 10f;
                                    float proximityRadius = 10f;
                                    float bossRadius = 13.5f;

                                    uint threatColor;
                                    DeepData.MobData mobData = DeepData.Mobs(c->NameID);
                                    /*
                                    Vector2 dataPos = pos;
                                    string dataLabel = c->NameID.ToString() + ": " + mobData.Threat.ToString();
                                    Vector2 dataLabelSize = ImGui.CalcTextSize(dataLabel);
                                    dataPos.X -= dataLabelSize.X / 2;
                                    dataPos.Y += 3;
                                    ImGui.GetWindowDrawList().AddText(dataPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1f)), dataLabel);
                                    */

                                    switch (mobData.Threat)
                                    {
                                        case DeepData.MobData.ThreatLevel.Easy:
                                            threatColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 0.3f));
                                            break;

                                        case DeepData.MobData.ThreatLevel.Caution:
                                            threatColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.851f, 0.4f, 0.8f));
                                            break;

                                        case DeepData.MobData.ThreatLevel.Dangerous:
                                            threatColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.6f, 0, 0.8f));
                                            break;

                                        case DeepData.MobData.ThreatLevel.Vicious:
                                            threatColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.8f));
                                            break;

                                        default:
                                            threatColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 0.3f));
                                            break;
                                    }

                                    // Touching them
                                    if (touchingRadius > 0)
                                        DrawRing(gameObject, 0.75f, 20, 2f, threatColor, true, false, new Vector4(), e > Plugin.Configuration.maxEnemies);

                                    switch (mobData.Aggro)
                                    {
                                        case DeepData.MobData.AggroType.Sight:
                                            DrawCone(gameObject, (sightRadius + gameObject.HitboxRadius), 45, true, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.2f)), e > Plugin.Configuration.maxEnemies);
                                            break;

                                        case DeepData.MobData.AggroType.Sound:
                                            DrawRing(gameObject, (soundRadius + gameObject.HitboxRadius), 50, 2f, Plugin.movementSpeed > 5 ? ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.2f)) : ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 0.2f)), true, false, new Vector4(), e > Plugin.Configuration.maxEnemies);
                                            break;

                                        case DeepData.MobData.AggroType.Proximity:
                                            DrawRing(gameObject, (proximityRadius + gameObject.HitboxRadius), 50, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.2f)), true, false, new Vector4(), e > Plugin.Configuration.maxEnemies);
                                            break;

                                        case DeepData.MobData.AggroType.Boss:
                                            DrawRing(gameObject, (bossRadius + gameObject.HitboxRadius), 50, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.2f)), true, false, new Vector4(), e > Plugin.Configuration.maxEnemies);
                                            break;

                                        default:

                                            break;
                                    }
                                }
                                #endregion
                                else
                                {
                                    float touchingRadius = 0.75f;
                                    float soundRadius = 11f;
                                    float sightRadius = 14f;

                                    bool mobIsBlind = IsMobBlind(gameObject);
                                    bool mobIsDeaf = IsMobDeaf(gameObject);

                                    bool lowLevel = (gameObject as Character).Level < Plugin.clientState.LocalPlayer?.Level - 5;
                                    if (Plugin.Condition[ConditionFlag.BoundByDuty]) lowLevel = false;
                                    else if (!Plugin.Configuration.showLowLevel && (gameObject as Character).Level < Plugin.clientState.LocalPlayer?.Level - 10)
                                    {
                                        // Very low level
                                        soundRadius = 0f;
                                        sightRadius = 0f;
                                        touchingRadius = 0f;
                                    }

                                    // ### DUNGEONS ###
                                    #region Dungeons Toggle
                                    if (Plugin.clientState.TerritoryType == 1036) //Satasha Toggle
                                    {
                                        soundRadius = 9.5f;
                                        sightRadius = 9.75f;
                                        if (gameObject.Name.ToString() == "Scurvy Dog")
                                        {
                                            soundRadius = 12f;
                                            sightRadius = 15f;
                                        }
                                    }
                                    #endregion

                                    //Plugin.Log.Debug(Plugin.clientState.TerritoryType.ToString());
                                    Plugin.Log.Debug(Plugin.clientState.TerritoryType.ToString());

                                    // ### EUREKA ###
                                    #region Eureka Toggle
                                    if (new int[] { 732, 763, 795 }.Contains(Plugin.clientState.TerritoryType))
                                    {
                                        var playerCharacter = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)Plugin.clientState.LocalPlayer!.Address;
                                        bool eurekaLowLevel = Plugin.Configuration.showLowLevel ? false : c->GetForayInfo()->ElementalLevel <= playerCharacter->GetForayInfo()->ElementalLevel - 2;
                                        soundRadius = 8.5f;
                                        sightRadius = 8.5f;
                                        if (gameObject.Name.ToString().Contains("Sprite") || gameObject.Name.ToString().ToLower().Contains("white flame"))
                                        {
                                            // Magic Aggro
                                            if (!eurekaLowLevel)
                                                if (Plugin.Configuration.fancyDrawMode && !(Plugin.Configuration.maxEnemyAction == "Simplify" && e > Plugin.Configuration.maxEnemies))
                                                    DrawRingWorld(gameObject, 17f, 50, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.75f, 0f, 1f, 0.15f)), true, false, new Vector4());
                                                else
                                                    DrawRingSimple(gameObject, 17f, 50, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.75f, 0f, 1f, 0.15f)), true, false, new Vector4());
                                            sightRadius = 0f;
                                            soundRadius = 0f;
                                            touchingRadius = 0f;
                                        }
                                        if (IsAshkin(gameObject))
                                        {
                                            // Blood Aggro
                                            var ringColor = healthPerc <= 0.3f ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0f, 0f, 0.1f)) : healthPerc <= 0.6f ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0f, 0.1f)) : ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0.25f, 0f, 0.1f));
                                            if (!eurekaLowLevel)
                                                if (Plugin.Configuration.fancyDrawMode && !(Plugin.Configuration.maxEnemyAction == "Simplify" && e > Plugin.Configuration.maxEnemies))
                                                    DrawRingWorld(gameObject, 20f, 50, 2f, ringColor, true, false, new Vector4());
                                                else
                                                    DrawRingSimple(gameObject, 20f, 30, 2f, ringColor, true, false, new Vector4());

                                            sightRadius = 0f;
                                            soundRadius = 0f;
                                        }


                                        if (gameObject.Name.ToString() == "Frozen Void Dragon" || gameObject.Name.ToString() == "Flame Voidragon")
                                        {
                                            soundRadius = 12f;
                                            sightRadius = 0f;
                                            mobIsBlind = true;
                                        }


                                        if (eurekaLowLevel)
                                        {
                                            sightRadius = 0f;
                                            soundRadius = 0f;
                                            touchingRadius = 0f;
                                            e--;
                                        }
                                    }
                                    #endregion

                                    if (mobIsBlind)
                                        sightRadius = 0f;
                                    if (mobIsDeaf)
                                        soundRadius = 0f;

                                    // Touching them
                                    if (touchingRadius > 0)
                                        if (Plugin.Configuration.fancyDrawMode && !(Plugin.Configuration.maxEnemyAction == "Simplify" && e > Plugin.Configuration.maxEnemies))
                                            DrawRingWorld(gameObject, 0.75f, 30, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.3f)), true, false, new Vector4());
                                        else
                                            DrawRingSimple(gameObject, 0.75f, 20, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.3f)), true, false, new Vector4());
                                    // True Aggro
                                    //DrawRingWorld(gameObject, (11f + gameObject.HitboxRadius), 50, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.3f)), false, new Vector4());
                                    // Sound Aggro
                                    if (soundRadius > 0)
                                        if (Plugin.Configuration.fancyDrawMode && !(Plugin.Configuration.maxEnemyAction == "Simplify" && e > Plugin.Configuration.maxEnemies))
                                            DrawRingWorld(gameObject, (soundRadius + gameObject.HitboxRadius) / (lowLevel ? 1.66666f : 1f), 50, 2f, Plugin.movementSpeed > 5 ? ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.2f)) : ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 0.2f)), mobIsBlind ? true : false, false, new Vector4());
                                        else
                                            DrawRingSimple(gameObject, (soundRadius + gameObject.HitboxRadius) / (lowLevel ? 1.66666f : 1f), 50, 2f, Plugin.movementSpeed > 5 ? ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.2f)) : ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 0.2f)), mobIsBlind ? true : false, false, new Vector4());
                                    // Sight Aggro
                                    if (sightRadius > 0)
                                    {
                                        if (Plugin.Configuration.fancyDrawMode && !(Plugin.Configuration.maxEnemyAction == "Simplify" && e > Plugin.Configuration.maxEnemies))
                                        {
                                            DrawConeWorld(gameObject, (sightRadius + gameObject.HitboxRadius) / (lowLevel ? 1.5f : 1f), 45, true, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.1f)));
                                            DrawRingWorld(gameObject, (sightRadius + gameObject.HitboxRadius) / (lowLevel ? 1.5f : 1f), 50, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.1f)), false, false, new Vector4());
                                        }
                                        else
                                        {
                                            DrawConeSimple(gameObject, (sightRadius + gameObject.HitboxRadius) / (lowLevel ? 1.5f : 1f), 45, true, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.1f)));
                                            DrawRingSimple(gameObject, (sightRadius + gameObject.HitboxRadius) / (lowLevel ? 1.5f : 1f), 50, 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.1f)), false, false, new Vector4());
                                        }
                                    }
                                    if (inView)
                                    {
                                        ImGui.SetCursorScreenPos(pos);
                                    }
                                }
                                //Plugin.GameGui.WorldToScreen(gameObject.Position, out Vector2 infoPos, out bool infoInView);
                                //ImGui.SetCursorPos(infoPos);
                                //if (infoInView) ImGui.Text($"{c->Battalion} : {Convert.ToString(c->StatusFlags, 2).PadLeft(8, '0')}");
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {

        }

        private bool IsMobBlind(GameObject mob)
        {
            bool blind = false;
            foreach (string blindName in blindMobs)
            {
                if (mob.Name.ToString().ToLower().Contains(blindName.ToLower()))
                {
                    blind = true;
                    break;
                }
            }
            return blind;
        }
        private bool IsMobDeaf(GameObject mob)
        {
            bool deaf = false;
            foreach (string deafName in deafMobs)
            {
                if (mob.Name.ToString().ToLower().Contains(deafName.ToLower()))
                {
                    deaf = true;
                    break;
                }
            }
            return deaf;
        }
        private bool IsAshkin(GameObject mob)
        {
            bool isAshkin = false;
            foreach (string ashkinName in ashkin)
            {
                if (mob.Name.ToString().ToLower().Contains(ashkinName.ToLower()))
                {
                    isAshkin = true;
                    break;
                }
            }
            return isAshkin;
        }

        private unsafe uint[] GetEnemyList()
        {
            var addonByName = Plugin.GameGui.GetAddonByName("_EnemyList", 1);
            if (addonByName == IntPtr.Zero)
                return Array.Empty<uint>();

            var addon = (AddonEnemyList*)addonByName;
            var numArray = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder.NumberArrays[21];
            var list = new List<uint>(addon->EnemyCount);
            for (var i = 0; i < addon->EnemyCount; i++)
            {
                var id = (uint)numArray->IntArray[8 + (i * 6)];
                list.Add(id);
            }
            return list.ToArray();
        }

        private void DrawRing(GameObject actor, float radius, int numSegments, float thicc, uint colour, bool fill, bool offset, Vector4 off, bool cull)
        {
            if (Plugin.Configuration.fancyDrawMode && !(Plugin.Configuration.maxEnemyAction == "Simplify" && cull))
                DrawRingWorld(actor, radius, numSegments, thicc, colour, fill, offset, off);
            else
                DrawRingSimple(actor, radius, numSegments, thicc, colour, fill, offset, off);
        }

        private void DrawRingSimple(GameObject actor, float radius, int numSegments, float thicc, uint colour, bool fill, bool offset, Vector4 off)
        {
            var xOff = 0f;
            var yOff = 0f;
            if (offset)
            {
                xOff = off.X;
                yOff = off.Y;
            }
            var seg = numSegments / 2;
            float lastSegmentY = actor.Position.Y;
            for (var i = 0; i <= numSegments; i++)
            {
                var newWorldPos = new Vector3(
                    actor.Position.X + xOff + (radius * (float)Math.Sin((Math.PI / seg) * i)),
                    actor.Position.Y,
                    actor.Position.Z + yOff + (radius * (float)Math.Cos((Math.PI / seg) * i))
                    );
                /*if (i != 0 && i != numSegments)
                    if (Math.Abs(adjustedPos.Y - lastSegmentY) > maxYChange)
                        adjustedPos.Y = lastSegmentY + (Math.Sign(adjustedPos.Y - lastSegmentY) * maxYChange);*/
                lastSegmentY = newWorldPos.Y;
                Plugin.GameGui.WorldToScreen(newWorldPos, out Vector2 screenPos);
                ImGui.GetWindowDrawList().PathLineTo(screenPos);
            }
            if (fill)
                ImGui.GetWindowDrawList().PathFillConvex(colour);
            else
                ImGui.GetWindowDrawList().PathStroke(colour, ImDrawFlags.None, thicc);
        }

        private void DrawRingWorld(GameObject actor, float radius, int numSegments, float thicc, uint colour, bool fill, bool offset, Vector4 off)
        {
            var xOff = 0f;
            var yOff = 0f;
            if (offset)
            {
                xOff = off.X;
                yOff = off.Y;
            }
            var seg = numSegments / 2;
            float lastSegmentY = actor.Position.Y;
            for (var i = 0; i <= numSegments; i++)
            {
                var newWorldPos = WorldPointToGround(new Vector3(
                    actor.Position.X + xOff + (radius * (float)Math.Sin((Math.PI / seg) * i)),
                    actor.Position.Y,
                    actor.Position.Z + yOff + (radius * (float)Math.Cos((Math.PI / seg) * i))
                    ));
                var adjustedPos = newWorldPos;
                if (!PointInView(newWorldPos))
                {
                    //adjustedPos.Y = actor.Position.Y;
                }
                /*if (i != 0 && i != numSegments)
                    if (Math.Abs(adjustedPos.Y - lastSegmentY) > maxYChange)
                        adjustedPos.Y = lastSegmentY + (Math.Sign(adjustedPos.Y - lastSegmentY) * maxYChange);*/
                lastSegmentY = adjustedPos.Y;
                Plugin.GameGui.WorldToScreen(adjustedPos, out Vector2 screenPos);
                ImGui.GetWindowDrawList().PathLineTo(screenPos);
            }
            if (fill)
                ImGui.GetWindowDrawList().PathFillConvex(colour);
            else
                ImGui.GetWindowDrawList().PathStroke(colour, ImDrawFlags.None, thicc);
        }

        private void DrawCone(GameObject actor, float radius, int angle, bool fill, uint color, bool cull)
        {
            if (Plugin.Configuration.fancyDrawMode && !(Plugin.Configuration.maxEnemyAction == "Simplify" && cull))
                DrawConeWorld(actor, radius, angle, fill, color);
            else
                DrawConeSimple(actor, radius, angle, fill, color);
        }


        private void DrawConeSimple(GameObject actor, float radius, int angle, bool fill, uint color)
        {
            var pos = actor.Position;
            bool onScreen = false;
            var lastPoint = pos;
            for (int degrees = -angle; degrees <= angle; degrees += 10)
            {
                float rad = (float)(degrees * Math.PI / 180) + actor.Rotation;
                Vector3 newPos = new Vector3(
                        pos.X + radius * (float)Math.Sin(rad),
                        pos.Y,
                        pos.Z + radius * (float)Math.Cos(rad));


                //First Line
                if (degrees == -angle)
                {
                    lastPoint = SplitLineSimple(pos, newPos, 5);
                    newPos.Y = lastPoint.Y;
                }
                /*if (Math.Abs(adjustedPos.Y - lastPoint.Y) > maxYChange)
                    adjustedPos.Y = lastPoint.Y + (Math.Sign(adjustedPos.Y - lastPoint.Y) * maxYChange);*/

                //Rest
                onScreen |= Plugin.GameGui.WorldToScreen(newPos,
                    out Vector2 vector2);

                ImGui.GetWindowDrawList().PathLineTo(vector2);
                if (degrees != -angle) lastPoint = newPos;
            }

            SplitLineSimple(lastPoint, pos, 10);

            /*onScreen |= Plugin.GameGui.WorldToScreen(new Vector3(
                pos.X,
                pos.Y,
                pos.Z),
                out Vector2 v2Local);
            ImGui.GetWindowDrawList().PathLineTo(v2Local);*/

            if (onScreen)
            {
                if (fill)
                    ImGui.GetWindowDrawList().PathFillConvex(color);
                else
                    ImGui.GetWindowDrawList().PathStroke(color, ImDrawFlags.Closed, 2);
            }
            else
                ImGui.GetWindowDrawList().PathClear();
        }

        private void DrawConeWorld(GameObject actor, float radius, int angle, bool fill, uint color)
        {
            var pos = actor.Position;
            bool onScreen = false;
            var lastPoint = pos;
            for (int degrees = -angle; degrees <= angle; degrees += 4)
            {
                float rad = (float)(degrees * Math.PI / 180) + actor.Rotation;
                Vector3 newPos = WorldPointToGround(new Vector3(
                        pos.X + radius * (float)Math.Sin(rad),
                        pos.Y,
                        pos.Z + radius * (float)Math.Cos(rad)));


                Vector3 adjustedPos = newPos;
                if (!PointInView(newPos))
                {
                    //adjustedPos.Y = pos.Y;
                }

                //First Line
                if (degrees == -angle)
                {
                    lastPoint = SplitLine(pos, adjustedPos, 10);
                    adjustedPos.Y = lastPoint.Y;
                }
                /*if (Math.Abs(adjustedPos.Y - lastPoint.Y) > maxYChange)
                    adjustedPos.Y = lastPoint.Y + (Math.Sign(adjustedPos.Y - lastPoint.Y) * maxYChange);*/

                //Rest
                onScreen |= Plugin.GameGui.WorldToScreen(adjustedPos,
                    out Vector2 vector2);

                ImGui.GetWindowDrawList().PathLineTo(vector2);
                if (degrees != -angle) lastPoint = adjustedPos;
            }

            SplitLine(lastPoint, pos, 10);

            /*onScreen |= Plugin.GameGui.WorldToScreen(new Vector3(
                pos.X,
                pos.Y,
                pos.Z),
                out Vector2 v2Local);
            ImGui.GetWindowDrawList().PathLineTo(v2Local);*/

            if (onScreen)
            {
                if (fill)
                    ImGui.GetWindowDrawList().PathFillConvex(color);
                else
                    ImGui.GetWindowDrawList().PathStroke(color, ImDrawFlags.Closed, 2);
            }
            else
                ImGui.GetWindowDrawList().PathClear();
        }
        private Vector3 SplitLineSimple(Vector3 from, Vector3 to, int steps)
        {
            Vector3 lastPoint = to;
            for (int i = 0; i < steps; i++)
            {
                var stepPos = Vector3.Lerp(from, to, i / (float)steps);
                /*if (Math.Abs(stepPos.Y - lastPoint.Y) > maxYChange)
                    stepPos.Y = lastPoint.Y + (Math.Sign(stepPos.Y - lastPoint.Y) * maxYChange);*/
                Plugin.GameGui.WorldToScreen(new Vector3(
                stepPos.X,
                stepPos.Y,
                stepPos.Z),
                out Vector2 newPos);
                ImGui.GetWindowDrawList().PathLineTo(newPos);
                lastPoint = stepPos;
            }
            return lastPoint;
        }

        private Vector3 SplitLine(Vector3 from, Vector3 to, int steps)
        {
            Vector3 lastPoint = to;
            for (int i = 0; i < steps; i++)
            {
                var stepPos = Vector3.Lerp(from, to, i / (float)steps);
                /*if (Math.Abs(stepPos.Y - lastPoint.Y) > maxYChange)
                    stepPos.Y = lastPoint.Y + (Math.Sign(stepPos.Y - lastPoint.Y) * maxYChange);*/
                Plugin.GameGui.WorldToScreen(WorldPointToGround(new Vector3(
                stepPos.X,
                stepPos.Y,
                stepPos.Z)),
                out Vector2 newPos);
                ImGui.GetWindowDrawList().PathLineTo(newPos);
                lastPoint = stepPos;
            }
            return lastPoint;
        }

        private unsafe Vector3 WorldPointToGround(Vector3 point)
        {
            point = new Vector3(point.X, point.Y + 10, point.Z);
            RaycastHit hit;
            int flags = raycastFlags;
            BGCollisionModule* CollisionModule = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->BGCollisionModule;
            bool hitSomething = CollisionModule->RaycastEx(&hit, point, new Vector3(0, -1, 0), 20, 1, &flags);
            if (hitSomething) return hit.Point;
            else return new Vector3(point.X, point.Y, point.Z);
        }

        private unsafe bool PointInView(Vector3 point)
        {
            var cameraPos = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager.Instance()->CurrentCamera->Object.Position;
            var direction = (FFXIVClientStructs.FFXIV.Common.Math.Vector3)point - cameraPos;
            var distance = direction.Magnitude - 1f;
            direction = direction.Normalized;

            RaycastHit hit;
            int flags = raycastFlags;
            BGCollisionModule* CollisionModule = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->BGCollisionModule;
            var isLoSBlocked = CollisionModule->RaycastEx(&hit, cameraPos, direction, distance, 1, &flags);

            return !isLoSBlocked;
        }
    }
}
