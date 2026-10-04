using UnityEngine;

namespace EvidenceRun.Gameplay.Guards
{
    public sealed class GuardSystem
    {
        private readonly Guard[] _guards;

        public GuardSystem(Guard[] guards)
        {
            _guards = guards;
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _guards.Length; i++)
            {
                _guards[i].Tick(deltaTime);
            }
        }
    }
}