using EvidenceRun.Core;
using EvidenceRun.Gameplay.Guards.Perception;
using UnityEngine;

namespace EvidenceRun.Gameplay.Guards
{
    public sealed class GuardContext
    {
        public GuardConfig Config { get; }
        public Transform Target { get; }

        public Awareness Awareness { get; }
        public IMover Mover { get; }

        public Vector3 LastKnownPlayerPosition { get; private set; }
        public bool HasLastKnownPlayerPosition { get; private set; }

        public float StateTimer { get; private set; }

        public Vector3 Position { get; private set; }
        public SightResult LastSightResult { get; private set; }

        public GameEvents Events { get; private set; }

        public GuardContext(
            GuardConfig config,
            Transform target,
            IMover mover)
        {
            Config = config;
            Target = target;
            Mover = mover;

            Awareness = new Awareness();

            LastKnownPlayerPosition = Vector3.zero;
            HasLastKnownPlayerPosition = false;

            StateTimer = 0f;
        }

        public void SetPosition(Vector3 position)
        {
            Position = position;
        }

        public void SetEvents(GameEvents events)
        {
            Events = events;
        }
        public void SetSightResult(SightResult result)
        {
            LastSightResult = result;
        }
        public void SetLastKnownPlayerPosition(Vector3 position)
        {
            LastKnownPlayerPosition = position;
            HasLastKnownPlayerPosition = true;
        }

        public void ResetLastKnownPlayerPosition()
        {
            LastKnownPlayerPosition = Vector3.zero;
            HasLastKnownPlayerPosition = false;
        }

        public void ResetStateTimer()
        {
            StateTimer = 0f;
        }

        public void TickStateTimer(float deltaTime)
        {
            StateTimer += deltaTime;
        }
    }
}