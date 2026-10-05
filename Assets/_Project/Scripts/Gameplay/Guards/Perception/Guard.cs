using UnityEngine;
using EvidenceRun.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EvidenceRun.Gameplay.Guards
{
    public sealed class Guard : MonoBehaviour, INoiseListener
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

        public GuardConfig Config => guardConfig;

        public float AwarenessValue =>
            _context != null ? _context.Awareness.Value : 0f;

        public Perception.SightSensor SightSensor => sightSensor;

        private GameEvents _gameEvents;

        private ActorIntent _currentIntent;

        public ActorIntent CurrentIntent => _currentIntent;
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
        public void Initialize(GameEvents gameEvents)
        {
            if (gameEvents == null)
            {
                Debug.LogError(
                    $"{nameof(Guard)} requires GameEvents.",
                    this);

                return;
            }

            _gameEvents = gameEvents;
            _context.SetEvents(gameEvents);
        }

        public void OnNoise(in NoiseEvent noiseEvent)
        {
            if (_context == null)
            {
                return;
            }

            Vector3 offset =
                noiseEvent.Position - transform.position;

            offset.y = 0f;

            float sqrDistance = offset.sqrMagnitude;
            float sqrRadius = noiseEvent.Radius * noiseEvent.Radius;

            if (sqrDistance > sqrRadius)
            {
                return;
            }

            _context.SetNoisePosition(
                noiseEvent.Position);
        }

        public void Tick(float deltaTime)
        {
            _context.SetPosition(transform.position);

            guardPerception.Tick(deltaTime);

            int previousState = _stateMachine.Current;

            _stateMachine.Tick(
                _context,
                deltaTime);

            int currentState = _stateMachine.Current;

            if (previousState >= 0 &&
                currentState != previousState)
            {
                _context.Events.PublishGuardStateChanged(
                    new GuardStateChangedEvent(
                        previousState,
                        currentState));
            }

            _currentIntent = new ActorIntent
            {
                Move = new Vector2(
                    _context.Mover.MoveDirection.x,
                    _context.Mover.MoveDirection.z),

                AimPoint = transform.position +
                _context.Mover.MoveDirection * 10f,

                Run = _stateMachine.Current == (int)GuardStateId.Chase
            };

            actorMotor.SetIntent(_currentIntent);
        }
    }
}