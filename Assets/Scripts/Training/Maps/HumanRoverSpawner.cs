using System;
using System.Collections.Generic;
using Evaluation;
using Rocks;
using Rover;
using Unity.Cinemachine;
using UnityEngine;

namespace Training
{
    // Keeps one drivable (keyboard) rover on the shown tile while no training or evaluation is running:
    //  - when a tile is shown (LoadMap.TileShown, including the start tile and reloading the same tile),
    //    the rover is removed and a new one is placed on a flat, rock-free spot near the tile's centre;
    //  - when training or evaluation starts, the rover is removed;
    //  - when it ends, a new rover is placed on whichever tile is shown.
    // Put it on GameManager and assign the HumanControlledRoverLQ prefab.
    public class HumanRoverSpawner : MonoBehaviour
    {
        [Tooltip("The keyboard-driven rover prefab (HumanControlledRoverLQ).")]
        [SerializeField] private GameObject roverPrefab;
        [Tooltip("Optional. Found automatically.")]
        [SerializeField] private LoadMap loadMap;
        [Tooltip("Optional. Found automatically.")]
        [SerializeField] private TrainingOrganizer training;
        [Tooltip("Optional. Found automatically.")]
        [SerializeField] private EvaluationOrganizer evaluation;

        [Header("Placement")]
        [Tooltip("Height of the wheels above the ground when the rover appears, in meters.")]
        [SerializeField, Min(0f)] private float spawnGap = 0.1f;
        [Tooltip("Steepest ground the rover may be placed on, in degrees.")]
        [SerializeField, Range(0f, 60f)] private float maxSlope = 20f;
        [Tooltip("No rock or other object may be within this distance of the spot.")]
        [SerializeField, Min(0f)] private float clearRadius = 2.5f;
        [Tooltip("Distance between the rings of spots tried around the tile's centre, in meters.")]
        [SerializeField, Min(0.5f)] private float searchStep = 4f;
        [Tooltip("Direction the rover faces, in degrees (0 = +Z, the tile's north).")]
        [SerializeField] private float heading;
        [Tooltip("Remove keyboard rovers already placed in the scene by hand when Play starts.")]
        [SerializeField] private bool removePlacedRovers = true;

        private readonly SpawnClearance clearance = new SpawnClearance();
        private GameObject pendingTile;
        private bool spawnPending;

        public GameObject Current { get; private set; }
        public CinemachineCamera CurrentCamera { get; private set; }
        public event Action<GameObject> RoverSpawned;

        private bool RunActive =>
            (training != null && training.State != TrainingState.Idle) ||
            (evaluation != null && evaluation.State != TrainingState.Idle);

        private void Awake()
        {
            if (loadMap == null)
                loadMap = FindAnyObjectByType<LoadMap>();
            if (training == null)
                training = FindAnyObjectByType<TrainingOrganizer>();
            if (evaluation == null)
                evaluation = FindAnyObjectByType<EvaluationOrganizer>();

            if (loadMap != null)
                loadMap.TileShown += HandleTileShown;
            if (training != null)
                training.StateChanged += HandleRunStateChanged;
            if (evaluation != null)
                evaluation.StateChanged += HandleRunStateChanged;
        }

        private void Start()
        {
            if (removePlacedRovers)
                RemovePlacedRovers();

            if (roverPrefab == null)
                Debug.LogWarning("[HumanRover] No Rover Prefab assigned; no drivable rover will be placed.", this);
            else if (roverPrefab.GetComponentInChildren<RoverAIController>(true) != null)
                Debug.LogWarning($"[HumanRover] '{roverPrefab.name}' is an AI rover. Assign HumanControlledRoverLQ.", this);

            // LoadMap may have shown its start tile before this subscribed.
            if (Current == null && !spawnPending)
                RequestSpawn(ShownTile());
        }

        private void OnDestroy()
        {
            if (loadMap != null)
                loadMap.TileShown -= HandleTileShown;
            if (training != null)
                training.StateChanged -= HandleRunStateChanged;
            if (evaluation != null)
                evaluation.StateChanged -= HandleRunStateChanged;
        }

        // Spawning waits a frame, so a newly shown tile's collider is in place and rocks removed at the
        // end of a run are gone before the spot is checked.
        private void Update()
        {
            if (!spawnPending)
                return;

            spawnPending = false;
            if (!RunActive)
                Spawn(pendingTile != null && pendingTile.activeInHierarchy ? pendingTile : ShownTile());
        }

        private void HandleTileShown(GameObject tile)
        {
            if (!RunActive)
                RequestSpawn(tile);
        }

