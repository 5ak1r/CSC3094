using Unity.VisualScripting;
using UnityEngine;

public class ParticleManager : MonoBehaviour {
    
    [Header("Constants")]
    public const int BOX_SIZE = 50;
    public const int ROW_COUNT = 12;
    public const int PARTICLE_COUNT = ROW_COUNT * ROW_COUNT * ROW_COUNT;
    public const float GRAVITY = -9.81f;

    [Header("Particles")]
    public GameObject particle;
    public static Vector3[] positions;

    [Header("Spatial Hash")]
    private SpatialHash _spatialHash;

    private void Awake() {
        positions = new Vector3[PARTICLE_COUNT];
    }

    private void Start() {

        for(int i = 0; i < ROW_COUNT; i++) {
            for(int j = 0; j < ROW_COUNT; j++) {
                for(int k = 0; k < ROW_COUNT; k++) {
                    Vector3 particlePosition = new(i, j, k);
                    int ID = i * ROW_COUNT * ROW_COUNT + j * ROW_COUNT + k;
                    GameObject particleInit = Instantiate(particle, particlePosition, Quaternion.identity);
                    particleInit.GetComponent<Particle>().ID = ID;
                    particleInit.hideFlags = HideFlags.HideInHierarchy;
                    positions[ID] = particlePosition;
                }
            }
        }
    }

}