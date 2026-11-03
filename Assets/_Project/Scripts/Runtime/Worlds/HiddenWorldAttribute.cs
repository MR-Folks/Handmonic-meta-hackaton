using System;

namespace MusicInYourHands
{
    /// <summary>A World class the menu and project setup leave out, such as the editor checks' test world.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HiddenWorldAttribute : Attribute { }
}
