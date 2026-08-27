using System.Collections.Generic;
using UnityEngine;

namespace Unity.AI.Navigation
{
    [ExecuteAlways]
    [AddComponentMenu("Navigation/NavMesh Modifier Volume", 31)]
    public class NavMeshModifierVolume : MonoBehaviour
    {
#pragma warning disable 0414

        [SerializeField, HideInInspector]
        private readonly byte m_SerializedVersion = 0;
#pragma warning restore 0414

        [SerializeField]
        private Vector3 m_Size = new(4.0f, 3.0f, 4.0f);

        [SerializeField]
        private Vector3 m_Center = new(0, 1.0f, 0);

        public Vector3 size { get => m_Size; set => m_Size = value; }

        public Vector3 center { get => m_Center; set => m_Center = value; }

        [field: SerializeField]
        public int area { get; set; }

        [SerializeField]
        private readonly List<int> m_AffectedAgents = new(new int[] { -1 });

        public static List<NavMeshModifierVolume> activeModifiers { get; } = new List<NavMeshModifierVolume>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ClearNavMeshModifiers()
        {
            activeModifiers.Clear();
        }

        private void OnEnable()
        {
            if (!activeModifiers.Contains(this))
            {
                activeModifiers.Add(this);
            }
        }

        private void OnDisable()
        {
            _ = activeModifiers.Remove(this);
        }

        public bool AffectsAgentType(int agentTypeID)
        {
            return m_AffectedAgents.Count != 0 && (m_AffectedAgents[0] == -1 || m_AffectedAgents.IndexOf(agentTypeID) != -1);
        }
    }
}
