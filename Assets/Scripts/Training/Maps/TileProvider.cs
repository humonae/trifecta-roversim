using UnityEngine;

namespace Training
{
    public class TileProvider
    {
        private readonly LoadMap loadMap;

        public TileProvider(LoadMap loadMap)
        {
            this.loadMap = loadMap;
        }

        public GameObject CurrentTile()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
            if (terrains.Length > 1)
                Debug.LogWarning($"[Training] {terrains.Length} terrain tiles are visible; training on '{terrains[0].name}'.");

            return terrains.Length > 0 ? terrains[0].gameObject : null;
        }

        public GameObject NextRandomTile()
        {
            if (loadMap != null)
                loadMap.LoadRandomTile();

            return CurrentTile();
        }
    }
}
