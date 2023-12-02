using Unity.VisualScripting;
using UnityEngine;

namespace GarmentSimulator
{

// Simulation will be done in a garment local coordinate
class GSSimulator : MonoBehaviour {
    public GSGarment _garment;

    public GSBody _body;

    [Range(0, 1)]
    public float _skinningLimitFactor = 0.0001f;
    
    private ComputeShader _computeShader;

    private ComputeBuffer _activeVoxelBuffer;           // Voxel[_vertexCount]
    private ComputeBuffer _activeVoxelCountBuffer;      // int[1]
    private ComputeBuffer _voxelIdToActiveVoxelBuffer;  // int[_voxelCount^3], Stores -1 if it's not active

    private const int   _voxelCount = 64; // per one dimension, up to 1024
    private const int   _voxelCount3 = _voxelCount * _voxelCount * _voxelCount;
    private const float _epsilon = 1e-6F;

    /* Simulation properties */
    public Vector3 _gravity = new Vector3(0F, -9.81F, 0F);
    public float _collisionFrontRadius = 0.01F;
    public float _collisionBackRadius = 0.02F;
    public float _velocityClamp = 10F;
    public int _subSteps = 3;
    public int _iterationCount = 25;

    void Start() {
        Debug.Assert(_garment);
        Debug.Assert(_body);

        GSSolver.LoadShaders();

        _computeShader = GSSolver.simulator_CS;

        _garment.Init();
        _body.Init();

        SetupBuffers();

        SetupComputeShader();
    }

    void FixedUpdate() {
        Debug.Assert(_garment);
        Debug.Assert(_computeShader);

        if (!_garment.ready) return;

        UpdateSimulationProperties();

        float dt = Time.deltaTime / _subSteps;
        for (int i = 0; i < _subSteps; i ++){
            IterateSimulation(dt);
        }
    }

    void OnDestroy() {
        _activeVoxelBuffer.Dispose();
        _activeVoxelCountBuffer.Dispose();
        _voxelIdToActiveVoxelBuffer.Dispose();
    }

    public void UpdateSimulationProperties() {
        _computeShader.SetVector(GSSolver.gravity_ID, GarmentLocalGravity());
        _computeShader.SetFloat(GSSolver.collisionFrontRadius_ID, GarmentLocalCollisionFrontRadius());
        _computeShader.SetFloat(GSSolver.collisionBackRadius_ID, GarmentLocalCollisionBackRadius());
        _computeShader.SetFloat(GSSolver.velocityClamp_ID, GarmentLocalVelocityClamp());
    }

    private void IterateSimulation(float dt) {
        _computeShader.SetFloat(GSSolver.dt_ID, dt);
        GSSolver.Dispatch1D(_computeShader, GSSolver.applyExternalForces_Kernel, _garment.vertexCount);
        GSSolver.Dispatch1D(_computeShader, GSSolver.dumpVelocities_Kernel, _garment.vertexCount);
        GSSolver.Dispatch1D(_computeShader, GSSolver.computeNextPositions_Kernel, _garment.vertexCount);
        _garment.ApplySkinningLimit(_skinningLimitFactor);

        for (int i = 0; i < _iterationCount; i++) {
            GSSolver.Dispatch1D(_computeShader, GSSolver.accumulateDistanceConstraint_Kernel, _garment.edgeCount);
            GSSolver.Dispatch1D(_computeShader, GSSolver.projectAccumulatedConstraint_Kernel, _garment.vertexCount);

            if (i == 0) SimulateClothBodyCollision();
        }

        GSSolver.Dispatch1D(_computeShader, GSSolver.updatePositions_Kernel, _garment.vertexCount);
    }

    private void SimulateClothBodyCollision() {
        var skinnedBodyVertexBuffer = _body.GetSkinnedVertexBuffer();
        if (skinnedBodyVertexBuffer == null) return;

        _garment.RecalculateMinMax();

        _activeVoxelCountBuffer.SetData(new int[1]{0});
        GSSolver.Dispatch1D(_computeShader, GSSolver.analyzeVoxelsPass1_Kernel, _voxelCount3);
        GSSolver.Dispatch1D(_computeShader, GSSolver.analyzeVoxelsPass2_Kernel, _garment.vertexCount);

        var garmentTransformation = _garment.gameObject.transform.localToWorldMatrix;
        var bodyTransformation = _body.GetTransformationMatrix();

        _computeShader.SetMatrix(GSSolver.bodyTransformMatrix_ID, garmentTransformation.inverse * bodyTransformation);
        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass1_Kernel, GSSolver.skinnedBodyVertexBuffer_ID, skinnedBodyVertexBuffer);

