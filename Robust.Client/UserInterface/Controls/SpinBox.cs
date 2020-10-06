﻿using Robust.Shared.Maths;
using System;
using System.Collections.Generic;

namespace Robust.Client.UserInterface.Controls
{
    /// <summary>
    ///     Number input LineEdit with increment buttons.
    /// </summary>
    public class SpinBox : HBoxContainer
    {
        private LineEdit _lineEdit;
        private List<Button> _leftButtons = new List<Button>();
        private List<Button> _rightButtons = new List<Button>();
        private int _stepSize = 1;

        /// <summary>
        ///     Determines whether the SpinBox value gets changed by the input text.
        /// </summary>
        public Func<int, bool>? IsValid { get; set; }

        public int Value
        {
            get => int.TryParse(_lineEdit.Text, out int i) ? i : 0;
            set
            {
                if (IsValid != null && !IsValid(value))
                {
                    return;
                }
                _lineEdit.Text = value.ToString();
                ValueChanged?.Invoke(this, new ValueChangedEventArgs(value));
            }
        }

        /// <summary>
        /// Overrides the value of the spinbox without calling the event.
        /// Still applies validity-checks
        /// </summary>
        /// <param name="value">the new value</param>
        public void OverrideValue(int value)
        {
            if (IsValid != null && !IsValid(value))
            {
                return;
            }
            _lineEdit.Text = value.ToString();
        }

        public event EventHandler<ValueChangedEventArgs> ValueChanged = null!;

        public SpinBox() : base()
        {
            MouseFilter = MouseFilterMode.Pass;

            _lineEdit = new LineEdit
            {
                CustomMinimumSize = new Vector2(40, 0),
                SizeFlagsHorizontal = SizeFlags.FillExpand
            };
            AddChild(_lineEdit);

            Value = 0;

            _lineEdit.IsValid = (str) => int.TryParse(str, out var i) && (IsValid == null || IsValid(i));
        }

        /// <summary>
        ///     Creates and sets buttons to - and +.
        /// </summary>
        public void InitDefaultButtons()
        {
            ClearButtons();
            AddLeftButton(-1, "-");
            AddRightButton(1, "+");
        }

        /// <summary>
        ///     Adds a button to the right of the SpinBox LineEdit.
        /// </summary>
        public void AddRightButton(int num, string text)
        {
            var button = new Button { Text = text };
            button.OnPressed += (args) => Value += num;
            AddChild(button);
            _rightButtons.Add(button);
        }

        /// <summary>
        ///     Adds a button to the left of the SpinBox LineEdit.
        /// </summary>
        public void AddLeftButton(int num, string text)
        {
            var button = new Button { Text = text };
            button.OnPressed += (args) => Value += num;
            AddChild(button);
            button.SetPositionInParent(_leftButtons.Count);
            _leftButtons.Add(button);
        }

        /// <summary>
        ///     Creates and sets buttons for each int in leftButtons and rightButtons.
        /// </summary>
        public void SetButtons(List<int> leftButtons, List<int> rightButtons)
        {
            ClearButtons();
            foreach (var num in leftButtons)
            {
                AddLeftButton(num, num.ToString());
            }
            foreach (var num in rightButtons)
            {
                AddRightButton(num, num.ToString());
            }
        }

        /// <summary>
        /// Changes the editability of the lineedit-field
        /// </summary>
        /// <param name="disabled"></param>
        public void SetLineEditDisabled(bool disabled)
        {
            _lineEdit.Editable = !disabled;
        }

        /// <summary>
        /// Changes the editability of the buttons
        /// </summary>
        /// <param name="disabled"></param>
        public void SetButtonDisabled(bool disabled)
        {
            foreach (var leftButton in _leftButtons)
            {
                leftButton.Disabled = disabled;
            }

            foreach (var rightButton in _rightButtons)
            {
                rightButton.Disabled = disabled;
            }
        }

        /// <summary>
        ///     Removes all buttons inside the SpinBox.
        /// </summary>
        public void ClearButtons()
        {
            foreach (var button in _leftButtons)
            {
                button.Dispose();
            }
            _leftButtons.Clear();
            foreach (var button in _rightButtons)
            {
                button.Dispose();
            }
            _rightButtons.Clear();
        }

        protected internal override void MouseWheel(GUIMouseWheelEventArgs args)
        {
            base.MouseWheel(args);

            if (!_lineEdit.HasKeyboardFocus())
            {
                return;
            }

            if (args.Delta.Y > 0)
                Value += _stepSize;
            else if (args.Delta.Y < 0)
                Value -= _stepSize;
        }
    }

    public class ValueChangedEventArgs : EventArgs
    {
        public readonly int Value;

        public ValueChangedEventArgs(int value)
        {
            Value = value;
        }
    }
}
