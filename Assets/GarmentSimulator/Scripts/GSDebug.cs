using UnityEngine;

namespace GarmentSimulator {

class GSDebug {

public static void DebugRenderVoxel(int voxelId, int voxelCount, Vector3 min, Vector3 max) {
    DebugRenderVoxel(voxelId, voxelCount, min, max, Color.blue);
}

public static void DebugRenderVoxel(int voxelId, int voxelCount, Vector3 min, Vector3 max, Color color) {
    Vector3 m = new Vector3(
        voxelId % voxelCount,
        (voxelId / voxelCount) % voxelCount,
        voxelId / voxelCount / voxelCount
    );

    Vector3 M = m + Vector3.one;

    m /= voxelCount;
    M /= voxelCount;
    m.Scale(max - min);
    M.Scale(max - min);
    m += min;
    M += min;

    var A = new Vector3(m.x, m.y, m.z);
    var B = new Vector3(m.x, M.y, m.z);
    var C = new Vector3(M.x, M.y, m.z);
    var D = new Vector3(M.x, m.y, m.z);
    var E = new Vector3(m.x, m.y, M.z);
    var F = new Vector3(m.x, M.y, M.z);
    var G = new Vector3(M.x, M.y, M.z);
    var H = new Vector3(M.x, m.y, M.z);

    Debug.DrawLine(A, B, color);
    Debug.DrawLine(B, C, color);
    Debug.DrawLine(C, D, color);
    Debug.DrawLine(D, A, color);
    Debug.DrawLine(E, F, color);
    Debug.DrawLine(F, G, color);
    Debug.DrawLine(G, H, color);
    Debug.DrawLine(H, E, color);
    Debug.DrawLine(A, E, color);
    Debug.DrawLine(B, F, color);
    Debug.DrawLine(C, G, color);
    Debug.DrawLine(D, H, color);
}

}
    
}