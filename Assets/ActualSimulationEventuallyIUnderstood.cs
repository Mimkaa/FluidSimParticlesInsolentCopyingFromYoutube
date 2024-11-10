using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Linq;

public struct Entry : IComparable<Entry>
{

    public uint cellKey;
    public uint particleIndex;

    

    // New constructor without particleIndex
    public Entry(uint index, uint cellKey)
    {
        this.particleIndex = index;
        this.cellKey = cellKey;
        
    }

    // Method to set particleIndex after initialization
    public void SetParticleIndex(uint particleIndex)
    {
        this.particleIndex = particleIndex;
    }

    // Comparison method to sort entries by cellKey
    public int CompareTo(Entry other)
    {
        return cellKey.CompareTo(other.cellKey);
    }
}

public class ActualSimulationEventuallyIUnderstood : MonoBehaviour
{
    private static int segments = 10;
    public int numParticles;
    public float smoothingRadius;
    public float radius;
    public float gravity;
    public float mass;
    public float dampingFactor;
    public Vector2 boundsSize;
    public float viscosityStrength;

    public float targetDensity;
    public float pressureMultiplier;

    private float particleSpacing = 0.1f;

    private float[] densities;
    private Vector2[] velocities;
    private Vector2[] positions;
    private Vector2[] predictedPositions;
    private Vector2[] randomDirections;
    private Entry[] spatialLookup;
    private uint[] startIndices;

    private Vector2[] points;
    private float cellRaduis;

    private Mesh circleMesh;
    private Material circleMaterial;

    float width;
    float height;

    private Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>> meshMaterialInstances;

    // Start is called before the first frame update
    void Start()
    {
        meshMaterialInstances = new Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>>();

        Camera cam = Camera.main;
        width = cam.orthographicSize * 2.0f * cam.aspect;
        height = cam.orthographicSize * 2.0f;

        //numParticles = 150;
        //smoothingRadius = 2.0f;
        //radius = 0.1f;
        //gravity = 0.0f;
        //mass = 1.0f;

        //targetDensity = 3.0f;
        //pressureMultiplier = 0.5f;
        //dampingFactor = 1.0f;
        //boundsSize = new Vector2(15f, 10f);

        circleMaterial = new Material(Shader.Find("Custom/SimpleVertexShader"));
        circleMaterial.SetColor("_Color", Color.red);
        circleMaterial.enableInstancing = true;
        circleMaterial.renderQueue = 2000;

        circleMesh = CreateFilledCircle();

        densities = new float[numParticles];
        predictedPositions = new Vector2[numParticles];
        velocities = new Vector2[numParticles];
        positions = new Vector2[numParticles];
        randomDirections = new Vector2[numParticles];
        spatialLookup = new Entry[numParticles];
        startIndices = new uint[numParticles];

        points = positions;

        int index = 0;
        foreach(Entry e in spatialLookup)
        {
            e.SetParticleIndex((uint)index);
            index++;
        }

        for (int i = 0; i < numParticles; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            randomDirections[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        
        //PopulateRandomPositions();
        ReshapeParticles();
        UpdateSpatialLookup(positions, smoothingRadius);

        for (int i = 0; i < numParticles; i++)
        {
            predictedPositions[i] = positions[i];
        }

        Debug.Log("spacial lookups: " + string.Join(", ", spatialLookup));
        Debug.Log("spacial lookups: " + string.Join(", ", startIndices));
        Debug.Log("kavabanga");
    }

     void ReshapeParticles()
    {
        // Create particle arrays
        positions = new Vector2[numParticles];
        velocities = new Vector2[numParticles];

        // Place particles in a grid formation
        int particlesPerRow = Mathf.CeilToInt(Mathf.Sqrt(numParticles));
        int particlesPerCol = Mathf.CeilToInt((float)numParticles / particlesPerRow);
        float spacing = radius * 2 + particleSpacing;

        for (int i = 0; i < numParticles; i++)
        {
            float x = (i % particlesPerRow - (particlesPerRow - 1) / 2f) * spacing;
            float y = (i / particlesPerRow - (particlesPerCol - 1) / 2f) * spacing;
            positions[i] = new Vector2(x, y);
        }
    }

    void PopulateRandomPositions()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main camera not found.");
            return;
        }

        for (int i = 0; i < positions.Length; i++)
        {
            float offset = 2.0f;
            float randomX = UnityEngine.Random.Range(-width / 2f + offset, width / 2f - offset);
            float randomY = UnityEngine.Random.Range(-height / 2f + offset, height / 2f - offset);
            positions[i] = new Vector2(randomX, randomY);
        }
    }

