using UnityEngine;

namespace ATC.Signalling
{
    public enum MovingBlockState
    {
        Normal,
        Coasting,
        ServiceBraking,
        EmergencyBraking,
        Standby
    }

    public class MovingBlockStateMachine : MonoBehaviour
    {
        [SerializeField] private float _reactionTime = 2.5f;
        [SerializeField] private float _safetyMargin = 5f;
        [SerializeField] private float _coastingSpeedThreshold = 0.5f;
        [SerializeField] private float _standbySpeedThreshold = 0.01f;

        private MovingBlockState _currentState = MovingBlockState.Standby;
        private MovingBlockState _previousState = MovingBlockState.Standby;
        private BrakingCurve _brakingCurve;
        private TrainConsist _ownTrain;
        private TrainConsist _precedingTrain;
        private float _distanceToPrecedingTail;
        private float _movementAuthority;
        private float _targetSpeed;
        private float _stateTimer;

        public MovingBlockState CurrentState => _currentState;
        public MovingBlockState PreviousState => _previousState;
        public BrakingCurve Curve => _brakingCurve;
        public float DistanceToPrecedingTail => _distanceToPrecedingTail;
        public float MovementAuthority => _movementAuthority;
        public float TargetSpeed => _targetSpeed;
        public float StateDuration => _stateTimer;

        public void Initialize(TrainConsist ownTrain)
        {
            _ownTrain = ownTrain;
            _brakingCurve = new BrakingCurve(
                _reactionTime,
                ownTrain.MaxServiceDeceleration,
                ownTrain.MaxEmergencyDeceleration,
                _safetyMargin);
        }

        public void SetPrecedingTrain(TrainConsist preceding)
        {
            _precedingTrain = preceding;
        }

        public void ClearPrecedingTrain()
        {
            _precedingTrain = null;
        }

        public void UpdateState(float deltaTime)
        {
            if (_ownTrain == null) return;

            _stateTimer += deltaTime;
            _distanceToPrecedingTail = ComputeDistanceToPrecedingTail();

            float gradient = GetCurrentGradient();
            _brakingCurve.Calculate(
                _ownTrain.CurrentSpeed,
                _distanceToPrecedingTail,
                gradient);

            float emergencyTarget = _brakingCurve.GetEmergencyTargetSpeed(_distanceToPrecedingTail);
            float serviceTarget = _brakingCurve.GetServiceTargetSpeed(_distanceToPrecedingTail);

            float speedLimit = _ownTrain.CurrentSegment != null
                ? _ownTrain.CurrentSegment.EffectiveSpeedLimit
                : _ownTrain.MaxSpeed;

            _targetSpeed = Mathf.Min(serviceTarget, speedLimit);
            _movementAuthority = _distanceToPrecedingTail - _safetyMargin;

            MovingBlockState newState = DetermineNewState();

            if (newState != _currentState)
            {
                TransitionTo(newState);
            }
        }

        private MovingBlockState DetermineNewState()
        {
            float speed = _ownTrain.CurrentSpeed;

            if (speed < _standbySpeedThreshold)
                return MovingBlockState.Standby;

            if (_brakingCurve.IsEmergencyBrakingRequired(speed, _distanceToPrecedingTail))
                return MovingBlockState.EmergencyBraking;

            if (_brakingCurve.IsServiceBrakingRequired(speed, _distanceToPrecedingTail))
                return MovingBlockState.ServiceBraking;

            if (speed < _coastingSpeedThreshold)
                return MovingBlockState.Coasting;

            return MovingBlockState.Normal;
        }

        private void TransitionTo(MovingBlockState newState)
        {
            _previousState = _currentState;
            _currentState = newState;
            _stateTimer = 0f;
        }

        public float GetCommandedDeceleration()
        {
            switch (_currentState)
            {
                case MovingBlockState.EmergencyBraking:
                    return _ownTrain.MaxEmergencyDeceleration;
                case MovingBlockState.ServiceBraking:
                    return _ownTrain.MaxServiceDeceleration;
                case MovingBlockState.Coasting:
                    return 0f;
                default:
                    return 0f;
            }
        }

        public float GetCommandedAcceleration()
        {
            if (_currentState == MovingBlockState.EmergencyBraking)
                return -_ownTrain.MaxEmergencyDeceleration;
            if (_currentState == MovingBlockState.ServiceBraking)
                return -_ownTrain.MaxServiceDeceleration;
            if (_currentState == MovingBlockState.Coasting)
                return 0f;
            return _ownTrain.MaxAcceleration;
        }

        public bool CanProceed()
        {
            return _currentState == MovingBlockState.Normal ||
                   _currentState == MovingBlockState.Coasting;
        }

        private float ComputeDistanceToPrecedingTail()
        {
            if (_precedingTrain == null) return float.MaxValue;
            if (_ownTrain.CurrentSegment == null) return float.MaxValue;

            if (_precedingTrain.CurrentSegment == _ownTrain.CurrentSegment)
            {
                float tailDist = _precedingTrain.HeadDistance - _precedingTrain.TotalLength;
                float headDist = _ownTrain.HeadDistance;
                return tailDist - headDist;
            }

            float dist = _ownTrain.CurrentSegment.Length - _ownTrain.HeadDistance;
            TrackSegment seg = _ownTrain.CurrentSegment;

            int maxHops = 30;
            while (seg.NextSegments.Count > 0 && maxHops-- > 0)
            {
                seg = seg.DefaultNextSegment ?? seg.NextSegments[0];
                if (seg == _precedingTrain.CurrentSegment)
                {
                    float tailDist = _precedingTrain.HeadDistance - _precedingTrain.TotalLength;
                    return dist + tailDist;
                }
                dist += seg.Length;
            }

            return float.MaxValue;
        }

        private float GetCurrentGradient()
        {
            if (_ownTrain.CurrentSegment != null)
                return _ownTrain.CurrentSegment.Gradient;
            return 0f;
        }
    }
}
