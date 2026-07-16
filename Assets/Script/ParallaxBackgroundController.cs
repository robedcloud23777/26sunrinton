using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies horizontal and vertical parallax only to the named woods background
/// sprites. It never changes the camera, tilemap, props, actors, or sorting.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class ParallaxBackgroundController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform backgroundRoot;

    [Header("Horizontal Parallax - Camera Motion Follow Rate")]
    [SerializeField, Range(0f, 1f)] private float backdropFactor = 0.98f;
    [SerializeField, Range(0f, 1f)] private float bushFactor = 0.94f;
    [SerializeField, Range(0f, 1f)] private float fourthWoodsFactor = 0.9f;
    [SerializeField, Range(0f, 1f)] private float thirdWoodsFactor = 0.84f;
    [SerializeField, Range(0f, 1f)] private float secondWoodsFactor = 0.78f;
    [SerializeField, Range(0f, 1f)] private float firstWoodsFactor = 0.7f;

    [Header("Vertical Parallax")]
    [SerializeField] private bool useVerticalParallax = true;
    [Tooltip("0 makes every layer follow vertically. 1 uses the full depth difference.")]
    [SerializeField, Range(0f, 1f)] private float verticalParallaxMultiplier = 0.65f;

    [Header("Background-Only Viewport Coverage")]
    [SerializeField] private bool stretchBackgroundVertically = true;
    [SerializeField, Min(0f)] private float verticalCoveragePadding = 0.05f;

    private readonly List<ParallaxLayer> layers = new List<ParallaxLayer>();
    private Vector3 initialCameraPosition;

    private struct ParallaxLayer
    {
        public Transform Transform;
        public Vector3 InitialPosition;
        public Vector3 InitialScale;
        public float InitialWorldHeight;
        public float CameraMotionFactor;
        public bool KeepsVerticalCoverage;
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureBackgroundLayers();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            return;
        }

        Vector3 cameraDelta = targetCamera.transform.position - initialCameraPosition;
        float requiredHeight = targetCamera.orthographic
            ? targetCamera.orthographicSize * 2f + verticalCoveragePadding * 2f
            : 0f;

        for (int i = 0; i < layers.Count; i++)
        {
            ParallaxLayer layer = layers[i];
            if (layer.Transform == null)
            {
                continue;
            }

            float verticalFactor = layer.KeepsVerticalCoverage
                ? 1f
                : Mathf.Lerp(1f, layer.CameraMotionFactor, verticalParallaxMultiplier);
            float verticalOffset = useVerticalParallax ? cameraDelta.y * verticalFactor : 0f;

            layer.Transform.position = layer.InitialPosition + new Vector3(
                cameraDelta.x * layer.CameraMotionFactor,
                verticalOffset,
                0f);

            if (stretchBackgroundVertically && requiredHeight > 0f && layer.InitialWorldHeight > 0.01f)
            {
                float heightMultiplier = Mathf.Max(1f, requiredHeight / layer.InitialWorldHeight);
                Vector3 scale = layer.InitialScale;
                scale.y *= heightMultiplier;
                layer.Transform.localScale = scale;
            }
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < layers.Count; i++)
        {
            ParallaxLayer layer = layers[i];
            if (layer.Transform == null)
            {
                continue;
            }

            layer.Transform.position = layer.InitialPosition;
            layer.Transform.localScale = layer.InitialScale;
        }
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (backgroundRoot == null)
        {
            GameObject mapObject = GameObject.Find("Map");
            backgroundRoot = mapObject != null ? mapObject.transform : null;
        }

        initialCameraPosition = targetCamera != null
            ? targetCamera.transform.position
            : transform.position;
    }

    private void CaptureBackgroundLayers()
    {
        layers.Clear();
        if (backgroundRoot == null)
        {
            return;
        }

        SpriteRenderer[] renderers = backgroundRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !TryGetLayerFactor(renderer.gameObject.name, out float factor))
            {
                continue;
            }

            layers.Add(new ParallaxLayer
            {
                Transform = renderer.transform,
                InitialPosition = renderer.transform.position,
                InitialScale = renderer.transform.localScale,
                InitialWorldHeight = renderer.bounds.size.y,
                CameraMotionFactor = factor,
                KeepsVerticalCoverage = renderer.gameObject.name == "BACKGROUND"
            });
        }
    }

    private bool TryGetLayerFactor(string layerName, out float factor)
    {
        switch (layerName)
        {
            case "BACKGROUND":
                factor = backdropFactor;
                return true;
            case "BUSH - BACKGROUND":
                factor = bushFactor;
                return true;
            case "WOODS - Fourth":
                factor = fourthWoodsFactor;
                return true;
            case "WOODS - Third":
                factor = thirdWoodsFactor;
                return true;
            case "WOODS - Second":
            case "VINES - Second":
                factor = secondWoodsFactor;
                return true;
            case "WOODS - First":
                factor = firstWoodsFactor;
                return true;
            default:
                factor = 0f;
                return false;
        }
    }
}
