using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AI.Navigation
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-103)]
    [AddComponentMenu("Navigation/NavMesh Modifier", 32)]
    public class NavMeshModifier : MonoBehaviour
    {
#pragma warning disable 0414

        [SerializeField, HideInInspector]
        private readonly byte m_SerializedVersion = 0;
#pragma warning restore 0414

        [SerializeField]
        private readonly List<int> m_AffectedAgents = new(new int[] { -1 });
        [field: SerializeField]
        public bool overrideArea { get; set; }

        [field: SerializeField]
        public int area { get; set; }

        [field: SerializeField]
        public bool overrideGenerateLinks { get; set; }

        [field: SerializeField]
        public bool generateLinks { get; set; }

        [field: SerializeField]
        public bool ignoreFromBuild { get; set; }

        [field: SerializeField]
        public bool applyToChildren { get; set; } = true;

        private static bool s_RebuildNavMeshModifiers = true;
        private static List<NavMeshModifier> s_NavMeshModifiers = new();
        private static readonly HashSet<NavMeshModifier> s_NavMeshModifiersSet = new();

        public static List<NavMeshModifier> activeModifiers
        {
            get
            {
                if (s_RebuildNavMeshModifiers)
                {
                    s_NavMeshModifiers = s_NavMeshModifiersSet.ToList();
                    s_RebuildNavMeshModifiers = false;
                }

                return s_NavMeshModifiers;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ClearNavMeshModifiers()
        {
            s_RebuildNavMeshModifiers = true;
            s_NavMeshModifiers.Clear();
            s_NavMeshModifiersSet.Clear();
        }

        private void OnEnable()
        {
            RegisterModifier();
        }

        private void OnDisable()
        {
            UnregisterModifier();
        }

        private void RegisterModifier()
        {
            if (s_NavMeshModifiersSet.Add(this))
            {
                s_RebuildNavMeshModifiers = true;
            }
        }

        private void UnregisterModifier()
        {
            if (s_NavMeshModifiersSet.Remove(this))
            {
                s_RebuildNavMeshModifiers = true;
            }
        }

        public bool AffectsAgentType(int agentTypeID)
        {
            return m_AffectedAgents.Count != 0 && (m_AffectedAgents[0] == -1 || m_AffectedAgents.IndexOf(agentTypeID) != -1);
        }
    }
}
