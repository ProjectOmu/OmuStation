namespace Content.Shared._Omu.Weapons.Ranged;

[DataDefinition, Serializable]
public sealed partial class OmuBattleExecutedEvent : EntityEventArgs
{
    public EntityUid Target;

    public OmuBattleExecutedEvent(EntityUid target)
    {
        Target = target;
    }
}
