using System.IO;
using UnityEngine;

namespace Training
{
    public static class PythonProbe
    {
        public const float TimeoutSeconds = 60f;

        private const string FileName = "roversim_python_probe.py";

        private const string Script =
            "import sys\n" +
            "print('PY %d.%d.%d' % sys.version_info[:3])\n" +
            "try:\n" +
            "    import torch\n" +
            "    print('TORCH ' + torch.__version__)\n" +
            "except Exception as e:\n" +
            "    print('TORCH_ERR ' + repr(e))\n" +
            "try:\n" +
            "    import mlagents.trainers\n" +
            "    import mlagents.trainers.learn\n" +
            "    print('MLAGENTS ' + mlagents.trainers.__version__)\n" +
            "except Exception as e:\n" +
            "    print('MLAGENTS_ERR ' + repr(e))\n";

        public static string ScriptPath()
        {
            string path = Path.Combine(Application.temporaryCachePath, FileName);
            if (!File.Exists(path) || File.ReadAllText(path) != Script)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Script);
            }

            return path;
        }

        public static string Arguments()
        {
            return Quote(ScriptPath());
        }

        public static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
