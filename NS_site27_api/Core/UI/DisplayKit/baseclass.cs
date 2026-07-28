using DisplayKit;
using DisplayKit.Elements;
using DisplayKit.Enums;
using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UIRotate = UnityEngine.UIElements.Rotate;
namespace NS_site27_api.Core.UI.DisplayKit
{
    public abstract class DisplayLayer : IModule
    {
        public abstract string Id { get; set; }
        public abstract void InitNodes(Player target, DisplayCanvas canvas);
        public abstract void Update(Player target, DisplayCanvas canvas);
        public virtual void DestroyNodes(Player target, DisplayCanvas canvas)
        {
            canvas.Destroy();
        }
        public virtual void SetVisible(bool vis, Player target, DisplayCanvas canvas)
        {
            canvas.SetVisibility(vis);
        }
        public virtual TimeSpan updateTime { get => TimeSpan.FromSeconds(0.2f); }

        // overrides
        public virtual void OnEnable()
        {
            DisplayKitRunner.Instance.RegisterLayer(this);
        }

        public virtual void OnDisable()
        {
            DisplayKitRunner.Instance.UnregisterLayer(this);

        }

        public void OnReloadConfig()
        {
        }


        public string ModuleName => Id;

        public bool IsEnabled => true;
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            return (obj is DisplayLayer l && l.Id == Id) || (obj is string i && i == Id);
        }
    }
}
