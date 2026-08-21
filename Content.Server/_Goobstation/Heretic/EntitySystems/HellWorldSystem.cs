using Content.Server.GameTicking.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Robust.Shared.Timing;
using System.Linq;
using Content.Server.Heretic.Components;
using Content.Shared.Heretic.Prototypes;
using Content.Shared.Examine;
using Content.Server.Body.Systems;
using Content.Server._Goobstation.Heretic.Components;
using Content.Server._Goobstation.Heretic.UI;
using System.Collections.Immutable;
using Content.Server.EUI;
using Robust.Shared.Random;
using Content.Server.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Server.Administration.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Server.Player;
using Robust.Server.GameObjects;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Eye.Blinding.Components;

//this is kind of badly named since we're doing infinite archives stuff now but i dont feel like changing it :)

namespace Content.Server._Goobstation.Heretic.EntitySystems
{

    public sealed partial class HellWorldSystem : EntitySystem
    {
        [Dependency] private readonly SharedMindSystem _mind = default!;
        [Dependency] private readonly SharedMapSystem _map = default!;
        [Dependency] private readonly MetaDataSystem _metaSystem = default!;
        [Dependency] private readonly SharedTransformSystem _xform = default!;
        [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
        [Dependency] private readonly EuiManager _euiMan = default!;
        [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
        [Dependency] private readonly BlindableSystem _blind = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IEntityManager _ent = default!;
        [Dependency] private readonly IPlayerManager _player = default!;

        private readonly ResPath _mapPath = new("Maps/_Impstation/Nonstations/InfiniteArchives.yml");

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HellVictimComponent, ExaminedEvent>(OnExamine);
        }

        /// <summary>
        /// Creates the hell world map.
        /// </summary>
        public void MakeHell()
        {
            if (_mapLoader.TryLoadMap(_mapPath, out var map, out _, new DeserializationOptions { InitializeMaps = true }))
                _map.SetPaused(map.Value.Comp.MapId, false);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            //hell world return
            var returnQuery = EntityQueryEnumerator<HellVictimComponent>();
            while (returnQuery.MoveNext(out var uid, out var victimComp))
            {
                //if they've been in hell long enough, return and revive them
                if (_timing.CurTime >= victimComp.ExitHellTime && !victimComp.CleanupDone)
                {
                    //make sure they won't get into this loop again
                    victimComp.CleanupDone = true;

                    if (!TryComp<MindComponent>(victimComp.Mind, out var mindComp)) //Omu prevent entities with a null mind crashing the server
                        continue;

                    //put them back in the original body
                    _mind.TransferTo(victimComp.Mind, victimComp.OriginalBody);
                    //let them ghost again
                    mindComp.PreventGhosting = false;
                    //give the original body some visual changes
                    TransformVictim(uid);
                    //tell them about the metashield
                    if (_player.TryGetSessionByEntity(victimComp.Owner, out var session))
                        _euiMan.OpenEui(new HellMemoryEui(), session);
                    //and then revive the old body
                    _rejuvenate.PerformRejuvenate(uid);
                }
            }
        }

        public void AddVictimComponent(EntityUid victim)
        {
            EnsureComp<HellVictimComponent>(victim, out var victimComp);
            victimComp.OriginalBody = victim;
            victimComp.ExitHellTime = _timing.CurTime + victimComp.HellDuration;
            victimComp.OriginalPosition = Transform(victim).Coordinates;
            //make sure the victim has a mind
            if (!TryComp<MindContainerComponent>(victim, out var mindContainer) || !mindContainer.HasMind)
            {
                return;
            }
            victimComp.Mind = mindContainer.Mind.Value;
        }

        //AddVictimComponent MUST BE RUN BEFORE CALLING THIS!!
        public void SendToHell(EntityUid target, RitualData args, SpeciesPrototype species)
        {
            //get the hell victim component
            if (!args.EntityManager.TryGetComponent<HellVictimComponent>(target, out var victimComp))
                return;
            //if already sent, don't send again
            if(victimComp.AlreadyHelled)
                return;

            //get all possible spawn points, choose one, then get the place
            var spawnPoints = EntityManager.GetAllComponents(typeof(HellSpawnPointComponent)).ToImmutableList();
            var newSpawn = _random.Pick(spawnPoints);
            var spawnTgt = Transform(newSpawn.Uid).Coordinates;

            //spawn your hellsona
            if (TryComp<MindComponent>(victimComp.Mind, out MindComponent? mindComp))       //Omu edit check for null reference to mind
            {
                mindComp = Comp<MindComponent>(victimComp.Mind);
                mindComp.PreventGhosting = true;
            }       //Omu end
            //don't have to change this one's blood because nobody's bringing a forensic scanner to hell
            var Entityinhell = Spawn(species.Prototype, spawnTgt);
            _metaSystem.SetEntityName(Entityinhell, MetaData(target).EntityName);
            _humanoid.CloneAppearance(victimComp.OriginalBody, Entityinhell);
            if (TryComp<BlindableComponent>(Entityinhell, out var blindable))
            {
                _blind.AdjustEyeDamage(Entityinhell, 5); //make it more disorienting

            }

            //and then send the mind into the hellsona
            if (mindComp is not null)   //Omu double check for a mind
                _mind.TransferTo(victimComp.Mind, Entityinhell);
            victimComp.AlreadyHelled = true;

            //returning the mind to the original body happens in Update()
        }

        //ported from funkystation
        public void TeleportRandomly(RitualData args, EntityUid uid) // start le teleporting loop -space
        {
            var maxrandomtp = 40; // this is how many attempts it will try before breaking the loop -space
            var maxrandomradius = 20; // this is the max range it will do -space


            if (!args.EntityManager.TryGetComponent<TransformComponent>(uid, out var xform))
                return;
            var coords = xform.Coordinates;
            var newCoords = coords.Offset(_random.NextVector2(maxrandomradius));
            for (var i = 0; i < maxrandomtp; i++) //start of the loop -space
            {
                var randVector = _random.NextVector2(maxrandomradius);
                newCoords = coords.Offset(randVector);
                if (!args.EntityManager.TryGetComponent<TransformComponent>(uid, out var trans))
                    continue;
                if (trans.GridUid != null && !_lookup.GetEntitiesIntersecting(newCoords.ToMap(_ent, _xform), LookupFlags.Static).Any()) // if they're not in space and not in wall, it will choose these coords and end the loop -space
                {
                    break;
                }
            }

            _xform.SetCoordinates(uid, newCoords);
        }

        private void TransformVictim(EntityUid ent)
        {
            if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
                return;

            //make them look like they've seen some shit
            const float palenessMultiplier = 0.25f;
            _humanoid.SetSkinColor(ent, AdjustSaturation(humanoid.SkinColor, palenessMultiplier), true, false, humanoid);
            humanoid.EyeColor = Color.White;
            _humanoid.SetBaseLayerColor(ent, HumanoidVisualLayers.Eyes, humanoid.EyeColor, true, humanoid);

            foreach (var (category, markings) in humanoid.MarkingSet.Markings)
            {
                for (var markingIndex = 0; markingIndex < markings.Count; markingIndex++)
                {
                    var markingColors = markings[markingIndex]
                        .MarkingColors.Select(color => AdjustSaturation(color, palenessMultiplier))
                        .ToList();

                    _humanoid.SetMarkingColor(ent, category, markingIndex, markingColors);
                }
            }
        }

        private static Color AdjustSaturation(Color color, float saturationMultiplier)
        {
            var hsv = Color.ToHsv(color);
            hsv.Y *= saturationMultiplier;
            return Color.FromHsv(hsv);
        }

        private void OnExamine(Entity<HellVictimComponent> ent, ref ExaminedEvent args)
        {
            args.PushMarkup($"[color=red]{Loc.GetString("heretic-hell-victim-examine", ("ent", args.Examined))}[/color]");
        }
    }
}
