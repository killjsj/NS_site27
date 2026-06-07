using Exiled.API.Features;
using NS_site27_api.Core;
using NS_site27_api.Modules.Weapons.Turret;

namespace NS_site27_api.Modules.Weapons.Whip
{
    public class WhipConfig : ModuleConfigBase
    {
        public float Damage { get; set; } = 2f;
    }

    public class WhipModule : ModuleBase<WhipConfig>
    {
        public override string ModuleName => "Whip";

        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }
    }
}
