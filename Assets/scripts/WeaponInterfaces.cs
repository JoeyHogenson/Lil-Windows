using System;
using UnityEngine;

// Weapon Interfaces namespace
namespace WeaponInterfaces
{
    /// <summary>
    /// Base gun interface with common properties and methods.
    /// </summary>
    public interface IGun
    {
        int Damage { get; }
        float ReloadTime { get; }

        void Shoot()
        {
            Console.WriteLine("Default shoot implementation");
        }

        void Reload()
        {
            Console.WriteLine("Default reload implementation");
        }
    }

    /// <summary>
    /// An interface for guns with a specific fire rate.
    /// </summary>
    public interface IFullAutoGun : IGun
    {
        float FireRate { get; }
    }

    /// <summary>
    /// An interface for guns with a magazine and ammo.
    /// </summary>
    public interface IMagazineGun : IGun
    {
        int MagazineSize { get; }
        int CurrentAmmo { get; }
    }

    /// <summary>
    /// An interface for guns with a scope or aiming capability.
    /// </summary>
    public interface ISniperRifle : IGun
    {
        void ZoomIn();
        void ZoomOut();
    }
}

// Weapon Interfaces namespace
namespace WeaponInterfaces
{
    /// <summary>
    /// Base weapon interface with common properties and methods.
    /// </summary>
    public interface IWeapon 
    {
        int Damage { get; }

        void Attack();
    }

    /// <summary>
    /// Interface for melee weapons.
    /// </summary>
    public interface IMeleeWeapon : IWeapon
    {
        float Range { get; }
    }

    /// <summary>
    /// Interface for a melee weapon with a special ability.
    /// </summary>
    public interface ISpecialMeleeWeapon : IMeleeWeapon
    {
        void SpecialAttack();
    }

    /// <summary>
    /// Interface for throwable melee weapons (e.g., throwable knives).
    /// </summary>
    public interface IThrowableMeleeWeapon : IMeleeWeapon
    {
        void Throw();
    }
}