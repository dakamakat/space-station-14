using System.Linq;
using Content.Shared.Metabolism;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Weapons.Melee.Metabolizer;

public sealed partial class BonusDamageOnMetabolismSystem : EntitySystem
{
    [Dependency] private MetabolizerSystem _metabolizer = default!;

    [SubscribeLocalEvent]
    private void OnGetVerb(Entity<BonusDamageOnMetabolismComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var allMetabolizers = ProtoMan.EnumeratePrototypes<MetabolizerTypePrototype>().ToList().OrderBy(x => Loc.GetString(x.LocalizedName));

        byte index = 0;
        foreach (var metabolizer in allMetabolizers)
        {
            if (ent.Comp.ExcludedMetabolizers.Contains(metabolizer))
                continue;

            var currIndex = index;
            var verb = new Verb
            {
                Priority = currIndex,
                Category = VerbCategory.Metabolizers,
                Disabled = ent.Comp.SelectedMetabolizer == metabolizer,
                Act = () =>
                {
                    ent.Comp.SelectedMetabolizer = metabolizer;
                    Dirty(ent);
                },
                Text = Loc.GetString(metabolizer.LocalizedName),
            };
            args.Verbs.Add(verb);
            index++;
        }
    }

    [SubscribeLocalEvent]
    private void OnGetTargetDamage(Entity<BonusDamageOnMetabolismComponent> ent, ref GetMeleeTargetDamageEvent args)
    {
        if (ent.Comp.SelectedMetabolizer is not { } metabolizer)
            return;

        if (TryComp<MobStateComponent>(args.Target, out var mobState) && !ent.Comp.ValidMobStates.Contains(mobState.CurrentState))
            return;

        if (!_metabolizer.BodyHasMetabolizer(args.Target, metabolizer))
            return;

        args.BonusDamage += ent.Comp.Damage;
    }
}
