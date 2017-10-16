﻿using System;
using System.Windows.Forms;
using OpenTK.Graphics;
using SFML.Graphics;
using SFML.Window;
using SS14.Client.Graphics;
using SS14.Client.Graphics.Sprite;
using SS14.Client.Graphics.Utility;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Interfaces.UserInterface;
using SS14.Client.ResourceManagement;
using SS14.Shared.IoC;
using SS14.Shared.Maths;
using KeyEventArgs = SFML.Window.KeyEventArgs;

namespace SS14.Client.UserInterface.Components
{
    internal class Textbox : GuiComponent
    {
        public delegate void TextSubmitHandler(string text, Textbox sender);

        private const float CaretHeight = 12;
        private const float CaretWidth = 2;
        private int _caretIndex;
        private float _caretPos;

        private Box2i _clientAreaLeft;
        private Box2i _clientAreaMain;
        private Box2i _clientAreaRight;

        private int _displayIndex;

        private string _displayText = "";
        private string _text = "";
        private Sprite _textboxLeft;
        private Sprite _textboxMain;
        private Sprite _textboxRight;

        private float blinkCount;

        public bool ClearFocusOnSubmit = true;
        public bool ClearOnSubmit = true;

        public Color4 drawColor = Color4.White;

        // Terrible hack to get around TextEntered AND KeyDown firing at once.
        // Set to true after handling a KeyDown that produces a character to this.
        public bool ignoreNextText;

        public int MaxCharacters = 255;
        public Color4 textColor = Color4.Black;

        public TextSprite TextSprite { get; private set; }
        public int Width;

