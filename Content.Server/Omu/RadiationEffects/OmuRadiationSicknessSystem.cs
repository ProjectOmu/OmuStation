
using System.Reflection;
using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Popups;
using Content.Shared.Radiation.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Omu.RadiationEffects;

public sealed class OmuRadiationSicknessSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextMinorMessage = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextMajorEffect = new();

    private static readonly string[] MinorMessages =
    {
        "You taste metal in your mouth.",
        "Your stomach twists.",
        "You feel nauseous...",
        "Your skin prickles.",
        "You feel a strange warmth under your skin.",
        "Your head pounds.",
        "Your vision swims for a moment.",
        "Your hands tremble.",
        "Your throat feels dry.",
        "You feel suddenly weak.",
        "You feel your skin burning.",
        "Your heart races, and then slows.",
        "A wave of sickness rolls through you.",
        "Your mouth fills with a bitter taste.",
        "You feel like something is very wrong.",
        "A cold sweat breaks over your skin.",
        "Your bones ache.",
        "Your guts churn.",
        "You feel lightheaded.",
        "Your vision flickers with static."
    };

    private static readonly string[] MajorMessages =
    {
        "Your stomach lurches violently!",
        "You feel like you are going to throw up!",
        "The world spins around you!",
        "Your knees almost give out!",
        "A hot, sick feeling crawls through your body!",
        "Your body rejects something inside you!",
        "Your vision blurs as radiation sickness takes hold!",
        "You stagger as nausea washes over you!",
        "Your body feels poisoned!"
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OnIrradiatedEvent>(OnIrradiated);
    }

    private void OnIrradiated(OnIrradiatedEvent args)
    {
        if (!_cfg.GetCVar(CCVars.OmuRadiationSicknessEnabled))
            return;

        if (!TryGetTarget(args, out var target))
            return;

        if (!Exists(target))
            return;

        var severity = GetSeverity(args);
        if (severity < 0.01f)
            severity = 0.25f;

        var now = _timing.CurTime;

        if (!_nextMinorMessage.TryGetValue(target, out var nextMsg) || now >= nextMsg)
        {
            _nextMinorMessage[target] = now + TimeSpan.FromSeconds(_random.NextFloat(4f, 8f));
            Popup(target, _random.Pick(MinorMessages));
        }

        var chance = Math.Clamp(0.08f + severity * 0.18f, 0.08f, 0.55f);

        if (_nextMajorEffect.TryGetValue(target, out var nextMajor) && now < nextMajor)
            return;

        if (!_random.Prob(chance))
            return;

        _nextMajorEffect[target] = now + TimeSpan.FromSeconds(_random.NextFloat(8f, 16f));

        Popup(target, _random.Pick(MajorMessages));
        TrySpawnVomit(target);
        TryApplyDrunkDizzyStutter(target);
    }

    private void Popup(EntityUid target, string message)
    {
        try { _popup.PopupEntity(message, target, target); }
        catch { }
    }

    private void TrySpawnVomit(EntityUid target)
    {
        foreach (var proto in new[] { "PuddleVomit", "VomitPuddle", "Vomit", "PuddleWater" })
        {
            try
            {
                if (!_proto.HasIndex<EntityPrototype>(proto))
                    continue;

                Spawn(proto, Transform(target).Coordinates);
                return;
            }
            catch { }
        }
    }

    private void TryApplyDrunkDizzyStutter(EntityUid target)
    {
        TryApplyStatusEffectByReflection(target, "Drunk", TimeSpan.FromSeconds(20));
        TryApplyStatusEffectByReflection(target, "Dizzy", TimeSpan.FromSeconds(16));
        TryApplyStatusEffectByReflection(target, "Stutter", TimeSpan.FromSeconds(12));

        foreach (var componentName in new[]
        {
            "DrunkComponent",
            "DizzyComponent",
            "DizzinessComponent",
            "StutteringComponent",
            "StutterComponent"
        })
        {
            var type = FindType(componentName);
            if (type != null)
                TryAddComponentByType(target, type);
        }
    }

    private void TryApplyStatusEffectByReflection(EntityUid uid, string effectId, TimeSpan duration)
    {
        try
        {
            var statusSystemType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.Name.Contains("StatusEffectsSystem"));

            if (statusSystemType == null)
                return;

            var iocType = FindType("IoCManager");
            var resolve = iocType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);

            var systemManagerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.Name.Contains("IEntitySystemManager"));

            if (resolve == null || systemManagerType == null)
                return;

            var mgr = resolve.MakeGenericMethod(systemManagerType).Invoke(null, Array.Empty<object>());
            var getSys = mgr?.GetType().GetMethods()
                .FirstOrDefault(m => m.Name.Contains("GetEntitySystem") && m.GetParameters().Length == 1);

            var statusSystem = getSys?.Invoke(mgr, new object[] { statusSystemType });
            if (statusSystem == null)
                return;

            foreach (var method in statusSystemType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!method.Name.Contains("StatusEffect") && !method.Name.Contains("Effect"))
                    continue;

                var parameters = method.GetParameters();
                var values = new object?[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i].ParameterType;

                    if (p == typeof(EntityUid))
                        values[i] = uid;
                    else if (p == typeof(string))
                        values[i] = effectId;
                    else if (p == typeof(TimeSpan))
                        values[i] = duration;
                    else if (p == typeof(bool))
                        values[i] = true;
                    else if (p.IsValueType)
                        values[i] = Activator.CreateInstance(p);
                    else
                        values[i] = null;
                }

                try
                {
                    method.Invoke(statusSystem, values);
                    return;
                }
                catch { }
            }
        }
        catch { }
    }

    private void TryAddComponentByType(EntityUid uid, Type type)
    {
        try
        {
            if (!typeof(Component).IsAssignableFrom(type))
                return;

            var entityManagerType = EntityManager.GetType();

            var hasComponent = entityManagerType.GetMethods()
                .FirstOrDefault(m =>
                    m.Name == "HasComponent" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(EntityUid) &&
                    m.GetParameters()[1].ParameterType == typeof(Type));

            if (hasComponent?.Invoke(EntityManager, new object[] { uid, type }) is true)
                return;

            var addComponent = entityManagerType.GetMethods()
                .FirstOrDefault(m =>
                    m.Name == "AddComponent" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(EntityUid) &&
                    m.GetParameters()[1].ParameterType == typeof(Type));

            var comp = addComponent?.Invoke(EntityManager, new object[] { uid, type });
            if (comp == null)
                return;

            foreach (var name in new[] { "Intensity", "Drunkness", "Drunkenness", "BoozePower", "Duration", "Time" })
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    SetCommonValue(field.FieldType, value => field.SetValue(comp, value));

                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop is { CanWrite: true })
                    SetCommonValue(prop.PropertyType, value => prop.SetValue(comp, value));
            }
        }
        catch { }
    }

    private static void SetCommonValue(Type valueType, Action<object> setter)
    {
        if (valueType == typeof(float))
            setter(2.5f);
        else if (valueType == typeof(double))
            setter(2.5d);
        else if (valueType == typeof(int))
            setter(3);
        else if (valueType == typeof(TimeSpan))
            setter(TimeSpan.FromSeconds(20));
    }

    private static Type? FindType(string shortName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in SafeTypes(asm))
            {
                if (type.Name == shortName)
                    return type;
            }
        }

        return null;
    }

    private static IEnumerable<Type> SafeTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch { return Array.Empty<Type>(); }
    }

    private static bool TryGetTarget(OnIrradiatedEvent args, out EntityUid uid)
    {
        uid = default;

        var boxed = (object) args;
        var type = boxed.GetType();

        foreach (var name in new[] { "Entity", "Uid", "Target", "Receiver", "Victim" })
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.GetValue(boxed) is EntityUid propUid)
            {
                uid = propUid;
                return true;
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field?.GetValue(boxed) is EntityUid fieldUid)
            {
                uid = fieldUid;
                return true;
            }
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (prop.PropertyType != typeof(EntityUid))
                continue;

            if (prop.GetValue(boxed) is EntityUid found)
            {
                uid = found;
                return true;
            }
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (field.FieldType != typeof(EntityUid))
                continue;

            if (field.GetValue(boxed) is EntityUid found)
            {
                uid = found;
                return true;
            }
        }

        return false;
    }

    private static float GetSeverity(OnIrradiatedEvent args)
    {
        var boxed = (object) args;
        var type = boxed.GetType();

        foreach (var name in new[] { "RadsPerSecond", "Radiation", "Rads", "Amount", "Intensity", "Dose", "Severity" })
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (TryFloat(prop?.GetValue(boxed), out var propValue))
                return MathF.Abs(propValue);

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (TryFloat(field?.GetValue(boxed), out var fieldValue))
                return MathF.Abs(fieldValue);
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (TryFloat(prop.GetValue(boxed), out var value))
                return MathF.Abs(value);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (TryFloat(field.GetValue(boxed), out var value))
                return MathF.Abs(value);
        }

        return 0.25f;
    }

    private static bool TryFloat(object? obj, out float value)
    {
        value = 0f;

        switch (obj)
        {
            case float f:
                value = f;
                return true;
            case double d:
                value = (float) d;
                return true;
            case int i:
                value = i;
                return true;
            default:
                return false;
        }
    }
}
