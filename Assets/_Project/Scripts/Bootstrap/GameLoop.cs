using EvidenceRun.Gameplay.Guards;
using UnityEngine;

namespace EvidenceRun.Bootstrap
{
    public sealed class GameLoop : MonoBehaviour
    {
        private GuardSystem _guardSystem;

        public void Initialize(Guard[] guards)
        {
            _guardSystem = new GuardSystem(guards);
        }

        private void Update()
        {
            _guardSystem?.Tick(Time.deltaTime);
        }
    }
}