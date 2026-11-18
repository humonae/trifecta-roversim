using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Training
{
    // A quick check, without Python, that a file is an ML-Agents training checkpoint.
    // torch.save writes a zip archive with a pickled state dict in <name>/data.pkl; an ML-Agents
    // checkpoint's dict has the keys "Policy" and "global_step", and its policy has
    // "network_body._body_endoder" layers (ML-Agents' own spelling).
    // Run Training still reads the checkpoint properly with Python (CheckpointInspector).
    public static class CheckpointFile
    {
        private const long MaxPickleBytes = 32L * 1024 * 1024;
        private static readonly string[] RequiredNames = { "Policy", "global_step", "_body_endoder" };

        // reason: short, for a label. detail: the full explanation, for the log or a message.
        public static bool Check(string path, out string reason, out string detail)
        {
            reason = null;
            detail = null;
            string name = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(path))
                return Fail("no file selected", "No .pt file was selected.", out reason, out detail);

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".onnx" || extension == ".sentis")
                return Fail("exported model, not a checkpoint",
                    $"'{name}' is an exported model, which can't be trained further. Pick the run's .pt checkpoint instead: " +
                    "<results folder>/<run>/<behavior>/<behavior>-<step>.pt, or checkpoint.pt for the latest.", out reason, out detail);
            if (extension != ".pt")
                return Fail("not a .pt file", $"'{name}' isn't a .pt checkpoint.", out reason, out detail);
            if (!File.Exists(path))
                return Fail("file not found", $"The checkpoint '{path}' doesn't exist.", out reason, out detail);

            byte[] pickle;
            try
            {
                pickle = ReadPickle(path);
            }
            catch (InvalidDataException)
            {
                return Fail("not a PyTorch checkpoint", $"'{name}' isn't a PyTorch checkpoint (it isn't a torch.save archive).", out reason, out detail);
            }
            catch (Exception e)
            {
                return Fail("couldn't be read", $"'{name}' couldn't be read: {e.Message}", out reason, out detail);
            }

            if (pickle == null)
                return Fail("not a PyTorch checkpoint", $"'{name}' isn't a PyTorch checkpoint (no data.pkl inside).", out reason, out detail);

            foreach (string required in RequiredNames)
            {
                if (IndexOf(pickle, Encoding.ASCII.GetBytes(required)) < 0)
                    return Fail("not an ML-Agents checkpoint",
                        $"'{name}' is a PyTorch file but not an ML-Agents training checkpoint (no '{required}' in it). " +
                        "Use a .pt from a training run's results folder.", out reason, out detail);
            }

            return true;
        }

        private static byte[] ReadPickle(string path)
        {
            using (var archive = ZipFileOpen(path))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // "<archive name>/data.pkl" (older torch versions: "archive/data.pkl").
                    if (!entry.FullName.EndsWith("/data.pkl", StringComparison.Ordinal) && entry.FullName != "data.pkl")
                        continue;
                    if (entry.Length > MaxPickleBytes)
                        throw new IOException("its data.pkl is unexpectedly large");

                    using (Stream stream = entry.Open())
                    using (var memory = new MemoryStream((int)Math.Max(0, entry.Length)))
                    {
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }
                }
            }

            return null;
        }

        private static ZipArchive ZipFileOpen(string path)
        {
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try
            {
                return new ZipArchive(file, ZipArchiveMode.Read, false);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j])
                    j++;
                if (j == needle.Length)
                    return i;
            }

            return -1;
        }

        private static bool Fail(string shortReason, string fullReason, out string reason, out string detail)
        {
            reason = shortReason;
            detail = fullReason;
            return false;
        }
    }
}
