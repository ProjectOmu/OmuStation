/*
Summery:

This file is used to declare the types of commands we have in the IngameConsole system.
To add a new one is rather simple, define the key add a enum and link them then declare the number of arguments it takes and you are done.

*/
using Content.Shared._EinsteinEngines.Language.Components;

namespace Content.Omu.Shared._BSD.IngameConsoleSystem;

public enum IngameConsoleCommandType
{//ICC == Ingame Concole Command
    ICC_EXIT,//recomended unified close the ui via console input
    ICC_START,//recomended unified start command
    ICC_STOP,//recommended unified stop command
    ICC_PRINT,//recomended unified print command aka print the information the user needs, takes one argument to determin what info should be printed
    ICC_PRINT_ALL,//recomended unified print all command, prints all available information
    ICC_HELP,//recomended unified help command prints all commands available to the console
    ICC_ASSIGN,//General use assign command
    ICC_CLS_EXCLUSIVE,//General use RESERVED command clears history
    ICC_PROXY,//Used to remote connect into things
    ICC_SET,//Used to set variables
    //ISCL -> Ingame Server Client Link
    ISCL_PROXY_TERMINATE,//Used to disconnect from remote connection
    ISCL_UNASSIGN,//removes a server client link TOdo possibly mvoe this to be a ICC command instead
    ISCL_REGISTER_GRID,//registers the grid
    SSA_FTL,//SignalSAlvage engaged FTL to destination(predefined by default)
}
public readonly struct IngameConsoleCommand
{
    public IngameConsoleCommand(string key, IngameConsoleCommandType type, int argumentsNumber, bool universalCommand = false)
    {
        Key = key;
        Type = type;
        ArgumentsNumberMin = argumentsNumber;
        UniversalCommand = universalCommand;
    }
    public string Key { get; init; }
    public IngameConsoleCommandType Type { get; init; }
    public int ArgumentsNumberMin { get; init; }//Number of Arguments
    public bool UniversalCommand { get; init; }
};
public readonly struct IngameConsoleCommandList
{
    public List<IngameConsoleCommand> List { get; init; }
    public IngameConsoleCommandList()
    {
        List = new();
        List.Add(new IngameConsoleCommand("exit", IngameConsoleCommandType.ICC_EXIT, 0));
        List.Add(new IngameConsoleCommand("print", IngameConsoleCommandType.ICC_PRINT, 1));
        List.Add(new IngameConsoleCommand("print_all", IngameConsoleCommandType.ICC_PRINT_ALL, 0));
        List.Add(new IngameConsoleCommand("help", IngameConsoleCommandType.ICC_HELP, 0));
        List.Add(new IngameConsoleCommand("assign", IngameConsoleCommandType.ICC_ASSIGN, 1));
        List.Add(new IngameConsoleCommand("ftl", IngameConsoleCommandType.SSA_FTL, 0));
        List.Add(new IngameConsoleCommand("start", IngameConsoleCommandType.ICC_START, 0));
        List.Add(new IngameConsoleCommand("stop", IngameConsoleCommandType.ICC_STOP, 0));
        List.Add(new IngameConsoleCommand("cls", IngameConsoleCommandType.ICC_CLS_EXCLUSIVE, 0, true));
        List.Add(new IngameConsoleCommand("unassign", IngameConsoleCommandType.ISCL_UNASSIGN, 1));
        List.Add(new IngameConsoleCommand("register_grid", IngameConsoleCommandType.ISCL_REGISTER_GRID, 0));
        List.Add(new IngameConsoleCommand("proxy", IngameConsoleCommandType.ICC_PROXY, 1));
        List.Add(new IngameConsoleCommand("proxy_terminate", IngameConsoleCommandType.ISCL_PROXY_TERMINATE, 1));
        List.Add(new IngameConsoleCommand("set", IngameConsoleCommandType.ICC_SET, 2));
    }


}