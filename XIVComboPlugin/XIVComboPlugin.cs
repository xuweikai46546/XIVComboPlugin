using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Messaging;
using Dalamud.Game.Chat;
using ImGuiNET;
using Serilog;
using System.Collections.Generic;
using System.Dynamic;

namespace XIVComboPlugin
{
    class XIVComboPlugin : IDalamudPlugin
    {
        public string Name => "XIV Combo Plugin";

        public XIVComboConfiguration Configuration;

        private DalamudPluginInterface pluginInterface;
        private IconReplacer iconReplacer;
        private readonly int CURRENT_CONFIG_VERSION = 3;
        private CustomComboPreset[] orderedByClassJob;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.pluginInterface.CommandManager.AddHandler("/pcombo", new CommandInfo(OnCommandDebugCombo)
            {
                HelpMessage = "打开编辑自定义连击设置窗口.",
                ShowInHelp = true
            });

            this.Configuration = pluginInterface.GetPluginConfig() as XIVComboConfiguration ?? new XIVComboConfiguration();
            if (Configuration.Version < 3)
            {
                Configuration.HiddenActions = new List<bool>();
                for (var i = 0; i < Enum.GetValues(typeof(CustomComboPreset)).Length; i++)
                    Configuration.HiddenActions.Add(false);
                Configuration.Version = 3;
            }

            this.iconReplacer = new IconReplacer(pluginInterface.TargetModuleScanner, pluginInterface.ClientState, this.Configuration);

            this.iconReplacer.Enable();

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => isImguiComboSetupOpen = true;
            this.pluginInterface.UiBuilder.OnBuildUi += UiBuilder_OnBuildUi;
            /*
            pluginInterface.Subscribe("PingPlugin", e => {
                dynamic msg = e;
                iconReplacer.UpdatePing(msg.LastRTT / 2);
                PluginLog.Log("Ping was updated to {0} ms", msg.LastRTT / 2);
                });
                */
            var values = Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>();
            orderedByClassJob = values.Where(x => x != CustomComboPreset.None && x.GetAttribute<CustomComboInfoAttribute>() != null).OrderBy(x => x.GetAttribute<CustomComboInfoAttribute>().ClassJob).ToArray();
            UpdateConfig();
        }

        private bool isImguiComboSetupOpen = false;

        private string ClassJobToName(byte key)
        {
            switch (key)
            {
                default: return "Unknown";
                case 1: return "剑术师";
                case 2: return "格斗家";
                case 3: return "斧术师";
                case 4: return "枪术师";
                case 5: return "弓箭手";
                case 6: return "幻术师";
                case 7: return "咒术师";
                case 8: return "刻木匠";
                case 9: return "锻铁匠";
                case 10: return "铸甲匠";
                case 11: return "雕金匠";
                case 12: return "制革匠";
                case 13: return "裁衣匠";
                case 14: return "炼金术士";
                case 15: return "烹调师";
                case 16: return "采矿工";
                case 17: return "园艺工";
                case 18: return "捕鱼人";
                case 19: return "骑士";
                case 20: return "武僧";
                case 21: return "战士";
                case 22: return "龙骑士";
                case 23: return "诗人";
                case 24: return "白魔法师";
                case 25: return "黑魔法师";
                case 26: return "秘术师";
                case 27: return "召唤师";
                case 28: return "学者";
                case 29: return "双剑师";
                case 30: return "忍者";
                case 31: return "机工士";
                case 32: return "暗黑骑士";
                case 33: return "占星术士";
                case 34: return "武士";
                case 35: return "赤魔法师";
                case 36: return "青魔法师";
                case 37: return "绝枪战士";
                case 38: return "舞者";
            }
        }

