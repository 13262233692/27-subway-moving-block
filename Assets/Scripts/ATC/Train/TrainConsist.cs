using UnityEngine;
using System.Collections.Generic;

namespace ATC.Train
{
    [System.Serializable]
    public class CarDescriptor
    {
        public Transform transform;
        public float length = 20f;
        public float distanceFromHead;
        public float couplingGap = 0.5f;
    }

    public class TrainConsist : MonoBehaviour
    {
        [SerializeField] private float _headDistanceOnTrack;
        [SerializeField] private TrackSegment _currentSegment;
        [SerializeField] private List<CarDescriptor> _cars = new List<CarDescriptor>();
        [SerializeField] private float _maxSpeed = 80f / 3.6f;
        [SerializeField] private float _maxAcceleration = 1.0f;
        [SerializeField] private float _maxServiceDeceleration = 1.2f;
        [SerializeField] private float _maxEmergencyDeceleration = 1.4f;
        [SerializeField] private float _trainMass = 200000f;
        [SerializeField] private float _rotatingMassFactor = 1.08f;
        [SerializeField] private float _runningResistanceA = 1.5f;
        [SerializeField] private float _runningResistanceB = 0.006f;
        [SerializeField] private float _runningResistanceC = 0.00035f;

        private float _currentSpeed;
        private float _currentAcceleration;

        public TrackSegment CurrentSegment
        {
            get => _currentSegment;
            set => _currentSegment = value;
        }

        public float HeadDistance
        {
            get => _headDistanceOnTrack;
            set => _headDistanceOnTrack = value;
        }

        public float TailDistance => _headDistanceOnTrack - TotalLength;
        public float TotalLength => ComputeTotalLength();
        public float CurrentSpeed => _currentSpeed;
        public float CurrentAcceleration => _currentAcceleration;
        public float MaxSpeed => _maxSpeed;
        public float MaxAcceleration => _maxAcceleration;
        public float MaxServiceDeceleration => _maxServiceDeceleration;
        public float MaxEmergencyDeceleration => _maxEmergencyDeceleration;
        public float TrainMass => _trainMass;
        public float EffectiveMass => _trainMass * _rotatingMassFactor;
        public IReadOnlyList<CarDescriptor> Cars => _cars;

        private float ComputeTotalLength()
        {
            if (_cars.Count == 0) return 0f;
            var last = _cars[_cars.Count - 1];
            return last.distanceFromHead + last.length;
        }

        public void SetSpeed(float speed)
        {
            _currentSpeed = Mathf.Clamp(speed, 0f, _maxSpeed);
        }

        public void SetAcceleration(float acceleration)
        {
            _currentAcceleration = acceleration;
        }

        public void SetPosition(TrackSegment segment, float distance)
        {
            _currentSegment = segment;
            _headDistanceOnTrack = distance;
        }

        public void AddCar(Transform carTransform, float carLength, float couplingGap = 0.5f)
        {
            float distFromHead = 0f;
            if (_cars.Count > 0)
            {
                var lastCar = _cars[_cars.Count - 1];
                distFromHead = lastCar.distanceFromHead + lastCar.length + couplingGap;
            }
            _cars.Add(new CarDescriptor
            {
                transform = carTransform,
                length = carLength,
                distanceFromHead = distFromHead,
                couplingGap = couplingGap
            });
        }

        public void RebuildCarLayout()
        {
            float dist = 0f;
            for (int i = 0; i < _cars.Count; i++)
            {
                _cars[i].distanceFromHead = dist;
                dist += _cars[i].length + _cars[i].couplingGap;
            }
        }

        public float ComputeRunningResistance(float speed)
        {
            float v = speed;
            float vKmh = v * 3.6f;
            float fA = _runningResistanceA * _trainMass / 1000f;
            float fB = _runningResistanceB * _trainMass * vKmh / 1000f;
            float fC = _runningResistanceC * _trainMass * vKmh * vKmh / 1000f;
            return fA + fB + fC;
        }

        public float ComputeGradeResistance(float gradient)
        {
            return _trainMass * 9.81f * gradient / 1000f;
        }

        public float ComputeCurveResistance(float radius)
        {
            if (radius <= 0f || radius >= 10000f) return 0f;
            return 650f * _trainMass / 1000f * (radius - 55f) / (radius + 200f);
        }

        public float ComputeTotalResistance(float speed, float gradient = 0f, float curveRadius = float.MaxValue)
        {
            return ComputeRunningResistance(speed)
                 + ComputeGradeResistance(gradient)
                 + ComputeCurveResistance(curveRadius);
        }
    }
}
