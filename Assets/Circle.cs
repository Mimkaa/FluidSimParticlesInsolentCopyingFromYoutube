using UnityEngine;
using System.Collections.Generic;

public class FilledCircleWithShader : MonoBehaviour
{
    public float radius = 1f;           // Radius of the circle
    public float gravity = 0f;
    public float dampingFactor = 1f;
    public Vector2 boundsSize = new Vector2(15f, 10f);
    public int numParticles = 4;
    public float particleSpacing = 0.5f;
    public float outterRingSize = 2.0f;
    private float PrevOutterRingSize = 2.0f;
    public float innerRingSize = 1.0f;
    private float PrevinnerRingSize = 1.0f;
    public float ringScale = 1.0f;

    private float dens = 0.0f;
    

    private int segments = 50;           // Number of segments for the circle
    private Vector3[] velocities;        // Velocities of particles
    private Vector3[] positionOffsets;   // Positions of particles
    private float angleIncrement = 30f;

    private Mesh circleMesh;
    private Mesh ringMesh;
    private Mesh cubeMesh;

    private Material circleMaterial;
    private Material ringMaterial;
    private Material cubeMaterial;

    private Vector3 ringPosition = new Vector3(0.5f, 0.5f, 0.0f);
    private Vector3 prevRingPosition = new Vector3(0.5f, 0.5f, 0.0f);

    // Dictionary to hold instances grouped by mesh and material
    private Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>> meshMaterialInstances;

    private Material underRingMaterial;  // Material for circles under the ring
    private List<Vector3> positionsInRing;
    private List<float> heights;
    private bool refillPosintInRing = false;

    // Class to hold instance data
    class InstanceData
    {
        public Matrix4x4 matrix;
    }

    void Start()
    {
        // Initialize the dictionary
        meshMaterialInstances = new Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>>();
        positionsInRing = new List<Vector3>();
        heights = new List<float>();

        // Create the circle and ring meshes
        circleMesh = CreateFilledCircle();
        ringMesh = CreateRing(innerRingSize, outterRingSize);
        cubeMesh = CreateCube(0.44f, 0.44f, 1.0f);

        // Create materials for the circle and ring
        circleMaterial = new Material(Shader.Find("Custom/SimpleVertexShader"));
        circleMaterial.SetColor("_Color", Color.white); // Set desired color
        circleMaterial.enableInstancing = true;
        circleMaterial.renderQueue = 2000;

        ringMaterial = new Material(Shader.Find("Custom/SimpleVertexShader"));
        ringMaterial.SetColor("_Color", Color.red); // Set desired color
        ringMaterial.enableInstancing = true;
        ringMaterial.renderQueue = 2001;

        // Initialize the under-ring material with a different color
        underRingMaterial = new Material(Shader.Find("Custom/SimpleVertexShader"));
        underRingMaterial.SetColor("_Color", Color.blue);  // Set to desired color
        underRingMaterial.enableInstancing = true;

        cubeMaterial = new Material(Shader.Find("Custom/SimpleLitShader"));
        cubeMaterial.SetColor("_Color", Color.yellow); // Set desired color
        cubeMaterial.enableInstancing = true;
        cubeMaterial.renderQueue = 2002;

        ReshapeParticles();
    }