        GSSolver.Dispatch1D(_computeShader, GSSolver.analyzeBodyMeshPass1_Kernel, _body.triangleCount);
        GSSolver.Dispatch1D(_computeShader, GSSolver.analyzeBodyMeshPass2_Kernel, 1);

        _body.AdaptSortedBodyTriangleBufferSize();
        _computeShader.SetBuffer(GSSolver.sortBodyTriangle_Kernel, GSSolver.sortedBodyTriangleBuffer_ID, _body.sortedBodyTriangleBuffer);
        _computeShader.SetBuffer(GSSolver.bodyGarmentCollision_Kernel, GSSolver.sortedBodyTriangleBuffer_ID, _body.sortedBodyTriangleBuffer);

        GSSolver.Dispatch1D(_computeShader, GSSolver.sortBodyTriangle_Kernel, _body.triangleCount);
        GSSolver.Dispatch1D(_computeShader, GSSolver.bodyGarmentCollision_Kernel, _garment.vertexCount);

        skinnedBodyVertexBuffer.Dispose();
    }

    private void SetupBuffers() {
        _activeVoxelBuffer = new ComputeBuffer(_garment.vertexCount, GSSolver.Voxel.size);

        _activeVoxelCountBuffer = new ComputeBuffer(1, sizeof(int));

        _voxelIdToActiveVoxelBuffer = new ComputeBuffer(_voxelCount3, sizeof(int));
    }

