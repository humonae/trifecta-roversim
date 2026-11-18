using System;
using Rover;
using UnityEngine;
using Unity.Cinemachine;

namespace Training
{
    public class TrainingInstance : MonoBehaviour
    {
        private InstanceSetup setup;
        private EpisodeLayoutPicker layoutPicker;
        private EpisodeMonitor monitor;
        private EpisodeRewards rewards;
        private RockContacts rockContacts;
        private readonly EpisodeTimer timer = new EpisodeTimer();
        private readonly SpawnSettle settle = new SpawnSettle();
        private readonly StallCheck stall = new StallCheck();

        private RoverAIController rover;
        private Transform roverRoot;
        
        public CinemachineCamera roverCamera;
        private Transform chassis;
        private float groundClearance;
        private EpisodeLayout layout;
        private bool hasPendingLayout;
        private bool episodeRunning;
        // Per episode, for TrainingReport.
        private bool hitRock;
        private bool wheelsOnRock;
        private float startDistance;
        private bool stalled;

        public event Action<TrainingInstance, EpisodeOutcome> EpisodeEnded;

        public int Index => setup != null ? setup.Index : -1;
        public RoverAIController Rover => rover;
        public Rect Arena => setup.Epoch.ArenaFor(Index);
        public EpisodeLayout Layout => layout;
        public EpisodeTimer Timer => timer;
        public bool EpisodeRunning => episodeRunning;
        // The current (or just-ended) episode; read by TrainingOrganizer when EpisodeEnded fires.
        public bool EpisodeHitRock => hitRock;
        public bool EpisodeWheelsOnRock => wheelsOnRock;
        public float EpisodeStartDistance => startDistance;
        public float EpisodeFinalDistance => chassis != null ? DistanceToGoal() : 0f;
        // The episode ended because the rover stopped making progress. Its outcome is still TimedOut.
        public bool EpisodeStalled => stalled;

        public bool Initialize(InstanceSetup instanceSetup, out string error)
        {
            setup = instanceSetup;
            layoutPicker = new EpisodeLayoutPicker(setup.Arena, new System.Random(setup.Seed));
            monitor = new EpisodeMonitor(setup.Rules);
            rewards = new EpisodeRewards(setup.Rules);

            if (!PickLayout())
            {
                error = $"Could not find a clear spawn point and goal on '{setup.Epoch.Tile.name}'. Try lowering obstacle density or goal distance, or raising the max slope.";
                return false;
            }

            hasPendingLayout = true;
            return SpawnRover(out error);
        }

        public void RestartEpisode()
        {
            if (rover == null)
                return;

            episodeRunning = false;
            hasPendingLayout = false;
            rover.EpisodeInterrupted();
        }

        public void Teardown()
        {
            episodeRunning = false;

            if (rover != null)
                rover.EpisodeBeginning -= HandleEpisodeBeginning;
        }

        private bool SpawnRover(out string error)
        {
            var holder = new GameObject("Rover");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);

            GameObject roverObject = Instantiate(setup.RoverPrefab, layout.Spawn.position, Quaternion.identity, holder.transform);
            rover = roverObject.GetComponentInChildren<RoverAIController>(true);
            if (rover == null)
            {
                error = $"'{setup.RoverPrefab.name}' has no {nameof(RoverAIController)}.";
                return false;
            }

            //for the camera


            roverRoot = roverObject.transform;
            roverCamera = roverObject.GetComponentInChildren<CinemachineCamera>(true);
            chassis = rover.Chassis.transform;
            rover.EpisodeBeginning += HandleEpisodeBeginning;
            AttachRockSensors();

            rover.enabled = false;
            holder.SetActive(true);
            groundClearance = RoverClearance.Measure(roverRoot, chassis);
            rover.enabled = true;

            error = null;
            return true;
        }

        private void AttachRockSensors()
        {
            rockContacts = new RockContacts(rover.Chassis);
            foreach (ArticulationBody body in roverRoot.GetComponentsInChildren<ArticulationBody>(true))
                body.gameObject.AddComponent<RockContactSensor>().Contacts = rockContacts;
        }

