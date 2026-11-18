using System.Collections.Generic;
using UnityEngine;

public class LoadMap : MonoBehaviour
{
    [Header("Map Terrain Arrays")]
    public GameObject[] JezeroTerrains;
    public GameObject[] TestTerrain;
    public GameObject[] TestTerrain2;

    public TileGrid gridSelection;

    [Tooltip("Tile of the opened map shown when the scene starts (its grid label, e.g. \"B4\"). Empty shows none.")]
    public string StartTile = "B4";

    // The tile currently shown, or null. Training and evaluation change it too.
    public GameObject ShownTile { get; private set; }
    // Fired every time a tile is shown, even the same one again (HumanRoverSpawner respawns the rover).
    public event System.Action<GameObject> TileShown;

    Dictionary<string, GameObject[]> maps; // Map name, dictionary of tiles
    readonly Dictionary<string, GameObject> currentTiles = new Dictionary<string, GameObject>();  // "C6" -> tile
    string currentMap = "";

    void Awake()
    {
        maps = new Dictionary<string, GameObject[]>
        {
            { "Jezero", JezeroTerrains },
            { "TestTerrain", TestTerrain },
            { "TestTerrain2", TestTerrain2 }
        };
        HideAll();

        OpenMap("Jezero");
    }

    void Start()
    {
        if (ShownTile == null && !string.IsNullOrEmpty(StartTile) && !LoadTile(StartTile))
            Debug.LogWarning($"Start tile \"{StartTile}\" isn't in the \"{currentMap}\" map.");
    }
    public void OpenMap(string mapName)
    {
        if (!maps.ContainsKey(mapName)){
            Debug.Log("This map set doesn't exist");
            return;
        }

        if(currentMap != mapName) // if they're the same, we do not have to remake the currentTilesList 
        {
            Debug.Log($"This map set \"{mapName}\" exists, setting current map and clearing current tiles");
            currentMap = mapName;
            currentTiles.Clear();

            foreach (var tile in maps[mapName])
            {
                if (tile == null) continue;
                // tile names must end in the grid label, e.g. "Jezero_C6", C6 also works
                string label = tile.name.Substring(tile.name.LastIndexOf('_') + 1);
                currentTiles[label] = tile;
            }

            gridSelection.SetAvailable(currentTiles.Keys);   // grid turns green where tiles exist
        }


    }

    // Read by Training.TileProvider (evaluation's Test on choices, the training record).
    public string CurrentMap => currentMap;
    public IReadOnlyCollection<string> CurrentTileLabels => currentTiles.Keys;
    public GameObject TileFor(string label) => label != null && currentTiles.TryGetValue(label, out var tile) ? tile : null;

    // Shows one tile of the opened map by its grid label ("C6"). False if there's no such tile.
    public bool LoadTile(string label)
    {
        if (label == null || !currentTiles.ContainsKey(label)) return false;
        ShowOnly(label);
        if (gridSelection != null) gridSelection.selectFromLabel(label);
        return true;
    }

    public void LoadSelectedTile()
    {
        string label = gridSelection.selectedTile;
        if (label == "" || !currentTiles.ContainsKey(label)) return;
        ShowOnly(label);
    }

    //a "Random tile" button
    public void LoadRandomTile()
    {
        if (currentTiles.Count == 0) return;
        var keys = new List<string>(currentTiles.Keys);
        string label = keys[Random.Range(0, keys.Count)];
        ShowOnly(label);
        gridSelection.selectFromLabel(label);

    }

    void ShowOnly(string label)
    {
        HideAll();                          // hides tiles from every map, not just this one
        currentTiles[label].SetActive(true);
        gridSelection.selectedTile = label;
        ShownTile = currentTiles[label];
        TileShown?.Invoke(ShownTile);
    }

    void HideAll()
    {
        ShownTile = null;
        foreach (var arr in maps.Values)
            foreach (var t in arr)
                if (t != null) t.SetActive(false);
    }
}