        private void UpdateConfig()
        {
            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                if (Configuration.HiddenActions[i])
                    iconReplacer.AddNoUpdate(orderedByClassJob[i].GetAttribute<CustomComboInfoAttribute>().Abilities);
                else
                    iconReplacer.RemoveNoUpdate(orderedByClassJob[i].GetAttribute<CustomComboInfoAttribute>().Abilities);
            }
        }

        private void UiBuilder_OnBuildUi()
        {
            if (!isImguiComboSetupOpen)
                return;

            var flagsSelected = new bool[orderedByClassJob.Length];
            var hiddenFlags = new bool[orderedByClassJob.Length];
            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                flagsSelected[i] = Configuration.ComboPresets.HasFlag(orderedByClassJob[i]);
                hiddenFlags[i] = Configuration.HiddenActions[i];
            }

            ImGui.SetNextWindowSize(new Vector2(740, 490));
            ImGui.Begin("自定义连击设置", ref isImguiComboSetupOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar);

            ImGui.Text("启用或禁用自定义连击.");
            ImGui.Separator();

            ImGui.BeginChild("scrolling", new Vector2(0, 400), true, ImGuiWindowFlags.HorizontalScrollbar);

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 5));

            var lastClassJob = 0;

            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                var flag = orderedByClassJob[i];
                var flagInfo = flag.GetAttribute<CustomComboInfoAttribute>();
                if (lastClassJob != flagInfo.ClassJob)
                {
                    lastClassJob = flagInfo.ClassJob;
                    if (ImGui.CollapsingHeader(ClassJobToName((byte)lastClassJob)))
                    {
                        for (int j = i; j < orderedByClassJob.Length; j++)
                        {
                            flag = orderedByClassJob[j];
                            flagInfo = flag.GetAttribute<CustomComboInfoAttribute>();
                            if (lastClassJob != flagInfo.ClassJob)
                            {
                                break;
                            }
                            ImGui.PushItemWidth(200);
                            ImGui.Checkbox(flagInfo.FancyName, ref flagsSelected[j]);
                            ImGui.PopItemWidth();
                            ImGui.SameLine(275);
                            ImGui.Checkbox("禁用图标更新" + $"##{j}", ref hiddenFlags[j]);
                            ImGui.TextColored(new Vector4(0.68f, 0.68f, 0.68f, 1.0f), $"#{j+1}:" + flagInfo.Description);
                            ImGui.Spacing();
                        }
                        
                    }
                    
                }
            }

            for (var i = 0; i < orderedByClassJob.Length; i++)
            {
                if (flagsSelected[i])
                {
                    Configuration.ComboPresets |= orderedByClassJob[i];
                }
                else
                {
                    Configuration.ComboPresets &= ~orderedByClassJob[i];
                }
                Configuration.HiddenActions[i] = hiddenFlags[i];
            }

            ImGui.PopStyleVar();

            ImGui.EndChild();

            ImGui.Separator();
            if (ImGui.Button("保存")) {
                this.pluginInterface.SavePluginConfig(Configuration);
                UpdateConfig();
            }
            ImGui.SameLine();
            if (ImGui.Button("保存并关闭")) {
                this.pluginInterface.SavePluginConfig(Configuration);
                this.isImguiComboSetupOpen = false;
                UpdateConfig();
            }
            ImGui.End();
        }

        public void Dispose()
        {
            this.iconReplacer.Dispose();

            this.pluginInterface.CommandManager.RemoveHandler("/pcombo");

            this.pluginInterface.Dispose();
        }

        private void OnCommandDebugCombo(string command, string arguments)
        {
            var argumentsParts = arguments.Split();

            switch (argumentsParts[0])
            {
                case "setall":
                    {
                        foreach (var value in Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>())
                        {
                            if (value == CustomComboPreset.None)
                                continue;

                            this.Configuration.ComboPresets |= value;
                        }

                        this.pluginInterface.Framework.Gui.Chat.Print("全部设置");
                    }
                    break;
                case "unsetall":
                    {
                        foreach (var value in Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>())
                        {
                            this.Configuration.ComboPresets &= value;
                        }

                        this.pluginInterface.Framework.Gui.Chat.Print("全部设置解除");
                    }
                    break;
                case "set":
                    {
                        foreach (var value in Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>())
                        {
                            if (value.ToString().ToLower() != argumentsParts[1].ToLower())
                                continue;

                            this.Configuration.ComboPresets |= value;
                        }
                    }
                    break;
                case "toggle":
                    {
                        foreach (var value in Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>())
                        {
                            if (value.ToString().ToLower() != argumentsParts[1].ToLower())
                                continue;

                            this.Configuration.ComboPresets ^= value;
                        }
                    }
                    break;

                case "unset":
                    {
                        foreach (var value in Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>())
                        {
                            if (value.ToString().ToLower() != argumentsParts[1].ToLower())
                                continue;

                            this.Configuration.ComboPresets &= ~value;
                        }
                    }
                    break;

                case "list":
                    {
                        foreach (var value in Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>().Where(x => x != CustomComboPreset.None))
                        {
                            if (argumentsParts[1].ToLower() == "set")
                            {
                                if (this.Configuration.ComboPresets.HasFlag(value))
                                    this.pluginInterface.Framework.Gui.Chat.Print(value.ToString());
                            }
                            else if (argumentsParts[1].ToLower() == "all")
                                this.pluginInterface.Framework.Gui.Chat.Print(value.ToString());
                        }
                    }
                    break;

                default:
                    this.isImguiComboSetupOpen = true;
                    break;
            }

            this.pluginInterface.SavePluginConfig(this.Configuration);
        }
    }
}
