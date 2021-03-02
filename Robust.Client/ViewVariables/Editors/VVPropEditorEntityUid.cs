﻿using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Robust.Client.ViewVariables.Editors
{
    public class VVPropEditorEntityUid : VVPropEditor
    {
        protected override Control MakeUI(object? value)
        {
            var hBox = new HBoxContainer
            {
                MinSize = new Vector2(200, 0)
            };

            var uid = (EntityUid)value!;
            var lineEdit = new LineEdit
            {
                Text = uid.ToString(),
                Editable = !ReadOnly,
                HorizontalExpand = true,
            };
            if (!ReadOnly)
            {
                lineEdit.OnTextEntered += e =>
                    ValueChanged(EntityUid.Parse(e.Text));
            }

            hBox.AddChild(lineEdit);
            return hBox;
        }
    }
}
