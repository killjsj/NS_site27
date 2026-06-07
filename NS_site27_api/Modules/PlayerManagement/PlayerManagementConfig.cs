using Exiled.API.Features;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;

namespace NS_site27_api.Modules.PlayerManagement
{
    public class PlayerManagementConfig : ModuleConfigBase
    {
        public bool EnableExperienceSystem { get; set; } = true;
        public bool EnablePhaseSystem { get; set; } = true;
        public bool EnableConductSystem { get; set; } = true;
        public bool EnableStateManager { get; set; } = true;
        public bool ScpStandHeal { get; set; } = true;
        public int ScpStandHealTime { get; set; } = 5;
        public int ScpStandHealAmount { get; set; } = 3;

        public int[] LevelThresholds { get; set; } = new int[] { 100, 300, 800, 1500, 3000, 10000 };
        public string[] LevelNames { get; set; } = new string[] { "小份薯条", "中份薯条", "大份薯条", "炸锅", "漏勺", "吃薯条", "吃薯条+" };

        public double[] PhaseHours { get; set; } = new double[] { 5, 10, 15, 20, 25, 30, 35, 45, 55 };
        public string[] PhaseNames { get; set; } = new string[] { "初入茅庐", "渐窥门径", "小有成就", "稳步前行", "久经沙场", "驰骋多时", "身经百战", "纵横一方", "威名远扬", "登峰造极" };
        public string[] PhaseColors { get; set; } = new string[] { "#808080", "#FFFFFF", "#00FF00", "#00FFFF", "#0099FF", "#FFAA00", "#FF6600", "#FF00FF", "#FFD700", "#FF004D" };
    }
}
