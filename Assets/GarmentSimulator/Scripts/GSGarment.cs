using System;
using System.Collections.Generic;
using UnityEngine;

namespace GarmentSimulator {

public class GSGarment : MonoBehaviour {

    private ComputeShader       _computeShader;
    private MeshRenderer        _renderer = null;
    private SkinnedMeshRenderer _skin = null;
    private MeshFilter          _meshFilter = null;
    private Mesh                _mesh = null;
    private int                 _vertexCount = 0;
    private int                 _triangleCount = 0;
    private int                 _edgeCount = 0;
    private int                 _blendShapeCount = 0;
    private float[]             _blendShapeWeights = null;
    private string[]            _blendShapeNames = null;
    private bool                _ready = false;
    private DateTime            _resetPositionRequestTime = DateTime.Now;

    public int      vertexCount         {get{ return _vertexCount;}}
    public int      edgeCount           {get{ return _edgeCount;}}
    public string[] blendShapeNames     {get{ return _blendShapeNames;}}
    public bool     ready               {get{ return _ready; }}

    private ComputeBuffer _vertexBuffer;            // Vertex[_vertexCount]
    private ComputeBuffer _blendShapeBaseBuffer;    // BlendShapeVertex[_vertexCount]
    private ComputeBuffer _blendShapeDeltaBuffer;   // BlendShapeVertex[_blendShapeCount * _vertexCount]
    private ComputeBuffer _blendShapeWeightBuffer;  // float[_blendShapeCount]
    private ComputeBuffer _restShapeVertexBuffer;   // Vertex[_vertexCount]
    private ComputeBuffer _restShapeEdgeBuffer;     // Edge[_edgeCount]
    private ComputeBuffer _nextPositionBuffer;      // NextPositionVertex[_vertexCount]
    private ComputeBuffer _deltaCountBuffer;        // int[_vertexCount]
    private ComputeBuffer _deltaUint3Buffer;        // uint3[_vertexCount]
    private ComputeBuffer _minMaxPositionBuffer;    // Vector3[2]

    public ComputeBuffer vertexBuffer           {get { return _vertexBuffer; }}
    public ComputeBuffer restShapeEdgeBuffer    {get { return _restShapeEdgeBuffer; }}
    public ComputeBuffer nextPositionBuffer     {get { return _nextPositionBuffer; }}
    public ComputeBuffer deltaCountBuffer       {get { return _deltaCountBuffer; }}
    public ComputeBuffer deltaUint3Buffer       {get { return _deltaUint3Buffer; }}
    public ComputeBuffer minMaxPositionBuffer   {get { return _minMaxPositionBuffer; }}

    private int _vertexBuffer_ID = Shader.PropertyToID("_vertexBuffer");

    public void Init() {
        GSSolver.Vertex[] vertices;
        GSSolver.Edge[] edges;
        GSSolver.BlendShapeVertex[] blendShapeBase;
        GSSolver.BlendShapeVertex[] blendShapeDeltas;

        SetupRenderer();

        Debug.Assert(_meshFilter);
        _mesh = _meshFilter.sharedMesh;

        CleanMesh(_mesh);

        SetupMeshInfo(out vertices, out edges);

        SetupBlendShapes(out blendShapeBase, out blendShapeDeltas);

        SetupBuffers(vertices, edges, blendShapeBase, blendShapeDeltas);

        SetupMaterial();

        SetupComputeShader();

        RecalculateRestShape();

        Debug.LogFormat("Garment Info - {0} blend shapes, {1} vertices, {2} edges, {3} triangles",
            _blendShapeCount, _vertexCount, _edgeCount, _triangleCount);
        
        _ready = true;
    }

    void OnDestroy() {
        if (!_ready) return;

        _blendShapeBaseBuffer.Dispose();
        _blendShapeDeltaBuffer.Dispose();
        _blendShapeWeightBuffer.Dispose();
        _restShapeVertexBuffer.Dispose();
        _restShapeEdgeBuffer.Dispose();
        _vertexBuffer.Dispose();
        _nextPositionBuffer.Dispose();
        _deltaCountBuffer.Dispose();
        _deltaUint3Buffer.Dispose();
        _minMaxPositionBuffer.Dispose();
    }

