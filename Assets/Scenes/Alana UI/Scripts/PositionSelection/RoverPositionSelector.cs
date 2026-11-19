using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Training;

// Lets the user pick a start and a goal on the terrain: Shift + left click once for the start,
// again for the goal. A third Shift + click starts over. Both points get a marker (green start,
// blue goal) drawn by GoalMarkers, and the events below say when something was picked.
public class RoverPositionSelector : MonoBehaviour
{
    [Tooltip("The camera the user looks through. Empty uses Camera.main.")]
    public Camera cam;

    [Tooltip("The layer the terrain is on. A click only counts when it hits this layer; everything else is ignored. " +
             "Empty (or Everything): looks for a layer named \"Ground\" or \"Terrain\".")]
    public LayerMask terrainLayer;

    [Tooltip("Goal ring size in metres. 0 = use the goal radius from the episode rules.")]
    public float radius;

    [Tooltip("Start ring size in metres.")]
    public float startRadius = 1f;

    [Header("Input")]
    [Tooltip("Only count clicks made while Shift is held, so orbiting the camera doesn't place points.")]
    [SerializeField] private bool requireShift = true;

    [Header("Rules")]
    [Tooltip("The goal has to be at least this far (metres, ignoring height) from the start. The evaluation rejects anything under 1 m.")]
    [SerializeField, Min(0f)] private float minSeparation = 1f;

    private Vector3? startPosition;
    private Vector3? endPosition;

    public Vector3? StartPosition => startPosition;
    public Vector3? EndPosition => endPosition;
    public bool HasStart => startPosition.HasValue;
    /// <summary>True once both the start and the goal are picked.</summary>
    public bool PositionsSet => startPosition.HasValue && endPosition.HasValue;

    /// <summary>Raised with the start point when it is picked.</summary>
    public event Action<Vector3> StartSet;

    /// <summary>Raised with the goal point when it is picked.</summary>
    public event Action<Vector3> GoalSet;

    /// <summary>Raised with (start, goal) once both are picked. Open your popup window from here.</summary>
    public event Action<Vector3, Vector3> SelectionComplete;

    /// <summary>Raised when the picks are cleared.</summary>
    public event Action SelectionCleared;

    private static readonly string[] TerrainLayerNames = { "Ground", "Terrain" };

    private void Awake()
    {
        // Nothing (0) and Everything (-1) aren't a terrain layer, so look for one by name.
        if (terrainLayer.value == 0 || terrainLayer.value == -1)
            terrainLayer = LayerMask.GetMask(TerrainLayerNames);

        if (terrainLayer.value == 0)
        {
            Debug.LogWarning("[RoverPositionSelector] Terrain Layer isn't set and no layer named \"Ground\" or \"Terrain\" exists. " +
                             "Set Terrain Layer to the terrain's layer. Picking is off until then.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        Vector2 screenPosition;
        if (!SelectPressed(out screenPosition))
            return;

        // A click on a button or panel belongs to the UI, not the scene behind it.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Camera camera = cam != null ? cam : Camera.main;
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayer, QueryTriggerInteraction.Ignore))
            HandlePointSelection(hit.point);
    }

    private void HandlePointSelection(Vector3 clickedPoint)
    {
        // Both points are already picked: this click starts a new selection.
        if (PositionsSet)
            ClearSelection();

        if (startPosition == null)
        {
            startPosition = clickedPoint;
            Debug.Log($"Start Position Set: {clickedPoint}");

            GoalMarkers markers = GoalMarkers.Instance;
            if (markers != null)
                markers.SetPickedStart(clickedPoint, startRadius);

            StartSet?.Invoke(clickedPoint);
        }
        else
        {
            if (HorizontalDistance(startPosition.Value, clickedPoint) < minSeparation)
            {
                Debug.Log($"Pick a goal at least {minSeparation:0.#} m from the start.");
                return;
            }

            endPosition = clickedPoint;
            Debug.Log($"End Position Set: {clickedPoint}");

            GoalMarkers markers = GoalMarkers.Instance;
            if (markers != null)
                markers.SetPickedGoal(clickedPoint, GoalRadius());

            GoalSet?.Invoke(clickedPoint);
            SelectionComplete?.Invoke(startPosition.Value, clickedPoint);
        }
    }

    /// <summary>Removes both picks and their markers. Call it from a Clear button, or when a new evaluation starts.</summary>
    public void ClearSelection()
    {
        bool hadAny = startPosition.HasValue || endPosition.HasValue;
        startPosition = null;
        endPosition = null;

        GoalMarkers markers = GoalMarkers.Instance;
        if (markers != null)
            markers.ClearPickedPoints();

        if (hadAny)
            SelectionCleared?.Invoke();
    }

    private float GoalRadius()
    {
        if (radius > 0f)
            return radius;

        TrainingOrganizer organizer = FindAnyObjectByType<TrainingOrganizer>();
        if (organizer != null && organizer.Rules != null)
            return organizer.Rules.GoalRadius;

        return 1.5f;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }

    private bool SelectPressed(out Vector2 position)
    {
        position = default;
        if (requireShift && !ShiftHeld())
            return false;

        return MousePressedThisFrame(out position);
    }

    // Works with the old Input Manager, the new Input System, or both (Project Settings > Player > Active Input Handling).
    private static bool ShiftHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.shiftKey.isPressed)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            return true;
#endif
        return false;
    }

    private static bool MousePressedThisFrame(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        position = default;
        return false;
    }
}