        private void HandleEpisodeBeginning(RoverAIController agent)
        {
            if (!hasPendingLayout && !PickLayout())
                Debug.LogWarning($"[Training] Instance {Index}: no clear spawn and goal found, reusing the previous ones.", this);

            hasPendingLayout = false;

            Pose spawn = ChassisPose(layout.Spawn);
            agent.ResetRover(spawn.position, spawn.rotation);
            agent.SetTarget(layout.Goal);

            timer.Restart(setup.Request.EpisodeSeconds);
            startDistance = EpisodeMonitor.HorizontalDistance(layout.Spawn.position, layout.Goal);
            // Not DistanceToGoal(): the chassis Transform still has the old pose here (see SpawnSettle).
            rewards.Begin(startDistance);
            rockContacts.Clear();
            hitRock = false;
            wheelsOnRock = false;
            stalled = false;
            settle.Begin(spawn.position);
            episodeRunning = true;
        }

        private bool PickLayout()
        {
            Epoch epoch = setup.Epoch;
            if (!layoutPicker.TryPick(epoch.Surface, epoch.ArenaFor(Index), roverRoot, out EpisodeLayout picked))
                return false;

            layout = picked;
            return true;
        }

        private void FixedUpdate()
        {
            if (!episodeRunning || rover == null)
                return;

            // Skip rewards, the timer and the end checks until the chassis reports the spawn pose.
            if (settle.Waiting)
            {
                rockContacts.Clear();
                SpawnSettle.Result result = settle.Step(chassis);
                if (result == SpawnSettle.Result.Waiting)
                    return;

                if (result == SpawnSettle.Result.GaveUp)
                    Debug.LogWarning($"[Training] Instance {Index}: rover not at its spawn after {SpawnSettle.MaxSteps} physics steps; starting the episode anyway.", this);

                rewards.Begin(DistanceToGoal());
                stall.Begin(DistanceToGoal());
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            timer.Advance(deltaTime);
            rover.AddReward(rewards.Step(DistanceToGoal(), ForwardSpeed(), rockContacts.WheelsTouching, rockContacts.ChassisTouching, deltaTime));
            hitRock |= rockContacts.ChassisTouching;
            wheelsOnRock |= rockContacts.WheelsTouching;
            rockContacts.Clear();

            EpisodeOutcome outcome = monitor.Evaluate(chassis, layout.Goal, Arena, timer);
            if (outcome != EpisodeOutcome.Running)
            {
                EndEpisode(outcome);
                return;
            }

            EpisodeRules rules = setup.Rules;
            if (stall.Step(DistanceToGoal(), deltaTime, rules.StallSeconds, rules.StallProgressMeters))
            {
                stalled = true;
                EndEpisode(EpisodeOutcome.TimedOut);
            }
        }

        private void EndEpisode(EpisodeOutcome outcome)
        {
            episodeRunning = false;
            // A stall is reported as a timeout, but it's the rover's own doing, so it's trained like a failure:
            // a penalty, and a real end of the episode. A real timeout is only the clock running out, so
            // ML-Agents is told the episode was cut short (EpisodeInterrupted) and it isn't penalised.
            rover.AddReward(stalled ? setup.Rules.StallPenalty : rewards.Final(outcome));

            if (EpisodeEnded != null)
                EpisodeEnded.Invoke(this, outcome);

            if (outcome == EpisodeOutcome.TimedOut && !stalled)
                rover.EpisodeInterrupted();
            else
                rover.EndEpisode();
        }

        private Pose ChassisPose(Pose ground)
        {
            Vector3 up = ground.rotation * Vector3.up;
            return new Pose(ground.position + up * (groundClearance + setup.Arena.SpawnGap), ground.rotation);
        }

        private float ForwardSpeed()
        {
            return Vector3.Dot(rover.Chassis.linearVelocity, chassis.forward);
        }

        private float DistanceToGoal()
        {
            return EpisodeMonitor.HorizontalDistance(chassis.position, layout.Goal);
        }
    }
}
