namespace Content.Omu.Server._BSD.IngameServerSystem.Helpers;

public enum IngameServerProgramTypes : int//reminder add the IDs to the guidbook or help command!!!!
{
    ResearchProgram,            //converts raw data to research points
    AI,                         //WIP
    CrewMonitor,                //WIP
    Cameras,                    //WIP
    Telecommunication,          //WIP
    CargoManagment,             //for easier managment of resources by cargo
    Lath,                       //handles local printing of things, no proper functionality YET
}

public struct IngameServerProgram
{
    public IngameServerProgram(IngameServerProgramTypes type, float baseProcessingCost, int priority)
    {
        Type = type;
        BaseProcessingCost = baseProcessingCost;
        CurrentProcessingCost = BaseProcessingCost;
        Efficency = 1f;
        Priority = priority;
        AssignedProcessingCost = 0;
    }
    public IngameServerProgramTypes Type { get; init; }
    public float BaseProcessingCost { get; init; }
    public float CurrentProcessingCost = 0f;
    public float AssignedProcessingCost = 0f;
    public float Efficency = 1f;
    public int Priority { get; init; }      //lower is better 3 levels: 1 = critical(AI ONLY!!!), 2 = High, 2 = Normal
}

public struct IngameProgramList
{
    public HashSet<IngameServerProgram> Content { get; init; }
    public IngameProgramList()
    {
        Content = new();
        Content.Add(new IngameServerProgram(IngameServerProgramTypes.ResearchProgram, 1f, 2));
        Content.Add(new IngameServerProgram(IngameServerProgramTypes.AI, 0f, 1));                    //WIP
        Content.Add(new IngameServerProgram(IngameServerProgramTypes.CrewMonitor, 0f, 1));           //WIP
        Content.Add(new IngameServerProgram(IngameServerProgramTypes.Cameras, 0f, 1));               //WIP
        Content.Add(new IngameServerProgram(IngameServerProgramTypes.Telecommunication, 0f, 1));     //WIP
    }
}