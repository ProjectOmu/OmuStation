using Content.Shared._Byrd.Components;
using Content.Shared.HealthExaminable;

namespace Content.Shared._Byrd
{
    public sealed class BloodSuckerSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BloodSuckedComponent, HealthBeingExaminedEvent>(OnHealthExamined);
        }

        private void OnHealthExamined(EntityUid uid, BloodSuckedComponent component, HealthBeingExaminedEvent args)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("bloodsucked-health-examine", ("target", uid)));
        }
    }
}