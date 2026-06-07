using at4;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Spawn;
using Exiled.API.Interfaces;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.Handlers;
using InventorySystem;
using InventorySystem.Items.Coin;
using LabApi.Events.Arguments.ServerEvents;
using MapGeneration;
using MEC;
using ProjectMER.Commands.Modifying.Rotation;
using ProjectMER.Commands.Utility;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using RemoteAdmin;
using RoundRestarting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity;
using UnityEngine;
using Player = Exiled.API.Features.Player;
namespace AT4
{
    [CustomItem(ItemType.Coin)]
    internal class AT4Item : CustomItem 
    {
        private Dictionary< Int64 , bool > _LastChangeIsKicked = new Dictionary< Int64 , bool >();
        internal void OnGlobalChangeingItem( ChangingItemEventArgs ev)
        {

            if (_LastChangeIsKicked.ContainsKey( ev.Player.Id ) && _LastChangeIsKicked[ ev.Player.Id ])
            {
                _LastChangeIsKicked[ ev.Player.Id ] = false ;

                return;
            }

            if (!_playerAT4Map.ContainsKey(ev.Player.Id) || _playerAT4Map[ev.Player.Id] == null ) return;

            bool CurrentIsAT4 = false;
            if (CustomItem.TryGet(ev.Item, out var item))
            {
                CurrentIsAT4 = item.Id == AT4id;

            }
            

            AT4Usage _curPlayerAT4useage = _playerAT4Map[ev.Player.Id];

            if( _curPlayerAT4useage._isFiring)
            {

                Player player = ev.Player;

                _LastChangeIsKicked[ player.Id ] = true;

                player.Inventory.ServerSelectItem( ((ushort)ItemCategory.None) );

                _curPlayerAT4useage?.AT4Unbind();

                return;

            }

            //Log.Info($"参数1{_at4Usage != null} 参数2{_at4Usage._at4Holder == ev.Player} 参数3{ev.Item != null} 参数4{CustomItem.TryGet(ev.Item, out var tem)} 参数5{(tem == null ? false : tem.Id == AT4id)}");

            if ( CurrentIsAT4 )
            {

                _curPlayerAT4useage.AT4Bind(ev.Player);

            }
            else
            {

                _curPlayerAT4useage.AT4Unbind();

            }

        }

        internal void OnItemDropped(DroppedItemEventArgs ev)
        {

            bool CurrentIsAT4 = false;
            if (CustomItem.TryGet(ev.Pickup, out var item))
            {
                CurrentIsAT4 = item.Id == AT4id;

            }
            if (CurrentIsAT4 == false) return;

            if (_at4GroundModels.ContainsKey(ev.Pickup))
            {

                if(_at4GroundModels[ev.Pickup].gameObject != null)
                {

                    GameObject.Destroy(_at4GroundModels[ev.Pickup].gameObject);
                    
                }
                _at4GroundModels.Remove(ev.Pickup);

            }

            GameObject ModelRoot = new GameObject("AT4GroundModelRoot");


            ModelRoot.transform.position = ev.Pickup.Transform.position;
            ModelRoot.transform.rotation = ev.Pickup.Transform.rotation;

            AT4GroundModel newModel = ModelRoot.AddComponent<AT4GroundModel>();

            newModel.Init(ev.Pickup.Transform.gameObject);


            _at4GroundModels[ev.Pickup] = newModel;

        }

        public static uint AT4id = 25000;

        public override uint Id { get => AT4id; set => AT4id = value; }

        public override string Name { get => "AT4火箭筒" ; set { } }

        public override string Description { get => "北约制式AT4反坦克火箭筒"; set{ } }

        public override float Weight { get => 50 ; set { } }

        public override SpawnProperties SpawnProperties { get; set ; } = new SpawnProperties();

        public static AT4Item Instance;

        private Dictionary< Int64 , AT4Usage > _playerAT4Map = new Dictionary< Int64 , AT4Usage >();
        private Dictionary< Int64 , int > _playerAT4count = new Dictionary< Int64 , int >();

        private Dictionary<Pickup, AT4GroundModel> _at4GroundModels = new Dictionary<Pickup, AT4GroundModel>();
        
        public bool IsIntalized => Instance != null;

