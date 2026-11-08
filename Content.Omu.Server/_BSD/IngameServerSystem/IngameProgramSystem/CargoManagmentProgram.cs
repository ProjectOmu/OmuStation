using Content.Omu.Server._BSD.IngameConsoleSystem.IngameProgramSystem.Components;

using Content.Omu.Shared._BSD.IngameConsoleSystem;

namespace Content.Omu.Server._BSD.IngameConsoleSystem.IngameProgramSystem;

public sealed class BSDIngamCarogManagmentProgramSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IngameCargoManagmentProgramComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommandCargoManagmentProgram);
    }

    public void IngameConsoleCommandCargoManagmentProgram(Entity<IngameCargoManagmentProgramComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.ICC_PRINT_ALL && args.Args!.Length > 1 && args.Args[1] == "materials")
        {
            PrintAllMaterial(ent);
            //Now add stuff to history to update that it worked;
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args!.Length > 2 && args.Args[1] == "material")
        {
            PrintMaterialsAcrossDepartments(ent, args.Args[2]);
            //Now add stuff to history to update that it worked;
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args!.Length > 2 && args.Args[1] == "department")
        {
            PrintDepartmentsMaterials(ent, args.Args[2]);
            //Now add stuff to history to update that it worked;
        }
    }

    private void PrintAllMaterial(Entity<IngameCargoManagmentProgramComponent> ent)
    {
        //make a table rows -> department, collum -> material
        return;
    }
    private void PrintMaterialsAcrossDepartments(Entity<IngameCargoManagmentProgramComponent> ent, string materialType)
    {
        //display material across all departments:
        // dep : amount
        return;
    }
    private void PrintDepartmentsMaterials(Entity<IngameCargoManagmentProgramComponent> ent, string departmentType)
    {
        //display materials in a department
        // mat: amount
        return;
    }
}