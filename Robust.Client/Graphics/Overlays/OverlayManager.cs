﻿using Robust.Client.Interfaces;
using Robust.Client.Interfaces.Graphics.Overlays;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Client.Graphics.Overlays
{
    internal class OverlayManager : IOverlayManagerInternal
    {
        private readonly Dictionary<Guid, Overlay> _overlays = new Dictionary<Guid, Overlay>();
        public IEnumerable<Overlay> AllOverlays => _overlays.Values;

        public void FrameUpdate(FrameEventArgs args)
        {
            foreach (var overlay in _overlays.Values)
            {
                overlay.FrameUpdate(args);
            }
        }

        public void AddOverlay(Guid id, Overlay overlay) {
            if (_overlays.ContainsKey(id)) {
                throw new InvalidOperationException($"We already have an overlay with guid '{id}'!");
            }

            _overlays.Add(id, overlay);
        }
        public void RemoveOverlay(Guid id) {
            if (!_overlays.TryGetValue(id, out var overlay)) {
                return;
            }

            overlay.Dispose();
            _overlays.Remove(id);
        }
        public void RemoveOverlaysOfClass(string className) {
            var overlaysCopy = new Dictionary<Guid, Overlay>(_overlays);
            foreach (var (id, overlay) in overlaysCopy) {
                if (overlay.GetType().ToString() == className) {
                    overlay.Dispose();
                    _overlays.Remove(id);
                }
            }

        }
        public bool HasOverlay(Guid id) {
            return _overlays.ContainsKey(id);
        }
        public bool HasOverlayOfClass(string className) {
            foreach (var overlay in _overlays.Values) {
                if (overlay.GetType().ToString() == className) {
                    return true;
                }
            }
            return false;
        }
        public bool HasOverlayOfType<T>() {
            foreach (var overlay in _overlays.Values) {
                if (overlay.GetType() == typeof(T)) {
                    return true;
                }
            }
            return false;
        }

        public Overlay GetOverlay(Guid id)
        {
            return _overlays[id];
        }

        public bool GetOverlaysOfClass<T>(out List<T> overlays) where T : Overlay
        {
            overlays = new List<T>();
            foreach (var overlay in _overlays.Values) {
                if (overlay.GetType() == typeof(T))
                    overlays.Add((T)overlay);
            }
            return overlays.Count > 0;
        }
        public bool GetOverlaysOfClass(string className, out List<Overlay> overlays) {
            Type? type = Type.GetType(className);
            overlays = new List<Overlay>();
            if(type == null)
                throw new InvalidOperationException("Class '" + className + "' was requested in GetOverlaysOfClass, but no such class exists!");
            if(!type.IsSubclassOf(typeof(Overlay)))
                throw new InvalidOperationException("Class '" + className + "' was requested in GetOverlaysOfClass, but this class is not a child of Overlay!");

            if (type != null) {
                foreach (var overlay in _overlays.Values) {
                    if (overlay.GetType() == type)
                        overlays.Add(overlay);
                }
            }
            return overlays.Count > 0;
        }

        public int GetOverlayTypeCount<T>() where T : Overlay
        {
            int i = 0;
            foreach (var overlay in _overlays.Values) {
                if (overlay.GetType() == typeof(T))
                    i++;
            }
            return i;
        }



        public bool TryGetOverlay(Guid id, [NotNullWhen(true)] out Overlay? overlay)
        {
            return _overlays.TryGetValue(id, out overlay);
        }

        public bool TryGetOverlay<T>(Guid id, [NotNullWhen(true)] out T? overlay) where T : Overlay
        {
            if (_overlays.TryGetValue(id, out var value))
            {
                overlay = (T) value;
                return true;
            }

            overlay = default;
            return false;
        }
    }
}
