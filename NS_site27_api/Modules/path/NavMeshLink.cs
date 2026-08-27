using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.AI;

#pragma warning disable IDE1006 
namespace Unity.AI.Navigation
{
        [ExecuteAlways]
    [DefaultExecutionOrder(-101)]
    [AddComponentMenu("Navigation/NavMesh Link", 33)]
    public partial class NavMeshLink : MonoBehaviour
    {
                                [SerializeField, HideInInspector]
        byte m_SerializedVersion = 0;

        [SerializeField]
        int m_AgentTypeID;

        [SerializeField]
        Vector3 m_StartPoint = new(0.0f, 0.0f, -2.5f);

        [SerializeField]
        Vector3 m_EndPoint = new(0.0f, 0.0f, 2.5f);

        [SerializeField]
        Transform m_StartTransform;

        [SerializeField]
        Transform m_EndTransform;

        [SerializeField]
        bool m_Activated = true;

        [SerializeField]
        float m_Width;

                                                [SerializeField]
        [Min(0f)]
        float m_CostModifier = -1f;

        [SerializeField]
        bool m_IsOverridingCost = false;

        [SerializeField]
        bool m_Bidirectional = true;

        [SerializeField]
        bool m_AutoUpdatePosition;

        [SerializeField]
        int m_Area;

#if UNITY_EDITOR
        int m_LastArea;
#endif

                public int agentTypeID
        {
            get => m_AgentTypeID;
            set
            {
                if (value == m_AgentTypeID)
                    return;

                m_AgentTypeID = value;
                UpdateLink();
            }
        }

                                public Vector3 startPoint
        {
            get => m_StartPoint;
            set
            {
                if (value == m_StartPoint)
                    return;

                m_StartPoint = value;
                UpdateLink();
            }
        }

                                public Vector3 endPoint
        {
            get => m_EndPoint;
            set
            {
                if (value == m_EndPoint)
                    return;

                m_EndPoint = value;
                UpdateLink();
            }
        }

                        public Transform startTransform
        {
            get => m_StartTransform;
            set
            {
                if (value == m_StartTransform)
                    return;

                m_StartTransform = value;

                UpdateLink();
            }
        }

                        public Transform endTransform
        {
            get => m_EndTransform;
            set
            {
                if (value == m_EndTransform)
                    return;

                m_EndTransform = value;

                UpdateLink();
            }
        }

                        public float width
        {
            get => m_Width;
            set
            {
                if (value.Equals(m_Width))
                    return;

                m_Width = value;
                UpdateLink();
            }
        }

                                public float costModifier
        {
            get => m_IsOverridingCost ? m_CostModifier : -m_CostModifier;
            set
            {
                var shouldOverride = value >= 0f;
                if (value.Equals(costModifier) && shouldOverride == m_IsOverridingCost)
                    return;

                m_IsOverridingCost = shouldOverride;
                m_CostModifier = Mathf.Abs(value);
                UpdateLink();
            }
        }

                        public bool bidirectional
        {
            get => m_Bidirectional;
            set
            {
                if (value == m_Bidirectional)
                    return;

                m_Bidirectional = value;
                UpdateLink();
            }
        }

                        public bool autoUpdate
        {
            get => m_AutoUpdatePosition;
            set
            {
                if (value == m_AutoUpdatePosition)
                    return;

                m_AutoUpdatePosition = value;

                if (m_AutoUpdatePosition)
                    AddTracking(this);
                else
                    RemoveTracking(this);
            }
        }

                public int area
        {
            get => m_Area;
            set
            {
                if (value == m_Area)
                    return;

                m_Area = value;
                UpdateLink();
            }
        }

                        public bool activated
        {
            get => m_Activated;
            set
            {
                m_Activated = value;
                NavMesh.SetLinkActive(m_LinkInstance, m_Activated);
            }
        }

                        public bool occupied => NavMesh.IsLinkOccupied(m_LinkInstance);

