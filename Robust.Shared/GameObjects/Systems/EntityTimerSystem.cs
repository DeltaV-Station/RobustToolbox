﻿
using System;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Robust.Shared.GameObjects;

/// <summary>
/// This handles spawning timer entities used for delaying directed event raises.
/// </summary>
public sealed class EntityTimerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private EntityQuery<RepeatingEntityTimerComponent> _repeatingQuery = default!;

    public override void Initialize()
    {
        _repeatingQuery = GetEntityQuery<RepeatingEntityTimerComponent>();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<EntityTimerComponent>();

        while (query.MoveNext(out var uid, out var timer))
        {
            if (_timing.CurTime < timer.AbsoluteTime)
                continue;

            var target = _xform.GetParentUid(uid);
            RaiseLocalEvent(target, timer.Event);

            // deletion behavior based on type
            if (!_repeatingQuery.TryGetComponent(uid, out var repeating))
            {
                QueueDel(uid);
                continue;
            }

            if (repeating.TotalRepetitions >= repeating.MaxRepetitions)
            {
                QueueDel(uid);
                continue;
            }

            repeating.TotalRepetitions += 1;
            timer.AbsoluteTime = _timing.CurTime + repeating.Delay;
        }
    }

    private bool CheckAttachedEntityValid([NotNullWhen(true)] EntityUid? uid)
    {
        // Entity timers should not be getting attached to entities
        // which are either being deleted, are deleted, or are not yet
        // map initialized.
        return uid is { } attachedTo
               && LifeStage(attachedTo) == EntityLifeStage.MapInitialized;
    }

    public EntityUid? SpawnEntityTimer<TEvent>(
        EntityUid? attachedTo,
        TimeSpan delay,
        TEvent args
        ) where TEvent: BaseEntityTimerEvent
    {
        if (!CheckAttachedEntityValid(attachedTo))
            return null;

        // Timer ent will have the same lifetime as the entity it is attached to.
        var timerEnt = Spawn();

        // debug purposes
        _metaData.SetEntityName(timerEnt, $"{args.GetType().Name} Timer - {delay.TotalMilliseconds:F} millis");

        // TODO not sure how pause should be handled exactly for this use case
        AddComp(timerEnt, new EntityTimerComponent
        {
            Event = new EntityTimerEvent<TEvent> { Data = args, Timer = timerEnt },
            AbsoluteTime = _timing.CurTime - _metaData.GetPauseTime(attachedTo.Value) + delay,
        });

        _xform.SetParent(timerEnt, attachedTo.Value);

        return timerEnt;
    }

    public EntityUid? SpawnRepeatingEntityTimer<TEvent>(
        EntityUid? attachedTo,
        TimeSpan delay,
        TEvent args,
        int maxRepetitions = int.MaxValue
    ) where TEvent: BaseEntityTimerEvent
    {
        if (!CheckAttachedEntityValid(attachedTo))
            return null;

        // Timer ent will have the same lifetime as the entity it is attached to.
        var timerEnt = Spawn();

        // debug purposes
        _metaData.SetEntityName(timerEnt, $"{args.GetType().Name} Repeating Timer - {delay.TotalMilliseconds:F} millis");

        AddComp(timerEnt, new EntityTimerComponent
        {
            Event = new RepeatingEntityTimerEvent<TEvent> { Data = args, Timer = timerEnt },
            AbsoluteTime = _timing.CurTime - _metaData.GetPauseTime(attachedTo.Value) + delay,
        });

        AddComp(timerEnt, new RepeatingEntityTimerComponent
        {
            MaxRepetitions = maxRepetitions,
            NextDelay = delay
        });

        return timerEnt;
    }
}
