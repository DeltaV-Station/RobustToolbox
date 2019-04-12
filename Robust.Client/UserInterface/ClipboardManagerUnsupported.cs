using System;
using Robust.Client.Interfaces.UserInterface;

namespace Robust.Client.UserInterface
{
    internal sealed class ClipboardManagerUnsupported : IClipboardManager
    {
        public bool Available => false;
        public string NotAvailableReason => "Sorry, the clipboard is not supported on your platform.";

        public string GetText()
        {
            throw new NotSupportedException();
        }

        public void SetText(string text)
        {
            throw new NotSupportedException();
        }
    }
}
