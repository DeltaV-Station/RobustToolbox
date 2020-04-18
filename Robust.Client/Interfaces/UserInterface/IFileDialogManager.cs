using System.Collections.Generic;
using System.Threading.Tasks;

namespace Robust.Client.Interfaces.UserInterface
{
    /// <summary>
    ///     Manager for opening of file dialogs.
    /// </summary>
    /// <remarks>
    ///     File dialogs are native to the OS being ran on.
    ///     All operations are asynchronous to prevent locking up the main thread while the user makes his pick.
    /// </remarks>
    public interface IFileDialogManager
    {
        /// <summary>
        ///     Open a file dialog used for opening a single file.
        /// </summary>
        /// <returns>The path the user selected to open. Null if the user cancelled the action.</returns>
        Task<string> OpenFile(FileDialogFilters filters = null);

        /// <summary>
        ///     Open a file dialog used for saving a single file.
        /// </summary>
        /// <returns>The path the user selected to save to. Null if the user cancelled the action.</returns>
        Task<string> SaveFile();

        /// <summary>
        ///     Open a file dialog used for opening a single folder.
        /// </summary>
        /// <returns>The path the user selected to open. Null if the user cancelled the action.</returns>
        Task<string> OpenFolder();
    }

    public class FileDialogFilters
    {
        public FileDialogFilters(IReadOnlyList<Group> groups)
        {
            Groups = groups;
        }

        public FileDialogFilters(params Group[] groups) : this((IReadOnlyList<Group>) groups)
        {
        }

        public IReadOnlyList<Group> Groups { get; }

        public class Group
        {
            public Group(IReadOnlyList<string> extensions)
            {
                Extensions = extensions;
            }

            public Group(params string[] extensions) : this((IReadOnlyList<string>) extensions)
            {
            }

            public IReadOnlyList<string> Extensions { get; }
        }
    }
}