   static float SmoothingKernel(float dst, float radius)
    {
        if (dst >= radius) return 0;

        float volume = (Mathf.PI * Mathf.Pow(radius, 4)) / 6;
        return (radius - dst) * (radius - dst) / volume;
    }

    static float SmoothingKernelDerivative(float dst, float radius)
    {
        if (dst >= radius) return 0;

        float scale = 12 / (Mathf.Pow(radius, 4) * Mathf.PI);
        return (dst - radius) * scale;
    }

    static float ViscositySmoothingKernel(float dst, float radius)
    {
        float value = Mathf.Max(0, radius * radius - dst * dst);
        return value * value * value;
    }

    Vector2 GetRandomDir(int particleIndex)
    {
        return randomDirections[particleIndex % numParticles];
    }

    float CalculateSharedPressure(float densityA, float densityB)
    {
        float pressureA = ConvertDensityToPressure(densityA);
        float pressureB = ConvertDensityToPressure(densityB);
        return (pressureA + pressureB) / 2;
    }

    Vector2 CalculateViscosityForce(int particleIndex)
    {
        Vector2 viscosityForce = Vector2.zero;
        Vector2 position = positions[particleIndex];

        foreach (int otherIndex in GetNeighbours(position))
        {
            float dst = (position - positions[otherIndex]).magnitude;
            float influence = ViscositySmoothingKernel(dst, smoothingRadius);
            viscosityForce += (velocities[otherIndex] - velocities[particleIndex]) * influence;
        }

        return viscosityForce * viscosityStrength;
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 pressureForce = Vector2.zero;
        Vector2 particlePosition = predictedPositions[particleIndex];
        float density = densities[particleIndex];  // Assume density for this particle is already calculated

        // Use ForeachPointWithinRadius to only loop over nearby particles
        ForeachPointWithinRadius(particlePosition, smoothingRadius, otherParticleIndex =>
        {
            if (particleIndex == otherParticleIndex) return;  // Skip self

            Vector2 offset = predictedPositions[otherParticleIndex] - particlePosition;
            float dst = offset.magnitude;

            // Determine direction; if distance is zero, use a random direction
            Vector2 dir = dst == 0 ? GetRandomDir(particleIndex) : offset / dst;

            // Calculate slope and shared pressure
            float slope = SmoothingKernelDerivative(dst, smoothingRadius);
            float otherDensity = densities[otherParticleIndex];
            float sharedPressure = CalculateSharedPressure(otherDensity, density);

            // Accumulate the pressure force
            pressureForce -= (sharedPressure * dir * slope * mass) / density;
        });

        return pressureForce;
    }

    float CalculateDensity(int particleIndex)
    {
        float density = 0;
        Vector2 particlePosition = predictedPositions[particleIndex];

        // Use ForeachPointWithinRadius to only loop over nearby particles
        ForeachPointWithinRadius(particlePosition, smoothingRadius, otherParticleIndex =>
        {
            Vector2 offset = predictedPositions[otherParticleIndex] - particlePosition;
            float dst = offset.magnitude;

            // Calculate influence based on smoothing kernel
            float influence = SmoothingKernel(dst, smoothingRadius);
            density += mass * influence;
        });

        return density;
    }

    float ConvertDensityToPressure(float density)
    {
        float densityError = density - targetDensity;
        float pressure = -densityError * pressureMultiplier;
        return pressure;
    }

    Vector2 InteractionForce(Vector2 inputPos, float radius, float strength, int particleIndex)
    {
        Vector2 interactionForce = Vector2.zero;
        Vector2 offset = inputPos - positions[particleIndex];
        float sqrDst = Vector2.Dot(offset, offset);

        // If particle is inside of input radius, calculate force towards input point
        if (sqrDst < radius * radius)
        {
            float dst = Mathf.Sqrt(sqrDst);
            Vector2 dirToInputPoint = dst <= float.Epsilon ? Vector2.zero : offset / dst;
            // Value is 1 when particle is exactly at input point; 0 when at edge of input circle
            float centreT = 1 - dst / radius;
            // Calculate the force (velocity is subtracted to slow the particle down)
            interactionForce += (dirToInputPoint * strength - velocities[particleIndex]) * centreT;
        }

        return interactionForce;
    }

