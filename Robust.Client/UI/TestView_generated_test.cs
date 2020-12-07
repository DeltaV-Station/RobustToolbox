﻿using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using XamlX.Ast;
using XamlX.Emit;
using XamlX.IL;
using XamlX.Parsers;
using XamlX.Transform;
using XamlX.TypeSystem;

namespace Robust.Client.UI
{
    public partial class TestView : SS14Window
    {
        public TestView()
        {
            var comp = new XamlCompiler();
            var content = File.ReadAllText("../../Robust.Client/UI/TestView.xaml");
            var obj = comp.Compile(content).create(null!);
            //throw new NotImplementedException();
            /*

            var thing = XDocumentXamlParser.Parse(content);

            if (thing.Root is XamlAstObjectNode objectNode)
            {
                AddChild(ParseNode(objectNode));
            }

            System.Console.WriteLine("aaa");*/
        }

        /*Control ParseNode(XamlAstObjectNode node)
        {
            foreach (var astNode in node.Children)
            {
                switch (astNode)
                {
                    case XamlAstObjectNode objNode:
                        var type = objNode.Type.GetClrType();
                        break;
                    case XamlAstXamlPropertyValueNode valueNode:
                        break;
                }
            }

            return null;
        }*/
    }



    /*public class TestCompiler
    {
        private readonly IXamlTypeSystem _typeSystem;
        public TransformerConfiguration Configuration { get; }

        public TestCompiler() : this(new SreTypeSystem())
        {

        }

        private TestCompiler(IXamlTypeSystem typeSystem)
        {
            _typeSystem = typeSystem;
            Configuration = new TransformerConfiguration(typeSystem,
                typeSystem.FindAssembly("Robust.Client.UI"),
                new XamlLanguageTypeMappings(typeSystem)
                {
                    XmlnsAttributes =
                    {
                        typeSystem.GetType("Robust.Client.UI.XmlnsDefinitionAttribute"),

                    },
                    ContentAttributes =
                    {
                        typeSystem.GetType("Robust.Client.UI.ContentAttribute")
                    },
                    UsableDuringInitializationAttributes =
                    {
                        typeSystem.GetType("Robust.Client.UI.UsableDuringInitializationAttribute")
                    },
                    DeferredContentPropertyAttributes =
                    {
                        typeSystem.GetType("Robust.Client.UI.DeferredContentAttribute")
                    },
                    RootObjectProvider = typeSystem.GetType("Robust.Client.UI.ITestRootObjectProvider"),
                    UriContextProvider = typeSystem.GetType("Robust.Client.UI.ITestUriContext"),
                    ProvideValueTarget = typeSystem.GetType("Robust.Client.UI.ITestProvideValueTarget"),
                    /*ParentStackProvider = typeSystem.GetType("XamlX.Runtime.IXamlParentStackProviderV1"),
                    XmlNamespaceInfoProvider = typeSystem.GetType("XamlX.Runtime.IXamlXmlNamespaceInfoProviderV1")*/
                /*}
            );
        }

        public (Func<IServiceProvider, object> create, Action<IServiceProvider, object> populate) Compile(string xaml)
        {
            var da = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);

            var dm = da.DefineDynamicModule("testasm.dll");
            var t = dm.DefineType(Guid.NewGuid().ToString("N"), TypeAttributes.Public);
            var ct = dm.DefineType(t.Name + "Context");
            var ctb = ((SreTypeSystem)_typeSystem).CreateTypeBuilder(ct);
            var contextTypeDef =
                XamlILContextDefinition.GenerateContextClass(
                    ctb,
                    _typeSystem,
                    Configuration.TypeMappings,
                    new XamlLanguageEmitMappings<IXamlILEmitter, XamlILNodeEmitResult>());


            var parserTypeBuilder = ((SreTypeSystem) _typeSystem).CreateTypeBuilder(t);

            var parsed = Compile(parserTypeBuilder, contextTypeDef, xaml);

            var created = t.CreateTypeInfo();

            return GetCallbacks(created);
        }

        XamlDocument Compile(IXamlTypeBuilder<IXamlILEmitter> builder, IXamlType context, string xaml)
        {
            var parsed = XDocumentXamlParser.Parse(xaml);
            var compiler = new XamlILCompiler(
                Configuration,
                new XamlLanguageEmitMappings<IXamlILEmitter, XamlILNodeEmitResult>(),
                true)
            {
                EnableIlVerification = true
            };
            compiler.Transform(parsed);
            compiler.Compile(parsed, builder, context, "Populate", "Build",
                "XamlNamespaceInfo",
                "http://example.com/", null);
            return parsed;
        }

        (Func<IServiceProvider, object> create, Action<IServiceProvider, object> populate)
            GetCallbacks(Type? created)
        {
            if (created == null) throw new NotImplementedException();
            var isp = Expression.Parameter(typeof(IServiceProvider));
            var createCb = Expression.Lambda<Func<IServiceProvider, object>>(
                Expression.Convert(Expression.Call(
                    created.GetMethod("Build")!, isp), typeof(object)), isp).Compile();

            var epar = Expression.Parameter(typeof(object));
            var populate = created.GetMethod("Populate")!;
            isp = Expression.Parameter(typeof(IServiceProvider));
            var populateCb = Expression.Lambda<Action<IServiceProvider, object>>(
                Expression.Call(populate, isp, Expression.Convert(epar, populate.GetParameters()[1].ParameterType)),
                isp, epar).Compile();

            return (createCb, populateCb);
        }
    }*/
}
