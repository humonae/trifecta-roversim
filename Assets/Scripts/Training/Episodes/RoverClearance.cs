using UnityEngine;

namespace Training
{
    public static class RoverClearance
    {
        public static float Measure(Transform roverRoot, Transform chassis)
        {
            Physics.SyncTransforms();

            float lowest = LowestCollider(roverRoot);
            if (float.IsPositiveInfinity(lowest))
                lowest = LowestRenderer(roverRoot);

            if (float.IsPositiveInfinity(lowest))
            {
                Debug.LogWarning($"[Training] '{roverRoot.name}' has no colliders or meshes to measure its height from; spawning the chassis at ground level.");
                return 0f;
            }

            return Mathf.Max(0f, chassis.position.y - lowest);
        }

        private static float LowestCollider(Transform root)
        {
            float lowest = float.PositiveInfinity;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.enabled && !collider.isTrigger && collider.bounds.size.sqrMagnitude > 0f)
                    lowest = Mathf.Min(lowest, collider.bounds.min.y);
            }

            return lowest;
        }

        private static float LowestRenderer(Transform root)
        {
            float lowest = float.PositiveInfinity;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer.enabled && renderer.bounds.size.sqrMagnitude > 0f)
                    lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            }

            return lowest;
        }
    }
}
