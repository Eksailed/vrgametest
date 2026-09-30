using System;
using UnityEngine;

namespace VRTraining
{
    // Управление визуальным выделением интерактивных объектов.
    // Если в проекте есть Quick Outline, работает через него.
    // Если ассет ещё не импортирован, включает fallback через Material Emission.
    public class ObjectHighlighter : MonoBehaviour
    {
        [Header("Параметры")]
        [SerializeField] private Color highlightColor = new Color(0.2f, 0.8f, 1f, 1f);
        [SerializeField] private float outlineWidth = 5f;

        [Header("Текущий объект")]
        [SerializeField] private GameObject currentHighlightedObject;

        // Кэш исходных параметров материалов для fallback-режима
        private Renderer[] cachedRenderers;
        private Color[] originalEmissionColors;
        private bool[] hadEmissionKeyword;

        // Включает подсветку на целевом объекте и гасит на предыдущем
        public void HighlightTarget(GameObject target)
        {
            if (currentHighlightedObject == target) return;

            ClearHighlight();

            if (target == null) return;
            currentHighlightedObject = target;

            // Сначала пробуем использовать Quick Outline
            if (TryEnableQuickOutline(target, true))
                return;

            // Если компонента Outline нет, используем fallback через emission
            EnableFallbackHighlight(target);
        }

        // Выключает текущую подсветку
        public void ClearHighlight()
        {
            if (currentHighlightedObject == null) return;

            if (TryEnableQuickOutline(currentHighlightedObject, false))
            {
                currentHighlightedObject = null;
                return;
            }

            DisableFallbackHighlight();
            currentHighlightedObject = null;
        }

        private bool TryEnableQuickOutline(GameObject target, bool enable)
        {
            Component outlineComponent = target.GetComponent("Outline");
            if (outlineComponent == null)
            {
                outlineComponent = target.GetComponentInChildren(Type.GetType("Outline") ?? typeof(Component));
                if (outlineComponent != null && outlineComponent.GetType().Name != "Outline")
                    outlineComponent = null;
            }

            if (outlineComponent != null)
            {
                if (outlineComponent is Behaviour behaviour)
                {
                    behaviour.enabled = enable;

                    if (enable)
                    {
                        var colorProp = outlineComponent.GetType().GetProperty("OutlineColor");
                        colorProp?.SetValue(outlineComponent, highlightColor);

                        var widthProp = outlineComponent.GetType().GetProperty("OutlineWidth");
                        widthProp?.SetValue(outlineComponent, outlineWidth);
                    }
                    return true;
                }
            }

            return false;
        }

        private void EnableFallbackHighlight(GameObject target)
        {
            cachedRenderers = target.GetComponentsInChildren<Renderer>();
            if (cachedRenderers == null || cachedRenderers.Length == 0) return;

            originalEmissionColors = new Color[cachedRenderers.Length];
            hadEmissionKeyword = new bool[cachedRenderers.Length];

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var rend = cachedRenderers[i];
                if (rend == null) continue;

                foreach (var mat in rend.materials)
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        hadEmissionKeyword[i] = mat.IsKeywordEnabled("_EMISSION");
                        originalEmissionColors[i] = mat.GetColor("_EmissionColor");
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", highlightColor * 1.5f);
                    }
                    else if (mat.HasProperty("_BaseColor"))
                    {
                        originalEmissionColors[i] = mat.GetColor("_BaseColor");
                        mat.SetColor("_BaseColor", Color.Lerp(originalEmissionColors[i], highlightColor, 0.4f));
                    }
                }
            }
        }

        private void DisableFallbackHighlight()
        {
            if (cachedRenderers == null) return;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var rend = cachedRenderers[i];
                if (rend == null) continue;

                foreach (var mat in rend.materials)
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", originalEmissionColors[i]);
                        if (!hadEmissionKeyword[i])
                            mat.DisableKeyword("_EMISSION");
                    }
                    else if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", originalEmissionColors[i]);
                    }
                }
            }

            cachedRenderers = null;
            originalEmissionColors = null;
            hadEmissionKeyword = null;
        }
    }
}
