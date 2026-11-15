using System.Collections.Generic;
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

        // The map set opened in the UI (e.g. "Jezero"), or "" if none.
        public string MapName => loadMap != null ? loadMap.CurrentMap : "";

        // Grid labels ("C6") of every tile in the opened map set.
        public List<string> TileLabels()
        {
            return loadMap != null ? new List<string>(loadMap.CurrentTileLabels) : new List<string>();
        }

        public GameObject TileFor(string label)
        {
            return loadMap != null ? loadMap.TileFor(label) : null;
        }

        public GameObject CurrentTile()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
            if (terrains.Length > 1)
                Debug.LogWarning($"[Training] {terrains.Length} terrain tiles are visible; using '{terrains[0].name}'.");

            return terrains.Length > 0 ? terrains[0].gameObject : null;
        }

        public GameObject NextRandomTile()
        {
            if (loadMap != null)
                loadMap.LoadRandomTile();

            return CurrentTile();
        }

        public GameObject LoadTile(string label)
        {
            if (loadMap == null || !loadMap.LoadTile(label))
                return null;

            return CurrentTile();
        }
    }
}