    void Update() {
        ResetPositionIfNeeded();
    }

    public void RecalculateMinMax() {
        GSSolver.Dispatch1D(_computeShader, GSSolver.calculateMinMaxPass1_Kernel, 3);
        GSSolver.Dispatch1D(_computeShader, GSSolver.calculateMinMaxPass2_Kernel, _vertexCount);
    }

    public float GetBlendShapeWeight(int index) {
        return _blendShapeWeights[index];
    }

    public void SetBlendShapeWeight(int index, float value) {
        _blendShapeWeights[index] = value;
        _blendShapeWeightBuffer.SetData(_blendShapeWeights);
        RecalculateRestShape();
    }

    public void RequestResetPosition() {
        if (!_ready) return;
        _resetPositionRequestTime = DateTime.Now;
        for (int i = 0; i < _blendShapeCount; i++) {
            _skin.SetBlendShapeWeight(i, _blendShapeWeights[i]);
        }
        _skin.enabled = true;
    }

    private void ResetPositionIfNeeded() {
        if (!_ready) return;
        if (!_skin.enabled) return;
        if (DateTime.Now - _resetPositionRequestTime < TimeSpan.FromMilliseconds(500)) return;

        var skinnedMesh = _skin.GetVertexBuffer();
        if (skinnedMesh == null) return;

        _skin.enabled = false;

        var rootBone = _skin.rootBone.transform;
        var rootBoneTransformMatrix = Matrix4x4.TRS(rootBone.position, rootBone.rotation, Vector3.one);
        var transformMatrix = transform.worldToLocalMatrix * rootBoneTransformMatrix;
        var rotateMatrix = Matrix4x4.TRS(Vector3.zero, transformMatrix.rotation, Vector3.one);

        _computeShader.SetMatrix(GSSolver.skinnedTransformMatrix_ID, transformMatrix);
        _computeShader.SetMatrix(GSSolver.skinnedRotationMatrix_ID, rotateMatrix);
        _computeShader.SetBuffer(GSSolver.forceSkinnedMesh_Kernel, GSSolver.skinnedVertexBuffer_ID, skinnedMesh);
        GSSolver.Dispatch1D(_computeShader, GSSolver.forceSkinnedMesh_Kernel, _vertexCount);

        skinnedMesh.Dispose();
    }

    private void SetupRenderer() {
        _skin = GetComponent<SkinnedMeshRenderer>();
        Debug.Assert(_skin);

        _renderer = gameObject.AddComponent<MeshRenderer>();
        _meshFilter = gameObject.AddComponent<MeshFilter>();
        _meshFilter.sharedMesh = _skin.sharedMesh;
        _renderer.materials = _skin.materials;
        _skin.enabled = false;
    }

    static private void CleanMesh(Mesh mesh) {
        var triangles = new int[mesh.triangles.Length];
        mesh.triangles.CopyTo(triangles, 0);

        var duplicatedVertexDictionary = new Dictionary<Vector3, List<int>>();
        
        // remove duplicated vertices
        for (var i = 0; i < mesh.vertices.Length; i++) {
            var vertex = mesh.vertices[i];

            bool found = false;
            foreach (var (v, list) in duplicatedVertexDictionary) {
                if ((v - vertex).sqrMagnitude <= 1e-10f) {
                    found = true;
                    list.Add(i);
                }
            }

            if (!found){
                duplicatedVertexDictionary.Add(vertex, new List<int>{i});
            }
        }

        foreach (var (_, duplicatedVertices) in duplicatedVertexDictionary) {
            duplicatedVertices.Sort();

            for (int i = 0; i < triangles.Length; i++) {
                if (duplicatedVertices.Contains(triangles[i])) {
                    triangles[i] = duplicatedVertices[0];
                }
            }
        }

        mesh.triangles = triangles;
    }

