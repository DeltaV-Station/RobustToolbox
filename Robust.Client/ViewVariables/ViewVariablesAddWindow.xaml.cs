using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Localization;

namespace Robust.Client.ViewVariables
{
    [GenerateTypedNameReferences]
    public sealed partial class ViewVariablesAddWindow : DefaultWindow
    {
        private string? _lastSearch;
        private string[] _entries = Array.Empty<string>();

        public event Action<AddButtonPressedEventArgs>? AddButtonPressed;

        public ViewVariablesAddWindow(IEnumerable<string> entries, string title)
        {
            RobustXamlLoader.Load(this);

            Title = Loc.GetString(title);

            EntryItemList.OnItemSelected += _ => RefreshAddButton();
            EntryItemList.OnItemDeselected += _ => RefreshAddButton();
            SearchLineEdit.OnTextChanged += OnSearchTextChanged;
            AddButton.OnPressed += OnAddButtonPressed;

            Populate(entries);

            SetSize = (200, 300);
        }

        private void RefreshAddButton()
        {
            AddButton.Disabled = !EntryItemList.GetSelected().Any();
        }

        public void Populate(IEnumerable<string> components)
        {
            _entries = components.ToArray();
            Array.Sort(_entries);
            Populate(_lastSearch);
        }

        private void Populate(string? search = null)
        {
            _lastSearch = search;
            EntryItemList.ClearSelected();
            EntryItemList.Clear();
            AddButton.Disabled = true;

            foreach (var component in _entries)
            {
                if(!string.IsNullOrEmpty(search) && !component.Contains(search, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                EntryItemList.AddItem(component);
            }
        }

        private void OnSearchTextChanged(LineEdit.LineEditEventArgs obj)
        {
            Populate(obj.Text);
        }

        private void OnAddButtonPressed(BaseButton.ButtonEventArgs obj)
        {
            var selected = EntryItemList.GetSelected().ToArray();

            // Nothing to do here!
            if (selected.Length == 0)
                return;

            var comp = selected[0];

            // This shouldn't really happen.
            if (comp.Text == null)
                return;

            AddButtonPressed?.Invoke(new AddButtonPressedEventArgs(comp.Text));
        }

        public sealed class AddButtonPressedEventArgs : EventArgs
        {
            public string Entry { get; }

            public AddButtonPressedEventArgs(string entry)
            {
                Entry = entry;
            }
        }
    }
}
