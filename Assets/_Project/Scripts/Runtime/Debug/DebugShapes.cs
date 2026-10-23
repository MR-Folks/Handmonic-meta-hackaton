using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>Helpers shared by the debug views.</summary>
    static class DebugShapes
    {
        /// <summary>Primitives come with colliders the debug shapes never need.</summary>
        public static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
    }
}
