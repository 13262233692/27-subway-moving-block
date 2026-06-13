using UnityEngine;

namespace ATC.Signalling
{
    public enum AtoMode
    {
        Disabled,
        Automatic,
        ManualOverride,
        Emergency
    }

    public class AtoController : MonoBehaviour
    {
        [Header("PID Gains")]
        [SerializeField] private float _kp = 1.2f;
        [SerializeField] private float _ki = 0.08f;
        [SerializeField] private float _kd = 0.15f;

        [Header("Limits")]
        [SerializeField] private float _integralLimit = 5f;
        [SerializeField] private float _outputRateLimit = 2f;
        [SerializeField] private float _jerkLimit = 1.5f;

        [Header("Station Stop")]
        [SerializeField] private float _stationStopTolerance = 0.3f;
        [SerializeField] private float _approachDecelDistance = 50f;
        [SerializeField] private float _approachDecelRate = 0.6f;

        [Header("Mode")]
        [SerializeField] private AtoMode _mode = AtoMode.Disabled;

        private TrainConsist _consist;
        private TrainPositionController _positionController;
        private MovingBlockStateMachine _movingBlock;

        private float _integralError;
        private float _previousError;
        private float _previousOutput;
        private float _targetSpeed;
        private float _commandedAcceleration;
        private float _scheduleSpeed;
        private float _stationStopPosition;
        private bool _hasStationStop;

        public AtoMode Mode => _mode;
        public float TargetSpeed => _targetSpeed;
        public float CommandedAcceleration => _commandedAcceleration;
        public float ScheduleSpeed => _scheduleSpeed;

        public void Initialize(TrainConsist consist, TrainPositionController posCtrl,
            MovingBlockStateMachine movingBlock)
        {
            _consist = consist;
            _positionController = posCtrl;
            _movingBlock = movingBlock;
        }

        public void SetMode(AtoMode mode)
        {
            _mode = mode;
            if (mode == AtoMode.Disabled || mode == AtoMode.Emergency)
            {
                _integralError = 0f;
                _previousError = 0f;
            }
        }

        public void SetScheduleSpeed(float speed)
        {
            _scheduleSpeed = Mathf.Clamp(speed, 0f, _consist != null ? _consist.MaxSpeed : 80f / 3.6f);
        }

        public void SetStationStop(float stopPosition)
        {
            _stationStopPosition = stopPosition;
            _hasStationStop = true;
        }

        public void ClearStationStop()
        {
            _hasStationStop = false;
        }

        public void UpdateControl(float deltaTime)
        {
            if (_consist == null || _movingBlock == null || _positionController == null) return;
            if (_mode == AtoMode.Disabled) return;

            _movingBlock.UpdateState(deltaTime);

            float movingBlockTarget = _movingBlock.TargetSpeed;
            float lineSpeedLimit = _consist.CurrentSegment != null
                ? _consist.CurrentSegment.EffectiveSpeedLimit
                : _consist.MaxSpeed;
            float scheduleLimit = _scheduleSpeed > 0f ? _scheduleSpeed : _consist.MaxSpeed;

            _targetSpeed = Mathf.Min(movingBlockTarget, lineSpeedLimit, scheduleLimit, _consist.MaxSpeed);

            if (_hasStationStop && _consist.CurrentSegment != null)
            {
                float distToStop = _stationStopPosition - _consist.HeadDistance;
                if (distToStop > 0f && distToStop < _approachDecelDistance)
                {
                    float approachProfile = ComputeApproachSpeed(distToStop);
                    _targetSpeed = Mathf.Min(_targetSpeed, approachProfile);
                }
            }

            if (_mode == AtoMode.Emergency)
            {
                _commandedAcceleration = -_consist.MaxEmergencyDeceleration;
            }
            else if (_movingBlock.CurrentState == MovingBlockState.EmergencyBraking)
            {
                _commandedAcceleration = -_consist.MaxEmergencyDeceleration;
            }
            else if (_movingBlock.CurrentState == MovingBlockState.ServiceBraking)
            {
                _commandedAcceleration = ComputeServiceBrakingCommand();
            }
            else
            {
                _commandedAcceleration = ComputePidOutput(deltaTime);
            }

            _commandedAcceleration = ApplyRateLimit(_commandedAcceleration, deltaTime);
            _commandedAcceleration = ApplyJerkLimit(_commandedAcceleration, deltaTime);

            float newSpeed = _consist.CurrentSpeed + _commandedAcceleration * deltaTime;

            float resistance = _consist.ComputeTotalResistance(
                _consist.CurrentSpeed,
                _consist.CurrentSegment != null ? _consist.CurrentSegment.Gradient : 0f);
            float resistanceDecel = resistance / _consist.EffectiveMass;
            newSpeed -= resistanceDecel * deltaTime;

            newSpeed = Mathf.Clamp(newSpeed, 0f, _consist.MaxSpeed);
            _consist.SetSpeed(newSpeed);
            _consist.SetAcceleration(_commandedAcceleration);

            _positionController.UpdatePosition(deltaTime);
        }

        private float ComputeApproachSpeed(float distanceToStop)
        {
            if (distanceToStop <= _stationStopTolerance) return 0f;
            float a = _approachDecelRate;
            float v = Mathf.Sqrt(2f * a * distanceToStop);
            return Mathf.Min(v, _consist.MaxSpeed);
        }

        private float ComputeServiceBrakingCommand()
        {
            float speedError = _consist.CurrentSpeed - _targetSpeed;
            if (speedError <= 0f) return 0f;

            float brakingRatio = Mathf.Clamp01(speedError / _consist.MaxSpeed);
            return -_consist.MaxServiceDeceleration * brakingRatio;
        }

        private float ComputePidOutput(float deltaTime)
        {
            float error = _targetSpeed - _consist.CurrentSpeed;

            _integralError += error * deltaTime;
            _integralError = Mathf.Clamp(_integralError, -_integralLimit, _integralLimit);

            if (error < 0f && _integralError > 0f)
                _integralError *= 0.95f;
            if (error > 0f && _integralError < 0f)
                _integralError *= 0.95f;

            float derivative = deltaTime > 1e-6f ? (error - _previousError) / deltaTime : 0f;
            _previousError = error;

            float output = _kp * error + _ki * _integralError + _kd * derivative;

            if (output > 0f)
                output = Mathf.Min(output, _consist.MaxAcceleration);
            else
                output = Mathf.Max(output, -_consist.MaxServiceDeceleration);

            return output;
        }

        private float ApplyRateLimit(float output, float deltaTime)
        {
            if (deltaTime <= 0f) return output;
            float maxChange = _outputRateLimit * deltaTime;
            float delta = output - _previousOutput;
            if (Mathf.Abs(delta) > maxChange)
                output = _previousOutput + Mathf.Sign(delta) * maxChange;
            _previousOutput = output;
            return output;
        }

        private float ApplyJerkLimit(float output, float deltaTime)
        {
            if (deltaTime <= 0f) return output;
            float maxJerk = _jerkLimit * deltaTime;
            float accelDelta = output - _commandedAcceleration;
            if (Mathf.Abs(accelDelta) > maxJerk)
                output = _commandedAcceleration + Mathf.Sign(accelDelta) * maxJerk;
            return output;
        }

        public void ResetPid()
        {
            _integralError = 0f;
            _previousError = 0f;
            _previousOutput = 0f;
        }
    }
}
