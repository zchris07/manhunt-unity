using System;
using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// Health, shield and stamina, as plain logic, following the original. Health runs 0 to 1 and never regenerates;
    /// the mini shield fills a second bar (0 to 1) that damage takes first. At 0 health the survivor is downed.
    /// Sprinting drains stamina; it refills after a short pause, and once it runs out the player cannot sprint again
    /// until it is back above a quarter.
    /// </summary>
    [Serializable]
    public sealed class Vitals
    {
        public float maxStamina = 100f;
        [Tooltip("Stamina per second while sprinting.")]
        public float sprintDrain = 18f;
        [Tooltip("Stamina per second while not sprinting, after the delay.")]
        public float regen = 12f;
        public float regenDelay = 0.8f;
        [Tooltip("After running dry, sprinting comes back once stamina is above this fraction.")]
        [Range(0f, 1f)] public float recoverFraction = 0.25f;

        /// <summary>Health left after getting back up (the original's revive).</summary>
        public const float ReviveHealth = 1f / 3f;

        /// <summary>0 to 1.</summary>
        public float Health { get; private set; }
        /// <summary>0 to 1: the mini shield's blue bar.</summary>
        public float Shield { get; private set; }
        public float Stamina { get; private set; }
        public bool Exhausted { get; private set; }
        public bool IsDowned => Health <= 0f;

        /// <summary>Raised with the damage taken (shield and health together).</summary>
        public event Action<float> Damaged;
        public event Action Downed;

        float sinceSprint;

        public Vitals() => Reset();

        public void Reset()
        {
            Health = 1f;
            Shield = 0f;
            Stamina = maxStamina;
            Exhausted = false;
            sinceSprint = regenDelay;
        }

        public bool CanSprint => !IsDowned && !Exhausted && Stamina > 0f;

        /// <summary>Advances stamina: <paramref name="sprinting"/> is true while the player is actually running.</summary>
        public void Tick(float dt, bool sprinting)
        {
            if (IsDowned) return;
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

        /// <summary>Takes <paramref name="amount"/> (a fraction of a full health bar): the shield first, then health.</summary>
        public void TakeDamage(float amount)
        {
            if (IsDowned || amount <= 0f) return;
            float fromShield = Mathf.Min(Shield, amount);
            Shield -= fromShield;
            Health = Mathf.Max(0f, Health - (amount - fromShield));
            Damaged?.Invoke(amount);
            if (IsDowned)
            {
                Stamina = 0f;
                Downed?.Invoke();
            }
        }

        /// <summary>Adds a fraction of a full bar of health (no effect while downed).</summary>
        public void Heal(float amount)
        {
            if (IsDowned || amount <= 0f) return;
            Health = Mathf.Min(1f, Health + amount);
        }

        /// <summary>Adds to the shield bar, up to full.</summary>
        public void AddShield(float amount)
        {
            if (IsDowned || amount <= 0f) return;
            Shield = Mathf.Min(1f, Shield + amount);
        }

        /// <summary>Gets back up from downed with a third of the health bar.</summary>
        public void StandUp()
        {
            if (!IsDowned) return;
            Health = ReviveHealth;
            Stamina = maxStamina * recoverFraction;
            Exhausted = false;
        }

        public void RestoreStamina(float amount)
        {
            if (IsDowned || amount <= 0f) return;
            Stamina = Mathf.Min(maxStamina, Stamina + amount);
            if (Exhausted && Stamina >= maxStamina * recoverFraction) Exhausted = false;
        }
    }
}
