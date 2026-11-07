using System.Linq;

using Robust.Server.GameObjects;

using Content.Shared.Examine;

using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;

using Content.Omu.Shared._BSD.IngameConsoleSystem;
using Content.Omu.Server._BSD.IngameConsoleSystem;
using Content.Shared._Omu.Components;

namespace Content.Omu.Server._BSD.IngameServerClientLinkSystem;

public sealed partial class BSDIngameServerClientLinkSystem : EntitySystem
{
    [Dependency] private readonly BSDIngameConsoleSystem _consoleSys = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IngameServerClientLinkInfrastructureComponent, ComponentStartup>(OnCompInit);
        SubscribeLocalEvent<IngameServerClientLinkInfrastructureComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<IngameServerClientLinkInfrastructureComponent, ExaminedEvent>(OnExamin);

        SubscribeLocalEvent<IngameServerClientLinkInfrastructureComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommand);
    }
    public void OnCompInit(Entity<IngameServerClientLinkInfrastructureComponent> ent, ref ComponentStartup args)
    {
        var unusedId = EntityQuery<IngameServerClientLinkInfrastructureComponent>(true)
            .Max(s => s.NetworkId) + 1;
        ent.Comp.NetworkId = unusedId;
        if (ent.Comp.AutoLink != null)
        {
            var machineQuerry = AllEntityQuery<IngameServerClientLinkInfrastructureComponent>();
            foreach (var iterator in ent.Comp.AutoLink)
            {
                while (machineQuerry.MoveNext(out var iterator2, out var infraCompIterator2))
                {
                    if (ent.Owner == iterator2) continue;
                    if (TryEstablishLink(ent, iterator2, iterator)) break;
                    //TODO: improve this as currently scales with n -> if we have thousends of these it will be a issue
                }
            }
        }
        Dirty(ent, ent.Comp);
    }
    public void OnComponentRemove(Entity<IngameServerClientLinkInfrastructureComponent> ent, ref ComponentRemove args)
    {
        //ensure we remove our entity from every list that could mention it if we remove the entity
        foreach (var iterator in ent.Comp.EntityDicClient.Keys)
        {
            foreach (var iterator2 in ent.Comp.EntityDicClient[iterator])
            {
                TerminateLink(ent, iterator2, iterator);
            }
        }
        foreach (var iterator in ent.Comp.EntityDicServer.Keys)
        {
            foreach (var iterator2 in ent.Comp.EntityDicServer[iterator])
            {
                TerminateLink(ent, iterator2, iterator);
            }
        }
    }
    #region UI
    public void OnExamin(Entity<IngameServerClientLinkInfrastructureComponent> ent, ref ExaminedEvent args)
    {
        string details;
        details = Loc.GetString("ISCL-netID-examin", ("ID", ent.Comp.NetworkId));
        args.PushMarkup(details, -1);
    }
    public void IngameConsoleCommand(Entity<IngameServerClientLinkInfrastructureComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.ICC_ASSIGN && args.Args!.Length > 3)
        {
            IngameConsoleHistoryChangeEvent ev = new(Loc.GetString("ISCL_Attempt_Link_Start", ("NID", args.Args[1]), ("Channel", args.Args[2]), ("ServerConnection", args.Args[3])));
            RaiseLocalEvent(ent, ref ev);
            if (Int32.TryParse(args.Args[1], out int nID1) == false)
            {
                IngameConsoleHistoryChangeEvent ev2 = new(Loc.GetString("ICC_Invalid_Number_Not_A_Number"));
                RaiseLocalEvent(ent, ref ev2);
                return;
            }
            if (!TryGetEntityUidFromNetID(nID1, out var targetUid))
            {
                IngameConsoleHistoryChangeEvent ev2 = new(Loc.GetString("ISCL_Entity_Not_Found"));
                RaiseLocalEvent(ent, ref ev2);
                return;
            }
            var actAsServer = false;
            if (args.Args.Length > 3 && _consoleSys.InputBoolCheck(args.Args[3])) actAsServer = true;
            if (TryEstablishLink(ent, (EntityUid) targetUid!, args.Args[2], actAsServer))
            {
                IngameConsoleHistoryChangeEvent ev2 = new(Loc.GetString("ISCL_Link_Success"));
                RaiseLocalEvent(ent, ref ev2);
                return;
            }
            IngameConsoleHistoryChangeEvent ev3 = new(Loc.GetString("ISCL_Link_Fail"));
            RaiseLocalEvent(ent, ref ev3);
            return;
        }
        else if (args.Type == IngameConsoleCommandType.ISCL_UNASSIGN && args.Args!.Length > 2)
        {
            IngameConsoleHistoryChangeEvent ev = new(Loc.GetString("ISCL_Attempt_Disconnet_Start", ("NID", args.Args[1])));
            RaiseLocalEvent(ent, ref ev);
            if (Int32.TryParse(args.Args[1], out int nID1) == false)
            {
                IngameConsoleHistoryChangeEvent ev2 = new(Loc.GetString("ICC_Invalid_Number_Not_A_Number"));
                RaiseLocalEvent(ent, ref ev2);
                return;
            }
            if (!TryGetEntityUidFromNetID(nID1, out var targetUid))
            {
                IngameConsoleHistoryChangeEvent ev2 = new(Loc.GetString("ISCL_Entity_Not_Found"));
                RaiseLocalEvent(ent, ref ev2);
                return;
            }
            TerminateLink(ent, (EntityUid) targetUid!, args.Args[2]);
            IngameConsoleHistoryChangeEvent ev1 = new(Loc.GetString("ISCL_Link_Terminated"));
            RaiseLocalEvent(ent, ref ev1);
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PRINT_ALL)
        {
            IngameConsoleHistoryChangeEvent ev = new(Loc.GetString("ISCL_Print_All_Start"));
            RaiseLocalEvent(ent, ref ev);
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args!.Length > 2 && args.Args[1] == "connections")
        {
            IngameConsoleHistoryChangeEvent ev = new(Loc.GetString("ISCL_Print_Category_Start", ("Category", args.Args[2])));
            RaiseLocalEvent(ent, ref ev);
        }
        else if (args.Type == IngameConsoleCommandType.ISCL_REGISTER_GRID)
        {
            if (Transform(ent).GridUid == null)
            {
                IngameConsoleHistoryChangeEvent ev = new(Loc.GetString("ISCL_No_Grid_Found"));
                RaiseLocalEvent(ent, ref ev);
            }
            else
            {
                TryAddSCICompToGridStandardGridSetup((EntityUid) Transform(ent).GridUid!);
                IngameConsoleHistoryChangeEvent ev = new(Loc.GetString("ISCL_Print_Grid_Registered"));
                RaiseLocalEvent(ent, ref ev);
            }
        }
        return;
    }
    #endregion
}