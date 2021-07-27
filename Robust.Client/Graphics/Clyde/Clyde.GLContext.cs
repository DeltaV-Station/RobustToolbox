﻿using System;
using Robust.Shared;
using Robust.Shared.Log;

namespace Robust.Client.Graphics.Clyde
{
    internal sealed partial class Clyde
    {
        private GLContextBase? _glContext;

        // Current OpenGL version we managed to initialize with.
        private RendererOpenGLVersion _openGLVersion;

        private void InitGLContextManager()
        {
            if (OperatingSystem.IsWindows()
                && _cfg.GetCVar(CVars.DisplayAngle)
                && _cfg.GetCVar(CVars.DisplayAngleCustomSwapChain))
            {
                _sawmillOgl.Debug("Trying custom swap chain ANGLE.");
                var ctxAngle = new GLContextAngle(this);

                if (ctxAngle.TryInitialize())
                {
                    _sawmillOgl.Debug("Successfully initialized custom ANGLE");
                    _glContext = ctxAngle;
                    return;
                }
            }

            _glContext = new GLContextWindow(this);
        }

        private struct GLContextSpec
        {
            public int Major;
            public int Minor;
            public GLContextProfile Profile;
            public GLContextCreationApi CreationApi;
        }

        private enum GLContextProfile
        {
            Compatibility,
            Core,
            Es
        }

        private enum GLContextCreationApi
        {
            Native,
            Egl,
        }
    }
}
