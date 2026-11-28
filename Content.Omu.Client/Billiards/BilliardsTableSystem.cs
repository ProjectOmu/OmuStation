// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Omu.Client.Billiards.UI;
using Content.Omu.Shared.Billiards;
using Content.Shared.Popups;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Omu.Client.Billiards;

public sealed class BilliardsTableSystem : SharedBilliardsTableSystem
{
    public const float Apron = 0.175f;

    private const float Rail = 0.2f;
    private const float TableLength = BilliardsSimulation.Length + Rail * 2f;
    private const float ContactSoundInterval = 0.08f;
    private const float SpriteMargin = 1.35f;

    public static readonly Vector2 SpriteSize = new(TableLength, TableLength + Apron);

    private static readonly ProtoId<ShaderPrototype> Shader = "BilliardsBalls";
    private static readonly Vector2 Hidden = new(-10f, -10f);
    private static readonly Vector2 Felt = new(BilliardsSimulation.Length, BilliardsSimulation.Width);
    private static readonly Vector2 SpriteExtent = Felt + new Vector2(SpriteMargin * 2f);
    private static readonly Vector2 BallLayerScale = SpriteExtent / SpriteSize;
    private static readonly AudioParams ContactAudio = AudioParams.Default.WithVolume(-6f).WithVariation(0.15f);

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Dictionary<EntityUid, Display> _displays = [];
    private readonly HashSet<EntityUid> _rolling = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BilliardsTableComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BilliardsTableComponent, AfterAutoHandleStateEvent>(OnState);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        using var query = EntityQueryEnumerator<BilliardsTableComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var table, out var sprite))
        {
            if (!_displays.TryGetValue(uid, out var display))
            {
                if (!_sprite.LayerMapTryGet((uid, sprite), BilliardsTableLayers.Balls, out var layer, false))
                    continue;

                display = new Display(_prototype.Index(Shader).InstanceUnique(), table.Shots);
                display.Shader.SetParameter("extent", SpriteExtent);
                display.Shader.SetParameter("flipped", 0f);
                display.Shader.SetParameter("stage", 0f);
                display.Shader.SetParameter("background", 0f);
                display.Shader.SetParameter("guideNormal", Vector2.Zero);
                _displays.Add(uid, display);
                sprite.LayerSetShader(layer, display.Shader);
                _sprite.LayerSetScale((uid, sprite), layer, BallLayerScale);
            }

            Animate((uid, table), display, frameTime);

            var facing = (_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).GetCardinalDir();
            var feltShift = (-facing.ToAngle()).RotateVec(new Vector2(0f, Apron / 2f));
            display.Shader.SetParameter("origin", new Vector2(-SpriteMargin) - feltShift);

            var pull = IsAiming((uid, table)) ? table.AimPower : -1f;
            ApplyShader(display, display.Shader, display.Balls, table.AimDirection, pull, 0f);
        }
    }

    public bool TryGetBalls(EntityUid uid, [NotNullWhen(true)] out Vector2[]? balls)
    {
        balls = _displays.GetValueOrDefault(uid)?.Balls;
        return balls != null;
    }

    public bool TryApplyShader(EntityUid uid,
        ShaderInstance shader,
        Vector2[] balls,
        Vector2 direction,
        float pull,
        float guide)
    {
        if (!_displays.TryGetValue(uid, out var display))
            return false;

        ApplyShader(display, shader, balls, direction, pull, guide);
        return true;
    }

    protected override void UpdateUi(Entity<BilliardsTableComponent> ent)
    {
        if (UserInterface.TryGetOpenUi(ent.Owner, BilliardsUiKey.Key, out var bui))
            bui.Update();
    }

    private static void ApplyShader(Display display,
        ShaderInstance shader,
        Vector2[] balls,
        Vector2 direction,
        float pull,
        float guide)
    {
        shader.SetParameter("balls", balls);
        shader.SetParameter("spinAxis", display.SpinAxis);
        shader.SetParameter("spinTurn", display.SpinTurn);
        shader.SetParameter("cueDirection", direction);
        shader.SetParameter("cuePull", pull);
        shader.SetParameter("guideLength", guide);
    }

    private static void Roll(Display display, int ball, Vector2 travel)
    {
        var distance = travel.Length();
        if (distance <= 0f)
            return;

        var axis = new Vector3(-travel.Y, travel.X, 0f) / distance;
        var turn = Quaternion.CreateFromAxisAngle(axis, distance / BilliardsSimulation.BallRadius);
        var spin = Quaternion.Normalize(turn * display.Spins[ball]);

        display.Spins[ball] = spin;
        display.SpinAxis[ball] = new Vector2(spin.X, spin.Y);
        display.SpinTurn[ball] = new Vector2(spin.Z, spin.W);
    }

    private void OnShutdown(Entity<BilliardsTableComponent> ent, ref ComponentShutdown args)
    {
        _rolling.Remove(ent);
        if (_displays.Remove(ent, out var display))
            display.Shader.Dispose();
    }

    private void OnState(Entity<BilliardsTableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var settled = false;
        if (ent.Comp.ShotEnd != null)
            _rolling.Add(ent);
        else
            settled = _rolling.Remove(ent);

        UpdateUi(ent);

        if (settled)
            AnnounceTurn(ent);
    }

    private void AnnounceTurn(Entity<BilliardsTableComponent> ent)
    {
        var table = ent.Comp;
        UserInterface.TryGetOpenUi<BilliardsBoundUserInterface>(ent.Owner, BilliardsUiKey.Key, out var bui);

        if (table.Mode == BilliardsMode.Freeplay)
        {
            if (table.BallInHand)
                bui?.ShowTurnCallout(true);

            return;
        }

        if (_player.LocalEntity is not { } user
            || table.Finished
            || table.Turn >= table.Players.Count
            || table.Players[table.Turn].Entity != GetNetEntity(user)
            || (table.Players.Count == 1 && !table.BallInHand))
        {
            return;
        }

        if (bui != null)
            bui.ShowTurnCallout(table.BallInHand);
        else
            Popup.PopupEntity(Loc.GetString("billiards-popup-your-turn"), user, user, PopupType.Large);
    }

    private void Animate(Entity<BilliardsTableComponent> ent, Display display, float frameTime)
    {
        var table = ent.Comp;
        if (table.Balls.Count != BilliardsSimulation.BallCount)
        {
            Array.Fill(display.Balls, Hidden);
            return;
        }

        if (table.ShotEnd == null)
        {
            display.Simulation = null;
            display.Shots = table.Shots;

            for (var ball = 0; ball < BilliardsSimulation.BallCount; ball++)
            {
                display.Balls[ball] = table.Balls[ball].Pocketed ? Hidden : table.Balls[ball].Position;
            }

            return;
        }

        if (display.Simulation is not { } simulation || display.Shots != table.Shots)
        {
            var seenFromStart = display.Shots != table.Shots;
            simulation = new BilliardsSimulation(table.Balls, table.ShotVelocity);
            display.Simulation = simulation;
            display.Shots = table.Shots;
            display.Contacts = 0;
            display.NextContactSound = 0f;
            display.Elapsed = seenFromStart || table.ShotStart is not { } start
                ? 0f
                : (float) (Timing.CurTime - start).TotalSeconds;
        }

        display.Elapsed += frameTime;
        simulation.AdvanceTo(display.Elapsed);

        for (var ball = 0; ball < BilliardsSimulation.BallCount; ball++)
        {
            if (simulation.Pocketed[ball])
            {
                display.Balls[ball] = Hidden;
                continue;
            }

            if (display.Balls[ball] != Hidden)
                Roll(display, ball, simulation.Positions[ball] - display.Balls[ball]);

            display.Balls[ball] = simulation.Positions[ball];
        }

        if (simulation.Contacts == display.Contacts)
            return;

        display.Contacts = simulation.Contacts;
        if (display.Elapsed < display.NextContactSound)
            return;

        display.NextContactSound = display.Elapsed + ContactSoundInterval;
        _audio.PlayEntity(table.ContactSound, Filter.Local(), ent, false, ContactAudio);
    }

    private sealed class Display
    {
        public readonly ShaderInstance Shader;
        public readonly Vector2[] Balls = new Vector2[BilliardsSimulation.BallCount];
        public readonly Quaternion[] Spins = new Quaternion[BilliardsSimulation.BallCount];
        public readonly Vector2[] SpinAxis = new Vector2[BilliardsSimulation.BallCount];
        public readonly Vector2[] SpinTurn = new Vector2[BilliardsSimulation.BallCount];
        public BilliardsSimulation? Simulation;
        public int Shots;
        public int Contacts;
        public float Elapsed;
        public float NextContactSound;

        public Display(ShaderInstance shader, int shots)
        {
            Shader = shader;
            Shots = shots;
            Array.Fill(Spins, Quaternion.Identity);
            Array.Fill(SpinTurn, Vector2.UnitY);
        }
    }
}
