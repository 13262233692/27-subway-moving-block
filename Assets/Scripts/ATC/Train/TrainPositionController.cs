using UnityEngine;

namespace ATC.Train
{
    [RequireComponent(typeof(TrainConsist))]
    public class TrainPositionController : MonoBehaviour
    {
        [SerializeField] private bool _snapToTrackOnStart = true;
        [SerializeField] private Vector3 _trackUpVector = Vector3.up;

        private TrainConsist _consist;
        private TrackNetwork _network;

        public TrainConsist Consist => _consist;
        public TrackNetwork Network => _network;

        private void Awake()
        {
            _consist = GetComponent<TrainConsist>();
        }

        private void Start()
        {
            if (_snapToTrackOnStart)
                SnapAllCarsToTrack();
        }

        public void Initialize(TrackNetwork network)
        {
            _network = network;
            if (_snapToTrackOnStart)
                SnapAllCarsToTrack();
        }

        public void UpdatePosition(float deltaTime)
        {
            if (_consist.CurrentSegment == null || !_consist.CurrentSegment.IsInitialized) return;

            float displacement = _consist.CurrentSpeed * deltaTime;
            float newDistance = _consist.HeadDistance + displacement;

            TrackSegment segment = _consist.CurrentSegment;
            float overflow = newDistance - segment.Length;

            while (overflow > 0f && segment.NextSegments.Count > 0)
            {
                segment = segment.DefaultNextSegment ?? segment.NextSegments[0];
                newDistance = overflow;
                overflow = newDistance - segment.Length;
            }

            float underflow = newDistance;
            while (underflow < 0f && segment.PrevSegments.Count > 0)
            {
                segment = segment.PrevSegments[0];
                underflow += segment.Length;
                newDistance = underflow;
            }

            newDistance = Mathf.Clamp(newDistance, 0f, segment.Length);
            _consist.SetPosition(segment, newDistance);

            UpdateCarTransforms();
        }

        private void UpdateCarTransforms()
        {
            if (_consist.CurrentSegment == null) return;

            foreach (var car in _consist.Cars)
            {
                if (car.transform == null) continue;

                float carHeadDist = _consist.HeadDistance - car.distanceFromHead;
                float carCenterDist = carHeadDist - car.length * 0.5f;

                var resolved = ResolveDistance(carCenterDist, _consist.CurrentSegment);
                if (resolved.segment == null || !resolved.segment.IsInitialized) continue;

                Vector3 pos = resolved.segment.GetPointAtDistance(resolved.localDistance);
                Vector3 tangent = resolved.segment.GetTangentAtDistance(resolved.localDistance);

                car.transform.position = pos;
                car.transform.rotation = Quaternion.LookRotation(tangent, _trackUpVector);
            }
        }

        public void SnapAllCarsToTrack()
        {
            UpdateCarTransforms();
        }

        private ResolvedTrackPosition ResolveDistance(float targetDistance, TrackSegment referenceSegment)
        {
            if (referenceSegment == null)
                return new ResolvedTrackPosition { segment = null, localDistance = 0f };

            if (targetDistance >= 0f && targetDistance <= referenceSegment.Length)
            {
                return new ResolvedTrackPosition
                {
                    segment = referenceSegment,
                    localDistance = targetDistance
                };
            }

            if (targetDistance < 0f)
            {
                TrackSegment seg = referenceSegment;
                float remaining = targetDistance;
                int maxHops = 20;
                while (remaining < 0f && seg.PrevSegments.Count > 0 && maxHops-- > 0)
                {
                    seg = seg.PrevSegments[0];
                    remaining += seg.Length;
                }
                remaining = Mathf.Clamp(remaining, 0f, seg.Length);
                return new ResolvedTrackPosition { segment = seg, localDistance = remaining };
            }

            TrackSegment fwd = referenceSegment;
            float fwdDist = targetDistance;
            int maxFwd = 20;
            while (fwdDist > fwd.Length && fwd.NextSegments.Count > 0 && maxFwd-- > 0)
            {
                fwdDist -= fwd.Length;
                fwd = fwd.DefaultNextSegment ?? fwd.NextSegments[0];
            }
            fwdDist = Mathf.Clamp(fwdDist, 0f, fwd.Length);
            return new ResolvedTrackPosition { segment = fwd, localDistance = fwdDist };
        }

        private struct ResolvedTrackPosition
        {
            public TrackSegment segment;
            public float localDistance;
        }
    }
}
