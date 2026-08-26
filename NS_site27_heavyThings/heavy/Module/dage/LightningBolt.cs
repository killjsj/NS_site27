using AdminToys;
using Exiled.API.Features.Toys;
using Mirror;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    [DisallowMultipleComponent]
    public class LightningBolt : MonoBehaviour
    {
        public Vector3 Destination = new(0f, -10f, 0f);

        public Transform Target;

        public int Segments = 14;

        public float StepSize = 0.6f;

        public float Thickness = 0.05f;

        public bool AllowReverse;

        public bool JoinCorners = true;

        public Color Color = new(0.75f, 0.85f, 1f, 1f);

        public bool Collidable;

        public byte Smoothing;

        public int Seed;

        public bool RandomizeSeed = true;

        public bool RebuildOnStart = true;

        public float FlickerInterval;

        public float FollowInterval = 0.05f;

        public float Lifetime;

        public Vector3 LocalDestination => Target == null ? Destination : transform.InverseTransformPoint(Target.position);

        public IReadOnlyList<Vector3> Points => _points;

        public IReadOnlyList<PrimitiveObjectToy> SpawnedSegments => _segments;

        public static LightningBolt Attach(GameObject parent, Vector3 worldTarget)
        {
            LightningBolt bolt = GetOrAdd(parent);
            bolt?.Strike(worldTarget);

            return bolt;
        }

        public static LightningBolt Attach(GameObject parent, Transform target)
        {
            LightningBolt bolt = GetOrAdd(parent);
            bolt?.Strike(target);

            return bolt;
        }

        public void Rebuild()
        {
            _nextFlicker = Time.time + FlickerInterval;
            Rebuild(RandomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : Seed);
        }

        public void Rebuild(int seed)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            Seed = seed;
            _destination = LocalDestination;

            BuildPath(_destination, seed);

            bool parented = TryGetComponent<NetworkIdentity>(out _);
            if (parented != _parented)
            {
                _parented = parented;
                ResizePool(0);
            }

            ResizePool(Mathf.Max(0, _points.Count - 1));
            ApplyLook();
            Layout();
        }

        public void Strike(Vector3 worldTarget)
        {
            Target = null;
            Destination = transform.InverseTransformPoint(worldTarget);
            Rebuild();
        }

        public void Strike(Transform target)
        {
            Target = target;
            Rebuild();
        }

        public void Clear()
        {
            ResizePool(0);
            _points.Clear();
        }

        private static LightningBolt GetOrAdd(GameObject parent)
        {
            if (parent == null)
            {
                Debug.LogError($"{nameof(LightningBolt)} needs a parent to attach to.");
                return null;
            }

            return parent.TryGetComponent(out LightningBolt bolt) ? bolt : parent.AddComponent<LightningBolt>();
        }

        private void Start()
        {
            if (RebuildOnStart && _segments.Count == 0)
            {
                Rebuild();
            }

            if (Lifetime > 0f)
            {
                Destroy(gameObject, Lifetime);
            }
        }

        private void Update()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (FlickerInterval > 0f && Time.time >= _nextFlicker)
            {
                Rebuild();
                return;
            }

            bool targetMoved = (LocalDestination - _destination).sqrMagnitude > 0.000001f;

            bool hostMoved = !_parented && (transform.position != _origin || transform.rotation != _originRotation);

            if (!targetMoved && !hostMoved)
            {
                return;
            }

            if (FollowInterval > 0f && Time.time < _nextFollow)
            {
                return;
            }

            _nextFollow = Time.time + FollowInterval;

            if (targetMoved)
            {
                Rebuild(Seed);
            }
            else
            {
                Layout();
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void BuildPath(Vector3 destination, int seed)
        {
            _points.Clear();

            int segments = Mathf.Max(2, Segments);
            float distance = destination.magnitude;
            if (distance < Epsilon)
            {
                return;
            }

            Vector3 forward = destination / distance;
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude < Epsilon)
            {
                right = Vector3.Cross(forward, Vector3.forward);
            }

            right.Normalize();
            Vector3 up = Vector3.Cross(right, forward);

            System.Random random = new(seed);
            Vector2 drift = Vector2.zero;
            int previous = -1;

            _points.Add(Vector3.zero);

            for (int i = 1; i < segments; i++)
            {
                int direction = random.Next(Directions.Length);
                if (!AllowReverse && previous >= 0)
                {
                    for (int attempt = 0; attempt < 8 && direction == (previous ^ 1); attempt++)
                    {
                        direction = random.Next(Directions.Length);
                    }
                }

                previous = direction;
                drift += Directions[direction] * StepSize;

                float progress = (float)i / segments;
                _points.Add((destination * progress) + (((right * drift.x) + (up * drift.y)) * (1f - progress)));
            }

            _points.Add(destination);
        }
        private void Layout()
        {
            for (int i = 0; i < _segments.Count && i + 1 < _points.Count; i++)
            {
                Vector3 from = _parented ? _points[i] : transform.TransformPoint(_points[i]);
                Vector3 to = _parented ? _points[i + 1] : transform.TransformPoint(_points[i + 1]);

                Stretch(_segments[i], from, to);
            }

            _origin = transform.position;
            _originRotation = transform.rotation;
        }

        private void Stretch(PrimitiveObjectToy segment, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;

            Transform t = segment.transform;
            t.SetLocalPositionAndRotation((from + to) * 0.5f, length > Epsilon ? Quaternion.FromToRotation(Vector3.forward, delta) : Quaternion.identity);
            t.localScale = new Vector3(Thickness, Thickness, length + (JoinCorners ? Thickness : 0f));

            segment.NetworkPosition = t.localPosition;
            segment.NetworkRotation = t.localRotation;
            segment.NetworkScale = t.localScale;
        }

        private void ApplyLook()
        {
            PrimitiveFlags flags = PrimitiveFlags.Visible | (Collidable ? PrimitiveFlags.Collidable : PrimitiveFlags.None);

            foreach (PrimitiveObjectToy segment in _segments)
            {
                if (segment.MaterialColor != Color)
                {
                    segment.NetworkMaterialColor = Color;
                }

                if (segment.PrimitiveFlags != flags)
                {
                    segment.NetworkPrimitiveFlags = flags;
                }

                if (segment.MovementSmoothing != Smoothing)
                {
                    segment.NetworkMovementSmoothing = Smoothing;
                }
            }
        }

        private void ResizePool(int count)
        {
            _ = _segments.RemoveAll(segment => segment == null);

            for (int i = _segments.Count - 1; i >= count; i--)
            {
                NetworkServer.Destroy(_segments[i].gameObject);
                _segments.RemoveAt(i);
            }

            while (_segments.Count < count)
            {
                PrimitiveObjectToy segment = CreateSegment();
                if (segment == null)
                {
                    return;
                }

                _segments.Add(segment);
            }
        }

        private PrimitiveObjectToy CreateSegment()
        {
            PrimitiveObjectToy prefab = Primitive.Prefab;
            if (prefab == null)
            {
                Debug.LogError($"{nameof(LightningBolt)} could not find the primitive toy prefab.");
                return null;
            }

            PrimitiveObjectToy segment = Instantiate(prefab);
            segment.name = $"LightningSegment {_segments.Count + 1}";

            if (_parented)
            {
                segment.transform.SetParent(transform, false);
            }

            segment.NetworkPrimitiveType = PrimitiveType.Cube;
            segment.NetworkMaterialColor = Color;
            segment.NetworkPrimitiveFlags = PrimitiveFlags.Visible | (Collidable ? PrimitiveFlags.Collidable : PrimitiveFlags.None);
            segment.NetworkMovementSmoothing = Smoothing;

            NetworkServer.Spawn(segment.gameObject);

            return segment;
        }

        private static readonly Vector2[] Directions = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        private const float Epsilon = 0.0001f;

        private readonly List<Vector3> _points = new();
        private readonly List<PrimitiveObjectToy> _segments = new();

        private Vector3 _destination;
        private Vector3 _origin;
        private Quaternion _originRotation = Quaternion.identity;
        private float _nextFlicker;
        private float _nextFollow;
        private bool _parented;
    }
}
