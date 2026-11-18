using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Training
{
    // Runs a short Python script (with the same Python as the trainer) that reads a .pt checkpoint,
    // reports its network, and saves a CPU copy for ML-Agents to load. The copy is needed because a
    // checkpoint saved on a CUDA machine can't be opened by ML-Agents on a machine without CUDA.
    public class CheckpointInspector
    {
        private const string ResultPrefix = "ROVERSIM_CHECKPOINT ";
        private const string ScriptName = "inspect_checkpoint.py";
        private const float TimeoutSeconds = 180f;

        private PythonProcess process;
        private float deadline;

        public bool IsRunning { get; private set; }
        public CheckpointInfo Result { get; private set; }
        public string Error { get; private set; }

        public bool Begin(PythonCommand python, string checkpointPath, string copyPath, out string error)
        {
            Cancel();
            Result = null;
            Error = null;

            string scriptPath;
            try
            {
                string folder = Path.GetDirectoryName(copyPath);
                Directory.CreateDirectory(folder);
                scriptPath = Path.Combine(folder, ScriptName);
                File.WriteAllText(scriptPath, Script, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                error = $"Could not write the checkpoint reader script: {e.Message}";
                return false;
            }

            string arguments = TrainerCommandLine.Join(new[] { "-u", scriptPath, checkpointPath, copyPath });
            process = new PythonProcess("[Checkpoint]", false);
            if (!process.Start(python, arguments, PythonPaths.BaseDirectory, out error))
            {
                process.Dispose();
                process = null;
                return false;
            }

            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            IsRunning = true;
            error = null;
            return true;
        }

        // Call every frame until IsRunning is false; then Result or Error is set.
        public void Update()
        {
            if (!IsRunning)
                return;

            process.PumpOutput();
            if (process.HasExited)
            {
                process.WaitForExit(1000);
                process.PumpOutput();
                Finish(Parse(process));
                return;
            }

            if (Time.realtimeSinceStartup > deadline)
            {
                process.Kill();
                Error = $"Reading the checkpoint took longer than {TimeoutSeconds:0} s. Last output:\n{process.RecentOutput()}";
                Finish(null);
            }
        }

        public void Cancel()
        {
            if (process != null)
            {
                process.Kill();
                process.Dispose();
                process = null;
            }

            IsRunning = false;
        }

        private void Finish(CheckpointInfo info)
        {
            Result = info;
            if (info == null && Error == null)
                Error = $"The checkpoint reader exited with code {process.ExitCode} without a result. Last output:\n{process.RecentOutput()}";
            else if (info != null && !info.ok)
                Error = FirstLine(info.error);

            process.Dispose();
            process = null;
            IsRunning = false;
        }

        public static CheckpointInfo Parse(PythonProcess finished)
        {
            for (int i = finished.Lines.Count - 1; i >= 0; i--)
            {
                string line = finished.Lines[i];
                if (!line.StartsWith(ResultPrefix, StringComparison.Ordinal))
                    continue;

                try
                {
                    return JsonUtility.FromJson<CheckpointInfo>(line.Substring(ResultPrefix.Length));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Training] Could not parse the checkpoint reader's result: {e.Message}\n{line}");
                    return null;
                }
            }

            return null;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "The checkpoint could not be read.";

            int newline = text.IndexOf('\n');
            return newline < 0 ? text : text.Substring(0, newline).TrimEnd();
        }

        // Kept in C# so builds don't need an extra file. Tested against mlagents 1.1.0 with torch 2.8.0.
        private const string Script = @"# Written by RoverSim (Training.CheckpointInspector). Reads an ML-Agents .pt checkpoint,
# reports the network it was trained with as one JSON line, and saves a CPU copy for the trainer.
import json
import re
import sys

PREFIX = ""ROVERSIM_CHECKPOINT ""


def emit(info):
    print(PREFIX + json.dumps(info), flush=True)


def first_int(tensor, default=0):
    try:
        flat = tensor.reshape(-1)
        return int(flat[0].item()) if flat.numel() > 0 else default
    except Exception:
        return default


def encoder_kind(policy, index, names):
    if any(n.startswith(""sequential."") for n in names):
        return ""resnet""
    convs = [n for n in names if re.match(r""conv_layers\.\d+\.weight$"", n)]
    if len(convs) >= 3:
        return ""nature_cnn""
    if len(convs) == 2:
        kernel = policy[""network_body.observation_encoder.processors.%d.conv_layers.0.weight"" % index].shape[-1]
        return ""simple"" if kernel == 8 else ""match3""
    if any(n.startswith(""dense."") for n in names):
        return ""fully_connected""
    return None


def main():
    if len(sys.argv) < 3:
        return emit({""ok"": False, ""error"": ""Usage: inspect_checkpoint.py <checkpoint.pt> <copy.pt>""})
    source, copy_to = sys.argv[1], sys.argv[2]

    try:
        import torch
    except Exception as e:
        return emit({""ok"": False, ""error"": ""PyTorch could not be imported: %s"" % e})

    try:
        # map_location: a checkpoint saved on a CUDA machine can't otherwise be opened without CUDA.
        # weights_only: the picked file is never unpickled as code; the trainer loads our re-saved copy.
        state = torch.load(source, map_location=""cpu"", weights_only=True)
    except Exception as e:
        return emit({""ok"": False, ""error"": ""Could not read the checkpoint: %s: %s"" % (type(e).__name__, e)})

    if not isinstance(state, dict) or ""Policy"" not in state or ""global_step"" not in state:
        found = list(state.keys())[:6] if isinstance(state, dict) else type(state).__name__
        return emit({""ok"": False, ""error"": ""This isn't an ML-Agents training checkpoint (it contains %s)."" % (found,)})

    policy = state[""Policy""]
    step = 0
    for value in state[""global_step""].values():
        step = first_int(value)
        break

    body = {}
    for key, value in policy.items():
        match = re.match(r""network_body\._body_endoder\.seq_layers\.(\d+)\.weight$"", key)
        if match:
            body[int(match.group(1))] = tuple(value.shape)
    if not body:
        return emit({""ok"": False, ""error"": ""The checkpoint's policy has no body layers; it may be from an unsupported ML-Agents version.""})

    first = body[min(body)]
    hidden_units, num_layers, body_inputs = int(first[0]), len(body), int(first[1])

    processors = {}
    for key in policy:
        match = re.match(r""network_body\.observation_encoder\.processors\.(\d+)\.(.+)$"", key)
        if match:
            processors.setdefault(int(match.group(1)), []).append(match.group(2))

    encoders = set()
    visual_inputs = 0
    normalize = False
    for index, names in sorted(processors.items()):
        if any(n.startswith(""normalizer."") for n in names):
            normalize = True
            continue
        kind = encoder_kind(policy, index, names)
        if kind is not None:
            encoders.add(kind)
            visual_inputs += 1

    if len(encoders) > 1:
        return emit({""ok"": False, ""error"": ""The checkpoint mixes visual encoders (%s)."" % "", "".join(sorted(encoders))})

    discrete = policy.get(""discrete_act_size_vector"")
    discrete_branches = [int(v) for v in discrete.reshape(-1).tolist()] if discrete is not None else []

    info = {
        ""ok"": True,
        ""error"": """",
        ""step"": step,
        ""hidden_units"": hidden_units,
        ""num_layers"": num_layers,
        ""vis_encode_type"": encoders.pop() if encoders else """",
        ""normalize"": normalize,
        ""memory_size"": first_int(policy[""memory_size_vector""]) if ""memory_size_vector"" in policy else 0,
        ""continuous_actions"": first_int(policy[""continuous_act_size_vector""]) if ""continuous_act_size_vector"" in policy else 0,
        ""discrete_branches"": discrete_branches,
        ""visual_inputs"": visual_inputs,
        ""vector_size"": max(0, body_inputs - visual_inputs * hidden_units),
        ""has_critic"": ""Optimizer:critic"" in state,
        ""torch_version"": torch.__version__,
    }

    try:
        torch.save(state, copy_to)
    except Exception as e:
        return emit({""ok"": False, ""error"": ""Could not save a copy of the checkpoint to %s: %s"" % (copy_to, e)})

    emit(info)


if __name__ == ""__main__"":
    try:
        main()
    except Exception as e:
        emit({""ok"": False, ""error"": ""Unexpected error while reading the checkpoint: %s: %s"" % (type(e).__name__, e)})
";
    }
}
