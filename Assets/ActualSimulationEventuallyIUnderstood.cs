using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public struct Entry : IComparable<Entry>
{
    public uint cellKey;
    public uint particleIndex;

    // Constructor with particleIndex and cellKey
    public Entry(uint particleIndex, uint cellKey)
    {
        this.particleIndex = particleIndex;
        this.cellKey = cellKey;
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
    public float centralForceStrength;

    public float targetDensity;
    public float pressureMultiplier;

    private float particleSpacing = 0.1f;

    private float[] densities;
    private Vector2[] velocities;
    private Vector2[] positions;
    private Vector2[] predictedPositions;
    private Vector2[] randomDirections;
    private Entry[] spatialLookup;
    private Dictionary<uint, int> startIndices;

    private Vector2[] points;
    private float cellRadius;

    private Mesh circleMesh;
    private Material circleMaterial;

    float width;
    float height;

    private Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>> meshMaterialInstances;

    void Start()
    {
        meshMaterialInstances = new Dictionary<Mesh, Dictionary<Material, List<Matrix4x4>>>();

        Camera cam = Camera.main;
        width = cam.orthographicSize * 2.0f * cam.aspect;
        height = cam.orthographicSize * 2.0f;

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
        startIndices = new Dictionary<uint, int>();

        points = positions;

        for (int i = 0; i < numParticles; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            randomDirections[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        ReshapeParticles();
        UpdateSpatialLookup(positions, smoothingRadius);

        for (int i = 0; i < numParticles; i++)
        {
            predictedPositions[i] = positions[i];
        }
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

        ForeachPointWithinRadius(position, smoothingRadius, otherParticleIndex =>
        {
            if (particleIndex == otherParticleIndex) return;

            float dst = (position - positions[otherParticleIndex]).magnitude;
            float influence = ViscositySmoothingKernel(dst, smoothingRadius);
            viscosityForce += (velocities[otherParticleIndex] - velocities[particleIndex]) * influence;
        });

        return viscosityForce * viscosityStrength;
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 pressureForce = Vector2.zero;
        Vector2 particlePosition = predictedPositions[particleIndex];
        float density = densities[particleIndex];

        ForeachPointWithinRadius(particlePosition, smoothingRadius, otherParticleIndex =>
        {
            if (particleIndex == otherParticleIndex) return;

            Vector2 offset = predictedPositions[otherParticleIndex] - particlePosition;
            float dst = offset.magnitude;

            Vector2 dir = dst == 0 ? GetRandomDir(particleIndex) : offset / dst;

            float slope = SmoothingKernelDerivative(dst, smoothingRadius);
            float otherDensity = densities[otherParticleIndex];
            float sharedPressure = CalculateSharedPressure(otherDensity, density);

            //float safeDensity = Mathf.Max(density, 0.0001f); // Avoid division by zero

            pressureForce -= (sharedPressure * dir * slope * mass) / density;
        });

        return pressureForce;
    }

    float CalculateDensity(int particleIndex)
    {
        float density = 0;
        Vector2 particlePosition = predictedPositions[particleIndex];

        ForeachPointWithinRadius(particlePosition, smoothingRadius, otherParticleIndex =>
        {
            Vector2 offset = predictedPositions[otherParticleIndex] - particlePosition;
            float dst = offset.magnitude;
            if (dst < 0.001f) dst = 0.001f;
            
            float influence = SmoothingKernel(dst, smoothingRadius);
            density += mass * influence;
        });
        density = Mathf.Clamp(density, -3000, 3000);
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

        if (sqrDst < radius * radius)
        {
            float dst = Mathf.Sqrt(sqrDst);
            Vector2 dirToInputPoint = dst <= float.Epsilon ? Vector2.zero : offset / dst;
            float centreT = 1 - dst / radius;
            interactionForce += (dirToInputPoint * strength - velocities[particleIndex]) * centreT;
        }

        return interactionForce;
    }

    void ApplyCentralForce(float deltaTime)
    {
        float desiredRadius = 4.0f; // Set your desired orbit radius
        float centralForceStrength = this.centralForceStrength; // Adjust strength as needed
        float damping = 0.1f; // Damping factor to stabilize radial movement

        Parallel.For(0, numParticles, i =>
        {
            Vector2 position = predictedPositions[i];
            float distanceToCenter = position.magnitude;

            if (distanceToCenter == 0f)
            {
                // Assign a small offset to avoid division by zero
                position = new Vector2(0.001f, 0f);
                distanceToCenter = position.magnitude;
            }

            Vector2 radialDirection = position / distanceToCenter;

            // Radial force to push/pull particles towards desired radius
            float radialForceMagnitude = (desiredRadius - distanceToCenter) * centralForceStrength;
            Vector2 radialForce = radialDirection * radialForceMagnitude;

            // Apply the radial force
            velocities[i] += radialForce * deltaTime;

            // Apply damping to radial velocity to prevent oscillations
            //float radialVelocity = Vector2.Dot(velocities[i], radialDirection);
            //velocities[i] -= radialDirection * radialVelocity * damping;
        });
    }


    void SimulationStep(float deltaTime)
    {
        // Apply gravity and predict next positions
        
        Parallel.For(0, numParticles, i =>
        {
            //velocities[i] += Vector2.down * gravity * deltaTime;
            predictedPositions[i] = positions[i] + velocities[i] * deltaTime;
        });
        ApplyCentralForce(deltaTime);

        // Update spatial lookup with predicted positions
        UpdateSpatialLookup(predictedPositions, smoothingRadius);

        // Calculate densities
        Parallel.For(0, numParticles, i =>
        {
            densities[i] = CalculateDensity(i);
        });

        // Calculate and apply pressure and viscosity forces
        Parallel.For(0, numParticles, i =>
        {
            Vector2 pressureForce = CalculatePressureForce(i);
            Vector2 pressureAcceleration = pressureForce / densities[i];

            Vector2 viscosityForce = CalculateViscosityForce(i);
            Vector2 viscosityAcceleration = viscosityForce / densities[i];

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
        int cellX = Mathf.FloorToInt(point.x / radius);
        int cellY = Mathf.FloorToInt(point.y / radius);
        return (cellX, cellY);
    }

    // Hash the cell coordinates to get a unique cell key
    public uint HashCell(int cellX, int cellY)
    {
        unchecked
        {
            uint a = (uint)(cellX * 73856093);
            uint b = (uint)(cellY * 19349663);
            return a ^ b;
        }
    }

    public void UpdateSpatialLookup(Vector2[] points, float radius)
    {
        this.points = points;
        this.cellRadius = radius;

        // Create (unordered) spatial lookup
        for (int i = 0; i < points.Length; i++)
        {
            (int cellX, int cellY) = PositionToCellCoord(points[i], cellRadius);
            uint cellKey = HashCell(cellX, cellY);
            spatialLookup[i] = new Entry((uint)i, cellKey);
        }

        // Sort by cell key
        Array.Sort(spatialLookup);

        // Build startIndices dictionary
        startIndices.Clear();
        for (int i = 0; i < spatialLookup.Length; i++)
        {
            uint key = spatialLookup[i].cellKey;
            if (!startIndices.ContainsKey(key))
            {
                startIndices[key] = i;
            }
        }
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
            int neighborCellX = centreX + offsetX;
            int neighborCellY = centreY + offsetY;
            uint key = HashCell(neighborCellX, neighborCellY);

            // Attempt to get the starting index of this cell
            if (!startIndices.TryGetValue(key, out int cellStartIndex))
            {
                continue; // Cell key not found
            }

            for (int i = cellStartIndex; i < spatialLookup.Length && spatialLookup[i].cellKey == key; i++)
            {
                int particleIndex = (int)spatialLookup[i].particleIndex;

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
        SimulationStep(Time.deltaTime);
        DrawCircles();

        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mousePosition2D = new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);

        float radius = 3.0f; // Set your desired radius

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
