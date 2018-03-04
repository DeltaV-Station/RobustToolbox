﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS14.Server.GameObjects;
using SS14.Server.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Interfaces.Physics;
using SS14.Shared.Interfaces.Timing;
using SS14.Shared.IoC;
using SS14.Shared.Maths;
using YamlDotNet.Serialization;

namespace SS14.Server.AI
{
    /// <summary>
    ///     The object stays stationary. The object will periodically scan for *any* life forms in its radius, and engage them.
    ///     The object will rotate itself to point at the locked entity, and if it has a weapon will shoot at the entity.
    /// </summary>
    [AiLogicProcessor("AimShootLife")]
    class AimShootLifeProcessor : AiLogicProcessor
    {
        private readonly ICollisionManager _physMan;
        private readonly IServerEntityManager _entMan;
        private readonly IGameTiming _timeMan;

        private List<IEntity> _workList = new List<IEntity>();

        private const float MaxAngSpeed = (float) (Math.PI / 2);
        private const float ScanPeriod = 1.0f; // tweak this for performance and gameplay experience
        private float _lastScan;
        
        private IEntity _curTarget;

        public AimShootLifeProcessor()
        {
            _physMan = IoCManager.Resolve<ICollisionManager>();
            _entMan = IoCManager.Resolve<IServerEntityManager>();
            _timeMan = IoCManager.Resolve<IGameTiming>();
        }

        public override void Update(float frameTime)
        {
            if(SelfEntity == null)
                return;
            
            DoScanning();
            DoTracking(frameTime);
        }

        private void DoScanning()
        {
            var curTime = _timeMan.CurTime.TotalSeconds;
            if (curTime - _lastScan > ScanPeriod)
            {
                _lastScan = (float) curTime;
                _curTarget = FindBestTarget();
            }
        }

        private void DoTracking(float frameTime)
        {
            // not valid entity to target.
            if (_curTarget == null || !_curTarget.IsValid())
            {
                _curTarget = null;
                return;
            }

            // point me at the target
            var tarPos = _curTarget.GetComponent<ITransformComponent>().WorldPosition;
            var myPos = SelfEntity.GetComponent<ITransformComponent>().WorldPosition;
            
            var curDir = SelfEntity.GetComponent<IServerTransformComponent>().LocalRotation.ToVec();
            var tarDir = (tarPos - myPos).Normalized;

            var step = MaxAngSpeed * frameTime;
            var up = Vector3.UnitZ;

            var tar3D = new Vector3(tarDir);
            var qCur = Quaternion.LookRotation(ref tar3D, ref up);

            var cur3D = new Vector3(curDir);
            var qTar = Quaternion.LookRotation(ref cur3D, ref up);
            var qRotation = Quaternion.RotateTowards(qCur, qTar, step);

            var result = Quaternion.ToEulerRad(qRotation);
            
            SelfEntity.GetComponent<IServerTransformComponent>().LocalRotation = new Angle(result.X * FloatMath.DegToRad);

            //TODO: shoot gun if i have it
        }

        private IEntity FindBestTarget()
        {
            // "best" target is the closest one with LOS

            var ents = _entMan.GetEntitiesInRange(SelfEntity, VisionRadius);
            var myTransform = SelfEntity.GetComponent<IServerTransformComponent>();
            var maxRayLen = VisionRadius * 2.5f; // circle inscribed in square, square diagonal = 2*r*sqrt(2)

            _workList.Clear();
            foreach (var entity in ents)
            {
                // filter to "people" entities (entities with controllers)
                if (!entity.HasComponent<IMoverComponent>())
                    continue;

                // build the ray
                var dir = entity.GetComponent<TransformComponent>().WorldPosition - myTransform.WorldPosition;
                var ray = new Ray(myTransform.WorldPosition, dir.Normalized);

                // cast the ray
                var result = _physMan.IntersectRay(ray, maxRayLen);

                // add to visible list
                if (result.HitEntity == entity)
                    _workList.Add(entity);
            }
            
            // get closest entity in list
            var closestEnt = GetClosest(myTransform.WorldPosition, _workList);

            // return closest
            return closestEnt;
        }

        private static IEntity GetClosest(Vector2 origin, IEnumerable<IEntity> list)
        {
            IEntity closest = null;
            var minDistSqrd = float.PositiveInfinity;

            foreach (var ent in list)
            {
                var pos = ent.GetComponent<ITransformComponent>().WorldPosition;
                var distSqrd = (pos - origin).LengthSquared;

                if (distSqrd > minDistSqrd)
                    continue;

                closest = ent;
                minDistSqrd = distSqrd;
            }

            return closest;
        }

        private static float MoveTowards(float current, float target, float speed, float delta)
        {
            var maxDeltaDist = speed * delta;
            var toGo = target - current;

            if (maxDeltaDist > Math.Abs(toGo))
                return target;

            return current + Math.Sign(toGo) * maxDeltaDist;
        }

        private static Vector2 MoveTowards(Vector2 current, Vector2 target, float speed, float delta)
        {
            var maxDeltaDist = speed * delta;
            var a = target - current;
            var magnitude = a.Length;
            if (magnitude <= maxDeltaDist)
            {
                return target;
            }
            return current + a / magnitude * maxDeltaDist;
        }
    }
}
