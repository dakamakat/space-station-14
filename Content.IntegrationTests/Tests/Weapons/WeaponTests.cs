#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Zombies;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Weapons;

public sealed class WeaponTests : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman"; // The default test mob only has one hand
    private static readonly EntProtoId MobHuman = "MobHuman";
    private static readonly EntProtoId SniperMosin = "WeaponSniperMosin";
    private static readonly EntProtoId MobMouse = "MobMouse";
    private static readonly EntProtoId BioKnife = "XenoborgBioKnife";
    private static readonly ProtoId<MetabolizerTypePrototype> HumanMetabolizer = "Human";

    [SidedDependency(Side.Server)] private DamageableSystem _sDamageable = default!;
    [SidedDependency(Side.Server)] private SharedGunSystem _sGun = default!;
    [SidedDependency(Side.Server)] private MetabolizerSystem _sMetabolizer = default!;
    [SidedDependency(Side.Server)] private ZombieSystem _sZombie = default!;

    [Test]
    [Description("Tests that a gun that needs wielding can be spawned and shot properly.")]
    public async Task GunRequiresWieldTest()
    {
        await AddAtmosphere(); // prevent the Urist from suffocating

        var uristNet = await SpawnTarget(MobHuman);
        Entity<DamageableComponent> sUrist = (ToServer(uristNet), Comp<DamageableComponent>(uristNet));

        var mosinNet = await PlaceInHands(SniperMosin);
        var mosinEnt = ToServer(mosinNet);

        await Pair.RunSeconds(2f); // Guns have a cooldown when picking them up.

        Assert.That(HasComp<GunRequiresWieldComponent>(mosinNet),
            $"Looks like you've removed the '{nameof(GunRequiresWieldComponent)}' from the mosin sniper." +
            "If this was intentional, please update WeaponTests.cs to reflect this change!");

        var startAmmo = _sGun.GetAmmoCount(mosinEnt);
        var wieldComp = Comp<WieldableComponent>(mosinNet);

        Assert.That(startAmmo, Is.GreaterThan(0), "Mosin was spawned with no ammo!");
        Assert.That(wieldComp.Wielded, Is.False, "Mosin was spawned wielded!");

        await AttemptShoot(uristNet, false); // should fail due to not being wielded
        var updatedAmmo = _sGun.GetAmmoCount(mosinEnt);

        Assert.That(updatedAmmo,
            Is.EqualTo(startAmmo),
            "Mosin discharged ammo when the weapon should not have fired!");
        Assert.That(_sDamageable.GetPositiveDamage(sUrist).GetTotal(),
            Is.EqualTo(FixedPoint2.Zero),
            "Urist took damage when the weapon should not have fired!");

        await UseInHand();

        Assert.That(wieldComp.Wielded, Is.True, "Mosin did not wield upon interaction!");

        await AttemptShoot(uristNet);
        updatedAmmo = _sGun.GetAmmoCount(mosinEnt);

        Assert.That(updatedAmmo, Is.EqualTo(startAmmo - 1), "Mosin failed to discharge appropriate amount of ammo!");
        Assert.That(_sDamageable.GetPositiveDamage(sUrist).GetTotal(),
            Is.GreaterThan(FixedPoint2.Zero),
            "Mosin was fired but urist sustained no damage!");
    }

    [Test]
    [Description("Tests that per-target melee bonus damage (bio knife) only applies to matching targets of a wide attack.")]
    public async Task PerTargetMeleeBonusDamageTest()
    {
        await AddAtmosphere(); // prevent the mobs from suffocating

        var humanNet = await SpawnTarget(MobHuman);
        // Put the mouse on another tile within reach of the player.
        var mouseCoords = SEntMan.GetNetCoordinates(SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(0f, 1f)));
        var mouseNet = await Spawn(MobMouse, mouseCoords);

        var sHuman = ToServer(humanNet);
        var sMouse = ToServer(mouseNet);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_sMetabolizer.BodyHasMetabolizer(sHuman, HumanMetabolizer), "Human has no human metabolizer!");
            Assert.That(_sMetabolizer.BodyHasMetabolizer(sMouse, HumanMetabolizer), Is.False, "Mouse has a human metabolizer!");
        });

        await PlaceInHands(BioKnife);
        await Pair.RunSeconds(2f); // Melee weapons have a cooldown when picking them up.

        await AttemptHeavyAttack(humanNet, mouseNet);
        await RunTicks(5);

        var humanDamage = _sDamageable.GetPositiveDamage((sHuman, Comp<DamageableComponent>(humanNet)));
        var mouseDamage = _sDamageable.GetPositiveDamage((sMouse, Comp<DamageableComponent>(mouseNet)));

        Assert.Multiple(() =>
        {
            Assert.That(mouseDamage.GetTotal(), Is.GreaterThan(FixedPoint2.Zero), "Mouse was not hit by the wide attack!");
            Assert.That(humanDamage.DamageDict.GetValueOrDefault("Poison"), Is.GreaterThan(FixedPoint2.Zero),
                "Human was hit by the bio knife but took no bonus poison damage!");
            Assert.That(mouseDamage.DamageDict.GetValueOrDefault("Poison"), Is.EqualTo(FixedPoint2.Zero),
                "Mouse took bio knife bonus damage even though it does not have the selected metabolizer!");
        });
    }

    [Test]
    [Description("Tests that a zombie wide attack skips fellow zombies without cancelling the damage to the other targets.")]
    public async Task ZombieWideAttackOnlySkipsZombieTargetsTest()
    {
        await AddAtmosphere(); // prevent the mobs from suffocating

        var humanNet = await SpawnTarget(MobHuman);
        // Put the other zombie on another tile within reach of the player.
        var zombieCoords = SEntMan.GetNetCoordinates(SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(0f, 1f)));
        var zombieNet = await Spawn(MobHuman, zombieCoords);

        var sHuman = ToServer(humanNet);
        var sZombie = ToServer(zombieNet);

        await Server.WaitPost(() =>
        {
            _sZombie.ZombifyEntity(SPlayer);
            _sZombie.ZombifyEntity(sZombie);
        });
        await Pair.RunSeconds(2f); // Let the zombie's own melee cooldown pass.

        var zombieDamageBefore = _sDamageable.GetPositiveDamage((sZombie, Comp<DamageableComponent>(zombieNet))).GetTotal();

        await AttemptHeavyAttack(humanNet, zombieNet);
        await RunTicks(5);

        var humanDamage = _sDamageable.GetPositiveDamage((sHuman, Comp<DamageableComponent>(humanNet))).GetTotal();
        var zombieDamage = _sDamageable.GetPositiveDamage((sZombie, Comp<DamageableComponent>(zombieNet))).GetTotal();

        Assert.Multiple(() =>
        {
            Assert.That(humanDamage, Is.GreaterThan(FixedPoint2.Zero),
                "Human took no damage from a zombie wide attack that also hit another zombie!");
            Assert.That(zombieDamage, Is.LessThanOrEqualTo(zombieDamageBefore),
                "Zombie took damage from another zombie's attack!");
        });
    }
}
