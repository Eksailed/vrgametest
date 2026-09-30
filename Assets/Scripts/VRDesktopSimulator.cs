using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRTraining
{
    public enum ControlMode
    {
        DesktopPC,
        VirtualReality
    }

    // Универсальный контроллер: переключение между чистым режимом ПК и VR.
    // В режиме ПК полностью скрывает все визуальные элементы VR (руки, лучи, симулятор).
    public class VRDesktopSimulator : MonoBehaviour
    {
        [Header("Текущий режим управления")]
        [SerializeField] private ControlMode currentMode = ControlMode.DesktopPC;

        [Header("Перемещение на ПК")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float sprintMultiplier = 1.8f;

        [Header("Обзор мышью на ПК")]
        [SerializeField] private float mouseSensitivity = 2.0f;

        [Header("Взаимодействие на ПК")]
        [Tooltip("Дистанция взаимодействия (1.35м = расстояние вытянутой руки)")]
        [SerializeField] private float interactDistance = 1.35f;
        [SerializeField] private Transform holdPoint;

        private float rotationX = 0f;
        private float rotationY = 0f;
        private XRGrabInteractable heldObject = null;
        private Rigidbody heldRb = null;
        private string promptText = "";
        private CharacterController characterController;

        // Ссылки на VR-объекты для полного скрытия в режиме ПК
        private GameObject leftControllerObj;
        private GameObject rightControllerObj;
        private GameObject xrDeviceSimulatorObj;

        public ControlMode CurrentMode => currentMode;

        private void Start()
        {
            Vector3 rot = transform.localEulerAngles;
            rotationX = rot.x;
            rotationY = rot.y;

            // Находим или добавляем CharacterController для физических столкновений
            Transform moveTarget = (transform.parent != null) ? transform.root : transform;
            characterController = moveTarget.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = moveTarget.gameObject.AddComponent<CharacterController>();
                characterController.radius = 0.25f;
                characterController.height = 1.8f;
                characterController.center = new Vector3(0, 0.9f, 0);
                characterController.stepOffset = 0.1f;
                characterController.slopeLimit = 45f;
            }

            if (holdPoint == null)
            {
                GameObject hp = new GameObject("HoldPoint");
                hp.transform.parent = transform;
                hp.transform.localPosition = new Vector3(0.2f, -0.15f, 0.8f);
                holdPoint = hp.transform;
            }

            // Находим VR-объекты в иерархии игрока
            CacheVRObjects();

            // Автоопределение на старте: если запущен шлем — выбираем VR, иначе ПК
            if (IsVRDeviceConnected())
            {
                SetControlMode(ControlMode.VirtualReality);
            }
            else
            {
                SetControlMode(ControlMode.DesktopPC);
            }
        }

        private void CacheVRObjects()
        {
            Transform root = (transform.parent != null) ? transform.root : transform;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Left Controller" || child.name == "LeftHand Controller")
                    leftControllerObj = child.gameObject;
                else if (child.name == "Right Controller" || child.name == "RightHand Controller")
                    rightControllerObj = child.gameObject;
            }

            var sim = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>(FindObjectsInactive.Include);
            if (sim != null)
                xrDeviceSimulatorObj = sim.gameObject;
        }

        private void SetVRVisualsActive(bool active)
        {
            // Отключаем/включаем контроллеры рук, их лучи, модели и ретикулы
            if (leftControllerObj != null) leftControllerObj.SetActive(active);
            if (rightControllerObj != null) rightControllerObj.SetActive(active);

            // Отключаем/включаем оверлей и симулятор XR Device Simulator
            if (xrDeviceSimulatorObj != null) xrDeviceSimulatorObj.SetActive(active);
        }

        public void SetControlMode(ControlMode mode)
        {
            currentMode = mode;

            if (currentMode == ControlMode.VirtualReality)
            {
                // Включаем драйвер трекинга шлема
                SetTrackedPoseDriverEnabled(true);

                // Показываем контроллеры рук и элементы VR
                SetVRVisualsActive(true);

                // Отпускаем предмет, если держали мышью
                if (heldObject != null)
                    DropObject();

                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                Debug.Log("[VR Training] Активирован режим: 🥽 VR Шлем");
            }
            else
            {
                // Режим ПК: отключаем TrackedPoseDriver на камере
                SetTrackedPoseDriverEnabled(false);

                // ПОЛНОСТЬЮ скрываем всё, что связано с VR (руки, лучи, оверлеи)
                SetVRVisualsActive(false);

                Debug.Log("[VR Training] Активирован режим: 💻 ПК (все элементы VR скрыты)");
            }
        }

        private bool IsVRDeviceConnected()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
                return true;

            var xrDisplays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(xrDisplays);
            foreach (var display in xrDisplays)
            {
                if (display.running) return true;
            }

            return false;
        }

        private void SetTrackedPoseDriverEnabled(bool enable)
        {
            var behaviours = GetComponents<Behaviour>();
            foreach (var b in behaviours)
            {
                if (b != null && b.GetType().Name.Contains("TrackedPoseDriver"))
                {
                    b.enabled = enable;
                }
            }
        }

        private void OnDisable()
        {
            SetTrackedPoseDriverEnabled(true);
            SetVRVisualsActive(true);
        }

        private void Update()
        {
            // Быстрое переключение режима по клавише F1
            if (Input.GetKeyDown(KeyCode.F1))
            {
                SetControlMode(currentMode == ControlMode.DesktopPC ? ControlMode.VirtualReality : ControlMode.DesktopPC);
            }

            // Логика управления с клавиатуры и мыши активна только в режиме ПК
            if (currentMode == ControlMode.DesktopPC)
            {
                HandleMouseLook();
                HandleMovement();
                HandleInteractionRay();
                HandleHeldObjectPhysics();
            }
        }

        // Поворот камеры мышью (зажатая ПКМ)
        private void HandleMouseLook()
        {
            if (Input.GetMouseButton(1))
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                rotationY += mouseX;
                rotationX -= mouseY;
                rotationX = Mathf.Clamp(rotationX, -85f, 85f);

                transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
            }
            else
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        // Ходьба на WASD с коллизиями
        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 moveDir = Vector3.zero;
            if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
            {
                float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1.0f);

                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();

                Vector3 right = transform.right;
                right.y = 0;
                right.Normalize();

                moveDir = (forward * vertical + right * horizontal).normalized * speed;
            }

            if (characterController != null && characterController.enabled)
            {
                Vector3 motion = moveDir;
                motion.y = characterController.isGrounded ? -1f : -9.81f;
                characterController.Move(motion * Time.deltaTime);

                Vector3 pos = characterController.transform.position;
                pos.x = Mathf.Clamp(pos.x, -3.4f, 3.4f);
                pos.z = Mathf.Clamp(pos.z, -3.4f, 3.4f);
                characterController.transform.position = pos;
            }
            else if (moveDir.sqrMagnitude > 0.01f)
            {
                Transform moveTarget = (transform.parent != null) ? transform.root : transform;
                moveTarget.position += moveDir * Time.deltaTime;
            }
        }

        // Луч взаимодействия
        private void HandleInteractionRay()
        {
            promptText = "";

            if (heldObject != null)
            {
                promptText = "[ЛКМ / E] Отпустить объект";

                Ray raySocket = new Ray(transform.position, transform.forward);
                if (Physics.Raycast(raySocket, out RaycastHit hitSocket, interactDistance))
                {
                    XRSocketInteractor socket = hitSocket.collider.GetComponentInParent<XRSocketInteractor>();
                    if (socket != null && socket.socketActive)
                    {
                        promptText = "[ЛКМ / E] Установить в сокет";
                        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
                        {
                            PlaceObjectIntoSocket(socket);
                            return;
                        }
                    }
                }

                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
                {
                    DropObject();
                }
                return;
            }

            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                XRGrabInteractable grab = hit.collider.GetComponentInParent<XRGrabInteractable>();
                VRLever lever = hit.collider.GetComponentInParent<VRLever>();

                if (lever != null)
                {
                    if (!lever.IsLocked && !lever.IsTurnedOn)
                    {
                        promptText = "[ЛКМ / E] Переключить рычаг в положение «Включено»";
                        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
                        {
                            ToggleLever(lever);
                        }
                    }
                }
                else if (grab != null && grab.GetComponent<VRLever>() == null)
                {
                    promptText = $"[ЛКМ / E] Взять {grab.gameObject.name}";
                    if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
                    {
                        GrabObject(grab);
                    }
                }
            }
        }

        private void GrabObject(XRGrabInteractable grab)
        {
            heldObject = grab;
            heldRb = grab.GetComponent<Rigidbody>();
            if (heldRb != null)
                heldRb.isKinematic = true;

            var manager = FindAnyObjectByType<TrainingScenarioManager>();
            if (manager != null && manager.CurrentStep == TrainingStep.Step1_GrabObjectA)
            {
                manager.SetStep(TrainingStep.Step2_SocketObjectA);
            }
        }

        private void DropObject()
        {
            if (heldObject == null) return;

            if (heldRb != null)
                heldRb.isKinematic = false;

            heldObject = null;
            heldRb = null;
        }

        private void PlaceObjectIntoSocket(XRSocketInteractor socket)
        {
            if (heldObject == null) return;

            Transform attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
            heldObject.transform.position = attach.position;
            heldObject.transform.rotation = attach.rotation;

            if (heldRb != null)
                heldRb.isKinematic = true;

            var manager = FindAnyObjectByType<TrainingScenarioManager>();
            if (manager != null && manager.CurrentStep == TrainingStep.Step2_SocketObjectA)
            {
                manager.SetStep(TrainingStep.Step3_ToggleLever);
            }

            heldObject = null;
            heldRb = null;
        }

        private void ToggleLever(VRLever lever)
        {
            lever.ForceActivate();

            var manager = FindAnyObjectByType<TrainingScenarioManager>();
            if (manager != null && manager.CurrentStep == TrainingStep.Step3_ToggleLever)
            {
                manager.SetStep(TrainingStep.Completed);
            }
        }

        private void HandleHeldObjectPhysics()
        {
            if (heldObject != null && holdPoint != null)
            {
                heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, holdPoint.position, Time.deltaTime * 15f);
                heldObject.transform.rotation = Quaternion.Slerp(heldObject.transform.rotation, holdPoint.rotation, Time.deltaTime * 15f);
            }
        }

        private void OnGUI()
        {
            bool isPC = (currentMode == ControlMode.DesktopPC);

            // Аккуратная кнопка в правом верхнем углу для возможности переключения в любой момент
            GUIStyle switchBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            if (isPC)
            {
                GUI.color = new Color(0.85f, 0.85f, 0.9f, 0.85f);
                if (GUI.Button(new Rect(Screen.width - 165, 12, 150, 30), "🥽 Режим VR [F1]", switchBtnStyle))
                {
                    SetControlMode(ControlMode.VirtualReality);
                }
            }
            else
            {
                GUI.color = new Color(0.35f, 0.9f, 0.5f, 0.95f);
                if (GUI.Button(new Rect(Screen.width - 165, 12, 150, 30), "💻 Режим ПК [F1]", switchBtnStyle))
                {
                    SetControlMode(ControlMode.DesktopPC);
                }
            }

            GUI.color = Color.white;

            // В режиме ПК показываем исключительно элементы ПК управления (без какого-либо мусора из VR)
            if (isPC)
            {
                // Прицел в центре экрана
                int crosshairSize = 6;
                float centerX = Screen.width / 2f;
                float centerY = Screen.height / 2f;

                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.Box(new Rect(centerX - crosshairSize / 2f, centerY - crosshairSize / 2f, crosshairSize, crosshairSize), GUIContent.none);

                // Подсказка действия перед объектом
                if (!string.IsNullOrEmpty(promptText))
                {
                    GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 18,
                        fontStyle = FontStyle.Bold
                    };
                    promptStyle.normal.textColor = Color.yellow;
                    GUI.Label(new Rect(centerX - 250, centerY + 30, 500, 40), promptText, promptStyle);
                }

                // Памятка по обычному ПК управлению (без упоминания VR)
                GUIStyle hintBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13
                };
                hintBoxStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);

                string pcControls = " Управление:\n [ПКМ] - Зажать для обзора\n [W, A, S, D] - Ходьба (Shift - бег)\n [E] или [ЛКМ] - Взаимодействие";
                GUI.Box(new Rect(15, 15, 255, 80), pcControls, hintBoxStyle);
            }
            else
            {
                // В режиме VR отображаем только подтверждение активности шлема
                GUIStyle vrHintStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13
                };
                vrHintStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);

                string vrControls = " Активен режим VR:\n Шлем и контроллеры работают\n Нажмите [F1] для перехода на ПК";
                GUI.Box(new Rect(15, 15, 260, 65), vrControls, vrHintStyle);
            }
        }
    }
}
