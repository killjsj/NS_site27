using Exiled.API.Features;
using NS_site27_api.Core;

namespace NS_site27_api.Modules.Weapons.Gun_JS_L1
{
    public class GunJsL1Config : ModuleConfigBase
    {
        public float Damage { get; set; } = 50f;
        public float BleedDamage { get; set; } = 3f;
        public float RegenAmount { get; set; } = 5f;
        public float MaxRange { get; set; } = 100f;
    }

    public class GunJsL1Module : ModuleBase<GunJsL1Config>
    {
        public override string ModuleName => "Gun_JS_L1";
        public override void OnEnable()
        {
            
        }

        public override void OnDisable()
        {
        }
    }
}
