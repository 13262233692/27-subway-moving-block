using UnityEngine;
using System.Collections.Generic;

namespace ATC.Simulation
{
    public class SimulationManager : MonoBehaviour
    {
        [SerializeField] private SimulationConfig _config;
        [SerializeField] private TrackNetwork _trackNetwork;
        [SerializeField] private bool _autoStart = false;
        [SerializeField] private bool _realTime = true;
        [SerializeField] private float _timeScale = 1f;

        private readonly List<TrainConsist> _trains = new List<TrainConsist>();
        private readonly List<AtoController> _atoControllers = new List<AtoController>();
        private readonly List<MovingBlockStateMachine> _movingBlockControllers = new List<MovingBlockStateMachine>();
        private readonly List<TrainPositionController> _positionControllers = new List<TrainPositionController>();

        private float _simulationTime;
        private float _fixedAccumulator;
        private bool _isRunning;
        private int _stepCount;

        public float SimulationTime => _simulationTime;
        public bool IsRunning => _isRunning;
        public int StepCount => _stepCount;
        public float FixedDeltaTime => _config != null ? _config.fixedDeltaTime : 0.02f;
        public IReadOnlyList<TrainConsist> Trains => _trains;
        public TrackNetwork TrackNetwork => _trackNetwork;

        public event System.Action<float> OnBeforeStep;
        public event System.Action<float> OnAfterStep;
        public event System.Action<TrainConsist, MovingBlockState> OnTrainStateChanged;

        private void Awake()
        {
            _simulationTime = 0f;
            _fixedAccumulator = 0f;
            _isRunning = false;
            _stepCount = 0;
        }

        private void Start()
        {
            if (_autoStart)
                StartSimulation();
        }

        private void FixedUpdate()
        {
            if (!_isRunning) return;

            float dt = FixedDeltaTime;
            if (_realTime)
            {
                _fixedAccumulator += Time.fixedDeltaTime * _timeScale;
                while (_fixedAccumulator >= dt)
                {
                    StepSimulation(dt);
                    _fixedAccumulator -= dt;
                    _simulationTime += dt;
                    _stepCount++;
                }
            }
            else
            {
                StepSimulation(dt);
                _simulationTime += dt;
                _stepCount++;
            }
        }

        public void StartSimulation()
        {
            _isRunning = true;
            _simulationTime = 0f;
            _fixedAccumulator = 0f;
            _stepCount = 0;
        }

        public void StopSimulation()
        {
            _isRunning = false;
        }

        public void PauseSimulation()
        {
            _isRunning = false;
        }

        public void ResumeSimulation()
        {
            _isRunning = true;
        }

        public void StepOnce()
        {
            StepSimulation(FixedDeltaTime);
            _simulationTime += FixedDeltaTime;
            _stepCount++;
        }

        private void StepSimulation(float dt)
        {
            OnBeforeStep?.Invoke(dt);

            CapturePreviousStates();

            for (int i = 0; i < _atoControllers.Count; i++)
            {
                if (_atoControllers[i] != null && _atoControllers[i].isActiveAndEnabled)
                {
                    var prevState = _movingBlockControllers[i].CurrentState;
                    _atoControllers[i].UpdateControl(dt);
                    var newState = _movingBlockControllers[i].CurrentState;
                    if (newState != prevState)
                        OnTrainStateChanged?.Invoke(_trains[i], newState);
                }
            }

            OnAfterStep?.Invoke(dt);
        }

        private void CapturePreviousStates()
        {
        }

