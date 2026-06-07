using NS_site27_api.Core;

namespace NS_site27_api.Modules.HarmonyPatches
{
    public class HarmonyPatchesConfig : Core.ModuleConfigBase
    {
        public bool EnableCustomFF { get; set; } = false;
        public int AmmoLimit { get; set; } = 150;
        public int Scp207MaxStack { get; set; } = 127;
        public bool PreventSCPSprint { get; set; } = true;
    }

    public class HarmonyPatchesModule : ModuleBase<HarmonyPatchesConfig>
    {
        public override string ModuleName => "HarmonyPatches";

        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }
    }
}
