﻿using SS14.Shared.Configuration;

namespace SS14.Shared.Interfaces.Configuration
{
    /// <summary>
    /// Stores and manages global configuration variables.
    /// </summary>
    public interface IConfigurationManager
    {
        /// <summary>
        /// Sets up the ConfigurationManager and loads a yml configuration file.
        /// </summary>
        /// <param name="configFile">the full name of the config file.</param>
        void Initialize(string configFile);

        /// <summary>
        /// Saves the configuration file to disk.
        /// </summary>
        void Save();

        /// <summary>
        /// Register a CVar with the system.
        /// </summary>
        /// <param name="name">The name of the CVar.</param>
        /// <param name="defaultValue">The default Value of the CVar.</param>
        /// <param name="flags">Optional flags to change behavior of the CVar.</param>
        void RegisterCVar(string name, object defaultValue, CVarFlags flags = CVarFlags.NONE);

        /// <summary>
        /// Is the named CVar already registered?
        /// </summary>
        /// <param name="name">The name of the CVar.</param>
        /// <returns></returns>
        bool IsCVarRegistered(string name);

        /// <summary>
        /// Sets a CVars value.
        /// </summary>
        /// <param name="name">The name of the CVar.</param>
        /// <param name="value">The value to set.</param>
        void SetCVar(string name, object value);

        /// <summary>
        /// Get the value of a CVar.
        /// </summary>
        /// <typeparam name="T">The Type of the CVar value.</typeparam>
        /// <param name="name">The name of the CVar.</param>
        /// <returns></returns>
        T GetCVar<T>(string name);
    }
}