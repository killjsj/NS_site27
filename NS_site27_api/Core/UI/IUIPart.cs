using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_api.Core.UI
{
    public interface IUIPart
    {
        public string GetMessagePart(Player player, bool isSpec);
    }
    public interface IUIProvider
    {
        public IUIPart GetUIPart(int index);
        public IUIPart[] GetUIParts();
        public void AddUIPart(IUIPart part);
        public void AddUIPart(IUIPart part,int index);
        public void RemoveUIPart(int index);
    }
}
