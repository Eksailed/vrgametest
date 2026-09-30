using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRTraining.Editor
{
    // Утилита для автоматической сборки тренировочной сцены из примитивов и компонентов.
    // Позволяет развернуть готовую сцену в 1 клик через пункт меню VR Training.
    [InitializeOnLoad]
    public static class TrainingSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/VR_Training_Scene.unity";

        static TrainingSceneBuilder()
        {
            EditorApplication.delayCall += AutoBuild;
        }

        private static void AutoBuild()
        {
            string text = System.IO.File.Exists(ScenePath) ? System.IO.File.ReadAllText(ScenePath) : "";
            if (!text.Contains("XR Interaction Manager") || !text.Contains("XR Input Action Manager"))
            {
                Debug.Log("[VR Training] Обновление сцены с XR Interaction Manager и Input Action Manager...");
                BuildScene();
            }
        }

        [MenuItem("VR Training/Build Complete Training Scene", false, 1)]
        public static void BuildScene()
        {
            OpenXRConfigurator.EnsureOpenXRConfigured();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Создаем центральный менеджер взаимодействий XR Interaction Manager
            GameObject interactionManagerObj = new GameObject("XR Interaction Manager");
            XRInteractionManager interactionManager = interactionManagerObj.AddComponent<XRInteractionManager>();

            // Создаем менеджер действий ввода XR Input Action Manager для работы кнопок и лучей
            GameObject inputManagerObj = new GameObject("XR Input Action Manager");
            var inputActionManager = inputManagerObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            var xriActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/Samples/Starter Assets/XRI Default Input Actions.inputactions");
            if (xriActions != null)
            {
                inputActionManager.actionAssets = new System.Collections.Generic.List<UnityEngine.InputSystem.InputActionAsset> { xriActions };
            }

            // Освещение сцены
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.98f, 0.95f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Окружение: пол и стены комнаты
            GameObject roomObj = new GameObject("Environment_Room");
            GameObject floor = CreateBox(roomObj.transform, "Floor", new Vector3(0, -0.05f, 0), new Vector3(8, 0.1f, 8), new Color(0.25f, 0.25f, 0.27f));
            floor.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
            CreateBox(roomObj.transform, "Wall_North", new Vector3(0, 1.5f, 4f), new Vector3(8, 3, 0.2f), new Color(0.85f, 0.85f, 0.88f));
            CreateBox(roomObj.transform, "Wall_South", new Vector3(0, 1.5f, -4f), new Vector3(8, 3, 0.2f), new Color(0.85f, 0.85f, 0.88f));
            CreateBox(roomObj.transform, "Wall_East", new Vector3(4f, 1.5f, 0), new Vector3(0.2f, 3, 8), new Color(0.82f, 0.82f, 0.85f));
            CreateBox(roomObj.transform, "Wall_West", new Vector3(-4f, 1.5f, 0), new Vector3(0.2f, 3, 8), new Color(0.82f, 0.82f, 0.85f));

            // Стол и Объект A (слева от игрока)
            GameObject tableObj = CreateBox(null, "Table", new Vector3(-1.8f, 0.4f, 1.8f), new Vector3(1.2f, 0.8f, 0.8f), new Color(0.45f, 0.32f, 0.22f));

            GameObject objectA = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            objectA.name = "Object_A";
            objectA.transform.position = new Vector3(-1.8f, 0.9f, 1.8f);
            objectA.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            SetColor(objectA, new Color(0.1f, 0.6f, 0.9f));

            Rigidbody rbA = objectA.AddComponent<Rigidbody>();
            rbA.mass = 1.0f;
            XRGrabInteractable grabA = objectA.AddComponent<XRGrabInteractable>();
            grabA.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grabA.interactionManager = interactionManager;
            Collider colA = objectA.GetComponent<Collider>();
            if (colA != null)
            {
                grabA.colliders.Clear();
                grabA.colliders.Add(colA);
            }
            TryAddOutline(objectA);

            // Стенд и сокет B (справа от игрока)
            GameObject standB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            standB.name = "Stand_B";
            standB.transform.position = new Vector3(1.8f, 0.4f, 1.8f);
            standB.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
            SetColor(standB, new Color(0.3f, 0.35f, 0.4f));

            GameObject socketObj = new GameObject("Socket_Slot");
            socketObj.transform.position = new Vector3(1.8f, 0.9f, 1.8f);

            SphereCollider socketCollider = socketObj.AddComponent<SphereCollider>();
            socketCollider.isTrigger = true;
            socketCollider.radius = 0.25f;

            XRSocketInteractor socketB = socketObj.AddComponent<XRSocketInteractor>();
            socketB.interactionManager = interactionManager;
            TryAddOutline(socketObj);

            // Визуальное кольцо посадочного места
            GameObject socketVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socketVisual.name = "Socket_Visual_Ring";
            socketVisual.transform.parent = socketObj.transform;
            socketVisual.transform.localPosition = Vector3.zero;
            socketVisual.transform.localScale = new Vector3(0.36f, 0.02f, 0.36f);
            GameObject.DestroyImmediate(socketVisual.GetComponent<Collider>());
            SetColor(socketVisual, new Color(0.9f, 0.7f, 0.2f, 0.5f));

            // Постамент и интерактивный рычаг (сзади по центру)
            GameObject leverStand = CreateBox(null, "Lever_Pedestal", new Vector3(0, 0.4f, -1.8f), new Vector3(0.4f, 0.8f, 0.4f), new Color(0.2f, 0.2f, 0.22f));
            GameObject leverBase = CreateBox(leverStand.transform, "Lever_Base", new Vector3(0, 0.82f, -1.8f), new Vector3(0.2f, 0.04f, 0.2f), new Color(0.1f, 0.1f, 0.1f));

            GameObject leverPivot = new GameObject("Lever_Pivot");
            leverPivot.transform.parent = leverBase.transform;
            leverPivot.transform.position = new Vector3(0, 0.85f, -1.8f);

            GameObject leverHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leverHandle.name = "Lever_Handle";
            leverHandle.transform.parent = leverPivot.transform;
            leverHandle.transform.localPosition = new Vector3(0, 0.12f, 0);
            leverHandle.transform.localScale = new Vector3(0.04f, 0.12f, 0.04f);
            SetColor(leverHandle, new Color(0.85f, 0.2f, 0.2f));

            GameObject leverKnob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leverKnob.name = "Lever_Knob";
            leverKnob.transform.parent = leverHandle.transform;
            leverKnob.transform.localPosition = new Vector3(0, 1.0f, 0);
            leverKnob.transform.localScale = new Vector3(2.5f, 0.8f, 2.5f);
            GameObject.DestroyImmediate(leverKnob.GetComponent<Collider>());
            SetColor(leverKnob, Color.white);

            XRGrabInteractable leverGrab = leverHandle.AddComponent<XRGrabInteractable>();
            leverGrab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            leverGrab.trackPosition = false;
            leverGrab.trackRotation = false;
            leverGrab.interactionManager = interactionManager;
            Collider colLever = leverHandle.GetComponent<Collider>();
            if (colLever != null)
            {
                leverGrab.colliders.Clear();
                leverGrab.colliders.Add(colLever);
            }

            // Блокируем гравитацию, чтобы рукоять рычага не падала на пол
            Rigidbody leverRb = leverHandle.GetComponent<Rigidbody>();
            if (leverRb == null)
                leverRb = leverHandle.AddComponent<Rigidbody>();
            leverRb.isKinematic = true;
            leverRb.useGravity = false;

            VRLever vrLever = leverHandle.AddComponent<VRLever>();
            SetSerializedProperty(vrLever, "handleTransform", leverPivot.transform);
            SetSerializedProperty(vrLever, "rotationAxis", Vector3.right);
            SetSerializedProperty(vrLever, "offAngle", 0f);
            SetSerializedProperty(vrLever, "onAngle", 60f);
            SetSerializedProperty(vrLever, "activationThreshold", 0.85f);
            SetSerializedProperty(vrLever, "lockWhenTurnedOn", true);
            TryAddOutline(leverHandle);

            // Пространственный Canvas с заданием
            GameObject canvasObj = new GameObject("Training_UI_Canvas");
            canvasObj.transform.position = new Vector3(0, 1.4f, 1.0f);
            canvasObj.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(700, 220);

            CanvasGroup cGroup = canvasObj.AddComponent<CanvasGroup>();

            // Фон панели UI
            GameObject panelObj = new GameObject("Panel_Background");
            panelObj.transform.parent = canvasObj.transform;
            panelObj.transform.localPosition = Vector3.zero;
            panelObj.transform.localScale = Vector3.one;
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(700, 220);
            var panelImg = panelObj.AddComponent<UnityEngine.UI.Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);

            // Текст задания
            GameObject textObj = new GameObject("Text_Task");
            textObj.transform.parent = panelObj.transform;
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localScale = Vector3.one;
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(650, 180);
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontSize = 34;
            tmpText.color = Color.white;
            tmpText.textWrappingMode = TextWrappingModes.Normal;
            tmpText.text = "Возьмите объект A со стола.";

            TrainingUI trainingUI = canvasObj.AddComponent<TrainingUI>();
            SetSerializedProperty(trainingUI, "taskText", tmpText);
            SetSerializedProperty(trainingUI, "canvasGroup", cGroup);

            // Настройка игрока: XR Origin для шлема + десктопный эмулятор
            GameObject xrOriginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/Starter Assets/Prefabs/XR Origin (XR Rig).prefab");

            if (xrOriginPrefab != null)
            {
                GameObject xrOriginInstance = (GameObject)PrefabUtility.InstantiatePrefab(xrOriginPrefab);
                xrOriginInstance.name = "XR Origin (XR Rig)";
                xrOriginInstance.transform.position = Vector3.zero;

                // Назначаем interactionManager всем интеракторам и группам
                foreach (var interactor in xrOriginInstance.GetComponentsInChildren<XRBaseInteractor>(true))
                {
                    interactor.interactionManager = interactionManager;
                }
                foreach (var group in xrOriginInstance.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRInteractionGroup>(true))
                {
                    group.interactionManager = interactionManager;
                }

                CharacterController cc = xrOriginInstance.GetComponent<CharacterController>();
                if (cc == null)
                {
                    cc = xrOriginInstance.AddComponent<CharacterController>();
                    cc.radius = 0.25f;
                    cc.height = 1.8f;
                    cc.center = new Vector3(0, 0.9f, 0);
                    cc.stepOffset = 0.1f;
                    cc.slopeLimit = 45f;
                }

                // Добавляем Continuous Move Provider для плавного перемещения левым стиком контроллера
                Transform locomotion = xrOriginInstance.transform.Find("Locomotion");
                GameObject locomotionTarget = locomotion != null ? locomotion.gameObject : xrOriginInstance;
                var moveProvider = locomotionTarget.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>();
                if (moveProvider == null)
                {
                    moveProvider = locomotionTarget.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>();
                    moveProvider.moveSpeed = 2.5f;

                    // Находим и привязываем InputActionReference перемещения
                    var allSubAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Samples/Starter Assets/XRI Default Input Actions.inputactions");
                    foreach (var sub in allSubAssets)
                    {
                        if (sub is UnityEngine.InputSystem.InputActionReference actionRef && actionRef.name == "XRI Left Locomotion/Move")
                        {
                            SerializedObject so = new SerializedObject(moveProvider);
                            SerializedProperty modeProp = so.FindProperty("m_LeftHandMoveInput.m_InputSourceMode");
                            if (modeProp != null) modeProp.intValue = 2; // InputActionReference
                            SerializedProperty refProp = so.FindProperty("m_LeftHandMoveInput.m_InputActionReference");
                            if (refProp != null) refProp.objectReferenceValue = actionRef;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            break;
                        }
                    }
                }

                Camera camInRig = xrOriginInstance.GetComponentInChildren<Camera>();
                if (camInRig != null)
                    camInRig.gameObject.AddComponent<VRDesktopSimulator>();
            }
            else
            {
                GameObject cameraObj = new GameObject("Main Camera");
                cameraObj.tag = "MainCamera";
                cameraObj.transform.position = new Vector3(0, 1.6f, 0);
                Camera cam = cameraObj.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cameraObj.AddComponent<AudioListener>();
                cameraObj.AddComponent<VRDesktopSimulator>();
            }

            // Добавляем префаб XR Device Simulator, если доступен в сэмплах
            GameObject simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Device Simulator/XR Device Simulator.prefab");
            if (simPrefab != null)
            {
                GameObject simInstance = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
                simInstance.name = "XR Device Simulator";
            }

            // Менеджер сценария и подсветки
            GameObject managerObj = new GameObject("[ScenarioManager]");
            ObjectHighlighter highlighter = managerObj.AddComponent<ObjectHighlighter>();
            TrainingScenarioManager scenarioManager = managerObj.AddComponent<TrainingScenarioManager>();

            SetSerializedProperty(scenarioManager, "trainingUI", trainingUI);
            SetSerializedProperty(scenarioManager, "highlighter", highlighter);
            SetSerializedProperty(scenarioManager, "objectA", grabA);
            SetSerializedProperty(scenarioManager, "socketB", socketB);
            SetSerializedProperty(scenarioManager, "lever", vrLever);

            // Сохраняем сцену и регистрируем в настройках сборки
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[VR Training] Сцена успешно сохранена в {ScenePath}");

            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log($"[VR Training] Сцена успешно открыта и готова к запуску: {ScenePath}");
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            if (parent != null) obj.transform.parent = parent;
            obj.transform.position = pos;
            obj.transform.localScale = size;
            SetColor(obj, color);
            return obj;
        }

        private static void SetColor(GameObject obj, Color color)
        {
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = color;
                rend.material = mat;
            }
        }

        private static void TryAddOutline(GameObject obj)
        {
            Type outlineType = Type.GetType("Outline, Assembly-CSharp");
            if (outlineType != null)
            {
                Component outline = obj.AddComponent(outlineType);
                if (outline is Behaviour behaviour)
                    behaviour.enabled = false;
            }
        }

        private static void SetSerializedProperty(UnityEngine.Object target, string propName, object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null)
            {
                if (value is UnityEngine.Object uObj)
                    prop.objectReferenceValue = uObj;
                else if (value is float fVal)
                    prop.floatValue = fVal;
                else if (value is bool bVal)
                    prop.boolValue = bVal;
                else if (value is Vector3 vVal)
                    prop.vector3Value = vVal;

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
