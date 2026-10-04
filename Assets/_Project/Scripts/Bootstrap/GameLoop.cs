using EvidenceRun.Gameplay.Guards;
using UnityEngine;

namespace EvidenceRun.Bootstrap
{
    public sealed class GameLoop : MonoBehaviour
    {
        [Header("Guards")]
        [SerializeField] private Guard[] guards;

        private GuardSystem _guardSystem;

        private void Awake()
        {
            if (guards == null)
            {
                Debug.LogError(
                    $"{nameof(GameLoop)} requires a guard array.",
                    this);

                enabled = false;
                return;
            }

            _guardSystem = new GuardSystem(guards);
        }

        private void Update()
        {
            _guardSystem.Tick(Time.deltaTime);
        }
    }
}