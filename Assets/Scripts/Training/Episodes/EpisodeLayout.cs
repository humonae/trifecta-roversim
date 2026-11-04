using UnityEngine;

namespace Training
{
    public readonly struct EpisodeLayout
    {
        public readonly Pose Spawn;
        public readonly Vector3 Goal;

        public EpisodeLayout(Pose spawn, Vector3 goal)
        {
            Spawn = spawn;
            Goal = goal;
        }
    }
}
