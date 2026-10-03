using System;
using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// Health and stamina, as plain logic. Sprinting drains stamina; it refills after a short pause, and
    /// once it runs out the player cannot sprint again until it is back above a quarter.
    /// </summary>
    [Serializable]
    public sealed class Vitals
    {
        public float maxHealth = 100f;
        public float maxStamina = 100f;
        [Tooltip("Stamina per second while sprinting.")]
        public float sprintDrain = 18f;
        [Tooltip("Stamina per second while not sprinting, after the delay.")]
        public float regen = 12f;
        public float regenDelay = 0.8f;
        [Tooltip("After running dry, sprinting comes back once stamina is above this fraction.")]
        [Range(0f, 1f)] public float recoverFraction = 0.25f;

        public float Health { get; private set; }
        public float Stamina { get; private set; }
        public bool Exhausted { get; private set; }
        public bool IsDead => Health <= 0f;

        public event Action<float> Damaged;
        public event Action Died;

        float sinceSprint;

        public Vitals() => Reset();

        public void Reset()
        {
            Health = maxHealth;
            Stamina = maxStamina;
            Exhausted = false;
            sinceSprint = regenDelay;
        }

        public bool CanSprint => !IsDead && !Exhausted && Stamina > 0f;

        /// <summary>Advances stamina: <paramref name="sprinting"/> is true while the player is actually running.</summary>
        public void Tick(float dt, bool sprinting)
        {
            if (IsDead) return;
            if (sprinting && CanSprint)
            {
                Stamina = Mathf.Max(0f, Stamina - sprintDrain * dt);
                sinceSprint = 0f;
                if (Stamina <= 0f) Exhausted = true;
                return;
            }
            sinceSprint += dt;
            if (sinceSprint >= regenDelay) Stamina = Mathf.Min(maxStamina, Stamina + regen * dt);
            if (Exhausted && Stamina >= maxStamina * recoverFraction) Exhausted = false;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            Damaged?.Invoke(amount);
            if (IsDead) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Min(maxHealth, Health + amount);
        }

        public void RestoreStamina(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Stamina = Mathf.Min(maxStamina, Stamina + amount);
            if (Exhausted && Stamina >= maxStamina * recoverFraction) Exhausted = false;
        }
    }
}
