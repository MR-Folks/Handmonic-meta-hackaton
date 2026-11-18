using UnityEngine;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// Tells the onboarding when the player is holding Touch controllers rather than using their hands, so its caption
    /// can ask them to put the controllers down.
    /// </summary>
    static class OnboardingControllers
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => Onboarding.ControllersInUse = InUse;

        static bool InUse() => (OVRInput.GetActiveController() & OVRInput.Controller.Touch) != 0;
    }
}
