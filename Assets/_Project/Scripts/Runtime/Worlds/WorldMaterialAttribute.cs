using System;

namespace MusicInYourHands
{
    /// <summary>
    /// Names the material a world's Material field gets when the project is set up, when it isn't the field's own
    /// name. Configure Project fills glowMaterial with Materials/Glow.mat (shader MusicInYourHands/Glow), lineMaterial
    /// with Line.mat, and so on; [WorldMaterial("OrbDots")] on dotsMaterial gives it Materials/OrbDots.mat instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class WorldMaterialAttribute : Attribute
    {
        public string Name { get; }

        public WorldMaterialAttribute(string name) => Name = name;
    }
}
