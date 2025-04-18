using System.Collections.Generic;
using UnityEngine;

public struct Particle
{
    public int ID;

    public Vector3 currentForce;
    public Vector3 velocity;
    public Vector3 position;

    public float density;
    public float pressure;

    public Vector3Int cell;
    public uint hash;

    public HashSet<int> neighbours;

    public GameObject gameObject;

    public void ResolveCollisions(int boxSize)
    {
        float particleRadius = ParticleManager.PARTICLE_RADIUS;
        float damping = -0.3f;

        if (position.y <= particleRadius)
        {
            position.y = particleRadius;
            velocity.y *= damping;
        }
        else if (position.y >= boxSize)
        {
            position.y = boxSize;
            velocity.y *= damping;
        }

        if (position.x <= particleRadius)
        {
            position.x = particleRadius;
            velocity.x *= damping;
        }
        else if (position.x >= boxSize)
        {
            position.x = boxSize;
            velocity.x *= damping;
        }

        if (position.z <= particleRadius)
        {
            position.z = particleRadius;
            velocity.z *= damping;
        }
        else if (position.z >= boxSize)
        {
            position.z = boxSize;
            velocity.z *= damping;
        }
    }
}

public class ParticleManager : MonoBehaviour
{

    [Header("General Constants")]
    public const int BOX_SIZE = 6;
    public const int HALF_BOX = BOX_SIZE / 2;
    public const float EPSILON = 1e-2f;
    public const float DELTA_TIME = 0.03f;

    [Header("Particle Properties")]
    public const float PARTICLE_MASS = 0.005f;
    public const float PARTICLE_MASS_SQUARED = PARTICLE_MASS * PARTICLE_MASS;
    public const float RECIPROCAL_MASS = 1 / PARTICLE_MASS;
    public const float TARGET_DENSITY = 1.0f;
    public const float GAS_CONSTANT = 50.0f;
    public const float VISCOSITY = 0.003f;

    [Header("Particle Settings")]
    public const int ROW_COUNT = 14;
    public const int HALF_ROW = ROW_COUNT / 2;
    public const int PARTICLE_COUNT = ROW_COUNT * ROW_COUNT * ROW_COUNT;
    public const float PARTICLE_RADIUS = 0.1f;
    public const float PARTICLE_EFFECT_RADIUS = 0.5f;
    public const float PARTICLE_EFFECT_RADIUS_SQUARED = PARTICLE_EFFECT_RADIUS * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_CUBED = PARTICLE_EFFECT_RADIUS_SQUARED * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_FOURTH = PARTICLE_EFFECT_RADIUS_CUBED * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_FIFTH = PARTICLE_EFFECT_RADIUS_FOURTH * PARTICLE_EFFECT_RADIUS;
    public const float SPAWN_VARIANCE = 0.2f;
    public readonly Vector3 SPAWN_POINT = new(HALF_BOX, BOX_SIZE * 0.9f, HALF_BOX);
    public Material redMaterial;
    public Material blueMaterial;
    public Material otherMaterial;

    [Header("Physics Settings")]
    public const float GRAVITY = -9.81f;

    [Header("Spatial Hash")]
    public Dictionary<uint, List<int>> neighbourTable;

    [Header("Particles")]
    public GameObject particleObj;
    public Particle[] particles;

