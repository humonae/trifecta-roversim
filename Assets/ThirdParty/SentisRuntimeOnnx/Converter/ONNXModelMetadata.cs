// Runtime stand-in for Unity.InferenceEngine.ONNXModelMetadata, which lives in the editor-only
// Unity.InferenceEngine.ONNX.Editor assembly. Same fields; the converter fills it in.
using System.Collections.Generic;

namespace Unity.InferenceEngine.RuntimeImport.Onnx
{
    struct ONNXModelMetadata
    {
        public string DocString;
        public string Domain;
        public long IRVersion;
        public Dictionary<string, string> MetadataProps;
        public string ProducerName;
        public string ProducerVersion;
        public long ModelVersion;
    }
}
