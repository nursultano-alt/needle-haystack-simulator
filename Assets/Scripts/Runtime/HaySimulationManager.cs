using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HaystackSimulator.Runtime
{
    [ExecuteAlways]
    public class HaySimulationManager : MonoBehaviour
    {
        public static HaySimulationManager Instance { get; private set; }

        [Header("Simulation Capacity")]
        [SerializeField] private int _maxStraws = 20000;
        [SerializeField] private int _initialStrawCount = 10000;

        [Header("Physical Properties")]
        [SerializeField] private Vector3 _gravity = new Vector3(0f, -9.81f, 0f);
        [Range(0.1f, 10f)] [SerializeField] private float _linearDrag = 2.2f;
        [Range(0.1f, 15f)] [SerializeField] private float _angularDrag = 3.5f;
        [Range(0f, 1f)] [SerializeField] private float _restitution = 0.25f;
        [Range(0f, 2f)] [SerializeField] private float _friction = 0.85f;
        [Range(0f, 2f)] [SerializeField] private float _tanglingFriction = 0.95f;

        [Header("Container Bounds (Barn Crate)")]
        [SerializeField] private Vector3 _boxMin = new Vector3(-1.9f, 0.08f, -1.9f);
        [SerializeField] private Vector3 _boxMax = new Vector3(1.9f, 2.5f, 1.9f);

        [Header("Rendering & Mesh")]
        [SerializeField] private Mesh _strawMesh;
        [SerializeField] private Material _strawMaterial;
        [SerializeField] private Mesh _needleMesh;
        [SerializeField] private Material _needleMaterial;
        [SerializeField] private ComputeShader _simulationShader;

        [Header("Golden Needle Settings")]
        [SerializeField] private bool _spawnNeedle = true;
        [SerializeField] private int _needleIndex = -1;
        [SerializeField] private bool _needleFound = false;

        // Structured buffer layout (matching HaySimulation.compute)
        [StructLayout(LayoutKind.Sequential)]
        public struct StrawData
        {
            public Vector3 position;
            public float radius;
            public Vector4 rotation; // Quaternion (x,y,z,w)
            public Vector3 velocity;
            public float length;
            public Vector3 angularVelocity;
            public float mass;
            public Vector4 color;
            public uint state; // 0 = free, 1 = grabbed, 2 = needle
            public Vector3 targetPos;
            public float padding;
        }

        // Buffers
        private ComputeBuffer _strawBuffer;
        private ComputeBuffer _transformBuffer;
        private ComputeBuffer _colorBuffer;
        private ComputeBuffer _argsBuffer;
        private uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };

        // Kernel IDs
        private int _kernelInit;
        private int _kernelEmit;
        private int _kernelIntegrate;
        private int _kernelSolveCollisions;
        private int _kernelSolveInteraction;
        private int _kernelBuildMatrices;

        // Simulation State
        private int _activeCount = 0;
        private StrawData[] _cpuStrawCache;
        private bool _isInitialized = false;

        // Events
        public event Action<int> OnStrawCountChanged;
        public event Action OnNeedleDiscovered;

        public int ActiveStrawCount => _activeCount;
        public int MaxStraws => _maxStraws;
        public bool IsNeedleFound => _needleFound;
        public int NeedleIndex => _needleIndex;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            InitializeSimulation();
        }

        private void OnDisable()
        {
            ReleaseBuffers();
        }

        public void InitializeSimulation()
        {
            if (_simulationShader == null || _strawMesh == null || _strawMaterial == null)
            {
                return;
            }

            ReleaseBuffers();

            _kernelInit = _simulationShader.FindKernel("CS_InitStraws");
            _kernelEmit = _simulationShader.FindKernel("CS_EmitStraw");
            _kernelIntegrate = _simulationShader.FindKernel("CS_Integrate");
            _kernelSolveCollisions = _simulationShader.FindKernel("CS_SolveCollisions");
            _kernelSolveInteraction = _simulationShader.FindKernel("CS_SolveInteraction");
            _kernelBuildMatrices = _simulationShader.FindKernel("CS_BuildMatrices");

            int stride = Marshal.SizeOf<StrawData>();
            _strawBuffer = new ComputeBuffer(_maxStraws, stride);
            _transformBuffer = new ComputeBuffer(_maxStraws, sizeof(float) * 16);
            _colorBuffer = new ComputeBuffer(_maxStraws, sizeof(float) * 4);

            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            _args[0] = (uint)_strawMesh.GetIndexCount(0);
            _args[1] = (uint)_maxStraws;
            _args[2] = (uint)_strawMesh.GetIndexStart(0);
            _args[3] = (uint)_strawMesh.GetBaseVertex(0);
            _args[4] = 0;
            _argsBuffer.SetData(_args);

            // Bind persistent buffers to all kernels
            BindBuffersToKernel(_kernelInit);
            BindBuffersToKernel(_kernelEmit);
            BindBuffersToKernel(_kernelIntegrate);
            BindBuffersToKernel(_kernelSolveCollisions);
            BindBuffersToKernel(_kernelSolveInteraction);
            BindBuffersToKernel(_kernelBuildMatrices);

            // Link buffers to material for GPU Instancing
            _strawMaterial.SetBuffer("_TransformBuffer", _transformBuffer);
            _strawMaterial.SetBuffer("_ColorBuffer", _colorBuffer);

            // Clear buffer
            _simulationShader.SetInt("_MaxStraws", _maxStraws);
            int threadGroups = Mathf.CeilToInt(_maxStraws / 64f);
            _simulationShader.Dispatch(_kernelInit, threadGroups, 1, 1);

            _activeCount = 0;
            _needleFound = false;
            _isInitialized = true;

            // Spawn initial haystack
            if (_initialStrawCount > 0)
            {
                EmitStrawBurst(_initialStrawCount, new Vector3(0f, 1.2f, 0f), Vector3.down * 1.5f, 0.45f);
            }
        }

        private void BindBuffersToKernel(int kernel)
        {
            _simulationShader.SetBuffer(kernel, "_StrawBuffer", _strawBuffer);
            _simulationShader.SetBuffer(kernel, "_TransformBuffer", _transformBuffer);
            _simulationShader.SetBuffer(kernel, "_ColorBuffer", _colorBuffer);
        }

        private void ReleaseBuffers()
        {
            _strawBuffer?.Release();
            _strawBuffer = null;
            _transformBuffer?.Release();
            _transformBuffer = null;
            _colorBuffer?.Release();
            _colorBuffer = null;
            _argsBuffer?.Release();
            _argsBuffer = null;
            _isInitialized = false;
        }

        public void EmitStrawStream(int count, Vector3 origin, Vector3 velocity, float coneAngle, float length = 0.36f, float radius = 0.007f, Color? color = null)
        {
            if (!_isInitialized || count <= 0) return;

            int available = _maxStraws - _activeCount;
            int toEmit = Mathf.Min(count, available);
            if (toEmit <= 0) return;

            int startIndex = _activeCount;
            _simulationShader.SetInt("_EmitStartIndex", startIndex);
            _simulationShader.SetInt("_EmitCount", toEmit);
            _simulationShader.SetVector("_EmitOrigin", origin);
            _simulationShader.SetVector("_EmitVelocity", velocity);
            _simulationShader.SetFloat("_EmitConeAngle", coneAngle);
            _simulationShader.SetFloat("_EmitStrawLength", length);
            _simulationShader.SetFloat("_EmitStrawRadius", radius);
            _simulationShader.SetInt("_Seed", UnityEngine.Random.Range(1, 100000));

            Color baseCol = color ?? new Color(0.88f, 0.76f, 0.42f, 1.0f);
            _simulationShader.SetVector("_EmitColor", baseCol);

            int threadGroups = Mathf.CeilToInt(toEmit / 64f);
            _simulationShader.Dispatch(_kernelEmit, threadGroups, 1, 1);

            _activeCount += toEmit;

            // Handle needle placement inside pile
            if (_spawnNeedle && _needleIndex < 0 && _activeCount >= 200)
            {
                _needleIndex = UnityEngine.Random.Range(50, _activeCount - 1);
                SetNeedleStraw(_needleIndex);
            }

            OnStrawCountChanged?.Invoke(_activeCount);
        }

        public void EmitStrawBurst(int count, Vector3 center, Vector3 initialImpulse, float spreadRadius)
        {
            int batchSize = 500;
            int remaining = count;
            while (remaining > 0)
            {
                int currentBatch = Mathf.Min(remaining, batchSize);
                Vector3 jitterPos = center + UnityEngine.Random.insideUnitSphere * spreadRadius;
                jitterPos.y = Mathf.Max(0.5f, jitterPos.y);
                EmitStrawStream(currentBatch, jitterPos, initialImpulse + UnityEngine.Random.insideUnitSphere * 0.8f, 0.75f);
                remaining -= currentBatch;
            }
        }

        public void SetNeedleStraw(int index)
        {
            if (index < 0 || index >= _activeCount) return;
            _needleIndex = index;

            // Readback single straw, set needle state and golden color
            StrawData[] one = new StrawData[1];
            _strawBuffer.GetData(one, 0, index, 1);
            one[0].state = 2; // Needle flag
            one[0].color = new Vector4(1.0f, 0.86f, 0.22f, 1.0f); // Polished gold
            one[0].radius = 0.0055f;
            one[0].length = 0.38f;
            _strawBuffer.SetData(one, 0, index, 1);
        }

        public void UpdateInteraction(int grabbedIdx, Vector3 targetPos, bool isSifting, Ray siftRay, float siftRadius, float siftStrength)
        {
            if (!_isInitialized) return;

            if (grabbedIdx >= 0 && grabbedIdx < _activeCount)
            {
                StrawData[] one = new StrawData[1];
                _strawBuffer.GetData(one, 0, grabbedIdx, 1);
                one[0].state = 1; // grabbed
                one[0].targetPos = targetPos;
                _strawBuffer.SetData(one, 0, grabbedIdx, 1);

                // Check if the grabbed straw is the Golden Needle!
                if (grabbedIdx == _needleIndex && !_needleFound)
                {
                    _needleFound = true;
                    OnNeedleDiscovered?.Invoke();
                }
            }

            _simulationShader.SetInt("_IsSifting", isSifting ? 1 : 0);
            if (isSifting)
            {
                _simulationShader.SetVector("_SiftRayOrigin", siftRay.origin);
                _simulationShader.SetVector("_SiftRayDir", siftRay.direction);
                _simulationShader.SetFloat("_SiftRadius", siftRadius);
                _simulationShader.SetFloat("_SiftStrength", siftStrength);
            }
        }

        public void ReleaseGrabbedStraw(int index, Vector3 releaseVelocity)
        {
            if (!_isInitialized || index < 0 || index >= _activeCount) return;

            StrawData[] one = new StrawData[1];
            _strawBuffer.GetData(one, 0, index, 1);
            one[0].state = (index == _needleIndex) ? 2u : 0u;
            one[0].velocity = releaseVelocity;
            _strawBuffer.SetData(one, 0, index, 1);
        }

        private void Update()
        {
            if (!_isInitialized || _activeCount <= 0) return;

            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            if (dt <= 0f) dt = 0.016f;

            // Set global simulation uniforms
            _simulationShader.SetFloat("_DeltaTime", dt);
            _simulationShader.SetVector("_Gravity", _gravity);
            _simulationShader.SetFloat("_LinearDrag", _linearDrag);
            _simulationShader.SetFloat("_AngularDrag", _angularDrag);
            _simulationShader.SetFloat("_Restitution", _restitution);
            _simulationShader.SetFloat("_Friction", _friction);
            _simulationShader.SetFloat("_TanglingFriction", _tanglingFriction);
            _simulationShader.SetVector("_BoxMin", _boxMin);
            _simulationShader.SetVector("_BoxMax", _boxMax);
            _simulationShader.SetInt("_ActiveCount", _activeCount);

            int threadGroups = Mathf.CeilToInt(_activeCount / 64f);

            // Step 1: Integrate forces
            _simulationShader.Dispatch(_kernelIntegrate, threadGroups, 1, 1);

            // Step 2: User interaction (sifting/digging hands)
            _simulationShader.Dispatch(_kernelSolveInteraction, threadGroups, 1, 1);

            // Step 3: Solve physical collisions and fiber interlocking
            _simulationShader.Dispatch(_kernelSolveCollisions, threadGroups, 1, 1);

            // Step 4: Build transformation matrices for GPU instancing
            int totalGroups = Mathf.CeilToInt(_maxStraws / 64f);
            _simulationShader.Dispatch(_kernelBuildMatrices, totalGroups, 1, 1);

            // Render all 10,000+ straws via GPU Instancing
            RenderStrawsIndirect();
        }

        private void RenderStrawsIndirect()
        {
            if (_strawMesh == null || _strawMaterial == null || _argsBuffer == null) return;

            Bounds bounds = new Bounds((_boxMin + _boxMax) * 0.5f, _boxMax - _boxMin + Vector3.one * 2f);
            Graphics.DrawMeshInstancedIndirect(_strawMesh, 0, _strawMaterial, bounds, _argsBuffer);
        }

        public int FindStrawUnderRay(Ray ray, out Vector3 hitPoint, float maxDistance = 6.0f)
        {
            hitPoint = Vector3.zero;
            if (!_isInitialized || _activeCount == 0) return -1;

            // Download CPU snapshot for raycasting against straws
            if (_cpuStrawCache == null || _cpuStrawCache.Length != _activeCount)
            {
                _cpuStrawCache = new StrawData[_activeCount];
            }
            _strawBuffer.GetData(_cpuStrawCache, 0, 0, _activeCount);

            int closestIdx = -1;
            float closestDist = maxDistance;

            // Check ray-cylinder intersection against active straws
            for (int i = 0; i < _activeCount; i++)
            {
                Vector3 center = _cpuStrawCache[i].position;
                Vector3 toCenter = center - ray.origin;
                float proj = Vector3.Dot(toCenter, ray.direction);

                if (proj < 0.2f || proj > maxDistance) continue;

                Vector3 closestPointOnRay = ray.origin + ray.direction * proj;
                float distSq = (center - closestPointOnRay).sqrMagnitude;
                float grabRadius = _cpuStrawCache[i].radius * 3.5f + 0.025f;

                if (distSq < grabRadius * grabRadius)
                {
                    if (proj < closestDist)
                    {
                        closestDist = proj;
                        closestIdx = i;
                        hitPoint = closestPointOnRay;
                    }
                }
            }

            return closestIdx;
        }

        public void ClearAllStraws()
        {
            if (!_isInitialized) return;

            _simulationShader.SetInt("_MaxStraws", _maxStraws);
            int threadGroups = Mathf.CeilToInt(_maxStraws / 64f);
            _simulationShader.Dispatch(_kernelInit, threadGroups, 1, 1);

            _activeCount = 0;
            _needleIndex = -1;
            _needleFound = false;
            OnStrawCountChanged?.Invoke(0);
        }

        public StrawData GetStraw(int index)
        {
            if (!_isInitialized || index < 0 || index >= _activeCount) return default;
            StrawData[] one = new StrawData[1];
            _strawBuffer.GetData(one, 0, index, 1);
            return one[0];
        }
    }
}
