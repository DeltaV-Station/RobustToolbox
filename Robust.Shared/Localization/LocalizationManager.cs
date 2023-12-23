using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Bundle.Errors;
using Linguini.Shared.Types.Bundle;
using Linguini.Syntax.Ast;
using Linguini.Syntax.Parser;
using Linguini.Syntax.Parser.Error;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Robust.Shared.Localization
{
    internal sealed partial class LocalizationManager : ILocalizationManagerInternal, IPostInjectInit
    {
        [Dependency] private readonly IResourceManager _res = default!;
        [Dependency] private readonly ILogManager _log = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly IEntityManager _entMan = default!;

        private ISawmill _logSawmill = default!;
        private readonly Dictionary<CultureInfo, FluentBundle> _contexts = new();

        private (CultureInfo, FluentBundle)? _defaultCulture;
        private (CultureInfo, FluentBundle)[]? _fallbackCultures;

        void IPostInjectInit.PostInject()
        {
            _logSawmill = _log.GetSawmill("loc");
            _prototype.PrototypesReloaded += OnPrototypesReloaded;
        }

        public string GetString(string messageId)
        {
            if (_defaultCulture == null)
                return messageId;

            if (!TryGetString(messageId, out var msg))
            {
                _logSawmill.Debug("Unknown messageId ({culture}): {messageId}", _defaultCulture.Value.Item1.Name, messageId);
                msg = messageId;
            }

            return msg;
        }

        public string GetString(string messageId, params (string, object)[] args)
        {
            if (_defaultCulture == null)
                return messageId;

            if (TryGetString(messageId, out var argMsg, args))
                return argMsg;

            _logSawmill.Debug("Unknown messageId ({culture}): {messageId}", _defaultCulture.Value.Item1.Name, messageId);
            return  messageId;

        }

        public bool HasString(string messageId)
        {
            return HasMessage(messageId, out _);
        }

        public bool TryGetString(string messageId, [NotNullWhen(true)] out string? value)
        {
            if (_defaultCulture == null)
            {
                value = null;
                return false;
            }

            if (TryGetString(messageId, _defaultCulture.Value, out value))
                return true;

            if (_fallbackCultures == null)
            {
                value = null;
                return false;
            }

            foreach (var fallback in  _fallbackCultures)
            {
                if (TryGetString(messageId, fallback, out value))
                    return true;
            }

            value = null;
            return false;
        }

        public bool TryGetString(string messageId, (CultureInfo, FluentBundle) bundle, [NotNullWhen(true)] out string? value)
        {
            try
            {
                // TODO LINGUINI error list nullable.
                var result = bundle.Item2.TryGetAttrMsg(messageId, null, out var errs, out value);
                foreach (var err in errs)
                {
                    _logSawmill.Error("{culture}/{messageId}: {error}", bundle.Item1.Name, messageId, err);
                }

                return result;
            }
            catch (Exception e)
            {
                _logSawmill.Error("{culture}/{messageId}: {exception}", bundle.Item1.Name, messageId, e);
                value = null;
                return false;
            }
        }

        public bool TryGetString(string messageId, [NotNullWhen(true)] out string? value,
            params (string, object)[] keyArgs)
        {
            // TODO LINGUINI add try-get-message variant that takes in a (string, object)[]
            // I.e., get rid of this has-message check
            if (!HasMessage(messageId, out var culture))
            {
                value = null;
                return false;
            }

            var (info, bundle) = culture.Value;
            var context = new LocContext(bundle);
            var args = new Dictionary<string, IFluentType>(keyArgs.Length);
            foreach (var (k, v) in keyArgs)
            {
                args.Add(k, v.FluentFromObject(context));
            }

            try
            {
                var result = bundle.TryGetAttrMsg(messageId, args, out var errs, out value);
                foreach (var err in errs)
                {
                    _logSawmill.Error("{culture}/{messageId}: {error}", info.Name, messageId, err);
                }

                return result;
            }
            catch (Exception e)
            {
                _logSawmill.Error("{culture}/{messageId}: {exception}", info.Name, messageId, e);
                value = null;
                return false;
            }
        }

        private bool HasMessage(
            string messageId,
            [NotNullWhen(true)] out (CultureInfo, FluentBundle)? culture)
        {
            if (_defaultCulture == null)
            {
                culture = null;
                return false;
            }

            var idx = messageId.IndexOf('.');
            if (idx != -1 )
                messageId = messageId.Remove(idx);

            culture = _defaultCulture;
            if (culture.Value.Item2.HasMessage(messageId))
                return true;


            if (_fallbackCultures == null)
            {
                culture = null;
                return false;
            }

            foreach (var fallback in  _fallbackCultures)
            {
                culture = fallback;
                if (culture.Value.Item2.HasMessage(messageId))
                    return true;
            }

            culture = null;
            return false;
        }

        private bool TryGetMessage(
            string messageId,
            [NotNullWhen(true)] out FluentBundle? bundle,
            [NotNullWhen(true)] out AstMessage? message)
        {
            if (_defaultCulture == null)
            {
                bundle = null;
                message = null;
                return false;
            }

            bundle = _defaultCulture.Value.Item2;
            if (bundle.TryGetAstMessage(messageId, out message))
                return true;

            if (_fallbackCultures == null)
            {
                bundle = null;
                return false;
            }

            foreach (var fallback in  _fallbackCultures)
            {
                bundle = fallback.Item2;
                if (bundle.TryGetAstMessage(messageId, out message))
                    return true;
            }

            bundle = null;
            return false;
        }

        public void ReloadLocalizations()
        {
            foreach (var (culture, context) in _contexts.ToArray())
            {
                _loadData(_res, culture, context);
            }

            FlushEntityCache();
        }

        public CultureInfo? DefaultCulture
        {
            get => _defaultCulture?.Item1;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (!_contexts.TryGetValue(value, out var bundle))
                {
                    throw new ArgumentException("That culture is not yet loaded and cannot be used.", nameof(value));
                }

                _defaultCulture = (value, bundle);
                CultureInfo.CurrentCulture = value;
                CultureInfo.CurrentUICulture = value;
            }
        }

        public void LoadCulture(CultureInfo culture)
        {
            var bundle = LinguiniBuilder.Builder()
                .CultureInfo(culture)
                .SkipResources()
                .SetUseIsolating(false)
                .UseConcurrent()
                .UncheckedBuild();

            _contexts.Add(culture, bundle);
            AddBuiltInFunctions(bundle);

            _loadData(_res, culture, bundle);
            DefaultCulture ??= culture;
        }

        public void SetFallbackCluture(params CultureInfo[] cultures)
        {
            _fallbackCultures = null;
            var tuples = new (CultureInfo, FluentBundle)[cultures.Length];
            var i = 0;
            foreach (var culture in cultures)
            {
                if (!_contexts.TryGetValue(culture, out var bundle))
                    throw new ArgumentException("That culture is not loaded.", nameof(culture));

                tuples[i++] = (culture, bundle);
            }

            _fallbackCultures = tuples;
        }

        public void AddLoadedToStringSerializer(IRobustMappedStringSerializer serializer)
        {
            /*
             * TODO: need to expose Messages on MessageContext in Fluent.NET
            serializer.AddStrings(StringIterator());

            IEnumerable<string> StringIterator()
            {
                foreach (var context in _contexts.Values)
                {
                    foreach (var (key, translations) in _context)
                    {
                        yield return key;

                        foreach (var t in translations)
                        {
                            yield return t;
                        }
                    }
                }
            }
            */
        }

        private void _loadData(IResourceManager resourceManager, CultureInfo culture, FluentBundle context)
        {
            // Load data from .ftl files.
            // Data is loaded from /Locale/<language-code>/*

            var root = new ResPath($"/Locale/{culture.Name}/");

            var files = resourceManager.ContentFindFiles(root)
                .Where(c => c.Filename.EndsWith(".ftl", StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            var resources = files.AsParallel().Select(path =>
            {
                string contents;

                using (var fileStream = resourceManager.ContentFileRead(path))
                using (var reader = new StreamReader(fileStream, EncodingHelpers.UTF8))
                {
                    contents = reader.ReadToEnd();
                }

                var parser = new LinguiniParser(contents);
                var resource = parser.Parse();
                return (path, resource, contents);
            });

            foreach (var (path, resource, data) in resources)
            {
                var errors = resource.Errors;
                context.AddResourceOverriding(resource);
                WriteWarningForErrs(path, errors, data);
            }
        }

        private void WriteWarningForErrs(ResPath path, List<ParseError> errs, string resource)
        {
            foreach (var err in errs)
            {
                _logSawmill.Error($"{path}:\n{err.FormatCompileErrors(resource.AsMemory())}");
            }
        }

        private void WriteWarningForErrs(IList<FluentError> errs, string locId)
        {
            foreach (var err in errs)
            {
                _logSawmill.Error("Error extracting `{locId}`\n{e1}", locId, err);
            }
        }
    }
}
