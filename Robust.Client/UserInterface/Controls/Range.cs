﻿using System;
using System.Diagnostics.Contracts;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using Robust.Shared.ViewVariables;

namespace Robust.Client.UserInterface.Controls
{
    [ControlWrap("Range")]
    public abstract class Range : Control
    {
        private float _maxValue = 100;
        private float _minValue;
        private float _value;
        private float _page;

        public Range()
        {
        }

        public Range(string name) : base(name)
        {
        }

        public event Action<Range> OnValueChanged;

        public float GetAsRatio()
        {
            return (_value - _minValue) / (_maxValue - _minValue);
        }

        [ViewVariables]
        public float Page
        {
            get => _page;
            set
            {
                _page = value;
                _ensureValueClamped();
            }
        }

        [ViewVariables]
        public float MaxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = value;
                _ensureValueClamped();
            }
        }

        [ViewVariables]
        public float MinValue
        {
            get => _minValue;
            set
            {
                _minValue = value;
                _ensureValueClamped();
            }
        }

        [ViewVariables]
        public float Value
        {
            get => _value;
            set
            {
                var newValue = ClampValue(value);
                if (!FloatMath.CloseTo(newValue, _value))
                {
                    _value = newValue;
                    OnValueChanged?.Invoke(this);
                }
            }
        }

        private void _ensureValueClamped()
        {
            var newValue = ClampValue(_value);
            if (!FloatMath.CloseTo(newValue, _value))
            {
                _value = newValue;
                OnValueChanged?.Invoke(this);
            }
        }

        [Pure]
        protected float ClampValue(float value)
        {
            return value.Clamp(_minValue, _maxValue-_page);
        }

        private protected override void SetGodotProperty(string property, object value, GodotAssetScene context)
        {
            base.SetGodotProperty(property, value, context);

            if (property == "max_value")
            {
                MaxValue = (float) value;
            }

            if (property == "min_value")
            {
                MinValue = (float) value;
            }

            if (property == "value")
            {
                Value = (float) value;
            }

            if (property == "page")
            {
                Page = (float) value;
            }
        }
    }
}
