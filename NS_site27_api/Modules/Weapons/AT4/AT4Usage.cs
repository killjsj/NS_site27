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
using LabApi.Features.Wrappers;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Modules.Weapons.AT4;
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

namespace AT4
{
    internal class AT4Usage
    {

        private GameObject _at4OBJ;
        private SchematicObject _at4SCM;

        private AT4Bounder _at4Bounder;

        internal Player _at4Holder = null ;

        private readonly LayerMask _collisionLayers = LayerMask.GetMask( "Hitbox" , "Player" , "ViewModel" , "Door" , "Fence" , "Glass" ); 

        internal bool _isFiring = false ;

        internal void AT4Bind( Player at4Holder )
        {
            _at4Holder = at4Holder;

            var sk = new SerializableSchematic
            {

                SchematicName = "at4",

                Position = at4Holder.Transform.position,

                Rotation = at4Holder.Transform.rotation.eulerAngles


            };

            _at4OBJ = sk.SpawnOrUpdateObject();

            _at4SCM = _at4OBJ.GetComponent<SchematicObject>();

            var at4Root = _at4OBJ;
            if (at4Root == null) return;
            
            _at4Bounder = _at4OBJ.AddComponent<AT4Bounder>();
            _at4Bounder._at4Root = at4Root;
            _at4Bounder.Bindplayer(at4Holder);

        }

        internal void AT4Unbind()
        {

            if( _at4Bounder != null)
            {

                GameObject.Destroy(_at4Bounder);

                _at4Bounder = null;

            }

            if (_at4OBJ != null)
            {

                GameObject.Destroy( _at4OBJ);
                _at4OBJ = null;

            }

            _at4SCM = null;
            _isFiring = false;

        }

        internal bool AT4Fire()
        {

            if (_at4Holder == null || _at4Bounder == null || _at4OBJ == null || _at4SCM == null ) return false ;

            if( _isFiring)
            {

                _at4Holder.ShowHint("AT4正在发射，稍安勿躁");

                return false;

            }

            _isFiring = true;

            GameObject muzzle = _at4SCM.AttachedBlocks.FirstOrDefault( b => b.name == "Cylinder")?.gameObject;
            muzzle = muzzle.transform.Find("Cylinder (2)")?.gameObject;

            Vector3 locFireDir = muzzle.transform.forward + new Vector3( 0f , 90f , 0f );

            Vector3 worldFirDir = muzzle.transform.TransformDirection(locFireDir).normalized;

            GameObject shell = CreateShellObject( muzzle.transform.position , worldFirDir );

            shell.AddComponent<AT4ShellCollision>();

            var shellcomp = shell.GetComponent<AT4ShellCollision>();

            shellcomp.Init(this, _collisionLayers);

            return true;

        }

        private GameObject CreateShellObject( Vector3 SpPos , Vector3 FireDir )
        {

            try
            {

                Vector3 rotationEuler = new Vector3
                (

                    0f,

                    Mathf.Atan2(FireDir.x, FireDir.z) * Mathf.Rad2Deg, 

                    -( Mathf.Asin(FireDir.y) * Mathf.Rad2Deg)

                );

                var sk = new SerializableSchematic
                {

                    SchematicName = "APShell",

                    Position = SpPos,

                    Rotation = rotationEuler + new Vector3( 0 , 90 , 0 ) ,

                };

                GameObject shell = sk.SpawnOrUpdateObject();

                if( shell == null)
                {

                    Log.Info("can't spawn shell SCH");

                    return null;

                }

                if( shell.TryGetComponent<Rigidbody>( out Rigidbody rb ) == false)
                {

                    rb = shell.AddComponent<Rigidbody>();

                }

                rb.useGravity = true;
                rb.mass = 10f;
                rb.linearDamping = 1f;
                rb.angularDamping = 8f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                rb.linearVelocity = FireDir * 80f;

                rb.excludeLayers |= (1 << 16);

                SphereCollider ShellCollider = shell.AddComponent<SphereCollider>();

                ShellCollider.enabled = true;
                ShellCollider.radius = 0.15f;
                ShellCollider.isTrigger = false;

                shell.layer = LayerMask.GetMask( "Grenade" );


                GameObject.Destroy( shell , 50f );

                return shell;

            }
            catch (Exception e)
            {

                Log.Info(e.Message);

                return null;

            }

        }

