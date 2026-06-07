using Exiled.API.Features;
using NS_site27_api.Core;

namespace NS_site27_api.Modules.Weapons.SpeedBuild
{
    public class SpeedBuildConfig : ModuleConfigBase
    {
        public int BunkerHP { get; set; } = 200;
        public float BuildCooldown { get; set; } = 3f;
    }

    public class SpeedBuildModule : ModuleBase<SpeedBuildConfig>
    {
        public override string ModuleName => "SpeedBuild";
        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }
    }
}
