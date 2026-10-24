using UnityEngine;

namespace HaystackSimulator.Runtime
{
    public class HayTactileInteractor : MonoBehaviour
    {
        [Header("Camera & View")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private float _lookSensitivity = 2.0f;
        [SerializeField] private float _moveSpeed = 3.5f;

        [Header("Tactile Interaction")]
        [SerializeField] private float _maxGrabDistance = 5.0f;
        [SerializeField] private float _holdDistance = 1.2f;
        [SerializeField] private float _throwForce = 4.5f;
        [SerializeField] private float _siftRadius = 0.55f;
        [SerializeField] private float _siftStrength = 3.8f;

        [Header("Handheld Pitchfork Prop")]
        [SerializeField] private Transform _pitchforkVisual;
        [SerializeField] private Vector3 _pitchforkHoldOffset = new Vector3(0.28f, -0.22f, 0.65f);
        [SerializeField] private Vector3 _pitchforkHoldEuler = new Vector3(-15f, -25f, 10f);

        // State
        private int _grabbedStrawIndex = -1;
        private Vector3 _grabTargetPos;
        private Vector3 _lastGrabPos;
        private Vector3 _grabVelocity;
        private float _yaw = 0f;
        private float _pitch = 20f;
        private bool _isCursorLocked = true;
        private bool _victoryTriggered = false;

        // Reticle texture cache
        private Texture2D _reticleDotTex;
        private Texture2D _reticleOutlineTex;
        private GUIStyle _counterStyle;
        private GUIStyle _victoryStyle;

        private void Start()
        {
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            CreateReticleTextures();

            if (HaySimulationManager.Instance != null)
            {
                HaySimulationManager.Instance.OnNeedleDiscovered += HandleNeedleDiscovered;
            }
        }

        private void OnDestroy()
        {
            if (HaySimulationManager.Instance != null)
            {
                HaySimulationManager.Instance.OnNeedleDiscovered -= HandleNeedleDiscovered;
            }
        }

        private void HandleNeedleDiscovered()
        {
            _victoryTriggered = true;
        }

        private void Update()
        {
            HandleCameraLook();
            HandleLocomotion();
            HandleStrawInteraction();
            UpdatePitchforkTransform();

            // Shortcut keys
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _isCursorLocked = !_isCursorLocked;
                Cursor.lockState = _isCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !_isCursorLocked;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetHaystack();
            }

            // Stream pour while holding Space
            if (Input.GetKey(KeyCode.Space))
            {
                PourHayStream();
            }
        }

        private void HandleCameraLook()
        {
            if (!_isCursorLocked) return;

            float mouseX = Input.GetAxisRaw("Mouse X") * _lookSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * _lookSensitivity;

            _yaw += mouseX;
            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void HandleLocomotion()
        {
            if (!_isCursorLocked) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 move = (transform.forward * v + transform.right * h).normalized;
            transform.position += move * (_moveSpeed * Time.deltaTime);

            // Keep within bounds above floor
            transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, -4f, 4f),
                Mathf.Max(transform.position.y, 1.2f),
                Mathf.Clamp(transform.position.z, -4f, 4f)
            );
        }

        private void HandleStrawInteraction()
        {
            if (_playerCamera == null || HaySimulationManager.Instance == null) return;

            Ray aimRay = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // 1. Left Click: Grab / Release straw
            if (Input.GetMouseButtonDown(0))
            {
                int hitIdx = HaySimulationManager.Instance.FindStrawUnderRay(aimRay, out Vector3 hitPoint, _maxGrabDistance);
                if (hitIdx >= 0)
                {
                    _grabbedStrawIndex = hitIdx;
                    _grabTargetPos = hitPoint;
                    _lastGrabPos = hitPoint;
                }
            }
            else if (Input.GetMouseButton(0) && _grabbedStrawIndex >= 0)
            {
                // Drag grabbed straw in front of player
                _grabTargetPos = _playerCamera.transform.position + _playerCamera.transform.forward * _holdDistance;
                _grabVelocity = (_grabTargetPos - _lastGrabPos) / Mathf.Max(Time.deltaTime, 0.001f);
                _lastGrabPos = _grabTargetPos;
            }
            else if (Input.GetMouseButtonUp(0) && _grabbedStrawIndex >= 0)
            {
                // Throw or drop straw with release velocity
                Vector3 throwImpulse = _grabVelocity * 0.4f + _playerCamera.transform.forward * _throwForce;
                HaySimulationManager.Instance.ReleaseGrabbedStraw(_grabbedStrawIndex, throwImpulse);
                _grabbedStrawIndex = -1;
            }

            // 2. Right Click: Sift / Dig through hay
            bool isSifting = Input.GetMouseButton(1);

            // Update simulation interaction
            HaySimulationManager.Instance.UpdateInteraction(
                _grabbedStrawIndex,
                _grabTargetPos,
                isSifting,
                aimRay,
                _siftRadius,
                _siftStrength
            );
        }

