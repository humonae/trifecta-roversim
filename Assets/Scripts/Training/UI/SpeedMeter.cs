using UnityEngine;

namespace Training
{
    public class SpeedMeter
    {
        private readonly float interval;
        private float lastSimTime;
        private float lastRealTime = -1f;

        public SpeedMeter(float interval)
        {
            this.interval = interval;
        }

        public float Actual { get; private set; }

        public void Sample()
        {
            float now = Time.unscaledTime;
            if (lastRealTime < 0f)
            {
                lastRealTime = now;
                lastSimTime = Time.time;
                return;
            }

            float realElapsed = now - lastRealTime;
            if (realElapsed < interval)
                return;

            Actual = (Time.time - lastSimTime) / realElapsed;
            lastSimTime = Time.time;
            lastRealTime = now;
        }

        public bool IsBehind(float requested)
        {
            return Actual > 0f && Actual < requested * 0.9f;
        }
    }
}
