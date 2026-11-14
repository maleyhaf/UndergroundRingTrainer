using System;

namespace FightTrainer.Core
{
    /// <summary>
    /// Tiny 2D vector for the fight sim (the core stays free of UnityEngine so it can run headless).
    /// Ring coordinates, seen from above: x = left/right, y = toward the back of the ring (away from camera).
    /// </summary>
    [Serializable]
    public struct Vec2
    {
        public float x;
        public float y;

        public Vec2(float x, float y) { this.x = x; this.y = y; }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float Length { get { return (float)Math.Sqrt(x * x + y * y); } }

        public Vec2 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-6f ? new Vec2(x / l, y / l) : new Vec2(1f, 0f);
            }
        }

        /// <summary>Rotated 90° counter-clockwise (to the left when looking along this vector).</summary>
        public Vec2 Left { get { return new Vec2(-y, x); } }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.x + b.x, a.y + b.y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.x - b.x, a.y - b.y); }
        public static Vec2 operator -(Vec2 a) { return new Vec2(-a.x, -a.y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.x * s, a.y * s); }

        public static float Dot(Vec2 a, Vec2 b) { return a.x * b.x + a.y * b.y; }
        public static float Cross(Vec2 a, Vec2 b) { return a.x * b.y - a.y * b.x; }
        public static float Distance(Vec2 a, Vec2 b) { return (a - b).Length; }

        /// <summary>Signed angle in degrees from a to b (positive = b is counter-clockwise / to the left of a).</summary>
        public static float SignedAngle(Vec2 a, Vec2 b)
        {
            return (float)(Math.Atan2(Cross(a, b), Dot(a, b)) * 180.0 / Math.PI);
        }

        /// <summary>Rotate a unit direction toward a target direction by at most maxDegrees.</summary>
        public static Vec2 RotateTowards(Vec2 from, Vec2 to, float maxDegrees)
        {
            float angle = SignedAngle(from, to);
            if (Math.Abs(angle) <= maxDegrees) return to.Normalized;
            float step = (float)((angle > 0f ? maxDegrees : -maxDegrees) * Math.PI / 180.0);
            float c = (float)Math.Cos(step), s = (float)Math.Sin(step);
            return new Vec2(from.x * c - from.y * s, from.x * s + from.y * c).Normalized;
        }

        /// <summary>Shortest distance from point p to the segment a-b.</summary>
        public static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float len2 = Dot(ab, ab);
            float t = len2 > 1e-9f ? Dot(p - a, ab) / len2 : 0f;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return Distance(p, a + ab * t);
        }

        public override string ToString() { return "(" + x.ToString("0.00") + ", " + y.ToString("0.00") + ")"; }
    }
}
