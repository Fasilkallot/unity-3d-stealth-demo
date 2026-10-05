using EvidenceRun.Core;
using EvidenceRun.Gameplay.Combat;
using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Player;
using EvidenceRun.Gameplay.Pooling;
using EvidenceRun.Gameplay.Projectiles;
using EvidenceRun.Gameplay.Session;
using EvidenceRun.Presentation.Effects;
using EvidenceRun.Presentation.HUD;
using UnityEngine;

namespace EvidenceRun.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Guard[] guards;
        [SerializeField] private GameLoop gameLoop;

        [Header("Pooling")]
        [SerializeField] private PoolConfig poolConfig;
        [SerializeField] private PooledProjectile projectilePrefab;
        [SerializeField] private Transform projectilePoolRoot;

        [SerializeField] private PlayerController playerController;
        [SerializeField] private ThrowableConfig throwableConfig;

        [Header("Impact Effects")]
        [SerializeField] private PooledImpactEffect impactEffectPrefab;
        [SerializeField] private Transform impactEffectPoolRoot;

        [Header("Objectives")]
        [SerializeField] private ObjectiveConfig objectiveConfig;
        [SerializeField] private Transform evidenceTarget;
        [SerializeField] private Transform extractionTarget;

        [Header("Presentation")]
        [SerializeField] private GameHUD gameHUD;

        private ObjectPool<PooledImpactEffect> _impactEffectPool;
        private ImpactEffectSystem _impactEffectSystem;

        private GameEvents _gameEvents;
        private NoiseBus _noiseBus;
        private ObjectPool<PooledProjectile> _projectilePool;
        private ProjectileSystem _projectileSystem;
        private ThrowableWeapon _throwableWeapon;
        private GameSession _gameSession;
        private ObjectiveSystem _objectiveSystem;

        public NoiseBus NoiseBus => _noiseBus;
        public GameEvents GameEvents => _gameEvents;

        private void Awake()
        {
            _gameEvents = new GameEvents();

        }

        private void Start()
        {

            InitializeGameSession();
            InitializeNoiseBus();
            InitializeGuards();
            InitializeProjectilePool();
            InitializeImpactEffectSystem();
            InitializePlayerWeapon();
            InitializeObjectiveSystem();
            InitializeHUD();

            _gameSession.Start();

            gameLoop.Initialize(
                guards,
                _projectileSystem,
                _impactEffectSystem,
                _objectiveSystem,
                playerController.transform,
                gameHUD
                );
            _gameEvents.EvidencePicked += OnEvidencePicked;

        }

        private void InitializeGameSession()
        {
            _gameSession = new GameSession(_gameEvents);
        }
        private void InitializeNoiseBus()
        {
            if (guards == null || guards.Length == 0)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires at least one guard.",
                    this);

                enabled = false;
                return;
            }

            _noiseBus = new NoiseBus(
                guards.Length);
        }

        private void InitializeHUD()
        {
            if (gameHUD == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires a GameHUD.",
                    this);

                enabled = false;
                return;
            }

            gameHUD.Initialize(_gameSession,_gameEvents);
        }
        private void InitializeGuards()
        {
            for (int i = 0; i < guards.Length; i++)
            {
                if (guards[i] == null)
                {
                    Debug.LogError(
                        $"Guard at index {i} is null.",
                        this);

                    enabled = false;
                    return;
                }

                guards[i].Initialize(_gameEvents);

                _noiseBus.Register(
                    guards[i]);
            }
        }
        private void OnEvidencePicked(EvidencePickedEvent eventData)
        {
            if (evidenceTarget == null)
            {
                return;
            }

            evidenceTarget.gameObject.SetActive(false);
        }
        private void InitializePlayerWeapon()
        {
            if (playerController == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires a PlayerController.",
                    this);

                enabled = false;
                return;
            }

            if (throwableConfig == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires a ThrowableConfig.",
                    this);

                enabled = false;
                return;
            }

            _throwableWeapon = new ThrowableWeapon(
                throwableConfig,
                _projectileSystem);

            playerController.Initialize(_throwableWeapon, _gameSession);
        }
        private void InitializeObjectiveSystem()
        {
            if (objectiveConfig == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires an ObjectiveConfig.",
                    this);

                enabled = false;
                return;
            }

            if (evidenceTarget == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires an evidence target.",
                    this);

                enabled = false;
                return;
            }

            if (extractionTarget == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires an extraction target.",
                    this);

                enabled = false;
                return;
            }

            _objectiveSystem = new ObjectiveSystem(
                _gameEvents,
                _gameSession,
                evidenceTarget.position,
                extractionTarget.position,
                objectiveConfig);
        }
        private void InitializeProjectilePool()
        {
            if (poolConfig == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires a PoolConfig.",
                    this);

                enabled = false;
                return;
            }

            if (projectilePrefab == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires a projectile prefab.",
                    this);

                enabled = false;
                return;
            }

            if (projectilePoolRoot == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires a projectile pool root.",
                    this);

                enabled = false;
                return;
            }

            _projectilePool = new ObjectPool<PooledProjectile>(
                CreateProjectile,
                poolConfig.ProjectileCapacity);

            _projectileSystem = new ProjectileSystem(
                _projectilePool,
                poolConfig.ProjectileCapacity,
                _noiseBus,
                _gameEvents);
        }

        private PooledProjectile CreateProjectile()
        {
            PooledProjectile projectile =
                Instantiate(
                    projectilePrefab,
                    projectilePoolRoot);

            projectile.gameObject.SetActive(false);

            return projectile;
        }
        private void InitializeImpactEffectSystem()
        {
            if (impactEffectPrefab == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires an impact effect prefab.",
                    this);

                enabled = false;
                return;
            }

            if (impactEffectPoolRoot == null)
            {
                Debug.LogError(
                    $"{nameof(GameBootstrap)} requires an impact effect pool root.",
                    this);

                enabled = false;
                return;
            }

            _impactEffectPool =
                new ObjectPool<PooledImpactEffect>(
                    CreateImpactEffect,
                    poolConfig.RippleCapacity);

            _impactEffectSystem =
                new ImpactEffectSystem(
                    _impactEffectPool,
                    poolConfig.RippleCapacity);
            _gameEvents.ProjectileImpact +=
                _impactEffectSystem.OnProjectileImpact;
        }
        private PooledImpactEffect CreateImpactEffect()
        {
            PooledImpactEffect effect =
                Instantiate(
                    impactEffectPrefab,
                    impactEffectPoolRoot);

            effect.gameObject.SetActive(false);

            return effect;
        }

        private void OnDestroy()
        {
            if (_gameEvents == null)
            {
                return;
            }

            _gameEvents.EvidencePicked -= OnEvidencePicked;
        }

    }
}