    void SimulationStep(float deltaTime)
    {
        // Apply gravity and predict next positions
        Parallel.For(0, numParticles, i =>
        {
            velocities[i] += Vector2.down * gravity * deltaTime;
            predictedPositions[i] = positions[i] + velocities[i] * deltaTime;
        });

        // Update spatial lookup with predicted positions
        UpdateSpatialLookup(predictedPositions, smoothingRadius);

        // Calculate densities
        Parallel.For(0, numParticles, i =>
        {
            densities[i] = CalculateDensity(i);
        });

        // Calculate and apply pressure forces
        Parallel.For(0, numParticles, i =>
        {
            Vector2 pressureForce = CalculatePressureForce(i);
            Vector2 pressureAcceleration = pressureForce / densities[i];
            
            Vector2 viscosityForce = CalculateViscosityForce(i); // Calculate the viscosity force
            Vector2 viscosityAcceleration = viscosityForce / densities[i]; // Convert viscosity force to acceleration

            // Update the velocity by adding both pressure and viscosity accelerations
            velocities[i] += (pressureAcceleration + viscosityAcceleration) * deltaTime;
        });

        // Update positions and resolve collisions
        Parallel.For(0, numParticles, i =>
        {
            positions[i] += velocities[i] * deltaTime;
            ResolveCollisions(ref positions[i], ref velocities[i]);
        });
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
        const int maxInstancesPerBatch = 1023;

        for (int i = 0; i < matrices.Count; i += maxInstancesPerBatch)
        {
            int batchSize = Mathf.Min(maxInstancesPerBatch, matrices.Count - i);
            Matrix4x4[] batchMatrices = new Matrix4x4[batchSize];
            matrices.CopyTo(i, batchMatrices, 0, batchSize);

            Graphics.DrawMeshInstanced(mesh, 0, material, batchMatrices, batchSize);
        }
    }

    void ResolveCollisions(ref Vector2 position, ref Vector2 velocity)
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
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2;
            vertices[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 2] = i + 1;
            triangles[i * 3 + 1] = (i + 2 > segments) ? 1 : i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Debug.Log("Circle Mesh Created with " + segments + " segments.");

