namespace MusicInYourHands
{
    /// <summary>
    /// Whether the world that is on wants to be hidden by the real room (<see cref="World.HiddenByRealRoom"/>): real
    /// furniture, people and your real hands in front of its things hide them, as they would hide anything real. World
    /// sets it as a world starts and clears it as it stops. The Meta feed (RoomDepthFeed) runs the headset's depth
    /// sensing only while it is set, and turns it off again for every other world, so they pay nothing for it.
    /// </summary>
    public static class RoomDepth
    {
        /// <summary>Set while a world that is hidden by the real room is on.</summary>
        public static bool Wanted;
    }
}
