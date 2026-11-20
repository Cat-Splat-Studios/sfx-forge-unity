using System;
using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Keeps the backdrop in place behind the camera: the sky and sun are fixed to the view (parallax factor 0), and
    /// each silhouette layer slides at its own fraction of the camera's movement (1 would move with the world).
    /// Runs after the follow camera has moved, so the backdrop never trails a frame behind.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class ParallaxBackground : MonoBehaviour
    {
        [Serializable]
        struct Layer
        {
            [Tooltip("The silhouette to move.")]
            public Transform Silhouette;
            [Tooltip("How much of the camera's movement the layer appears to make: 0 = fixed to the view, 1 = moves with the world.")]
            public float ParallaxFactor;
            [Tooltip("Height of the wave's zero line relative to the middle of the view, in ninths of the view's half-height.")]
            public float Baseline;
        }

        [SerializeField, Tooltip("The game camera the backdrop stays behind.")]
        Camera viewCamera;
        [SerializeField, Tooltip("The gradient quad, stretched to fill the view.")]
        Transform sky;
        [SerializeField, Tooltip("The sun disc, low on the right.")]
        Transform sun;
        [SerializeField, Tooltip("The silhouettes, back to front.")]
        List<Layer> layers = new List<Layer>();
        [SerializeField, Tooltip("World height of the middle of the level: the layers rise and sink a little round it.")]
        float levelMiddleY;
        [SerializeField, Tooltip("How much of its horizontal parallax a layer also shows vertically.")]
        float verticalParallax = 0.3f;
        [SerializeField, Tooltip("The most a layer rises or sinks, in ninths of the view's half-height.")]
        float largestVerticalShift = 3f;
        [SerializeField, Tooltip("Where the sun sits, as a fraction of the view's half-size from its centre.")]
        Vector2 sunPosition = new Vector2(0.55f, -0.3f);
        [SerializeField, Tooltip("The sun's radius as a fraction of the view's half-height.")]
        float sunRadius = 0.3f;

        /// <summary>Wires the camera and the view-fixed parts. Called by <see cref="BackgroundArt"/>.</summary>
        public void Configure(Camera followedCamera, Transform skyPart, Transform sunPart, float middleOfLevelY)
        {
            viewCamera = followedCamera;
            sky = skyPart;
            sun = sunPart;
            levelMiddleY = middleOfLevelY;
        }

        /// <summary>Adds a silhouette layer. Called by <see cref="BackgroundArt"/>, back to front.</summary>
        public void AddLayer(Transform layer, float parallaxFactor, float baseline)
        {
            layers.Add(new Layer { Silhouette = layer, ParallaxFactor = parallaxFactor, Baseline = baseline });
        }

        void LateUpdate()
        {
            Arrange();
        }

        /// <summary>Places everything for where the camera is now. Also called once when the scene is built, so the backdrop is in place before the first frame.</summary>
        public void Arrange()
        {
            Vector2 view = viewCamera.transform.position;
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            float unit = halfHeight / ArtStyle.ViewHalfHeight;

            sky.position = view;
            sky.localScale = new Vector3(halfWidth * 2f + 1f, halfHeight * 2f + 1f, 1f);
            sun.position = view + new Vector2(sunPosition.x * halfWidth, sunPosition.y * halfHeight);
            sun.localScale = Vector3.one * (sunRadius * halfHeight);

            foreach (Layer layer in layers)
            {
                float shift = Mathf.Clamp(-(view.y - levelMiddleY) * layer.ParallaxFactor * verticalParallax,
                    -largestVerticalShift * unit, largestVerticalShift * unit);
                layer.Silhouette.position = new Vector3(view.x * (1f - layer.ParallaxFactor), view.y + layer.Baseline * unit + shift, 0f);
            }
        }
    }
}
