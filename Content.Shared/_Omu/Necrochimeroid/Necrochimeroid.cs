using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;

namespace Content.Shared._Omu.Entities.Necrochimeroid;

[RegisterComponent]
public sealed partial class NecroChimeroidComponent : Component
{
    /// <summary>
    /// The action used by an NC to enter an entity
    /// </summary>
    [DataField]
    public string EnterAction = "ActionNecroEnter";

    /// <summary>
    /// The action used by an NC to leave an entity
    /// </summary>
    [DataField]
    public string LeaveAction = "ActionNecroLeave";

    [DataField("InEntity")]
    public bool InEntity { get; set; } = false;

    /// <summary>
    /// The actual action used by an NC to leave an entity
    /// </summary>
    public Entity<ActionComponent?>? ActualLeaveAction;

    /// <summary>
    /// The action used by an NC to leave an entity
    /// </summary>
    public Entity<ActionComponent?>? ActualEnterAction;

    /// <summary>
    /// Whether an NC is inside of an entity - do not fuck with this unless you absolutely know what you are doing
    /// </summary>
    [DataField]
    public bool Burrowed;

    /// <summary>
    /// The time take for an NC to enter an entity
    /// </summary>
    [DataField]
    public TimeSpan EnterDuration = TimeSpan.FromSeconds(5);
}

public sealed class NecroChimeroidSystem : EntitySystem
{

    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NecroChimeroidComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NecroChimeroidComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<NecroChimeroidComponent, MindRemovedMessage>(OnMindRemove);

        SubscribeLocalEvent<NecroChimeroidComponent, NecroEnterEvent>(OnEnterAttempt);
        SubscribeLocalEvent<NecroChimeroidComponent, NecroEnterDoafter>(OnEnterDoAfter);
    }
    private void OnStartup(EntityUid uid, NecroChimeroidComponent component, ref ComponentStartup args)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) || mindContainer.Mind is not { } mind)
            return;

        component.ActualEnterAction = _actionsSystem.AddAction(mind, component.EnterAction);
        component.ActualLeaveAction = _actionsSystem.AddAction(mind, component.LeaveAction);
    }

    private void OnMindAdded(EntityUid uid, NecroChimeroidComponent component, ref MindAddedMessage args)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) || mindContainer.Mind is not { } mind)
            return;

        component.ActualEnterAction = _actionsSystem.AddAction(mind, component.EnterAction);
        component.ActualLeaveAction = _actionsSystem.AddAction(mind, component.LeaveAction);
    }

    private void OnMindRemove(EntityUid uid, NecroChimeroidComponent component, ref MindRemovedMessage args)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) || mindContainer.Mind is not { } mind)
            return;

        _actionsSystem.RemoveAction(component.ActualEnterAction);
        _actionsSystem.RemoveAction(component.ActualLeaveAction);
    }
    #region actions
    private void OnEnterAttempt(EntityUid uid, NecroChimeroidComponent component, ref NecroEnterEvent args)
    {
        var target = args.Target;

        if (component.Burrowed)
        {
            _popup.PopupEntity(Loc.GetString("necrochimeroid-enter-fail-burrow"), uid, uid);
            args.Handled = true;
            return;
        }

        if (!TryComp<MindContainerComponent>(target, out var mindContainer) || mindContainer.HasMind)
        {
            _popup.PopupEntity(Loc.GetString("necrochimeroid-enter-fail-mind"), uid, uid);
            args.Handled = true;
            return;
        }


        if (TryComp<BodyComponent>(target, out var bodyComp))
        {
            if (_body.TryGetBodyOrganEntityComps<BrainComponent>((target, bodyComp), out var brains))
            {
                _popup.PopupEntity(Loc.GetString("necrochimeroid-enter-fail-brain"), uid, uid);
                args.Handled = true;
                return;
            }

            foreach (var container in _body.GetBodyContainers(target, bodyComp))
            {
                if (_body.CanInsertOrgan(uid, container.ID))
                {
                    var doAfterArgs = new DoAfterArgs(
                    EntityManager,
                    uid,
                    component.EnterDuration,
                    new NecroEnterDoafter()
                    {
                        Container = container.ID,
                    },
                    uid,
                    args.Target)
                    {
                        BreakOnDamage = true,
                        BreakOnMove = true,
                        NeedHand = false,
                    };

                    if (_doAfterSystem.TryStartDoAfter(doAfterArgs))
                    {
                        args.Handled = true;
                        return;
                    }
                }
            }
        }
    }
    private void OnEnterDoAfter(EntityUid uid, NecroChimeroidComponent component, NecroEnterDoafter args)
    {
        _body.InsertOrgan(uid, uid, args.Container);
    }
}
    #endregion

