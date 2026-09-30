# VR-Тренажёр — Тестовое задание (Unity 6 / XR Interaction Toolkit)

Реализация тестового задания на **Unity 6000.0.58f2** с использованием **XR Interaction Toolkit (XRI 3.x)**.

---

## 📌 Архитектура решения

Решение построено на событийно-ориентированной модели с конечным автоматом (FSM), гарантирующей строгое соблюдение последовательности шагов и защиту от преждевременного срабатывания последующих этапов.

### Основные компоненты:
1. **`TrainingScenarioManager.cs`** — Центральный менеджер сценария:
   - Управляет состояниями тренировки (`Step1_GrabObjectA` ➔ `Step2_SocketObjectA` ➔ `Step3_ToggleLever` ➔ `Completed`).
   - Блокирует элементы последующих шагов (сокет отключен до взятия объекта, рычаг заблокирован до установки в сокет).
   - Подписывается на события XRI (`selectEntered`) и события рычага (`OnLeverTurnedOn`).
   - Автоматически переключает подсветку активного объекта и текст в UI.
2. **`VRLever.cs`** — Интерактивный VR-рычаг:
   - Основан на `XRGrabInteractable` без физической нестабильности PhysX.
   - Проецирует положение руки на локальную плоскость вращения и рассчитывает угол.
   - Определяет угол включения (`onAngle`) и порог срабатывания (`activationThreshold = 0.85`).
   - Поддерживает возврат (snap-back) и фиксацию при срабатывании (`lockWhenTurnedOn`).
   - Вызывает событие `OnLeverTurnedOn`.
