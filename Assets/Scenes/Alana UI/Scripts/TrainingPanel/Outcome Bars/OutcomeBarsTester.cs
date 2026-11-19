using UnityEngine;
using Evaluation;

// Fills OutcomeBar rows with fake counts so you can check the bars without running anything.
// Add it to the same object as the bars view (e.g. "Reason"). Press Play, then right-click this
// component's header and choose "Fill with test data". Delete it when you're done.
public class OutcomeBarsTester : MonoBehaviour
{
    [Tooltip("Leave empty to use every OutcomeBar under this object.")]
    [SerializeField] private OutcomeBar[] bars;
    [SerializeField, Min(1)] private int total = 100;
    [SerializeField, Min(0)] private int success = 62;
    [SerializeField, Min(0)] private int timedOut = 18;
    [SerializeField, Min(0)] private int flipped = 12;
    [SerializeField, Min(0)] private int outOfBounds = 8;

    [ContextMenu("Fill with test data")]
    public void Fill()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[OutcomeBarsTester] Press Play first: the bars only move in Play mode.", this);
            return;
        }

        if (bars == null || bars.Length == 0)
            bars = GetComponentsInChildren<OutcomeBar>(true);

        foreach (OutcomeBar bar in bars)
        {
            if (bar != null)
                bar.Set(CountFor(bar.Outcome), total);
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        if (!Application.isPlaying) return;

        if (bars == null || bars.Length == 0)
            bars = GetComponentsInChildren<OutcomeBar>(true);

        foreach (OutcomeBar bar in bars)
        {
            if (bar != null)
                bar.Set(0, total);
        }
    }

    private int CountFor(OutcomeBar.Kind kind)
    {
        switch (kind)
        {
            case OutcomeBar.Kind.Success: return success;
            case OutcomeBar.Kind.TimedOut: return timedOut;
            case OutcomeBar.Kind.Flipped: return flipped;
            case OutcomeBar.Kind.OutOfBounds: return outOfBounds;
            default: return 0;
        }
    }
}