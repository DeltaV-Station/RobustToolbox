namespace Robust.Shared.GameObjects
{

    /// <summary>
    /// Arguments for an event related to a component.
    /// </summary>
    public readonly struct ComponentEventArgs
    {
        /// <summary>
        /// Component that this event relates to.
        /// </summary>
        public Component Component { get; }

        /// <summary>
        /// EntityUid of the entity this component belongs to.
        /// </summary>
        public EntityUid Owner { get; }

        /// <summary>
        /// Constructs a new instance of <see cref="ComponentEventArgs"/>.
        /// </summary>
        /// <param name="component">The relevant component</param>
        /// <param name="owner">EntityUid of the entity this component belongs to.</param>
        public ComponentEventArgs(Component component, EntityUid owner)
        {
            Component = component;
            Owner = owner;
        }
    }
}
