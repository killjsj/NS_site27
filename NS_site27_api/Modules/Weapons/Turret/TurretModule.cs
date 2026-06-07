using Exiled.API.Features;
using NS_site27_api.Core;

namespace NS_site27_api.Modules.Weapons.Turret
{
    public class TurretConfig : ModuleConfigBase
    {
        public float Range { get; set; } = 50f;
        public float FireRate { get; set; } = 0.5f;
        public int TurretHP { get; set; } = 500;
        public float Damage { get; set; } = 20f;
    }

    public class TurretModule : ModuleBase<TurretConfig>
    {
        public override string ModuleName => "Turret";
        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }
    }
}
