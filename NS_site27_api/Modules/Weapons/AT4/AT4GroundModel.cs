using CommandSystem;
using DrawableLine;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.DamageHandlers;
using Exiled.API.Features.Spawn;
using Exiled.API.Interfaces;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.Handlers;
using Footprinting;
using MEC;
using PlayerStatsSystem;
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
using Utils.Networking;
using static PlayerList;
using Player = Exiled.API.Features.Player;

namespace at4
{
    internal class AT4GroundModel : MonoBehaviour
    {

        internal GameObject at4GroundModel = null ;
        internal GameObject at4Object = null ;

        private void Awake()
        {

            this.enabled = true ;

        }

        internal void Init( GameObject at4obj )
        {

            at4Object = at4obj;

            if( at4Object == null)
            {

                Log.Info("Intalization failed");
                return;

            }

            var sk = new SerializableSchematic
            {

                SchematicName = "at4",

                Position = at4Object.transform.position + new Vector3( 0 , 0.12f , 0 ),
                
                Rotation = at4Object.transform.rotation.eulerAngles + new Vector3(90, 0, 0)

            };

            at4GroundModel = sk.SpawnOrUpdateObject();

            if( at4GroundModel == null)
            {

                return;

            }

        }

        private void LateUpdate()
        {


            if (at4GroundModel == null || at4Object == null)
            {

                Destroy(this);
                return;

            }

            if (at4GroundModel.transform.position != at4Object.transform.position + new Vector3(0, 0.12f, 0))
                at4GroundModel.transform.position = at4Object.transform.position + new Vector3(0, 0.12f, 0);

            Vector3 targetEuler = at4Object.transform.rotation.eulerAngles + new Vector3( 90 , 0 , 0 );

            if (at4GroundModel.transform.rotation.eulerAngles != targetEuler)
                at4GroundModel.transform.rotation = Quaternion.Euler(targetEuler);

        }

        private void OnDestroy()
        {

            if (at4GroundModel != null)
            {

                GameObject.Destroy(at4GroundModel);
                

            }
            at4GroundModel = null;
            at4Object = null;

        }

    }
}
