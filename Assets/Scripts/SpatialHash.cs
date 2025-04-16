using System;
using System.Collections.Generic;
using UnityEngine;

public class SpatialHash : MonoBehaviour
{
    // property calculations
    public static Vector3Int CalculateCell(Vector3 position) {
        return Vector3Int.FloorToInt(position / ParticleManager.BOX_SIZE);
    }

    public static uint CalculateCellHash(Vector3Int cell) {
        return  (uint)(cell.x * 73856093) ^
                (uint)(cell.y * 19349663) ^
                (uint)(cell.z * 83492791);
    }

    public static int CalculateCellKey(uint hash) {
        return (int)(hash % ParticleManager.PARTICLE_COUNT);
    }

    // sort keys for spatial lookup
    public static Particle[] SortParticles(Particle[] particles) {
        Array.Sort(particles, (i, j) => i.key.CompareTo(j.key));

        return particles;
    }

    public static Dictionary<uint, uint> NeighbourTable(Particle[] particles) {
        Dictionary<uint, uint> neighbourTable = new();

        for(uint i = 0; i < particles.Length; i++) {
            neighbourTable[particles[i].hash] = i;
        }

        return neighbourTable;
    }

    public static HashSet<Particle> GetNeighbours(Particle particle, Particle[] particles) {
        HashSet<Particle> neighbours = new();

        for(int dx = -1; dx <= 1; dx++) {
            for(int dy = -1; dy <= 1; dy++) {
                for(int dz = -1; dz <= 1; dz++) {
                    Vector3Int neighbourCell = particle.cell + new Vector3Int(dx, dy, dz);
                    uint neighbourHash = CalculateCellHash(neighbourCell);

                    if(particle.neighbourTable.ContainsKey(neighbourHash)) {
                        neighbours.Add(particles[particle.neighbourTable[neighbourHash]]);
                    }
                }
            }
        }
        
        return neighbours;
    }
}
