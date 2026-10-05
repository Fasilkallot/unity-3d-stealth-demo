using UnityEngine;

namespace EvidenceRun.Core
{
    public interface IWeapon
    {
        bool TryUse(
            Vector3 origin,
            Vector3 aimPoint,
            Object source);
    }
}