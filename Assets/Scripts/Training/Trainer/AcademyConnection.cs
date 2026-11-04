using System;
using System.Globalization;
using System.Reflection;
using Unity.MLAgents;
using UnityEngine;

namespace Training
{
    public static class AcademyConnection
    {
        public const int TrainerPort = 5004;
        public const string RuntimePortField = "k_RuntimePortVariable";
        public const string RuntimePortVariable = "MLAGENTS_RUNTIME_PORT";
        private const string PortFlag = "--mlagents-port";
        private const string PatchMenu = "Tools > RoverSim > Patch ML-Agents For In-App Training";

        public static bool CanConnect(out string error)
        {
            error = null;
            if (Application.isEditor)
                return true;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != PortFlag)
                    continue;

                int port;
                if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out port) && port == TrainerPort)
                    return true;

                error = $"This build was started with {PortFlag} {args[i + 1]}, but the trainer listens on {TrainerPort}.";
                return false;
            }

            return true;
        }

        public static bool Connect(QuitCommandHandler onTrainerQuit, out string error)
        {
            Disconnect();
            SetRuntimePort(TrainerPort.ToString(CultureInfo.InvariantCulture));
            CommunicatorFactory.Enabled = true;

            Academy academy = Academy.Instance;
            if (!academy.IsCommunicatorOn)
            {
                Disconnect();
                error = Application.isEditor
                    ? $"Python is running but ML-Agents could not connect to it. Check that Project Settings > ML-Agents > Connect to Trainer is on and the editor port is {TrainerPort}."
                    : $"Python is running but this build could not connect to it. Run {PatchMenu} in the editor, then rebuild.";
                return false;
            }

            if (!RedirectQuitCommand(academy, onTrainerQuit))
                Debug.LogWarning("[Trainer] Could not intercept the trainer's quit command, so the app may close when training finishes.");

            error = null;
            return true;
        }

        public static void Disconnect()
        {
            if (Academy.IsInitialized)
                Academy.Instance.Dispose();

            SetRuntimePort(null);
        }

        private static void SetRuntimePort(string port)
        {
            Environment.SetEnvironmentVariable(RuntimePortVariable, port);
        }

        private static bool RedirectQuitCommand(Academy academy, QuitCommandHandler handler)
        {
            const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;

            FieldInfo communicatorField = typeof(Academy).GetField("Communicator", instanceFlags);
            MethodInfo quitMethod = typeof(Academy).GetMethod("OnQuitCommandReceived", staticFlags);
            if (communicatorField == null || quitMethod == null)
                return false;

            var communicator = communicatorField.GetValue(academy) as ICommunicator;
            if (communicator == null)
                return false;

            var academyHandler = (QuitCommandHandler)Delegate.CreateDelegate(typeof(QuitCommandHandler), quitMethod);
            communicator.QuitCommandReceived -= academyHandler;
            communicator.QuitCommandReceived += handler;
            return true;
        }
    }
}
