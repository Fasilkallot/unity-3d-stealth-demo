using UnityEngine;

namespace EvidenceRun.Core
{
    public interface IMover
    {
        Vector3 MoveDirection { get; }
        bool HasArrived { get; }

        void SetDestination(Vector3 destination);
        void ResumeRoute();
        void Tick(Vector3 currentPosition, float deltaTime);
    }
}