using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRTraining
{
    // VR-рычаг на базе XRGrabInteractable.
    // Вместо нестабильного в VR HingeJoint угол вычисляется проекцией положения руки на плоскость вращения.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class VRLever : MonoBehaviour
    {
        [Header("Геометрия")]
        [Tooltip("Трансформ рукояти (поворотная часть)")]
        [SerializeField] private Transform handleTransform;

        [Tooltip("Ось вращения в локальных координатах")]
        [SerializeField] private Vector3 rotationAxis = Vector3.right;

        [Header("Углы и порог срабатывания")]
        [Tooltip("Исходный угол (Выключено)")]
        [SerializeField] private float offAngle = 0f;

        [Tooltip("Целевой угол (Включено)")]
        [SerializeField] private float onAngle = 60f;

        [Range(0f, 1f)]
        [Tooltip("Процент хода до срабатывания (0.85 = 85%)")]
        [SerializeField] private float activationThreshold = 0.85f;

        [Header("Поведение")]
        [Tooltip("Зафиксировать рычаг во включенном положении")]
        [SerializeField] private bool lockWhenTurnedOn = true;

        [Tooltip("Скорость возврата при отпускании")]
        [SerializeField] private float snapSpeed = 10f;

        [Header("Звук")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip switchOnSound;

        [Header("Состояние")]
        [SerializeField] private bool isTurnedOn = false;
        [SerializeField] private bool isLocked = false;

        private XRGrabInteractable grabInteractable;
        private IXRSelectInteractor activeInteractor;
        private float currentAngle = 0f;
        private Quaternion initialLocalRotation;

        public bool IsTurnedOn => isTurnedOn;
        public bool IsLocked => isLocked;

        public event Action OnLeverTurnedOn;
        public event Action OnLeverTurnedOff;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();

            if (handleTransform == null)
                handleTransform = transform;

            initialLocalRotation = handleTransform.localRotation;
            currentAngle = offAngle;
            UpdateHandleRotation(currentAngle);

            // Отключаем физическое падение: рычаг зафиксирован в шарнире и не должен падать под гравитацией
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;

            // Отключаем перемещение самого объекта рукой, вращением управляем программно
            grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
        }

        private void OnEnable()
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
            grabInteractable.activated.AddListener(OnActivated);
        }

        private void OnDisable()
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
            grabInteractable.activated.RemoveListener(OnActivated);
        }

        private void OnActivated(ActivateEventArgs args)
        {
            if (isLocked) return;
            ForceActivate();
        }

        public void SetInteractableLocked(bool locked)
        {
            isLocked = locked;
            if (grabInteractable != null)
                grabInteractable.enabled = !locked;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (isLocked) return;

            // Если взаимодействие происходит лучом на дистанции (XRRayInteractor / NearFarInteractor) — сразу переключаем рычаг
            if (args.interactorObject is NearFarInteractor or XRRayInteractor)
            {
                ForceActivate();
                return;
            }

            activeInteractor = args.interactorObject;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            activeInteractor = null;
        }

        private void Update()
        {
            if (activeInteractor != null && (!isLocked || !lockWhenTurnedOn || !isTurnedOn))
            {
                TrackHandAngle();
            }
            else
            {
                SnapHandle();
            }
        }

        // Вычисляем угол поворота рукояти за рукой игрока
        private void TrackHandAngle()
        {
            Transform handTransform = activeInteractor.GetAttachTransform(grabInteractable);
            if (handTransform == null) return;

            Vector3 leverToHand = handTransform.position - handleTransform.position;
            Vector3 localLeverToHand = handleTransform.parent != null
                ? handleTransform.parent.InverseTransformDirection(leverToHand)
                : leverToHand;

            // Проецируем вектор на плоскость вращения, чтобы рука двигала рычаг строго по заданной оси
            Vector3 perpendicularAxis = Vector3.Cross(rotationAxis, Vector3.up);
            if (perpendicularAxis.sqrMagnitude < 0.001f)
                perpendicularAxis = Vector3.Cross(rotationAxis, Vector3.forward);

            Vector3 projected = Vector3.ProjectOnPlane(localLeverToHand, rotationAxis).normalized;
            float angle = Vector3.SignedAngle(Vector3.up, projected, rotationAxis);

            // Учитываем отклонение рукояти от исходного положения
            float range = Mathf.Abs(onAngle - offAngle);
            if (range < 0.01f) range = 60f;

            float delta = Mathf.Abs(angle - offAngle);
            float progress = Mathf.Clamp01(delta / range);
            currentAngle = Mathf.Lerp(offAngle, onAngle, progress);

            UpdateHandleRotation(currentAngle);
            CheckThreshold(progress);
        }

        // Возврат рукояти в исходное положение при отпускании
        private void SnapHandle()
        {
            float targetAngle = (isTurnedOn && lockWhenTurnedOn) ? onAngle : (isTurnedOn ? onAngle : offAngle);
            if (Mathf.Abs(currentAngle - targetAngle) > 0.01f)
            {
                currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * snapSpeed);
                UpdateHandleRotation(currentAngle);
            }
        }

        private void UpdateHandleRotation(float angle)
        {
            handleTransform.localRotation = initialLocalRotation * Quaternion.AngleAxis(angle, rotationAxis);
        }

        private void CheckThreshold(float progress)
        {
            if (!isTurnedOn && progress >= activationThreshold)
            {
                isTurnedOn = true;
                PlaySound();
                OnLeverTurnedOn?.Invoke();

                if (lockWhenTurnedOn)
                {
                    currentAngle = onAngle;
                    UpdateHandleRotation(currentAngle);
                    SetInteractableLocked(true);
                }
            }
            else if (isTurnedOn && !lockWhenTurnedOn && progress < (activationThreshold - 0.1f))
            {
                isTurnedOn = false;
                OnLeverTurnedOff?.Invoke();
            }
        }

        // Принудительная активация (используется эмулятором десктопа)
        public void ForceActivate()
        {
            if (isTurnedOn) return;

            isTurnedOn = true;
            currentAngle = onAngle;
            UpdateHandleRotation(currentAngle);
            PlaySound();
            OnLeverTurnedOn?.Invoke();

            if (lockWhenTurnedOn)
                SetInteractableLocked(true);
        }

        private void PlaySound()
        {
            if (audioSource != null && switchOnSound != null)
                audioSource.PlayOneShot(switchOnSound);
        }
    }
}
