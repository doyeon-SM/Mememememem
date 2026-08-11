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

        private readonly struct RollKey
        {
            public readonly float Time;
            public readonly float Roll;

            public RollKey(float time, float roll)
            {
                Time = time;
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

        private static readonly Key[] AxeKeys =
        {
            new Key(0f, new Vector3(0.12f, 0.22f, 1f), 0f),
            new Key(0.10f, new Vector3(0.34f, 0.38f, 0.86f), -3f),
            new Key(0.20f, new Vector3(0.42f, 0.86f, 0.28f), -5f),
            new Key(0.30f, new Vector3(0.40f, 0.82f, 0.12f), -3f),
            new Key(0.39f, new Vector3(0.30f, -0.18f, 0.94f), 1f),
            new Key(0.50f, new Vector3(0.25f, -0.30f, 0.90f), 3f),
            new Key(0.64f, new Vector3(0.20f, -0.22f, 0.96f), 2f),
            new Key(0.76f, new Vector3(0.16f, -0.12f, 1f), 1f),
            new Key(0.90f, new Vector3(0.12f, 0.22f, 1f), 0f),
            new Key(1f, new Vector3(0.12f, 0.22f, 1f), 0f)
        };

        private static readonly Key[] HoeGrassCutKeys =
        {
            new Key(0f, new Vector3(0.12f, 0.22f, 1f), 0f),
            new Key(0.10f, new Vector3(0.35f, -0.45f, 0.82f), 0f),
            new Key(0.20f, new Vector3(0.25f, -0.95f, 0.18f), 0f),
            new Key(0.30f, new Vector3(0.20f, -0.96f, 0.20f), 0f),
            new Key(0.39f, new Vector3(0.10f, 0.32f, 0.94f), 0f),
            new Key(0.50f, new Vector3(0.08f, 0.65f, 0.75f), 0f),
            new Key(0.64f, new Vector3(0.10f, 0.45f, 0.88f), 0f),
            new Key(0.76f, new Vector3(0.12f, 0.22f, 1f), 0f),
            new Key(0.90f, new Vector3(0.12f, 0.22f, 1f), 0f),
            new Key(1f, new Vector3(0.12f, 0.22f, 1f), 0f)
        };

        // The axe body motion is retained, while these offsets keep the hooked
        // blade near middle height and make its concave cutting edge lead.
        private static readonly RollKey[] HoeInwardRollKeys =
        {
            new RollKey(0f, 0f),
            new RollKey(0.10f, 30f),
            new RollKey(0.18f, 10f),
            new RollKey(0.24f, 5f),
            new RollKey(0.30f, 5f),
            new RollKey(0.45f, 5f),
            new RollKey(0.60f, 25f),
            new RollKey(0.76f, 0f),
            new RollKey(0.90f, 0f),
            new RollKey(1f, 0f)
        };

        public static float EvaluateAxeBladeAlignmentWeight(float normalizedTime)
        {
            float enter = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.12f, 0.28f, normalizedTime));
            float exit = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.54f, 0.80f, normalizedTime));
            return enter * exit;
        }

        public static float EvaluateHoeInwardRoll(float normalizedTime)
        {
            float time = Mathf.Clamp01(normalizedTime);
            for (int i = 1; i < HoeInwardRollKeys.Length; i++)
            {
                if (time > HoeInwardRollKeys[i].Time) continue;

                RollKey previous = HoeInwardRollKeys[i - 1];
                RollKey next = HoeInwardRollKeys[i];
                float segmentTime = Mathf.InverseLerp(previous.Time, next.Time, time);
                return Mathf.Lerp(previous.Roll, next.Roll, Mathf.SmoothStep(0f, 1f, segmentTime));
            }

            return HoeInwardRollKeys[HoeInwardRollKeys.Length - 1].Roll;
        }

        public static bool TryEvaluate(
            ToolMotionType motionType,
            float normalizedTime,
            out Vector3 direction,
            out float roll)
        {
            Key[] keys;
            switch (motionType)
            {
                case ToolMotionType.Axe:
                    keys = AxeKeys;
                    break;
                case ToolMotionType.Hoe:
                    keys = HoeGrassCutKeys;
                    break;
                case ToolMotionType.Pickaxe:
                    keys = PickaxeKeys;
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
