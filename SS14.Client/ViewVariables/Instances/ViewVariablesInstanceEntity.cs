using System.Linq;
using SS14.Client.Graphics;
using SS14.Client.Interfaces.GameObjects.Components;
using SS14.Client.Interfaces.ResourceManagement;
using SS14.Client.ResourceManagement;
using SS14.Client.UserInterface;
using SS14.Client.UserInterface.Controls;
using SS14.Client.UserInterface.CustomControls;
using SS14.Client.ViewVariables.Editors;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.IoC;
using SS14.Shared.Log;
using SS14.Shared.Maths;
using SS14.Shared.ViewVariables;

namespace SS14.Client.ViewVariables.Instances
{
    internal class ViewVariablesInstanceEntity : ViewVariablesInstance
    {
        private const int TabClientVars = 0;
        private const int TabClientComponents = 1;
        private const int TabServerVars = 2;
        private const int TabServerComponents = 3;

        private TabContainer _tabs;
        private IEntity _entity;

        private ViewVariablesRemoteSession _entitySession;
        private ViewVariablesBlobEntity _blob;

        private VBoxContainer _serverVariables;
        private VBoxContainer _serverComponents;

        private bool _serverLoaded;

        public ViewVariablesInstanceEntity(IViewVariablesManagerInternal vvm) : base(vvm)
        {
        }

        public override void Initialize(SS14Window window, object obj)
        {
            _entity = (IEntity) obj;

            var type = obj.GetType();

            var vBoxContainer = new VBoxContainer();
            vBoxContainer.SetAnchorPreset(Control.LayoutPreset.Wide, true);

            // Handle top bar displaying type and ToString().
            {
                Control top;
                var stringified = obj.ToString();
                if (type.FullName != stringified)
                {
                    var resourceCache = IoCManager.Resolve<IResourceCache>();
                    var smallFont = new VectorFont(resourceCache.GetResource<FontResource>("/Fonts/CALIBRI.TTF"))
                    {
                        Size = 10,
                    };
                    // Custom ToString() implementation.
                    var headBox = new VBoxContainer {SeparationOverride = 0};
                    headBox.AddChild(new Label {Text = stringified});
                    headBox.AddChild(new Label
                        {Text = type.FullName, FontOverride = smallFont, FontColorOverride = Color.DarkGray});
                    top = headBox;
                }
                else
                {
                    top = new Label {Text = stringified};
                }

                if (_entity.TryGetComponent(out ISpriteComponent sprite))
                {
                    var hBox = new HBoxContainer();
                    top.SizeFlagsHorizontal = Control.SizeFlags.FillExpand;
                    hBox.AddChild(top);
                    hBox.AddChild(new SpriteView {Sprite = sprite});
                    vBoxContainer.AddChild(hBox);
                }
                else
                {
                    vBoxContainer.AddChild(top);
                }
            }

            _tabs = new TabContainer();
            _tabs.OnTabChanged += _tabsOnTabChanged;
            vBoxContainer.AddChild(_tabs);

            var clientVBox = new VBoxContainer {SeparationOverride = 0};
            _tabs.AddChild(clientVBox);
            _tabs.SetTabTitle(TabClientVars, "Client Variables");

            foreach (var control in LocalPropertyList(obj, ViewVariablesManager))
            {
                clientVBox.AddChild(control);
            }

            var clientComponents = new VBoxContainer {SeparationOverride = 0};
            _tabs.AddChild(clientComponents);
            _tabs.SetTabTitle(TabClientComponents, "Client Components");

            // See engine#636 for why the Distinct() call.
            var componentList = _entity.GetAllComponents().Distinct().OrderBy(c => c.GetType().ToString());
            foreach (var component in componentList)
            {
                var button = new Button {Text = component.GetType().ToString(), TextAlign = Button.AlignMode.Left};
                button.OnPressed += args => { ViewVariablesManager.OpenVV(component); };
                clientComponents.AddChild(button);
            }

            if (!_entity.Uid.IsClientSide())
            {
                _serverVariables = new VBoxContainer {SeparationOverride = 0};
                _tabs.AddChild(_serverVariables);
                _tabs.SetTabTitle(TabServerVars, "Server Variables");

                _serverComponents = new VBoxContainer {SeparationOverride = 0};
                _tabs.AddChild(_serverComponents);
                _tabs.SetTabTitle(TabServerComponents, "Server Components");
            }

            window.Contents.AddChild(vBoxContainer);
        }

        public override void Initialize(SS14Window window, ViewVariablesBlob blob, ViewVariablesRemoteSession session)
        {
            // TODO: this is pretty poorly implemented right now.
            // For example, it assumes a client-side entity exists,
            // so it also means client bubbling won't work in this context.

            _entitySession = session;
            _blob = (ViewVariablesBlobEntity) blob;

            var entityManager = IoCManager.Resolve<IEntityManager>();
            var uid = (EntityUid)blob.Properties.Single(p => p.Name == "Uid").Value;

            var entity = entityManager.GetEntity(uid);
            Initialize(window, entity);
        }

        public override void Close()
        {
            base.Close();

            if (_entitySession != null && !_entitySession.Closed)
            {
                ViewVariablesManager.CloseSession(_entitySession);
                _entitySession = null;
            }
        }

        private async void _tabsOnTabChanged(int tab)
        {
            if (_serverLoaded || tab != TabServerComponents && tab != TabServerVars)
            {
                return;
            }

            _serverLoaded = true;

            if (_entitySession == null)
            {
                try
                {
                    _entitySession =
                        await ViewVariablesManager.RequestSession(new ViewVariablesEntitySelector(_entity.Uid));
                }
                catch (SessionDenyException e)
                {
                    Logger.ErrorS("vv", "Server denied VV request: {0}", e.Reason);
                    return;
                }

                _blob = (ViewVariablesBlobEntity) await ViewVariablesManager.RequestData(_entitySession);
            }

            var otherStyle = false;
            foreach (var propertyData in _blob.Properties)
            {
                var propertyEdit = new ViewVariablesPropertyControl();
                propertyEdit.SetStyle(otherStyle = !otherStyle);
                var editor = propertyEdit.SetProperty(propertyData);
                editor.OnValueChanged += o =>
                    ViewVariablesManager.ModifyRemote(_entitySession, propertyData.Name, o);
                if (editor is ViewVariablesPropertyEditorReference refEditor)
                {
                    refEditor.OnPressed += () =>
                        ViewVariablesManager.OpenVV(
                            new ViewVariablesSessionRelativeSelector(_entitySession.SessionId,
                                propertyData.Name));
                }

                _serverVariables.AddChild(propertyEdit);
            }

            _serverComponents.DisposeAllChildren();
            _blob.ComponentTypes.Sort();
            foreach (var componentType in _blob.ComponentTypes.OrderBy(t => t.stringified))
            {
                var button = new Button {Text = componentType.stringified, TextAlign = Button.AlignMode.Left};
                button.OnPressed += args =>
                {
                    ViewVariablesManager.OpenVV(
                        new ViewVariablesComponentSelector(_entity.Uid, componentType.qualified));
                };
                _serverComponents.AddChild(button);
            }
        }
    }
}
