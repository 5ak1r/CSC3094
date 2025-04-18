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

    public GameObject gameObject;
    public HashSet<int> neighbours;

    public void ResolveCollisions(int boxSize)
    {
        float particleRadius = ParticleManagerCPU.PARTICLE_RADIUS;
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
        else if (position.z >= boxSize / 2)
        {
            position.z = boxSize / 2;
            velocity.z *= damping;
        }
    }
}

public class ParticleManagerCPU : MonoBehaviour
{

    [Header("General Constants")]
    public const int BOX_SIZE = 8;
    public const int HALF_BOX = BOX_SIZE / 2;
    public const float EPSILON = 1e-2f;
    public const float DELTA_TIME = 0.03f;

    [Header("Particle Properties")]
    public const float PARTICLE_MASS = 1.0f;
    public const float PARTICLE_MASS_SQUARED = PARTICLE_MASS * PARTICLE_MASS;
    public const float RECIPROCAL_MASS = 1 / PARTICLE_MASS;
    public const float TARGET_DENSITY = 3.0f;
    public const float GAS_CONSTANT = 50.0f;
    public const float VISCOSITY = 0.003f;

    [Header("Particle Settings")]
    public const int ROW_COUNT = 10; //row count = 10 gives 13 fps
    public const int HALF_ROW = ROW_COUNT / 2;
    public const int PARTICLE_COUNT = ROW_COUNT * ROW_COUNT * ROW_COUNT;
    public const float PARTICLE_RADIUS = 0.05f;
    public const float PARTICLE_EFFECT_RADIUS = 1.5f;
    public const float PARTICLE_EFFECT_RADIUS_SQUARED = PARTICLE_EFFECT_RADIUS * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_CUBED = PARTICLE_EFFECT_RADIUS_SQUARED * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_FOURTH = PARTICLE_EFFECT_RADIUS_CUBED * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_FIFTH = PARTICLE_EFFECT_RADIUS_FOURTH * PARTICLE_EFFECT_RADIUS;
    public const float SPAWN_VARIANCE = 0.2f;
    public readonly Vector3 SPAWN_POINT = new(HALF_BOX, HALF_BOX, HALF_BOX / 2);

    [Header("Physics Settings")]
    public const float GRAVITY = 9.81f;

    [Header("Spatial Hash")]
    public Dictionary<uint, List<int>> neighbourTable;

    [Header("Particles")]
    public GameObject particleObj;
    public Particle[] particles;

    private void Awake()
    {
        particles = new Particle[PARTICLE_COUNT];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new(HALF_BOX, HALF_BOX, HALF_BOX / 2), new(BOX_SIZE, BOX_SIZE, HALF_BOX));
    }

    private void Start()
    {
        SpawnParticles();
    }

    private void Update()
    {
        //apply gravity and update position prediction
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i].velocity += DELTA_TIME * GRAVITY * Vector3.down;
            //particles[i].position += particles[i].velocity * DELTA_TIME;
        }

        //update spatial hash and keys
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i].cell = SpatialHashCPU.CalculateCell(particles[i].position);
            particles[i].hash = SpatialHashCPU.CalculateCellHash(particles[i].cell);
        }

        //update neighbour table
        neighbourTable = SpatialHashCPU.NeighbourTable(particles);

        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i].neighbours = SpatialHashCPU.GetNeighbours(neighbourTable, particles[i], particles);
        }

        //calculate density and pressure
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i] = CalculateDensityPressure(particles[i]);
        }

        //apply forces
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            /*particles[i] = ComputeForces(particles[i]);

            particles[i].velocity += RECIPROCAL_MASS * DELTA_TIME * particles[i].currentForce;
            particles[i].position = particles[i].gameObject.transform.position;
            particles[i].position += particles[i].velocity * DELTA_TIME;*/
            Vector3 pressureForce = CalculatePressureForce(particles[i].position, particles[i]);
            Vector3 pressureAcceleration = pressureForce / particles[i].density;
            particles[i].velocity += pressureAcceleration * DELTA_TIME;

            Vector3 viscosityForce = CalculateViscosityForce(particles[i]);
            Vector3 viscosityAcceleration = viscosityForce / particles[i].density;
            particles[i].velocity += viscosityAcceleration * DELTA_TIME;
        }

        // apply actual position
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i].position += particles[i].velocity * DELTA_TIME;
            particles[i].ResolveCollisions(BOX_SIZE);

            particles[i].gameObject.transform.position = particles[i].position; //make the actual particle move now
        }
    }

    private void SpawnParticles()
    {
        int counter = 0;
        for (int i = -HALF_ROW; i < HALF_ROW; i++)
        {
            for (int j = -HALF_ROW; j < HALF_ROW; j++)
            {
                for (int k = -HALF_ROW; k < HALF_ROW; k++)
                {

                    Vector3 particlePosition = SPAWN_POINT + SPAWN_VARIANCE * (new Vector3(i, j, k) + Random.onUnitSphere);

                    GameObject particleInit = Instantiate(particleObj, particlePosition, Quaternion.identity);
                    particleInit.hideFlags = HideFlags.HideInHierarchy;

                    Particle particleInst = new()
                    {
                        ID = counter,
                        position = particlePosition,
                        gameObject = particleInit,
                        currentForce = new(0.0f, PARTICLE_MASS * GRAVITY, 0.0f)
                    };

                    particles[counter] = particleInst;
                    counter++;
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
        if (dist > PARTICLE_EFFECT_RADIUS) return 0;

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

            float dist = (particle.position - other.position).magnitude;
            float distSquared = dist * dist;

            if (distSquared < PARTICLE_EFFECT_RADIUS_SQUARED)
                sum += Poly6(distSquared);
        }

        sum += Poly6(0.0f); //add own density

        particle.density = sum * PARTICLE_MASS + EPSILON; //add small value to prevent division by 0;
        particle.pressure = GAS_CONSTANT * (particle.density - TARGET_DENSITY);

        return particle;
    }

    public Vector3 CalculatePressureForce(Vector3 position, Particle particle)
    {
        Vector3 pressureForce = Vector3.zero;

        foreach (int otherID in particle.neighbours)
        {
            Particle other = particles[otherID];
            if (particle.ID == other.ID) continue;

            Vector3 offset = other.position - position;
            float dist = offset.magnitude;

            if (dist < EPSILON) continue;

            Vector3 dir = offset / dist;
            pressureForce += (particle.pressure + other.pressure) * 0.5f * PARTICLE_MASS * SpikyKernelGradient(dist, dir) / other.density;
        }

        return pressureForce;
    }

    public Vector3 CalculateViscosityForce(Particle particle) {
        
        Vector3 viscosityForce = Vector3.zero;

        foreach (int otherID in particle.neighbours)
        {
            Particle other = particles[otherID];
            if(particle.ID == other.ID) continue;

            float dist = (particle.position - other.position).magnitude;
            viscosityForce += (other.velocity - particle.velocity) * ViscosityLaplacianKernel(dist);
        }

        return viscosityForce * VISCOSITY;
    }
}