using Training;
using Unity.VisualScripting;
using UnityEngine;

// Holds the training panel's settings.
// TrainingRequestBuilder and TrainingOrganizer read these fields by name,
// so keep the field names in sync with them.

public class EvaluationSettings : MonoBehaviour
{
    public string modelSource;
    public bool TrainOnAll;
    public bool RandomPosition;

    public Vector3 Start;

    public Vector3 Goal;

    public int Seed;


    public void SetRandomPosition(float a)
    {
        RandomPosition = a != 0.0f;  
    }
 
    // Vector3 can't be passed from an Inspector UI event, so call these from code
    // (for example when the user clicks the terrain).
    public void SetStartPosition(Vector3 position) => Start = position;
 
    public void SetGoalPosition(Vector3 position) => Goal = position;

    
}