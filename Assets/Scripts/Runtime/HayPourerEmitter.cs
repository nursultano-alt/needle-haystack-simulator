using UnityEngine;

namespace HaystackSimulator.Runtime
{
    public class HayPourerEmitter : MonoBehaviour
    {
        [Header("Emission Stream Controls")]
        [SerializeField] private bool _autoPour = false;
        [Range(10, 1500)] [SerializeField] private int _pourRatePerSecond = 250;
        [Range(0.05f, 1.2f)] [SerializeField] private float _coneAngle = 0.35f;
        [Range(0.5f, 12f)] [SerializeField] private float _pourVelocity = 3.2f;

        [Header("Straw Physical Properties")]
        [Range(0.15f, 0.6f)] [SerializeField] private float _strawLength = 0.36f;
        [Range(0.003f, 0.015f)] [SerializeField] private float _strawRadius = 0.007f;

        [Header("Color Palette Preset")]
        [SerializeField] private ColorPreset _palette = ColorPreset.GoldenWheat;
        [SerializeField] private Color _customColor = new Color(0.88f, 0.76f, 0.42f, 1.0f);

        public enum ColorPreset
        {
            GoldenWheat,
            SunBleached,
            FreshGreenHay,
            AutumnAmber,
            Custom
        }

        private float _strawAccumulator = 0f;

        public bool IsAutoPouring
        {
            get => _autoPour;
            set => _autoPour = value;
        }

        public void PourBurst(int count)
        {
            if (HaySimulationManager.Instance == null) return;
            Vector3 origin = transform.position;
            Vector3 velocity = transform.forward * _pourVelocity;
            Color col = GetColorForPreset(_palette);
            HaySimulationManager.Instance.EmitStrawStream(count, origin, velocity, _coneAngle, _strawLength, _strawRadius, col);
        }

        private void Update()
        {
            if (!_autoPour || HaySimulationManager.Instance == null) return;

            _strawAccumulator += _pourRatePerSecond * Time.deltaTime;
            int countToEmit = Mathf.FloorToInt(_strawAccumulator);

            if (countToEmit > 0)
            {
                _strawAccumulator -= countToEmit;
                Vector3 origin = transform.position;
                Vector3 velocity = transform.forward * _pourVelocity;
                Color col = GetColorForPreset(_palette);
                HaySimulationManager.Instance.EmitStrawStream(countToEmit, origin, velocity, _coneAngle, _strawLength, _strawRadius, col);
            }
        }

        private Color GetColorForPreset(ColorPreset preset)
        {
            return preset switch
            {
                ColorPreset.GoldenWheat => new Color(0.88f, 0.76f, 0.40f, 1.0f),
                ColorPreset.SunBleached => new Color(0.92f, 0.88f, 0.65f, 1.0f),
                ColorPreset.FreshGreenHay => new Color(0.68f, 0.74f, 0.35f, 1.0f),
                ColorPreset.AutumnAmber => new Color(0.82f, 0.58f, 0.28f, 1.0f),
                ColorPreset.Custom => _customColor,
                _ => new Color(0.88f, 0.76f, 0.40f, 1.0f)
            };
        }

        private void OnDrawGizmosSelected()
        {
            // Draw Nozzle Funnel & Spray Cone
            Gizmos.color = new Color(1.0f, 0.84f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.12f);
            Gizmos.DrawRay(transform.position, transform.forward * _pourVelocity * 0.4f);

            // Cone rays
            Vector3 fwd = transform.forward * (_pourVelocity * 0.4f);
            Vector3 right = transform.right * (_coneAngle * 0.5f);
            Vector3 up = transform.up * (_coneAngle * 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + fwd + right);
            Gizmos.DrawLine(transform.position, transform.position + fwd - right);
            Gizmos.DrawLine(transform.position, transform.position + fwd + up);
            Gizmos.DrawLine(transform.position, transform.position + fwd - up);
        }
    }
}
