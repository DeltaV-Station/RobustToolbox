using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.Client.Utility
{
    /// <summary>
    ///     Represents a Godot .(t)res or .(t)scn resource file that we loaded manually.
    /// </summary>
    internal abstract class GodotAsset
    {
        protected GodotAsset(IReadOnlyDictionary<int, ResourceDef> subResources,
            IReadOnlyList<ExtResourceRef> extResources)
        {
            ExtResources = extResources;
            SubResources = subResources;
        }

        /// <summary>
        ///     A list of all the external resources that are referenced by this resource.
        /// </summary>
        public IReadOnlyList<ExtResourceRef> ExtResources { get; }

        public IReadOnlyDictionary<int, ResourceDef> SubResources { get; }

        public ExtResourceRef GetExtResource(TokenExtResource token)
        {
            return ExtResources[(int) token.ResourceId - 1];
        }

        /// <summary>
        ///     A token value to indicate "this is a reference to an external resource".
        /// </summary>
        public readonly struct TokenExtResource : IEquatable<TokenExtResource>
        {
            public readonly long ResourceId;

            public TokenExtResource(long resourceId)
            {
                ResourceId = resourceId;
            }

            public bool Equals(TokenExtResource other)
            {
                return ResourceId == other.ResourceId;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is TokenExtResource other && Equals(other);
            }

            public override int GetHashCode()
            {
                return ResourceId.GetHashCode();
            }
        }

        /// <summary>
        ///     A token value to indicate "this is a reference to a sub resource".
        /// </summary>
        public readonly struct TokenSubResource : IEquatable<TokenSubResource>
        {
            public readonly long ResourceId;

            public TokenSubResource(long resourceId)
            {
                ResourceId = resourceId;
            }

            public bool Equals(TokenSubResource other)
            {
                return ResourceId == other.ResourceId;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is TokenSubResource other && Equals(other);
            }

            public override int GetHashCode()
            {
                return ResourceId.GetHashCode();
            }
        }

        /// <summary>
        ///     A reference to an external resource.
        /// </summary>
        public sealed class ExtResourceRef
        {
            /// <summary>
            ///     The godot file path of the external resource,
            ///     usually prefixed with res://
            /// </summary>
            public string Path { get; }

            /// <summary>
            ///     The Godot type of the referenced resource.
            ///     This is NOT a .NET type!
            /// </summary>
            public string Type { get; }

            /// <summary>
            ///     The ID of this external resource, so what <see cref="TokenExtResource"/> stores.
            /// </summary>
            public long Id { get; }

            public ExtResourceRef(string path, string type, long id)
            {
                Path = path;
                Type = type;
                Id = id;
            }
        }

        [PublicAPI]
        public sealed class ResourceDef
        {
            public ResourceDef(string type, [NotNull] IReadOnlyDictionary<string, object> properties)
            {
                Type = type;
                Properties = properties;
            }

            [NotNull] public IReadOnlyDictionary<string, object> Properties { get; }

            public string Type { get; }
        }
    }

    /// <inheritdoc />
    /// <summary>
    ///     This type is specifically a loaded .tscn file.
    /// </summary>
    internal sealed class GodotAssetScene : GodotAsset
    {
        public NodeDef RootNode => Nodes[0];
        public IReadOnlyList<NodeDef> Nodes { get; }

        public GodotAssetScene(IReadOnlyList<NodeDef> nodes, IReadOnlyDictionary<int, ResourceDef> subResources,
            IReadOnlyList<ExtResourceRef> extResourceRefs) : base(subResources, extResourceRefs)
        {
            Nodes = nodes;
        }

        public class NodeDef
        {
            /// <summary>
            ///     The name of this node.
            /// </summary>
            [NotNull]
            public string Name { get; }

            /// <summary>
            ///     The type of this node.
            ///     Can be null if this node is actually an instance (or part of one).
            /// </summary>
            [CanBeNull]
            public string Type { get; }

            /// <summary>
            ///     The scene-relative parent of this node.
            /// </summary>
            [CanBeNull]
            public string Parent { get; }

            /// <summary>
            ///     Index of this node among its siblings.
            /// </summary>
            public int Index { get; }

            /// <summary>
            ///     Index of this node definition in the scene file.
            /// </summary>
            public int DefinitionIndex { get; }

            /// <summary>
            ///     An external resource reference pointing to the scene we are instancing, if any.
            /// </summary>
            public TokenExtResource? Instance { get; }

            [NotNull] public IReadOnlyDictionary<string, object> Properties { get; }

            public NodeDef(string name, [CanBeNull] string type, [CanBeNull] string parent, int index,
                TokenExtResource? instance, Dictionary<string, object> properties, int definitionIndex)
            {
                Name = name;
                Type = type;
                Parent = parent;
                Index = index;
                Instance = instance;
                Properties = properties;
                DefinitionIndex = definitionIndex;
            }

            private sealed class FlattenedTreeComparerImpl : IComparer<NodeDef>
            {
                public int Compare(NodeDef x, NodeDef y)
                {
                    if (ReferenceEquals(x, y)) return 0;
                    if (ReferenceEquals(null, y)) return 1;
                    if (ReferenceEquals(null, x)) return -1;

                    var parentComparison = ParentFieldWeight(x.Parent).CompareTo(ParentFieldWeight(y.Parent));
                    if (parentComparison != 0) return parentComparison;
                    var indexComparison = x.Index.CompareTo(y.Index);
                    if (indexComparison != 0) return indexComparison;
                    return x.DefinitionIndex.CompareTo(y.DefinitionIndex);
                }

                private static int ParentFieldWeight(string parentField)
                {
                    switch (parentField)
                    {
                        case null:
                            return 0;
                        case ".":
                            return 1;
                        default:
                            return parentField.Count(c => c == '/') + 2;
                    }
                }
            }

            public static IComparer<NodeDef> FlattenedTreeComparer { get; } = new FlattenedTreeComparerImpl();
        }
    }

    internal sealed class GodotAssetRes : GodotAsset
    {
        public GodotAssetRes(ResourceDef mainResource, IReadOnlyDictionary<int, ResourceDef> subResources,
            IReadOnlyList<ExtResourceRef> extResources) : base(subResources, extResources)
        {
            MainResource = mainResource;
        }

        public ResourceDef MainResource { get; }
    }

        /// <summary>
    ///     Parser for Godot asset files.
    /// </summary>
    internal class GodotParser
    {
        private TextParser _parser;

        /// <summary>
        ///     Parse a Godot .tscn or .tres file's contents into a <see cref="GodotAsset"/>.
        /// </summary>
        /// <param name="reader">A text reader reading the resource file contents.</param>
        public static GodotAsset Parse(TextReader reader)
        {
            var parser = new GodotParser();
            return parser._parse(reader);
        }

        private GodotAsset _parse(TextReader reader)
        {
            try
            {
                return _parseInternal(reader);
            }
            catch (Exception e)
            {
                if (_parser == null)
                {
                    throw;
                }

                throw new TextParser.ParserException(
                    $"Exception while parsing at ({_parser.CurrentLine}, {_parser.CurrentIndex})", e);
            }
        }

        private GodotAsset _parseInternal(TextReader reader)
        {
            ResourceDefInParsing? mainResource = null;
            _parser = new TextParser(reader);
            _parser.NextLine();
            _parser.Parse('[');
            var nodeCount = 0;
            if (_parser.TryParse("gd_scene"))
            {
                // Nothing yet, maybe nothing ever.
            }
            else if (_parser.TryParse("gd_resource"))
            {
                string resourceType;
                while (true)
                {
                    _parser.EatWhitespace();
                    if (_parser.Peek() == ']')
                    {
                        throw new TextParser.ParserException("Didn't find a resource type!");
                    }

                    var (key, value) = ParseKeyValue();
                    if (key == "type")
                    {
                        resourceType = (string) value;
                        break;
                    }
                }

                mainResource = new ResourceDefInParsing(0, resourceType, new Dictionary<string, object>());
            }
            else
            {
                throw new TextParser.ParserException("Expected gd_scene or gd_resource");
            }

            var extResources = new List<GodotAsset.ExtResourceRef>();
            var subResources = new List<ResourceDefInParsing>();
            var nodes = new List<NodeHeader>();

            NodeHeader? currentParsingHeader = null;
            ResourceDefInParsing? currentParsingDef = null;

            // Go over all the [] headers in the file.
            while (!_parser.IsEOF())
            {
                _parser.NextLine();
                _parser.EatWhitespace();
                if (_parser.IsEOL())
                {
                    continue;
                }

                if (!_parser.TryParse('['))
                {
                    if (currentParsingDef.HasValue)
                    {
                        _parseProperty(currentParsingDef.Value.Properties);
                    }
                    else
                    {
                        DebugTools.Assert(currentParsingHeader.HasValue);
                        _parseProperty(currentParsingHeader.Value.Properties);
                    }
                    continue;
                }

                _parser.EatWhitespace();

                if (_parser.TryParse("node"))
                {
                    DebugTools.Assert(!mainResource.HasValue);

                    currentParsingHeader = ParseNodeHeader(nodeCount++);
                    nodes.Add(currentParsingHeader.Value);
                    currentParsingDef = null;
                }
                else if (_parser.TryParse("ext_resource"))
                {
                    extResources.Add(ParseExtResourceRef());
                }
                else if (_parser.TryParse("resource"))
                {
                    DebugTools.Assert(mainResource.HasValue);
                    currentParsingDef = mainResource;
                }
                else if (_parser.TryParse("sub_resource"))
                {
                    currentParsingDef = ParseSubResourceHeader();
                    subResources.Add(currentParsingDef.Value);
                    currentParsingHeader = null;
                }
                else
                {
                    // Probably something like sub_resource or whatever. Ignore it.
                    currentParsingHeader = null;
                    continue;
                }

                _parser.Parse(']');
                _parser.EnsureEOL();
            }

            var finalSubResources = subResources
                .ToDictionary(s => s.Index, s => new GodotAsset.ResourceDef(s.Type, s.Properties));

            if (mainResource.HasValue)
            {
                var mainResourceDef = new GodotAsset.ResourceDef(mainResource.Value.Type, mainResource.Value.Properties);
                return new GodotAssetRes(mainResourceDef, finalSubResources, extResources);
            }

            var finalNodes = nodes
                .Select(n => new GodotAssetScene.NodeDef(n.Name, n.Type, n.Parent, n.Index, n.Instance, n.Properties, n.DefIndex))
                .ToList();

            // Alright try to resolve tree graph.
            // Sort based on tree depth by parsing parent path.
            // This way, when doing straight iteration, we'll always have the parent.

            finalNodes.Sort(GodotAssetScene.NodeDef.FlattenedTreeComparer);

            return new GodotAssetScene(finalNodes, finalSubResources, extResources);
        }

        private void _parseProperty(IDictionary<string, object> propertyDict)
        {
            var key = _parser.EatUntilWhitespace();
            _parser.EatWhitespace();
            _parser.Parse('=');
            _parser.EatWhitespace();

            try
            {
                var value = ParseGodotValue();
                if (propertyDict.ContainsKey(key))
                {
                    Logger.WarningS("gdparse", "Duplicate property key: {0}", key);
                }

                propertyDict[key] = value;
            }
            catch (NotImplementedException e)
            {
                Logger.WarningS("gdparse", "Can't parse Godot property value: {0}", e.Message);
            }
        }

        private NodeHeader ParseNodeHeader(int defIndex)
        {
            _parser.EatWhitespace();

            string name = null;
            string type = null;
            string index = null;
            string parent = null;
            GodotAsset.TokenExtResource? instance = null;

            while (_parser.Peek() != ']')
            {
                var (keyName, value) = ParseKeyValue();

                switch (keyName)
                {
                    case "name":
                        name = (string) value;
                        break;
                    case "type":
                        type = (string) value;
                        break;
                    case "index":
                        index = (string) value;
                        break;
                    case "parent":
                        parent = (string) value;
                        break;
                    case "instance":
                        instance = (GodotAsset.TokenExtResource) value;
                        break;
                }

                _parser.EatWhitespace();
            }

            return new NodeHeader(name, type, index == null ? 0 : int.Parse(index, CultureInfo.InvariantCulture),
                parent, instance, defIndex);
        }

        private ResourceDefInParsing ParseSubResourceHeader()
        {
            _parser.EatWhitespace();

            string type = null;
            var index = 0L;

            while (_parser.Peek() != ']')
            {
                var (keyName, value) = ParseKeyValue();

                switch (keyName)
                {
                    case "type":
                        type = (string) value;
                        break;
                    case "id":
                        index = (long) value;
                        break;
                }

                _parser.EatWhitespace();
            }

            DebugTools.AssertNotNull(index);
            return new ResourceDefInParsing((int)index, type, new Dictionary<string, object>());
        }

        private GodotAsset.ExtResourceRef ParseExtResourceRef()
        {
            _parser.EatWhitespace();

            string path = null;
            string type = null;
            var id = 0L;

            while (_parser.Peek() != ']')
            {
                var (keyName, value) = ParseKeyValue();

                switch (keyName)
                {
                    case "path":
                        path = (string) value;
                        break;
                    case "type":
                        type = (string) value;
                        break;
                    case "id":
                        id = (long) value;
                        break;
                }

                _parser.EatWhitespace();
            }

            return new GodotAsset.ExtResourceRef(path, type, id);
        }

        private (string name, object value) ParseKeyValue()
        {
            _parser.EatWhitespace();
            var keyList = new List<char>();

            while (true)
            {
                _parser.EnsureNoEOL();

                // Eat until = or whitespace.
                if (_parser.Peek() == '=' || _parser.EatWhitespace())
                {
                    break;
                }

                keyList.Add(_parser.Take());
            }

            var name = new string(keyList.ToArray());

            _parser.Parse('=');
            _parser.EatWhitespace();

            return (name, ParseGodotValue());
        }

        private object ParseGodotValue()
        {
            if (_parser.Peek() == '"')
            {
                return ParseGodotString();
            }

            if (_parser.PeekIsDigit() || _parser.Peek() == '-')
            {
                return ParseGodotNumber();
            }

            if (_parser.TryParse("null"))
            {
                return null;
            }

            if (_parser.TryParse("true"))
            {
                return true;
            }

            if (_parser.TryParse("false"))
            {
                return false;
            }

            if (_parser.TryParse("Color("))
            {
                _parser.EatWhitespace();
                var r = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(',');
                _parser.EatWhitespace();
                var g = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(',');
                _parser.EatWhitespace();
                var b = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(',');
                _parser.EatWhitespace();
                var a = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(')');
                return new Color(r, g, b, a);
            }

            if (_parser.TryParse("Vector2("))
            {
                _parser.EatWhitespace();
                var x = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(',');
                _parser.EatWhitespace();
                var y = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(')');
                return new Vector2(x, y);
            }

            if (_parser.TryParse("Vector3("))
            {
                _parser.EatWhitespace();
                var x = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(',');
                _parser.EatWhitespace();
                var y = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(',');
                _parser.EatWhitespace();
                var z = ParseGodotFloat();
                _parser.EatWhitespace();
                _parser.Parse(')');
                return new Vector3(x, y, z);
            }

            if (_parser.TryParse("ExtResource("))
            {
                _parser.EatWhitespace();
                var val = new GodotAsset.TokenExtResource((long) ParseGodotNumber());
                _parser.EatWhitespace();
                _parser.Parse(')');
                return val;
            }

            if (_parser.TryParse("SubResource("))
            {
                _parser.EatWhitespace();
                var val = new GodotAsset.TokenSubResource((long) ParseGodotNumber());
                _parser.EatWhitespace();
                _parser.Parse(')');
                return val;
            }

            throw new NotImplementedException($"Unable to handle complex kv pairs: '{_parser.Peek()}'");
        }

        private string ParseGodotString()
        {
            _parser.Parse('"');
            var list = new List<char>();
            var escape = false;

            while (true)
            {
                if (_parser.IsEOL())
                {
                    list.Add('\n');
                    _parser.NextLine();
                    continue;
                }

                var value = _parser.Take();
                if (value == '\\')
                {
                    if (escape)
                    {
                        list.Add('\\');
                    }
                    else
                    {
                        escape = true;
                    }

                    continue;
                }

                if (value == '"' && !escape)
                {
                    break;
                }

                if (escape)
                {
                    throw new TextParser.ParserException("Unknown escape sequence");
                }

                list.Add(value);
            }

            return new string(list.ToArray());
        }

        private float ParseGodotFloat()
        {
            var number = ParseGodotNumber();
            if (number is long l)
            {
                return l;
            }

            return (float) number;
        }

        private object ParseGodotNumber()
        {
            var list = new List<char>();

            if (_parser.Peek() == '-')
            {
                list.Add('-');
                _parser.Advance();
            }

            while (!_parser.IsEOL())
            {
                if (!_parser.PeekIsDigit() && _parser.Peek() != '.')
                {
                    break;
                }

                list.Add(_parser.Take());
            }

            var number = new string(list.ToArray());

            if (number.IndexOf('.') != -1)
            {
                return float.Parse(number, CultureInfo.InvariantCulture);
            }

            return long.Parse(number, CultureInfo.InvariantCulture);
        }

        private readonly struct NodeHeader
        {
            public readonly string Name;
            public readonly string Type;
            public readonly int Index;
            public readonly string Parent;
            public readonly int DefIndex;
            public readonly GodotAsset.TokenExtResource? Instance;
            public readonly Dictionary<string, object> Properties;

            public NodeHeader(string name, string type, int index, string parent, GodotAsset.TokenExtResource? instance, int defIndex)
            {
                Name = name;
                Type = type;
                Index = index;
                Parent = parent;
                Instance = instance;
                Properties = new Dictionary<string, object>();
                DefIndex = defIndex;
            }
        }

        private readonly struct ResourceDefInParsing
        {
            public readonly int Index;
            public readonly string Type;
            public readonly Dictionary<string, object> Properties;

            public ResourceDefInParsing(int index, string type, Dictionary<string, object> properties)
            {
                Index = index;
                Type = type;
                Properties = properties;
            }
        }
    }
}
