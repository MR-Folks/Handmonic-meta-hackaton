using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// One world the player can pick from the wheel. WorldManager switches worlds by turning their GameObjects on
    /// and off, so a world sets itself up in OnEnable and must undo anything it changed outside itself in OnDisable.
    /// </summary>
    public abstract class World : MonoBehaviour
    {
        [SerializeField] string displayName = "World";
        [Tooltip("The world's orb colour on the wheel.")]
        [SerializeField] Color color = Color.white;

        public string DisplayName => displayName;
        public Color Color => color;
    }
}
