using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.AI.Navigation
{
                    [ExecuteAlways]
    [DefaultExecutionOrder(-103)]
    [AddComponentMenu("Navigation/NavMesh Modifier", 32)]
    public class NavMeshModifier : MonoBehaviour
    {
#pragma warning disable 0414

                        [SerializeField, HideInInspector]
        byte m_SerializedVersion = 0;
#pragma warning restore 0414

        [SerializeField]
        bool m_OverrideArea;

        [SerializeField]
        int m_Area;

        [SerializeField]
        bool m_OverrideGenerateLinks;

        [SerializeField]
        bool m_GenerateLinks;

        [SerializeField]
        bool m_IgnoreFromBuild;

        [SerializeField]
        bool m_ApplyToChildren = true;

                        [SerializeField]
        List<int> m_AffectedAgents = new List<int>(new int[] { -1 }); 
                                public bool overrideArea { get { return m_OverrideArea; } set { m_OverrideArea = value; } }

                                public int area { get { return m_Area; } set { m_Area = value; } }

                public bool overrideGenerateLinks
        {
            get { return m_OverrideGenerateLinks; }
            set { m_OverrideGenerateLinks = value; }
        }

                public bool generateLinks { get { return m_GenerateLinks; } set { m_GenerateLinks = value; } }

                public bool ignoreFromBuild { get { return m_IgnoreFromBuild; } set { m_IgnoreFromBuild = value; } }

                public bool applyToChildren { get { return m_ApplyToChildren; } set { m_ApplyToChildren = value; } }

        static bool s_RebuildNavMeshModifiers = true;
        static List<NavMeshModifier> s_NavMeshModifiers = new List<NavMeshModifier>();
        static readonly HashSet<NavMeshModifier> s_NavMeshModifiersSet = new HashSet<NavMeshModifier>();

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
        static void ClearNavMeshModifiers()
        {
            s_RebuildNavMeshModifiers = true;
            s_NavMeshModifiers.Clear();
            s_NavMeshModifiersSet.Clear();
        }

        void OnEnable()
        {
            RegisterModifier();
        }

        void OnDisable()
        {
            UnregisterModifier();
        }

        void RegisterModifier()
        {
            if (s_NavMeshModifiersSet.Add(this))
            {
                s_RebuildNavMeshModifiers = true;
            }
        }

        void UnregisterModifier()
        {
            if (s_NavMeshModifiersSet.Remove(this))
            {
                s_RebuildNavMeshModifiers = true;
            }
        }

                                public bool AffectsAgentType(int agentTypeID)
        {
            if (m_AffectedAgents.Count == 0)
                return false;
            if (m_AffectedAgents[0] == -1)
                return true;
            return m_AffectedAgents.IndexOf(agentTypeID) != -1;
        }
    }
}
