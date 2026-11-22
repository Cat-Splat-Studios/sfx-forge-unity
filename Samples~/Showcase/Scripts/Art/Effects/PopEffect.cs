using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The pop of a collected Chime: a Chime Cyan circle that grows for a fifth of a second while its line thins to
    /// nothing, then removes itself. The circle is redrawn each frame (a scaled mesh would keep its line thickness).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class PopEffect : MonoBehaviour
    {
        [SerializeField, Tooltip("How long the pop lasts, seconds.")]
        float seconds = 0.2f;

        [SerializeField, Tooltip("Radius at the start and at the end, metres.")]
        Vector2 radius = new Vector2(0.25f, 0.9f);

        [SerializeField, Tooltip("Thickness of the line at the start, metres; it thins to nothing.")]
        float thickness = 0.14f;

        const int Segments = 24;

        readonly MeshBuilder builder = new MeshBuilder();
        Mesh mesh;
        float age;

        void Awake()
        {
            mesh = new Mesh { name = "Chime Pop" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            Draw(0f);
        }

        void OnDestroy()
        {
            Destroy(mesh);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= seconds)
            {
                Destroy(gameObject);
                return;
            }

            Draw(age / seconds);
        }

        void Draw(float progress)
        {
            // Ease out: the circle leaps outwards and slows, like a struck note decaying.
            float eased = 1f - (1f - progress) * (1f - progress);
            float outer = Mathf.Lerp(radius.x, radius.y, eased);
            float inner = outer - thickness * (1f - progress);
            builder.Clear();
            builder.AddArc(Vector2.zero, inner, outer, 0f, 360f, Segments, Palette.ChimeCyan);
            builder.ApplyTo(mesh);
        }
    }
}
