using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Guards
{
    public sealed class WaypointMover : IMover
    {
        private readonly Vector3[] _waypoints;
        private readonly float _arriveThresholdSqr;
        private readonly float _lookAroundDuration;
        private readonly bool _loop;

        private int _currentWaypointIndex;

        private Vector3 _destination;
        private bool _hasTemporaryDestination;

        private float _lookAroundTimer;

        private Vector3 _moveDirection;
        private bool _hasArrived;

        public Vector3 MoveDirection => _moveDirection;
        public bool HasArrived => _hasArrived;

        public WaypointMover(
            Vector3[] waypoints,
            float arriveThreshold,
            float lookAroundDuration,
            bool loop = true)
        {
            _waypoints = waypoints;

            float safeThreshold = Mathf.Max(
                0.01f,
                arriveThreshold);

            _arriveThresholdSqr =
                safeThreshold * safeThreshold;

            _lookAroundDuration =
                Mathf.Max(0f, lookAroundDuration);

            _loop = loop;

            _currentWaypointIndex = 0;

            _destination = Vector3.zero;
            _hasTemporaryDestination = false;

            _lookAroundTimer = 0f;

            _moveDirection = Vector3.zero;
            _hasArrived = false;
        }

        public void SetDestination(Vector3 destination)
        {
            _destination = destination;
            _destination.y = 0f;

            _hasTemporaryDestination = true;
            _lookAroundTimer = 0f;
            _hasArrived = false;
        }

        public void ResumeRoute()
        {
            _hasTemporaryDestination = false;
            _lookAroundTimer = 0f;
            _hasArrived = false;
        }

        public void Tick(
            Vector3 currentPosition,
            float deltaTime)
        {
            Vector3 target = GetCurrentDestination(
                currentPosition);

            Vector3 toTarget =
                target - currentPosition;

            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= _arriveThresholdSqr)
            {
                _moveDirection = Vector3.zero;
                _hasArrived = true;

                _lookAroundTimer += deltaTime;

                if (_lookAroundTimer >= _lookAroundDuration)
                {
                    AdvanceDestination();
                }

                return;
            }

            _moveDirection = toTarget.normalized;
            _hasArrived = false;
            _lookAroundTimer = 0f;
        }

        private Vector3 GetCurrentDestination(
            Vector3 currentPosition)
        {
            if (_hasTemporaryDestination)
            {
                return _destination;
            }

            if (_waypoints == null ||
                _waypoints.Length == 0)
            {
                return currentPosition;
            }

            return _waypoints[_currentWaypointIndex];
        }

        private void AdvanceDestination()
        {
            _lookAroundTimer = 0f;
            _hasArrived = false;

            if (_hasTemporaryDestination)
            {
                // Stay at the temporary destination
                // until the state tells us to resume.
                _moveDirection = Vector3.zero;
                return;
            }

            if (_currentWaypointIndex + 1 <
                _waypoints.Length)
            {
                _currentWaypointIndex++;
                return;
            }

            if (_loop)
            {
                _currentWaypointIndex = 0;
            }
            else
            {
                _hasArrived = true;
            }
        }
    }
}