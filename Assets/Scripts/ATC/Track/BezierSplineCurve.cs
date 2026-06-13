using UnityEngine;
using System.Collections.Generic;

namespace ATC.Track
{
    public class BezierSplineCurve
    {
        private readonly List<Vector3[]> _segments;
        private readonly List<float> _segmentLengths;
        private readonly List<float> _cumulativeLengths;
        private float _totalLength;
        private const int ArcLengthSamples = 200;

        public float TotalLength => _totalLength;
        public int SegmentCount => _segments.Count;

        public BezierSplineCurve()
        {
            _segments = new List<Vector3[]>();
            _segmentLengths = new List<float>();
            _cumulativeLengths = new List<float>();
            _totalLength = 0f;
        }

        public void AddSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _segments.Add(new[] { p0, p1, p2, p3 });
            RecalculateLengths();
        }

        public void AddContinuousSegment(Vector3 handleOut, Vector3 p3)
        {
            if (_segments.Count == 0)
                throw new System.InvalidOperationException("Cannot add continuous segment to empty spline");

            var lastSeg = _segments[_segments.Count - 1];
            Vector3 p0 = lastSeg[3];
            Vector3 p1 = 2f * p0 - lastSeg[2];
            Vector3 p2 = handleOut;
            _segments.Add(new[] { p0, p1, p2, p3 });
            RecalculateLengths();
        }

        public void AddSmoothSegment(Vector3 p3, float handleScale = 0.33f)
        {
            if (_segments.Count == 0)
                throw new System.InvalidOperationException("Cannot add smooth segment to empty spline");

            var lastSeg = _segments[_segments.Count - 1];
            Vector3 p0 = lastSeg[3];
            Vector3 tangent = (lastSeg[3] - lastSeg[2]).normalized;
            Vector3 p1 = p0 + tangent * Vector3.Distance(p0, p3) * handleScale;
            Vector3 p2 = p3 - (p3 - p0).normalized * Vector3.Distance(p0, p3) * handleScale;
            _segments.Add(new[] { p0, p1, p2, p3 });
            RecalculateLengths();
        }

        public void InsertSegment(int index, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _segments.Insert(index, new[] { p0, p1, p2, p3 });
            RecalculateLengths();
        }

        public void RemoveSegment(int index)
        {
            if (index >= 0 && index < _segments.Count)
            {
                _segments.RemoveAt(index);
                RecalculateLengths();
            }
        }

