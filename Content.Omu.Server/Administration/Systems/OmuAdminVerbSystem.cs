using Content.Shared.Verbs;

namespace Content.Omu.Server.Administration.Systems;

public sealed partial class OmuAdminVerbSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetVerbs);
    }

    private void GetVerbs(GetVerbsEvent<Verb> args)
    {
        AddSmiteVerbs(args);
    }
}
