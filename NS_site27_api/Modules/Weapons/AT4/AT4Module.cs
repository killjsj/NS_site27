using Exiled.API.Features;
using NS_site27_api.Core;

namespace NS_site27_api.Modules.Weapons.AT4
{
    public class AT4Config : ModuleConfigBase
    {
        public float Damage { get; set; } = 3000f;
    }

    public class AT4Module : ModuleBase<AT4Config>
    {
        public override string ModuleName => "AT4";

        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }
    }
}