        return mesh;
    }

    void DrawCircles()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            Matrix4x4 circleMatrix = Matrix4x4.TRS(positions[i], Quaternion.identity, Vector3.one * radius);
            AddInstance(circleMesh, circleMaterial, circleMatrix);
        }
    }

    // Convert a position to the coordinate of the cell it is within
    public (int x, int y) PositionToCellCoord(Vector2 point, float radius)
    {
        int cellX = (int)(point.x / radius);
        int cellY = (int)(point.y / radius);
        return (cellX, cellY);
    }

    // Convert a cell coordinate into a single number.
    // Hash collisions (different cells -> same value) are unavoidable, but we want to at
    // least try to minimize collisions for nearby cells. I'm sure there are better ways,
    // but this seems to work okay.
    public uint HashCell(int cellX, int cellY)
    {
        uint a = (uint)cellX * 15823;
        uint b = (uint)cellY * 9737333;
        return a + b;
    }

    // Wrap the hash value around the length of the array (so it can be used as an index)
    public uint GetKeyFromHash(uint hash)
    {
        return hash % (uint)spatialLookup.Length;
    }

    public void UpdateSpatialLookup(Vector2[] points, float radius)
    {
        this.points = points;
        this.cellRaduis = radius;

        // Create (unordered) spatial lookup
        Parallel.For(0, points.Length, i =>
        {
            (int cellX, int cellY) = PositionToCellCoord(points[i], cellRaduis);
            uint cellKey = GetKeyFromHash(HashCell(cellX, cellY));
            spatialLookup[i] = new Entry((uint)i, cellKey);
            startIndices[i] = int.MaxValue; // Reset start index
        });

        // Sort by cell key
        Array.Sort(spatialLookup);

        //string cellKeys = string.Join(", ", spatialLookup.Select(entry => entry.cellKey.ToString()));
        //Debug.Log("cellKeys: " + cellKeys);

        // Calculate start indices of each unique cell key in the spatial lookup
        Parallel.For(0, points.Length, i =>
        {
            uint key = spatialLookup[i].cellKey;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialLookup[i - 1].cellKey;
            if (key != keyPrev)
            {
                startIndices[key] = (uint)i;
            }
        });


    }

    public List<int> GetNeighbours(Vector2 position)
    {
        List<int> particlesInCell = new List<int>();

        // Step 1: Convert position to cell coordinates
        (int cellX, int cellY) = PositionToCellCoord(position, cellRaduis);

        // Step 2: Hash the cell coordinates to get the cell key
        uint key = GetKeyFromHash(HashCell(cellX, cellY));

        // Step 3: Use startIndices to find the beginning of the cell's entries
        int cellStartIndex = (int)startIndices[key];

        // Check if cellStartIndex is valid
        if (cellStartIndex < 0 || cellStartIndex >= spatialLookup.Length)
        {
            //Debug.LogWarning($"Cell key {key} out of bounds or uninitialized.");
            return particlesInCell; // Return an empty list if out of bounds
        }

        // Step 4: Loop through spatialLookup from cellStartIndex
        for (int i = cellStartIndex; i < spatialLookup.Length; i++)
        {
            // Stop if we reach entries belonging to the next cell
            if (spatialLookup[i].cellKey != key) break;

            // Add particle index to the list
            particlesInCell.Add((int)spatialLookup[i].particleIndex);
        }

        return particlesInCell;
    }

    public void ForeachPointWithinRadius(Vector2 samplePoint, float radius, Action<int> callback)
    {
        // Define the offsets for a 3x3 block around the center cell
        List<(int offsetX, int offsetY)> cellOffsets = new List<(int, int)>
        {
            (-1, -1), (0, -1), (1, -1),
            (-1,  0), (0,  0), (1,  0),
            (-1,  1), (0,  1), (1,  1)
        };

        // Find which cell the sample point is in (this will be the center of our 3x3 block)
        (int centreX, int centreY) = PositionToCellCoord(samplePoint, radius);
        float sqrRadius = radius * radius;

        // Loop over all cells of the 3x3 block around the center cell
        foreach ((int offsetX, int offsetY) in cellOffsets)
        {
            // Get key of current cell, then loop over all points that share that key
            uint key = GetKeyFromHash(HashCell(centreX + offsetX, centreY + offsetY));

            // Bounds check for startIndices
            //if (key >= startIndices.Length || startIndices[key] == uint.MaxValue)
            //{
                //Debug.LogWarning($"Key {key} is out of bounds or uninitialized in startIndices with length {startIndices.Length}");
                //continue;
            //}

            int cellStartIndex = (int)startIndices[key];
            
            // Bounds check for cellStartIndex
            //if (cellStartIndex < 0 || cellStartIndex >= spatialLookup.Length)
            //{
               // Debug.LogWarning($"cellStartIndex {cellStartIndex} is out of bounds for spatialLookup with length {spatialLookup.Length}");
                //continue;
            //}
            
            for (int i = cellStartIndex; i < spatialLookup.Length; i++)
            {
                // Exit loop if we're no longer looking at the correct cell
                if (spatialLookup[i].cellKey != key) break;

                int particleIndex = (int)spatialLookup[i].particleIndex;

                // Validate particleIndex
                //if (particleIndex < 0 || particleIndex >= points.Length)
                //{
                //    Debug.LogWarning($"Invalid particleIndex {particleIndex} for points with length {points.Length}");
                //    break;
                //}

                float sqrDst = (points[particleIndex] - samplePoint).sqrMagnitude;

                // Test if the point is inside the radius
                if (sqrDst <= sqrRadius)
                {
                    callback(particleIndex);
                }
            }
        }
    }


    void Update()
    {
        meshMaterialInstances.Clear();  // Clear old instances
        SimulationStep(1/180f);
        DrawCircles();

        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mousePosition2D = new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);

        float radius = 3.0f; // Set your desired radius
        //float strength = 10.0f; // Set the force strength

        float strength = 0.0f;
        if (Input.GetMouseButton(0)) // Left mouse button
        {
            strength = 100.0f;
        }
        else if (Input.GetMouseButton(1)) // Right mouse button
        {
            strength = -100.0f;
        }

        // Loop through particles and apply interaction force based on mouse position
        for (int i = 0; i < numParticles; i++)
        {
            Vector2 force = InteractionForce(mousePosition2D, radius, strength, i);
            velocities[i] += force * Time.deltaTime; // Apply the force to particle velocity
        }

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
}
