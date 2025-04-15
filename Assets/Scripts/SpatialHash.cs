using System;
using UnityEngine;

public struct IndexKey {
    private int _index;
    public int Index { 
        get => _index;
        set {
            if(value < 0 || value >= ParticleManager.PARTICLE_COUNT)
                throw new ArgumentException("ERROR::INDEX_KEY::INDEX::SET::OUT_OF_BOUNDS");
            _index = value;
        }
    }

    private int _key;
    public int Key { 
        get => _key;
        set {
            if(value < 0 || value >= ParticleManager.PARTICLE_COUNT)
                throw new ArgumentException("ERROR::INDEX_KEY::KEY::SET::OUT_OF_BOUNDS");
            _key = value;
        }
    }
};

public class SpatialHash : MonoBehaviour
{
    [Header("Hash Variables")]
    private Vector3Int[] _cells;
    private uint[] _hashes;
    private IndexKey[] _keys;
    private int[] _startIndices;

    // setters
    public void SetCell(int index, Vector3Int value) {
        if(index < 0 || index >= _cells.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::SET_CELL::INDEX_OUT_OF_BOUNDS");
        else _cells[index] = value;
    }

    public void SetCells(Vector3Int[] cells) {
        _cells = cells;
    }

    public void SetHash(int index, uint value) {
        if(index < 0 || index >= _hashes.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::SET_HASH::INDEX_OUT_OF_BOUNDS");
        else _hashes[index] = value;
    }

    public void SetHashes(uint[] hashes) {
        _hashes = hashes;
    }

    public void SetKey(int index, IndexKey key) {
        if(index < 0 || index >= _keys.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::SET_KEY::INDEX_OUT_OF_BOUNDS");
        else _keys[index] = key;
    }

    public void SetKeys(IndexKey[] keys) {
        _keys = keys;
    }

    public void SetStartIndex(int index, int startIndex) {
        if(index < 0 || index >= _startIndices.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::SET_START_INDEX::INDEX_OUT_OF_BOUNDS");
        else _startIndices[index] = startIndex;
    }

    public void SetStartIndices(int[] startIndices) {
        _startIndices = startIndices;
    }

    // getters
    public Vector3Int GetCell(int index) {
        if(index < 0 || index >= _cells.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::GET_CELL::INDEX_OUT_OF_BOUNDS");
        return _cells[index];
    }

    public Vector3Int[] GetCells() {
        return _cells;
    }

    public uint GetHash(int index) {
        if(index < 0 || index >= _hashes.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::GET_HASH::INDEX_OUT_OF_BOUNDS");
        return _hashes[index];
    }

    public uint[] GetHashes() {
        return _hashes;
    }

    public IndexKey GetKey(int index) {
        if(index < 0 || index >= _keys.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::GET_KEY::INDEX_OUT_OF_BOUNDS");
        return _keys[index];
    }

    public IndexKey[] GetKeys() {
        return _keys;
    }

    public int GetStartIndex(int index) {
        if(index < 0 || index >= _startIndices.Length) throw new ArgumentException("ERROR::SPATIAL_HASH::SET_START_INDEX::INDEX_OUT_OF_BOUNDS");
        else return _startIndices[index];
    }

    public int[] GetStartIndices() {
        return _startIndices;
    }

    // property calculations
    public Vector3Int CalculateCell(Vector3 position) {
        return Vector3Int.FloorToInt(position / ParticleManager.BOX_SIZE);
    }

    public uint CalculateCellHash(Vector3Int cell) {
        return  (uint)(cell.x * 73856093) ^
                (uint)(cell.y * 19349663) ^
                (uint)(cell.z * 83492791);
    }

    public int CalculateCellKey(uint hash) {
        return (int)(hash % ParticleManager.PARTICLE_COUNT);
    }

    // update arrays with new values
    public void UpdateCells(Vector3[] positions) {
        Vector3Int[] cells = new Vector3Int[positions.Length];

        for(int i = 0; i < positions.Length; i++)
            cells[i] = CalculateCell(positions[i]);

        SetCells(cells);
    }

    public void UpdateHashes(Vector3Int[] cells) {
        uint[] hashes = new uint[cells.Length];

        for(int i = 0; i < cells.Length; i++)
            hashes[i] = CalculateCellHash(cells[i]);

        SetHashes(hashes);
    }

    public void UpdateKeys(uint[] hashes) {
        IndexKey[] keys = new IndexKey[hashes.Length];

        for(int i = 0; i < hashes.Length; i++)
            keys[i] = new IndexKey {
                Index = i,
                Key = CalculateCellKey(hashes[i])
            };

        SetKeys(keys);
    }

    // sort keys for spatial lookup
    public void SortKeys() {
        IndexKey[] keys = GetKeys();

        Array.Sort(keys, (i, j) => i.Key.CompareTo(j.Key));
        SetKeys(keys);
    }

    public void CalculateStartIndex(IndexKey[] keys) {
        for(int i = 0; i < keys.Length; i++) {
            int key = keys[i].Key;
            int keyPrev = i == 0 ? int.MaxValue : keys[i-1].Key;

            if(key != keyPrev) SetStartIndex(key, i);
        }
    }

    public void InRadius(Vector3 point) {
        Vector3 cell = CalculateCell(point);
    }
}
