using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// Tells the game which section of the level the player is in. One trigger volume covers the whole level; while the
    /// player is inside it, its x position is looked up in the <see cref="Sections"/> table and
    /// <see cref="GameEvents.SectionEntered"/> is raised whenever the answer changes – and once when a run starts. The
    /// music follows this event; nothing here knows that.
    /// </summary>
    public sealed class SectionTrigger : MonoBehaviour
    {
        [Tooltip("The section the run starts in, announced when the game starts before the player has been seen.")]
        [SerializeField] SectionId startSection = SectionId.SoftStart;

        PlayerBase player;
        SectionId current;
        bool running;

        /// <summary>Sets the section the run starts in; called by the level's construction code.</summary>
        public void Configure(SectionId sectionAtSpawn)
        {
            startSection = sectionAtSpawn;
        }

        void OnEnable() => GameEvents.GameStarted += AnnounceStart;

        void OnDisable() => GameEvents.GameStarted -= AnnounceStart;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerBase entered)) player = entered;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerBase left) && left == player) player = null;
        }

        void Update()
        {
            if (!running || player == null) return;
            SectionId section = Sections.At(player.Position.x);
            if (section == current) return;
            current = section;
            GameEvents.RaiseSectionEntered(current);
        }

        void AnnounceStart()
        {
            running = true;
            current = player != null ? Sections.At(player.Position.x) : startSection;
            GameEvents.RaiseSectionEntered(current);
        }
    }
}
