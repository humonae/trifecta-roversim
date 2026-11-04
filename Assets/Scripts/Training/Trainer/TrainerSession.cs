using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Training
{
    public class TrainerSession
    {
        private enum Phase
        {
            Closed,
            CheckingPort,
            FreeingPort,
            WaitingForTrainer,
            Ready,
            Connected,
            Closing
        }

        private const float ProbeInterval = 0.5f;
        private const int ProbeTimeoutMilliseconds = 1000;
        private const float GracefulShutdownTimeout = 15f;
        private const float ForcedStopTimeout = 10f;

        private Phase phase = Phase.Closed;
        private TrainerLaunchSettings launch;
        private TrainingRequest request;
        private string configPath;
        private PythonProcess process;
        private PythonCommand python;
        private Task<bool> probe;
        private float nextProbeTime;
        private float deadline;
        private bool closingGracefully;
        private Task<int> shutdownSignal;
        private Task<List<PortOwner>> ownerSearch;
        private List<PortOwner> owners;
        private bool forcedStop;

        public string RunId { get; private set; }
        public string ResultsDirectory { get; private set; }
        public string Error { get; private set; }
        public bool QuitReceived { get; private set; }
        public string Progress { get; private set; } = "";

        public bool IsLaunching => phase == Phase.CheckingPort || phase == Phase.FreeingPort || phase == Phase.WaitingForTrainer;
        public bool IsReady => phase == Phase.Ready;
        public bool IsConnected => phase == Phase.Connected;
        public bool IsClosed => phase == Phase.Closed;
        public bool ProcessExited => process != null && process.HasExited;
        public int ExitCode => process != null ? process.ExitCode : 0;
        public string RunDirectory => RunId != null ? Path.Combine(ResultsDirectory, RunId) : null;

        public bool BeginLaunch(TrainerLaunchSettings launchSettings, TrainingRequest trainingRequest, string trainerConfigPath, PythonCommand pythonCommand)
        {
            if (phase != Phase.Closed)
            {
                Error = "The trainer is already running.";
                return false;
            }

            launch = launchSettings;
            request = trainingRequest;
            configPath = trainerConfigPath;
            python = pythonCommand;
            Error = null;
            QuitReceived = false;
            RunId = null;
            ResultsDirectory = null;

            string error;
            if (!AcademyConnection.CanConnect(out error))
                return Fail(error);

            Progress = "Starting the Python trainer...";
            phase = Phase.CheckingPort;
            probe = PortProbe.IsListeningAsync(AcademyConnection.TrainerPort, ProbeTimeoutMilliseconds);
            return true;
        }

        public void Update()
        {
            if (process != null)
                process.PumpOutput();

            switch (phase)
            {
                case Phase.CheckingPort:
                    UpdatePortCheck();
                    break;
                case Phase.FreeingPort:
                    UpdateFreeingPort();
                    break;
                case Phase.WaitingForTrainer:
                    UpdateWaitingForTrainer();
                    break;
                case Phase.Closing:
                    UpdateClosing();
                    break;
            }
        }

        public bool Connect()
        {
            if (phase != Phase.Ready)
                return Fail("The trainer is not ready to connect.");

            string error;
            if (!AcademyConnection.Connect(OnTrainerQuit, out error))
                return Fail(error);

            phase = Phase.Connected;
            return true;
        }

        public string RecentOutput()
        {
            return process != null ? process.RecentOutput() : "";
        }

        public void BeginClose()
        {
            if (phase == Phase.Closed || phase == Phase.Closing)
                return;

            closingGracefully = phase == Phase.Connected;
            if (closingGracefully)
                AcademyConnection.Disconnect();
            else if (process != null)
                process.Kill();

            deadline = Time.realtimeSinceStartup + launch.ShutdownTimeout;
            phase = Phase.Closing;
            UpdateClosing();
        }

        public void CloseImmediately()
        {
            if (phase == Phase.Connected)
                AcademyConnection.Disconnect();
            else if (phase != Phase.Closing || !closingGracefully)
                KillProcess();

            ReleaseProcess();
            phase = Phase.Closed;
        }

        private void UpdatePortCheck()
        {
            if (!probe.IsCompleted)
                return;

            bool inUse = probe.Result;
            probe = null;

            if (inUse)
            {
                BeginFreeingPort();
                return;
            }

            Progress = "Starting the Python trainer...";
            StartProcess();
        }

        private void UpdateFreeingPort()
        {
            if (shutdownSignal != null)
            {
                if (!shutdownSignal.IsCompleted)
                    return;

                Debug.Log($"[Trainer] Sent the shutdown message to the trainer on port {AcademyConnection.TrainerPort} ({ShutdownResult()} delivered).");
                shutdownSignal = null;
            }

            if (ownerSearch != null)
            {
                if (!ownerSearch.IsCompleted)
                    return;

                StopLeftoverTrainers(ownerSearch.Result);
                ownerSearch = null;
                if (phase != Phase.FreeingPort)
                    return;
            }

            if (probe != null)
            {
                if (!probe.IsCompleted)
                    return;

                bool stillInUse = probe.Result;
                probe = null;

                if (!stillInUse)
                {
                    Progress = "Starting the Python trainer...";
                    StartProcess();
                    return;
                }
            }

            if (Time.realtimeSinceStartup > deadline)
            {
                if (!forcedStop)
                {
                    forcedStop = true;
                    Progress = "The old trainer did not exit; stopping it...";
                    ownerSearch = PortOwnerFinder.FindAsync(AcademyConnection.TrainerPort, PythonPaths.IsWindows);
                    deadline = Time.realtimeSinceStartup + ForcedStopTimeout;
                    return;
                }

                Fail($"Port {AcademyConnection.TrainerPort} is still in use{DescribeOwners()}. Close that program and try again.");
                return;
            }

            if (Time.realtimeSinceStartup >= nextProbeTime)
            {
                probe = PortProbe.IsListeningAsync(AcademyConnection.TrainerPort, ProbeTimeoutMilliseconds);
                nextProbeTime = Time.realtimeSinceStartup + ProbeInterval;
            }
        }

        private void BeginFreeingPort()
        {
            Debug.Log($"[Trainer] Port {AcademyConnection.TrainerPort} is in use, probably by a trainer left over from an earlier run. Asking it to shut down.");
            Progress = "Closing a trainer left over from an earlier run...";
            forcedStop = false;
            owners = null;
            shutdownSignal = TrainerShutdownSignal.SendAsync(AcademyConnection.TrainerPort);
            deadline = Time.realtimeSinceStartup + GracefulShutdownTimeout;
            nextProbeTime = Time.realtimeSinceStartup + ProbeInterval;
            phase = Phase.FreeingPort;
        }

        private void StopLeftoverTrainers(List<PortOwner> found)
        {
            owners = found;
            foreach (PortOwner owner in found)
            {
                if (!owner.MayBeForceStopped)
                {
                    Debug.LogWarning($"[Trainer] Not stopping {owner.Describe()} because it is not Python or an ML-Agents trainer.");
                    continue;
                }

                string error;
                Debug.Log($"[Trainer] Force-stopping {owner.Describe()} because it did not shut down when asked.");
                if (!ProcessKiller.KillTree(owner.Pid, PythonPaths.IsWindows, out error))
                    Debug.LogWarning($"[Trainer] Could not stop process {owner.Pid}: {error}");
            }
        }

        private string ShutdownResult()
        {
            int delivered = shutdownSignal.Status == TaskStatus.RanToCompletion ? shutdownSignal.Result : 0;
            return delivered == 1 ? "1 message" : $"{delivered} messages";
        }

        private string DescribeOwners()
        {
            if (owners == null || owners.Count == 0)
                return "";

            var names = new List<string>();
            foreach (PortOwner owner in owners)
                names.Add(owner.Describe());
            return " by " + string.Join(", ", names);
        }

        private void StartProcess()
        {
            if (!File.Exists(configPath))
            {
                Fail($"Trainer config not found at '{configPath}'.");
                return;
            }

            ResultsDirectory = PythonPaths.Resolve(request.ResultsDirectory);
            RunId = request.RunId;

            try
            {
                Directory.CreateDirectory(ResultsDirectory);
            }
            catch (Exception e)
            {
                Fail($"Could not create the results folder '{ResultsDirectory}': {e.Message}");
                return;
            }

            string arguments = TrainerCommandLine.Build(launch, request, configPath, ResultsDirectory, RunId);
            Debug.Log($"[Trainer] Launching: {python} {arguments}");

            process = new PythonProcess("[Trainer]");
            string error;
            if (!process.Start(python, arguments, PythonPaths.BaseDirectory, out error))
            {
                Fail(error);
                return;
            }

            deadline = Time.realtimeSinceStartup + launch.StartupTimeout;
            nextProbeTime = Time.realtimeSinceStartup + ProbeInterval;
            phase = Phase.WaitingForTrainer;
        }

        private void UpdateWaitingForTrainer()
        {
            if (probe != null)
            {
                if (!probe.IsCompleted)
                    return;

                bool listening = probe.Result;
                probe = null;

                if (listening)
                {
                    phase = Phase.Ready;
                    return;
                }
            }

            if (process.HasExited)
            {
                Fail($"The Python trainer exited with code {process.ExitCode} before it was ready. Last output:\n{process.RecentOutput()}");
                return;
            }

            if (Time.realtimeSinceStartup > deadline)
            {
                Fail($"The Python trainer did not start listening within {launch.StartupTimeout:0} s. Last output:\n{process.RecentOutput()}");
                return;
            }

            if (Time.realtimeSinceStartup >= nextProbeTime)
            {
                probe = PortProbe.IsListeningAsync(AcademyConnection.TrainerPort, ProbeTimeoutMilliseconds);
                nextProbeTime = Time.realtimeSinceStartup + ProbeInterval;
            }
        }

        private void UpdateClosing()
        {
            if (process != null && process.IsRunning)
            {
                if (Time.realtimeSinceStartup < deadline)
                    return;

                Debug.LogWarning($"[Trainer] Python did not exit within {launch.ShutdownTimeout:0} s; killing it.");
                process.Kill();
            }

            ReleaseProcess();
            phase = Phase.Closed;
        }

        private bool Fail(string error)
        {
            Error = error;
            KillProcess();
            ReleaseProcess();
            phase = Phase.Closed;
            return false;
        }

        private void KillProcess()
        {
            if (process != null)
                process.Kill();
        }

        private void ReleaseProcess()
        {
            probe = null;
            shutdownSignal = null;
            ownerSearch = null;
            if (process == null)
                return;

            process.PumpOutput();
            process.Dispose();
            process = null;
        }

        private void OnTrainerQuit()
        {
            QuitReceived = true;
        }
    }
}
