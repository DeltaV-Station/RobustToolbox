﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using ILVerify;
using Pidgin;
using Robust.Shared.Interfaces.Resources;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Robust.Shared.ContentPack
{
    /// <summary>
    ///     Manages the type white/black list of types and namespaces, and verifies assemblies against them.
    /// </summary>
    internal sealed partial class AssemblyTypeChecker
    {
        private readonly IResourceManager _res;

        /// <summary>
        ///     Completely disables type checking, allowing everything.
        /// </summary>
        public bool DisableTypeCheck { get; set; } = false;

        public DumpFlags Dump { get; set; } = DumpFlags.None;
        public bool VerifyIL { get; set; } = true;

        private bool WouldNoOp => Dump == DumpFlags.None && DisableTypeCheck && !VerifyIL;

        public AssemblyTypeChecker(IResourceManager res)
        {
            _res = res;
        }

        private Resolver CreateResolver()
        {
            var dotnetDir = Path.GetDirectoryName(typeof(int).Assembly.Location)!;
            var ourDir = Path.GetDirectoryName(typeof(AssemblyTypeChecker).Assembly.Location)!;

            Logger.DebugS("res.typecheck", ".NET runtime directory is {0}", dotnetDir);
            Logger.DebugS("res.typecheck", "Robust directory is {0}", ourDir);

            return new Resolver(
                this,
                new[] {dotnetDir, ourDir},
                new[] {new ResourcePath("/Assemblies/")}
            );
        }

        /// <summary>
        ///     Check the assembly for any illegal types. Any types not on the white list
        ///     will cause the assembly to be rejected.
        /// </summary>
        /// <param name="assembly">Assembly to load.</param>
        /// <returns></returns>
        public bool CheckAssembly(Stream assembly)
        {
            if (WouldNoOp)
            {
                // This method is a no-op in this case so don't bother
                return true;
            }

            Logger.DebugS("res.typecheck", "Checking assembly...");
            var fullStopwatch = Stopwatch.StartNew();

            var config = LoadConfig();
            var resolver = CreateResolver();
            using var peReader = new PEReader(assembly, PEStreamOptions.LeaveOpen);
            var reader = peReader.GetMetadataReader();

            if (VerifyIL)
            {
                if (!DoVerifyIL(resolver, config, peReader, reader))
                {
                    return false;
                }
            }

            var errors = new ConcurrentBag<SandboxError>();

            var types = GetReferencedTypes(reader, errors);
            var members = GetReferencedMembers(reader, errors);
            var inherited = GetExternalInheritedTypes(reader, errors);
            Logger.DebugS("res.typecheck", $"References loaded... {fullStopwatch.ElapsedMilliseconds}ms");

            if ((Dump & DumpFlags.Types) != 0)
            {
                foreach (var mType in types)
                {
                    Logger.DebugS("res.typecheck", $"RefType: {mType}");
                }
            }

            if ((Dump & DumpFlags.Members) != 0)
            {
                foreach (var memberRef in members)
                {
                    Logger.DebugS("res.typecheck", $"RefMember: {memberRef}");
                }
            }

            if ((Dump & DumpFlags.Inheritance) != 0)
            {
                foreach (var (name, baseType, interfaces) in inherited)
                {
                    Logger.DebugS("res.typecheck", $"Inherit: {name} -> {baseType}");
                    foreach (var @interface in interfaces)
                    {
                        Logger.DebugS("res.typecheck", $"  Interface: {@interface}");
                    }
                }
            }

            if (DisableTypeCheck)
            {
                return true;
            }

            // We still do explicit type reference scanning, even though the actual whitelists work with raw members.
            // This is so that we can simplify handling of generic type specifications during member checking:
            // we won't have to check that any types in their type arguments are whitelisted.
            foreach (var type in types)
            {
                if (!IsTypeAccessAllowed(type, config, out _))
                {
                    errors.Add(new SandboxError($"Access to type not allowed: {type}"));
                }
            }

            Logger.DebugS("res.typecheck", $"Types... {fullStopwatch.ElapsedMilliseconds}ms");

            CheckInheritance(inherited, errors, config);

            Logger.DebugS("res.typecheck", $"Inheritance... {fullStopwatch.ElapsedMilliseconds}ms");

            CheckMemberReferences(members, config, errors);

            foreach (var error in errors)
            {
                Logger.ErrorS("res.typecheck", $"Sandbox violation: {error.Message}");
            }

            Logger.DebugS("res.typecheck", $"Checked assembly in {fullStopwatch.ElapsedMilliseconds}ms");

            return errors.IsEmpty;
        }

        private static bool DoVerifyIL(Resolver resolver, SandboxConfig config, PEReader peReader,
            MetadataReader reader)
        {
            Logger.DebugS("res.typecheck", "Verifying IL...");
            var sw = Stopwatch.StartNew();
            var ver = new Verifier(resolver);
            ver.SetSystemModuleName(new AssemblyName(config.SystemAssemblyName));
            var verifyErrors = false;
            foreach (var res in ver.Verify(peReader))
            {
                if (config.AllowedVerifierErrors.Contains(res.Code))
                {
                    continue;
                }

                var msg = $"ILVerify: {res.Message}";

                try
                {
                    if (!res.Method.IsNil)
                    {
                        var method = reader.GetMethodDefinition(res.Method);
                        var methodSig = method.DecodeSignature(new TypeProvider(), 0);
                        var type = GetTypeFromDefinition(reader, method.GetDeclaringType());

                        var methodName =
                            $"{methodSig.ReturnType} {type}.{reader.GetString(method.Name)}({string.Join(", ", methodSig.ParameterTypes)})";

                        msg = $"{msg}, method: {methodName}";
                    }

                    if (!res.Type.IsNil)
                    {
                        var type = GetTypeFromDefinition(reader, res.Type);
                        msg = $"{msg}, type: {type}";
                    }
                }
                catch (UnsupportedMetadataException e)
                {
                    Logger.ErrorS("res.typecheck", $"{e}");
                }

                verifyErrors = true;
                Logger.ErrorS("res.typecheck", msg);
            }

            Logger.DebugS("res.typecheck", $"Verified IL in {sw.Elapsed.TotalMilliseconds}ms");

            if (verifyErrors)
            {
                return false;
            }

            return true;
        }

        private static void CheckMemberReferences(List<MMemberRef> members, SandboxConfig config,
            ConcurrentBag<SandboxError> errors)
        {
            Parallel.ForEach(members, memberRef =>
            {
                MType baseType = memberRef.ParentType;
                while (!(baseType is MTypeReferenced))
                {
                    switch (baseType)
                    {
                        case MTypeGeneric generic:
                        {
                            baseType = generic.GenericType;

                            break;
                        }
                        case MTypeArray array:
                        {
                            // For this kind of array we just need access to the type itself.
                            if (!IsTypeAccessAllowed((MTypeReferenced) array.ElementType, config, out _))
                            {
                                errors.Add(new SandboxError($"Access to type not allowed: {array}"));
                            }

                            return; // Found
                        }
                        default:
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                    }
                }

                var baseTypeReferenced = (MTypeReferenced) baseType;

                if (!IsTypeAccessAllowed(baseTypeReferenced, config, out var typeCfg))
                {
                    errors.Add(new SandboxError($"Access to type not allowed: {baseTypeReferenced}"));
                    return;
                }

                if (typeCfg.All)
                {
                    // Fully whitelisted for the type, we good.
                    return;
                }

                switch (memberRef)
                {
                    case MMemberRefField mMemberRefField:
                    {
                        foreach (var field in typeCfg.FieldsParsed)
                        {
                            if (field.Name == mMemberRefField.Name &&
                                mMemberRefField.FieldType.WhitelistEquals(field.FieldType))
                            {
                                return; // Found
                            }
                        }

                        errors.Add(new SandboxError($"Access to field not allowed: {mMemberRefField}"));
                        break;
                    }
                    case MMemberRefMethod mMemberRefMethod:
                        foreach (var parsed in typeCfg.MethodsParsed)
                        {
                            if (parsed.Name == mMemberRefMethod.Name &&
                                mMemberRefMethod.ReturnType.WhitelistEquals(parsed.ReturnType) &&
                                mMemberRefMethod.ParameterTypes.Length == parsed.ParameterTypes.Length &&
                                mMemberRefMethod.GenericParameterCount == parsed.GenericParameterCount)
                            {
                                for (var i = 0; i < mMemberRefMethod.ParameterTypes.Length; i++)
                                {
                                    var a = mMemberRefMethod.ParameterTypes[i];
                                    var b = parsed.ParameterTypes[i];

                                    if (!a.WhitelistEquals(b))
                                    {
                                        goto paramMismatch;
                                    }
                                }

                                return; // Found
                            }

                            paramMismatch: ;
                        }

                        errors.Add(new SandboxError($"Access to method not allowed: {mMemberRefMethod}"));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(memberRef));
                }
            });
        }

        private static void CheckInheritance(
            List<(MType type, MType parent, ArraySegment<MType> interfaceImpls)> inherited,
            ConcurrentBag<SandboxError> errors, SandboxConfig config)
        {
            // This inheritance whitelisting primarily serves to avoid content doing funny stuff
            // by e.g. inheriting Type.
            foreach (var (_, baseType, interfaces) in inherited)
            {
                if (!CanInherit(baseType))
                {
                    errors.Add(new SandboxError($"Inheriting of type not allowed: {baseType}"));
                }

                foreach (var @interface in interfaces)
                {
                    if (!CanInherit(@interface))
                    {
                        errors.Add(new SandboxError($"Implementing of interface not allowed: {@interface}"));
                    }
                }

                bool CanInherit(MType inheritType)
                {
                    var realBaseType = inheritType switch
                    {
                        MTypeGeneric generic => (MTypeReferenced) generic.GenericType,
                        MTypeReferenced referenced => referenced,
                        _ => throw new InvalidOperationException() // Can't happen.
                    };

                    if (!IsTypeAccessAllowed(realBaseType, config, out var cfg))
                    {
                        return false;
                    }

                    return cfg.Inherit != InheritMode.Block && (cfg.Inherit == InheritMode.Allow || cfg.All);
                }
            }
        }

        private static bool IsTypeAccessAllowed(MTypeReferenced type, SandboxConfig config,
            [NotNullWhen(true)] out TypeConfig? cfg)
        {
            if (type.Namespace == null)
            {
                if (type.ResolutionScope is MResScopeType parentType)
                {
                    if (!IsTypeAccessAllowed((MTypeReferenced) parentType.Type, config, out var parentCfg))
                    {
                        cfg = null;
                        return false;
                    }

                    if (parentCfg.All)
                    {
                        // Enclosing type is namespace-whitelisted so we don't have to check anything else.
                        cfg = TypeConfig.DefaultAll;
                        return true;
                    }

                    // Found enclosing type, checking if we are allowed to access this nested type.
                    // Also pass it up in case of multiple nested types.
                    if (parentCfg.NestedTypes != null && parentCfg.NestedTypes.TryGetValue(type.Name, out cfg))
                    {
                        return true;
                    }

                    cfg = null;
                    return false;
                }

                // Types without namespaces or nesting parent are not allowed at all.
                cfg = null;
                return false;
            }

            // Check if in whitelisted namespaces.
            foreach (var whNamespace in config.WhitelistedNamespaces)
            {
                if (type.Namespace.StartsWith(whNamespace))
                {
                    cfg = TypeConfig.DefaultAll;
                    return true;
                }
            }

            if (!config.Types.TryGetValue(type.Namespace, out var nsDict))
            {
                cfg = null;
                return false;
            }

            return nsDict.TryGetValue(type.Name, out cfg);
        }

        private List<MTypeReferenced> GetReferencedTypes(MetadataReader reader, ConcurrentBag<SandboxError> errors)
        {
            return reader.TypeReferences.Select(typeRefHandle =>
                {
                    try
                    {
                        return ParseTypeReference(reader, typeRefHandle);
                    }
                    catch (UnsupportedMetadataException e)
                    {
                        errors.Add(new SandboxError(e));
                        return null;
                    }
                })
                .Where(p => p != null)
                .ToList()!;
        }

        private List<MMemberRef> GetReferencedMembers(MetadataReader reader, ConcurrentBag<SandboxError> errors)
        {
            return reader.MemberReferences.AsParallel()
                .Select(memRefHandle =>
                {
                    var memRef = reader.GetMemberReference(memRefHandle);
                    var memName = reader.GetString(memRef.Name);
                    MType parent;
                    switch (memRef.Parent.Kind)
                    {
                        // See II.22.25 in ECMA-335.
                        case HandleKind.TypeReference:
                        {
                            // Regular type reference.
                            try
                            {
                                parent = ParseTypeReference(reader, (TypeReferenceHandle) memRef.Parent);
                            }
                            catch (UnsupportedMetadataException u)
                            {
                                errors.Add(new SandboxError(u));
                                return null;
                            }

                            break;
                        }
                        case HandleKind.TypeSpecification:
                        {
                            var typeSpec = reader.GetTypeSpecification((TypeSpecificationHandle) memRef.Parent);
                            // Generic type reference.
                            var provider = new TypeProvider();
                            parent = typeSpec.DecodeSignature(provider, 0);

                            if (parent.IsCoreTypeDefined())
                            {
                                // Ensure this isn't a self-defined type.
                                // This can happen due to generics since MethodSpec needs to point to MemberRef.
                                return null;
                            }

                            break;
                        }
                        case HandleKind.ModuleReference:
                        {
                            errors.Add(new SandboxError(
                                $"Module global variables and methods are unsupported. Name: {memName}"));
                            return null;
                        }
                        case HandleKind.MethodDefinition:
                        {
                            errors.Add(new SandboxError($"Vararg calls are unsupported. Name: {memName}"));
                            return null;
                        }
                        default:
                        {
                            errors.Add(new SandboxError(
                                $"Unsupported member ref parent type: {memRef.Parent}. Name: {memName}"));
                            return null;
                        }
                    }

                    MMemberRef memberRef;

                    switch (memRef.GetKind())
                    {
                        case MemberReferenceKind.Method:
                        {
                            var sig = memRef.DecodeMethodSignature(new TypeProvider(), 0);

                            memberRef = new MMemberRefMethod(
                                parent,
                                memName,
                                sig.ReturnType,
                                sig.GenericParameterCount,
                                sig.ParameterTypes);

                            break;
                        }
                        case MemberReferenceKind.Field:
                        {
                            var fieldType = memRef.DecodeFieldSignature(new TypeProvider(), 0);
                            memberRef = new MMemberRefField(parent, memName, fieldType);
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    return memberRef;
                })
                .Where(p => p != null)
                .ToList()!;
        }

        private List<(MType type, MType parent, ArraySegment<MType> interfaceImpls)> GetExternalInheritedTypes(
            MetadataReader reader,
            ConcurrentBag<SandboxError> errors)
        {
            var list = new List<(MType, MType, ArraySegment<MType>)>();
            foreach (var typeDefHandle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(typeDefHandle);
                ArraySegment<MType> interfaceImpls;
                MTypeDefined type = GetTypeFromDefinition(reader, typeDefHandle);

                if (!ParseInheritType(type, typeDef.BaseType, out var parent))
                {
                    continue;
                }

                var interfaceImplsCollection = typeDef.GetInterfaceImplementations();
                if (interfaceImplsCollection.Count == 0)
                {
                    interfaceImpls = Array.Empty<MType>();
                }
                else
                {
                    interfaceImpls = new MType[interfaceImplsCollection.Count];
                    var i = 0;
                    foreach (var implHandle in interfaceImplsCollection)
                    {
                        var interfaceImpl = reader.GetInterfaceImplementation(implHandle);

                        if (ParseInheritType(type, interfaceImpl.Interface, out var implemented))
                        {
                            interfaceImpls[i++] = implemented;
                        }
                    }

                    interfaceImpls = interfaceImpls[..i];
                }

                list.Add((type, parent, interfaceImpls));
            }

            return list;


            bool ParseInheritType(MType ownerType, EntityHandle handle, [NotNullWhen(true)] out MType? type)
            {
                type = default;

                switch (handle.Kind)
                {
                    case HandleKind.TypeDefinition:
                        // Definition to type in same assembly, allowed without hassle.
                        return false;

                    case HandleKind.TypeReference:
                        // Regular type reference.
                        try
                        {
                            type = ParseTypeReference(reader, (TypeReferenceHandle) handle);
                            return true;
                        }
                        catch (UnsupportedMetadataException u)
                        {
                            errors.Add(new SandboxError(u));
                            return false;
                        }

                    case HandleKind.TypeSpecification:
                        var typeSpec = reader.GetTypeSpecification((TypeSpecificationHandle) handle);
                        // Generic type reference.
                        var provider = new TypeProvider();
                        type = typeSpec.DecodeSignature(provider, 0);

                        if (type.IsCoreTypeDefined())
                        {
                            // Ensure this isn't a self-defined type.
                            // This can happen due to generics.
                            return false;
                        }

                        break;

                    default:
                        errors.Add(new SandboxError(
                            $"Unsupported BaseType of kind {handle.Kind} on type {ownerType}"));
                        return false;
                }

                type = default!;
                return false;
            }
        }

        private sealed class SandboxError
        {
            public string Message;

            public SandboxError(string message)
            {
                Message = message;
            }

            public SandboxError(UnsupportedMetadataException ume) : this($"Unsupported metadata: {ume.Message}")
            {
            }
        }


        /// <exception href="UnsupportedMetadataException">
        ///     Thrown if the metadata does something funny we don't "support" like type forwarding.
        /// </exception>
        private static MTypeReferenced ParseTypeReference(MetadataReader reader, TypeReferenceHandle handle)
        {
            var typeRef = reader.GetTypeReference(handle);
            var name = reader.GetString(typeRef.Name);
            var nameSpace = NilNullString(reader, typeRef.Namespace);
            MResScope resScope;

            // See II.22.38 in ECMA-335
            if (typeRef.ResolutionScope.IsNil)
            {
                throw new UnsupportedMetadataException(
                    $"Null resolution scope on type Name: {nameSpace}.{name}. This indicates exported/forwarded types");
            }

            switch (typeRef.ResolutionScope.Kind)
            {
                case HandleKind.AssemblyReference:
                {
                    // Different assembly.
                    var assemblyRef =
                        reader.GetAssemblyReference((AssemblyReferenceHandle) typeRef.ResolutionScope);
                    var assemblyName = reader.GetString(assemblyRef.Name);
                    resScope = new MResScopeAssembly(assemblyName);
                    break;
                }
                case HandleKind.TypeReference:
                {
                    // Nested type.
                    var enclosingType = ParseTypeReference(reader, (TypeReferenceHandle) typeRef.ResolutionScope);
                    resScope = new MResScopeType(enclosingType);
                    break;
                }
                case HandleKind.ModuleReference:
                {
                    // Same-assembly-different-module
                    throw new UnsupportedMetadataException(
                        $"Cross-module reference to type {nameSpace}.{name}. ");
                }
                default:
                    // Edge cases not handled:
                    // https://github.com/dotnet/runtime/blob/b2e5a89085fcd87e2fa9300b4bb00cd499c5845b/src/libraries/System.Reflection.Metadata/tests/Metadata/Decoding/DisassemblingTypeProvider.cs#L130-L132
                    throw new UnsupportedMetadataException(
                        $"TypeRef to {typeRef.ResolutionScope.Kind} for type {nameSpace}.{name}");
            }

            return new MTypeReferenced(resScope, name, nameSpace);
        }

        private sealed class UnsupportedMetadataException : Exception
        {
            public UnsupportedMetadataException()
            {
            }

            public UnsupportedMetadataException(string message) : base(message)
            {
            }

            public UnsupportedMetadataException(string message, Exception inner) : base(message, inner)
            {
            }
        }

        public bool CheckAssembly(string diskPath)
        {
            if (WouldNoOp)
            {
                // This method is a no-op in this case so don't bother
                return true;
            }

            using var file = File.OpenRead(diskPath);

            return CheckAssembly(file);
        }

        private static string? NilNullString(MetadataReader reader, StringHandle handle)
        {
            return handle.IsNil ? null : reader.GetString(handle);
        }

        private sealed class Resolver : ResolverBase
        {
            private readonly AssemblyTypeChecker _parent;
            private readonly string[] _diskLoadPaths;
            private readonly ResourcePath[] _resLoadPaths;

            public Resolver(AssemblyTypeChecker parent, string[] diskLoadPaths, ResourcePath[] resLoadPaths)
            {
                _parent = parent;
                _diskLoadPaths = diskLoadPaths;
                _resLoadPaths = resLoadPaths;
            }

            protected override PEReader? ResolveCore(string simpleName)
            {
                var dllName = $"{simpleName}.dll";
                foreach (var diskLoadPath in _diskLoadPaths)
                {
                    try
                    {
                        var path = Path.Combine(diskLoadPath, dllName);
                        return new PEReader(File.OpenRead(path));
                    }
                    catch (FileNotFoundException)
                    {
                    }
                }

                foreach (var resLoadPath in _resLoadPaths)
                {
                    try
                    {
                        var path = resLoadPath / dllName;
                        return new PEReader(_parent._res.ContentFileRead(path));
                    }
                    catch (FileNotFoundException)
                    {
                    }
                }

                return null;
            }
        }

        private sealed class TypeProvider : ISignatureTypeProvider<MType, int>
        {
            public MType GetSZArrayType(MType elementType)
            {
                return new MTypeSZArray(elementType);
            }

            public MType GetArrayType(MType elementType, ArrayShape shape)
            {
                return new MTypeArray(elementType, shape);
            }

            public MType GetByReferenceType(MType elementType)
            {
                return new MTypeByRef(elementType);
            }

            public MType GetGenericInstantiation(MType genericType, ImmutableArray<MType> typeArguments)
            {
                return new MTypeGeneric(genericType, typeArguments);
            }

            public MType GetPointerType(MType elementType)
            {
                return new MTypePointer(elementType);
            }

            public MType GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return new MTypePrimitive(typeCode);
            }

            public MType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                return AssemblyTypeChecker.GetTypeFromDefinition(reader, handle);
            }

            public MType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return ParseTypeReference(reader, handle);
            }

            public MType GetFunctionPointerType(MethodSignature<MType> signature)
            {
                throw new NotImplementedException();
            }

            public MType GetGenericMethodParameter(int genericContext, int index)
            {
                return new MTypeGenericMethodPlaceHolder(index);
            }

            public MType GetGenericTypeParameter(int genericContext, int index)
            {
                return new MTypeGenericTypePlaceHolder(index);
            }

            public MType GetModifiedType(MType modifier, MType unmodifiedType, bool isRequired)
            {
                return new MTypeModified(unmodifiedType, modifier, isRequired);
            }

            public MType GetPinnedType(MType elementType)
            {
                throw new NotImplementedException();
            }

            public MType GetTypeFromSpecification(MetadataReader reader, int genericContext,
                TypeSpecificationHandle handle,
                byte rawTypeKind)
            {
                return reader.GetTypeSpecification(handle).DecodeSignature(this, 0);
            }
        }

        private static MTypeDefined GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            var name = reader.GetString(typeDef.Name);
            var ns = NilNullString(reader, typeDef.Namespace);
            MTypeDefined? enclosing = null;
            if (typeDef.IsNested)
            {
                enclosing = GetTypeFromDefinition(reader, typeDef.GetDeclaringType());
            }

            return new MTypeDefined(name, ns, enclosing);
        }

        [Flags]
        public enum DumpFlags : byte
        {
            None = 0,
            Types = 1,
            Members = 2,
            Inheritance = 4,

            All = Types | Members | Inheritance
        }
    }
}
