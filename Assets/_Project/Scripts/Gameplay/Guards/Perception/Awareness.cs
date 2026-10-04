using EvidenceRun.Gameplay.Guards.Perception;
using UnityEngine;

namespace EvidenceRun.Gameplay.Guards
{
    public sealed class Awareness
    {
        private float _value;

        public float Value => _value;

        public void Tick(
            in SightResult sight,
            float rangeSqr,
            float gainPerSecond,
            float decayPerSecond,
            float deltaTime)
        {
            if (sight.Visible)
            {
                float closeness = GetCloseness(
                    sight.SqrDistance,
                    rangeSqr);

                float gainMultiplier = 1f + closeness;

                _value +=
                    gainPerSecond *
                    gainMultiplier *
                    deltaTime;
            }
            else
            {
                _value -=
                    decayPerSecond *
                    deltaTime;
            }

            _value = Mathf.Clamp01(_value);
        }

        public void Reset()
        {
            _value = 0f;
        }

        private static float GetCloseness(
            float sqrDistance,
            float rangeSqr)
        {
            if (rangeSqr <= 0f)
            {
                return 0f;
            }

            float normalizedDistance =
                Mathf.Sqrt(sqrDistance / rangeSqr);

            return 1f - Mathf.Clamp01(normalizedDistance);
        }
    }
}