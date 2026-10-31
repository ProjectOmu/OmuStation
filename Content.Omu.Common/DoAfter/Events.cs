
namespace Content.Omu.Common.DoAfter;
/// <summary>
/// Event raised on:
/// The item used
/// The person
/// The target of the event
/// 
/// This exists to modify the entire doafter more easily,
/// </summary>
/// <param name="user"></param>
/// <param name="item"></param>
/// <param name="target"></param>
/// <param name="multiplier"></param>
public sealed class ModifyDelayOfDoAfter(EntityUid user, EntityUid? item, EntityUid? target, float multiplier = 1f, bool cancel = false) : EntityEventArgs
{
    public float Multiplier = multiplier;

    public EntityUid User = user;
    public EntityUid? Item = item;
    public EntityUid? Target = target;
    public bool Cancle = cancel;

}