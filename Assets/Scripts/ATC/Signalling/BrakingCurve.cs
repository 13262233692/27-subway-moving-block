using UnityEngine;
using System.Collections.Generic;

namespace ATC.Signalling
{
    public struct BrakingPoint
    {
        public float distanceFromObstacle;
        public float permittedSpeed;
        public float effectiveDeceleration;
        public float gradient;
    }

    public class BrakingCurve
    {
        private const float Gravity = 9.80665f;

        private readonly List<BrakingPoint> _emergencyCurve;
        private readonly List<BrakingPoint> _serviceCurve;
        private readonly float _reactionTime;
        private readonly float _serviceDecel;
        private readonly float _emergencyDecel;
        private readonly float _safetyMargin;

        private float _safeStopDistanceEmergency;
        private float _safeStopDistanceService;

        public IReadOnlyList<BrakingPoint> EmergencyCurvePoints => _emergencyCurve;
        public IReadOnlyList<BrakingPoint> ServiceCurvePoints => _serviceCurve;
        public float SafeStopDistanceEmergency => _safeStopDistanceEmergency;
        public float SafeStopDistanceService => _safeStopDistanceService;

        public BrakingCurve(float reactionTime, float serviceDeceleration,
            float emergencyDeceleration, float safetyMargin)
        {
            _reactionTime = reactionTime;
            _serviceDecel = serviceDeceleration;
            _emergencyDecel = emergencyDeceleration;
            _safetyMargin = safetyMargin;
            _emergencyCurve = new List<BrakingPoint>();
            _serviceCurve = new List<BrakingPoint>();
        }

        public void Calculate(float currentSpeed, float distanceToObstacle, float gradient = 0f)
        {
            _emergencyCurve.Clear();
            _serviceCurve.Clear();

            float obstacleDist = Mathf.Max(0f, distanceToObstacle - _safetyMargin);

            _safeStopDistanceEmergency = ComputeSafeStopDistance(
                currentSpeed, _emergencyDecel, gradient);
            _safeStopDistanceService = ComputeSafeStopDistance(
                currentSpeed, _serviceDecel, gradient);

            BuildCurve(_emergencyCurve, obstacleDist, _emergencyDecel, gradient);
            BuildCurve(_serviceCurve, obstacleDist, _serviceDecel, gradient);
        }

        private float ComputeSafeStopDistance(float speed, float decel, float gradient)
        {
            float aEff = decel + Gravity * gradient / 1000f;
            if (aEff <= 0f) aEff = 0.01f;
            return speed * _reactionTime + speed * speed / (2f * aEff) + _safetyMargin;
        }

        private void BuildCurve(List<BrakingPoint> curve, float obstacleDist,
            float nominalDecel, float gradient)
        {
            float aEff = nominalDecel + Gravity * gradient / 1000f;
            if (aEff <= 0f) aEff = 0.01f;

            float stepSize = 2f;
            int stepCount = Mathf.CeilToInt(obstacleDist / stepSize) + 1;

            curve.Add(new BrakingPoint
            {
                distanceFromObstacle = 0f,
                permittedSpeed = 0f,
                effectiveDeceleration = aEff,
                gradient = gradient
            });

            for (int i = 1; i <= stepCount; i++)
            {
                float d = i * stepSize;
                if (d > obstacleDist) d = obstacleDist;

                float trAe = _reactionTime * aEff;
                float discriminant = trAe * trAe + 2f * aEff * d;
                float maxSpeed = -trAe + Mathf.Sqrt(Mathf.Max(0f, discriminant));

                curve.Add(new BrakingPoint
                {
                    distanceFromObstacle = d,
                    permittedSpeed = maxSpeed,
                    effectiveDeceleration = aEff,
                    gradient = gradient
                });

                if (d >= obstacleDist) break;
            }

            curve.Sort((a, b) => a.distanceFromObstacle.CompareTo(b.distanceFromObstacle));
        }

