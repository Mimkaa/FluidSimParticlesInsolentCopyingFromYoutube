using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FilledCircle : MonoBehaviour
{
    public float radius = 1f;    // Radius of the circle
    public int segments = 50;     // Reduced number of segments for simplicity (start with a square)
    private MeshRenderer meshRenderer;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        // Ensure the MeshRenderer has a visible material
        if (meshRenderer.sharedMaterial == null)
        {
            Debug.LogWarning("MeshRenderer does not have a material assigned. Creating a default material.");
            meshRenderer.sharedMaterial = new Material(Shader.Find("Unlit/Color"));
            meshRenderer.sharedMaterial.color = Color.red; // For visibility
        }
        meshRenderer.sharedMaterial = new Material(Shader.Find("Unlit/Color"));
        meshRenderer.sharedMaterial.color = Color.blue; // For visibility

        CreateFilledCircle();
    }

    void CreateFilledCircle()
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 1];  // One center + four corner vertices for a square
        int[] triangles = new int[segments * 3];         // 3 indices per triangle (center + 2 corners)

        // Center of the shape (pivot point)
        vertices[0] = Vector3.zero;

        // Define vertices for the circle (we'll make a square first to simplify)
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            vertices[i] = new Vector3(x, y, 0f);
        }

        // Define triangles (connect the center to each pair of adjacent vertices)
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;               // Center of the shape
            triangles[i * 3 + 2] = i + 1;       // Current vertex
            triangles[i * 3 + 1] = (i + 2 > segments) ? 1 : i + 2;  // Wrap around to first vertex
        }

        // Assign vertices and triangles to the mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Assign the mesh to the MeshFilter component
        GetComponent<MeshFilter>().mesh = mesh;

        Debug.Log("Circle Mesh Created with " + segments + " segments.");
    }

    // This function draws gizmos to help visualize the circle in the Scene view
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            float nextAngle = angleStep * (i + 1) * Mathf.Deg2Rad;

            Vector3 currentPoint = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            Vector3 nextPoint = new Vector3(Mathf.Cos(nextAngle) * radius, Mathf.Sin(nextAngle) * radius, 0);

            Gizmos.DrawLine(transform.position + currentPoint, transform.position + nextPoint);
        }
    }
}
