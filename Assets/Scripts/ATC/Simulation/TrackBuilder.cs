using UnityEngine;
using ATC.Track;
using ATC.Train;
using ATC.Signalling;
using ATC.Simulation;

namespace ATC.Simulation
{
    public class TrackBuilder : MonoBehaviour
    {
        [Header("Spline Settings")]
        [SerializeField] private float _segmentLength = 100f;
        [SerializeField] private int _segmentCount = 5;
        [SerializeField] private float _curvature = 0f;
        [SerializeField] private float _speedLimit = 80f / 3.6f;
        [SerializeField] private float _gradient = 0f;

        [Header("Build")]
        [SerializeField] private bool _buildOnStart = false;

        private TrackNetwork _network;

        public TrackNetwork Network => _network;

        private void Start()
        {
            if (_buildOnStart)
                BuildStraightTrack();
        }

        public TrackNetwork BuildStraightTrack()
        {
            EnsureNetwork();
            _network.RegisterAllInChildren();

            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;

            for (int i = 0; i < _segmentCount; i++)
            {
                Vector3 p0 = origin + direction * (i * _segmentLength);
                Vector3 p3 = origin + direction * ((i + 1) * _segmentLength);

                float curveOffset = Mathf.Sin(i * _curvature) * _segmentLength * 0.3f;
                Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                Vector3 p1 = p0 + direction * (_segmentLength * 0.33f) + right * curveOffset;
                Vector3 p2 = p3 - direction * (_segmentLength * 0.33f) + right * curveOffset * 0.5f;

                var segGo = new GameObject($"TrackSeg_{i:D3}");
                segGo.transform.SetParent(transform);
                segGo.transform.position = (p0 + p3) * 0.5f;

                var trackSeg = segGo.AddComponent<TrackSegment>();
                var curve = new BezierSplineCurve();
                curve.AddSegment(p0, p1, p2, p3);
                trackSeg.Initialize(curve);

                _network.RegisterSegment(trackSeg);
            }

            ConnectSegments();

            return _network;
        }

        public TrackNetwork BuildLoopTrack(float radius, int segmentCount)
        {
            EnsureNetwork();
            _network.RegisterAllInChildren();

            float circumference = 2f * Mathf.PI * radius;
            float segLen = circumference / segmentCount;
            Vector3 center = transform.position;

            TrackSegment firstSeg = null;
            TrackSegment prevSeg = null;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle0 = (i / (float)segmentCount) * 2f * Mathf.PI;
                float angle1 = ((i + 1) / (float)segmentCount) * 2f * Mathf.PI;
                float angleMid = (angle0 + angle1) * 0.5f;

                Vector3 p0 = center + new Vector3(Mathf.Cos(angle0), 0f, Mathf.Sin(angle0)) * radius;
                Vector3 p3 = center + new Vector3(Mathf.Cos(angle1), 0f, Mathf.Sin(angle1)) * radius;

                Vector3 tangent0 = new Vector3(-Mathf.Sin(angle0), 0f, Mathf.Cos(angle0));
                Vector3 tangent1 = new Vector3(-Mathf.Sin(angle1), 0f, Mathf.Cos(angle1));

                float handleScale = segLen * 0.33f;
                Vector3 p1 = p0 + tangent0 * handleScale;
                Vector3 p2 = p3 - tangent1 * handleScale;

                var segGo = new GameObject($"TrackSeg_{i:D3}");
                segGo.transform.SetParent(transform);

                var trackSeg = segGo.AddComponent<TrackSegment>();
                var curve = new BezierSplineCurve();
                curve.AddSegment(p0, p1, p2, p3);
                trackSeg.Initialize(curve);

                _network.RegisterSegment(trackSeg);

                if (i == 0) firstSeg = trackSeg;
                if (prevSeg != null)
                    prevSeg.ConnectNext(trackSeg, true);

                prevSeg = trackSeg;
            }

            if (prevSeg != null && firstSeg != null)
                prevSeg.ConnectNext(firstSeg, true);

            return _network;
        }

        private void EnsureNetwork()
        {
            _network = GetComponent<TrackNetwork>();
            if (_network == null)
                _network = gameObject.AddComponent<TrackNetwork>();
        }

        private void ConnectSegments()
        {
            var segments = _network.AllSegments;
            for (int i = 0; i < segments.Count - 1; i++)
            {
                segments[i].ConnectNext(segments[i + 1], true);
            }
        }
    }
}
