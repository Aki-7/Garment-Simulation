using System.Collections.Generic;
using UnityEngine;

namespace GarmentSimulator {

class GSSolver {
    public struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 tangent;
        public Vector3 velocity;
        public static int size = sizeof(float) * 3 * 4;
    }

    public struct Edge
    {
        public int startIndex;
        public int endIndex;
        public float length;
        public static int size = sizeof(int) * 2 + sizeof(float);

        public Edge(int indexA, int indexB, float length) {
            startIndex = Mathf.Min(indexA, indexB);
            endIndex = Mathf.Max(indexA, indexB);
            this.length = length;
        }

        public class IndexComparer : EqualityComparer<Edge>
        {
            public override int GetHashCode(Edge edge)
            {
                return edge.startIndex * 10000 + edge.endIndex;
            }

            public override bool Equals(Edge x, Edge y)
            {
                return x.startIndex == y.startIndex && x.endIndex == y.endIndex;
            }
        }
    }

    public struct BlendShapeVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 tangent;
        public static int size = sizeof(float) * 3 * 3;
    }

    public struct NextPositionVertex
    {
        public Vector3 position;
        public int voxelId; // xIndex + (yIndex + zIndex * voxelCount) * _voxelCount
        public static int size = sizeof(float) * 3 + sizeof(int);
    }

    public struct Voxel {
        public int bodyTriangleCount;
        public int index;
        public int endIndex;
        public int id;
        public static int size = sizeof(int) * 4;
    };

    public struct Int3 {
        public int x;
        public int y;
        public int z;
    }

    public struct BodyTriangle {
        public Vector3 pos0;
        public Vector3 pos1;
        public Vector3 pos2;
        public Vector3 normal;
        public Int3 minVoxel;
        public Int3 maxVoxel;
        public static int size = sizeof(float) * 3 * 4 + sizeof(int) * 3 * 2;
    };

    public struct SkinnedVertex {
	    public Vector3 position;
	    public Vector3 normal;
	    public Vector4 tangent;
    };

    public static ComputeShader simulator_CS;
    public static int applyExternalForces_Kernel;
    public static int dumpVelocities_Kernel;
    public static int computeNextPositions_Kernel;
    public static int accumulateDistanceConstraint_Kernel;
    public static int updatePositions_Kernel;
    public static int projectAccumulatedConstraint_Kernel;
    public static int analyzeVoxelsPass1_Kernel;
    public static int analyzeVoxelsPass2_Kernel;
    public static int analyzeBodyMeshPass1_Kernel;
    public static int analyzeBodyMeshPass2_Kernel;
    public static int sortBodyTriangle_Kernel;
    public static int bodyGarmentCollision_Kernel;

    public static ComputeShader garment_CS;
    public static int calculateRestShape_Kernel;
    public static int calculateRestEdge_Kernel;
    public static int calculateMinMaxPass1_Kernel;
    public static int calculateMinMaxPass2_Kernel;
    public static int forceSkinnedMesh_Kernel;

    public static int vertexBuffer_ID                   = Shader.PropertyToID("_vertexBuffer");
    public static int nextPositionBuffer_ID             = Shader.PropertyToID("_nextPositionBuffer");
    public static int restShapeEdgeBuffer_ID            = Shader.PropertyToID("_restShapeEdgeBuffer");
    public static int restShapeVertexBuffer_ID          = Shader.PropertyToID("_restShapeVertexBuffer");
    public static int deltaCountBuffer_ID               = Shader.PropertyToID("_deltaCountBuffer");
    public static int deltaUint3Buffer_ID               = Shader.PropertyToID("_deltaUint3Buffer");
    public static int blendShapeBaseBuffer_ID           = Shader.PropertyToID("_blendShapeBaseBuffer");
    public static int blendShapeDeltaBuffer_ID          = Shader.PropertyToID("_blendShapeDeltaBuffer");
    public static int blendShapeWeightBuffer_ID         = Shader.PropertyToID("_blendShapeWeightBuffer");
    public static int skinnedVertexBuffer_ID            = Shader.PropertyToID("_skinnedVertexBuffer");
    public static int minMaxPositionUint3Buffer_ID      = Shader.PropertyToID("_minMaxPositionUint3Buffer");
    public static int minMaxPositionBuffer_ID           = Shader.PropertyToID("_minMaxPositionBuffer");
    public static int activeVoxelBuffer_ID              = Shader.PropertyToID("_activeVoxelBuffer");
    public static int activeVoxelCountBuffer_ID         = Shader.PropertyToID("_activeVoxelCountBuffer");
    public static int voxelIdToActiveVoxelBuffer_ID     = Shader.PropertyToID("_voxelIdToActiveVoxelBuffer");
    public static int bodyMeshIndexBuffer_ID            = Shader.PropertyToID("_bodyMeshIndexBuffer");
    public static int bodyTriangleBuffer_ID             = Shader.PropertyToID("_bodyTriangleBuffer");
    public static int skinnedBodyVertexBuffer_ID        = Shader.PropertyToID("_skinnedBodyVertexBuffer");
    public static int sortedBodyTriangleBuffer_ID       = Shader.PropertyToID("_sortedBodyTriangleBuffer");
    public static int sortedBodyTriangleCountBuffer_ID  = Shader.PropertyToID("_sortedBodyTriangleCountBuffer");

    public static int dt_ID                     = Shader.PropertyToID("_dt");
    public static int gravity_ID                = Shader.PropertyToID("_gravity");
    public static int collisionRadius_ID        = Shader.PropertyToID("_collisionRadius");
    public static int epsilon_ID                = Shader.PropertyToID("_epsilon");
    public static int velocityClamp_ID          = Shader.PropertyToID("_velocityClamp");
    public static int vertexCount_ID            = Shader.PropertyToID("_vertexCount");
    public static int edgeCount_ID              = Shader.PropertyToID("_edgeCount");
    public static int blendShapeCount_ID        = Shader.PropertyToID("_blendShapeCount");
    public static int voxelCount_ID             = Shader.PropertyToID("_voxelCount");
    public static int voxelCount3_ID            = Shader.PropertyToID("_voxelCount3");
    public static int bodyTriangleCount_ID      = Shader.PropertyToID("_bodyTriangleCount");
    public static int bodyTransformMatrix_ID    = Shader.PropertyToID("_bodyTransformMatrix");
    public static int skinnedTransformMatrix_ID = Shader.PropertyToID("_skinnedTransformMatrix");
    public static int skinnedRotationMatrix_ID  = Shader.PropertyToID("_skinnedRotationMatrix");

    private const int _threadCount = 256; 

    public static void LoadShaders() {
        simulator_CS = Resources.Load("Shaders/GSSimulator") as ComputeShader;
        applyExternalForces_Kernel          = simulator_CS.FindKernel("ApplyExternalForces");
        dumpVelocities_Kernel               = simulator_CS.FindKernel("DumpVelocities");
        computeNextPositions_Kernel         = simulator_CS.FindKernel("ComputeNextPositions");
        accumulateDistanceConstraint_Kernel = simulator_CS.FindKernel("AccumulateDistanceConstraint");
        updatePositions_Kernel              = simulator_CS.FindKernel("UpdatePositions");
        projectAccumulatedConstraint_Kernel = simulator_CS.FindKernel("ProjectAccumulatedConstraint");
        analyzeVoxelsPass1_Kernel           = simulator_CS.FindKernel("AnalyzeVoxelsPass1");
        analyzeVoxelsPass2_Kernel           = simulator_CS.FindKernel("AnalyzeVoxelsPass2");
        analyzeBodyMeshPass1_Kernel         = simulator_CS.FindKernel("AnalyzeBodyMeshPass1");
        analyzeBodyMeshPass2_Kernel         = simulator_CS.FindKernel("AnalyzeBodyMeshPass2");
        sortBodyTriangle_Kernel             = simulator_CS.FindKernel("SortBodyTriangle");
        bodyGarmentCollision_Kernel         = simulator_CS.FindKernel("BodyGarmentCollision");

        garment_CS = Resources.Load("Shaders/GSGarment") as ComputeShader;
        calculateRestShape_Kernel   = garment_CS.FindKernel("CalculateRestShape");
        calculateRestEdge_Kernel    = garment_CS.FindKernel("CalculateRestEdge");
        calculateMinMaxPass1_Kernel = garment_CS.FindKernel("CalculateMinMaxPass1");
        calculateMinMaxPass2_Kernel = garment_CS.FindKernel("CalculateMinMaxPass2");
        forceSkinnedMesh_Kernel     = garment_CS.FindKernel("ForceSkinnedMesh");
    }

    public static void Dispatch1D(ComputeShader shader, int kernel, int count) {
        shader.Dispatch(kernel, CalcGroupSize(count), 1, 1);
    }

    private static int CalcGroupSize(int itemCount)
    {
        return (itemCount + _threadCount - 1) / _threadCount;
    }

}

}