    private void SetupMeshInfo(out GSSolver.Vertex[] vertices, out GSSolver.Edge[] edges) {
        Debug.Assert(_mesh);

        _vertexCount = _mesh.vertexCount;

        vertices = new GSSolver.Vertex[_vertexCount];
        for (int i = 0; i < _vertexCount; i++) {
            vertices[i].position = _mesh.vertices[i];
            vertices[i].normal = _mesh.normals[i];
            vertices[i].tangent = _mesh.tangents[i];
            vertices[i].velocity = Vector3.zero;
        }

        var triangles = _mesh.triangles;
        var edgeSet = new HashSet<GSSolver.Edge>(new GSSolver.Edge.IndexComparer());
        for (int i = 0; i < triangles.Length / 3; i++) {
            int A = triangles[3 * i];
            int B = triangles[3 * i + 1];
            int C = triangles[3 * i + 2];

            edgeSet.Add(new GSSolver.Edge(A, B, 0));
            edgeSet.Add(new GSSolver.Edge(B, C, 0));
            edgeSet.Add(new GSSolver.Edge(C, A, 0));
        }

        _edgeCount = edgeSet.Count;
        edges = new GSSolver.Edge[_edgeCount];
        edgeSet.CopyTo(edges);

        _triangleCount = triangles.Length / 3;
    }

    private void SetupBlendShapes(out GSSolver.BlendShapeVertex[] blendShapeBase, out GSSolver.BlendShapeVertex[] blendShapeDeltas) {
        Debug.Assert(_mesh);
        Debug.Assert(_vertexCount != 0);

        _blendShapeCount = _mesh.blendShapeCount;
        _blendShapeWeights = new float[_blendShapeCount];
        _blendShapeNames = new string[_blendShapeCount];

        blendShapeBase = new GSSolver.BlendShapeVertex[_vertexCount];
        for (int i = 0; i < _vertexCount; i++) {
            blendShapeBase[i].position = _mesh.vertices[i];
            blendShapeBase[i].normal = _mesh.normals[i];
            blendShapeBase[i].tangent = _mesh.tangents[i];
        }

        blendShapeDeltas = new GSSolver.BlendShapeVertex[_vertexCount * _blendShapeCount];
        for (int i = 0; i < _blendShapeCount; i++) {
            Vector3[] blendShapePositionDeltas = new Vector3[_vertexCount];
            Vector3[] blendShapeNormalDeltas = new Vector3[_vertexCount];
            Vector3[] blendShapeTangentDeltas = new Vector3[_vertexCount];

            _mesh.GetBlendShapeFrameVertices(i, 0, blendShapePositionDeltas, blendShapeNormalDeltas, blendShapeTangentDeltas);

            for (int j = 0; j < _vertexCount; j ++)
            {
                int index = j * _blendShapeCount + i;
                blendShapeDeltas[index].position = blendShapePositionDeltas[j];
                blendShapeDeltas[index].normal = blendShapeNormalDeltas[j];
                blendShapeDeltas[index].tangent = blendShapeTangentDeltas[j];
            }

            _blendShapeNames[i] = _mesh.GetBlendShapeName(i);
        }
    }