        internal void CreatAT4Explosion( Vector3 explosionPos)
        {

            Exiled.API.Features.Map.Explode(explosionPos, ProjectileType.FragGrenade);
            Exiled.API.Features.Map.ExplodeEffect(explosionPos + new Vector3(3, 0, 0), ProjectileType.FragGrenade);
            Exiled.API.Features.Map.ExplodeEffect(explosionPos + new Vector3(-3, 0, 0), ProjectileType.FragGrenade);
            Exiled.API.Features.Map.ExplodeEffect(explosionPos + new Vector3(0, 3, 0), ProjectileType.FragGrenade);
            Exiled.API.Features.Map.ExplodeEffect(explosionPos + new Vector3(0, -3, 0), ProjectileType.FragGrenade);
            Exiled.API.Features.Map.ExplodeEffect(explosionPos + new Vector3(0, 0, 3), ProjectileType.FragGrenade);
            Exiled.API.Features.Map.ExplodeEffect(explosionPos + new Vector3(0, 0, -3), ProjectileType.FragGrenade);

            foreach ( Player player in Player.List)
            {

                if (Physics.Raycast(explosionPos, (player.Position - explosionPos).normalized, out RaycastHit hit, ~((1 << 2) | (1 << 13))))
                {
                    continue;
                }

                if (player.IsDead) continue;

                float Distance = Vector3.Distance(player.Position, explosionPos);

                Footprint atkfootprint = _at4Holder.Footprint;

                var ExDamageHandler = new ExplosionDamageHandler(atkfootprint, (player.Position - explosionPos).normalized * 100f, 500f, 100, ExplosionType.Grenade);

                if ( Distance <= 8f)
                {

                    player.Hurt(ExDamageHandler);
                    

                }

            }

        }

    }

    internal class AT4ShellCollision : MonoBehaviour
    {

        private AT4Usage _at4Usage;
        private LayerMask _CLayers;

        public void Init( AT4Usage at4u , LayerMask la )
        {

            _at4Usage = at4u;
            _CLayers = la;

        }

        private void OnCollisionEnter( Collision collision)
        {

            if( collision.gameObject.layer == _CLayers || collision.gameObject.layer == 16 )
            {

                return;

            }

            Vector3 hitPoint = collision.contacts[0].point;

            bool StraightDamageApplied = false;
            Player hitPlayer = null;

            foreach ( ContactPoint contact in collision.contacts)
            {

                Player pl = Player.Get(contact.otherCollider);

                if ( pl != null )
                {

                    StraightDamageApplied = true;
                    hitPlayer = pl;
                    break;
                }

            }

            if( StraightDamageApplied)
            {

                Log.Info("straight damage");

                float Distance = Vector3.Distance(hitPlayer.Position, hitPoint);

                Footprint atkfootprint = _at4Usage._at4Holder.Footprint;

                var ExDamageHandler = new ExplosionDamageHandler(atkfootprint, (hitPlayer.Position - hitPoint).normalized * 100f, CorePlugin.GetModule<AT4Module>().Config.Damage, 100, ExplosionType.Grenade);

                hitPlayer.Hurt(ExDamageHandler);
            }

            _at4Usage.CreatAT4Explosion( hitPoint );

            Destroy(gameObject);

        }

        private void OnDestroy()
        {

            

        }

    }

    internal class AT4Bounder : MonoBehaviour
    {

        internal Player _boundPlayer;

        internal GameObject _at4Root;

        private readonly Vector3 _positionOffset = new Vector3(0f, 0f, 0f); // 炮筒高度|横向度|近远
        private readonly Vector3 _rotationOffset = new Vector3(0f, 0f, 0f);

         internal void Bindplayer( Player player )
        {

            _boundPlayer = player;

        }

        private void Update()
        {

            if (_boundPlayer == null || !_boundPlayer.IsConnected || !_boundPlayer.IsAlive)
            {

                AT4Usage usage = GetComponentInParent<AT4Usage>();
                usage?.AT4Unbind();

                Destroy(this);
                return;

            }

            UpdateAT4Transform();

        }

        private void UpdateAT4Transform()
        {

            if (_boundPlayer == null) return ;

            Vector3 targetWorldPos = _boundPlayer.CameraTransform.TransformPoint( _positionOffset );

            Quaternion ExtraRot = Quaternion.Euler(0f, 90f, 90f);

            Quaternion targetWorldRot = _boundPlayer.CameraTransform.rotation * Quaternion.Euler(_rotationOffset) * ExtraRot;

            _at4Root.transform.position = targetWorldPos;
            _at4Root.transform.rotation = targetWorldRot;

        }

        private void OnDestroy() {

            _boundPlayer = null;
            _at4Root = null;

            AT4Usage usage = GetComponentInParent<AT4Usage>();
            usage?.AT4Unbind();

            Destroy(this); 
        }

    }

}
