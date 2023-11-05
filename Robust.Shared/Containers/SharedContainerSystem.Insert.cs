using Robust.Shared.GameObjects;

namespace Robust.Shared.Containers;

public abstract partial class SharedContainerSystem
{
    /// <summary>
    /// Checks if the entity can be inserted into the given container.
    /// </summary>
    /// <param name="assumeEmpty">If true, this will check whether the entity could be inserted if the container were
    /// empty.</param>
    public bool CanInsert(
        EntityUid toInsert,
        BaseContainer container,
        bool assumeEmpty = false)
    {
        return CanInsert(toInsert, container.Owner, container, assumeEmpty);
    }

    /// <summary>
    /// Checks if the entity can be inserted into the given container.
    /// </summary>
    /// <param name="assumeEmpty">If true, this will check whether the entity could be inserted if the container were
    /// empty.</param>
    public bool CanInsert(
        EntityUid toInsert,
        Entity<TransformComponent?> containerEntity,
        BaseContainer container,
        bool assumeEmpty = false)
    {
        if (container.Owner == toInsert)
            return false;

        if (!assumeEmpty && container.Contains(toInsert))
            return false;

        if (!container.CanInsert(toInsert, assumeEmpty, EntityManager))
            return false;

        // no, you can't put maps or grids into containers
        if (_mapQuery.HasComponent(toInsert) || _gridQuery.HasComponent(toInsert))
            return false;

        // Prevent circular insertion.
        if (_transform.ContainsEntity(toInsert, containerEntity))
            return false;

        var insertAttemptEvent = new ContainerIsInsertingAttemptEvent(container, toInsert, assumeEmpty);
        RaiseLocalEvent(container.Owner, insertAttemptEvent, true);
        if (insertAttemptEvent.Cancelled)
            return false;

        var gettingInsertedAttemptEvent = new ContainerGettingInsertedAttemptEvent(container, toInsert, assumeEmpty);
        RaiseLocalEvent(toInsert, gettingInsertedAttemptEvent, true);

        return !gettingInsertedAttemptEvent.Cancelled;
    }
}
