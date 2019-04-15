﻿using Robust.Shared.GameObjects.Systems;
using Robust.Shared.GameObjects;

namespace Robust.Client.GameObjects.EntitySystems
{
    sealed class AppearanceSystem : EntitySystem
    {
        public AppearanceSystem()
        {
            EntityQuery = new TypeEntityQuery(typeof(AppearanceComponent));
        }

        public override void FrameUpdate(float frameTime)
        {
            foreach (var entity in RelevantEntities)
            {
                var component = entity.GetComponent<AppearanceComponent>();
                if (component.AppearanceDirty)
                {
                    UpdateComponent(component);
                    component.AppearanceDirty = false;
                }
            }
        }

        static void UpdateComponent(AppearanceComponent component)
        {
            foreach (var visualizer in component.Visualizers)
            {
                switch (visualizer)
                {
                    case AppearanceComponent.SpriteLayerToggle spriteLayerToggle:
                        UpdateSpriteLayerToggle(component, spriteLayerToggle);
                        break;

                    default:
                        visualizer.OnChangeData(component);
                        break;
                }
            }
        }

        static void UpdateSpriteLayerToggle(AppearanceComponent component, AppearanceComponent.SpriteLayerToggle toggle)
        {
            component.TryGetData(toggle.Key, out bool visible);
            var sprite = component.Owner.GetComponent<SpriteComponent>();
            sprite.LayerSetVisible(toggle.SpriteLayer, visible);
        }
    }
}
