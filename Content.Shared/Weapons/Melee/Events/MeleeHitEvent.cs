using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Shared.Weapons.Melee.Events;

/// <summary>
///     Raised directed on the melee weapon entity used to attack something in combat mode,
///     whether through a click attack or wide attack.
/// </summary>
/// <remarks>
///     This is raised once per attack, so <see cref="BonusDamage"/> and <see cref="ModifiersList"/> apply to every hit entity.
///     For target-specific damage changes use <see cref="GetMeleeTargetDamageEvent"/>.
/// </remarks>
public sealed class MeleeHitEvent : HandledEntityEventArgs
{
    /// <summary>
    ///     The base amount of damage dealt by the melee hit.
    /// </summary>
    public readonly DamageSpecifier BaseDamage;

    /// <summary>
    ///     Modifier sets to apply to the hit event when it's all said and done.
    ///     This should be modified by adding a new entry to the list.
    /// </summary>
    public List<DamageModifierSet> ModifiersList = new();

    /// <summary>
    ///     Damage to add to the default melee weapon damage. Applied before modifiers.
    /// </summary>
    /// <remarks>
    ///     This might be required as damage modifier sets cannot add a new damage type to a DamageSpecifier.
    /// </remarks>
    public DamageSpecifier BonusDamage = new();

    /// <summary>
    ///     A set containing every hit entity. Can be zero.
    /// </summary>
    public IReadOnlySet<EntityUid> HitEntities;

    /// <summary>
    ///     Used to define a new hit sound in case you want to override the default GenericHit.
    ///     Also gets a pitch modifier added to it.
    /// </summary>
    public SoundSpecifier? HitSoundOverride;

    /// <summary>
    /// The user who attacked with the melee weapon.
    /// </summary>
    public readonly EntityUid User;

    /// <summary>
    /// The melee weapon used.
    /// </summary>
    public readonly EntityUid Weapon;

    /// <summary>
    /// The direction of the attack.
    /// If null, it was a click-attack.
    /// </summary>
    public readonly Vector2? Direction;

    /// <summary>
    /// Check if this is true before attempting to do something during a melee attack other than changing/adding bonus damage. <br/>
    /// For example, do not spend charges unless <see cref="IsHit"/> equals true.
    /// </summary>
    /// <remarks>
    /// Examining melee weapons calls this event, but with <see cref="IsHit"/> set to false.
    /// </remarks>
    public bool IsHit = true;

    public MeleeHitEvent(HashSet<EntityUid> hitEntities, EntityUid user, EntityUid weapon, DamageSpecifier baseDamage, Vector2? direction)
    {
        HitEntities = hitEntities;
        User = user;
        Weapon = weapon;
        BaseDamage = baseDamage;
        Direction = direction;
    }
}

/// <summary>
/// Raised on a melee weapon to calculate potential damage bonuses or decreases.
/// </summary>
[ByRefEvent]
public record struct GetMeleeDamageEvent(EntityUid Weapon, DamageSpecifier Damage, List<DamageModifierSet> Modifiers, EntityUid User, bool ResistanceBypass = false);

/// <summary>
/// Raised directed on the melee weapon once for every entity that is about to be damaged by a melee attack,
/// after <see cref="MeleeHitEvent"/> and <see cref="AttackedEvent"/>.
/// Use this instead of <see cref="MeleeHitEvent"/> for bonus damage or modifiers that should only apply to specific targets.
/// </summary>
/// <param name="Weapon">The melee weapon used.</param>
/// <param name="User">The entity attacking with the weapon.</param>
/// <param name="Target">The entity about to be damaged.</param>
/// <param name="BonusDamage">Extra damage dealt to only this target. Applied before modifiers.</param>
/// <param name="Modifiers">Extra modifier sets applied to the damage dealt to only this target.</param>
[ByRefEvent]
public record struct GetMeleeTargetDamageEvent(EntityUid Weapon, EntityUid User, EntityUid Target, DamageSpecifier BonusDamage, List<DamageModifierSet> Modifiers);

/// <summary>
/// Raised on a melee weapon to calculate the attack rate.
/// </summary>
[ByRefEvent]
public record struct GetMeleeAttackRateEvent(EntityUid Weapon, float Rate, float Multipliers, EntityUid User);

/// <summary>
/// Raised on a melee weapon to calculate the heavy damage modifier.
/// </summary>
[ByRefEvent]
public record struct GetHeavyDamageModifierEvent(EntityUid Weapon, FixedPoint2 DamageModifier, float Multipliers, EntityUid User);
