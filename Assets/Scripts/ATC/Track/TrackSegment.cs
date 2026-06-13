using UnityEngine;
using System.Collections.Generic;

namespace ATC.Track
{
    public enum TrackSegmentType
    {
        Mainline,
        Siding,
        Crossover,
        Platform,
        Depot
    }

    public class TrackSegment : MonoBehaviour
    {
        [SerializeField] private TrackSegmentType _segmentType = TrackSegmentType.Mainline;
        [SerializeField] private float _speedLimit = 80f / 3.6f;
        [SerializeField] private float _gradient = 0f;
        [SerializeField] private float _curvatureSpeedPenalty = 0f;
        [SerializeField] private List<TrackSegment> _nextSegments = new List<TrackSegment>();
        [SerializeField] private List<TrackSegment> _prevSegments = new List<TrackSegment>();
        [SerializeField] private int _defaultNextIndex;

        private BezierSplineCurve _curve;
        private bool _isInitialized;

        public TrackSegmentType SegmentType => _segmentType;
        public float SpeedLimit => _speedLimit;
        public float EffectiveSpeedLimit => Mathf.Max(0f, _speedLimit - _curvatureSpeedPenalty);
        public float Gradient => _gradient;
        public float Length => _curve != null ? _curve.TotalLength : 0f;
        public BezierSplineCurve Curve => _curve;
        public bool IsInitialized => _isInitialized;
        public IReadOnlyList<TrackSegment> NextSegments => _nextSegments;
        public IReadOnlyList<TrackSegment> PrevSegments => _prevSegments;
        public int DefaultNextIndex => _defaultNextIndex;

        public TrackSegment DefaultNextSegment =>
            _nextSegments.Count > 0 && _defaultNextIndex < _nextSegments.Count
                ? _nextSegments[_defaultNextIndex]
                : null;

        public void Initialize(BezierSplineCurve curve)
        {
            _curve = curve;
            _isInitialized = true;
        }

        public Vector3 GetPointAtDistance(float distance)
        {
            if (_curve == null) return transform.position;
            return _curve.GetPointAtDistance(distance);
        }

        public Vector3 GetTangentAtDistance(float distance)
        {
            if (_curve == null) return transform.forward;
            return _curve.GetTangentAtDistance(distance);
        }

        public Vector3 GetNormalAtDistance(float distance, Vector3 up)
        {
            if (_curve == null) return transform.up;
            return _curve.GetNormalAtDistance(distance, up);
        }

        public float GetCurvatureAtDistance(float distance)
        {
            if (_curve == null) return 0f;
            return _curve.GetCurvatureAtDistance(distance);
        }

        public void ConnectNext(TrackSegment next, bool isDefault = false)
        {
            if (next == null || next == this) return;
            if (!_nextSegments.Contains(next))
                _nextSegments.Add(next);
            if (!next._prevSegments.Contains(this))
                next._prevSegments.Add(this);
            if (isDefault)
                _defaultNextIndex = _nextSegments.IndexOf(next);
        }

        public void DisconnectNext(TrackSegment next)
        {
            int idx = _nextSegments.IndexOf(next);
            _nextSegments.Remove(next);
            next._prevSegments.Remove(this);
            if (_defaultNextIndex >= _nextSegments.Count)
                _defaultNextIndex = Mathf.Max(0, _nextSegments.Count - 1);
        }

        public void SetDefaultNext(int index)
        {
            if (index >= 0 && index < _nextSegments.Count)
                _defaultNextIndex = index;
        }

        public float GetEffectiveGradientAtDistance(float distance)
        {
            return _gradient;
        }
    }
}
