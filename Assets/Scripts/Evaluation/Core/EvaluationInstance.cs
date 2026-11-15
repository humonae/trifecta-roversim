using System;
using Rocks;
using Rover;
using Training;
using Unity.Cinemachine;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Evaluation
{
    public class EvaluationInstanceSetup
    {
        public int Index;
        public GameObject RoverPrefab;
        public ModelAsset Model;
        public InferenceDevice InferenceDevice;
        public bool DeterministicInference;
        public Epoch Epoch;
        public EvaluationRequest Request;
        public ArenaSettings Arena;
        public EpisodeRules Rules;
        public int Seed;
        // Picked start and goal (Start and Goal: Set on Preview). Null for random spawns and goals.
        public Vector3? FixedStart;
        public Vector3? FixedGoal;
    }

    // One rover running the trained model. Like TrainingInstance, but with no rewards and a fixed
    // number of episodes per tile + rock layout. Between layouts (and once it has used up its
    // episodes) the rover is parked: its GameObject is switched off so it can't drive or block the
    // other rovers.
    public class EvaluationInstance : MonoBehaviour
    {
        private EvaluationInstanceSetup setup;
        private EpisodeLayoutPicker layoutPicker;
        private System.Random headingRng;
        private EpisodeMonitor monitor;
        private readonly EpisodeTimer timer = new EpisodeTimer();

        private GameObject holder;
        private RoverAIController rover;
        private Transform roverRoot;
        private Transform chassis;
        private float groundClearance;
        private RockContacts rockContacts;
        private bool hitRock;
        private bool wheelsOnRock;

        private EpisodeLayout layout;
        private bool hasLayout;
        private bool attemptPending;
        private bool attemptRunning;
        private int attemptsLeft;
        private int layoutIndex;
        private float startDistance;

        public event Action<EvaluationInstance, AttemptResult> AttemptFinished;

        public int Index => setup != null ? setup.Index : -1;
        public RoverAIController Rover => rover;
        // The rover prefab's follow camera, for CameraSwitcher. Inactive while the rover is parked.
        public CinemachineCamera RoverCamera { get; private set; }
        public EpisodeTimer Timer => timer;
        public bool AttemptRunning => attemptRunning;
        // The current episode's goal (meaningful while AttemptRunning), for GoalMarkers.
        public Vector3 Goal => layout.Goal;
        public float GoalRadius => setup != null && setup.Rules != null ? setup.Rules.GoalRadius : 1.5f;
        public bool FinishedLayout => attemptsLeft <= 0 && !attemptPending && !attemptRunning;
        public Rect Arena => setup.Epoch.ArenaFor(Index);

        public bool Initialize(EvaluationInstanceSetup instanceSetup, out string error)
        {
            setup = instanceSetup;
            layoutPicker = new EpisodeLayoutPicker(setup.Arena, new System.Random(setup.Seed));
            headingRng = new System.Random(unchecked(setup.Seed * 17 + 3));
            monitor = new EpisodeMonitor(setup.Rules);
            return SpawnRover(out error);
        }

        // Starts this rover's episodes on the epoch's current tile and rocks. 0 episodes parks it.
        public void BeginLayout(int layout, int episodes)
        {
            layoutIndex = layout;
            attemptsLeft = episodes;
            Park();
            StartNextAttempt();
        }

        // Ends the current attempt without recording it, for example when the run is stopped.
        public void Abort()
        {
            attemptsLeft = 0;
            attemptPending = false;
            Park();
        }

        public void Teardown()
        {
            attemptRunning = false;
            attemptPending = false;
            if (rover != null)
                rover.EpisodeBeginning -= HandleEpisodeBeginning;
        }

        private bool SpawnRover(out string error)
        {
            holder = new GameObject("Rover");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);

            Vector2 start = setup.Epoch.ArenaFor(setup.Index).center;
            GameObject roverObject = Instantiate(setup.RoverPrefab, new Vector3(start.x, 0f, start.y), Quaternion.identity, holder.transform);
            rover = roverObject.GetComponentInChildren<RoverAIController>(true);
            if (rover == null)
            {
                error = $"'{setup.RoverPrefab.name}' has no {nameof(RoverAIController)}.";
                return false;
            }

            var behavior = rover.GetComponent<BehaviorParameters>();
            if (behavior == null)
            {
                error = $"'{setup.RoverPrefab.name}' has no {nameof(BehaviorParameters)} next to its {nameof(RoverAIController)}.";
                return false;
            }

            // Set before the agent first initializes, so it never tries to reach a trainer or fall back to the keyboard.
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            behavior.InferenceDevice = setup.InferenceDevice;
            behavior.DeterministicInference = setup.DeterministicInference;
            behavior.Model = setup.Model;
            rover.MaxStep = 0;

            roverRoot = roverObject.transform;
            chassis = rover.Chassis.transform;
            RoverCamera = roverObject.GetComponentInChildren<CinemachineCamera>(true);
            rover.EpisodeBeginning += HandleEpisodeBeginning;
            AttachRockSensors();

            rover.enabled = false;
            holder.SetActive(true);
            groundClearance = RoverClearance.Measure(roverRoot, chassis);
            holder.SetActive(false);
            rover.enabled = true;

            error = null;
            return true;
        }

        // Same contact detection as training: RockContactSensor on every articulation body.
        private void AttachRockSensors()
        {
            rockContacts = new RockContacts(rover.Chassis);
            foreach (ArticulationBody body in roverRoot.GetComponentsInChildren<ArticulationBody>(true))
                body.gameObject.AddComponent<RockContactSensor>().Contacts = rockContacts;
        }

        private void StartNextAttempt()
        {
            while (attemptsLeft > 0)
            {
                attemptsLeft--;

                Epoch epoch = setup.Epoch;
                EpisodeLayout picked;
                bool found = setup.FixedStart.HasValue
                    ? TryFixedLayout(epoch.Surface, out picked)
                    : layoutPicker.TryPick(epoch.Surface, epoch.ArenaFor(Index), roverRoot, out picked);
                if (found)
                {
                    layout = picked;
                    hasLayout = true;
                    attemptPending = true;
                    Launch();
                    return;
                }

                Debug.LogWarning($"[Evaluation] Rover {Index}: no clear spawn and goal found on '{epoch.Tile.name}'; episode skipped.", this);
                Report(new AttemptResult { Outcome = EvaluationReport.SkippedOutcome });
            }

            Park();
        }

        // The picked start and goal, snapped to the ground. The heading is random (seeded), as in training,
        // so episodes from the same spot still differ.
        private bool TryFixedLayout(TerrainSurface surface, out EpisodeLayout fixedLayout)
        {
            Vector3 start = setup.FixedStart.Value;
            Vector3 goalPoint = setup.FixedGoal.Value;
            if (!surface.TrySample(new Vector2(start.x, start.z), out Vector3 ground, out Vector3 normal) ||
                !surface.TrySample(new Vector2(goalPoint.x, goalPoint.z), out Vector3 goal, out _))
            {
                fixedLayout = default;
                return false;
            }

            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion heading = Quaternion.Euler(0f, (float)headingRng.NextDouble() * 360f, 0f);
            fixedLayout = new EpisodeLayout(new Pose(ground, tilt * heading), goal);
            return true;
        }

        // Switching the rover on runs OnEpisodeBegin (right away, or on the Academy's first step),
        // which lands in HandleEpisodeBeginning. If it is already on, ending the episode does the same.
        private void Launch()
        {
            if (holder.activeSelf)
            {
                rover.EpisodeInterrupted();
                return;
            }

            // Roughly in place already, in case the first OnEpisodeBegin waits for the Academy's first step.
            Pose spawn = ChassisPose(layout.Spawn);
            roverRoot.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, layout.Spawn.rotation.eulerAngles.y, 0f));
            holder.SetActive(true);
        }

        private void HandleEpisodeBeginning(RoverAIController agent)
        {
            if (!attemptPending)
            {
                // The agent restarted on its own, which shouldn't happen with MaxStep 0. Put it back at the spawn.
                if (hasLayout)
                {
                    Pose rest = ChassisPose(layout.Spawn);
                    agent.ResetRover(rest.position, rest.rotation);
                }
                return;
            }

            attemptPending = false;

            Pose spawn = ChassisPose(layout.Spawn);
            agent.ResetRover(spawn.position, spawn.rotation);
            agent.SetTarget(layout.Goal);

            timer.Restart(setup.Request.EpisodeSeconds);
            startDistance = EpisodeMonitor.HorizontalDistance(layout.Spawn.position, layout.Goal);
            rockContacts.Clear();
            hitRock = false;
            wheelsOnRock = false;
            attemptRunning = true;
        }

        private void FixedUpdate()
        {
            if (!attemptRunning || rover == null)
                return;

            timer.Advance(Time.fixedDeltaTime);

            hitRock |= rockContacts.ChassisTouching;
            wheelsOnRock |= rockContacts.WheelsTouching;
            rockContacts.Clear();

            EpisodeOutcome outcome = monitor.Evaluate(chassis, layout.Goal, Arena, timer);
            if (outcome != EpisodeOutcome.Running)
                EndAttempt(outcome);
        }

        private void EndAttempt(EpisodeOutcome outcome)
        {
            attemptRunning = false;

            float finalDistance = EpisodeMonitor.HorizontalDistance(chassis.position, layout.Goal);
            Report(AttemptResult.From(outcome, timer.Elapsed, startDistance, finalDistance, hitRock, wheelsOnRock, layout.Spawn.position, layout.Goal));

            StartNextAttempt();
        }

        private void Report(AttemptResult result)
        {
            result.Layout = layoutIndex;
            result.Tile = setup.Epoch.Tile != null ? setup.Epoch.Tile.name : "";
            result.Rover = Index;

            if (AttemptFinished != null)
                AttemptFinished.Invoke(this, result);
        }

        private void Park()
        {
            attemptRunning = false;
            if (holder != null && holder.activeSelf)
                holder.SetActive(false);
        }

        private Pose ChassisPose(Pose ground)
        {
            Vector3 up = ground.rotation * Vector3.up;
            return new Pose(ground.position + up * (groundClearance + setup.Arena.SpawnGap), ground.rotation);
        }
    }
}
