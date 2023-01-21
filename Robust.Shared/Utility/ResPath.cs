﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Robust.Shared.Serialization;
using ArgumentException = System.ArgumentException;

namespace Robust.Shared.Utility;

/// <summary>
///     Provides object-oriented path manipulation for resource paths.
///     ResourcePaths are immutable.
/// </summary>
[PublicAPI, Serializable, NetSerializable]
public readonly struct ResPath : IEquatable<ResPath>
{
    /// <summary>
    ///     The separator for the file system of the system we are compiling to.
    ///     Backslash on Windows, forward slash on sane systems.
    /// </summary>
#if WINDOWS
    public const char SystemSeparator = '\\';
    public const string SystemSeparatorStr = @"\";
#else
    public const char SystemSeparator = '/';
    public const string SystemSeparatorStr = "/";
#endif
    /// <summary>
    /// Normalized separator. Chosen because <c>/</c> is illegal path
    /// character on Linux and Windows.
    /// </summary>
    public const char Separator = '/';

    /// <summary>
    ///     "." as a static. Separator used is <c>/</c>.
    /// </summary>
    public static readonly ResPath Self = new(".");

    /// <summary>
    ///     "/" (root) as a static. Separator used is <c>/</c>.
    /// </summary>
    public static readonly ResPath Root = new("/");

    /// <summary>
    ///     "/" (root) as a static. Separator used is <c>/</c>.
    /// </summary>
    public static readonly ResPath Empty = new("");

    /// <summary>
    ///     Internal system independent path. It uses `/` internally as
    ///     separator and will translate to it on creation.
    /// </summary>
    internal readonly string CanonPath;

    /// <summary>
    ///     Create a new path from a string, splitting it by the separator provided.
    /// </summary>
    /// <param name="canonPath">The string path to turn into a resource path.</param>
    /// <param name="separator">The separator for the resource path. If null or empty it will default to <c>/</c></param>
    /// <exception cref="ArgumentException">Thrown if you try to use "." as separator.</exception>
    public ResPath(string canonPath, char separator = '/')
    {
        if (separator is '.')
        {
            throw new ArgumentException("Separator may not be `.`  Prefer \\ or /");
        }


        if (canonPath == "" || canonPath == ".")
        {
            CanonPath = ".";
            return;
        }

        var sb = new StringBuilder(canonPath.Length);
        var segments = canonPath.Split(separator);
        if (canonPath[0] == separator) sb.Append('/');

        var needsSeparator = false;
        foreach (var segment in segments)
        {
            if ((segment == "." && segments.Length != 0) || segment == "") continue;

            if (needsSeparator) sb.Append('/');

            sb.Append(segment);
            needsSeparator = true;
        }

        CanonPath = sb.Length == 0 ? "." : sb.ToString();
    }

    private ResPath(string canonPath)
    {
        CanonPath = canonPath;
    }

    /// <summary>
    /// Needed for serv3
    /// </summary>
    public ResPath() : this("")
    {
    }

    /// <summary>
    /// Fast factory method will assume you did all necessary cleaning.
    /// WARNING: Breaking this assumption may lead to bugs
    /// </summary>
    /// <param name="assumedCanon"></param>
    /// <returns></returns>
    public static ResPath CreateUnsafePath(string assumedCanon)
    {
        // TODO is the switch necessary? might help with allocation at expense of branching
        return assumedCanon switch
        {
            "" => Empty,
            "." => Self,
            "/" => Root,
            _ => new ResPath(assumedCanon),
        };
    }

    /// <summary>
    ///     Returns true if the path is equal to "."
    /// </summary>
    public bool IsSelf => CanonPath == Self.CanonPath;

    /// <summary>
    ///     Returns the parent directory that this file resides in
    ///     as a <see cref="ResPath"/>.
    ///     If path points to folder, it will return parent directory
    /// </summary>
    /// <example>
    /// <code>
    ///     // Directory property of a directory resourcePath.
    ///     Assert.AreEqual("/foo", new ResPath("/foo/bar").Directory.ToString());
    ///     // Directory of a file resourcePath.
    ///     Assert.AreEqual("/foo", new ResPath("/foo/x.txt").Directory.ToString());
    /// </code>
    /// </example>
    public ResPath Directory
    {
        get
        {
            if (IsSelf) return Self;

            var ind = CanonPath.LastIndexOf('/');
            return ind != -1
                ? new ResPath(CanonPath[..ind])
                : Self;
        }
    }

    /// <summary>
    ///     Returns the file extension of <see cref="ResPath"/>, if any as string.
    ///     Returns "" if there is no file extension. (Hidden) Files starting
    ///     with period (".") are counted as files with no extension.
    ///     The extension returned does NOT include a period.
    /// </summary>
    /// <example>
    /// <code>
    ///     // file with normal extension
    ///     var picPath = new ResPath("/a/b/c.png");
    ///     Assert.AreEqual("png", picPath.Extension);
    ///     // hidden file starting with `.`
    ///     var gitignore = new ResPath("/a/b/.gitignore");
    ///     Assert.AreEqual("", gitignore.Extension);
    /// </code>
    /// </example>
    public string Extension
    {
        get
        {
            var filename = Filename;

            var ind = filename.LastIndexOf('.') + 1;
            return ind <= 1
                ? string.Empty
                : filename[ind..];
        }
    }

    /// <summary>
    ///     Returns the file name part of a <see cref="ResPath"/>, as string.
    ///     In essence reverse of <see cref="Extension"/>.
    ///     If last segment divided of a path (e.g. <c>/foo/bar/baz.png</c>) divided by separator (e.g <c>/</c>)
    ///     is considered a filename (e.g. <c>baz.png</c>). In that segment part before period is
    ///     considered filename (e.g. <c>baz</c>, unless file start with period, then whole segment
    ///     is filename without extension.
    /// </summary>
    /// <example>
    /// <code>
    ///     // file with normal extension
    ///     var picPath = new ResPath("/a/b/foo.png");
    ///     Assert.AreEqual("foo", picPath.FilenameWithoutExtension());
    ///     // hidden file starting with `.`
    ///     var gitignore = new ResPath("/a/b/.gitignore");
    ///     Assert.AreEqual(".gitignore", gitignore.FilenameWithoutExtension());
    /// </code>
    /// </example>
    public string FilenameWithoutExtension
    {
        get
        {
            var filename = Filename;

            var ind = filename.LastIndexOf('.');
            return ind <= 0
                ? filename
                : filename[..ind];
        }
    }

    /// <summary>
    ///     Returns the file name (folders are files) for given path,
    ///     or "." if path is empty.
    ///     If last segment divided of a path (e.g. <c>/foo/bar/baz.png</c>) divided by separator (e.g <c>/</c>)
    ///     is considered a filename (e.g. <c>baz.png</c>).
    /// </summary>
    /// <example>
    /// <code>
    ///     // file
    ///     Assert.AreEqual("c.png", new ResPath("/a/b/c.png").Filename);
    ///     // folder
    ///     Assert.AreEqual("foo", new ResPath("/foo").Filename);
    ///     // empty
    ///     Assert.AreEqual(".", new ResPath("").Filename);
    /// </code>
    /// </example>
    public string Filename
    {
        get
        {
            if (CanonPath is "." or "")
                return ".";

            // CanonicalResource[..^1] avoids last char if its a folder, it won't matter if
            // it's a filename
            // Uses +1 to skip `/` found in or starts from beginning of string
            // if we found nothing (ind == -1)
            var ind = CanonPath[..^1].LastIndexOf('/') + 1;
            return CanonPath[^1] == '/'
                ? CanonPath[ind .. ^1] // Omit last `/`
                : CanonPath[ind..];
        }
    }

    #region Operators & Equality

    /// <summary>
    ///     Converts this element to String
    /// </summary>
    /// <returns> System independent representation of path</returns>
    public override string ToString()
    {
        return CanonPath;
    }

    /// <inheritdoc/>
    public bool Equals(ResPath other)
    {
        return CanonPath == other.CanonPath;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ResPath other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return CanonPath.GetHashCode();
    }


    public static bool operator ==(ResPath left, ResPath right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ResPath left, ResPath right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    ///     Joins two resource paths together, with separator in between.
    ///     If the second path is absolute (i.e. rooted), the first path is completely ignored.
    ///     <seealso cref="IsRooted"/>
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the separators of the two paths do not match.</exception>
    // Copied comment
    // "Why use / instead of +" you may think:
    // * It's clever, although I got the idea from Python's `pathlib`.
    // * It avoids confusing operator precedence causing you to join two strings,
    //   because path + string + string != path + (string + string),
    //   whereas path / (string / string) doesn't compile.
    public static ResPath operator /(ResPath left, ResPath right)
    {
        if (right.IsRooted) return right;

        if (right.IsSelf) return left;

        return new ResPath(left.CanonPath + "/" + right.CanonPath);
    }

    /// <summary>
    ///     Joins resource and string path together, by converting string to <see cref="ResPath"/>
    ///     If the second path is absolute (i.e. rooted), the first path is completely ignored.
    ///     <seealso cref="IsRooted"/>
    /// </summary>
    public static ResPath operator /(ResPath left, string right)
    {
        return left / new ResPath(right);
    }

    #endregion

    #region WithMethods

    /// <summary>
    ///     Return a copy of this resource path with the file extension changed.
    /// </summary>
    /// <param name="newExtension">
    ///     The new file extension.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="newExtension"/> is null, empty,
    ///     contains <c>/</c> or is equal to <c>.</c>
    /// </exception>
    public ResPath WithExtension(string newExtension)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            throw new ArgumentException("New file name cannot be null or empty.");
        }

        if (newExtension.Contains('/'))
        {
            throw new ArgumentException("New file name cannot contain the separator.");
        }

        return WithName($"{FilenameWithoutExtension}.{newExtension}");
    }

    /// <summary>
    ///     Return a copy of this resource path with the file name changed.
    /// </summary>
    /// <param name="name">
    ///     The new file name.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="name"/> is null, empty,
    ///     contains <c>/</c> or is equal to <c>.</c>
    /// </exception>
    public ResPath WithName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("New file name cannot be null or empty.");
        }

        if (name.Contains('/'))
        {
            throw new ArgumentException("New file name cannot contain the separator.");
        }

        if (name == ".")
        {
            throw new ArgumentException("New file name cannot be '.'");
        }

        return new ResPath(Directory + "/" + name);
    }

    #endregion

    #region Roots & Relatives

    /// <summary>
    ///     Returns true if the path is rooted/absolute (starts with the separator).
    /// </summary>
    /// <seealso cref="IsRelative" />
    /// <seealso cref="ToRootedPath"/>
    public bool IsRooted => CanonPath[0] == '/';

    /// <summary>
    ///     Returns true if the path is not rooted.
    /// </summary>
    /// <seealso cref="IsRooted" />
    /// <seealso cref="ToRelativePath"/>
    public bool IsRelative => !IsRooted;

    /// <summary>
    ///     Gets the common base of two paths.
    /// </summary>
    /// <example>
    ///     <code>
    ///     var path1 = new ResourcePath("/a/b/c");
    ///     var path2 = new ResourcePath("/a/e/d");
    ///     Console.WriteLine(path1.RelativeTo(path2)); // prints "/a".
    ///     </code>
    /// </example>
    /// <param name="other">The other path.</param>
    /// <exception cref="ArgumentException">Thrown if there is no common base between the two paths.</exception>
    public ResPath CommonBase(ResPath other)
    {
        if (CanonPath.Equals(other.CanonPath))
        {
            return this;
        }

        var minLen = Math.Min(CanonPath.Length, other.CanonPath.Length);
        var lastSeparatorPos = IsRooted && other.IsRooted ? 1 : 0;
        for (int len = lastSeparatorPos; len < minLen; len++)
        {
            if (CanonPath[len] != other.CanonPath[len])
            {
                break;
            }

            if (CanonPath[len] != '/')
            {
                continue;
            }

            lastSeparatorPos = len;
        }

        if (lastSeparatorPos == 0)
        {
            throw new ArgumentException($"{this} and {other} have no common base.");
        }

        return new ResPath(CanonPath[..lastSeparatorPos]);
    }


    /// <summary>
    ///     Returns the path of how this instance is "relative" to <paramref name="basePath" />,
    ///     such that <c>basePath/result == this</c>.
    /// </summary>
    /// <example>
    ///     <code>
    ///     var path1 = new ResourcePath("/a/b/c");
    ///     var path2 = new ResourcePath("/a");
    ///     Console.WriteLine(path1.RelativeTo(path2)); // prints "b/c".
    ///     </code>
    /// </example>
    /// <exception cref="ArgumentException">Thrown if we are not relative to the base path.</exception>
    public ResPath RelativeTo(ResPath basePath)
    {
        if (TryRelativeTo(basePath, out var relative)) return relative.Value;

        throw new ArgumentException($"{CanonPath} does not start with {basePath}.");
    }

    /// <summary>
    ///     Try pattern version of <see cref="RelativeTo(ResPath)" />.
    /// </summary>
    /// <param name="basePath">The base path which we can be made relative to.</param>
    /// <param name="relative">The path of how we are relative to <paramref name="basePath" />, if at all.</param>
    /// <returns>True if we are relative to <paramref name="basePath" />, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown if the separators are not the same.</exception>
    public bool TryRelativeTo(ResPath basePath, [NotNullWhen(true)] out ResPath? relative)
    {
        if (this == basePath)
        {
            relative = Self;
            return true;
        }

        if (CanonPath.StartsWith(basePath.CanonPath))
        {
            var x = CanonPath[basePath.CanonPath.Length..]
                .TrimStart('/');
            relative = new ResPath(x);
            return true;
        }

        relative = null;
        return false;
    }


    /// <summary>
    ///     Turns the path into a rooted path by prepending it with the separator.
    ///     Does nothing if the path is already rooted.
    /// </summary>
    /// <seealso cref="IsRooted" />
    /// <seealso cref="ToRelativePath" />
    public ResPath ToRootedPath()
    {
        return IsRooted
            ? this
            : new ResPath("/" + CanonPath);
    }

    /// <summary>
    ///     Turns the path into a relative path by removing the root separator, if any.
    ///     Does nothing if the path is already relative.
    /// </summary>
    /// <seealso cref="IsRelative" />
    /// <seealso cref="ToRootedPath" />
    public ResPath ToRelativePath()
    {
        if (IsRelative) return this;

        return this == Root
            ? Self
            : new ResPath(CanonPath[1..]);
    }

    /// <summary>
    ///     Turns the path into a relative path with system-specific separator.
    ///     For usage in disk I/O.
    /// </summary>
    public string ToRelativeSystemPath()
    {
        return ToRelativePath().ChangeSeparator(SystemSeparatorStr);
    }

    /// <summary>
    ///     Converts a system path into a resource path.
    /// </summary>
    public static ResPath FromRelativeSystemPath(string path, char newSeparator = SystemSeparator)
    {
        // ReSharper disable once RedundantArgumentDefaultValue
        return new ResPath(path, newSeparator);
    }

    #endregion

    /// <summary>
    ///     Returns cleaned version of the resource path, removing <c>..</c>.
    /// </summary>
    /// <remarks>
    ///     If <c>..</c> appears at the base of a path, it is left alone. If it appears at root level (like <c>/..</c>) it is removed entirely.
    /// </remarks>
    public ResPath Clean()
    {
        var segments = new List<string>();
        if (CanonPath == "")
        {
            return Empty;
        }

        if (IsRooted)
        {
            segments.Add("/");
        }

        foreach (var segment in EnumerateSegments())
        {
            // Skip pointless segments
            if (segment == "." || segment == "")
            {
                continue;
            }

            // If you have ".." cleaning that up doesn't remove that.
            if (segment == ".." && segments.Count > 0)
            {
                if (segments is ["/"])
                {
                    continue;
                }

                var pos = segments.Count - 1;
                if (segments[pos] != "..")
                {
                    segments.RemoveAt(pos);
                    continue;
                }
            }

            segments.Add(segment);
        }

        // Build Canon path from segments with StringBuilder
        var sb = new StringBuilder(CanonPath.Length);
        var start = IsRooted && segments.Count > 1 ? 1 : 0;
        for (var i = 0; i < segments.Count; i++)
        {
            if (i > start)
            {
                sb.Append('/');
            }

            sb.Append(segments[i]);
        }

        return sb.Length == 0
            ? Self
            : new ResPath(sb.ToString());
    }

    /// <summary>
    ///     Check whether a path is clean, i.e. <see cref="Clean"/> would not modify it.
    /// </summary>
    /// <returns>if true if path wouldn't be modifed </returns>
    public bool IsClean()
    {
        var segments = CanonPath.Split(Separator).ToArray();
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i] != "..") continue;

            if (IsRooted)
            {
                return false;
            }

            if (i > 0 && segments[i - 1] != "..")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Turns the path into a relative path with system-specific separator.
    ///     For usage in disk I/O.
    /// </summary>
    public string ChangeSeparator(string newSeparator)
    {
        if (newSeparator is "." or "\0") throw new ArgumentException("New separator can't be `.` or `NULL`");

        return newSeparator == "/"
            ? CanonPath
            : CanonPath.Replace("/", newSeparator);
    }


    /// <summary>
    ///     Enumerates the segments of this path.
    /// </summary>
    /// <remarks>
    ///     Segments are returned from highest to deepest.
    ///     For example <c>/a/b</c> will yield <c>a</c> then <c>b</c>.
    ///     No special indication is given for rooted paths,
    ///     so <c>/a/b</c> yields the same as <c>a/b</c>.
    /// </remarks>
    public IEnumerable<string> EnumerateSegments()
    {
        foreach (var fragment in CanonPath.Split(Separator))
        {
            yield return fragment;
        }
    }
}

