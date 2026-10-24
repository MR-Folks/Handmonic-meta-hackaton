using UnityEngine;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// Opens and closes the world menu with Meta's menu button: with hand tracking, that is the left-hand gesture
    /// (palm facing you, pinch); with controllers, the menu button. The right-hand gesture stays Meta's own.
    /// </summary>
    public class MenuButtonFeed : MonoBehaviour
    {
        void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.Start)) WorldMenu.RequestToggle();
        }
    }
}
