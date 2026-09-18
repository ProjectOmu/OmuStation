namespace Content.Server.Omu.Werewolf;

[RegisterComponent, Access(typeof(WerewolfDevouredSystem))]
public sealed partial class WerewolfDevouredComponent : Component
{}

public sealed class WerewolfDevouredSystem : EntitySystem
{}
