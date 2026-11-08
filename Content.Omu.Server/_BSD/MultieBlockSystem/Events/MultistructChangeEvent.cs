using Content.Omu.Server._BSD.MultiBlockSystem.Components;

namespace Content.Omu.Server._BSD.MultiBlockSystem.Events;

//this event does nothing on the client side!!!
//event used to communicate the structure change and get the subsystems informed that they are in fact to update there numbers!!
[ByRefEvent]
public readonly record struct MultiStructChangeEvent(Dictionary<string, List<Node>> LostEntityDict);