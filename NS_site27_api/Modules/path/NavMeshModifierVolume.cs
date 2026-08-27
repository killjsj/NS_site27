using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.AI.Navigation
{
        [ExecuteAlways]
    [AddComponentMenu("Navigation/NavMesh Modifier Volume", 31)]
    public class NavMeshModifierVolume : MonoBehaviour
    {
#pragma warning disable 0414

                        [SerializeField, HideInInspector]
        byte m_SerializedVersion = 0;
#pragma warning restore 0414

        [SerializeField]
        Vector3 m_Size = new Vector3(4.0f, 3.0f, 4.0f);

        [SerializeField]
        Vector3 m_Center = new Vector3(0, 1.0f, 0);

        [SerializeField]
        int m_Area;

                        public Vector3 size { get { return m_Size; } set { m_Size = value; } }

                        public Vector3 center { get { return m_Center; } set { m_Center = value; } }

                                public int area { get { return m_Area; } set { m_Area = value; } }

                        [SerializeField]
        List<int> m_AffectedAgents = new List<int>(new int[] { -1 }); 
        static readonly List<NavMeshModifierVolume> s_NavMeshModifiers = new List<NavMeshModifierVolume>();

                public static List<NavMeshModifierVolume> activeModifiers
        {
            get { return s_NavMeshModifiers; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ClearNavMeshModifiers()
        {
            s_NavMeshModifiers.Clear();
        }

        void OnEnable()
        {
            if (!s_NavMeshModifiers.Contains(this))
                s_NavMeshModifiers.Add(this);
        }

        void OnDisable()
        {
            s_NavMeshModifiers.Remove(this);
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
