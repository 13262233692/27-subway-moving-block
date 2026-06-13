using UnityEngine;
using System.Collections.Generic;

namespace ATC.Track
{
    public class TrackNetwork : MonoBehaviour
    {
        [SerializeField] private List<TrackSegment> _allSegments = new List<TrackSegment>();

        public IReadOnlyList<TrackSegment> AllSegments => _allSegments;
        public int SegmentCount => _allSegments.Count;

        public void RegisterSegment(TrackSegment segment)
        {
            if (segment != null && !_allSegments.Contains(segment))
                _allSegments.Add(segment);
        }

        public void UnregisterSegment(TrackSegment segment)
        {
            if (segment != null)
                _allSegments.Remove(segment);
        }

        public void RegisterAllInChildren()
        {
            _allSegments.Clear();
            var segments = GetComponentsInChildren<TrackSegment>();
            _allSegments.AddRange(segments);
        }

        public List<TrackSegment> FindPath(TrackSegment from, TrackSegment to)
        {
            if (from == null || to == null) return null;
            if (from == to) return new List<TrackSegment> { from };

            var visited = new HashSet<TrackSegment>();
            var queue = new Queue<List<TrackSegment>>();
            var startPath = new List<TrackSegment> { from };
            queue.Enqueue(startPath);
            visited.Add(from);

            while (queue.Count > 0)
            {
                var currentPath = queue.Dequeue();
                var current = currentPath[currentPath.Count - 1];

                if (current == to)
                    return currentPath;

                foreach (var next in current.NextSegments)
                {
                    if (next != null && !visited.Contains(next))
                    {
                        visited.Add(next);
                        var newPath = new List<TrackSegment>(currentPath) { next };
                        queue.Enqueue(newPath);
                    }
                }
            }

            return null;
        }

        public float ComputePathDistance(List<TrackSegment> path)
        {
            if (path == null) return float.MaxValue;
            float dist = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] != null)
                    dist += path[i].Length;
            }
            return dist;
        }

        public float DistanceBetween(TrackSegment fromSeg, float fromDist, TrackSegment toSeg, float toDist)
        {
            if (fromSeg == toSeg)
            {
                return toDist - fromDist;
            }

            if (fromSeg == null || toSeg == null) return float.MaxValue;

            float dist = fromSeg.Length - fromDist;
            TrackSegment seg = fromSeg;

            int maxHops = 50;
            while (seg.DefaultNextSegment != null && seg.DefaultNextSegment != toSeg && maxHops-- > 0)
            {
                seg = seg.DefaultNextSegment;
                if (seg == toSeg)
                    return dist + toDist;
                dist += seg.Length;
            }

            var path = FindPath(fromSeg, toSeg);
            if (path != null && path.Count >= 2)
            {
                dist = fromSeg.Length - fromDist;
                for (int i = 1; i < path.Count - 1; i++)
                    dist += path[i].Length;
                dist += toDist;
                return dist;
            }

            return float.MaxValue;
        }

        public TrackSegment FindNearestSegment(Vector3 worldPosition)
        {
            TrackSegment nearest = null;
            float minDist = float.MaxValue;

            foreach (var seg in _allSegments)
            {
                if (seg == null || !seg.IsInitialized) continue;

                float approxDist = Vector3.Distance(worldPosition, seg.transform.position);
                if (approxDist > minDist + 100f) continue;

                int samples = 20;
                for (int i = 0; i <= samples; i++)
                {
                    float d = (i / (float)samples) * seg.Length;
                    Vector3 pt = seg.GetPointAtDistance(d);
                    float dist = Vector3.Distance(worldPosition, pt);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = seg;
                    }
                }
            }

            return nearest;
        }

        public float FindNearestDistance(TrackSegment segment, Vector3 worldPosition)
        {
            if (segment == null || !segment.IsInitialized) return 0f;

            float bestDist = 0f;
            float minSqrDist = float.MaxValue;
            int samples = 50;

            for (int i = 0; i <= samples; i++)
            {
                float d = (i / (float)samples) * segment.Length;
                Vector3 pt = segment.GetPointAtDistance(d);
                float sqrDist = (worldPosition - pt).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    bestDist = d;
                }
            }

            return bestDist;
        }
    }
}
