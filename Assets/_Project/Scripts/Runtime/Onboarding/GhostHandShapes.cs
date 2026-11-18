using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The thumb shapes a ghost hand finds for itself (see <see cref="GhostHand"/>), stored on the ghost hand in
    /// Resources/Onboarding by the editor setup, so the headset doesn't spend its first frames searching for them. A
    /// solver version tells old shapes apart; without matching ones the hand solves them itself.
    /// </summary>
    public sealed class GhostHandShapes : MonoBehaviour
    {
        [SerializeField] int version;
        [SerializeField] Vector3[] thumb = new Vector3[0];
        [SerializeField] float pinchIndex;

        public bool Has(int solver, int count) => version == solver && thumb != null && thumb.Length == count;

        public Vector3 Thumb(int shape) => thumb[shape];

        public float PinchIndex => pinchIndex;

        public void Store(int solver, Vector3[] shapes, float pinch)
        {
            version = solver;
            thumb = (Vector3[])shapes.Clone();
            pinchIndex = pinch;
        }
    }
}
