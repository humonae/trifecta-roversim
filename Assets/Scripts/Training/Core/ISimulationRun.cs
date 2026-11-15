using System;

namespace Training
{
    // What the shared run controls (play/pause/stop, speed arrows, status label) need from a run.
    // TrainingOrganizer and Evaluation.EvaluationOrganizer both implement it, and SimulationRuns
    // forwards to whichever one is active.
    public interface ISimulationRun
    {
        TrainingState State { get; }
        string Status { get; }
        // Extra line for the status label while running, such as the timers. Null when there is nothing to add.
        string ProgressDetail { get; }
        float Speed { get; }
        float SimulatedSeconds { get; }
        bool CanStartFromPlay { get; }

        event Action<TrainingState> StateChanged;
        event Action<string> StatusChanged;
        event Action<float> SpeedChanged;

        void Play();
        void Pause();
        void Stop();
        void SetTimeScale(float speed);
        void SetTimeScale(string text);
        void SpeedUp();
        void SlowDown();
    }
}