        NavMeshLinkInstance m_LinkInstance;

        bool m_StartTransformWasEmpty = true;
        bool m_EndTransformWasEmpty = true;

        Vector3 m_LastStartWorldPosition = Vector3.positiveInfinity;
        Vector3 m_LastEndWorldPosition = Vector3.positiveInfinity;
        Vector3 m_LastPosition = Vector3.positiveInfinity;
        Quaternion m_LastRotation = Quaternion.identity;

        static readonly List<NavMeshLink> s_Tracked = new();

#if UNITY_EDITOR
        bool m_DelayEndpointUpgrade;
        static string s_LastWarnedPrefab;
        static double s_NextPrefabWarningTime;
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ClearTrackedList()
        {
            NavMesh.onPreUpdate -= UpdateTrackedInstances;
            s_Tracked.Clear();
        }

        void UpgradeSerializedVersion()
        {
            if (m_SerializedVersion < 1)
            {
#if UNITY_EDITOR
                if (!StartEndpointUpgrade())
                    return;
#endif
                m_SerializedVersion = 1;
                m_IsOverridingCost = m_CostModifier >= 0f;
                m_CostModifier = Mathf.Abs(m_CostModifier);

                if (m_StartTransform == gameObject.transform)
                    m_StartTransform = null;

                if (m_EndTransform == gameObject.transform)
                    m_EndTransform = null;
            }
        }

                void Awake()
        {
            UpgradeSerializedVersion();
#if UNITY_EDITOR
            m_LastArea = m_Area;
#endif
        }

        void OnEnable()
        {
            AddLink();
            if (m_AutoUpdatePosition && NavMesh.IsLinkValid(m_LinkInstance))
                AddTracking(this);
        }

        void OnDisable()
        {
            RemoveTracking(this);
            NavMesh.RemoveLink(m_LinkInstance);
        }

                public void UpdateLink()
        {
            if (!isActiveAndEnabled)
                return;

            NavMesh.RemoveLink(m_LinkInstance);
            AddLink();
        }

        static void AddTracking(NavMeshLink link)
        {
#if UNITY_EDITOR
            if (s_Tracked.Contains(link))
            {
                Debug.LogError("Link is already tracked: " + link);
                return;
            }
#endif
            if (s_Tracked.Count == 0)
                NavMesh.onPreUpdate += UpdateTrackedInstances;

            s_Tracked.Add(link);

            link.RecordEndpointTransforms();
        }

        static void RemoveTracking(NavMeshLink link)
        {
            s_Tracked.Remove(link);

            if (s_Tracked.Count == 0)
                NavMesh.onPreUpdate -= UpdateTrackedInstances;
        }

                                internal void GetWorldPositions(
            out Vector3 worldStartPosition,
            out Vector3 worldEndPosition)
        {
            var startIsLocal = m_StartTransform == null;
            var endIsLocal = m_EndTransform == null;
            var toWorld = startIsLocal || endIsLocal ? LocalToWorldUnscaled() : Matrix4x4.identity;

            worldStartPosition = startIsLocal ? toWorld.MultiplyPoint3x4(m_StartPoint) : m_StartTransform.position;
            worldEndPosition = endIsLocal ? toWorld.MultiplyPoint3x4(m_EndPoint) : m_EndTransform.position;
        }

                                internal void GetLocalPositions(
            out Vector3 localStartPosition,
            out Vector3 localEndPosition)
        {
            var startIsLocal = m_StartTransform == null;
            var endIsLocal = m_EndTransform == null;
            var toLocal = startIsLocal && endIsLocal ? Matrix4x4.identity : LocalToWorldUnscaled().inverse;

            localStartPosition = startIsLocal ? m_StartPoint : toLocal.MultiplyPoint3x4(m_StartTransform.position);
            localEndPosition = endIsLocal ? m_EndPoint : toLocal.MultiplyPoint3x4(m_EndTransform.position);
        }

