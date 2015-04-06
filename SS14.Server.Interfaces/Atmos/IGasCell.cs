﻿using BKSystem.IO;
using SS14.Server.Interfaces.Map;
using SS14.Shared;
using SS14.Shared.Maths;

namespace SS14.Server.Interfaces.Atmos
{
    public interface IGasCell
    {
        Vector2 GasVelocity { get; }
        float TotalGas { get; }
        bool Calculated { get; }
        float Pressure { get; }
        void Update();
        void InitSTP();
        void CalculateNextGasAmount(IMapManager m);
        int PackDisplayBytes(BitStream bits, bool all = false);
        float GasAmount(GasType type);
        void AddGas(float amount, GasType gas);
        void SetNeighbours(IMapManager m);
    }
}