        public TrainConsist RegisterTrain(
            GameObject trainGo,
            TrackSegment startSegment,
            float startDistance,
            TrainConsist precedingTrain = null)
        {
            var consist = trainGo.GetComponent<TrainConsist>();
            if (consist == null)
                consist = trainGo.AddComponent<TrainConsist>();

            var posCtrl = trainGo.GetComponent<TrainPositionController>();
            if (posCtrl == null)
                posCtrl = trainGo.AddComponent<TrainPositionController>();

            var movingBlock = trainGo.GetComponent<MovingBlockStateMachine>();
            if (movingBlock == null)
                movingBlock = trainGo.AddComponent<MovingBlockStateMachine>();

            var ato = trainGo.GetComponent<AtoController>();
            if (ato == null)
                ato = trainGo.AddComponent<AtoController>();

            consist.SetPosition(startSegment, startDistance);
            posCtrl.Initialize(_trackNetwork);
            movingBlock.Initialize(consist);
            if (precedingTrain != null)
                movingBlock.SetPrecedingTrain(precedingTrain);
            ato.Initialize(consist, posCtrl, movingBlock);
            ato.SetMode(AtoMode.Automatic);

            _trains.Add(consist);
            _positionControllers.Add(posCtrl);
            _movingBlockControllers.Add(movingBlock);
            _atoControllers.Add(ato);

            return consist;
        }

        public void UnregisterTrain(TrainConsist train)
        {
            int idx = _trains.IndexOf(train);
            if (idx < 0) return;

            _trains.RemoveAt(idx);
            _positionControllers.RemoveAt(idx);
            _movingBlockControllers.RemoveAt(idx);
            _atoControllers.RemoveAt(idx);
        }

        public void SetPrecedingTrain(int followerIndex, int precedingIndex)
        {
            if (followerIndex >= 0 && followerIndex < _movingBlockControllers.Count &&
                precedingIndex >= 0 && precedingIndex < _trains.Count)
            {
                _movingBlockControllers[followerIndex].SetPrecedingTrain(_trains[precedingIndex]);
            }
        }

        public void SetPrecedingTrain(TrainConsist follower, TrainConsist preceding)
        {
            int idx = _trains.IndexOf(follower);
            if (idx >= 0)
                _movingBlockControllers[idx].SetPrecedingTrain(preceding);
        }

        public void SetAllScheduleSpeeds(float speed)
        {
            foreach (var ato in _atoControllers)
            {
                if (ato != null)
                    ato.SetScheduleSpeed(speed);
            }
        }

        public TrainConsist GetTrain(int index)
        {
            if (index >= 0 && index < _trains.Count)
                return _trains[index];
            return null;
        }

        public MovingBlockStateMachine GetMovingBlockController(int index)
        {
            if (index >= 0 && index < _movingBlockControllers.Count)
                return _movingBlockControllers[index];
            return null;
        }

        public AtoController GetAtoController(int index)
        {
            if (index >= 0 && index < _atoControllers.Count)
                return _atoControllers[index];
            return null;
        }

        public SimulationState GetSimulationState()
        {
            var state = new SimulationState
            {
                simulationTime = _simulationTime,
                stepCount = _stepCount,
                isRunning = _isRunning,
                trainCount = _trains.Count,
                trainStates = new TrainState[_trains.Count]
            };

            for (int i = 0; i < _trains.Count; i++)
            {
                state.trainStates[i] = new TrainState
                {
                    speed = _trains[i].CurrentSpeed,
                    speedKmh = _trains[i].CurrentSpeed * 3.6f,
                    headDistance = _trains[i].HeadDistance,
                    tailDistance = _trains[i].TailDistance,
                    movingBlockState = _movingBlockControllers[i].CurrentState,
                    targetSpeed = _atoControllers[i].TargetSpeed,
                    commandedAcceleration = _atoControllers[i].CommandedAcceleration,
                    distanceToPreceding = _movingBlockControllers[i].DistanceToPrecedingTail,
                    movementAuthority = _movingBlockControllers[i].MovementAuthority
                };
            }

            return state;
        }

        public struct TrainState
        {
            public float speed;
            public float speedKmh;
            public float headDistance;
            public float tailDistance;
            public MovingBlockState movingBlockState;
            public float targetSpeed;
            public float commandedAcceleration;
            public float distanceToPreceding;
            public float movementAuthority;
        }

        public struct SimulationState
        {
            public float simulationTime;
            public int stepCount;
            public bool isRunning;
            public int trainCount;
            public TrainState[] trainStates;
        }
    }
}
