using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRTraining
{
    public enum TrainingStep
    {
        NotStarted,
        Step1_GrabObjectA,
        Step2_SocketObjectA,
        Step3_ToggleLever,
        Completed
    }

    // Менеджер сценария тренировки (конечный автомат).
    // Контролирует строгую последовательность шагов и исключает срабатывание этапов раньше времени.
    public class TrainingScenarioManager : MonoBehaviour
    {
        [Header("Интерфейс и подсветка")]
        [SerializeField] private TrainingUI trainingUI;
        [SerializeField] private ObjectHighlighter highlighter;

        [Header("Шаг 1: Объект A")]
        [SerializeField] private XRGrabInteractable objectA;
        [TextArea(2, 3)]
        [SerializeField] private string step1Text = "Возьмите объект A со стола.";

        [Header("Шаг 2: Сокет B")]
        [SerializeField] private XRSocketInteractor socketB;
        [TextArea(2, 3)]
        [SerializeField] private string step2Text = "Подойдите к объекту B и установите в него объект A.";

        [Header("Шаг 3: Рычаг")]
        [SerializeField] private VRLever lever;
        [TextArea(2, 3)]
        [SerializeField] private string step3Text = "Подойдите к рычагу и переведите его в положение «Включено».";

        [Header("Финал")]
        [TextArea(2, 3)]
        [SerializeField] private string completedText = "Тренировка завершена.";

        [Header("Отладка")]
        [SerializeField] private TrainingStep currentStep = TrainingStep.NotStarted;

        public TrainingStep CurrentStep => currentStep;
        public event Action<TrainingStep> OnStepChanged;

        private void Awake()
        {
            EnsureInteractionInfrastructure();
        }

        // Автоматическая проверка и настройка инфраструктуры XRI при старте
        private void EnsureInteractionInfrastructure()
        {
            // 1. Центральный менеджер взаимодействий
            var im = FindAnyObjectByType<XRInteractionManager>();
            if (im == null)
            {
                var imObj = new GameObject("XR Interaction Manager");
                im = imObj.AddComponent<XRInteractionManager>();
            }

            // 2. Менеджер действий ввода (Input Action Manager) для активации InputActionAsset (лучи, захват, перемещение)
            var iam = FindAnyObjectByType<InputActionManager>();
            if (iam == null)
            {
                var iamObj = new GameObject("XR Input Action Manager");
                iam = iamObj.AddComponent<InputActionManager>();
#if UNITY_EDITOR
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Samples/Starter Assets/XRI Default Input Actions.inputactions");
                if (asset != null)
                {
                    iam.actionAssets = new List<InputActionAsset> { asset };
                }
#endif
            }

            if (iam != null)
            {
                iam.EnableInput();
            }

            // 3. Гарантируем, что у всех объектов и интеракторов проставлен менеджер взаимодействий
            foreach (var interactable in FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (interactable.interactionManager == null)
                    interactable.interactionManager = im;
            }

            foreach (var interactor in FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (interactor.interactionManager == null)
                    interactor.interactionManager = im;
            }

            foreach (var group in FindObjectsByType<XRInteractionGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (group.interactionManager == null)
                    group.interactionManager = im;
            }

            // 4. Отключаем виброотклик для симулятора (у мыши/клавиатуры нет вибромоторов, Unity выдает предупреждение при наведении)
            foreach (var haptic in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Feedback.SimpleHapticFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                haptic.enabled = false;
            }
            foreach (var inputInteractor in FindObjectsByType<XRBaseInputInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                inputInteractor.playHapticsOnHoverEntered = false;
                inputInteractor.playHapticsOnSelectEntered = false;
            }
        }

        private void Start()
        {
            // Блокируем сокет и рычаг на старте, чтобы сценарий нельзя было сломать случайным действием
            InitializeInteractables();

            // Стартуем тренировку с первого шага
            SetStep(TrainingStep.Step1_GrabObjectA);
        }

        private void OnEnable()
        {
            if (objectA != null)
                objectA.selectEntered.AddListener(OnObjectAGrabbed);

            if (socketB != null)
                socketB.selectEntered.AddListener(OnSocketBFilled);

            if (lever != null)
                lever.OnLeverTurnedOn += OnLeverActivated;
        }

        private void OnDisable()
        {
            if (objectA != null)
                objectA.selectEntered.RemoveListener(OnObjectAGrabbed);

            if (socketB != null)
                socketB.selectEntered.RemoveListener(OnSocketBFilled);

            if (lever != null)
                lever.OnLeverTurnedOn -= OnLeverActivated;
        }

        private void InitializeInteractables()
        {
            // Отключаем сокет до шага 2 — пока игрок не возьмет объект A, сокет не реагирует
            if (socketB != null)
                socketB.socketActive = false;

            // Блокируем рычаг до шага 3
            if (lever != null)
                lever.SetInteractableLocked(true);
        }

        public void SetStep(TrainingStep newStep)
        {
            currentStep = newStep;

            switch (currentStep)
            {
                case TrainingStep.Step1_GrabObjectA:
                    UpdateUI(step1Text);
                    if (highlighter != null && objectA != null)
                        highlighter.HighlightTarget(objectA.gameObject);
                    break;

                case TrainingStep.Step2_SocketObjectA:
                    UpdateUI(step2Text);
                    // Активируем сокет для приёма объекта
                    if (socketB != null)
                    {
                        socketB.socketActive = true;
                        if (highlighter != null)
                            highlighter.HighlightTarget(socketB.gameObject);
                    }
                    break;

                case TrainingStep.Step3_ToggleLever:
                    UpdateUI(step3Text);
                    // Разрешаем взаимодействие с рычагом
                    if (lever != null)
                    {
                        lever.SetInteractableLocked(false);
                        if (highlighter != null)
                            highlighter.HighlightTarget(lever.gameObject);
                    }
                    break;

                case TrainingStep.Completed:
                    UpdateUI(completedText);
                    if (highlighter != null)
                        highlighter.ClearHighlight();
                    break;
            }

            OnStepChanged?.Invoke(currentStep);
        }

        // Колбэк захвата объекта A
        private void OnObjectAGrabbed(SelectEnterEventArgs args)
        {
            if (currentStep != TrainingStep.Step1_GrabObjectA)
                return;

            // Засчитываем только если объект взят рукой игрока (не сокетом)
            if (args.interactorObject is not XRSocketInteractor)
            {
                SetStep(TrainingStep.Step2_SocketObjectA);
            }
        }

        // Колбэк установки в сокет B
        private void OnSocketBFilled(SelectEnterEventArgs args)
        {
            if (currentStep != TrainingStep.Step2_SocketObjectA)
                return;

            // Проверяем, что в сокет установлен именно объект A
            if (objectA != null && args.interactableObject == (IXRSelectInteractable)objectA)
            {
                SetStep(TrainingStep.Step3_ToggleLever);
            }
        }

        // Колбэк переключения рычага
        private void OnLeverActivated()
        {
            if (currentStep != TrainingStep.Step3_ToggleLever)
                return;

            SetStep(TrainingStep.Completed);
        }

        private void UpdateUI(string message)
        {
            if (trainingUI != null)
                trainingUI.SetMessage(message);
        }
    }
}
