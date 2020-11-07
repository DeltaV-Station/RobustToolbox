﻿using System;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Maths;

namespace Robust.Client.UserInterface.Controls
{
    /// <summary>
    ///     A container that lays out its children in a grid.
    /// </summary>
    public class GridContainer : Container
    {
        private int _columns = 1;

        /// <summary>
        ///     The amount of columns to organize the children into.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown if the value assigned is less than or equal to 0.
        /// </exception>
        public int Columns
        {
            get => _columns;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be greater than zero.");
                }

                _columns = value;
                MinimumSizeChanged();
                UpdateLayout();
            }
        }

        /// <summary>
        ///     The amount of rows being used for the current amount of children.
        /// </summary>
        public int Rows
        {
            get
            {
                if (ChildCount == 0)
                {
                    return 1;
                }

                var div = ChildCount / Columns;
                if (ChildCount % Columns != 0)
                {
                    div += 1;
                }

                return div;
            }
        }

        private int? _vSeparationOverride;

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        public int? VSeparationOverride
        {
            get => _vSeparationOverride;
            set => _vSeparationOverride = value;
        }

        private int? _hSeparationOverride;

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        public int? HSeparationOverride
        {
            get => _hSeparationOverride;
            set => _hSeparationOverride = value;
        }

        private Vector2i Separations => (_hSeparationOverride ?? 4, _vSeparationOverride ?? 4);

        protected override Vector2 CalculateMinimumSize()
        {
            var (wSep, hSep) = (Vector2i) (Separations * UIScale);
            var rows = Rows;

            // Minimum width of the columns.
            Span<int> columnWidths = stackalloc int[_columns];
            // Minimum height of the rows.
            Span<int> rowHeights = stackalloc int[rows];

            var index = 0;
            foreach (var child in Children)
            {
                if (!child.Visible)
                {
                    index--;
                    continue;
                }

                var row = index / _columns;
                var column = index % _columns;

                var (minSizeX, minSizeY) = child.CombinedPixelMinimumSize;
                columnWidths[column] = Math.Max(minSizeX, columnWidths[column]);
                rowHeights[row] = Math.Max(minSizeY, rowHeights[row]);

                index += 1;
            }

            var minWidth = AccumSizes(columnWidths, wSep);
            var minHeight = AccumSizes(rowHeights, hSep);

            return new Vector2(minWidth, minHeight) / UIScale;
        }

        private static int AccumSizes(Span<int> sizes, int separator)
        {
            var totalSize = 0;
            var firstColumn = true;

            foreach (var size in sizes)
            {
                totalSize += size;

                if (firstColumn)
                {
                    firstColumn = false;
                }
                else
                {
                    totalSize += separator;
                }
            }

            return totalSize;
        }

        protected override void LayoutUpdateOverride()
        {
            var rows = Rows;

            // Minimum width of the columns.
            Span<int> columnWidths = stackalloc int[_columns];
            // Minimum height of the rows.
            Span<int> rowHeights = stackalloc int[rows];
            // Columns that are set to expand horizontally.
            Span<bool> columnExpandWidth = stackalloc bool[_columns];
            // Rows that are set to expand vertically.
            Span<bool> rowExpandHeight = stackalloc bool[rows];

            // Get minSize and size flag expand of each column and row.
            // All we need to apply the same logic BoxContainer does.
            var index = 0;
            foreach (var child in Children)
            {
                if (!child.Visible)
                {
                    continue;
                }

                var row = index / _columns;
                var column = index % _columns;

                var (minSizeX, minSizeY) = child.CombinedPixelMinimumSize;
                columnWidths[column] = Math.Max(minSizeX, columnWidths[column]);
                rowHeights[row] = Math.Max(minSizeY, rowHeights[row]);
                columnExpandWidth[column] = columnExpandWidth[column] || (child.SizeFlagsHorizontal & SizeFlags.Expand) != 0;
                rowExpandHeight[row] = rowExpandHeight[row] || (child.SizeFlagsVertical & SizeFlags.Expand) != 0;

                index += 1;
            }

            // Basically now we just apply BoxContainer logic on rows and columns.
            var (vSep, hSep) = (Vector2i) (Separations * UIScale);
            var stretchMinX = 0;
            var stretchMinY = 0;
            // We do not use stretch ratios because Godot doesn't,
            // which makes sense since what happens if two things on the same column have a different stretch ratio?
            // Maybe there's an answer for that but I'm too lazy to think of a concrete solution
            // and it would make this code more complex so...
            // pass.
            var stretchCountX = 0;
            var stretchCountY = 0;

            for (var i = 0; i < columnWidths.Length; i++)
            {
                if (!columnExpandWidth[i])
                {
                    stretchMinX += columnWidths[i];
                }
                else
                {
                    stretchCountX++;
                }
            }

            for (var i = 0; i < rowHeights.Length; i++)
            {
                if (!rowExpandHeight[i])
                {
                    stretchMinY += rowHeights[i];
                }
                else
                {
                    stretchCountY++;
                }
            }

            var stretchMaxX = Width - hSep * (_columns - 1);
            var stretchMaxY = Height - vSep * (rows - 1);

            var stretchAvailX = Math.Max(0, stretchMaxX - stretchMinX);
            var stretchAvailY = Math.Max(0, stretchMaxY - stretchMinY);

            for (var i = 0; i < columnWidths.Length; i++)
            {
                if (!columnExpandWidth[i])
                {
                    continue;
                }

                columnWidths[i] = (int) (stretchAvailX / stretchCountX);
            }

            for (var i = 0; i < rowHeights.Length; i++)
            {
                if (!rowExpandHeight[i])
                {
                    continue;
                }

                rowHeights[i] = (int) (stretchAvailY / stretchCountY);
            }

            // Actually lay them out.
            var vOffset = 0;
            var hOffset = 0;
            index = 0;
            for (var i = 0; i < ChildCount; i++, index++)
            {
                var child = GetChild(i);
                if (!child.Visible)
                {
                    index--;
                    continue;
                }

                var row = index / _columns;
                var column = index % _columns;

                if (column == 0)
                {
                    // Just started a new row.
                    hOffset = 0;
                    if (row != 0)
                    {
                        vOffset += vSep + rowHeights[row - 1];
                    }
                }

                var box = UIBox2i.FromDimensions(hOffset, vOffset, columnWidths[column], rowHeights[row]);
                FitChildInPixelBox(child, box);

                hOffset += columnWidths[column] + hSep;
            }
        }
    }
}
