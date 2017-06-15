using System;

namespace SS14.Shared.GameObjects.Components.Damageable.Health
{
    [Serializable]
    public class HealthComponentState : DamageableComponentState
    {
        public float Health;
        public float MaxHealth;

        public HealthComponentState(bool isDead, float health, float maxHealth)
            : base(isDead)
        {
            Health = health;
            MaxHealth = maxHealth;
        }
    }
}
