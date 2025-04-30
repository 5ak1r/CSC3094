using UnityEngine;

[System.Serializable]
public struct Particle {
    public int ID;
    public float density;
    public float pressure;

    public Vector3 currentForce;
    public Vector3 velocity;
    public Vector3 position;

    public GameObject gameObject;

    public void ResolveCollisions(int boxSize) {
        int walls = boxSize;
        
        if(position.y <= 0.0f) {
            Vector3 pos = position;
            pos.y = 0.0f;
            position = pos;

            velocity.y *= -0.9f;

            if(Mathf.Abs(velocity.y) < 0.1f) velocity.y = 0.0f; //stop if negligible bounce
        }

        if (Mathf.Abs(position.x) >= walls) {
            Vector3 pos = position;
            pos.x = (position.x > 0.0f) ? walls : -walls;
            position = pos;

            velocity.x *= -0.9f;
        }

        if (Mathf.Abs(position.z) >= walls) {
            Vector3 pos = position;
            pos.z = (position.z > 0.0f) ? walls : -walls;
            position = pos;

            velocity.z *= -0.9f;
        }
    }
}

public class ParticleManager : MonoBehaviour {
    
    [Header("General Constants")]
    public const int BOX_SIZE = 10;
    public const float HALF_BOX = BOX_SIZE / 2;

    [Header("Particle Settings")]
    public const int ROW_COUNT = 10;
    public const int HALF_ROW = ROW_COUNT / 2;
    public const int PARTICLE_COUNT = ROW_COUNT * ROW_COUNT * ROW_COUNT;
    public const float PARTICLE_RADIUS = 0.2f;
    public const float PARTICLE_MASS = 1.0f;
    public const float SPAWN_VARIANCE = 0.1f;
    public readonly Vector3 SPAWN_POINT = new(HALF_BOX, BOX_SIZE, HALF_BOX);
    
    [Header("Physics Settings")]
    public const float GRAVITY = -9.81f;

    [Header("Particles")]
    public GameObject particleObj;
    public Particle[] particles;

    private void Awake() {
        particles = new Particle[PARTICLE_COUNT];
        SpawnParticles();
    }

    private void Update() {
        for(int i = 0; i < PARTICLE_COUNT; i++) {
            Particle currentParticle = particles[i];

            currentParticle.velocity += currentParticle.currentForce / PARTICLE_MASS * Time.deltaTime;
            currentParticle.position += currentParticle.velocity * Time.deltaTime;

            currentParticle.ResolveCollisions(BOX_SIZE);

            currentParticle.gameObject.transform.position = currentParticle.position;
            particles[i] = currentParticle;
        }
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new(HALF_BOX, HALF_BOX, HALF_BOX), new(BOX_SIZE, BOX_SIZE, BOX_SIZE));
    }

    private void SpawnParticles() {

        for(int i = -HALF_ROW; i < HALF_ROW; i++) {
            for(int j = -HALF_ROW; j < HALF_ROW; j++) {
                for(int k = -HALF_ROW; k < HALF_ROW; k++) {
                    
                    Vector3 particlePosition = SPAWN_POINT + new Vector3(i, j, k) * PARTICLE_RADIUS + PARTICLE_RADIUS * SPAWN_VARIANCE * Random.onUnitSphere;
                    int id = (i + HALF_ROW) * ROW_COUNT * ROW_COUNT + (j + HALF_ROW) * ROW_COUNT + k + HALF_ROW;

                    GameObject particleInit = Instantiate(particleObj, particlePosition, Quaternion.identity);
                    particleInit.hideFlags = HideFlags.HideInHierarchy;

                    Particle particleInst = new()
                    {
                        ID = id,
                        position = particlePosition,
                        currentForce = new(0, GRAVITY, 0),
                        gameObject = particleInit
                    };

                    particles[id] = particleInst;
                }
            }
        }
    }
}