
using Content.Omu.Shared._BSD.IngameConsoleSystem;

using Content.Omu.Server._BSD.SignalSalv.Components;
using Content.Omu.Server._BSD.SignalSalv.Events;

using System.Linq;



using Content.Shared.Materials;

namespace Content.Omu.Server._BSD.SignalSalv;

public sealed partial class BSDSignalSalvSystem : EntitySystem
{
    #region  User Interfacing
    public void IngameConsoleCommandMatReciver(Entity<SignalSalvMaterialReciverStructureComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {

        // assign reciver | sets this machine to be the material reciver
        if (args.Type == IngameConsoleCommandType.ICC_ASSIGN && args.Args!.Length > 1 && args.Args[1] == "reciver")
        {
            _materialTransitSystem.ChangeMaterialReciverOnTransitComp(ent);
            IngameConsoleHistoryChangeEvent ev = new("-> Material destination changed");
            RaiseLocalEvent(ent, ref ev);
            //Now add stuff to history to update that it worked;
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args!.Length > 1 && args.Args[1] == "materials")
        {
            IngameConsoleHistoryChangeEvent ev = new(PrintMaterialInbound(ent));
            RaiseLocalEvent(ent, ref ev);
        }
    }
    #endregion
}