    private void Awake()
    {
        particles = new Particle[PARTICLE_COUNT];
        SpawnParticles();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new(HALF_BOX, HALF_BOX, HALF_BOX), new(BOX_SIZE, BOX_SIZE, BOX_SIZE));
    }

    private void Update()
    {

        //spatial hash and keys
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            Particle currentParticle = particles[i];

            currentParticle.cell = SpatialHash.CalculateCell(currentParticle.position);
            currentParticle.hash = SpatialHash.CalculateCellHash(currentParticle.cell);

            particles[i] = currentParticle;
        }

        //update neighbour table
        neighbourTable = SpatialHash.NeighbourTable(particles);

        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i].neighbours = SpatialHash.GetNeighbours(neighbourTable, particles[i], particles);
        }

        //apply velocity and resolve collisions
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            Particle currentParticle = particles[i];

            currentParticle.velocity += RECIPROCAL_MASS * DELTA_TIME * currentParticle.currentForce; //multiplying is faster than dividing
            currentParticle.position += currentParticle.velocity * DELTA_TIME;

            currentParticle.ResolveCollisions(BOX_SIZE);

            particles[i] = currentParticle;
        }

        //calculate density and pressure
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i] = CalculateDensityPressure(particles[i]);
        }

        //apply forces
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            Particle currentParticle = particles[i];

            currentParticle = ComputeForces(currentParticle);
            currentParticle.gameObject.transform.position = currentParticle.position;

            particles[i] = currentParticle;
        }
    }

    private void SpawnParticles()
    {

        for (int i = -HALF_ROW; i < HALF_ROW; i++)
        {
            for (int j = -HALF_ROW; j < HALF_ROW; j++)
            {
                for (int k = -HALF_ROW; k < HALF_ROW; k++)
                {

                    Vector3 particlePosition = SPAWN_POINT + 1.2f * (PARTICLE_RADIUS * new Vector3(i, j, k) + PARTICLE_RADIUS * SPAWN_VARIANCE * Random.onUnitSphere);
                    int id = (i + HALF_ROW) * ROW_COUNT * ROW_COUNT + (j + HALF_ROW) * ROW_COUNT + k + HALF_ROW;

                    GameObject particleInit = Instantiate(particleObj, particlePosition, Quaternion.identity);
                    particleInit.hideFlags = HideFlags.HideInHierarchy;

                    Particle particleInst = new()
                    {
                        ID = id,
                        position = particlePosition,
                        gameObject = particleInit,
                        currentForce = new(0.0f, PARTICLE_MASS * GRAVITY, 0.0f)
                    };

                    particles[id] = particleInst;
                }
            }
        }
    }

    //kernels
    public float Poly6(float r2)
    {
        //if(r2 > PARTICLE_EFFECT_RADIUS_SQUARED) return 0; don't need this check as it's done beforehand

        float x = 1.0f - r2 / PARTICLE_EFFECT_RADIUS_SQUARED;
        return 315.0f / (64.0f * Mathf.PI * PARTICLE_EFFECT_RADIUS_CUBED) * x * x * x;
    }

    public float SpikyKernelFirstDerivative(float dist)
    {
        float x = 1.0f - dist / PARTICLE_EFFECT_RADIUS;
        return -45.0f / (Mathf.PI * PARTICLE_EFFECT_RADIUS_FOURTH) * x * x;
    }

    public float SpikyKernelSecondDerivative(float dist)
    {
        float x = 1.0f - dist / PARTICLE_EFFECT_RADIUS;
        return 90.0f / (Mathf.PI * PARTICLE_EFFECT_RADIUS_FIFTH) * x;
    }

    public Vector3 SpikyKernelGradient(float dist, Vector3 dir)
    {
        return SpikyKernelFirstDerivative(dist) * dir;
    }

    public float ViscosityLaplacianKernel(float r)
    {
        if (r >= 0.0f && r <= PARTICLE_EFFECT_RADIUS)
        {
            return 45.0f / (Mathf.PI * Mathf.Pow(PARTICLE_EFFECT_RADIUS, 6)) * (PARTICLE_EFFECT_RADIUS - r);
        }

        return 0f;
    }

    //calculations
    public Particle CalculateDensityPressure(Particle particle)
    {
        float sum = 0;

        foreach (int otherID in particle.neighbours)
        {
            Particle other = particles[otherID]; //foreach(otherID in particle.neighbours) Particle other = particles[otherID];
            if (particle.ID == other.ID) continue;

            Vector3 diff = particle.position - other.position;
            float diffSquared = Vector3.SqrMagnitude(diff);

            if (diffSquared > PARTICLE_EFFECT_RADIUS_SQUARED) continue;

            sum += Poly6(diffSquared * 0.004f);
        }

        particle.density = sum * PARTICLE_MASS + EPSILON; //add small value to prevent division by 0;
        particle.pressure = GAS_CONSTANT * (particle.density - TARGET_DENSITY);

        return particle;
    }

    public Particle ComputeForces(Particle particle)
    {
        Vector3 pos = particle.position;

        Vector3 pressureForce = Vector3.zero;
        Vector3 viscosityForce = Vector3.zero;

        foreach (int otherID in particle.neighbours)
        {
            Particle other = particles[otherID]; //foreach(otherID in particle.neighbours) Particle other = particles[otherID];
            if (particle.ID == other.ID) continue;
            if (other.density <= EPSILON) continue;

            float dist = Vector3.Distance(other.position, pos);

            if (dist >= PARTICLE_RADIUS * 2) continue;

            Vector3 pressureGradientDir = Vector3.Normalize(pos - other.position);
            float pressureTerm = (particle.pressure + other.pressure) / (2.0f * other.density);
            Vector3 pressureContribution = -PARTICLE_MASS_SQUARED * pressureTerm * SpikyKernelGradient(dist, pressureGradientDir);

            Vector3 velDiff = other.velocity - particle.velocity;
            Vector3 viscosityContribution = VISCOSITY * PARTICLE_MASS * velDiff / other.density;
            viscosityContribution *= ViscosityLaplacianKernel(dist);

            pressureForce += pressureContribution;
            viscosityForce += viscosityContribution;
        }

        particle.currentForce = new Vector3(0.0f, GRAVITY * PARTICLE_MASS, 0.0f) + pressureForce + viscosityForce;

        return particle;
    }
}