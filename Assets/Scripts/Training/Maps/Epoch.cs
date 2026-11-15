using System;
using System.Collections.Generic;
using Rocks;
using UnityEngine;

namespace Training
{
    public class Epoch
    {
        private readonly TileProvider tiles;
        private readonly RockPlacer rockPlacer;
        private readonly ArenaSettings arenaSettings;
        private readonly System.Random rng;
        private readonly EpisodeTimer timer = new EpisodeTimer();

        public Epoch(TileProvider tiles, RockPlacer rockPlacer, ArenaSettings arenaSettings, System.Random rng)
        {
            this.tiles = tiles;
            this.rockPlacer = rockPlacer;
            this.arenaSettings = arenaSettings;
            this.rng = rng;
        }

        // A spot rocks must not cover, such as a start or goal the user picked.
        public struct ClearSpot
        {
            public Vector3 Position;
            public float Radius;
        }

        // Evaluation with a picked start and goal: every rover uses this arena instead of randomly
        // planned ones, and rocks are kept off KeepClear. Training leaves both empty.
        public Rect? FixedArena;
        public readonly List<ClearSpot> KeepClear = new List<ClearSpot>();

        public GameObject Tile { get; private set; }
        public TerrainSurface Surface { get; private set; }
        public List<Rect> Arenas { get; private set; } = new List<Rect>();
        public EpisodeTimer Timer => timer;
        public int Number { get; private set; }

        public bool Begin(TrainingRequest request, int arenaCount, bool newTile, out string error)
        {
            return Begin(request, arenaCount, newTile ? (Func<GameObject>)tiles.NextRandomTile : tiles.CurrentTile, out error);
        }

        // chooseTile loads and returns the tile to use, such as a specific tile for evaluation.
        public bool Begin(TrainingRequest request, int arenaCount, Func<GameObject> chooseTile, out string error)
        {
            GameObject previous = Tile;
            GameObject tile = chooseTile();
            if (tile == null)
            {
                error = "No map tile is loaded. Pick a map (or load a tile from the map grid) before training.";
                return false;
            }

            if (previous != null && previous != tile)
                ClearRocks(previous);

            var surface = new TerrainSurface(tile);
            if (surface.IsEmpty)
            {
                error = $"'{tile.name}' has no Terrain component.";
                return false;
            }

            List<Rect> arenas = FixedArena.HasValue
                ? new List<Rect> { FixedArena.Value }
                : ArenaPlanner.Plan(surface.BoundsXZ, arenaSettings.Size, arenaCount, rng);
            if (!FixedArena.HasValue && arenas.Count < arenaCount)
                Debug.LogWarning($"[Training] '{tile.name}' only fits {arenas.Count} arena(s) of {arenaSettings.Size} m; {arenaCount - arenas.Count} rover(s) will share.");

            Tile = tile;
            Surface = surface;
            Arenas = arenas;
            Number++;

            PlaceRocks(request);
            timer.Restart(request.EpochSeconds);

            error = null;
            return true;
        }

        public void Advance(float deltaTime)
        {
            timer.Advance(deltaTime);
        }

        public Rect ArenaFor(int index)
        {
            return Arenas[index % Arenas.Count];
        }

        public void End()
        {
            if (Tile != null)
                ClearRocks(Tile);

            Tile = null;
            Surface = null;
            Arenas = new List<Rect>();
        }

        private void PlaceRocks(TrainingRequest request)
        {
            if (rockPlacer == null)
                return;

            if (!request.RocksEnabled || request.RockDensity <= 0f)
            {
                rockPlacer.ClearRocks(Tile);
                return;
            }

            var options = new RockPlacementOptions
            {
                AreaXZ = ArenaPlanner.Bounds(Arenas),
                Seed = rng.Next()
            };
            foreach (ClearSpot spot in KeepClear)
                options.KeepClear(spot.Position, spot.Radius);

            rockPlacer.PlaceRocks(request.RockDensity, Tile, options);
            Physics.SyncTransforms();
        }

        private void ClearRocks(GameObject tile)
        {
            if (rockPlacer != null)
                rockPlacer.ClearRocks(tile);
        }
    }
}