3. **`ObjectHighlighter.cs`** — Контроллер визуального выделения:
   - Поддерживает интеграцию с ассетом [Quick Outline](https://assetstore.unity.com/packages/tools/particles-effects/quick-outline-115488).
   - Имеет автоматический Fallback (подсветка через Material Emission/Tint), если ассет ещё не импортирован.
   - Гарантирует отключение подсветки у завершённого объекта и включение у следующего.
4. **`TrainingUI.cs`** — Плавающий интерфейс задач (Lazy Follow HUD):
   - World-Space Canvas с плавным следованием за взглядом игрока (`Vector3.SmoothDamp` + мёртвая зона угла).
   - Устраняет дрожание и укачивание в VR.
   - Опционально поддерживает привязку прямо к целевому объекту (`UIMode.AnchorToTargetObject`).

---

## 🚀 Пошаговая инструкция по настройке в Unity 6 (6000.0.58f2)

### Шаг 1. Установка пакетов
1. Откройте **Window ➔ Package Manager**.
2. Переключитесь на **Unity Registry** и установите:
   - **XR Interaction Toolkit** (версия 3.x для Unity 6).
   - Во вкладке **Samples** у XR Interaction Toolkit импортируйте:
     - `Starter Assets` (пресеты ввода, контроллеров и перемещения).
     - `XR Device Simulator` (для тестирования сцены мышью и клавиатурой без шлема).
3. Перейдите в **Edit ➔ Project Settings ➔ XR Plug-in Management**:
   - Активируйте галочку **OpenXR**.
   - Во вкладке **OpenXR** добавьте Interaction Profiles: *Meta Quest Touch Pro Controller Profile*, *Oculus Touch Controller Profile*.
4. Импортируйте бесплатный ассет [Quick Outline](https://assetstore.unity.com/packages/tools/particles-effects/quick-outline-115488) через Package Manager (My Assets).

---

### Шаг 2. Настройка сцены и окружения
1. Создайте пол: **GameObject ➔ 3D Object ➔ Plane** (Scale: `2, 1, 2`).
2. Добавьте стены из кубов для создания замкнутой комнаты.
3. Добавьте **XR Origin**:
   - В иерархии: Правый клик ➔ **XR ➔ Complete XR Origin Setup** (или используйте префаб из *Starter Assets*).
   - Проверьте наличие `Locomotion System`, `Continuous Move Provider` (или `Teleportation Provider`) и `Snap Turn Provider`.
   - В сцену перетащите префаб **XR Device Simulator** для отладки без VR-шлема.

---

### Шаг 3. Стол и Объект A
1. **Стол**: Куб (Scale `1.5, 0.8, 0.8`), добавьте Box Collider.
2. **Объект A**:
   - Создайте шар или колбу: **3D Object ➔ Sphere** (Scale `0.15, 0.15, 0.15`), положите на стол.
   - Добавьте компоненты:
     - `Rigidbody` (Mass = 1, Use Gravity = true).
     - `XR Grab Interactable` (из пакета XRI).
     - `Outline` (из Quick Outline, изначально `enabled = false`).

---

### Шаг 4. Объект B (Стенд с XR Socket Interactor)
1. В другой части комнаты создайте подставку: **3D Object ➔ Cylinder** (Scale `0.5, 0.9, 0.5`).
2. Создайте дочерний пустой объект `Socket`:
   - Позиция: немного выше верхней крышки подставки (`Y = 0.5`).
   - Добавьте компонент `Sphere Collider` (Radius = `0.15`, установите галочку **Is Trigger = true**).
   - Добавьте компонент `XR Socket Interactor`.
   - Создайте внутри дочерний пустой объект `AttachTransform` для точной фиксации позиции и ориентации Объекта A.
   - Добавьте компонент `Outline` (изначально `enabled = false`).

---

### Шаг 5. Интерактивный рычаг
1. Создайте основание: Куб (Scale `0.2, 0.1, 0.2`).
2. Создайте поворотную точку `LeverPivot` на верхней грани основания:
   - Внутрь добавьте рукоять `LeverHandle`: **3D Object ➔ Cylinder** (Scale `0.04, 0.2, 0.04`, центр смещен вверх, чтобы ось вращения была у основания).
   - Добавьте `Capsule Collider` на рукоять.
   - Добавьте компонент `XR Grab Interactable`.
   - Добавьте компонент `VRLever`:
     - `Handle Transform` ➔ перетащите `LeverPivot`.
     - `Rotation Axis` ➔ `(1, 0, 0)` (или ось X).
     - `Off Angle` = `0`, `On Angle` = `60`.
     - `Activation Threshold` = `0.85`.
     - `Lock When Turned On` = `true`.
   - Добавьте компонент `Outline` (изначально `enabled = false`).

---

### Шаг 6. UI (Плавающий HUD)
1. В иерархии: **GameObject ➔ UI ➔ Canvas**.
2. В компоненте Canvas переключите **Render Mode ➔ World Space**.
3. Настройте размер:
   - RectTransform: Width `600`, Height `200`, Scale `0.001, 0.001, 0.001`.
4. Внутри создайте дочерний `TextMeshPro - Text` (Шрифт `32`, выравнивание по центру).
5. На Canvas повесьте скрипт `TrainingUI`:
   - `Task Text` ➔ перетащите созданный TextMeshPro.
   - `Mode` ➔ `FollowPlayerHead`.
   - `Distance From Camera` = `1.8`.

---

### Шаг 7. Менеджер сценария
1. Создайте пустой объект в корне сцены: `[ScenarioManager]`.
2. Добавьте скрипты:
   - `ObjectHighlighter`
   - `TrainingScenarioManager`
3. В инспекторе `TrainingScenarioManager` свяжите ссылки:
   - `Training UI` ➔ объект Canvas с компонентом `TrainingUI`.
   - `Highlighter` ➔ компонент `ObjectHighlighter`.
   - `Object A` ➔ объект со стола (`XR Grab Interactable`).
   - `Socket B` ➔ сокет на объекте B (`XR Socket Interactor`).
   - `Lever` ➔ компонент `VRLever`.

---

## 🎮 Тестирование через XR Device Simulator (без шлема)

1. Запустите сцену по кнопке **Play**.
2. **Управление**:
   - **Правая кнопка мыши** — поворот головы/камеры.
   - **W / A / S / D** — перемещение по комнате.
   - **Пробел / Левый Shift** — управление правой / левой рукой в пространстве.
   - **G** — захват объекта (Grip).
   - **Левый клик мыши** — триггер (Trigger/Select).
3. **Проверка сценария**:
   - На старте подсвечен только Объект A, UI показывает: *"Возьмите объект A со стола."*
   - Сокет B отключен (не примет ничего раньше времени).
   - Рычаг заблокирован.
   - При взятии Объекта A рукой UI обновляется: *"Подойдите к объекту B и установите в него объект A."* Подсветка переходит на Объект B, сокет активируется.
   - При установке в сокет UI обновляется: *"Подойдите к рычагу и переведите его в положение «Включено»."* Подсветка переходит на рычаг, рычаг разблокируется.
   - При наклоне рычага на 85%+ UI отображает: *"Тренировка завершена."*, подсветка выключается.
