using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace UnityEngine.AI
{
    public enum CollectObjects
    {
        All = 0,
        Volume = 1,
        Children = 2,
    }

    [ExecuteAlways]
    [DefaultExecutionOrder(-102)]
    [AddComponentMenu("Navigation/NavMeshSurface", 30)]
    [HelpURL("https://github.com/Unity-Technologies/NavMeshComponents#documentation-draft")]
    public class NavMeshSurface : MonoBehaviour
    {
        public static readonly List<Mesh> Blacklist = new();

        [field: SerializeField]
        public int agentTypeID { get; set; }

        [field: SerializeField]
        public CollectObjects collectObjects { get; set; } = CollectObjects.All;

        [SerializeField]
        private Vector3 m_Size = new(10.0f, 10.0f, 10.0f);
        public Vector3 size { get => m_Size; set => m_Size = value; }

        [SerializeField]
        private Vector3 m_Center = new(0, 2.0f, 0);
        public Vector3 center { get => m_Center; set => m_Center = value; }

        [SerializeField]
        private LayerMask m_LayerMask = ~0;
        public LayerMask layerMask { get => m_LayerMask; set => m_LayerMask = value; }

        [field: SerializeField]
        public NavMeshCollectGeometry useGeometry { get; set; } = NavMeshCollectGeometry.RenderMeshes;

        [field: SerializeField]
        public int defaultArea { get; set; }

        [field: SerializeField]
        public bool ignoreNavMeshAgent { get; set; } = true;

        [field: SerializeField]
        public bool ignoreNavMeshObstacle { get; set; } = true;

        [field: SerializeField]
        public bool overrideTileSize { get; set; }

        [field: SerializeField]
        public int tileSize { get; set; } = 128;

        [field: SerializeField]
        public bool overrideVoxelSize { get; set; }

        [field: SerializeField]
        public float voxelSize { get; set; }

        [field: SerializeField]
        public bool buildHeightMesh { get; set; }

        [field: UnityEngine.Serialization.FormerlySerializedAs("m_BakedNavMeshData")]
        [field: SerializeField]
        public NavMeshData navMeshData { get; set; }

        private NavMeshDataInstance m_NavMeshDataInstance;
        private Vector3 m_LastPosition = Vector3.zero;
        private Quaternion m_LastRotation = Quaternion.identity;

        public static List<NavMeshSurface> activeSurfaces { get; } = new List<NavMeshSurface>();

        private void OnEnable()
        {
            Register(this);
            AddData();
        }

        private void OnDisable()
        {
            RemoveData();
            Unregister(this);
        }

        public void AddData()
        {
#if UNITY_EDITOR
            var isInPreviewScene = EditorSceneManager.IsPreviewSceneObject(this);
            var isPrefab = isInPreviewScene || EditorUtility.IsPersistent(this);
            if (isPrefab)
            {
                                                return;
            }
#endif
            if (m_NavMeshDataInstance.valid)
            {
                return;
            }

            if (navMeshData != null)
            {
                m_NavMeshDataInstance = NavMesh.AddNavMeshData(navMeshData, transform.position, transform.rotation);
                m_NavMeshDataInstance.owner = this;
            }

            m_LastPosition = transform.position;
            m_LastRotation = transform.rotation;
        }

        public void RemoveData()
        {
            m_NavMeshDataInstance.Remove();
            m_NavMeshDataInstance = new NavMeshDataInstance();
        }

        public NavMeshBuildSettings GetBuildSettings()
        {
            var buildSettings = new NavMeshBuildSettings()
            {
                agentTypeID = 0,
                agentClimb = 0.2f,
                agentHeight = 1.7f,
                agentRadius = 0.5f,
                agentSlope = 45f,
            };

            if (buildSettings.agentTypeID == -1)
            {
                Debug.LogWarning("No build settings for agent type ID " + agentTypeID, this);
                buildSettings.agentTypeID = agentTypeID;
            }

            if (overrideTileSize)
            {
                buildSettings.overrideTileSize = true;
                buildSettings.tileSize = tileSize;
            }
            if (overrideVoxelSize)
            {
                buildSettings.overrideVoxelSize = true;
                buildSettings.voxelSize = voxelSize;
            }
            return buildSettings;
        }

        public void BuildNavMesh()
        {
            var sources = CollectSources();

            var sourcesBounds = new Bounds(m_Center, Abs(m_Size));
            if (collectObjects is CollectObjects.All or CollectObjects.Children)
            {
                sourcesBounds = CalculateWorldBounds(sources);
            }

            var data = NavMeshBuilder.BuildNavMeshData(GetBuildSettings(),
                    sources, sourcesBounds, transform.position, transform.rotation);

            if (data != null)
            {
                data.name = gameObject.name;
                RemoveData();
                navMeshData = data;
                if (isActiveAndEnabled)
                {
                    AddData();
                }
            }
        }

        public AsyncOperation UpdateNavMesh(NavMeshData data)
        {
            var sources = CollectSources();

            var sourcesBounds = new Bounds(m_Center, Abs(m_Size));
            if (collectObjects is CollectObjects.All or CollectObjects.Children)
            {
                sourcesBounds = CalculateWorldBounds(sources);
            }

            return NavMeshBuilder.UpdateNavMeshDataAsync(data, GetBuildSettings(), sources, sourcesBounds);
        }

        private static void Register(NavMeshSurface surface)
        {
#if UNITY_EDITOR
            var isInPreviewScene = EditorSceneManager.IsPreviewSceneObject(surface);
            var isPrefab = isInPreviewScene || EditorUtility.IsPersistent(surface);
            if (isPrefab)
            {
                                                return;
            }
#endif
            if (activeSurfaces.Count == 0)
            {
                NavMesh.onPreUpdate += UpdateActive;
            }

            if (!activeSurfaces.Contains(surface))
            {
                activeSurfaces.Add(surface);
            }
        }

        private static void Unregister(NavMeshSurface surface)
        {
            _ = activeSurfaces.Remove(surface);

            if (activeSurfaces.Count == 0)
            {
                NavMesh.onPreUpdate -= UpdateActive;
            }
        }

        private static void UpdateActive()
        {
            for (var i = 0; i < activeSurfaces.Count; ++i)
            {
                activeSurfaces[i].UpdateDataIfTransformChanged();
            }
        }

        private void AppendModifierVolumes(ref List<NavMeshBuildSource> sources)
        {
#if UNITY_EDITOR
            var myStage = StageUtility.GetStageHandle(gameObject);
            if (!myStage.IsValid())
                return;
#endif
            List<NavMeshModifierVolume> modifiers;
            if (collectObjects == CollectObjects.Children)
            {
                modifiers = new List<NavMeshModifierVolume>(GetComponentsInChildren<NavMeshModifierVolume>());
                modifiers.RemoveAll(x => !x.isActiveAndEnabled);
            }
            else
            {
                modifiers = NavMeshModifierVolume.activeModifiers;
            }

            foreach (var m in modifiers)
            {
                if ((m_LayerMask & (1 << m.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (!m.AffectsAgentType(agentTypeID))
                {
                    continue;
                }
#if UNITY_EDITOR
                if (!myStage.Contains(m.gameObject))
                    continue;
#endif
                var mcenter = m.transform.TransformPoint(m.center);
                var scale = m.transform.lossyScale;
                var msize = new Vector3(m.size.x * Mathf.Abs(scale.x), m.size.y * Mathf.Abs(scale.y), m.size.z * Mathf.Abs(scale.z));

                var src = new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(mcenter, m.transform.rotation, Vector3.one),
                    size = msize,
                    area = m.area
                };
                sources.Add(src);
            }
        }

        private List<NavMeshBuildSource> CollectSources()
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();

            List<NavMeshModifier> modifiers;
            if (collectObjects == CollectObjects.Children)
            {
                modifiers = new List<NavMeshModifier>(GetComponentsInChildren<NavMeshModifier>());
                modifiers.RemoveAll(x => !x.isActiveAndEnabled);
            }
            else
            {
                modifiers = NavMeshModifier.activeModifiers;
            }

            foreach (var m in modifiers)
            {
                if ((m_LayerMask & (1 << m.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (!m.AffectsAgentType(agentTypeID))
                {
                    continue;
                }

                var markup = new NavMeshBuildMarkup
                {
                    root = m.transform,
                    overrideArea = m.overrideArea,
                    area = m.area,
                    ignoreFromBuild = m.ignoreFromBuild
                };
                markups.Add(markup);
            }

#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                if (m_CollectObjects == CollectObjects.All)
                {
                    UnityEditor.AI.NavMeshBuilder.CollectSourcesInStage(
                        null, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, gameObject.scene, sources);
                }
                else if (m_CollectObjects == CollectObjects.Children)
                {
                    UnityEditor.AI.NavMeshBuilder.CollectSourcesInStage(
                        transform, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, gameObject.scene, sources);
                }
                else if (m_CollectObjects == CollectObjects.Volume)
                {
                    Matrix4x4 localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                    var worldBounds = GetWorldBounds(localToWorld, new Bounds(m_Center, m_Size));

                    UnityEditor.AI.NavMeshBuilder.CollectSourcesInStage(
                        worldBounds, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, gameObject.scene, sources);
                }
            }
            else
#endif
            {
                if (collectObjects == CollectObjects.All)
                {
                    NavMeshBuilder.CollectSources(null, m_LayerMask, useGeometry, defaultArea, markups, sources);
                }
                else if (collectObjects == CollectObjects.Children)
                {
                    NavMeshBuilder.CollectSources(transform, m_LayerMask, useGeometry, defaultArea, markups, sources);
                }
                else if (collectObjects == CollectObjects.Volume)
                {
                    Matrix4x4 localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                    var worldBounds = GetWorldBounds(localToWorld, new Bounds(m_Center, m_Size));
                    NavMeshBuilder.CollectSources(worldBounds, m_LayerMask, useGeometry, defaultArea, markups, sources);
                }
            }

            if (ignoreNavMeshAgent)
            {
                sources.RemoveAll((x) => x.component != null && x.component.gameObject.GetComponent<NavMeshAgent>() != null);
            }

            if (ignoreNavMeshObstacle)
            {
                sources.RemoveAll((x) => x.component != null && x.component.gameObject.GetComponent<NavMeshObstacle>() != null);
            }

            AppendModifierVolumes(ref sources);

            return sources;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static Bounds GetWorldBounds(Matrix4x4 mat, Bounds bounds)
        {
            var absAxisX = Abs(mat.MultiplyVector(Vector3.right));
            var absAxisY = Abs(mat.MultiplyVector(Vector3.up));
            var absAxisZ = Abs(mat.MultiplyVector(Vector3.forward));
            var worldPosition = mat.MultiplyPoint(bounds.center);
            var worldSize = (absAxisX * bounds.size.x) + (absAxisY * bounds.size.y) + (absAxisZ * bounds.size.z);
            return new Bounds(worldPosition, worldSize);
        }

        private Bounds CalculateWorldBounds(List<NavMeshBuildSource> sources)
        {
            Matrix4x4 worldToLocal = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            worldToLocal = worldToLocal.inverse;

            var result = new Bounds();
            foreach (var src in sources)
            {
                if (Blacklist.Contains(src.sourceObject))
                {
                    continue;
                }

                switch (src.shape)
                {
                    case NavMeshBuildSourceShape.Mesh:
                        {
                            var m = src.sourceObject as Mesh;
                            result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, m.bounds));
                            break;
                        }
                    /*                    case NavMeshBuildSourceShape.Terrain:
                                            {
                                                                                                var t = src.sourceObject as TerrainData;
                                                result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(0.5f * t.size, t.size)));
                                                break;
                                            }*/
                    case NavMeshBuildSourceShape.Box:
                    case NavMeshBuildSourceShape.Sphere:
                    case NavMeshBuildSourceShape.Capsule:
                    case NavMeshBuildSourceShape.ModifierBox:
                        result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(Vector3.zero, src.size)));
                        break;
                }
            }
            result.Expand(0.1f);
            return result;
        }

        private bool HasTransformChanged()
        {
            return m_LastPosition != transform.position || m_LastRotation != transform.rotation;
        }

        private void UpdateDataIfTransformChanged()
        {
            if (HasTransformChanged())
            {
                RemoveData();
                AddData();
            }
        }

#if UNITY_EDITOR
        bool UnshareNavMeshAsset()
        {
                        if (m_NavMeshData == null)
                return false;

                        var isInPreviewScene = EditorSceneManager.IsPreviewSceneObject(this);
            var isPersistentObject = EditorUtility.IsPersistent(this);
            if (isInPreviewScene || isPersistentObject)
                return false;

                        var prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(this) as NavMeshSurface;
            if (prefab != null && prefab.navMeshData == navMeshData)
                return false;

                        for (var i = 0; i < s_NavMeshSurfaces.Count; ++i)
            {
                var surface = s_NavMeshSurfaces[i];
                if (surface != this && surface.m_NavMeshData == m_NavMeshData)
                    return true;
            }

                        return false;
        }

        void OnValidate()
        {
            if (UnshareNavMeshAsset())
            {
                Debug.LogWarning("Duplicating NavMeshSurface does not duplicate the referenced navmesh data", this);
                m_NavMeshData = null;
            }

            var settings = NavMesh.GetSettingsByID(m_AgentTypeID);
            if (settings.agentTypeID != -1)
            {
                                const float kMinVoxelSize = 0.01f;
                if (!m_OverrideVoxelSize)
                    m_VoxelSize = settings.agentRadius / 3.0f;
                if (m_VoxelSize < kMinVoxelSize)
                    m_VoxelSize = kMinVoxelSize;

                                const int kMinTileSize = 16;
                const int kMaxTileSize = 1024;
                const int kDefaultTileSize = 256;

                if (!m_OverrideTileSize)
                    m_TileSize = kDefaultTileSize;
                                if (m_TileSize < kMinTileSize)
                    m_TileSize = kMinTileSize;
                if (m_TileSize > kMaxTileSize)
                    m_TileSize = kMaxTileSize;
            }
        }
#endif
    }
}