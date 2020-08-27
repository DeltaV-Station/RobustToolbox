using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NGettext;
using Robust.Shared.Interfaces.Resources;
using Robust.Shared.IoC;
using Robust.Shared.Localization.Macros;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Robust.Shared.Localization
{
    internal sealed class LocalizationManager : ILocalizationManager
    {
        [Dependency] private readonly IResourceManager _resourceManager = default!;
        [Dependency] private readonly ITextMacroFactory _textMacroFactory = default!;
        [Dependency] private readonly IRobustMappedStringSerializer _stringSerializer = default!;

        private readonly Dictionary<CultureInfo, Catalog> _catalogs = new Dictionary<CultureInfo, Catalog>();
        private CultureInfo? _defaultCulture;

        public string GetString(string text)
        {
            if (_defaultCulture == null)
            {
                return text;
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetString(text);
        }

        public string GetString(string text, params object[] args)
        {
            if (_defaultCulture == null)
            {
                return string.Format(text, args);
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetString(text, args);
        }

        public string GetParticularString(string context, string text)
        {
            if (_defaultCulture == null)
            {
                return text;
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetParticularString(context, text);
        }

        public string GetParticularString(string context, string text, params object[] args)
        {
            if (_defaultCulture == null)
            {
                return string.Format(text, args);
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetParticularString(context, text, args);
        }

        public string GetPluralString(string text, string pluralText, long n)
        {
            if (_defaultCulture == null)
            {
                return n == 1 ? text : pluralText;
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetPluralString(text, pluralText, n);
        }

        public string GetPluralString(string text, string pluralText, long n, params object[] args)
        {
            if (_defaultCulture == null)
            {
                return string.Format(n == 1 ? text : pluralText, args);
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetPluralString(text, pluralText, n, args);
        }

        public string GetParticularPluralString(string context, string text, string pluralText, long n)
        {
            if (_defaultCulture == null)
            {
                return n == 1 ? text : pluralText;
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetParticularPluralString(context, text, pluralText, n, pluralText);
        }

        public string GetParticularPluralString(string context, string text, string pluralText, long n, params object[] args)
        {
            if (_defaultCulture == null)
            {
                return string.Format(n == 1 ? text : pluralText, args);
            }
            var catalog = _catalogs[_defaultCulture];
            return catalog.GetParticularPluralString(context, text, pluralText, n, args);
        }

        public CultureInfo? DefaultCulture
        {
            get => _defaultCulture;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!_catalogs.ContainsKey(value))
                {
                    throw new ArgumentException("That culture is not yet loaded and cannot be used.", nameof(value));
                }

                _defaultCulture = value;
                CultureInfo.CurrentCulture = value;
                CultureInfo.CurrentUICulture = value;
            }
        }

        public void LoadCulture(CultureInfo culture)
        {
            var catalog = new CustomFormatCatalog(culture);
            _catalogs.Add(culture, catalog);

            _loadData(culture, catalog);
            _loadMacros(culture, catalog);
            if (DefaultCulture == null)
            {
                DefaultCulture = culture;
            }
        }

        private void _loadData(CultureInfo culture, Catalog catalog)
        {
            // Load data from .yml files.
            // Data is loaded from /Locale/<language-code>/*

            var root = new ResourcePath($"/Locale/{culture.IetfLanguageTag}/");

            foreach (var file in _resourceManager.ContentFindFiles(root))
            {
                var yamlFile = root / file;
                _loadFromFile(yamlFile, catalog);
            }
        }

        private void _loadFromFile(ResourcePath filePath, Catalog catalog)
        {
            var yamlStream = new YamlStream();
            using (var fileStream = _resourceManager.ContentFileRead(filePath))
            using (var reader = new StreamReader(fileStream, EncodingHelpers.UTF8))
            {
                yamlStream.Load(reader);
            }

            foreach (var entry in yamlStream.Documents
                .SelectMany(d => (YamlSequenceNode) d.RootNode)
                .Cast<YamlMappingNode>())
            {
                _readEntry(entry, catalog);
            }

            _stringSerializer.AddStrings(yamlStream);
        }

        private static void _readEntry(YamlMappingNode entry, Catalog catalog)
        {
            var id = entry.GetNode("msgid").AsString();
            var str = entry.GetNode("msgstr");
            string[] strings;
            if (str is YamlScalarNode scalar)
            {
                strings = new[] {scalar.AsString()};
            }
            else if (str is YamlSequenceNode sequence)
            {
                strings = sequence.Children.Select(c => c.AsString()).ToArray();
            }
            else
            {
                // TODO: Improve error reporting here.
                throw new Exception("Invalid format in translation file.");
            }

            catalog.Translations.Add(id, strings);
        }

        private void _loadMacros(CultureInfo culture, CustomFormatCatalog catalog)
        {
            var macros = _textMacroFactory.GetMacrosForLanguage(culture.IetfLanguageTag);
            catalog.CustomFormatProvider = new MacroFormatProvider(new MacroFormatter(macros), culture);
        }
    }
}