        private void HandleRunStateChanged(TrainingState state)
        {
            if (RunActive)
            {
                spawnPending = false;
                Despawn();
            }
            else if (Current == null)
            {
                RequestSpawn(ShownTile());
            }
        }

        private void RequestSpawn(GameObject tile)
        {
            pendingTile = tile;
            spawnPending = true;
        }

        public void Despawn()
        {
            if (Current != null)
                Destroy(Current);
            Current = null;
            CurrentCamera = null;
        }

        private void Spawn(GameObject tile)
        {
            // The old rover is only destroyed at the end of this frame; don't let it block its own spot.
            Transform replaced = Current != null ? Current.transform : null;
            Despawn();
            if (roverPrefab == null)
                return;
            if (tile == null)
            {
                Debug.LogWarning("[HumanRover] No tile is shown, so the drivable rover wasn't placed. Load a tile from the map.", this);
                return;
            }

            var surface = new TerrainSurface(tile);
            Vector3 ground, normal;
            if (surface.IsEmpty || !FindSpot(surface, replaced, out ground, out normal))
            {
                Debug.LogWarning($"[HumanRover] Couldn't find ground on '{tile.name}' to place the drivable rover.", this);
                return;
            }

            Quaternion facing = Quaternion.Euler(0f, heading, 0f);
            GameObject rover = Instantiate(roverPrefab, ground, facing);
            rover.name = roverPrefab.name;

            ArticulationBody chassis = RootBody(rover);
            if (chassis == null)
            {
                Debug.LogWarning($"[HumanRover] '{roverPrefab.name}' has no articulation root; it was placed at ground level.", this);
            }
            else
            {
                // Same placement as the training rovers: the chassis's height above the rover's lowest
                // point, plus a small gap, along the ground's normal.
                float lift = RoverClearance.Measure(rover.transform, chassis.transform) + spawnGap;
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal) * facing;
                chassis.TeleportRoot(ground + normal * lift, tilt);
                chassis.linearVelocity = Vector3.zero;
                chassis.angularVelocity = Vector3.zero;
            }

            Current = rover;
            CurrentCamera = rover.GetComponentInChildren<CinemachineCamera>(true);
            RoverSpawned?.Invoke(rover);
        }

        // The tile's centre if it's flat and clear, else the nearest such spot on rings around it.
        // Falls back to the centre (whatever its slope) so the rover always appears somewhere.
        private bool FindSpot(TerrainSurface surface, Transform ignore, out Vector3 ground, out Vector3 normal)
        {
            Rect bounds = surface.BoundsXZ;
            Vector2 centre = bounds.center;
            float minUp = Mathf.Cos(maxSlope * Mathf.Deg2Rad);
            float maxRing = Mathf.Min(bounds.width, bounds.height) * 0.5f;

            for (float ring = 0f; ring <= maxRing; ring += searchStep)
            {
                int samples = ring <= 0f ? 1 : Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * ring / searchStep));
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * 2f * Mathf.PI / samples;
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                    if (surface.TrySample(point, out ground, out normal) && normal.y >= minUp
                        && clearance.IsClear(ground, clearRadius, ignore))
                        return true;
                }
            }

            return surface.TrySample(centre, out ground, out normal);
        }

        private GameObject ShownTile()
        {
            if (loadMap != null && loadMap.ShownTile != null)
                return loadMap.ShownTile;

            // Fallback: any visible terrain (the same rule TileProvider.CurrentTile uses).
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
            return terrains.Length > 0 ? terrains[0].gameObject : null;
        }

        // Keyboard rovers placed in the scene by hand: a KeyboardDriveInput without a RoverAIController,
        // at the top of the hierarchy (AI rovers live under their training or evaluation instance).
        private void RemovePlacedRovers()
        {
            var removed = new List<string>();
            foreach (KeyboardDriveInput input in FindObjectsByType<KeyboardDriveInput>(FindObjectsInactive.Include))
            {
                Transform root = input.transform.root;
                if (root.gameObject == Current || root.GetComponentInChildren<RoverAIController>(true) != null)
                    continue;

                removed.Add(root.name);
                Destroy(root.gameObject);
            }

            if (removed.Count > 0)
                Debug.Log($"[HumanRover] Removed the rover(s) placed in the scene ({string.Join(", ", removed)}); " +
                          "the drivable rover is now placed on the shown tile. You can delete them from the scene.", this);
        }

        private static ArticulationBody RootBody(GameObject rover)
        {
            foreach (ArticulationBody body in rover.GetComponentsInChildren<ArticulationBody>(true))
            {
                if (body.isRoot)
                    return body;
            }

            return null;
        }
    }
}
