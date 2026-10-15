using UnityEngine;

namespace EvidenceRun.Core
{
    public interface ICameraInputOrientation
    {
        // Player-controlled orientation, independent of automatic camera framing.
        Quaternion InputRotation { get; }
    }
}
