using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Omu.Shared.Traits;
using System.Numerics;

namespace Content.Omu.Client.Eye
{
    public sealed class PhotophobiaOverlay : Overlay
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        private const float Max_Strength_Multiplier = 1.25f; // So that an admin or particularly sneaky yamlmaxxer couldn't just, up the effect tenfold and flashbang somebody. The effect is decently strong as it is.
        private const float Min_Strength_Multiplier = 0f;

        public override bool RequestScreenTexture => true;
        public override OverlaySpace Space => OverlaySpace.WorldSpace;
        private readonly ShaderInstance _photophobiaShader;

        public PhotophobiaOverlay()
        {
            IoCManager.InjectDependencies(this);
            _photophobiaShader = _prototypeManager.Index<ShaderPrototype>("PhotophobiaShader").InstanceUnique();
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (ScreenTexture == null)
                return;

            var playerEntity = _playerManager.LocalSession?.AttachedEntity;

            if (playerEntity == null)
                return;

            // make sure the player actually has photophobia.
            if (!_entityManager.TryGetComponent<PhotophobiaComponent>(playerEntity, out var blurComp))
                return;

            var worldHandle = args.WorldHandle;
            var viewport = args.WorldBounds;

            _photophobiaShader.SetParameter("SCREEN_TEXTURE", args.Viewport.RenderTarget.Texture);
            _photophobiaShader.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
            _photophobiaShader.SetParameter("effectStrength", Math.Clamp(blurComp.ShaderStrengthMultiplier, Min_Strength_Multiplier, Max_Strength_Multiplier));

            worldHandle.SetTransform(Matrix3x2.Identity);
            worldHandle.UseShader(_photophobiaShader);
            worldHandle.DrawRect(viewport, Color.White);
            worldHandle.UseShader(null);

        }
    }
}
