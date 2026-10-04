using UnityEngine;
using EvidenceRun.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EvidenceRun.Gameplay.Guards
{
    public sealed class Guard : MonoBehaviour
    {
        #if UNITY_EDITOR
[ContextMenu("Debug/Log Awareness")]
        private void LogAwareness()
        {
            float value = _context?.Awareness.Value ?? 0f;

            Debug.Log(
                $"[{name}] Awareness: {value:F2}",
                this);
        }
#endif

        [Header("Configuration")]
        [SerializeField] private GuardConfig guardConfig;

        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Perception")]
        [SerializeField] private Perception.SightSensor sightSensor;
        [SerializeField] private Perception.GuardPerception guardPerception;

        [Header("Movement")]
        [SerializeField] private Actors.ActorMotor actorMotor;
        [SerializeField] private Transform[] patrolWaypoints;

        [SerializeField, Min(0.01f)]
        private float arriveThreshold = 0.25f;

        [SerializeField, Min(0f)]
        private float lookAroundDuration = 1.5f;

        private GuardContext _context;

        public GuardContext Context => _context;

        private WaypointMover _waypointMover;

        private StateMachine<GuardContext> _stateMachine;

        private void Awake()
        {
            if (guardConfig == null)
            {
                Debug.LogError(
                    $"{nameof(Guard)} requires a GuardConfig.",
                    this);

                enabled = false;
                return;
            }

            if (target == null)
            {
                Debug.LogError(
                    $"{nameof(Guard)} requires a target.",
                    this);

                enabled = false;
                return;
            }

            if (sightSensor == null)
            {
                Debug.LogError(
                    $"{nameof(Guard)} requires a SightSensor.",
                    this);

                enabled = false;
                return;
            }

            if (guardPerception == null)
            {
                Debug.LogError(
                    $"{nameof(Guard)} requires a GuardPerception.",
                    this);

                enabled = false;
                return;
            }

            if (actorMotor == null)
            {
                Debug.LogError(
                    $"{nameof(Guard)} requires an ActorMotor.",
                    this);

                enabled = false;
                return;
            }

            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                Debug.LogError(
                    $"{nameof(Guard)} requires at least one patrol waypoint.",
                    this);

                enabled = false;
                return;
            }

            Vector3[] waypointPositions =
                new Vector3[patrolWaypoints.Length];

            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                waypointPositions[i] = patrolWaypoints[i].position;
            }

            _waypointMover = new WaypointMover(
                waypointPositions,
                arriveThreshold,
                lookAroundDuration);

            _context = new GuardContext(
                guardConfig,
                target,
                _waypointMover);

            _stateMachine = new StateMachine<GuardContext>(
                new IState<GuardContext>[]
                {
                    new States.PatrolState(),
                    new States.SuspiciousState(),
                    new States.ChaseState()
                });
            _stateMachine.Request((int)GuardStateId.Patrol);

            sightSensor.Initialize(
                guardConfig,
                target);

            guardPerception.Initialize(
                _context);

            actorMotor.Initialize(
                guardConfig);
        }

        public void Tick(float deltaTime)
        {
            _context.SetPosition(transform.position);

            guardPerception.Tick(deltaTime);

            _stateMachine.Tick(_context, deltaTime);

            ActorIntent intent = new ActorIntent
            {
                Move = new Vector2(
                    _context.Mover.MoveDirection.x,
                    _context.Mover.MoveDirection.z),

                AimPoint = transform.position +
                           _context.Mover.MoveDirection * 10f
            };

            actorMotor.SetIntent(intent);
        }
    }
}