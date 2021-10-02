using Robust.Shared.Maths;

namespace Robust.Client.Graphics
{
    public interface ILightManager
    {
        /// <summary>
        /// Enables/disables the entire light manager.
        /// </summary>
        bool Enabled { get; set; }
        /// <summary>
        /// Enables/disables shadows, but lights are still functional.
        /// </summary>
        bool DrawShadows { get; set; }
        /// <summary>
        /// Enables/disables hard FOV.
        /// </summary>
        bool DrawHardFov { get; set; }
        /// <summary>
        /// Enables/disables everything to do with the lighting buffer, without interfering with hard FOV.
        /// </summary>
        bool DrawLighting { get; set; }
        /// <summary>
        /// Minimum radius at which to draw lights with shadow casting. Helps with performance to opt out of shadow
        /// casting for very small lights. This isn't a cvar since setting it high enough is essentially equivalent
        /// to turning off shadows.
        /// </summary>
        float MinimumShadowCastLightRadius { get; set; }
        /// <summary>
        /// This is useful to prevent players messing with lighting setup when they shouldn't.
        /// </summary>
        bool LockConsoleAccess { get; set; }
        /// <summary>
        /// Ambient light. This is in linear-light, i.e. when providing a fixed colour, you must use Color.FromSrgb(Color.Black)!
        /// </summary>
        Color AmbientLightColor { get; set; }
    }
}
