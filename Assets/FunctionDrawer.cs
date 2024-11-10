using UnityEngine;
using System.Collections.Generic;

public class FunctionDrawer : MonoBehaviour
{
    public float radius = 1f;           // Radius of the circle
    public float gravity = 0f;
    public float dampingFactor = 1f;
    public Vector2 boundsSize = new Vector2(15f, 10f);
    public float mass = 1.0f;
 
    

    private int segments = 50;           
    private Vector3[] randomPositions;
    private float[] particleProperties;
    private Color[] fieldColors;


    private Mesh circleMesh;
    private Mesh planeMesh;

    private Material circleMaterial;
    private Material planeMaterial;
   

    private Vector3 ringPosition = new Vector3(0.5f, 0.5f, 0.0f);
    private Vector3 prevRingPosition = new Vector3(0.5f, 0.5f, 0.0f);

    // Dictionary to hold instances grouped by mesh and material
    private Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>> meshMaterialInstances;


    float width;
    float height;

    int numParticles = 150;

    private int numColumns = 50;
    private float rectWidth; 
    private float rectHeight;
    private int numRows;

    
    // Class to hold instance data
    class InstanceData
    {
        public Matrix4x4 matrix;
    }

    void Start()
    {
        
        // Initialize the dictionary
        meshMaterialInstances = new Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>>();
       

        // Create the circle and ring meshes
        circleMesh = CreateFilledCircle();
    
        

        Camera cam = Camera.main;
        width = cam.orthographicSize * 2.0f * cam.aspect;
        height = cam.orthographicSize * 2.0f;

        rectWidth = width / numColumns;
        rectHeight = rectWidth;
        numRows = Mathf.CeilToInt(height / rectHeight);

        planeMesh = CreatePlane(width, height);
       

        // Create materials for the circle and ring
        circleMaterial = new Material(Shader.Find("Custom/SimpleVertexShader"));
        circleMaterial.SetColor("_Color", Color.red); // Set desired color
        circleMaterial.enableInstancing = true;
        circleMaterial.renderQueue = 2000;

       
        
        planeMaterial = new Material(Shader.Find("Custom/FunctionShader"));
        //planeMaterial.SetColor("_Color", Color.yellow); // Set desired color
        planeMaterial.enableInstancing = true;
        planeMaterial.renderQueue = 1999;

        randomPositions = new Vector3[numParticles];
        particleProperties = new float[numParticles];
        fieldColors = new Color[numRows*numColumns];

        PopulateRandomPositions();

        // fill random colors 
        for (int i = 0; i < fieldColors.Length; i++)
        {
            Color randomColor = new Color(
                    Random.Range(0f, 1f), // Red
                    Random.Range(0f, 1f), // Green
                    Random.Range(0f, 1f)  // Blue
                );
            fieldColors[i] = randomColor;
        }


     
    }

    float ExampleFunction(Vector2 pos)
    {
        return Mathf.Cos(pos.y - 3 + Mathf.Sin(pos.x));
    }

    Vector2 WorldPositionToUV(Vector3 worldPosition)
    {
        float u = (worldPosition.x + width / 2f) / width;
        float v = (worldPosition.y + height / 2f) / height;
        return new Vector2(u, v);
    }

    Vector2 UVToPos(Vector2 uv)
    {
        Vector2 pos = 5.0f * (uv * 2.0f - Vector2.one);
        return pos;
    }

