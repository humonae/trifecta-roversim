using UnityEngine;

namespace Training
{
    public static class SpeedLimits
    {
        public const float Min = 0.1f;
        public const float Max = 100f;
        public const float MinAtLaunch = 1f;

        public static float Clamp(float speed)
        {
            return Mathf.Clamp(speed, Min, Max);
        }

        public static float Faster(float speed)
        {
            return Clamp(speed * 2f);
        }

        public static float Slower(float speed)
        {
            return Clamp(speed * 0.5f);
        }
    }
}
