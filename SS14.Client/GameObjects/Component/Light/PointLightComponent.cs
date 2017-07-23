﻿using Lidgren.Network;
using SFML.System;
using SS14.Client.Interfaces.Lighting;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.Components;
using SS14.Shared.GameObjects.Components.Light;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.IoC;
using SS14.Shared.Utility;
using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace SS14.Client.GameObjects
{
    public class PointLightComponent : ClientComponent
    {
        public override string Name => "PointLight";
        public override uint? NetID => NetIDs.POINT_LIGHT;
        //Contains a standard light
        public ILight _light;
        public Vector3f _lightColor = new Vector3f(190, 190, 190);
        public Vector2f _lightOffset = new Vector2f(0, 0);
        public int _lightRadius = 512;
        protected string _mask = "";
        public LightModeClass _mode = LightModeClass.Constant;

        public override Type StateType => typeof(PointLightComponentState);

        //When added, set up the light.
        public override void OnAdd(IEntity owner)
        {
            base.OnAdd(owner);

            _light = IoCManager.Resolve<ILightManager>().CreateLight();
            IoCManager.Resolve<ILightManager>().AddLight(_light);

            _light.SetRadius(_lightRadius);
            _light.SetColor(255, (int)_lightColor.X, (int)_lightColor.Y, (int)_lightColor.Z);
            _light.Move(Owner.GetComponent<TransformComponent>().Position + _lightOffset);
            _light.SetMask(_mask);
            Owner.GetComponent<TransformComponent>().OnMove += OnMove;
        }

        public override void LoadParameters(YamlMappingNode mapping)
        {
            YamlNode node;
            if (mapping.TryGetNode("lightoffsetx", out node))
            {
                _lightOffset.X = node.AsFloat();
            }

            if (mapping.TryGetNode("lightoffsety", out node))
            {
                _lightOffset.Y = node.AsFloat();
            }

            if (mapping.TryGetNode("lightradius", out node))
            {
                _lightRadius = node.AsInt();
            }

            if (mapping.TryGetNode("lightColorR", out node))
            {
                _lightColor.X = node.AsInt();
            }

            if (mapping.TryGetNode("lightColorG", out node))
            {
                _lightColor.Y = node.AsInt();
            }

            if (mapping.TryGetNode("lightColorB", out node))
            {
                _lightColor.Z = node.AsInt();
            }

            if (mapping.TryGetNode("mask", out node))
            {
                _mask = node.AsString();
            }
        }

        protected void SetState(LightState state)
        {
            _light.SetState(state);
        }

        protected void SetMode(LightModeClass mode)
        {
            IoCManager.Resolve<ILightManager>().SetLightMode(mode, _light);
        }

        public override void OnRemove()
        {
            Owner.GetComponent<TransformComponent>().OnMove -= OnMove;
            IoCManager.Resolve<ILightManager>().RemoveLight(_light);
            base.OnRemove();
        }

        private void OnMove(object sender, VectorEventArgs args)
        {
            _light.Move(Owner.GetComponent<TransformComponent>().Position + _lightOffset);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            _light.Update(frameTime);
        }

        protected void SetMask(string mask)
        {
            _light.SetMask(mask);
        }

        public override void HandleComponentState(dynamic state)
        {
            if (_light.LightState != state.State)
                _light.SetState(state.State);
            if (_light.Color.R != state.ColorR || _light.Color.G != state.ColorG || _light.Color.B != state.ColorB)
            {
                SetColor(state.ColorR, state.ColorG, state.ColorB);
            }
            if (_mode != state.Mode)
                SetMode(state.Mode);
        }

        protected void SetColor(int R, int G, int B)
        {
            _lightColor.X = R;
            _lightColor.Y = G;
            _lightColor.Z = B;
            _light.SetColor(255, R, G, B);
        }
    }
}
