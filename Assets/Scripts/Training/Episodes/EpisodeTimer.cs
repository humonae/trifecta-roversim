using UnityEngine;

namespace Training
{
    public class EpisodeTimer
    {
        public float Limit { get; private set; }
        public float Elapsed { get; private set; }

        public bool HasLimit => Limit > 0f;
        public bool Expired => HasLimit && Elapsed >= Limit;
        public float Remaining => HasLimit ? Mathf.Max(0f, Limit - Elapsed) : float.PositiveInfinity;

        public void Restart(float limit)
        {
            Limit = limit;
            Elapsed = 0f;
        }

        public void Advance(float deltaTime)
        {
            Elapsed += deltaTime;
        }
    }
}