        private void PourHayStream()
        {
            if (HaySimulationManager.Instance == null || _playerCamera == null) return;

            Vector3 spawnOrigin = _playerCamera.transform.position + _playerCamera.transform.forward * 0.9f + _playerCamera.transform.right * 0.15f;
            Vector3 pourVelocity = _playerCamera.transform.forward * 2.8f + Vector3.down * 1.2f;

            // Emit 8 straws per frame while holding Space
            HaySimulationManager.Instance.EmitStrawStream(8, spawnOrigin, pourVelocity, 0.35f);
        }

        private void ResetHaystack()
        {
            if (HaySimulationManager.Instance == null) return;
            _victoryTriggered = false;
            _grabbedStrawIndex = -1;
            HaySimulationManager.Instance.ClearAllStraws();
            HaySimulationManager.Instance.EmitStrawBurst(10000, new Vector3(0f, 1.2f, 0f), Vector3.down * 1.5f, 0.45f);
        }

        private void UpdatePitchforkTransform()
        {
            if (_pitchforkVisual == null || _playerCamera == null) return;

            // Smooth sway in hands
            bool isSifting = Input.GetMouseButton(1);
            Vector3 targetOffset = isSifting 
                ? new Vector3(0.12f, -0.15f, 0.85f) 
                : _pitchforkHoldOffset;

            Quaternion targetRot = Quaternion.Euler(_pitchforkHoldEuler) * (isSifting ? Quaternion.Euler(20f, 0f, 0f) : Quaternion.identity);

            _pitchforkVisual.position = _playerCamera.transform.TransformPoint(targetOffset);
            _pitchforkVisual.rotation = _playerCamera.transform.rotation * targetRot;
        }

        private void CreateReticleTextures()
        {
            // 7px center dot
            _reticleDotTex = new Texture2D(7, 7, TextureFormat.RGBA32, false);
            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3, 3));
                    if (dist <= 2.2f)
                        _reticleDotTex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.95f)); // White core
                    else
                        _reticleDotTex.SetPixel(x, y, Color.clear);
                }
            }
            _reticleDotTex.Apply();

            // 9px dark outline ring
            _reticleOutlineTex = new Texture2D(9, 9, TextureFormat.RGBA32, false);
            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(4, 4));
                    if (dist >= 2.4f && dist <= 3.8f)
                        _reticleOutlineTex.SetPixel(x, y, new Color(0.05f, 0.05f, 0.05f, 0.85f)); // Dark 1px outline
                    else
                        _reticleOutlineTex.SetPixel(x, y, Color.clear);
                }
            }
            _reticleOutlineTex.Apply();
        }

        private void OnGUI()
        {
            // Draw Reticle (6-7px with dark outline, centered on screen)
            if (_isCursorLocked && _reticleDotTex != null && _reticleOutlineTex != null)
            {
                float cx = Screen.width * 0.5f;
                float cy = Screen.height * 0.5f;

                GUI.DrawTexture(new Rect(cx - 4.5f, cy - 4.5f, 9f, 9f), _reticleOutlineTex);
                GUI.DrawTexture(new Rect(cx - 3.5f, cy - 3.5f, 7f, 7f), _reticleDotTex);
            }

            // Minimalist HUD: Strictly [Icon] [Number]
            if (_counterStyle == null)
            {
                _counterStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                _counterStyle.normal.textColor = Color.white;
            }

            int strawCount = HaySimulationManager.Instance != null ? HaySimulationManager.Instance.ActiveStrawCount : 0;
            bool needleFound = HaySimulationManager.Instance != null && HaySimulationManager.Instance.IsNeedleFound;

            GUILayout.BeginArea(new Rect(24, 24, 320, 140));
            GUILayout.Label($"🌾 {strawCount:N0}", _counterStyle);
            
            if (needleFound)
            {
                GUI.color = new Color(1f, 0.85f, 0.2f);
                GUILayout.Label($"🪡 1/1 [ЗОЛОТАЯ ИГЛА НАЙДЕНА!]", _counterStyle);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label($"🪡 0/1", _counterStyle);
            }

            GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.75f);
            GUILayout.Label("[ЛКМ] Взять соломку | [ПКМ] Ворошить стог", GUI.skin.label);
            GUILayout.Label("[Пробел] Досыпать сено | [R] Сброс стога", GUI.skin.label);
            GUI.color = Color.white;
            GUILayout.EndArea();

            // Victory banner
            if (_victoryTriggered)
            {
                if (_victoryStyle == null)
                {
                    _victoryStyle = new GUIStyle(GUI.skin.box)
                    {
                        fontSize = 28,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    _victoryStyle.normal.textColor = new Color(1f, 0.9f, 0.25f);
                }

                GUI.Box(new Rect(Screen.width * 0.5f - 240, 60, 480, 56), "✨ ИГОЛКА В СТОГЕ СЕНА НАЙДЕНА! ✨", _victoryStyle);
            }
        }
    }
}
