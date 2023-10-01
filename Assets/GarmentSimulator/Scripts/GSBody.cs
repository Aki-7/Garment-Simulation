using UnityEngine;

namespace GarmentSimulator {

class GSBody : MonoBehaviour {
    private SkinnedMeshRenderer _renderer = null;

    private ComputeBuffer _bodyMeshIndexBuffer;             // int[_bodyTriangleCount * 3]
    private ComputeBuffer _bodyTriangleBuffer;              // BodyTriangle[_bodyTriangleCount]
    private ComputeBuffer _sortedBodyTriangleBuffer;        // int[<adaptive>]
    private ComputeBuffer _sortedBodyTriangleCountBuffer;   // int[1]

    public ComputeBuffer bodyMeshIndexBuffer            {get{ return _bodyMeshIndexBuffer; }}
    public ComputeBuffer bodyTriangleBuffer             {get{ return _bodyTriangleBuffer; }}
    public ComputeBuffer sortedBodyTriangleBuffer       {get{ return _sortedBodyTriangleBuffer; }}
    public ComputeBuffer sortedBodyTriangleCountBuffer  {get{ return _sortedBodyTriangleCountBuffer; }}

    private int _vertexCount;
    private int _triangleCount;

    public int vertexCount      {get{ return _vertexCount; }}
    public int triangleCount    {get{ return _triangleCount; }}

    private bool _ready = false;

    public bool ready {get{ return _ready; }}

    public void Init() {
        _renderer = GetComponent<SkinnedMeshRenderer>();
        Debug.Assert(_renderer);

        var bodyTriangles = _renderer.sharedMesh.triangles;
        _vertexCount = _renderer.sharedMesh.vertexCount;
        _triangleCount = bodyTriangles.Length / 3;

        _renderer.vertexBufferTarget = GraphicsBuffer.Target.Structured;

        SetupBuffers(bodyTriangles);

        Debug.LogFormat("Body: vertices: {0}, triangles: {1}", _vertexCount, _triangleCount);

        _ready = true;
    }

    void OnDestroy() {
        if (!ready) return;

        bodyMeshIndexBuffer.Dispose();
        bodyTriangleBuffer.Dispose();
        sortedBodyTriangleBuffer.Dispose();
        sortedBodyTriangleCountBuffer.Dispose();
    }

    public GraphicsBuffer GetSkinnedVertexBuffer() {
        return _renderer.GetVertexBuffer();
    }

    public void AdaptSortedBodyTriangleBufferSize() {
        var targetSize = new int[1];
        _sortedBodyTriangleCountBuffer.GetData(targetSize);

        bool changed = false;
        int size = _sortedBodyTriangleBuffer.count;
        while(size < targetSize[0]) {
            changed = true;
            size *= 2;
        }

        if (changed) {
            Debug.LogFormat("Increase _bodyTriangleSortedByVoxelsBuffer size to {0}", size);
            _sortedBodyTriangleBuffer.Dispose();
            _sortedBodyTriangleBuffer = new ComputeBuffer(size, sizeof(int));
        }
    }

    public Matrix4x4 GetTransformationMatrix() {
        var root = _renderer.rootBone.transform;
        return Matrix4x4.TRS(root.position, root.rotation, Vector3.one);
    }

    private void SetupBuffers(int[] triangles) {
        Debug.Assert(_vertexCount != 0);
        Debug.Assert(_triangleCount != 0);

        _bodyMeshIndexBuffer = new ComputeBuffer(_triangleCount * 3, sizeof(int));
        _bodyMeshIndexBuffer.SetData(triangles);

        _bodyTriangleBuffer = new ComputeBuffer(_triangleCount, GSSolver.BodyTriangle.size);

        _sortedBodyTriangleBuffer = new ComputeBuffer(1, sizeof(int));

        _sortedBodyTriangleCountBuffer = new ComputeBuffer(1, sizeof(int));
    }
}
    
}