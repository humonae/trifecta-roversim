using System;
using Evaluation;
using UnityEngine;

namespace Training
{
    // One set of run controls for both training and evaluation. The play/pause/stop buttons, speed
    // arrows and status label talk to this, and it forwards to:
    //  - whichever organizer is running or paused, otherwise
    //  - the one matching SimulationManager's mode (the Training / Evaluate tab).
    // The controls create it on demand, so it doesn't need to be added to the scene.
    public class SimulationRuns : MonoBehaviour, ISimulationRun
    {
        [SerializeField] private TrainingOrganizer training;
        [SerializeField] private EvaluationOrganizer evaluation;
        [Tooltip("Optional. Found automatically if empty. Its mode decides which run the controls drive while nothing is running.")]
        [SerializeField] private SimulationManager simulationManager;

        private static SimulationRuns instance;
        private ISimulationRun lastActive;
        private bool initialized;

        public event Action<TrainingState> StateChanged;
        public event Action<string> StatusChanged;
        public event Action<float> SpeedChanged;

        public ISimulationRun Active
        {
            get
            {
                if (training != null && training.State != TrainingState.Idle)
                    return training;
                if (evaluation != null && evaluation.State != TrainingState.Idle)
                    return evaluation;
                if (evaluation != null && simulationManager != null && simulationManager.settings.simMode == global::State.EVALUATE)
                    return evaluation;
                if (training != null)
                    return training;
                return evaluation;
            }
        }

        public TrainingState State => Active != null ? Active.State : TrainingState.Idle;
        public string Status => Active != null ? Active.Status : "";
        public string ProgressDetail => Active != null ? Active.ProgressDetail : null;
        public float Speed => Active != null ? Active.Speed : 1f;
        public float SimulatedSeconds => Active != null ? Active.SimulatedSeconds : 0f;
        public bool CanStartFromPlay => Active != null && Active.CanStartFromPlay;

        // The run the controls should use: this hub when the scene has an EvaluationOrganizer, otherwise
        // the given training organizer, exactly as before evaluation existed.
        public static ISimulationRun Resolve(TrainingOrganizer fallback)
        {
            if (instance == null)
                instance = FindAnyObjectByType<SimulationRuns>();

            if (instance == null)
            {
                if (FindAnyObjectByType<EvaluationOrganizer>() == null)
                    return fallback;

                instance = new GameObject("Simulation Runs").AddComponent<SimulationRuns>();
            }

            instance.Initialize(fallback);
            return instance;
        }

        private void Awake()
        {
            if (instance == null)
                instance = this;
            Initialize(null);
        }

        private void Initialize(TrainingOrganizer fallback)
        {
            if (training == null)
                training = fallback != null ? fallback : FindAnyObjectByType<TrainingOrganizer>();
            if (evaluation == null)
                evaluation = FindAnyObjectByType<EvaluationOrganizer>();
            if (simulationManager == null)
                simulationManager = FindAnyObjectByType<SimulationManager>();

            if (initialized)
                return;

            initialized = true;
            Subscribe(training);
            Subscribe(evaluation);
            lastActive = Active;
        }

        private void Subscribe(ISimulationRun run)
        {
            if (run == null)
                return;

            run.StateChanged += state => Forward(run);
            run.StatusChanged += status =>
            {
                if (run == Active && StatusChanged != null)
                    StatusChanged.Invoke(status);
            };
            run.SpeedChanged += speed =>
            {
                if (run == Active && SpeedChanged != null)
                    SpeedChanged.Invoke(speed);
            };
        }

        // Also catches the Evaluation panel being opened or closed while idle.
        private void Update()
        {
            if (Active != lastActive)
                Forward(Active);
        }

        private void Forward(ISimulationRun changed)
        {
            ISimulationRun active = Active;
            bool switched = active != lastActive;
            lastActive = active;

            if (changed != active && !switched)
                return;

            if (StateChanged != null)
                StateChanged.Invoke(State);
            if (switched && SpeedChanged != null)
                SpeedChanged.Invoke(Speed);
            if (StatusChanged != null)
                StatusChanged.Invoke(Status);
        }

        public void Play() { if (Active != null) Active.Play(); }
        public void Pause() { if (Active != null) Active.Pause(); }
        public void Stop() { if (Active != null) Active.Stop(); }
        public void SetTimeScale(float speed) { if (Active != null) Active.SetTimeScale(speed); }
        public void SetTimeScale(string text) { if (Active != null) Active.SetTimeScale(text); }
        public void SpeedUp() { if (Active != null) Active.SpeedUp(); }
        public void SlowDown() { if (Active != null) Active.SlowDown(); }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
