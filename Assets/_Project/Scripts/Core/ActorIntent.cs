using UnityEngine;

namespace EvidenceRun.Core
{
    public struct ActorIntent
    {
        public Vector2 Move;
        public Vector3 AimPoint;

        public bool Crouch;
        public bool Throw;
        public bool Fire;
        public bool Interact;
    }
}