   void PopulateRandomPositions()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main camera not found.");
            return;
        }

        for (int i = 0; i < randomPositions.Length; i++)
        {
            // Generate a random position within the plane's bounds
            float randomX = Random.Range(-width / 2f, width / 2f);
            float randomY = Random.Range(-height / 2f, height / 2f);

            Vector3 worldPosition = new Vector3(randomX, randomY, 0.0f);
            randomPositions[i] = worldPosition;

            // Map world position to UV coordinates
            Vector2 uv = WorldPositionToUV(worldPosition);

            // Map UV to shader's 'pos' coordinate
            Vector2 pos = UVToPos(uv);

            // Calculate the function value at this position
            float functionValue = ExampleFunction(pos);

            particleProperties[i] = functionValue;

            
        }
    }

    float CalculateProperty(Vector3 samplePoint, float radius)
    {
        float property = 0;

        for (int i = 0; i < numParticles; i++)
        {
            float dst = (randomPositions[i] - samplePoint).magnitude;
            float influence = SmoothingKernel(dst, radius);
            float density = CalculateDensity(samplePoint, randomPositions, radius);
            property += particleProperties[i] * influence * mass/density;
        }

        return property;
    }

    
    static float SmoothingKernel(float radius, float dst)
    {
        float volume = Mathf.PI * Mathf.Pow(radius, 8) / 4;
        float value = Mathf.Max(0, radius * radius - dst * dst);
        return value * value * value/volume;
    }

    float CalculateDensity(Vector3 samplePoint, Vector3[] positions, float radius)
    {
        float density = 0;
        const float mass = 1;

        // Loop over all particle positions
        // TODO: optimize to only look at particles inside the smoothing radius
        foreach (Vector3 position in positions)
        {
            float dst = (position - samplePoint).magnitude;
            float influence = SmoothingKernel(radius, dst);
            density += mass * influence;
            
        }

        return density;
    }

    void Update()
    {
        CalculatePropertiesForGrid();
        
        DrawFunctionPLaneAndRandowPoints();
       
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
        //foreach(float v in particleProperties)
        //{
        //    Debug.Log(v);
        //}
        
    }

    void CalculatePropertiesForGrid()
    {
        float startX = -width / 2 + rectWidth / 2;
        float startY = -height / 2 + rectHeight / 2;

        int index = 0;
        for (int row = 0; row < numRows; row++)
        {
            for (int col = 0; col < numColumns; col++)
            {
                // Calculate the center position of the cell
                float x = startX + col * rectWidth;
                float y = startY + row * rectHeight;
                Vector3 samplePoint = new Vector3(x, y, 0f);

                // Call CalculateProperty for this sample point
                float property = CalculateProperty(samplePoint, radius);

                // Map the property value to a color
                Color color = new Color(property,property,property, 1.0f);

                // Assign the color to the fieldColors array
                fieldColors[index] = color;

                index++;
            }
        }

        
    }

    void DrawFunctionPLaneAndRandowPoints()
    {
        Matrix4x4 planeMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
        //AddInstance(planeMesh, planeMaterial, planeMatrix);
        Mesh midPlaneMesh = CreatePlane(rectWidth, rectHeight);
        for (int x = -numColumns/2 + 1; x < numColumns/2; x++)
        {
            for (int y = -numRows/2 + 1; y < numRows/2; y++)
            {
                Matrix4x4 midPlaneMatrix = Matrix4x4.TRS(new Vector3(x*rectWidth, y*rectHeight, 0.0f), Quaternion.identity, Vector3.one);
                

                Material midPlaneMeshesMat = new Material(Shader.Find("Custom/SimpleVertexShader"));
                midPlaneMeshesMat.SetColor("_Color", Color.red); 
                midPlaneMeshesMat.enableInstancing = true;
                midPlaneMeshesMat.renderQueue = 1999;

                
                midPlaneMeshesMat.SetColor("_Color", fieldColors[(y+numRows/2) * numColumns + (x+numColumns/2)]); 

                AddInstance(midPlaneMesh, midPlaneMeshesMat, midPlaneMatrix);
            }
        }

        for (int i = 0; i<randomPositions.Length; i++)
        {
            
            Matrix4x4 circleMatrix = Matrix4x4.TRS(randomPositions[i], Quaternion.identity, Vector3.one * 0.1f);
            circleMaterial.SetColor("_Color", Color.red);
            AddInstance(circleMesh, circleMaterial, circleMatrix);
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

    
    Mesh CreatePlane(float width, float height)
    {
        Mesh mesh = new Mesh();

        // Define vertices for the corners of the plane based on width and height
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(-width / 2, -height / 2, 0f); // Bottom-left
        vertices[1] = new Vector3(width / 2, -height / 2, 0f);  // Bottom-right
        vertices[2] = new Vector3(-width / 2, height / 2, 0f);  // Top-left
        vertices[3] = new Vector3(width / 2, height / 2, 0f);   // Top-right

        // Define standard UV coordinates for each vertex
        Vector2[] uv = new Vector2[4];
        uv[0] = new Vector2(0, 0); // Bottom-left
        uv[1] = new Vector2(1, 0); // Bottom-right
        uv[2] = new Vector2(0, 1); // Top-left
        uv[3] = new Vector2(1, 1); // Top-right

        // Define triangles with standard winding
        int[] triangles = new int[6];
        triangles[0] = 0; // First triangle
        triangles[1] = 2;
        triangles[2] = 1;
        triangles[3] = 1; // Second triangle
        triangles[4] = 2;
        triangles[5] = 3;

        // Assign vertices, UVs, and triangles to the mesh
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        

        return mesh;
    }




    // Optional: Draw the bounds in the Scene view for visualization
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boundsSize.x, boundsSize.y, 1));
    }

    
}