    private void SetupComputeShader() {
        _computeShader.SetBuffer(GSSolver.applyExternalForces_Kernel, GSSolver.vertexBuffer_ID, _garment.vertexBuffer);

        _computeShader.SetBuffer(GSSolver.dumpVelocities_Kernel, GSSolver.vertexBuffer_ID, _garment.vertexBuffer);

        _computeShader.SetBuffer(GSSolver.computeNextPositions_Kernel, GSSolver.vertexBuffer_ID, _garment.vertexBuffer);
        _computeShader.SetBuffer(GSSolver.computeNextPositions_Kernel, GSSolver.nextPositionBuffer_ID, _garment.nextPositionBuffer);

        _computeShader.SetBuffer(GSSolver.accumulateDistanceConstraint_Kernel, GSSolver.restShapeEdgeBuffer_ID, _garment.restShapeEdgeBuffer);
        _computeShader.SetBuffer(GSSolver.accumulateDistanceConstraint_Kernel, GSSolver.nextPositionBuffer_ID, _garment.nextPositionBuffer);
        _computeShader.SetBuffer(GSSolver.accumulateDistanceConstraint_Kernel, GSSolver.deltaCountBuffer_ID, _garment.deltaCountBuffer);
        _computeShader.SetBuffer(GSSolver.accumulateDistanceConstraint_Kernel, GSSolver.deltaUint3Buffer_ID, _garment.deltaUint3Buffer);

        _computeShader.SetBuffer(GSSolver.projectAccumulatedConstraint_Kernel, GSSolver.nextPositionBuffer_ID, _garment.nextPositionBuffer);
        _computeShader.SetBuffer(GSSolver.projectAccumulatedConstraint_Kernel, GSSolver.deltaCountBuffer_ID, _garment.deltaCountBuffer);
        _computeShader.SetBuffer(GSSolver.projectAccumulatedConstraint_Kernel, GSSolver.deltaUint3Buffer_ID, _garment.deltaUint3Buffer);

        _computeShader.SetBuffer(GSSolver.analyzeVoxelsPass1_Kernel, GSSolver.voxelIdToActiveVoxelBuffer_ID, _voxelIdToActiveVoxelBuffer);

        _computeShader.SetBuffer(GSSolver.analyzeVoxelsPass2_Kernel, GSSolver.minMaxPositionBuffer_ID, _garment.minMaxPositionBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeVoxelsPass2_Kernel, GSSolver.nextPositionBuffer_ID, _garment.nextPositionBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeVoxelsPass2_Kernel, GSSolver.voxelIdToActiveVoxelBuffer_ID, _voxelIdToActiveVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeVoxelsPass2_Kernel, GSSolver.activeVoxelBuffer_ID, _activeVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeVoxelsPass2_Kernel, GSSolver.activeVoxelCountBuffer_ID, _activeVoxelCountBuffer);

        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass1_Kernel, GSSolver.minMaxPositionBuffer_ID, _garment.minMaxPositionBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass1_Kernel, GSSolver.bodyMeshIndexBuffer_ID, _body.bodyMeshIndexBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass1_Kernel, GSSolver.voxelIdToActiveVoxelBuffer_ID, _voxelIdToActiveVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass1_Kernel, GSSolver.activeVoxelBuffer_ID, _activeVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass1_Kernel, GSSolver.bodyTriangleBuffer_ID, _body.bodyTriangleBuffer);

        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass2_Kernel, GSSolver.activeVoxelBuffer_ID, _activeVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass2_Kernel, GSSolver.activeVoxelCountBuffer_ID, _activeVoxelCountBuffer);
        _computeShader.SetBuffer(GSSolver.analyzeBodyMeshPass2_Kernel, GSSolver.sortedBodyTriangleCountBuffer_ID, _body.sortedBodyTriangleCountBuffer);

        _computeShader.SetBuffer(GSSolver.sortBodyTriangle_Kernel, GSSolver.bodyTriangleBuffer_ID, _body.bodyTriangleBuffer);
        _computeShader.SetBuffer(GSSolver.sortBodyTriangle_Kernel, GSSolver.voxelIdToActiveVoxelBuffer_ID, _voxelIdToActiveVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.sortBodyTriangle_Kernel, GSSolver.activeVoxelBuffer_ID, _activeVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.sortBodyTriangle_Kernel, GSSolver.sortedBodyTriangleBuffer_ID, _body.sortedBodyTriangleBuffer);

        _computeShader.SetBuffer(GSSolver.bodyGarmentCollision_Kernel, GSSolver.nextPositionBuffer_ID, _garment.nextPositionBuffer);
        _computeShader.SetBuffer(GSSolver.bodyGarmentCollision_Kernel, GSSolver.voxelIdToActiveVoxelBuffer_ID, _voxelIdToActiveVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.bodyGarmentCollision_Kernel, GSSolver.activeVoxelBuffer_ID, _activeVoxelBuffer);
        _computeShader.SetBuffer(GSSolver.bodyGarmentCollision_Kernel, GSSolver.sortedBodyTriangleBuffer_ID, _body.sortedBodyTriangleBuffer);
        _computeShader.SetBuffer(GSSolver.bodyGarmentCollision_Kernel, GSSolver.bodyTriangleBuffer_ID, _body.bodyTriangleBuffer);

        _computeShader.SetBuffer(GSSolver.updatePositions_Kernel, GSSolver.vertexBuffer_ID, _garment.vertexBuffer);
        _computeShader.SetBuffer(GSSolver.updatePositions_Kernel, GSSolver.nextPositionBuffer_ID, _garment.nextPositionBuffer);

        _computeShader.SetInt(GSSolver.vertexCount_ID, _garment.vertexCount);
        _computeShader.SetInt(GSSolver.edgeCount_ID, _garment.edgeCount);
        _computeShader.SetInt(GSSolver.voxelCount_ID, _voxelCount);
        _computeShader.SetInt(GSSolver.voxelCount3_ID, _voxelCount3);
        _computeShader.SetFloat(GSSolver.epsilon_ID, GarmentLocalEpsilon());
        _computeShader.SetInt(GSSolver.bodyTriangleCount_ID, _body.triangleCount);
        UpdateSimulationProperties();
    }

    private Vector3 GarmentLocalGravity() {
        return _garment.transform.worldToLocalMatrix.MultiplyVector(_gravity);
    }

    private float GarmentLocalCollisionFrontRadius() {
        var scale = _garment.transform.lossyScale;
        return _collisionFrontRadius / ((scale.x + scale.y + scale.z) / 3);
    }

    private float GarmentLocalCollisionBackRadius() {
        var scale = _garment.transform.lossyScale;
        return _collisionBackRadius / ((scale.x + scale.y + scale.z) / 3);
    }

    private float GarmentLocalEpsilon() {
        var scale = _garment.transform.lossyScale;
        return _epsilon / ((scale.x + scale.y + scale.z) / 3);
    }

    private float GarmentLocalVelocityClamp() {
        var scale = _garment.transform.lossyScale;
        return _velocityClamp / ((scale.x + scale.y + scale.z) / 3);
    }
}
    
}