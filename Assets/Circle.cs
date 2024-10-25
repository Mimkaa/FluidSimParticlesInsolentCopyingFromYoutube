using UnityEngine;


// todo update the drawing thing with the Graphics.DrawMesh(Mesh.CreateSphere(1), circle.positionOffset, Quaternion.identity, material, 0); to draw many circles
     
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FilledCircleWithShader : MonoBehaviour
{
    public float radius = 1f;           // Radius of the circle
    public float gravity = 0f;
    public float dampingFactor = 1f;
    public Vector2 boundsSize = new Vector2(5f, 5f);
    
    private int segments = 50;           // Number of segments for the circle
    private Vector3 velocity;      // Position offset applied in the shader
    private Vector3 positionOffset;      // Position offset applied in the shader
    private Color color = Color.blue;    // Color of the circle

    private MeshRenderer meshRenderer;
    private Material material;

    void Start()
    {
        // Get or create the MeshRenderer and Material components
        meshRenderer = GetComponent<MeshRenderer>();

        // Apply our custom shader to the material
        material = new Material(Shader.Find("Custom/SimpleVertexShader"));
        material.SetColor("_Color", color);
        material.SetFloat("_Radius", radius);
        material.SetVector("_Position", positionOffset);

        // Assign the material to the MeshRenderer
        meshRenderer.material = material;

        // Create the mesh once and assign it to the MeshFilter
        CreateFilledCircle(Vector3.zero);
    }

    void Update()
    {
        // Dynamically update shader properties without recreating the mesh
        velocity += Vector3.down * gravity * Time.deltaTime; 
        positionOffset += velocity * Time.deltaTime;
        ResolveCollisions();
        material.SetFloat("_Radius", radius);
        material.SetVector("_Position", positionOffset);
    }

    void ResolveCollisions()
    {
        Vector2 halfBoundsSize = boundsSize / 2 - Vector2.one * radius;
        
        if (Mathf.Abs(positionOffset.x) > halfBoundsSize.x)
        {
            positionOffset.x = halfBoundsSize.x * Mathf.Sign(positionOffset.x);
            velocity.x *= -1 * dampingFactor;
        }

        if (Mathf.Abs(positionOffset.y) > halfBoundsSize.y)
        {
            positionOffset.y = halfBoundsSize.y * Mathf.Sign(positionOffset.y);
            velocity.y *= -1 * dampingFactor;
        }
    }

    void CreateFilledCircle(Vector3 position)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 1];  // Center vertex + vertices around the circle
        int[] triangles = new int[segments * 3];         // 3 indices per triangle

        // Center of the shape (pivot point)
        vertices[0] = position;

        // Define vertices for the circle based on radius and segments
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2;
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);
            vertices[i] = new Vector3(x, y, 0f);  // Set vertices based on unit circle
        }

        // Define triangles (connect the center to each pair of adjacent vertices)
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;               // Center of the shape
            triangles[i * 3 + 2] = i + 1;       // Current vertex
            triangles[i * 3 + 1] = (i + 2 > segments) ? 1 : i + 2;  // Wrap around to first vertex
        }

        // Assign vertices and triangles to the mesh
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Assign the mesh to the MeshFilter component
        GetComponent<MeshFilter>().mesh = mesh;

        Debug.Log("Circle Mesh Created with " + segments + " segments.");
    }

    // Draw the circle outline in the Scene view for visualization
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

            //Gizmos.DrawLine(transform.position + currentPoint, transform.position + nextPoint);
        }

         
        Gizmos.color = Color.green;

        //Vector3 center = transform.position;

        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boundsSize.x, boundsSize.y, 1));
    }
}
