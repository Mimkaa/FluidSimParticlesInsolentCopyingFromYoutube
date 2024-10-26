using UnityEngine;


// todo update the drawing thing with the Graphics.DrawMesh(Mesh.CreateSphere(1), circle.positionOffset, Quaternion.identity, material, 0); to draw many circles
     
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FilledCircleWithShader : MonoBehaviour
{
    public float radius = 1f;           // Radius of the circle
    public float gravity = 0f;
    public float dampingFactor = 1f;
    public Vector2 boundsSize = new Vector2(10f, 10f);
    public int numParticles = 4;
    public float particleSpacing = 0.5f;
    
    private int segments = 50;           // Number of segments for the circle
    private Vector3[] velocities;      // Position offset applied in the shader
    private Vector3[] positionOffsets;      // Position offset applied in the shader
    private Color color = Color.blue;    // Color of the circle

    private MeshRenderer meshRenderer;
    private Material material;
    private MaterialPropertyBlock propertyBlock;
    private Mesh circleMesh;

    void Start()
    {
        // Get or create the MeshRenderer and Material components
        meshRenderer = GetComponent<MeshRenderer>();

        // Apply our custom shader to the material
        material = new Material(Shader.Find("Custom/SimpleVertexShader"));
        material.SetColor("_Color", color);
        material.SetFloat("_Radius", radius);
        material.SetVector("_Position", Vector3.zero);

        // Assign the material to the MeshRenderer
        meshRenderer.material = material;

        // Create the mesh once and assign it to the MeshFilter
        circleMesh = CreateFilledCircle();

        propertyBlock = new MaterialPropertyBlock();
        ReshapeParticles();
    }

    void ReshapeParticles()
    {
        // Create particle arrays
        positionOffsets = new Vector3[numParticles];
        velocities = new Vector3[numParticles];

        // Place particles in a grid formation
        int particlesPerRow = (int)Mathf.Sqrt(numParticles);
        int particlesPerCol = (numParticles - 1) / particlesPerRow + 1;
        float spacing = radius * 2 + particleSpacing;

        for (int i = 0; i < numParticles; i++)
        {
            float x = (i % particlesPerRow - particlesPerRow / 2f + 0.5f) * spacing;
            float y = (i / particlesPerRow - particlesPerCol / 2f + 0.5f) * spacing;
            positionOffsets[i] = new Vector2(x, y);
        }
    }

    void Update()
    {
        
        for (int i = 0; i < positionOffsets.Length; i++)
        {
            velocities[i] += Vector3.down * gravity * Time.deltaTime;
            positionOffsets[i] += velocities[i] * Time.deltaTime;
            propertyBlock.SetFloat("_Radius", radius);
            propertyBlock.SetVector("_Position", positionOffsets[i]);
            ResolveCollisions(ref positionOffsets[i], ref velocities[i]);
            DrawCircle();
        }
    }

    void DrawCircle()
    {
        Graphics.DrawMesh(circleMesh, Matrix4x4.identity, material, 0, null, 0, propertyBlock);
    }

    void ResolveCollisions(ref Vector3 position, ref Vector3 velocity)
    {
        Vector2 halfBoundsSize = boundsSize / 2 - Vector2.one * radius;

        if (Mathf.Abs(position.x) > halfBoundsSize.x)
        {
            position.x = halfBoundsSize.x * Mathf.Sign(position.x);
            velocity.x *= -1 * dampingFactor;
        }

        if (Mathf.Abs(position.y) > halfBoundsSize.y)
        {
            position.y = halfBoundsSize.y * Mathf.Sign(position.y);
            velocity.y *= -1 * dampingFactor;
        }
    }

    

    Mesh CreateFilledCircle()
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 1];  // Center vertex + vertices around the circle
        int[] triangles = new int[segments * 3];         // 3 indices per triangle

        // Center of the shape (pivot point)
        vertices[0] = Vector3.zero;

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

        Debug.Log("Circle Mesh Created with " + segments + " segments.");

        return mesh;
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
