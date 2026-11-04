using Rocks;
using UnityEngine;

namespace Training
{
    public class EpisodeLayoutPicker
    {
        private readonly ArenaSettings settings;
        private readonly System.Random rng;
        private readonly SpawnClearance clearance = new SpawnClearance();
        private readonly float minUpDot;

        public EpisodeLayoutPicker(ArenaSettings settings, System.Random rng)
        {
            this.settings = settings;
            this.rng = rng;
            minUpDot = Mathf.Cos(settings.MaxSlope * Mathf.Deg2Rad);
        }

        public bool TryPick(TerrainSurface surface, Rect arena, Transform rover, out EpisodeLayout layout)
        {
            Rect usable = Inset(arena, settings.EdgeMargin);
            if (usable.width <= 0f || usable.height <= 0f)
                usable = arena;

            for (int attempt = 0; attempt < settings.PlacementAttempts; attempt++)
            {
                Vector2 spawnPoint = new Vector2(Range(usable.xMin, usable.xMax), Range(usable.yMin, usable.yMax));
                if (!TrySampleClear(surface, spawnPoint, settings.SpawnClearRadius, rover, out Vector3 spawnGround, out Vector3 spawnNormal))
                    continue;

                if (!TryPickGoal(surface, usable, spawnPoint, rover, out Vector3 goal))
                    continue;

                layout = new EpisodeLayout(SpawnPose(spawnGround, spawnNormal), goal);
                return true;
            }

            layout = default;
            return false;
        }

        private bool TryPickGoal(TerrainSurface surface, Rect usable, Vector2 spawnPoint, Transform rover, out Vector3 goal)
        {
            float minDistance = Mathf.Min(settings.MinGoalDistance, settings.MaxGoalDistance);
            float maxDistance = Mathf.Max(settings.MinGoalDistance, settings.MaxGoalDistance);

            for (int attempt = 0; attempt < settings.PlacementAttempts; attempt++)
            {
                float angle = Range(0f, 2f * Mathf.PI);
                float distance = Range(minDistance, maxDistance);
                Vector2 point = spawnPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                if (usable.Contains(point) && TrySampleClear(surface, point, settings.GoalClearRadius, rover, out goal, out _))
                    return true;
            }

            goal = default;
            return false;
        }

        private bool TrySampleClear(TerrainSurface surface, Vector2 point, float radius, Transform rover, out Vector3 ground, out Vector3 normal)
        {
            return surface.TrySample(point, out ground, out normal)
                   && normal.y >= minUpDot
                   && clearance.IsClear(ground, radius, rover);
        }

        private Pose SpawnPose(Vector3 ground, Vector3 normal)
        {
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion heading = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            return new Pose(ground, tilt * heading);
        }

        private float Range(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static Rect Inset(Rect rect, float margin)
        {
            return Rect.MinMaxRect(rect.xMin + margin, rect.yMin + margin, rect.xMax - margin, rect.yMax - margin);
        }
    }
}
