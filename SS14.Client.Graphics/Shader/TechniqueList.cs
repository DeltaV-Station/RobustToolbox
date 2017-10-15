﻿using System.Collections.Generic;

namespace SS14.Client.Graphics.Shader
{

    /// <para>
    /// With the transition from FX to GLSL, GLSL shaders cannot do multiple techniques per shader, 
    /// to fix that, each Technique has been converted into a standalone shader.
    /// This class combines those individual shaders into a technique list.
    /// </para>
    /// <summary>
    /// Creates a Dictionary to combine individual shaders into a list of techniques
    /// </summary>
    public class TechniqueList
    {
        private Dictionary<string, GLSLShader> _techniqueList; 
        private string _name;

        public TechniqueList()
        {
            _techniqueList = new Dictionary<string,GLSLShader>();
        }

        public void Add(GLSLShader Shader)
        {
            _techniqueList.Add(Shader.ResourceName, Shader);
        }

        public GLSLShader getShader(string ShaderName)
        {
            return _techniqueList[ShaderName];
        }

        public GLSLShader this[string key]
        {

            get { return _techniqueList[key.ToLowerInvariant()]; }
            private set{ _techniqueList[key.ToLowerInvariant()] = value; }
        }

        public Dictionary<string, GLSLShader> Dictonary
        {
            get { return _techniqueList; }
            private set { _techniqueList = value; }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

    }
}