        public void CalculateDetailed(float currentSpeed, float distanceToObstacle,
            List<float> gradientProfile, List<float> gradientDistances)
        {
            _emergencyCurve.Clear();
            _serviceCurve.Clear();

            float obstacleDist = Mathf.Max(0f, distanceToObstacle - _safetyMargin);

            _safeStopDistanceEmergency = ComputeSafeStopDistance(
                currentSpeed, _emergencyDecel, 0f);
            _safeStopDistanceService = ComputeSafeStopDistance(
                currentSpeed, _serviceDecel, 0f);

            BuildDetailedCurve(_emergencyCurve, obstacleDist, _emergencyDecel,
                gradientProfile, gradientDistances);
            BuildDetailedCurve(_serviceCurve, obstacleDist, _serviceDecel,
                gradientProfile, gradientDistances);
        }

        private void BuildDetailedCurve(List<BrakingPoint> curve, float obstacleDist,
            float nominalDecel, List<float> gradientProfile, List<float> gradientDistances)
        {
            float stepSize = 2f;
            int stepCount = Mathf.CeilToInt(obstacleDist / stepSize) + 1;

            curve.Add(new BrakingPoint
            {
                distanceFromObstacle = 0f,
                permittedSpeed = 0f,
                effectiveDeceleration = nominalDecel,
                gradient = 0f
            });

            for (int i = 1; i <= stepCount; i++)
            {
                float d = i * stepSize;
                if (d > obstacleDist) d = obstacleDist;

                float gradient = InterpolateGradient(d, gradientProfile, gradientDistances);
                float aEff = nominalDecel + Gravity * gradient / 1000f;
                if (aEff <= 0f) aEff = 0.01f;

                float trAe = _reactionTime * aEff;
                float discriminant = trAe * trAe + 2f * aEff * d;
                float maxSpeed = -trAe + Mathf.Sqrt(Mathf.Max(0f, discriminant));

                curve.Add(new BrakingPoint
                {
                    distanceFromObstacle = d,
                    permittedSpeed = maxSpeed,
                    effectiveDeceleration = aEff,
                    gradient = gradient
                });

                if (d >= obstacleDist) break;
            }

            curve.Sort((a, b) => a.distanceFromObstacle.CompareTo(b.distanceFromObstacle));
        }

        private float InterpolateGradient(float distance, List<float> gradientProfile,
            List<float> gradientDistances)
        {
            if (gradientProfile == null || gradientProfile.Count == 0) return 0f;
            if (gradientProfile.Count == 1) return gradientProfile[0];
            if (gradientDistances == null || gradientDistances.Count < gradientProfile.Count)
                return gradientProfile[0];

            for (int i = 1; i < gradientDistances.Count; i++)
            {
                if (distance <= gradientDistances[i])
                {
                    float t = (distance - gradientDistances[i - 1]) /
                              (gradientDistances[i] - gradientDistances[i - 1]);
                    return Mathf.Lerp(gradientProfile[i - 1], gradientProfile[i], t);
                }
            }

            return gradientProfile[gradientProfile.Count - 1];
        }

        public float GetEmergencyTargetSpeed(float distanceToObstacle)
        {
            return InterpolateSpeedFromCurve(_emergencyCurve, distanceToObstacle);
        }

        public float GetServiceTargetSpeed(float distanceToObstacle)
        {
            return InterpolateSpeedFromCurve(_serviceCurve, distanceToObstacle);
        }

        private float InterpolateSpeedFromCurve(List<BrakingPoint> curve, float distance)
        {
            if (curve == null || curve.Count == 0) return 0f;
            if (curve.Count == 1) return curve[0].permittedSpeed;

            for (int i = 1; i < curve.Count; i++)
            {
                if (distance <= curve[i].distanceFromObstacle)
                {
                    float t = (distance - curve[i - 1].distanceFromObstacle) /
                              (curve[i].distanceFromObstacle - curve[i - 1].distanceFromObstacle);
                    return Mathf.Lerp(curve[i - 1].permittedSpeed, curve[i].permittedSpeed, t);
                }
            }

            return curve[curve.Count - 1].permittedSpeed;
        }

        public bool IsEmergencyBrakingRequired(float currentSpeed, float distanceToObstacle)
        {
            return distanceToObstacle <= _safeStopDistanceEmergency;
        }

        public bool IsServiceBrakingRequired(float currentSpeed, float distanceToObstacle)
        {
            return distanceToObstacle <= _safeStopDistanceService;
        }

        public float ComputeOverspeedDelta(float currentSpeed, float distanceToObstacle)
        {
            float emergencyLimit = GetEmergencyTargetSpeed(distanceToObstacle);
            return currentSpeed - emergencyLimit;
        }
    }
}
