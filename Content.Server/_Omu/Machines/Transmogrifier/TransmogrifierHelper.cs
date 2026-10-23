using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Machines.Transmogrifier;

[Serializable]
public enum TransmogrifierMode : byte
{
    Coarse = 0,

    Rough = 1,

    OneToOne = 2,

    Fine = 3,

    VeryFine = 4
}
