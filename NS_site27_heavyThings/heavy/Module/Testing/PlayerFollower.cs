using Exiled.API.Features;
using FacilityNavigation;
using Mirror;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp049.Zombies;
using RelativePositioning;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Next_generationSite_27.UnionP
{
    public class PlayerFollower : MonoBehaviour
    {
        private const float DefaultMaxDistance = 20f;
        private const float DefaultMinDistance = 0.6f;
        private const float DefaultSpeed = 30f;

        private const float RepathInterval = 0.4f;
        private const float LookAheadDistance = 2.5f; private const float StuckCheckInterval = 0.8f;
        private const float StuckMoveThreshold = 0.25f;
        private const int MaxStuckAttempts = 3;

        public static bool DebugDrawPath = true;

        private ReferenceHub _hub;
        private ReferenceHub _hubToFollow;

        private float _maxDistance = DefaultMaxDistance;
        private float _minDistance = DefaultMinDistance;
        private float _speed = DefaultSpeed;

        private readonly List<Vector3> _waypoints = new(48);
        private bool _directLine;
        private float _nextRepathTime;
        private Vector3 _lastStuckSample;
        private float _stuckTimer;
        private int _stuckAttempts;
        public Func<ReferenceHub, Vector3> TargetPos = null;

        public void Init(ReferenceHub playerToFollow, float maxDistance = 20f, float minDistance = 0.6f, float speed = 30f)
        {
            _hub = GetComponent<ReferenceHub>();
            _hubToFollow = playerToFollow;
            _maxDistance = maxDistance;
            _minDistance = minDistance;
            _speed = speed;
            _lastStuckSample = transform.position;
        }

        private void Update()
        {
            if (!NetworkServer.active ||
                _hubToFollow == null ||
                _hub == null ||
                _hub.roleManager.CurrentRole is not IFpcRole)
            {
                Destroy(this);
                return;
            }
            if (_hubToFollow.roleManager.CurrentRole is not IFpcRole)
            {
                _hubToFollow = OwnerHub;
            }
            IFpcRole fpc = (IFpcRole)_hub.roleManager.CurrentRole;

            if (_hub.roleManager.CurrentRole is ZombieRole zombieRole &&
                zombieRole.SubroutineModule.TryGetSubroutine<ZombieConsumeAbility>(out var consume) &&
                consume.IsInProgress)
            {
                return;
            }

            Vector3 goal = TargetPos?.Invoke(_hubToFollow) ?? _hubToFollow.transform.position;
            Vector3 pos = transform.position;
            float dist = Vector3.Distance(pos, goal);

            if (dist > _maxDistance)
            {
                _hubToFollow = OwnerHub;

                if (_hub.roleManager.CurrentRole is IFpcRole fpcr)
                {
                    fpcr.FpcModule.ServerOverridePosition(OwnerHub.transform.position);
                }

                ResetPath();
                _stuckAttempts = 0;
                _nextRepathTime = 0f;
                _lastStuckSample = OwnerHub.transform.position;

                OnStuck?.Invoke();
                return;
            }

            if (dist < _minDistance)
            {
                ResetPath();
                FaceTarget(fpc, goal);
                return;
            }
            float speed = fpc.FpcModule.VelocityForState(PlayerMovementState.Sprinting, false);


            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + RepathInterval;
                RebuildPath(pos, goal);
            }

            Vector3 moveTarget = GetLookAheadPoint(pos, goal);
            Step(fpc, pos, moveTarget, speed, goal);
            DetectStuck(pos);
        }

        private void RebuildPath(Vector3 from, Vector3 to)
        {
            ResetPath();

            _directLine = !NavMesh.Raycast(from, to, out _, NavMesh.AllAreas);
            if (_directLine)
            {
                _waypoints.Add(to);
                return;
            }

            if (PathModule.TryFindPathAtoB(from, to, out List<Vector3> corners, out _))
            {
                for (int i = 1; i < corners.Count; i++)
                {
                    _waypoints.Add(corners[i]);
                }
            }

            if (_waypoints.Count == 0)
            {
                _waypoints.Add(to);
            }

            if (DebugDrawPath && _waypoints.Count > 1)
            {
                Draw.Path(_waypoints.ToArray(), Color.red, RepathInterval + 0.001f);
            }
        }

        private Vector3 GetLookAheadPoint(Vector3 pos, Vector3 fallbackGoal)
        {
            if (_waypoints.Count == 0)
            {
                return fallbackGoal;
            }

            float accumulated = 0f;
            Vector3 previousPoint = pos;

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Vector3 currentPoint = _waypoints[i];
                float segmentLength = Vector3.Distance(previousPoint, currentPoint);
                accumulated += segmentLength;

                if (accumulated >= LookAheadDistance)
                {
                    float overshoot = accumulated - LookAheadDistance;
                    float t = 1f - (overshoot / segmentLength);
                    return Vector3.Lerp(previousPoint, currentPoint, t);
                }

                previousPoint = currentPoint;
            }

            return _waypoints[_waypoints.Count - 1];
        }

        private void Step(IFpcRole fpc, Vector3 pos, Vector3 targetPos, float speed, Vector3 faceTarget)
        {
            if (DebugDrawPath)
            {
                Draw.Line(pos, targetPos, Color.green, 0.05f);
            }

            Vector3 dir = targetPos - pos;
            dir.y = 0f;

            float magnitude = dir.magnitude;
            if (magnitude > 0.05f)
            {
                dir /= magnitude;
                Vector3 next = pos + (dir * (speed * Time.deltaTime));
                fpc.FpcModule.Motor.ReceivedPosition = new RelativePosition(next);
            }
            FaceTarget(fpc, faceTarget);
        }

        private void FaceTarget(IFpcRole fpc, Vector3 targetPos)
        {
            Vector3 dirToTarget = targetPos - transform.position;
            dirToTarget.y = 0f;
            if (dirToTarget.sqrMagnitude < 0.01f)
            {
                return;
            }

            float yaw = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;
            if (yaw < 0f)
            {
                yaw += 360f;
            }

            fpc.FpcModule.MouseLook.CurrentHorizontal = yaw;
        }
        public ReferenceHub OwnerHub { get; set; }
        public System.Action OnStuck { get; set; }

        private void DetectStuck(Vector3 pos)
        {
            if ((pos - _lastStuckSample).sqrMagnitude >= StuckMoveThreshold * StuckMoveThreshold)
            {
                _lastStuckSample = pos;
                _stuckTimer = 0f;
                _stuckAttempts = 0;
                return;
            }

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer < StuckCheckInterval)
            {
                return;
            }

            _stuckTimer = 0f;
            _lastStuckSample = pos;
            _stuckAttempts++;

            if (_stuckAttempts >= MaxStuckAttempts)
            {
                if (OwnerHub != null)
                {
                    _hubToFollow = OwnerHub;

                    if (_hub.roleManager.CurrentRole is IFpcRole fpcr)
                    {
                        fpcr.FpcModule.ServerOverridePosition(OwnerHub.transform.position);
                    }

                    ResetPath();
                    _stuckAttempts = 0;
                    _nextRepathTime = 0f;
                    _lastStuckSample = OwnerHub.transform.position;

                    OnStuck?.Invoke();
                    return;
                }
                NetworkServer.Destroy(_hub.gameObject);
                return;
            }

            _nextRepathTime = 0f;
        }

        private Vector3 FindClosestWaypointAhead(Vector3 pos)
        {
            Vector3 best = pos;
            float minSqr = float.MaxValue;
            foreach (var wp in _waypoints)
            {
                Vector3 toWp = wp - pos;
                toWp.y = 0f;
                if (Vector3.Dot(transform.forward, toWp.normalized) > 0.2f)
                {
                    float sqr = toWp.sqrMagnitude;
                    if (sqr < minSqr)
                    {
                        minSqr = sqr;
                        best = wp;
                    }
                }
            }
            return best;
        }

        private void ResetPath()
        {
            _waypoints.Clear();
            _directLine = false;
        }
    }
}