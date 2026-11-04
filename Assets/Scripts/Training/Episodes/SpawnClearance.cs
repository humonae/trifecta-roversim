using UnityEngine;

namespace Training
{
    public class SpawnClearance
    {
        private const float GroundGap = 0.2f;
        private const float CheckHeight = 2.5f;

        private readonly Collider[] hits = new Collider[32];

        public bool IsClear(Vector3 ground, float radius, Transform ignoreRoot)
        {
            if (radius <= 0f)
                return true;

            Vector3 bottom = ground + Vector3.up * (radius + GroundGap);
            Vector3 top = ground + Vector3.up * (radius + CheckHeight);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                if (hit is TerrainCollider)
                    continue;
                if (ignoreRoot != null && hit.transform.IsChildOf(ignoreRoot))
                    continue;
                return false;
            }

            return true;
        }
    }
}
