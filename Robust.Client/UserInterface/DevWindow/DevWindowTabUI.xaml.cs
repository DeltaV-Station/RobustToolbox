﻿using System;
using Robust.Client.AutoGenerated;
using Robust.Client.Console.Commands;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface
{
    [GenerateTypedNameReferences]
    public sealed partial class DevWindowTabUI : Control
    {
        public Control? SelectedControl { get; private set; }

        public event Action? SelectedControlChanged;

        public DevWindowTabUI()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            RobustXamlLoader.Load(this);

            ControlTreeRoot.OnKeyBindDown += ControlTreeRootOnOnKeyBindDown;
            RefreshPropertiesButton.OnPressed += _ => Refresh();
        }

        private void ControlTreeRootOnOnKeyBindDown(GUIBoundKeyEventArgs obj)
        {
            if (obj.Function != EngineKeyFunctions.UIClick)
                return;

            obj.Handle();
            SelectControl(null);
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();

            // Load tree roots.
            foreach (var root in UserInterfaceManager.AllRoots)
            {
                var entry = new DevWindowUITreeEntry(this, root);

                ControlTreeRoot.AddChild(entry);
            }

            UserInterfaceManager.OnPostDrawUIRoot += OnPostDrawUIRoot;
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            // Clear tree children.
            ControlTreeRoot.RemoveAllChildren();
            UserInterfaceManager.OnPostDrawUIRoot -= OnPostDrawUIRoot;
        }

        private void OnPostDrawUIRoot(PostDrawUIRootEventArgs eventArgs)
        {
            if (SelectedControl == null || eventArgs.Root != SelectedControl.Root)
                return;

            var rect = UIBox2i.FromDimensions(SelectedControl.GlobalPixelPosition, SelectedControl.PixelSize);
            eventArgs.DrawingHandle.DrawRect(rect, Color.Cyan.WithAlpha(0.35f));
        }

        public void EntryRemoved(DevWindowUITreeEntry entry)
        {
            if (SelectedControl == entry.VisControl)
                SelectControl(null);
        }

        public void SelectControl(Control? control)
        {
            SelectedControl = control;

            SelectedControlChanged?.Invoke();

            Refresh();
        }


        private void Refresh()
        {
            ControlProperties.RemoveAllChildren();

            if (SelectedControl == null)
                return;

            var props = GuiDumpCommand.PropertyValuesFor(SelectedControl);
            foreach (var (prop, value) in props)
            {
                ControlProperties.AddChild(new Label { Text = prop });
                ControlProperties.AddChild(new Label { Text = value });
            }
        }
    }
}
