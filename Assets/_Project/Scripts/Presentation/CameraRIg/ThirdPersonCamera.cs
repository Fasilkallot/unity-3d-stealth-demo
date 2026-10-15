using EvidenceRun.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EvidenceRun.Presentation.Camera
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour, ICameraInputOrientation
    {
        private const int QueryCapacity = 32;
        private const int DepenetrationPasses = 4;
        private const float ContactMargin = 0.002f;

        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform ignoreRoot;
        [SerializeField] private UnityEngine.Camera cameraComponent;
        [SerializeField] private ThirdPersonCameraConfig settings;

        private readonly RaycastHit[] _castHits = new RaycastHit[QueryCapacity];
        private readonly Collider[] _overlaps = new Collider[QueryCapacity];
        private SphereCollider _querySphere;
        private Vector2 _pendingLook;
        private Vector3 _pivot;
        private Vector3 _lastTargetPosition;
        private Vector3 _targetVelocity;
        private Vector3 _lastSafePosition;
        private Vector3 _lastCollisionPivot;
        private Vector3 _playerCapsuleA;
        private Vector3 _playerCapsuleB;
        private Vector3 _preferredRight;
        private Vector3 _alternateOffset;
        private Quaternion _alternateOrbit;
        private float _playerRadius;
        private float _desiredYaw;
        private float _desiredPitch;
        private float _yaw;
        private float _pitch;
        private float _currentDistance;
        private float _manualOrbitDegrees;
        private bool _orbitInputActive;
        private bool _initialized;
        private bool _hasSafePosition;
        private bool _lastViewWasClear;
        private bool _hasAlternateFraming;
        private bool _isReframing;

        public Quaternion InputRotation => Quaternion.Euler(0f, _initialized ? _yaw : transform.eulerAngles.y, 0f);

        private void Awake()
        {
            if (target == null || ignoreRoot == null || cameraComponent == null || settings == null)
            {
                Debug.LogError($"{nameof(ThirdPersonCamera)} requires a target, ignore root, camera and settings.", this);
                enabled = false;
                return;
            }

            SnapToTarget();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _orbitInputActive = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            }

            if (!Input.GetMouseButton(0))
            {
                _orbitInputActive = false;
            }

            if (_orbitInputActive)
            {
                // Legacy mouse axes already represent displacement, not a rate per second.
                _pendingLook += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime, _pendingLook, _orbitInputActive);
            _pendingLook = Vector2.zero;
        }

        private void OnDisable()
        {
            ClearInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearInput();
            }
        }

        private void OnDestroy()
        {
            if (_querySphere != null)
            {
                if (Application.isPlaying)
                    Destroy(_querySphere);
                else
                    DestroyImmediate(_querySphere);
            }
        }

        private void ClearInput()
        {
            _pendingLook = Vector2.zero;
            _orbitInputActive = false;
            _manualOrbitDegrees = 0f;
        }

        [ContextMenu("Snap to target")]
        public void SnapToTarget()
        {
            if (target == null || ignoreRoot == null || cameraComponent == null || settings == null)
            {
                return;
            }

            if (!_initialized)
            {
                _desiredYaw = _yaw = transform.eulerAngles.y;
                _desiredPitch = _pitch = Mathf.Clamp(settings.InitialPitch, settings.MinPitch, settings.MaxPitch);
                // This disabled shape only supplies geometry to ComputePenetration.
                _querySphere = gameObject.AddComponent<SphereCollider>();
                _querySphere.enabled = false;
                _querySphere.hideFlags = HideFlags.HideAndDontSave;
                _initialized = true;
            }

            ClearInput();
            _pivot = target.position + settings.TargetOffset;
            _lastTargetPosition = target.position;
            _targetVelocity = Vector3.zero;
            _currentDistance = settings.Distance;
            _hasSafePosition = false;
            _hasAlternateFraming = false;
            _isReframing = false;
            UpdatePose(0f, true, Vector3.zero);
        }

        internal void Tick(float deltaTime, Vector2 lookDelta, bool orbitHeld)
        {
            if (!_initialized || target == null || settings == null || deltaTime <= 0f)
            {
                return;
            }

            if (orbitHeld)
            {
                _desiredYaw = Mathf.Repeat(_desiredYaw + lookDelta.x * settings.SensitivityX + 180f, 360f) - 180f;
                _desiredPitch = Mathf.Clamp(_desiredPitch - lookDelta.y * settings.SensitivityY,
                    settings.MinPitch, settings.MaxPitch);
            }
            float previousYaw = _yaw;
            float previousPitch = _pitch;
            float orbitBlend = Response(settings.OrbitResponse, deltaTime);
            _yaw = Mathf.LerpAngle(_yaw, _desiredYaw, orbitBlend);
            _pitch = Mathf.Lerp(_pitch, _desiredPitch, orbitBlend);
            _manualOrbitDegrees = new Vector2(Mathf.DeltaAngle(previousYaw, _yaw), _pitch - previousPitch).magnitude;

            Vector3 targetPosition = target.position;
            bool teleported = (targetPosition - _lastTargetPosition).sqrMagnitude >
                              settings.TeleportDistance * settings.TeleportDistance;
            _targetVelocity = teleported ? Vector3.zero : (targetPosition - _lastTargetPosition) / deltaTime;
            _lastTargetPosition = targetPosition;
            Vector3 previousPivot = _pivot;
            Vector3 desiredPivot = targetPosition + settings.TargetOffset;
            _pivot = teleported ? desiredPivot : Vector3.Lerp(_pivot, desiredPivot,
                Response(settings.PivotResponse, deltaTime));

            if (teleported)
            {
                _currentDistance = settings.Distance;
                _hasSafePosition = false;
                _hasAlternateFraming = false;
                _isReframing = false;
            }

            UpdatePose(deltaTime, teleported, teleported ? Vector3.zero : _pivot - previousPivot);
        }

        private void UpdatePose(float deltaTime, bool snap, Vector3 followDisplacement)
        {
            float radius = GetCollisionRadius();
            Vector3 scale = transform.lossyScale;
            float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            _querySphere.radius = radius / Mathf.Max(0.0001f, largestScale);

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 direction = orbit * Vector3.back;
            _preferredRight = orbit * Vector3.right;
            UpdatePlayerEnvelope();
            Vector3 collisionPivot = _pivot;

            // Sphere casts do not report shapes overlapping their origin.
            if (!ResolveOverlaps(ref collisionPivot, radius) &&
                !TryFindClearPivot(_pivot, orbit, radius, out collisionPivot))
            {
                HoldSafePose(radius, orbit);
                return;
            }

            float allowedDistance = CastDistance(collisionPivot, direction, settings.Distance, radius, out _);
            bool compressing = allowedDistance < _currentDistance;
            _currentDistance = snap || compressing || _isReframing
                ? allowedDistance
                : Mathf.Lerp(_currentDistance, allowedDistance, Response(settings.DistanceRestoreResponse, deltaTime));
            Vector3 boomPosition = collisionPivot + direction * _currentDistance;
            Vector3 candidate = boomPosition;
            bool usedAlternate = false;
            Vector3 previous = transform.position;
            bool canSweep = !snap && _hasSafePosition && ResolveCameraOverlaps(ref previous, radius);
            bool newOccluder = canSweep && compressing && _lastViewWasClear &&
                (collisionPivot - _lastCollisionPivot).sqrMagnitude < 1e-10f &&
                IsObstructed(collisionPivot, previous, radius);

            float framingMargin = 0f;
            if (canSweep && allowedDistance < settings.Distance - ContactMargin)
            {
                float closingSpeed = Mathf.Max(0f, Vector3.Dot(_targetVelocity, direction));
                float leadTime = Mathf.Max(90f / settings.ReframingAngularSpeed, 1f / settings.ReframingResponse);
                framingMargin = radius + closingSpeed * leadTime;
            }

            // Start moving aside before the player can trap the lens against a wall.
            bool needsPlayerClearance = !IsPlayerClear(candidate, radius);
            if (!IsPlayerClear(candidate, radius + framingMargin))
            {
                if (TryFindSafeFraming(collisionPivot, orbit, radius, previous, canSweep, newOccluder,
                    framingMargin, out Vector3 reframed))
                {
                    candidate = reframed;
                    usedAlternate = true;
                }
                else if (needsPlayerClearance)
                {
                    if (framingMargin <= 0f || !TryFindSafeFraming(collisionPivot, orbit, radius, previous,
                        canSweep, newOccluder, 0f, out candidate))
                    {
                        HoldSafePose(radius, orbit);
                        return;
                    }
                    usedAlternate = true;
                }
                else if (canSweep)
                {
                    candidate = SweepAndSlide(previous, candidate, radius, !newOccluder);
                }
            }
            else if (canSweep)
            {
                // New obstacles can require inward correction; player clearance is still swept.
                candidate = SweepAndSlide(previous, candidate, radius, !newOccluder);
            }

            bool clear = ResolveCameraOverlaps(ref candidate, radius);
            bool viewBlocked = clear && (candidate - boomPosition).sqrMagnitude > ContactMargin * ContactMargin &&
                IsObstructed(collisionPivot, candidate, radius);
            if (!clear || viewBlocked)
            {
                if (!TryFindSafeFraming(collisionPivot, orbit, radius, previous, canSweep, newOccluder, 0f, out Vector3 reframed))
                {
                    HoldSafePose(radius, orbit);
                    return;
                }

                candidate = reframed;
                usedAlternate = true;
            }

            if (usedAlternate) _isReframing = true;
            if (canSweep && _isReframing && !newOccluder)
            {
                Vector3 goal = candidate;
                // Smooth recovery relative to the moving target, without filtering follow motion twice.
                Vector3 reframingStart = previous + followDisplacement;
                Vector3 startOffset = reframingStart - collisionPivot;
                Vector3 goalOffset = goal - collisionPivot;
                float blend = Response(settings.ReframingResponse, deltaTime);
                Vector3 intermediate = Vector3.Lerp(reframingStart, goal, blend);
                if (startOffset.sqrMagnitude > 0.001f && goalOffset.sqrMagnitude > 0.001f)
                {
                    float startYaw = Mathf.Atan2(startOffset.x, startOffset.z) * Mathf.Rad2Deg;
                    float goalYaw = Mathf.Atan2(goalOffset.x, goalOffset.z) * Mathf.Rad2Deg;
                    float startElevation = Mathf.Asin(Mathf.Clamp(startOffset.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                    float goalElevation = Mathf.Asin(Mathf.Clamp(goalOffset.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                    Vector2 angleDelta = new Vector2(Mathf.DeltaAngle(startYaw, goalYaw), goalElevation - startElevation);
                    float angle = angleDelta.magnitude;
                    float angleStep = Mathf.Min(angle, Mathf.Min(angle * blend + _manualOrbitDegrees,
                        settings.ReframingAngularSpeed * deltaTime + _manualOrbitDegrees));
                    float angleBlend = angle > 0.0001f ? angleStep / angle : 1f;
                    float yaw = (startYaw + angleDelta.x * angleBlend) * Mathf.Deg2Rad;
                    float elevation = (startElevation + angleDelta.y * angleBlend) * Mathf.Deg2Rad;
                    Vector3 orbitDirection = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(elevation),
                        Mathf.Sin(elevation), Mathf.Cos(yaw) * Mathf.Cos(elevation));
                    float orbitDistance = Mathf.Lerp(startOffset.magnitude, goalOffset.magnitude, blend);
                    intermediate = collisionPivot + orbitDirection * orbitDistance;
                }

                // Smooth the chosen view around the player, then sweep that intermediate pose for safety.
                candidate = SweepAndSlide(previous, intermediate, radius, !newOccluder);
            }

            if (canSweep)
            {
                Vector3 movement = candidate - previous;
                float length = movement.magnitude;
                if (length > ContactMargin)
                {
                    Vector3 travelDirection = movement / length;
                    float travel = CastPlayerDistance(previous, travelDirection, length, radius, out _);
                    if (!newOccluder)
                    {
                        travel = Mathf.Min(travel, CastDistance(previous, travelDirection, length, radius, out _));
                    }
                    candidate = previous + travelDirection * travel;
                }
            }

            if (!IsCameraClear(candidate, radius))
            {
                HoldSafePose(radius, orbit);
                return;
            }

            if (!usedAlternate && (candidate - boomPosition).sqrMagnitude < 0.0004f) _isReframing = false;
            _currentDistance = Mathf.Min(settings.Distance, Vector3.Distance(collisionPivot, candidate));
            _lastViewWasClear = (candidate - boomPosition).sqrMagnitude < ContactMargin * ContactMargin ||
                !IsObstructed(collisionPivot, candidate, radius);
            _lastCollisionPivot = collisionPivot;
            if (!usedAlternate) _hasAlternateFraming = false;
            _lastSafePosition = candidate;
            _hasSafePosition = true;
            PlaceCamera(candidate, orbit);
        }

        private void UpdatePlayerEnvelope()
        {
            Vector3 scale = ignoreRoot.lossyScale;
            _playerRadius = settings.PlayerClearanceRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfSegment = Mathf.Max(0f, settings.PlayerClearanceHeight * Mathf.Abs(scale.y) * 0.5f - _playerRadius);
            Vector3 center = ignoreRoot.TransformPoint(settings.PlayerClearanceCenter);
            _playerCapsuleA = center - ignoreRoot.up * halfSegment;
            _playerCapsuleB = center + ignoreRoot.up * halfSegment;
        }

        private Vector3 ClosestPlayerAxis(Vector3 position)
        {
            Vector3 segment = _playerCapsuleB - _playerCapsuleA;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(position - _playerCapsuleA, segment) / lengthSquared)
                : 0f;
            return _playerCapsuleA + segment * t;
        }

        private bool IsPlayerClear(Vector3 position, float radius)
        {
            float clearance = _playerRadius + radius;
            return (position - ClosestPlayerAxis(position)).sqrMagnitude >= clearance * clearance;
        }

        private bool IsCameraClear(Vector3 position, float radius)
        {
            return IsPlayerClear(position, radius) && IsClear(position, radius);
        }

        private bool ResolveCameraOverlaps(ref Vector3 position, float radius)
        {
            for (int pass = 0; pass < DepenetrationPasses; pass++)
            {
                if (!ResolveOverlaps(ref position, radius)) return false;
                Vector3 separation = position - ClosestPlayerAxis(position);
                float clearance = _playerRadius + radius;
                if (separation.sqrMagnitude >= clearance * clearance) return true;
                Vector3 outward = separation.sqrMagnitude > 0.0001f
                    ? separation.normalized
                    : _preferredRight;
                position += outward * (clearance - separation.magnitude + ContactMargin);
                if (IsClear(position, radius)) return true;
            }

            return IsCameraClear(position, radius);
        }

        private float CastPlayerDistance(Vector3 origin, Vector3 direction, float length, float radius, out Vector3 normal)
        {
            Vector3 axis = _playerCapsuleB - _playerCapsuleA;
            Vector3 offset = origin - _playerCapsuleA;
            float axisLengthSquared = axis.sqrMagnitude;
            float axisDirection = Vector3.Dot(axis, direction);
            float axisOffset = Vector3.Dot(axis, offset);
            float rayOffset = Vector3.Dot(direction, offset);
            float expandedRadius = _playerRadius + radius;
            float nearest = length;
            normal = Vector3.zero;

            float a = axisLengthSquared - axisDirection * axisDirection;
            float b = axisLengthSquared * rayOffset - axisOffset * axisDirection;
            float c = axisLengthSquared * (offset.sqrMagnitude - expandedRadius * expandedRadius) - axisOffset * axisOffset;
            float discriminant = b * b - a * c;
            if (a > 0.0001f && discriminant >= 0f)
            {
                float hit = (-b - Mathf.Sqrt(discriminant)) / a;
                float alongAxis = axisOffset + hit * axisDirection;
                if (hit >= 0f && hit <= nearest && alongAxis >= 0f && alongAxis <= axisLengthSquared)
                {
                    nearest = hit;
                    normal = (origin + direction * hit - (_playerCapsuleA + axis * (alongAxis / axisLengthSquared))).normalized;
                }
            }

            for (int cap = 0; cap < 2; cap++)
            {
                Vector3 center = cap == 0 ? _playerCapsuleA : _playerCapsuleB;
                Vector3 relative = origin - center;
                float projection = Vector3.Dot(direction, relative);
                float sphereDiscriminant = projection * projection - relative.sqrMagnitude + expandedRadius * expandedRadius;
                if (sphereDiscriminant < 0f) continue;
                float hit = -projection - Mathf.Sqrt(sphereDiscriminant);
                if (hit < 0f || hit > nearest) continue;
                nearest = hit;
                normal = (origin + direction * hit - center).normalized;
            }

            return normal.sqrMagnitude > 0.5f ? Mathf.Max(0f, nearest - ContactMargin) : length;
        }

        private bool TryFindSafeFraming(Vector3 focus, Quaternion orbit, float radius, Vector3 previous,
            bool canSweep, bool newOccluder, float playerMargin, out Vector3 result)
        {
            result = previous;
            if (_hasAlternateFraming && Quaternion.Angle(orbit, _alternateOrbit) < 1f)
            {
                Vector3 cached = focus + _alternateOffset;
                if (canSweep) cached = SweepAndSlide(previous, cached, radius, !newOccluder);
                if (IsCameraClear(cached, radius) && IsPlayerClear(cached, radius + playerMargin) &&
                    !IsObstructed(focus, cached, radius))
                {
                    result = cached;
                    return true;
                }
            }

            float bestScore = float.PositiveInfinity;
            Vector3 preferred = focus + orbit * Vector3.back * settings.Distance;
            float closeDistance = Mathf.Min(settings.Distance,
                Mathf.Max(_playerRadius + radius, settings.PlayerClearanceHeight * 0.5f) * 1.2f);

            for (int sample = 0; sample < 24; sample++)
            {
                int heading = sample % 12;
                float yawOffset = heading < 8 ? ((heading + 1) / 2) * 45f * (heading % 2 == 0 ? -1f : 1f) : (heading - 8) * 90f;
                float pitch = heading < 8 ? _pitch : 70f;
                Vector3 direction = Quaternion.Euler(pitch, _yaw + yawOffset, 0f) * Vector3.back;
                float desiredDistance = sample < 12 ? closeDistance : settings.Distance;
                float distance = CastDistance(focus, direction, desiredDistance, radius, out _);
                Vector3 candidate = focus + direction * distance;
                if (!IsCameraClear(candidate, radius) || !IsPlayerClear(candidate, radius + playerMargin)) continue;
                if (canSweep) candidate = SweepAndSlide(previous, candidate, radius, !newOccluder);
                if (!IsCameraClear(candidate, radius) || !IsPlayerClear(candidate, radius + playerMargin) ||
                    IsObstructed(focus, candidate, radius)) continue;
                Vector3 framing = candidate - focus;
                float shortfall = Mathf.Max(0f, settings.Distance - framing.magnitude);
                float actualPitch = Mathf.Asin(Mathf.Clamp(framing.normalized.y, -1f, 1f));
                float pitchDifference = actualPitch - _pitch * Mathf.Deg2Rad;
                float score = (candidate - previous).sqrMagnitude * 0.2f + shortfall * shortfall +
                    pitchDifference * pitchDifference * settings.Distance * settings.Distance +
                    (candidate - preferred).sqrMagnitude * 0.05f;
                if (score >= bestScore) continue;
                bestScore = score;
                result = candidate;
            }

            if (float.IsPositiveInfinity(bestScore)) return false;
            _alternateOffset = result - focus;
            _alternateOrbit = orbit;
            _hasAlternateFraming = true;
            return true;
        }

        private float GetCollisionRadius()
        {
            float near = cameraComponent.nearClipPlane;
            float halfHeight = cameraComponent.orthographic
                ? cameraComponent.orthographicSize
                : near * Mathf.Tan(cameraComponent.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * cameraComponent.aspect;
            float nearPlaneEnvelope = Mathf.Sqrt(near * near + halfHeight * halfHeight + halfWidth * halfWidth);
            return Mathf.Max(settings.CollisionRadius, nearPlaneEnvelope) + settings.SurfaceOffset;
        }

        private Vector3 SweepAndSlide(Vector3 start, Vector3 destination, float radius, bool includeEnvironment = true)
        {
            Vector3 remaining = destination - start;
            for (int pass = 0; pass < 2; pass++)
            {
                float length = remaining.magnitude;
                if (length < ContactMargin)
                {
                    break;
                }

                Vector3 direction = remaining / length;
                Vector3 normal = Vector3.zero;
                float travel = includeEnvironment ? CastDistance(start, direction, length, radius, out normal) : length;
                float playerTravel = CastPlayerDistance(start, direction, length, radius, out Vector3 playerNormal);
                bool hitPlayer = playerTravel < travel;
                if (hitPlayer)
                {
                    travel = playerTravel;
                    normal = playerNormal;
                }
                start += direction * travel;
                if (travel >= length || normal.sqrMagnitude < 0.5f)
                {
                    break;
                }

                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, normal);
                if (hitPlayer && remaining.sqrMagnitude < ContactMargin * ContactMargin)
                {
                    Vector3 tangent = Vector3.Cross(ignoreRoot.up, normal);
                    if (tangent.sqrMagnitude < 0.001f) tangent = _preferredRight;
                    if (Vector3.Dot(tangent, _preferredRight) < 0f) tangent = -tangent;
                    remaining = tangent.normalized * Mathf.Min(length - travel, (_playerRadius + radius) * 0.75f);
                }
            }

            return start;
        }

        private bool IsObstructed(Vector3 origin, Vector3 destination, float radius)
        {
            Vector3 displacement = destination - origin;
            float length = displacement.magnitude;
            return length > ContactMargin &&
                   CastDistance(origin, displacement / length, length, radius, out _) < length - ContactMargin;
        }

        private float CastDistance(Vector3 origin, Vector3 direction, float length, float radius, out Vector3 normal)
        {
            normal = Vector3.zero;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, _castHits, length,
                settings.CollisionLayers, QueryTriggerInteraction.Ignore);
            if (count == _castHits.Length)
            {
                // Results are unordered; a full buffer cannot prove the nearest surface was returned.
                return 0f;
            }

            float nearest = length;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _castHits[i];
                if (IsIgnored(hit.collider) || hit.distance > nearest)
                {
                    continue;
                }

                nearest = Mathf.Max(0f, hit.distance - ContactMargin);
                normal = hit.normal;
            }

            return nearest;
        }

        private bool ResolveOverlaps(ref Vector3 position, float radius)
        {
            for (int pass = 0; pass < DepenetrationPasses; pass++)
            {
                int count = Physics.OverlapSphereNonAlloc(position, radius, _overlaps,
                    settings.CollisionLayers, QueryTriggerInteraction.Ignore);
                if (count == _overlaps.Length)
                {
                    return false;
                }

                bool foundObstacle = false;
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider obstacle = _overlaps[i];
                    if (IsIgnored(obstacle))
                    {
                        continue;
                    }

                    foundObstacle = true;
                    Transform obstacleTransform = obstacle.transform;
                    if (Physics.ComputePenetration(_querySphere, position, Quaternion.identity,
                            obstacle, obstacleTransform.position, obstacleTransform.rotation,
                            out Vector3 direction, out float distance))
                    {
                        position += direction * (distance + ContactMargin);
                        moved = true;
                    }
                }

                if (!foundObstacle)
                {
                    return true;
                }

                if (!moved)
                {
                    return false;
                }
            }

            return IsClear(position, radius);
        }

        private bool TryFindClearPivot(Vector3 focus, Quaternion orbit, float radius, out Vector3 pivot)
        {
            Vector3 back = orbit * Vector3.back;
            Vector3 right = orbit * Vector3.right;

            // Backfacing mesh overlaps can lack a penetration direction; sample a nearby clear cast origin.
            for (int ring = 0; ring < 3; ring++)
            {
                float offset = (radius + ContactMargin) * (1 << ring);
                for (int directionIndex = 0; directionIndex < 6; directionIndex++)
                {
                    Vector3 direction;
                    switch (directionIndex)
                    {
                        case 0: direction = back; break;
                        case 1: direction = Vector3.up; break;
                        case 2: direction = right; break;
                        case 3: direction = -right; break;
                        case 4: direction = -back; break;
                        default: direction = Vector3.down; break;
                    }

                    pivot = focus + direction * offset;
                    if (IsClear(pivot, radius))
                    {
                        return true;
                    }
                }
            }

            pivot = focus;
            return false;
        }

        private bool IsClear(Vector3 position, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(position, radius, _overlaps,
                settings.CollisionLayers, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!IsIgnored(_overlaps[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsIgnored(Collider obstacle)
        {
            return obstacle == null || obstacle == _querySphere ||
                   (ignoreRoot != null && obstacle.transform.IsChildOf(ignoreRoot));
        }

        private void HoldSafePose(float radius, Quaternion orbit)
        {
            _lastViewWasClear = false;
            if (_hasSafePosition && IsCameraClear(_lastSafePosition, radius))
            {
                PlaceCamera(_lastSafePosition, orbit);
                return;
            }

            Vector3 recovered = transform.position;
            if (ResolveCameraOverlaps(ref recovered, radius))
            {
                _lastSafePosition = recovered;
                _hasSafePosition = true;
                _currentDistance = Mathf.Min(settings.Distance, Vector3.Distance(_pivot, recovered));
                PlaceCamera(recovered, orbit);
            }
        }

        private void PlaceCamera(Vector3 position, Quaternion orbit)
        {
            Vector3 view = _pivot - position;
            Quaternion rotation = view.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(view, Vector3.up) : orbit;
            transform.SetPositionAndRotation(position, rotation);
        }

        private static float Response(float rate, float deltaTime)
        {
            return 1f - Mathf.Exp(-rate * deltaTime);
        }
    }
}
