using Content.Server._Shitmed.ItemSwitch;
using Content.Shared.Weapons.Melee.Events;
using Content.Omu.Server.SmiteOnMeleeHit.Components;
using Robust.Shared.Player;
using Content.Shared.Projectiles;
using Content.Goobstation.Shared.MisandryBox.Smites;
using Content.Shared.Database;
using Content.Shared.Administration.Logs;
using Content.Shared.Objectives.Components;
using Content.Shared.Humanoid;
using Content.Server.Chat.Managers;

namespace Content.Omu.Server.SmiteOnMeleeHit.Systems;

public sealed class SmiteOnMeleeHit : EntitySystem
{
    [Dependency] private readonly ThunderstrikeSystem _thunderstrikeSystem = default!;

    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;

    [Dependency] private readonly IChatManager _achat = default!;

    private const string JSTARE = "/Textures/_Omu/ScreenEffects/JohnStare.png";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmiteOnMeleeHitComponent, MeleeHitEvent>(OnHit, before: [typeof(ItemSwitchSystem)]);
    }

    private void OnHit(Entity<SmiteOnMeleeHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<ProjectileComponent>(hit))
            {
                _adminLog.Add(LogType.Supermatter, LogImpact.Medium, $"SmiteOnMeleeHit {ToPrettyString(args.Weapon)} has consumed {ToPrettyString(hit)}");
                if (HasComp<HumanoidAppearanceComponent>(hit) || HasComp<ActorComponent>(hit) || HasComp<StealTargetComponent>(hit))     //omu - alert for humanoids, controld entities, and steal hits
                    _achat.SendAdminAlert($"SmiteOnMeleeHit {ToPrettyString(args.Weapon)} has consumed {ToPrettyString(hit)}");      //omu admin alert
                if (HasComp<ActorComponent>(hit))
                    _thunderstrikeSystem.Smite(hit, true, null, JSTARE); // funny :3
                else
                {
                    EntityManager.SpawnEntity("Ash", Transform(hit).Coordinates);
                    //_audio.PlayPvs(sm.DustSound, uid); //Comes from "Content.Goobstation.Server\Supermatter\Systems\SupermatterSystem.cs" Define using soundHit on .yml side
                }
            }
            QueueDel(hit);
        }
    }
}