    void ReshapeParticles()
    {
        // Create particle arrays
        positionOffsets = new Vector3[numParticles];
        velocities = new Vector3[numParticles];

        // Place particles in a grid formation
        int particlesPerRow = Mathf.CeilToInt(Mathf.Sqrt(numParticles));
        int particlesPerCol = Mathf.CeilToInt((float)numParticles / particlesPerRow);
        float spacing = radius * 2 + particleSpacing;

        for (int i = 0; i < numParticles; i++)
        {
            float x = (i % particlesPerRow - (particlesPerRow - 1) / 2f) * spacing;
            float y = (i / particlesPerRow - (particlesPerCol - 1) / 2f) * spacing;
            positionOffsets[i] = new Vector2(x, y);
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPosition = Input.mousePosition;
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f; // 2D plane
        return mouseWorldPosition;
    }

    
    void HandleRingSizeChange()
    {
        bool sizeChanged = false;
        bool moved = false;
        // Check if innerRingSize has changed
        if (innerRingSize != PrevinnerRingSize)
        {
            sizeChanged = true;
            PrevinnerRingSize = innerRingSize;
        }

        // Check if outterRingSize has changed
        if (outterRingSize != PrevOutterRingSize)
        {
            sizeChanged = true;
            PrevOutterRingSize = outterRingSize;
        }
        if (prevRingPosition != ringPosition)
        {
            moved = true;
            prevRingPosition = ringPosition;
        }

        // If any size has changed, update the ring mesh
        if (sizeChanged)
        {
            ringMesh = CreateRing(innerRingSize, outterRingSize);
        }
        if(moved || sizeChanged)
        {
            positionsInRing.Clear();
            heights.Clear();
            refillPosintInRing = true;
            
        }
    }

    Vector3 CalculateMeanPosition()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < positionOffsets.Length; i++)
        {
            sum += positionOffsets[i];
        }
        return sum / positionOffsets.Length;
    }

    static float SmoothingKernel(float radius, float dst)
    {
        float volume = Mathf.PI * Mathf.Pow(radius, 8) / 4;
        float value = Mathf.Max(0, radius * radius - dst * dst);
        return value * value * value/volume;
    }

    float CalculateDensity(Vector3 samplePoint, List<Vector3> positions)
    {
        float density = 0;
        const float mass = 1;

        // Loop over all particle positions
        // TODO: optimize to only look at particles inside the smoothing radius
        foreach (Vector3 position in positions)
        {
            float dst = (position - samplePoint).magnitude;
            float influence = SmoothingKernel(innerRingSize, dst);
            density += mass * influence;
            heights.Add(mass * influence);
        }

        return density;
    }

    void Update()
    {
        HandleRingSizeChange();
        if (Input.GetMouseButtonDown(0)) // Left mouse button
        {
            ringPosition = GetMouseWorldPosition();
            Debug.Log("Ring Position Updated: " + ringPosition);
        }

        // Clear previous instance data
        meshMaterialInstances.Clear();
        Quaternion rotation = Quaternion.Euler(angleIncrement, angleIncrement, 0f);
        Vector3 meanPosition = CalculateMeanPosition();
        

        for (int i = 0; i < positionOffsets.Length; i++)
        {
            // Update velocities and positions
            velocities[i] += Vector3.down * gravity * Time.deltaTime;
            positionOffsets[i] += velocities[i] * Time.deltaTime;

            // Rotate each particle around the mean position
            Vector3 offsetFromMean = positionOffsets[i] - meanPosition;
            Vector3 rotatedOffset = rotation * offsetFromMean;
            Vector3 newPosition = meanPosition + rotatedOffset;

            ResolveCollisions(ref positionOffsets[i], ref velocities[i]);

            // Create transformation matrix for the circle particle
            
            Matrix4x4 circleMatrix = Matrix4x4.TRS(newPosition, Quaternion.identity, Vector3.one * radius);

            float distanceToRingCenter = Vector3.Distance(positionOffsets[i], ringPosition);
            bool isUnderRing = distanceToRingCenter <= innerRingSize;
            Material particleMaterial = isUnderRing ? underRingMaterial : circleMaterial;
            if(isUnderRing && refillPosintInRing)
            {
                positionsInRing.Add(positionOffsets[i]);
            }
            

            // Add instance for the circle mesh
            AddInstance(circleMesh, particleMaterial, circleMatrix);

        }
        refillPosintInRing = false;
        dens = CalculateDensity(ringPosition, positionsInRing);

        // Create transformation matrix for the ring particle
        Vector3 offsetFromMeanRing = ringPosition - meanPosition;
        Vector3 rotatedOffsetRing = rotation * offsetFromMeanRing;
        Vector3 newPositionRing = meanPosition + rotatedOffsetRing;

        Matrix4x4 ringMatrix = Matrix4x4.TRS(newPositionRing, rotation, Vector3.one * (ringScale));
        //angleIncrement += 0.1f;

        // Add instance for the ring mesh
        AddInstance(ringMesh, ringMaterial, ringMatrix);

        //Matrix4x4 cubeMatrix = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one );

        //AddInstance(cubeMesh, cubeMaterial, cubeMatrix);

        // calculate density 
        
        //Debug.Log("density: " + dens);

        int iter = 0;
        foreach (Vector3 pos in positionsInRing)
        {
            float height = heights[iter];
            Vector3 cubePosition = new Vector3(pos.x, pos.y, pos.z - (height*5.0f)/2); // Bottom side (y = 0)
            Vector3 offsetFromMeanCube = cubePosition - meanPosition;
            Vector3 rotatedOffsetCube = rotation * offsetFromMeanCube;
            Vector3 newPositionCube = meanPosition + rotatedOffsetCube;

            Vector3 scale = new Vector3(1.0f, 1.0f, height * 5.0f);

            Matrix4x4 cubeMatrix = Matrix4x4.TRS(newPositionCube, rotation, scale); // Scale to 1 for each cube

            AddInstance(cubeMesh, cubeMaterial, cubeMatrix);
            iter++;
        }


        // Draw all instances
        foreach (var meshEntry in meshMaterialInstances)
        {
            Mesh mesh = meshEntry.Key;
            var materialDict = meshEntry.Value;

            foreach (var materialEntry in materialDict)
            {
                Material material = materialEntry.Key;
                List<Matrix4x4> matrices = materialEntry.Value;

                DrawInstances(mesh, material, matrices);
            }
        }

        
    }

    void AddInstance(Mesh mesh, Material material, Matrix4x4 matrix)
    {
        if (!meshMaterialInstances.ContainsKey(mesh))
        {
            meshMaterialInstances[mesh] = new Dictionary<Material, List<Matrix4x4>>();
        }

        var materialInstances = meshMaterialInstances[mesh];

        if (!materialInstances.ContainsKey(material))
        {
            materialInstances[material] = new List<Matrix4x4>();
        }

        materialInstances[material].Add(matrix);
    }

    void DrawInstances(Mesh mesh, Material material, List<Matrix4x4> matrices)
    {
        const int maxInstancesPerBatch = 1023; // Unity's limit per draw call

        for (int i = 0; i < matrices.Count; i += maxInstancesPerBatch)
        {
            int batchSize = Mathf.Min(maxInstancesPerBatch, matrices.Count - i);
            Matrix4x4[] batchMatrices = new Matrix4x4[batchSize];
            matrices.CopyTo(i, batchMatrices, 0, batchSize);

            // Draw without MaterialPropertyBlock
            Graphics.DrawMeshInstanced(mesh, 0, material, batchMatrices, batchSize);
        }
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

    Mesh CreateRing(float innerRadius, float outerRadius)
    {
        Mesh mesh = new Mesh();

        int segmentCount = segments;

        int vertexCount = segmentCount * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[segmentCount * 6]; // 2 triangles per segment

        float angleIncrement = 2 * Mathf.PI / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * angleIncrement;

            float xOuter = Mathf.Cos(angle) * outerRadius;
            float yOuter = Mathf.Sin(angle) * outerRadius;
            float xInner = Mathf.Cos(angle) * innerRadius;
            float yInner = Mathf.Sin(angle) * innerRadius;

            vertices[i * 2] = new Vector3(xOuter, yOuter, 0f);      // Outer vertex
            vertices[i * 2 + 1] = new Vector3(xInner, yInner, 0f);  // Inner vertex
        }

        for (int i = 0; i < segmentCount; i++)
        {
            int currentOuter = i * 2;
            int currentInner = i * 2 + 1;
            int nextOuter = (i * 2 + 2) % vertexCount;
            int nextInner = (i * 2 + 3) % vertexCount;

            // First triangle
            triangles[i * 6] = currentOuter;
            triangles[i * 6 + 2] = nextOuter;
            triangles[i * 6 + 1] = currentInner;

            // Second triangle
            triangles[i * 6 + 3] = nextOuter;
            triangles[i * 6 + 5] = nextInner;
            triangles[i * 6 + 4] = currentInner;
        }

        // Assign vertices and triangles to the mesh
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Debug.Log("Ring Mesh Created with " + segments + " segments.");

        return mesh;
    }

    Mesh CreateCube(float scaleX, float scaleY, float scaleZ)
    {
        Mesh mesh = new Mesh();

        // Define the vertices for the cube with scaling along each axis
        Vector3[] vertices = {
            // Front face
            new Vector3(-scaleX, -scaleY, scaleZ),  // Bottom left front
            new Vector3(scaleX, -scaleY, scaleZ),   // Bottom right front
            new Vector3(scaleX, scaleY, scaleZ),    // Top right front
            new Vector3(-scaleX, scaleY, scaleZ),   // Top left front

            // Back face
            new Vector3(-scaleX, -scaleY, -scaleZ), // Bottom left back
            new Vector3(scaleX, -scaleY, -scaleZ),  // Bottom right back
            new Vector3(scaleX, scaleY, -scaleZ),   // Top right back
            new Vector3(-scaleX, scaleY, -scaleZ)   // Top left back
        };

        // Define the triangles for the cube (two triangles per face, 12 triangles total)
        int[] triangles = {
            // Front face
            0, 1, 2,  0, 2, 3,
            // Right face
            1, 6, 2,  1, 5, 6,
            // Back face
            5, 7, 6,  5, 4, 7,
            // Left face
            4, 3, 7,  4, 0, 3,
            // Top face
            3, 6, 7,  3, 2, 6,
            // Bottom face
            4, 1, 0,  4, 5, 1
        };

        

        // Assign the vertices, triangles, and normals to the mesh
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        // Recalculate normals automatically
        mesh.RecalculateNormals(); // This generates outward-facing normals for shading

        // Calculate bounds for proper culling
        mesh.RecalculateBounds();

        Debug.Log("Cube Mesh Created with scales: " + scaleX + ", " + scaleY + ", " + scaleZ);

        return mesh;
    }




    // Optional: Draw the bounds in the Scene view for visualization
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boundsSize.x, boundsSize.y, 1));
    }

    void OnGUI()
    {
        // Set font size and color (optional)
        GUIStyle style = new GUIStyle();
        style.fontSize = 24; // Set font size
        style.normal.textColor = Color.white; // Set text color

        // Display the number at the top-left corner of the screen
        GUI.Label(new Rect(10, 10, 100, 30), dens.ToString(), style);
    }
}
