using UnityEngine;

namespace EvidenceRun.Gameplay.Session
{
    [CreateAssetMenu(
        fileName = "ObjectiveConfig",
        menuName = "EvidenceRun/Objectives/Objective Config")]
    public sealed class ObjectiveConfig : ScriptableObject
    {
        [Header("Evidence")]
        [Min(0.1f)]
        [SerializeField] private float evidencePickupRadius = 1.5f;

        [Header("Extraction")]
        [Min(0.1f)]
        [SerializeField] private float extractionRadius = 2f;

        public float EvidencePickupRadius => evidencePickupRadius;
        public float ExtractionRadius => extractionRadius;
    }
}