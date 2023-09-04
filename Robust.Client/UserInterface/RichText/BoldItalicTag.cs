using Robust.Client.ResourceManagement;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface.RichText;

[InjectDependencies]
public sealed partial class BoldItalicTag : IMarkupTag
{
    public const string BoldItalicFont = "DefaultBoldItalic";

    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public string Name => "bolditalic";

    /// <inheritdoc/>
    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        var font = FontTag.CreateFont(context.Font, node, _resourceCache, _prototypeManager, BoldItalicFont);
        context.Font.Push(font);
    }

    /// <inheritdoc/>
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        context.Font.Pop();
    }
}
