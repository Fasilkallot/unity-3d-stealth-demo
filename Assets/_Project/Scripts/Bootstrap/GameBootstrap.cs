using EvidenceRun.Core;
using EvidenceRun.Gameplay.Combat;
using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Noise;
using EvidenceRun.Gameplay.Player;
using EvidenceRun.Gameplay.Pooling;
using EvidenceRun.Gameplay.Projectiles;
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

        private GameEvents _gameEvents;
        private NoiseBus _noiseBus;
        private ObjectPool<PooledProjectile> _projectilePool;
        private ProjectileSystem _projectileSystem;
        private ThrowableWeapon _throwableWeapon;

        public NoiseBus NoiseBus => _noiseBus;
        public GameEvents GameEvents => _gameEvents;


        private void Awake()
        {
            _gameEvents = new GameEvents();

            InitializeNoiseBus();
            InitializeGuards();
            InitializeProjectilePool();
            InitializePlayerWeapon();

            gameLoop.Initialize(
                guards,
                _projectileSystem);
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

            playerController.Initialize(
                _throwableWeapon);
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
                _noiseBus);
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
    }
}