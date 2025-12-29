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
            // Create a default semi-transparent blue material
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = new Color(0, 0.5f, 1f, 0.3f);
            defaultMat.SetFloat("_Mode", 3); // Transparent mode
            defaultMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            defaultMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            defaultMat.SetInt("_ZWrite", 0);
            defaultMat.DisableKeyword("_ALPHATEST_ON");
            defaultMat.EnableKeyword("_ALPHABLEND_ON");
            defaultMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            defaultMat.renderQueue = 3000;

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
