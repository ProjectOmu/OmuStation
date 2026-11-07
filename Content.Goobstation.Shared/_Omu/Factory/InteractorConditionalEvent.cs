using JetBrains.Annotations;

namespace Content.Goobstation.Shared._Omu.Factory;


public partial class InteractorConditionalEvent : HandledEntityEventArgs
{
    /// <summary>
    ///     The entity performing the interact.
    /// </summary>
    public EntityUid Interactor { get; set; }

    /// <summary>
    ///     Entity being interacted with.
    /// </summary>
    public EntityUid Interactee { get; set;}
}
