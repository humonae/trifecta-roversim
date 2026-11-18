using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class PythonSetupSettings
    {
        public const string EnvironmentVariable = "ROVERSIM_PYTHON";

        [Tooltip("Optional. A Python that already has ml-agents installed. It is used as is and never modified. Empty uses ROVERSIM_PYTHON, then the venv below, then python on the PATH.")]
        public string PythonExecutable = "";
        [Tooltip("Virtual environment the app creates and repairs. Relative paths start at the project folder (or the build folder).")]
        public string VenvFolder = "venv";
        [Tooltip("When the venv is missing or broken, offer to create it and install PyTorch and ML-Agents (the user is asked first). Off: just say what to install by hand.")]
        public bool InstallAutomatically = true;
        [Tooltip("Opened by the \"Python 3.10 is required\" popup. 3.10.11 is the last 3.10 release with installers.")]
        public string PythonDownloadUrl = "https://www.python.org/downloads/release/python-31011/";

        [Header("Versions (com.unity.ml-agents 4.1.0)")]
        public string MinPythonVersion = "3.10.1";
        public string MaxPythonVersion = "3.10.12";
        public string MlAgentsPackage = "mlagents==1.1.0";
        public string MinMlAgentsVersion = "1.1.0";
        [Tooltip("Installed together with ml-agents. ml-agents 1.1.0 still imports pkg_resources, which newer setuptools releases removed.")]
        public string SupportPackages = "setuptools<70";
        public string TorchPackage = "torch>=2.1.1,<=2.8.0";
        public string MaxTorchVersion = "2.8.99";
        [Tooltip("PyTorch package index used on Windows. Empty uses the default PyPI build.")]
        public string WindowsTorchIndexUrl = "https://download.pytorch.org/whl/cu121";
    }
}
