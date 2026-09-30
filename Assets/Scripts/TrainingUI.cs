using TMPro;
using UnityEngine;

namespace VRTraining
{
    public enum UIMode
    {
        FollowPlayerHead,
        AnchorToTargetObject
    }

    // Отображение текущего задания тренировки в пространстве (World-Space Canvas).
    // По умолчанию работает в режиме Lazy Follow HUD: плавно висит перед взглядом игрока
    // с мёртвой зоной по углу, чтобы не мельтешить при мелких движениях головы в VR.
    public class TrainingUI : MonoBehaviour
    {
        [Header("Компоненты")]
        [SerializeField] private TextMeshProUGUI taskText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Режим")]
        [SerializeField] private UIMode mode = UIMode.FollowPlayerHead;

        [Header("Параметры следования (Lazy Follow)")]
        [Tooltip("Камера игрока (HMD). Если пусто, берется Camera.main")]
        [SerializeField] private Transform targetCamera;

        [Tooltip("Дистанция от глаз (м)")]
        [SerializeField] private float distanceFromCamera = 1.8f;

        [Tooltip("Смещение по вертикали относительно глаз")]
        [SerializeField] private float heightOffset = -0.15f;

        [Tooltip("Время сглаживания позиции")]
        [SerializeField] private float positionSmoothTime = 0.3f;

        [Tooltip("Скорость доворота к взгляду")]
        [SerializeField] private float rotationSpeed = 5f;

        [Tooltip("Мёртвая зона (градусы): панель не поворачивается при мелких движениях головы")]
        [SerializeField] private float angleDeadzone = 25f;

        [Header("Привязка к объекту")]
        [SerializeField] private Vector3 objectOffset = new Vector3(0, 0.4f, 0);

        private Vector3 currentVelocity;
        private Transform currentAnchorTarget;

        private void Awake()
        {
            if (targetCamera == null && Camera.main != null)
                targetCamera = Camera.main.transform;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                if (Camera.main != null)
                    targetCamera = Camera.main.transform;
                return;
            }

            switch (mode)
            {
                case UIMode.FollowPlayerHead:
                    UpdateFollowPlayer();
                    break;
                case UIMode.AnchorToTargetObject:
                    UpdateAnchorToObject();
                    break;
            }
        }

        public void SetMessage(string message)
        {
            if (taskText != null)
                taskText.text = message;
        }

        public void SetAnchorTarget(Transform target)
        {
            currentAnchorTarget = target;
        }

        public void SetMode(UIMode newMode)
        {
            mode = newMode;
        }

        private void UpdateFollowPlayer()
        {
            // Направление взгляда без учета наклона головы по вертикали (держим плоскость пола)
            Vector3 forwardFlat = targetCamera.forward;
            forwardFlat.y = 0;
            if (forwardFlat.sqrMagnitude < 0.01f)
                forwardFlat = targetCamera.up;
            forwardFlat.Normalize();

            Vector3 targetPos = targetCamera.position + forwardFlat * distanceFromCamera;
            targetPos.y = targetCamera.position.y + heightOffset;

            // Плавный переход к целевой точке перед глазами
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, positionSmoothTime);

            // Поворачиваем панель лицом к игроку с проверкой мёртвой зоны
            Vector3 lookDirection = transform.position - targetCamera.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection, Vector3.up);
                float angle = Quaternion.Angle(transform.rotation, targetRot);

                if (angle > angleDeadzone)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                }
            }
        }

        private void UpdateAnchorToObject()
        {
            if (currentAnchorTarget != null)
            {
                Vector3 targetPos = currentAnchorTarget.position + objectOffset;
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, positionSmoothTime);

                Vector3 lookDirection = transform.position - targetCamera.position;
                if (lookDirection.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
            else
            {
                UpdateFollowPlayer();
            }
        }
    }
}
