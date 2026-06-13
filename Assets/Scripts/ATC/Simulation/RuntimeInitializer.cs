using UnityEngine;
using ATC.Track;
using ATC.Train;
using ATC.Signalling;
using ATC.Simulation;

namespace ATC.Simulation
{
    public class RuntimeInitializer : MonoBehaviour
    {
        [Header("Track")]
        [SerializeField] private TrackNetwork _trackNetwork;
        [SerializeField] private TrackSegment[] _trackSegments;

        [Header("Trains")]
        [SerializeField] private int _trainCount = 3;
        [SerializeField] private float _trainSpacing = 200f;
        [SerializeField] private float _initialSpeed = 0f;
        [SerializeField] private float _scheduleSpeed = 60f / 3.6f;
        [SerializeField] private TrackSegment _startSegment;

        [Header("Config")]
        [SerializeField] private SimulationConfig _config;

        private SimulationManager _simManager;

        private void Awake()
        {
            _simManager = FindObjectOfType<SimulationManager>();
            if (_simManager == null)
            {
                var go = new GameObject("SimulationManager");
                _simManager = go.AddComponent<SimulationManager>();
            }
        }

        private void Start()
        {
            InitializeTrack();
            InitializeTrains();
            _simManager.StartSimulation();
        }

        private void InitializeTrack()
        {
            if (_trackNetwork == null)
                _trackNetwork = FindObjectOfType<TrackNetwork>();

            if (_trackNetwork == null) return;

            if (_trackSegments != null && _trackSegments.Length > 0)
            {
                foreach (var seg in _trackSegments)
                {
                    if (seg != null && seg.IsInitialized)
                        _trackNetwork.RegisterSegment(seg);
                }
            }
            else
            {
                _trackNetwork.RegisterAllInChildren();
            }
        }

        private void InitializeTrains()
        {
            if (_trackNetwork == null || _trackNetwork.SegmentCount == 0) return;

            TrackSegment startSeg = _startSegment;
            if (startSeg == null)
                startSeg = _trackNetwork.AllSegments[0];

            TrainConsist preceding = null;

            for (int i = 0; i < _trainCount; i++)
            {
                GameObject trainGo = new GameObject($"Train_{i:D3}");

                var consist = _simManager.RegisterTrain(trainGo, startSeg, i * _trainSpacing, preceding);
                consist.SetSpeed(_initialSpeed);

                var ato = trainGo.GetComponent<AtoController>();
                if (ato != null)
                    ato.SetScheduleSpeed(_scheduleSpeed);

                preceding = consist;
            }
        }
    }
}
