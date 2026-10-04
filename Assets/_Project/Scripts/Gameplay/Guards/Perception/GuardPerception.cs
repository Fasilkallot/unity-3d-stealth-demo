using UnityEngine;

namespace EvidenceRun.Gameplay.Guards.Perception
{
    public sealed class GuardPerception : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SightSensor sightSensor;

        private GuardContext _context;

        public void Initialize(GuardContext context)
        {
            _context = context;
        }

        public void Tick(float deltaTime)
        {
            SightResult sightResult = sightSensor.Evaluate();

            _context.SetSightResult(sightResult);

            if (sightResult.Visible)
            {
                _context.SetLastKnownPlayerPosition(
                    _context.Target.position);
            }

            _context.Awareness.Tick(
                sightResult,
                _context.Config.VisionRangeSqr,
                _context.Config.AwarenessGainPerSecond,
                _context.Config.AwarenessDecayPerSecond,
                deltaTime);
        }
    }
}