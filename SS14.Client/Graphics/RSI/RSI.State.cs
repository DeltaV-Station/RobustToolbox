﻿using SS14.Shared.Maths;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace SS14.Client.Graphics
{
    public sealed partial class RSI
    {
        /// <summary>
        ///     Represents a single icon state inside an RSI.
        /// </summary>
        public sealed class State
        {
            public Vector2u Size { get; }
            public StateId StateId { get; }
            public DirectionType Directions { get; }
            public int DirectionsCount
            {
                get
                {
                    switch (Directions)
                    {
                        case DirectionType.Dir1:
                            return 1;
                        case DirectionType.Dir4:
                            return 4;
                        case DirectionType.Dir8:
                            return 8;
                        default:
                            throw new InvalidOperationException("Unknown direction");
                    }
                }
            }
            private (Texture icon, float delay)[][] Icons;

            public State(Vector2u size, StateId stateId, DirectionType direction, (Texture icon, float delay)[][] icons)
            {
                Size = size;
                StateId = stateId;
                Directions = direction;
                Icons = icons;
            }

            public enum DirectionType : byte
            {
                Dir1,
                Dir4,
                Dir8,
            }

            public (Texture icon, float delay) GetFrame(int direction, int frame)
            {
                return Icons[direction][frame];
            }

            public IReadOnlyCollection<(Texture icon, float delay)> GetDirectionFrames(int direction)
            {
                return Icons[direction];
            }
        }
    }
}
