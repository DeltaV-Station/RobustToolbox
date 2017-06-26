﻿using System;
using System.Collections.Generic;
using Nett;
using SS14.Shared.Interfaces.Configuration;
using SS14.Shared.Log;

namespace SS14.Shared.Configuration
{
    /// <summary>
    /// Stores and manages global configuration variables.
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {
        private string _configFile;
        private readonly Dictionary<string, ConfigVar> _configVars;

        /// <summary>
        /// Constructs a new ConfigurationManager.
        /// </summary>
        public ConfigurationManager()
        {
            _configVars = new Dictionary<string, ConfigVar>();
        }

        /// <inheritdoc />
        public void LoadFile(string configFile)
        {
            try
            {
                var tblRoot = Toml.ReadFile(configFile);
                var tblCfg = (TomlTable)tblRoot["Configuration"];

                foreach (var kvObj in tblCfg)
                {
                    // if the CVar has already been registered
                    ConfigVar cfgVar;
                    if (_configVars.TryGetValue(kvObj.Key, out cfgVar))
                    {
                        // overwrite the value with the saved one
                        cfgVar.Value = TypeConvert(kvObj.Value);
                        continue;
                    }

                    //or add another unregistered CVar
                    var configVar = new ConfigVar(kvObj.Key, null, CVarFlags.NONE) {Value = TypeConvert(kvObj.Value)};
                    _configVars.Add(kvObj.Key, configVar);
                }


                _configFile = configFile;
                Logger.Log($"[CFG] Configuration Loaded from '{configFile}'");
            }
            catch (Exception e)
            {
                Logger.Warning("[CFG] Unable to load configuration file:\n{0}", e);
            }
        }

        // because apparently Nett can't do this for you...
        private static object TypeConvert(TomlObject obj)
        {
            var tmlType = obj.TomlType;
            switch (tmlType)
            {
                case TomlObjectType.Bool:
                    return obj.Get<bool>();

                case TomlObjectType.Float:
                    return obj.Get<float>();

                case TomlObjectType.Int:
                    return obj.Get<int>();

                case TomlObjectType.String:
                    return obj.Get<string>();

                default:
                    throw new Exception($"[CFG] Cannot convert {tmlType}.");
            }
        }

        /// <inheritdoc />
        public void SaveFile()
        {
            if (_configFile == null)
            {
                Logger.Log("[CFG] Cannot save the config file, because one was never loaded.", LogLevel.Warning);
                return;
            }

            try
            {
                var tblRoot = Toml.Create();
                var tblCfg = tblRoot.AddTable("Configuration");

                foreach (var kvCVar in _configVars)
                {
                    var cVar = kvCVar.Value;

                    if(!cVar.Registered)
                        continue;

                    var name = kvCVar.Key;
                    var cVarVal = cVar.Value;

                    //runtime unboxing, either this or generic hell... ¯\_(ツ)_/¯
                    if (cVarVal is int || cVarVal is Enum)
                        tblCfg.AddValue(name, (int)cVarVal);
                    else if (cVarVal is long)
                        tblCfg.AddValue(name, (long)cVarVal);
                    else if (cVarVal is bool)
                        tblCfg.AddValue(name, (bool)cVarVal);
                    else if (cVarVal is string)
                        tblCfg.AddValue(name, (string)cVarVal);
                    else if (cVarVal is float)
                        tblCfg.AddValue(name, (float)cVarVal);
                    else if (cVarVal is double)
                        tblCfg.AddValue(name, (double) cVarVal);
                    else
                        Logger.Log($"Cannot serialize '{name}', unsupported type.", LogLevel.Warning);
                }
                
                Toml.WriteFile(tblRoot, _configFile);

                Logger.Log($"[CFG] Server config saved to '{_configFile}'.");
            }
            catch (Exception e)
            {
                Logger.Log($"[CFG] Cannot save the config file '{_configFile}'.\n {e.Message}", LogLevel.Warning);
            }
        }

        /// <inheritdoc />
        public void RegisterCVar(string name, object defaultValue, CVarFlags flags = CVarFlags.NONE)
        {
            ConfigVar cVar;
            if (_configVars.TryGetValue(name, out cVar))
            {
                if (cVar.Registered)
                {
                    Logger.Log($"[CVar] The variable '{name}' has already been registered.", LogLevel.Error);
                }

                cVar.DefaultValue = defaultValue;
                cVar.Flags = flags;
                cVar.Registered = true;
                return;
            }

            _configVars.Add(name, new ConfigVar(name, defaultValue, flags) { Registered = true, Value = defaultValue});
        }

        /// <inheritdoc />
        public bool IsCVarRegistered(string name)
        {
            ConfigVar cVar;
            return _configVars.TryGetValue(name, out cVar) && cVar.Registered;
        }

        /// <inheritdoc />
        public void SetCVar(string name, object value)
        {
            ConfigVar cVar;
            if (_configVars.TryGetValue(name, out cVar) && cVar.Registered)
            {
                //TODO: Make flags work, required non-derpy net system.
                if(cVar.DefaultValue != value)
                {
                    cVar.Value = value;
                }
            }

            Logger.Log($"[CVar] Trying to set unregistered variable '{name}'", LogLevel.Fatal);
            throw new Exception($"[CVar] Trying to set unregistered variable '{name}'");
        }

        /// <inheritdoc />
        public T GetCVar<T>(string name)
        {
            ConfigVar cVar;
            if (_configVars.TryGetValue(name, out cVar) && cVar.Registered)
            {
                //TODO: Make flags work, required non-derpy net system.
                return (T) (cVar.Value ?? cVar.DefaultValue);
            }

            Logger.Log($"[CVar] Trying to get unregistered variable '{name}'", LogLevel.Fatal);
            throw new Exception($"[CVar] Trying to get unregistered variable '{name}'");
        }
    }
}
