﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.ViewVariables;

namespace Robust.Client.GameObjects.Components.Containers
{
    public sealed partial class ContainerManagerComponent
    {
        [DebuggerDisplay("ClientContainer {Owner.Uid}/{ID}")]
        private sealed class ClientContainer : IContainer
        {
            public List<IEntity> Entities { get; } = new List<IEntity>();

            public ClientContainer(string id, ContainerManagerComponent manager)
            {
                ID = id;
                Manager = manager;
            }

            [ViewVariables] public IContainerManager Manager { get; }
            [ViewVariables] public string ID { get; }
            [ViewVariables] public IEntity Owner => Manager.Owner;
            [ViewVariables] public bool Deleted { get; private set; }
            [ViewVariables] public IReadOnlyList<IEntity> ContainedEntities => Entities;
            public bool ShowContents { get; set; }

            public bool CanInsert(IEntity toinsert)
            {
                return false;
            }

            public bool Insert(IEntity toinsert)
            {
                return false;
            }

            public bool CanRemove(IEntity toremove)
            {
                return false;
            }

            public bool Remove(IEntity toremove)
            {
                return false;
            }

            public void ForceRemove(IEntity toRemove)
            {
                throw new NotSupportedException("Cannot directly modify containers on the client");
            }

            public bool Contains(IEntity contained)
            {
                return Entities.Contains(contained);
            }

            public void DoInsert(IEntity entity)
            {
                Entities.Add(entity);

                Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new UpdateContainerOcclusionMessage(entity));
            }

            public void DoRemove(IEntity entity)
            {
                Entities.Remove(entity);

                Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new UpdateContainerOcclusionMessage(entity));
            }

            public void Shutdown()
            {
                Deleted = true;
            }
        }
    }
}