    private void SetupBuffers(GSSolver.Vertex[] vertices, GSSolver.Edge[] edges, GSSolver.BlendShapeVertex[] blendShapeBase, GSSolver.BlendShapeVertex[] blendShapeDeltas) {
        Debug.Assert(_vertexCount != 0);
        Debug.Assert(_edgeCount != 0);
        Debug.Assert(_blendShapeCount != 0);

        _vertexBuffer = new ComputeBuffer(_vertexCount, GSSolver.Vertex.size);
        _vertexBuffer.SetData(vertices);

        _blendShapeBaseBuffer = new ComputeBuffer(_vertexCount, GSSolver.BlendShapeVertex.size);
        _blendShapeBaseBuffer.SetData(blendShapeBase);

        _blendShapeDeltaBuffer = new ComputeBuffer(_blendShapeCount * _vertexCount, GSSolver.BlendShapeVertex.size);
        _blendShapeDeltaBuffer.SetData(blendShapeDeltas);

        _blendShapeWeightBuffer = new ComputeBuffer(_blendShapeCount, sizeof(float));
        _blendShapeWeightBuffer.SetData(_blendShapeWeights);

        _restShapeVertexBuffer = new ComputeBuffer(_vertexCount, GSSolver.Vertex.size);

        _restShapeEdgeBuffer = new ComputeBuffer(_edgeCount, GSSolver.Edge.size);
        _restShapeEdgeBuffer.SetData(edges);

        _nextPositionBuffer = new ComputeBuffer(_vertexCount, GSSolver.NextPositionVertex.size);

        _deltaCountBuffer = new ComputeBuffer(_vertexCount, sizeof(int));
        _deltaCountBuffer.SetData(new int[_vertexCount]);

        _deltaUint3Buffer = new ComputeBuffer(_vertexCount, sizeof(uint) * 3);
        _deltaUint3Buffer.SetData(new uint[_vertexCount * 3]);

        _minMaxPositionBuffer = new ComputeBuffer(2, sizeof(float) * 3);
    }

    private void SetupMaterial() {
        Debug.Assert(_renderer);
        Debug.Assert(_vertexBuffer != null);

        var mpb = new MaterialPropertyBlock();
        mpb.SetBuffer(_vertexBuffer_ID, _vertexBuffer);
        _renderer.SetPropertyBlock(mpb);

        var shader = Resources.Load("Shaders/GSGarment", typeof(Shader)) as Shader;

        foreach (var material in _renderer.materials) {
            material.shader = shader;
        }
    }

    private void SetupComputeShader() {
        _computeShader = GSSolver.garment_CS;

        _computeShader.SetBuffer(GSSolver.calculateRestShape_Kernel, GSSolver.restShapeVertexBuffer_ID, _restShapeVertexBuffer);
        _computeShader.SetBuffer(GSSolver.calculateRestShape_Kernel, GSSolver.blendShapeBaseBuffer_ID, _blendShapeBaseBuffer);
        _computeShader.SetBuffer(GSSolver.calculateRestShape_Kernel, GSSolver.blendShapeDeltaBuffer_ID, _blendShapeDeltaBuffer);
        _computeShader.SetBuffer(GSSolver.calculateRestShape_Kernel, GSSolver.blendShapeWeightBuffer_ID, _blendShapeWeightBuffer);

        _computeShader.SetBuffer(GSSolver.calculateRestEdge_Kernel, GSSolver.restShapeEdgeBuffer_ID, _restShapeEdgeBuffer);
        _computeShader.SetBuffer(GSSolver.calculateRestEdge_Kernel, GSSolver.restShapeVertexBuffer_ID, _restShapeVertexBuffer);

        _computeShader.SetBuffer(GSSolver.calculateMinMaxPass1_Kernel, GSSolver.minMaxPositionBuffer_ID, _minMaxPositionBuffer);

        _computeShader.SetBuffer(GSSolver.calculateMinMaxPass2_Kernel, GSSolver.minMaxPositionUint3Buffer_ID, _minMaxPositionBuffer);
        _computeShader.SetBuffer(GSSolver.calculateMinMaxPass2_Kernel, GSSolver.nextPositionBuffer_ID, _nextPositionBuffer);

        _computeShader.SetBuffer(GSSolver.forceSkinnedMesh_Kernel, GSSolver.vertexBuffer_ID, vertexBuffer);

        _computeShader.SetInt(GSSolver.vertexCount_ID, _vertexCount);
        _computeShader.SetInt(GSSolver.edgeCount_ID, _edgeCount);
        _computeShader.SetInt(GSSolver.blendShapeCount_ID, _blendShapeCount);
    }

    private void RecalculateRestShape() {
        GSSolver.Dispatch1D(_computeShader, GSSolver.calculateRestShape_Kernel, _vertexCount);
        GSSolver.Dispatch1D(_computeShader, GSSolver.calculateRestEdge_Kernel, _edgeCount);
    }
}

}