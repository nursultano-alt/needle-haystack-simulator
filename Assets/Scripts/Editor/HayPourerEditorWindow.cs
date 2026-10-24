using UnityEditor;
using UnityEngine;
using HaystackSimulator.Runtime;

namespace HaystackSimulator.Editor
{
    public class HayPourerEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Haystack/Hay Pourer Studio")]
        public static void Open()
        {
            HayPourerEditorWindow window = GetWindow<HayPourerEditorWindow>("Hay Pourer Studio");
            window.minSize = new Vector2(360, 520);
            window.Show();
        }

        private bool _sceneBrushEnabled = false;
        private float _brushHeight = 2.0f;
        private int _brushRate = 200;
        private float _strawLength = 0.36f;
        private float _strawRadius = 0.007f;
        private Color _strawColor = new Color(0.88f, 0.76f, 0.40f, 1.0f);

        private double _lastEmitTime = 0.0;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            GUILayout.Label("🌾 Haystack Tech & Artist Studio", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Профессиональный инструмент насыпания и настройки физического стога соломы (10,000+ соломинок).", MessageType.Info);

            EditorGUILayout.Space(6);
            HaySimulationManager mgr = FindFirstObjectByType<HaySimulationManager>();

            if (mgr == null)
            {
                EditorGUILayout.HelpBox("В активной сцене не найден HaySimulationManager! Создайте менеджер симуляции или откройте сцену HaystackPlayground.", MessageType.Warning);
                if (GUILayout.Button("🛠️ Создать HaySimulationManager в сцене"))
                {
                    GameObject go = new GameObject("HaySimulationManager");
                    go.AddComponent<HaySimulationManager>();
                    Undo.RegisterCreatedObjectUndo(go, "Create HaySimulationManager");
                }
                return;
            }

            // Status Dashboard
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📊 Метрики симуляции", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Активных соломинок:", $"{mgr.ActiveStrawCount:N0} / {mgr.MaxStraws:N0}");
            EditorGUILayout.LabelField("Статус Золотой Иглы:", mgr.IsNeedleFound ? "✨ НАЙДЕНА ИГРОКОМ" : (mgr.NeedleIndex >= 0 ? "🪡 Спрятана в стоге" : "❌ Не создана"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            GUILayout.Label("🎯 Быстрое насыпание сена (Physics Pour)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🌾 Досыпать 500", GUILayout.Height(32)))
            {
                mgr.EmitStrawBurst(500, new Vector3(0f, 1.8f, 0f), Vector3.down * 2.0f, 0.35f);
            }
            if (GUILayout.Button("🌾 Досыпать 2,500", GUILayout.Height(32)))
            {
                mgr.EmitStrawBurst(2500, new Vector3(0f, 2.2f, 0f), Vector3.down * 2.5f, 0.5f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🪡 Спрятать Иглу в стог", GUILayout.Height(28)))
            {
                int randomIdx = UnityEngine.Random.Range(50, Mathf.Max(100, mgr.ActiveStrawCount - 1));
                mgr.SetNeedleStraw(randomIdx);
                Debug.Log($"[Haystack] Golden Needle placed at straw index #{randomIdx}");
            }
            if (GUILayout.Button("🧹 Очистить стог (Clear)", GUILayout.Height(28)))
            {
                mgr.ClearAllStraws();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);
            GUILayout.Label("🖌️ Интерактивная кисть в SceneView", EditorStyles.boldLabel);

            _sceneBrushEnabled = EditorGUILayout.Toggle("Включить кисть [SceneView]", _sceneBrushEnabled);
            _brushHeight = EditorGUILayout.Slider("Высота сопла насыпания", _brushHeight, 0.5f, 4.0f);
            _brushRate = EditorGUILayout.IntSlider("Интенсивность (соломинок/сек)", _brushRate, 50, 800);
            _strawLength = EditorGUILayout.Slider("Длина соломинки", _strawLength, 0.15f, 0.60f);
            _strawRadius = EditorGUILayout.Slider("Радиус соломинки", _strawRadius, 0.004f, 0.015f);
            _strawColor = EditorGUILayout.ColorField("Оттенок партии", _strawColor);

            if (_sceneBrushEnabled)
            {
                EditorGUILayout.HelpBox("Зажмите [ЛКМ] в окне SceneView над ящиком или полом, чтобы высыпать непрерывную струю сена с реальной физикой!", MessageType.None);
            }

            Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_sceneBrushEnabled) return;

            HaySimulationManager mgr = FindFirstObjectByType<HaySimulationManager>();
            if (mgr == null) return;

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // Raycast against ground plane Y=0
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 nozzlePos = hitPoint + Vector3.up * _brushHeight;

                // Draw Gizmo Nozzle & Stream
                Handles.color = new Color(1.0f, 0.85f, 0.2f, 0.9f);
                Handles.DrawWireDisc(nozzlePos, Vector3.up, 0.25f);
                Handles.DrawDottedLine(nozzlePos, hitPoint, 4.0f);
                Handles.color = new Color(0.9f, 0.75f, 0.2f, 0.35f);
                Handles.DrawSolidDisc(hitPoint, Vector3.up, 0.45f);

                // Handle mouse pouring
                if (e.type == EventType.MouseDown && e.button == 0 || e.type == EventType.MouseDrag && e.button == 0)
                {
                    double now = EditorApplication.timeSinceStartup;
                    float elapsed = (float)(now - _lastEmitTime);
                    if (elapsed >= 0.033f)
                    {
                        _lastEmitTime = now;
                        int toEmit = Mathf.Max(2, Mathf.RoundToInt(_brushRate * elapsed));
                        Vector3 pourDir = (hitPoint - nozzlePos).normalized * 2.8f;
                        mgr.EmitStrawStream(toEmit, nozzlePos, pourDir, 0.25f, _strawLength, _strawRadius, _strawColor);
                    }
                    e.Use();
                }

                sceneView.Repaint();
            }
        }
    }
}
