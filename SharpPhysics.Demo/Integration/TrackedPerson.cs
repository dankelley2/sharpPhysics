#nullable enable
using System.Numerics;
using physics.Engine.Objects;

namespace SharpPhysics.Demo.Integration
{
    /// <summary>
    /// Represents tracking data for a single detected person.
    /// </summary>
    public sealed class TrackedPerson
    {
        public int PersonId { get; }
        public PhysicsObject? HeadBall { get; set; }
        public PhysicsObject? LeftHandBall { get; set; }
        public PhysicsObject? RightHandBall { get; set; }

        // Smoothed positions
        public Vector2 SmoothedHeadPos { get; set; }
        public Vector2 SmoothedLeftHandPos { get; set; }
        public Vector2 SmoothedRightHandPos { get; set; }

        // Full skeleton data (17 COCO keypoints)
        public Vector2[] RawKeypoints { get; } = new Vector2[17];
        public Vector2[] SmoothedKeypoints { get; } = new Vector2[17];
        public float[] KeypointConfidences { get; } = new float[17];

        public bool HasInitialPositions { get; set; } = false;
        public long LastSeenTimestamp { get; set; }
        public float Confidence { get; set; }

        public TrackedPerson(int personId)
        {
            PersonId = personId;
        }

        public IReadOnlyList<PhysicsObject> GetTrackingBalls()
        {
            var balls = new List<PhysicsObject>();
            if (HeadBall != null) balls.Add(HeadBall);
            if (LeftHandBall != null) balls.Add(LeftHandBall);
            if (RightHandBall != null) balls.Add(RightHandBall);
            return balls;
        }
    }
}
