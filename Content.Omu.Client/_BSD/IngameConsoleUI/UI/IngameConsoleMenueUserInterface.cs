
using Content.Shared.Research.Components;

using Robust.Client.UserInterface;

using Content.Omu.Client._BSD.IngameConsoleUI.UI;

using Content.Omu.Shared._BSD.IngameConsoleSystem;

namespace Content.Omu.Client._BSD.IngameConsoleUI.UI
{
    public sealed class IngameConsoleBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private IngameConsoleMenue? _menu;

        public IngameConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {

        }

        protected override void Open()
        {
            base.Open();
            _menu = this.CreateWindow<IngameConsoleMenue>();
            _menu.CommandAttempt += AttemptedCommand;
            IngameConsoleBoundUserInterfaceState state = new(["Start"]);
            _menu.PopulateHistory(state.OutputHistory);
            return;
        }

        public void AttemptedCommand(string text)
        {
            SendMessage(new IngameConsoleCommandAttemptMessage(text));
        }


        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            if (state is not IngameConsoleBoundUserInterfaceState rState) return;
            _menu?.PopulateHistory(rState.OutputHistory);
            return;
        }
    }
}