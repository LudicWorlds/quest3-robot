using UnityEngine;
using UnityEngine.AI;

public class NavMeshVisualizer : MonoBehaviour
{
    [SerializeField] private Material navMeshMaterial;
    [SerializeField] private bool showNavMesh = true;
    [SerializeField] private float yOffset = 0.01f; // Slight offset to prevent z-fighting

    private GameObject navMeshObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void Start()
    {
        CreateNavMeshVisualization();
    }

    void Update()
    {
        // Toggle visualization with a button press
        if (OVRInput.GetDown(OVRInput.RawButton.X))
        {
            ToggleVisualization();
        }
    }

    public void CreateNavMeshVisualization()
    {
        // Get the NavMesh triangulation
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices.Length == 0)
        {
            Debug.LogWarning("No NavMesh found to visualize!");
            return;
        }

        // Create GameObject for visualization
        if (navMeshObject == null)
        {
            navMeshObject = new GameObject("NavMesh Visualization");
            navMeshObject.transform.SetParent(transform);
            meshFilter = navMeshObject.AddComponent<MeshFilter>();
            meshRenderer = navMeshObject.AddComponent<MeshRenderer>();
        }

        // Create mesh from triangulation
        Mesh mesh = new Mesh();

        // Offset vertices slightly upward to prevent z-fighting
        Vector3[] offsetVertices = new Vector3[triangulation.vertices.Length];
        for (int i = 0; i < triangulation.vertices.Length; i++)
        {
            offsetVertices[i] = triangulation.vertices[i] + Vector3.up * yOffset;
        }

        mesh.vertices = offsetVertices;
        mesh.triangles = triangulation.indices;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;

        // Apply material
        if (navMeshMaterial != null)
        {
            meshRenderer.material = navMeshMaterial;
        }
        else
        {
            // Create a default semi-transparent blue material.
            // URP/Lit is tried first so this works after the URP migration; the
            // Standard branch keeps it working on the Built-in pipeline.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            bool isUrp = shader != null;

            if (!isUrp)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("-> NavMeshVisualizer::CreateNavMeshVisualization() - no usable shader for the active render pipeline! :(");
                return;
            }

            Material defaultMat = new Material(shader);
            defaultMat.color = new Color(0, 0.5f, 1f, 0.3f); // [MainColor] -> _BaseColor on URP/Lit

            if (isUrp)
            {
                defaultMat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
                defaultMat.SetFloat("_Blend", 0f);   // 0 = Alpha
                defaultMat.SetFloat("_AlphaClip", 0f);
                defaultMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                defaultMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                defaultMat.SetFloat("_ZWrite", 0f);
                defaultMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                defaultMat.DisableKeyword("_ALPHATEST_ON");
                defaultMat.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                defaultMat.SetFloat("_Mode", 3f); // Transparent mode
                defaultMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                defaultMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                defaultMat.SetFloat("_ZWrite", 0f);
                defaultMat.DisableKeyword("_ALPHATEST_ON");
                defaultMat.EnableKeyword("_ALPHABLEND_ON");
                defaultMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            defaultMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            meshRenderer.material = defaultMat;
        }

        navMeshObject.SetActive(showNavMesh);
    }

    public void ToggleVisualization()
    {
        showNavMesh = !showNavMesh;
        if (navMeshObject != null)
        {
            navMeshObject.SetActive(showNavMesh);
        }
    }

    public void RefreshVisualization()
    {
        CreateNavMeshVisualization();
    }
}