        void AddLink()
        {
#if UNITY_EDITOR
            if (NavMesh.IsLinkValid(m_LinkInstance))
            {
                Debug.LogError("Link is already added: " + this);
                return;
            }
#endif
            GetLocalPositions(out var localStartPosition, out var localEndPosition);
            var link = new NavMeshLinkData
            {
                startPosition = localStartPosition,
                endPosition = localEndPosition,
                width = m_Width,
                costModifier = costModifier,
                bidirectional = m_Bidirectional,
                area = m_Area,
                agentTypeID = m_AgentTypeID,
            };
            m_LinkInstance = NavMesh.AddLink(link, transform.position, transform.rotation);
            if (NavMesh.IsLinkValid(m_LinkInstance))
            {
                NavMesh.SetLinkOwner(m_LinkInstance, this);
                NavMesh.SetLinkActive(m_LinkInstance, m_Activated);
            }

            m_LastPosition = transform.position;
            m_LastRotation = transform.rotation;
#if UNITY_EDITOR
            m_LastArea = m_Area;
#endif
            RecordEndpointTransforms();

            GetWorldPositions(out m_LastStartWorldPosition, out m_LastEndWorldPosition);
        }

        internal void RecordEndpointTransforms()
        {
            m_StartTransformWasEmpty = m_StartTransform == null;
            m_EndTransformWasEmpty = m_EndTransform == null;
        }

        internal bool HaveTransformsChanged()
        {
            var startIsLocal = m_StartTransform == null;
            var endIsLocal = m_EndTransform == null;

            if (startIsLocal && endIsLocal &&
                m_StartTransformWasEmpty && m_EndTransformWasEmpty &&
                transform.position == m_LastPosition && transform.rotation == m_LastRotation)
                return false;

            var toWorld = startIsLocal || endIsLocal ? LocalToWorldUnscaled() : Matrix4x4.identity;

            var startWorldPos = startIsLocal ? toWorld.MultiplyPoint3x4(m_StartPoint) : m_StartTransform.position;
            if (startWorldPos != m_LastStartWorldPosition)
                return true;

            var endWorldPos = endIsLocal ? toWorld.MultiplyPoint3x4(m_EndPoint) : m_EndTransform.position;
            return endWorldPos != m_LastEndWorldPosition;
        }

        internal Matrix4x4 LocalToWorldUnscaled()
        {
            return Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        }

        void OnDidApplyAnimationProperties()
        {
            UpdateLink();
        }

        static void UpdateTrackedInstances()
        {
            foreach (var instance in s_Tracked)
            {
                if (instance.HaveTransformsChanged())
                    instance.UpdateLink();

                instance.RecordEndpointTransforms();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
                        UpgradeSerializedVersion();

            m_Width = Mathf.Max(0.0f, m_Width);

            if (!NavMesh.IsLinkValid(m_LinkInstance) && (m_LastArea != 1 || m_Area == 1))
                return;

            UpdateLink();

            if (!m_AutoUpdatePosition)
            {
                RemoveTracking(this);
            }
            else if (!s_Tracked.Contains(this))
            {
                AddTracking(this);
            }

            m_LastArea = m_Area;
        }

        void Reset()
        {
            UpgradeSerializedVersion();
        }

        bool StartEndpointUpgrade()
        {
            m_DelayEndpointUpgrade =
                (m_StartTransform != null &&
                    m_StartTransform != gameObject.transform &&
                    m_StartPoint.sqrMagnitude > 0.0001f)
                || (m_EndTransform != null &&
                    m_EndTransform != gameObject.transform &&
                    m_EndPoint.sqrMagnitude > 0.0001f);

            if (m_DelayEndpointUpgrade)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(this))
                {
                    var isInstance = PrefabUtility.IsPartOfPrefabInstance(this);
                    var prefabPath = isInstance
                        ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject)
                        : AssetDatabase.GetAssetPath(gameObject);

                    if ((prefabPath != s_LastWarnedPrefab
                            || EditorApplication.timeSinceStartup > s_NextPrefabWarningTime)
                        && prefabPath != "")
                    {
                        var prefabToPing = AssetDatabase.LoadAssetAtPath<Object>(prefabPath);

                        Debug.LogWarning(L10n.Tr(
                                "A NavMesh Link component has an outdated format. "
                                + "To upgrade it, open and save the prefab at: ") + prefabPath
                            + (isInstance
                                ? L10n.Tr(" . The prefab instance is ") +
                                PrefabUtility.GetNearestPrefabInstanceRoot(gameObject).name
                                : ""),
                            prefabToPing);

                        s_LastWarnedPrefab = prefabPath;
                        s_NextPrefabWarningTime = EditorApplication.timeSinceStartup + 5f;
                    }

                    m_DelayEndpointUpgrade = false;
                    return false;
                }

