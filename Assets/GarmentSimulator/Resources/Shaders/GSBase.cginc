#define THREAD_COUNT 256

struct Vertex {
	float3 position;
	float3 normal;
	float3 tangent;
	float3 velocity;
};

struct SkinnedVertex {
	float3 position;
	float3 normal;
	float4 tangent;
};

struct BlendShapeVertex {
	float3 position;
	float3 normal;
	float3 tangent;
};

struct Edge {
	int startIndex;
	int endIndex;
	float length;
};

struct NextPositionVertex {
	float3 position;
	int voxelId;
};

struct Voxel {
	int bodyTriangleCount;
	int index;
	int endIndex;
	int id;
};

struct BodyTriangle {
	float3 pos0;
	float3 pos1;
	float3 pos2;
	float3 normal;
	int3 minVoxel;
	int3 maxVoxel;
};

RWStructuredBuffer<BlendShapeVertex>	_blendShapeBaseBuffer;
RWStructuredBuffer<BlendShapeVertex>	_blendShapeDeltaBuffer;
RWStructuredBuffer<float>				_blendShapeWeightBuffer;
StructuredBuffer<SkinnedVertex>			_skinnedVertexBuffer;
RWStructuredBuffer<Vertex>				_restShapeVertexBuffer;
RWStructuredBuffer<Edge>				_restShapeEdgeBuffer;
RWStructuredBuffer<Vertex>				_vertexBuffer;
RWStructuredBuffer<NextPositionVertex>	_nextPositionBuffer;
RWStructuredBuffer<int>					_deltaCountBuffer;
RWStructuredBuffer<uint3>				_deltaUint3Buffer;
RWStructuredBuffer<uint3>				_minMaxPositionUint3Buffer;
RWStructuredBuffer<float3>				_minMaxPositionBuffer;
RWStructuredBuffer<Voxel>				_activeVoxelBuffer;
RWStructuredBuffer<int>					_activeVoxelCountBuffer;
RWStructuredBuffer<int>					_voxelIdToActiveVoxelBuffer;
RWStructuredBuffer<int>					_bodyMeshIndexBuffer;
RWStructuredBuffer<BodyTriangle>		_bodyTriangleBuffer;
StructuredBuffer<SkinnedVertex>			_skinnedBodyVertexBuffer;
RWStructuredBuffer<int>					_sortedBodyTriangleBuffer;
RWStructuredBuffer<int>					_sortedBodyTriangleCountBuffer;

float		_dt;
float3		_gravity;
float		_collisionFrontRadius;
float		_collisionBackRadius;
float 		_epsilon;
float 		_velocityClamp;
uint		_vertexCount;
uint		_edgeCount;
uint		_blendShapeCount;
uint		_voxelCount; // per one dimension
uint		_voxelCount3; // _voxelCount^3
uint		_bodyTriangleCount;
float4x4 	_bodyTransformMatrix;
float4x4 	_skinnedTransformMatrix;
float4x4 	_skinnedRotationMatrix;

#define FLT_MAX 3.402823466e+38F
#define COMPRESSION_STIFFNESS 1.0
#define STRETCH_STIFFNESS 2.0
#define VELOCITY_DAMPING 0.999 // must not be zero
#define INITIALIZED -1
#define IN_USE -2
#define TRUE 1
#define FALSE 0

int3 PositionToVoxel(float3 position)
{
	float3 minPosition = _minMaxPositionBuffer[0];
	float3 maxPosition = _minMaxPositionBuffer[1] + float3(_epsilon, _epsilon, _epsilon);
	float3 relativePosition = (position - minPosition) / (maxPosition - minPosition);
 	return floor(relativePosition * _voxelCount);
}

int PositionToVoxelId(float3 position) {
	int3 voxel = PositionToVoxel(position);
	return voxel.x + (voxel.y + voxel.z * _voxelCount) * _voxelCount;
}
