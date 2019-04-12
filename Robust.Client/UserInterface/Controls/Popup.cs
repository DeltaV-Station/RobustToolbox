﻿using System;
using Robust.Client.GodotGlue;
using Robust.Client.Utility;
using Robust.Shared.Maths;

namespace Robust.Client.UserInterface.Controls
{
    [ControlWrap(typeof(Godot.Popup))]
    public class Popup : Control
    {
        public Popup() : base()
        {
        }

        public Popup(string name) : base(name)
        {
        }

        internal Popup(Godot.Popup control) : base(control)
        {
        }

        public event Action OnPopupHide;

        private protected override Godot.Control SpawnSceneControl()
        {
            return new Godot.Popup();
        }

        public void Open(UIBox2? box = null)
        {
            if (GameController.OnGodot)
            {
                SceneControl.Call("popup", box?.Convert());
            }
            else
            {
                if (box != null)
                {
                    Position = box.Value.TopLeft;
                    Size = box.Value.Size;
                }

                Visible = true;
                UserInterfaceManagerInternal.PushModal(this);
            }
        }

        protected internal override void ModalRemoved()
        {
            base.ModalRemoved();

            Visible = false;
            OnPopupHide?.Invoke();
        }

        private GodotSignalSubscriber0 __popupHideSubscriber;

        protected override void SetupSignalHooks()
        {
            base.SetupSignalHooks();

            __popupHideSubscriber = new GodotSignalSubscriber0();
            __popupHideSubscriber.Connect(SceneControl, "popup_hide");
            __popupHideSubscriber.Signal += __popupHideHook;
        }

        protected override void DisposeSignalHooks()
        {
            base.DisposeSignalHooks();

            __popupHideSubscriber.Disconnect(SceneControl, "popup_hide");
            __popupHideSubscriber.Dispose();
            __popupHideSubscriber = null;
        }

        private void __popupHideHook()
        {
            OnPopupHide?.Invoke();
        }

        protected override void SetDefaults()
        {
            base.SetDefaults();

            Visible = false;
        }
    }
}