                if (IsInAuthoringScene())
                {
                    EditorApplication.delayCall += CompleteEndpointUpgrade;

                    EditorApplication.delayCall -= WarnAboutUnsavedUpgrade;
                    EditorApplication.delayCall += WarnAboutUnsavedUpgrade;

                    EditorSceneManager.MarkSceneDirty(gameObject.scene);

                    Debug.Log(L10n.Tr(
                            "A NavMesh Link component has auto-upgraded and it references a newly created object. "
                            + "Save your scene to keep the changes. "
                            + "GameObject: ") + gameObject.name,
                        gameObject);
                }
                else
                {
                    Debug.LogWarning(L10n.Tr(
                            "The NavMesh Link component does not reference the intended transforms. " +
                            "To correct it, save this NavMesh Link again at edit time. GameObject: ") + gameObject.name,
                        gameObject);
                }
            }

            return true;
        }

        static void WarnAboutUnsavedUpgrade()
        {
            Debug.LogWarning(L10n.Tr(
                "At least one NavMesh Link component has auto-upgraded to a new format. "
                + "Save your scene to keep the changes. "));
        }

        void CompleteEndpointUpgrade()
        {
            var discardedByPrefabStageOnHiddenReload = this == null;
            if (discardedByPrefabStageOnHiddenReload ||
                gameObject == null || !m_DelayEndpointUpgrade)
                return;

            var linkIndexString = "";
            var allMyLinks = gameObject.GetComponents<NavMeshLink>();
            if (allMyLinks.Length > 1)
            {
                for (var i = 0; i < allMyLinks.Length; i++)
                {
                    if (allMyLinks[i] == this)
                    {
                        linkIndexString = " " + i;
                        break;
                    }
                }
            }

            var localToWorldUnscaled = LocalToWorldUnscaled();

            if (m_StartTransform != null &&
                m_StartTransform != gameObject.transform &&
                m_StartPoint.sqrMagnitude > 0.0001f)
            {
                var startGO = new GameObject($"Link Start {gameObject.name}{linkIndexString}");
                startGO.transform.SetParent(m_StartTransform);
                startGO.transform.position =
                    localToWorldUnscaled.MultiplyPoint3x4(
                        transform.InverseTransformPoint(m_StartTransform.position + m_StartPoint));
                m_StartTransform = startGO.transform;
            }

            if (m_EndTransform != null &&
                m_EndTransform != gameObject.transform &&
                m_EndPoint.sqrMagnitude > 0.0001f)
            {
                var endGO = new GameObject($"Link End {gameObject.name}{linkIndexString}");
                endGO.transform.SetParent(m_EndTransform);
                endGO.transform.position =
                    localToWorldUnscaled.MultiplyPoint3x4(
                        transform.InverseTransformPoint(m_EndTransform.position + m_EndPoint));
                m_EndTransform = endGO.transform;
            }

            if (IsInAuthoringScene())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);

            m_DelayEndpointUpgrade = false;
        }

        bool IsInAuthoringScene()
        {
            return !EditorApplication.isPlaying || PrefabStageUtility.GetPrefabStage(gameObject) != null;
        }
#endif
    }
}
