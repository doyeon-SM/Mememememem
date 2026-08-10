using UnityEngine;

namespace KMS
{
    public static class ToolActionGripPose
    {
        private readonly struct Key
        {
            public readonly float Time;
            public readonly Vector3 Direction;
            public readonly float Roll;

            public Key(float time, Vector3 direction, float roll = 0f)
            {
                Time = time;
                Direction = direction.normalized;
                Roll = roll;
            }
        }

        private static readonly Key[] PickaxeKeys =
        {
            new Key(0f, new Vector3(0.22f, -0.12f, 1f), -8f),
            new Key(0.12f, new Vector3(0.48f, 0.48f, 0.82f), -5f),
            new Key(0.24f, new Vector3(0.48f, 0.92f, 0.16f), 0f),
            new Key(0.36f, new Vector3(0.42f, 0.52f, -0.78f), 3f),
            new Key(0.48f, new Vector3(0.38f, -0.30f, 0.86f), 0f),
            new Key(0.56f, new Vector3(0.30f, -0.90f, 0.38f), -4f),
            new Key(0.68f, new Vector3(0.24f, -0.66f, 0.72f), -6f),
            new Key(0.80f, new Vector3(0.18f, -0.12f, 1f), -8f),
            new Key(0.90f, new Vector3(0.12f, 0.22f, 1f), 0f),
            new Key(1f, new Vector3(0.12f, 0.22f, 1f), 0f)
        };

        private static readonly Key[] HoeKeys =
        {
            new Key(0f, new Vector3(0.28f, -0.24f, 1f), 28f),
            new Key(0.12f, new Vector3(0.62f, 0.28f, 0.78f), 42f),
            new Key(0.24f, new Vector3(0.72f, 0.68f, 0.30f), 55f),
            new Key(0.36f, new Vector3(0.58f, 0.78f, -0.36f), 48f),
            new Key(0.48f, new Vector3(0.48f, -0.12f, 0.90f), 30f),
            new Key(0.60f, new Vector3(0.36f, -0.90f, 0.34f), 18f),
            new Key(0.72f, new Vector3(0.24f, -0.76f, 0.64f), 12f),
            new Key(0.82f, new Vector3(0.18f, -0.18f, 1f), 8f),
            new Key(0.90f, new Vector3(0.12f, 0.22f, 1f), 0f),
            new Key(1f, new Vector3(0.12f, 0.22f, 1f), 0f)
        };

        public static bool TryEvaluate(
            ToolMotionType motionType,
            float normalizedTime,
            out Vector3 direction,
            out float roll)
        {
            Key[] keys;
            switch (motionType)
            {
                case ToolMotionType.Pickaxe:
                    keys = PickaxeKeys;
                    break;
                case ToolMotionType.Hoe:
                    keys = HoeKeys;
                    break;
                default:
                    direction = Vector3.forward;
                    roll = 0f;
                    return false;
            }

            float time = Mathf.Clamp01(normalizedTime);
            for (int i = 1; i < keys.Length; i++)
            {
                if (time > keys[i].Time) continue;

                Key previous = keys[i - 1];
                Key next = keys[i];
                float t = Mathf.InverseLerp(previous.Time, next.Time, time);
                direction = Vector3.Slerp(previous.Direction, next.Direction, t).normalized;
                roll = Mathf.LerpAngle(previous.Roll, next.Roll, t);
                return true;
            }

            Key last = keys[keys.Length - 1];
            direction = last.Direction;
            roll = last.Roll;
            return true;
        }
    }
}