        public Pickup SpawnAT4( Vector3 pos )
        {

            CustomItem.TrySpawn(AT4id, pos , out Pickup pickup);

            GameObject ModelRoot = new GameObject("AT4GroundModelRoot");

            ModelRoot.transform.position = pickup.Transform.position;
            ModelRoot.transform.rotation = pickup.Transform.rotation;

            AT4GroundModel newModel = ModelRoot.AddComponent<AT4GroundModel>();

            newModel.Init(pickup.Transform.gameObject);


            _at4GroundModels[pickup] = newModel;

            if (pickup == null)
            {

                Log.Info("Spawn Failed");

                return null;
            }

            Log.Info("Spawn sucess");

            return pickup;

        }
        public override void Init()
        {
            base.Init();

            Instance = this;

            Exiled.Events.Handlers.Player.FlippingCoin += OnShootingAT4;


        }

        public override void Destroy()
        {

            base.Destroy();

            Exiled.Events.Handlers.Player.FlippingCoin -= OnShootingAT4;    

            foreach ( var kv in _playerAT4Map)
            {

                kv.Value?.AT4Unbind();

            }_playerAT4Map.Clear();

            foreach (var gv in _at4GroundModels)
            {

                if (_at4GroundModels[gv.Key].gameObject != null)
                {

                    GameObject.Destroy(_at4GroundModels[gv.Key].gameObject);

                }

            }_at4GroundModels.Clear();

            _playerAT4count.Clear();
            _LastChangeIsKicked.Clear();
           

            Instance = null;

        }

        protected override void OnAcquired(Player player, Exiled.API.Features.Items.Item item, bool displayMessage)
        {
            base.OnAcquired(player, item, displayMessage);

            if( !_playerAT4Map.ContainsKey( player.Id ))
            {

                _playerAT4Map[player.Id] = new AT4Usage() ;

                _playerAT4count[player.Id] = 0 ;

                _LastChangeIsKicked[player.Id] = false;

            }

            _playerAT4count[player.Id]++;

            player.ShowHint($"你拿起了AT4火箭筒");

        }

        protected override void OnDroppingItem(DroppingItemEventArgs ev)
        {
            base.OnDroppingItem(ev);

            if (ev.Player != null && _playerAT4Map.ContainsKey( ev.Player.Id ) )
            {

                _playerAT4Map[ ev.Player.Id ]?.AT4Unbind();

            }

            if (--_playerAT4count[ev.Player.Id] == 0)
            {

                _playerAT4Map.Remove(ev.Player.Id);
                _playerAT4count.Remove(ev.Player.Id);
                _LastChangeIsKicked.Remove(ev.Player.Id);

            }

        }

        protected void OnShootingAT4( FlippingCoinEventArgs ev )
        {

            if (!CustomItem.TryGet(ev.Player.CurrentItem, out var item)) return;
            bool CurrentIsAT4 = item.Id == AT4id;

            if (!_playerAT4Map.ContainsKey(ev.Player.Id) || _playerAT4Map[ev.Player.Id] == null || _playerAT4Map[ ev.Player.Id ]._at4Holder != ev.Player ) return;

            if (!CurrentIsAT4) return;

            AT4Usage _curPlayerAT4useage = _playerAT4Map[ev.Player.Id];

            if (_curPlayerAT4useage.AT4Fire())
            {

                Timing.CallDelayed(1f, () =>
                {

                    _curPlayerAT4useage.AT4Unbind();

                    ev.Item.Destroy();

                    _curPlayerAT4useage._isFiring = false;

                    if ( --_playerAT4count[ev.Player.Id] == 0)
                    {

                        _playerAT4Map.Remove(ev.Player.Id);
                        _playerAT4count.Remove(ev.Player.Id);
                        _LastChangeIsKicked.Remove(ev.Player.Id);
                    }

                });

            }

        }


    }


    [CommandHandler(typeof(RemoteAdminCommandHandler))] 
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    [CommandHandler(typeof(ClientCommandHandler))]
    public class GiveAT4Command : ICommand
    {
        public string Command  => "give.at4";

        public string[] Aliases => new[] { "at4" };

        public string Description => "给予自己AT4火箭筒";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {

            Player player = Player.Get(sender);

            if( player == null || player.IsDead)
            {

                response = "你必须活着才能刷出物品";

                return false;

            }

            if( AT4Item.Instance == null || !CustomItem.TryGet(AT4Item.AT4id , out CustomItem at4 ))
            {

                response = "AT4物品未能正确注册，刷出失败";

                return false;

            }

            if( CustomItem.TryGive(player , 25000 ) == false)
            {

                response = "物品给予失败";

                return false;   

            }

            response = "AT4刷新成功";

            return true;

        }
    }

}