        public Vector3[] GetControlPoints(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _segments.Count)
                throw new System.ArgumentOutOfRangeException(nameof(segmentIndex));
            return _segments[segmentIndex];
        }

        public void SetControlPoint(int segmentIndex, int pointIndex, Vector3 position)
        {
            if (segmentIndex < 0 || segmentIndex >= _segments.Count)
                return;
            _segments[segmentIndex][pointIndex] = position;
            RecalculateLengths();
        }

        private void RecalculateLengths()
        {
            _segmentLengths.Clear();
            _cumulativeLengths.Clear();
            _totalLength = 0f;

            float cumulative = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                float len = ComputeSegmentArcLength(i);
                _segmentLengths.Add(len);
                cumulative += len;
                _cumulativeLengths.Add(cumulative);
            }
            _totalLength = cumulative;
        }

        private float ComputeSegmentArcLength(int segmentIndex)
        {
            var pts = _segments[segmentIndex];
            float length = 0f;
            Vector3 prev = EvaluateCubic(pts, 0f);
            for (int i = 1; i <= ArcLengthSamples; i++)
            {
                float t = i / (float)ArcLengthSamples;
                Vector3 curr = EvaluateCubic(pts, t);
                length += Vector3.Distance(prev, curr);
                prev = curr;
            }
            return length;
        }

        private static Vector3 EvaluateCubic(Vector3[] pts, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float uuu = uu * u;
            float tt = t * t;
            float ttt = tt * t;
            return uuu * pts[0] + 3f * uu * t * pts[1] + 3f * u * tt * pts[2] + ttt * pts[3];
        }

        private static Vector3 EvaluateCubicFirstDerivative(Vector3[] pts, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (pts[1] - pts[0])
                 + 6f * u * t * (pts[2] - pts[1])
                 + 3f * t * t * (pts[3] - pts[2]);
        }

        private static Vector3 EvaluateCubicSecondDerivative(Vector3[] pts, float t)
        {
            float u = 1f - t;
            return 6f * u * (pts[2] - 2f * pts[1] + pts[0])
                 + 6f * t * (pts[3] - 2f * pts[2] + pts[1]);
        }

        public Vector3 GetPoint(float t)
        {
            ResolveSegmentAndLocalT(ref t, out int segIdx, out float localT);
            return EvaluateCubic(_segments[segIdx], localT);
        }

        public Vector3 GetTangent(float t)
        {
            ResolveSegmentAndLocalT(ref t, out int segIdx, out float localT);
            Vector3 tangent = EvaluateCubicFirstDerivative(_segments[segIdx], localT);
            float mag = tangent.magnitude;
            return mag > 1e-6f ? tangent / mag : Vector3.forward;
        }

        public Vector3 GetNormal(float t, Vector3 up)
        {
            Vector3 tangent = GetTangent(t);
            Vector3 binormal = Vector3.Cross(tangent, up);
            float mag = binormal.magnitude;
            if (mag < 1e-6f)
            {
                binormal = Vector3.Cross(tangent, up == Vector3.up ? Vector3.right : Vector3.up);
                mag = binormal.magnitude;
            }
            binormal /= mag;
            return Vector3.Cross(binormal, tangent).normalized;
        }

        public Vector3 GetBinormal(float t, Vector3 up)
        {
            Vector3 tangent = GetTangent(t);
            Vector3 binormal = Vector3.Cross(tangent, up);
            float mag = binormal.magnitude;
            return mag > 1e-6f ? binormal / mag : Vector3.right;
        }

        public float GetCurvature(float t)
        {
            ResolveSegmentAndLocalT(ref t, out int segIdx, out float localT);
            Vector3 d1 = EvaluateCubicFirstDerivative(_segments[segIdx], localT);
            Vector3 d2 = EvaluateCubicSecondDerivative(_segments[segIdx], localT);
            float d1Mag = d1.magnitude;
            if (d1Mag < 1e-6f) return 0f;
            return Vector3.Cross(d1, d2).magnitude / (d1Mag * d1Mag * d1Mag);
        }

        public float GetRadius(float t)
        {
            float curvature = GetCurvature(t);
            return curvature > 1e-8f ? 1f / curvature : float.MaxValue;
        }

        public Vector3 GetPointAtDistance(float distance)
        {
            float t = DistanceToParameter(distance);
            return GetPoint(t);
        }

        public Vector3 GetTangentAtDistance(float distance)
        {
            float t = DistanceToParameter(distance);
            return GetTangent(t);
        }

        public Vector3 GetNormalAtDistance(float distance, Vector3 up)
        {
            float t = DistanceToParameter(distance);
            return GetNormal(t, up);
        }

        public float GetCurvatureAtDistance(float distance)
        {
            float t = DistanceToParameter(distance);
            return GetCurvature(t);
        }

        public float DistanceToParameter(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, _totalLength);
            if (distance <= 0f) return 0f;
            if (distance >= _totalLength) return 1f;

            int segIdx = FindSegmentForDistance(distance);
            float prevCum = segIdx > 0 ? _cumulativeLengths[segIdx - 1] : 0f;
            float localDist = distance - prevCum;

            float localT = BisectLocalParameter(segIdx, localDist);
            return (segIdx + localT) / _segments.Count;
        }

        public float ParameterToDistance(float t)
        {
            t = Mathf.Clamp01(t);
            ResolveSegmentAndLocalT(ref t, out int segIdx, out float localT);

            float dist = segIdx > 0 ? _cumulativeLengths[segIdx - 1] : 0f;
            dist += ComputePartialArcLength(segIdx, localT);
            return dist;
        }

        private int FindSegmentForDistance(float distance)
        {
            int lo = 0, hi = _cumulativeLengths.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_cumulativeLengths[mid] < distance)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private float BisectLocalParameter(int segmentIndex, float targetLocalDist)
        {
            float tLow = 0f, tHigh = 1f;
            for (int i = 0; i < 30; i++)
            {
                float tMid = (tLow + tHigh) * 0.5f;
                float midDist = ComputePartialArcLength(segmentIndex, tMid);
                if (midDist < targetLocalDist)
                    tLow = tMid;
                else
                    tHigh = tMid;
                if (tHigh - tLow < 1e-8f) break;
            }
            return (tLow + tHigh) * 0.5f;
        }

        private float ComputePartialArcLength(int segmentIndex, float tMax)
        {
            var pts = _segments[segmentIndex];
            int steps = Mathf.Max(1, Mathf.CeilToInt(tMax * ArcLengthSamples));
            float length = 0f;
            Vector3 prev = EvaluateCubic(pts, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float t = (i / (float)steps) * tMax;
                Vector3 curr = EvaluateCubic(pts, t);
                length += Vector3.Distance(prev, curr);
                prev = curr;
            }
            return length;
        }

        private void ResolveSegmentAndLocalT(ref float t, out int segIdx, out float localT)
        {
            t = Mathf.Clamp01(t);
            float scaledT = t * _segments.Count;
            segIdx = Mathf.FloorToInt(scaledT);
            if (segIdx >= _segments.Count) segIdx = _segments.Count - 1;
            localT = scaledT - segIdx;
            if (localT < 0f) localT = 0f;
            if (localT > 1f) localT = 1f;
        }

        public void Clear()
        {
            _segments.Clear();
            _segmentLengths.Clear();
            _cumulativeLengths.Clear();
            _totalLength = 0f;
        }

        public float GetSegmentLength(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _segmentLengths.Count) return 0f;
            return _segmentLengths[segmentIndex];
        }

        public float GetCumulativeLength(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _cumulativeLengths.Count) return 0f;
            return _cumulativeLengths[segmentIndex];
        }
    }
}
