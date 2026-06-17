using Exiled.API.Features;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace NS_site27_api.Modules.PlayerManagement
{
    public class PlayerManagementConfig : ModuleConfigBase
    {
        public int ScpStandHealTime { get; set; } = 5;
        public int ScpStandHealAmount { get; set; } = 3;
        public string SpecUI { get; set; } = "<line-height=65%><size=17><color=#FFC0CB>Site-27轻量一服\nQ群:123\n我们诚挚的邀请你与我们共创服务器未来</color>";
        [YamlMember(Description ="每次刷新阵营后随机")]
        public List<string> tips { get; set; } = new List<string>()
        {
            "1","2","4"
        };

    }
}
