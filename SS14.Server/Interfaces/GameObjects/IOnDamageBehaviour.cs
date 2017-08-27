﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS14.Server.GameObjects;

namespace SS14.Server.Interfaces
{
    /// <summary>
    /// Any component/entity that has behaviour linked to taking damage should implement this interface.
    /// TODO: Don't know how to work around this currently, but due to how events work
    /// you need to hook it up to the DamageableComponent via Initialize().
    /// See DestructibleComponent.Initialize() for an example.
    /// </summary>
    interface IOnDamageBehaviour
    {
        /// <summary>
        /// Gets a list of all DamageThresholds this component/entity are interested in.
        /// </summary>
        /// <returns>List of DamageThresholds to be added to DamageableComponent for watching.</returns>
        List<DamageThreshold> GetAllDamageThresholds();

        /// <summary>
        /// Damage threshold passed event hookup.
        /// </summary>
        /// <param name="obj">Damageable component.</param>
        /// <param name="e">Damage threshold and whether it's passed in one way or another.</param>
        void OnDamageThresholdPassed(object obj, DamageThresholdPassedEventArgs e);
    }
}
