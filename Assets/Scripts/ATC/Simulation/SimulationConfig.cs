using UnityEngine;

namespace ATC.Simulation
{
    [CreateAssetMenu(fileName = "SimulationConfig", menuName = "ATC/Simulation Config")]
    public class SimulationConfig : ScriptableObject
    {
        [Header("Physics Timestep")]
        public float fixedDeltaTime = 0.02f;
        public float gravity = 9.80665f;

        [Header("Track Defaults")]
        public float defaultSpeedLimit = 80f / 3.6f;
        public int arcLengthSamples = 200;

        [Header("Train Defaults")]
        public float defaultMaxSpeed = 80f / 3.6f;
        public float defaultMaxAcceleration = 1.0f;
        public float defaultServiceDeceleration = 1.2f;
        public float defaultEmergencyDeceleration = 1.4f;
        public float defaultCarLength = 20f;
        public int defaultCarCount = 6;
        public float defaultCouplingGap = 0.5f;
        public float defaultTrainMass = 200000f;

        [Header("Moving Block")]
        public float reactionTime = 2.5f;
        public float safetyMargin = 5f;
        public float coastingSpeedThreshold = 0.5f;
        public float standbySpeedThreshold = 0.01f;

        [Header("ATO PID")]
        public float kp = 1.2f;
        public float ki = 0.08f;
        public float kd = 0.15f;
        public float integralLimit = 5f;
        public float outputRateLimit = 2f;
        public float jerkLimit = 1.5f;

        [Header("Station Stop")]
        public float stationStopTolerance = 0.3f;
        public float approachDecelDistance = 50f;
        public float approachDecelRate = 0.6f;

        [Header("Resistance")]
        public float runningResistanceA = 1.5f;
        public float runningResistanceB = 0.006f;
        public float runningResistanceC = 0.00035f;
        public float rotatingMassFactor = 1.08f;
    }
}
