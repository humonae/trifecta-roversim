using System.Collections.Generic;
using Evaluation;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Training
{
    // Marks each rover's goal with a light beam and a ring the size of the goal radius, for the user's
    // cameras only.
    //
    // It draws per camera from URP's beginCameraRendering (like the rocks' InstancedRenderer), and only
    // for cameras with a CinemachineBrain (the user's view) plus the Scene view. The rover's sensor
    // cameras have no brain, so the policy never sees the marker. Nothing is added to the scene, so
    // physics, raycasts and sensors are untouched.
    //
    // One is created automatically when a scene loads. To change its look, add GoalMarkers to any
    // object (e.g. GameManager) and edit it there; to turn it off, add one and disable it.
    public class GoalMarkers : MonoBehaviour
    {
        public const string ResourcesMaterialName = "GoalMarkerMaterial";

        [Tooltip("Optional. A URP Unlit, transparent, two-sided material. Empty uses Resources/" + ResourcesMaterialName +
                 " (Tools > RoverSim > Create Goal Marker Material), or one made at runtime.")]
        [SerializeField] private Material material;
        [SerializeField] private Color color = new Color32(0x4F, 0xC3, 0xE0, 0xFF);

        [Header("Beam")]
        [SerializeField, Min(1f)] private float beamHeight = 25f;
        [SerializeField, Min(0.01f)] private float beamRadius = 0.12f;
        [Tooltip("Widens the beam with distance so it stays visible from far away: radius is at least distance × this.")]
        [SerializeField, Min(0f)] private float beamRadiusPerMeter = 0.004f;
        [SerializeField, Range(0f, 1f)] private float beamOpacity = 0.35f;

        [Header("Ring")]
        [Tooltip("The ring's outer edge is the goal radius from the episode rules: inside it counts as reaching the goal.")]
        [SerializeField, Min(0.01f)] private float ringWidth = 0.15f;
        [SerializeField, Range(0f, 1f)] private float ringOpacity = 0.85f;
        [SerializeField, Min(0f)] private float ringLift = 0.05f;

        [Header("Other")]
        [SerializeField] private bool pulse = true;
        [SerializeField] private bool showInSceneView = true;
        [Tooltip("Ring size used when the rover's rules can't be read.")]
        [SerializeField, Min(0.1f)] private float fallbackGoalRadius = 1.5f;

        private const float RefreshInterval = 0.5f;
        private const int Segments = 48;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<TrainingInstance> trainingInstances = new List<TrainingInstance>();
        private readonly List<EvaluationInstance> evaluationInstances = new List<EvaluationInstance>();
        private readonly Dictionary<Camera, bool> userCameras = new Dictionary<Camera, bool>();
        private readonly Dictionary<float, Mesh> rings = new Dictionary<float, Mesh>();
        private TrainingOrganizer trainingOrganizer;
        private MaterialPropertyBlock beamProps;
        private MaterialPropertyBlock ringProps;
        private Material runtimeMaterial;
        private Mesh beam;
        private float nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfMissing()
        {
            if (FindAnyObjectByType<GoalMarkers>(FindObjectsInactive.Include) == null)
                new GameObject("Goal Markers").AddComponent<GoalMarkers>();
        }

        private void OnEnable()
        {
            beamProps = new MaterialPropertyBlock();
            ringProps = new MaterialPropertyBlock();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
            if (beam != null)
                Destroy(beam);
            foreach (Mesh ring in rings.Values)
                Destroy(ring);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;

            // Instances come and go with runs, so look for them twice a second rather than every frame.
            nextRefresh = Time.unscaledTime + RefreshInterval;
            trainingInstances.Clear();
            trainingInstances.AddRange(FindObjectsByType<TrainingInstance>(FindObjectsInactive.Exclude));
            evaluationInstances.Clear();
            evaluationInstances.AddRange(FindObjectsByType<EvaluationInstance>(FindObjectsInactive.Exclude));
            if (trainingOrganizer == null)
                trainingOrganizer = FindAnyObjectByType<TrainingOrganizer>();
            userCameras.Clear();
        }

        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if ((trainingInstances.Count == 0 && evaluationInstances.Count == 0) || !IsUserCamera(camera))
                return;

            Material drawMaterial = ResolveMaterial();
            if (drawMaterial == null)
                return;

            float glow = pulse ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
            beamProps.SetColor(BaseColorId, WithAlpha(color, beamOpacity * glow));
            ringProps.SetColor(BaseColorId, WithAlpha(color, ringOpacity));

            float trainingRadius = trainingOrganizer != null && trainingOrganizer.Rules != null
                ? trainingOrganizer.Rules.GoalRadius
                : fallbackGoalRadius;

            foreach (TrainingInstance instance in trainingInstances)
            {
                if (instance != null && instance.EpisodeRunning)
                    DrawGoal(camera, drawMaterial, instance.Layout.Goal, trainingRadius);
            }

            foreach (EvaluationInstance instance in evaluationInstances)
            {
                if (instance != null && instance.AttemptRunning)
                    DrawGoal(camera, drawMaterial, instance.Goal, instance.GoalRadius);
            }
        }

        private void DrawGoal(Camera camera, Material drawMaterial, Vector3 goal, float goalRadius)
        {
            float distance = Vector3.Distance(camera.transform.position, goal);
            float radius = Mathf.Max(beamRadius, distance * beamRadiusPerMeter);

            var beamParams = new RenderParams(drawMaterial)
            {
                camera = camera,
                matProps = beamProps,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(goal + Vector3.up * (beamHeight * 0.5f), new Vector3(radius * 2f, beamHeight, radius * 2f))
            };
            Graphics.RenderMesh(beamParams, Beam(), 0, Matrix4x4.TRS(goal, Quaternion.identity, new Vector3(radius, beamHeight, radius)));

            var ringParams = new RenderParams(drawMaterial)
            {
                camera = camera,
                matProps = ringProps,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(goal, new Vector3(goalRadius * 2f, 0.5f, goalRadius * 2f))
            };
            Graphics.RenderMesh(ringParams, Ring(goalRadius), 0, Matrix4x4.Translate(goal + Vector3.up * ringLift));
        }

        // The user's view: any camera with a CinemachineBrain, and the Scene view. The cache is cleared
        // on every refresh in case a brain is added or removed.
        private bool IsUserCamera(Camera camera)
        {
            if (camera.cameraType == CameraType.SceneView)
                return showInSceneView;
            if (camera.cameraType != CameraType.Game)
                return false;

            bool isUser;
            if (!userCameras.TryGetValue(camera, out isUser))
            {
                isUser = camera.GetComponent<CinemachineBrain>() != null;
                userCameras[camera] = isUser;
            }

            return isUser;
        }

        private Material ResolveMaterial()
        {
            if (material != null)
                return material;

            material = Resources.Load<Material>(ResourcesMaterialName);
            if (material != null)
                return material;

            if (runtimeMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    Debug.LogWarning("[GoalMarkers] The URP Unlit shader isn't in this build. Run Tools > RoverSim > Create Goal Marker Material in the editor and rebuild.", this);
                    enabled = false;
                    return null;
                }

                runtimeMaterial = new Material(shader) { name = "Goal Marker (runtime)", hideFlags = HideFlags.HideAndDontSave };
                MakeTransparent(runtimeMaterial);
            }

            return runtimeMaterial;
        }

        // URP Unlit set to Transparent, alpha blended, both faces, no depth writes. Also used by the
        // editor menu that creates the material asset.
        public static void MakeTransparent(Material target)
        {
            target.SetFloat("_Surface", 1f);
            target.SetFloat("_Blend", 0f);
            target.SetFloat("_Cull", (float)CullMode.Off);
            target.SetFloat("_ZWrite", 0f);
            target.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            target.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            target.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            target.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            target.SetOverrideTag("RenderType", "Transparent");
            target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            target.renderQueue = (int)RenderQueue.Transparent;
            target.enableInstancing = true;
        }

        // An open cylinder, radius 1 and height 1, standing on its origin.
        private Mesh Beam()
        {
            if (beam != null)
                return beam;

            var vertices = new Vector3[(Segments + 1) * 2];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * 2f * Mathf.PI / Segments;
                var rim = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = rim;
                vertices[i * 2 + 1] = rim + Vector3.up;
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2;
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = a + 1;
                triangles[t + 2] = a + 2;
                triangles[t + 3] = a + 2;
                triangles[t + 4] = a + 1;
                triangles[t + 5] = a + 3;
            }

            beam = new Mesh { name = "Goal Beam", vertices = vertices, triangles = triangles, hideFlags = HideFlags.HideAndDontSave };
            beam.RecalculateNormals();
            beam.RecalculateBounds();
            return beam;
        }

        // A flat band whose outer edge is the goal radius.
        private Mesh Ring(float outerRadius)
        {
            Mesh ring;
            if (rings.TryGetValue(outerRadius, out ring) && ring != null)
                return ring;

            float innerRadius = Mathf.Max(0f, outerRadius - ringWidth);
            var vertices = new Vector3[(Segments + 1) * 2];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * 2f * Mathf.PI / Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * innerRadius;
                vertices[i * 2 + 1] = direction * outerRadius;
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2;
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = a + 2;
                triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 1;
                triangles[t + 4] = a + 2;
                triangles[t + 5] = a + 3;
            }

            ring = new Mesh { name = "Goal Ring", vertices = vertices, triangles = triangles, hideFlags = HideFlags.HideAndDontSave };
            ring.RecalculateNormals();
            ring.RecalculateBounds();
            rings[outerRadius] = ring;
            return ring;
        }

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }
}