        public bool AllowEmptySubmit { get; set; } = true;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                SetVisibleText();
            }
        }

        public Textbox(int width, IResourceCache resourceCache)
        {
            _textboxLeft = resourceCache.GetSprite("text_left");
            _textboxMain = resourceCache.GetSprite("text_middle");
            _textboxRight = resourceCache.GetSprite("text_right");

            Width = width;

            TextSprite = new TextSprite("", resourceCache.GetResource<FontResource>(@"Fonts/CALIBRI.TTF").Font) {Color = Color4.Black};

            Update(0);
        }

        public event TextSubmitHandler OnSubmit;

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (Focus)
            {
                blinkCount += 1 * frameTime;
                if (blinkCount > 0.50f) blinkCount = 0;
            }
        }

        /// <inheritdoc />
        protected override void OnCalcRect()
        {
            var boundsLeft = _textboxLeft.GetLocalBounds();
            var boundsMain = _textboxMain.GetLocalBounds();
            var boundsRight = _textboxRight.GetLocalBounds();

            _clientAreaLeft = Box2i.FromDimensions(new Vector2i(), new Vector2i((int) boundsLeft.Width, (int) boundsLeft.Height));
            _clientAreaMain = Box2i.FromDimensions(_clientAreaLeft.Right, 0, Width, (int) boundsMain.Height);
            _clientAreaRight = Box2i.FromDimensions(_clientAreaMain.Right, 0, (int) boundsRight.Width, (int) boundsRight.Height);

            _clientArea = Box2i.FromDimensions(new Vector2i(),
                new Vector2i(_clientAreaLeft.Width + _clientAreaMain.Width + _clientAreaRight.Width,
                    Math.Max(Math.Max(_clientAreaLeft.Height, _clientAreaRight.Height),
                        _clientAreaMain.Height)));
        }

        /// <inheritdoc />
        protected override void OnCalcPosition()
        {
            base.OnCalcPosition();

            SetVisibleText();
            TextSprite.Position = Position + new Vector2i(_clientAreaMain.Left, (int) (_clientArea.Height / 2f) - (int) (TextSprite.Height / 2f));
        }

        /// <inheritdoc />
        public override void Render()
        {
            if (drawColor != Color4.White)
            {
                _textboxLeft.Color = drawColor.Convert();
                _textboxMain.Color = drawColor.Convert();
                _textboxRight.Color = drawColor.Convert();
            }

            _textboxLeft.SetTransformToRect(_clientAreaLeft.Translated(Position));
            _textboxMain.SetTransformToRect(_clientAreaMain.Translated(Position));
            _textboxRight.SetTransformToRect(_clientAreaRight.Translated(Position));
            _textboxLeft.Draw();
            _textboxMain.Draw();
            _textboxRight.Draw();

            if (Focus && blinkCount <= 0.25f)
                CluwneLib.drawRectangle(TextSprite.Position.X + _caretPos - CaretWidth, TextSprite.Position.Y + TextSprite.Height / 2f - CaretHeight / 2f, CaretWidth, CaretHeight, new Color4(255, 255, 250, 255));

            if (drawColor != Color4.White)
            {
                _textboxLeft.Color = Color.White;
                _textboxMain.Color = Color.White;
                _textboxRight.Color = Color.White;
            }

            TextSprite.Color = textColor;
            TextSprite.Text = _displayText;

            TextSprite.Draw();

            base.Render();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            TextSprite = null;
            _textboxLeft = null;
            _textboxMain = null;
            _textboxRight = null;
            OnSubmit = null;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override bool MouseDown(MouseButtonEventArgs e)
        {
            if (base.MouseDown(e))
                return true;

            if (ClientArea.Translated(Position).Contains(e.X, e.Y))
                return true;

            return false;
        }

        public override bool KeyDown(KeyEventArgs e)
        {
            if (base.KeyDown(e))
                return true;

            if (!Focus) return false;

            if (e.Control && e.Code == Keyboard.Key.V)
            {
                var ret = Clipboard.GetText();
                Text = Text.Insert(_caretIndex, ret);
                if (_caretIndex < _text.Length) _caretIndex += ret.Length;
                SetVisibleText();
                ignoreNextText = true;
                return true;
            }

            if (e.Control && e.Code == Keyboard.Key.C)
            {
                Clipboard.SetText(Text);
                ignoreNextText = true;
                return true;
            }

            // Control + Backspace to delete all text
            if (e.Control && e.Code == Keyboard.Key.BackSpace && Text.Length >= 1)
            {
                Clear();
                ignoreNextText = true;
                return true;
            }

            if (e.Code == Keyboard.Key.Left)
            {
                if (_caretIndex > 0) _caretIndex--;
                SetVisibleText();
                return true;
            }
            if (e.Code == Keyboard.Key.Right)
            {
                if (_caretIndex < _text.Length) _caretIndex++;
                SetVisibleText();
                return true;
            }

            if (e.Code == Keyboard.Key.Return && (AllowEmptySubmit || Text.Length >= 1))
            {
                Submit();
                return true;
            }

            if (e.Code == Keyboard.Key.BackSpace && Text.Length >= 1)
            {
                if (_caretIndex == 0) return true;

                Text = Text.Remove(_caretIndex - 1, 1);
                if (_caretIndex > 0 && _caretIndex < Text.Length) _caretIndex--;
                SetVisibleText();
                return true;
            }

            if (e.Code == Keyboard.Key.Delete && Text.Length >= 1)
            {
                if (_caretIndex >= Text.Length) return true;
                Text = Text.Remove(_caretIndex, 1);
                SetVisibleText();
                return true;
            }

            return true;
        }

        public override bool TextEntered(TextEventArgs e)
        {
            if (base.TextEntered(e))
                return true;

            if (!Focus) return false;

            if (Text.Length >= MaxCharacters || "\b\n\u001b\r".Contains(e.Unicode))
                return false;

            if (ignoreNextText)
            {
                ignoreNextText = false;
                return false;
            }

            Text = Text.Insert(_caretIndex, e.Unicode);
            if (_caretIndex < _text.Length) _caretIndex++;
            SetVisibleText();
            return true;
        }

        private void SetVisibleText()
        {
            _displayText = "";

            if (TextSprite.MeasureLine(_text) >= _clientAreaMain.Width) //Text wider than box.
            {
                if (_caretIndex < _displayIndex)
                    //Caret outside to the left. Move display text to the left by setting its index to the caret.
                    _displayIndex = _caretIndex;

                var glyphCount = 0;

                while (_displayIndex + glyphCount + 1 < _text.Length &&
                       TextSprite.MeasureLine(Text.Substring(_displayIndex + 1, glyphCount + 1)) < _clientAreaMain.Width)
                {
                    glyphCount++; //How many glyphs we could/would draw with the current index.
                }

                if (_caretIndex > _displayIndex + glyphCount) //Caret outside?
                    if (_text.Substring(_displayIndex + 1).Length != glyphCount) //Still stuff outside the screen?
                    {
                        _displayIndex++;
                        //Increase display index by one since the carret is one outside to the right. But only if there's still letters to the right.

                        glyphCount = 0; //Update glyphcount with new index.

                        while (_displayIndex + glyphCount + 1 < _text.Length &&
                               TextSprite.MeasureLine(Text.Substring(_displayIndex + 1, glyphCount + 1)) <
                               _clientAreaMain.Width)
                        {
                            glyphCount++;
                        }
                    }
                _displayText = Text.Substring(_displayIndex + 1, glyphCount);

                _caretPos = TextSprite.Position.X + TextSprite.MeasureLine(Text.Substring(_displayIndex, _caretIndex - _displayIndex));
            }
            else //Text fits completely inside box.
            {
                _displayIndex = 0;
                _displayText = Text;

                if (Text.Length <= _caretIndex - 1)
                    _caretIndex = Text.Length;

                _caretPos = TextSprite.MeasureLine(Text.Substring(_displayIndex, _caretIndex - _displayIndex));
            }
        }

        private void Submit()
        {
            OnSubmit?.Invoke(Text, this);

            if (ClearOnSubmit)
                Clear();

            if (ClearFocusOnSubmit)
            {
                Focus = false;
                IoCManager.Resolve<IUserInterfaceManager>().RemoveFocus(this);
            }
        }

        public void Clear()
        {
            Text = string.Empty;
        }
    }
}
