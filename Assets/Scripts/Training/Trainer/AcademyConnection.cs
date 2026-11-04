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
        private const string PortFlag = "--mlagents-port";

        public static string RuntimePortVariable
        {
            get
            {
                FieldInfo field = typeof(Academy).GetField(RuntimePortField, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                return field != null && field.IsLiteral ? field.GetRawConstantValue() as string : null;
            }
        }

        public static bool IsPackagePatched => RuntimePortVariable != null;

        public static bool CanConnect(out string error)
        {
            error = null;
            if (Application.isEditor || IsPackagePatched)
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

            error = "This build can't connect to the Python trainer. Run Tools > RoverSim > Patch ML-Agents For In-App Training in the editor and rebuild, " +
                    $"or start the build with {PortFlag} {TrainerPort}.";
            return false;
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
                error = IsPackagePatched
                    ? "Python is running but ML-Agents could not connect to it."
                    : $"Python is running but ML-Agents could not connect to it. Check that Project Settings > ML-Agents > Connect to Trainer is on and the editor port is {TrainerPort}, " +
                      "or run Tools > RoverSim > Patch ML-Agents For In-App Training.";
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
            string variable = RuntimePortVariable;
            if (variable != null)
                Environment.SetEnvironmentVariable(variable, port);
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
