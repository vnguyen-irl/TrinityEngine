﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace PhysicsTestbed
{
    public class BodyImpulse : EnvironmentImpulse
    {
        [Controllable(Type = ControllableAttribute.ControllerType.Slider, Caption = "CCD time offset (pure visual)", Min = 0.01, Max = 0.99)]
        public static double ccdTimeOffset = 0.01;
        [Controllable(Type = ControllableAttribute.ControllerType.Slider, Caption = "0.01x CCD time offset", Min = -0.01, Max = 0.01)]
        public static double ccdTimeOffset001x = -0.01;

        public static double CCDTimeOffset { get{ return ccdTimeOffset + ccdTimeOffset001x; } }

        private const double epsilon = 0.00001;

        private bool CollideSweptSegments(LineSegment first, LineSegment second, ref Vector2 intersection)
        {
            double Ua, Ub;
            // Equations to determine whether lines intersect
            Ua = ((second.end.X - second.start.X) * (first.start.Y - second.start.Y) - (second.end.Y - second.start.Y) * (first.start.X - second.start.X)) /
                ((second.end.Y - second.start.Y) * (first.end.X - first.start.X) - (second.end.X - second.start.X) * (first.end.Y - first.start.Y));
            Ub = ((first.end.X - first.start.X) * (first.start.Y - second.start.Y) - (first.end.Y - first.start.Y) * (first.start.X - second.start.X)) /
                ((second.end.Y - second.start.Y) * (first.end.X - first.start.X) - (second.end.X - second.start.X) * (first.end.Y - first.start.Y));
            if (Ua >= 0.0f && Ua <= 1.0f && Ub >= 0.0f && Ub <= 1.0f)
            {
                intersection.X = first.start.X + Ua*(first.end.X - first.start.X);
                intersection.Y = first.start.Y + Ua*(first.end.Y - first.start.Y);
                return true;
            }
            return false;
        }

        private void CheckSegment(
            Vector2 origin, Vector2 neighbor,
            Vector2 pos, Vector2 posNext, Vector2 velocity, ref List<CollisionSubframeBuffer> collisionBuffer,
            CollisionSubframeBuffer subframeToAdd
        )
        {
            Vector2 intersection = new Vector2();
            if (CollideSweptSegments(new LineSegment(origin, neighbor), new LineSegment(pos, posNext), ref intersection))
            {
                double particleOffset = (intersection - pos).Length();
                if (particleOffset > epsilon) // to prevent slipping of start point to just reflected edge // TODO: check if this condition is really usefull. may be (timeOffset > 0.0) is a sufficient condition
                {
                    // reflect velocity relative edge
                    Vector2 reflectSurface = neighbor - origin;
                    Vector2 reflectNormal = new Vector2(-reflectSurface.Y, reflectSurface.X);
                    if (reflectNormal.Dot(velocity) < 0) reflectNormal = -reflectNormal;
                    Vector2 newVelocity = velocity - 2.0 * reflectNormal * (reflectNormal.Dot(velocity) / reflectNormal.LengthSq());

                    subframeToAdd.vParticle = newVelocity;
                    subframeToAdd.vEdgeStart = new Vector2(0.0, 0.0);
                    subframeToAdd.vEdgeEnd = new Vector2(0.0, 0.0);
                    subframeToAdd.timeCoefficient = particleOffset / velocity.Length();
                    collisionBuffer.Add(subframeToAdd);
                }
            }
        }

        private void CheckParticleEdge(
            bool isFrozenEdge, ref List<Particle.CCDDebugInfo> ccds,
            Particle origin, Particle neighbor,
            Vector2 pos, Vector2 posNext, Vector2 velocity,
            ref List<CollisionSubframeBuffer> collisionBuffer, double timeCoefficientPrediction,
            CollisionSubframeBuffer subframeToAdd
        )
        {
            if (isFrozenEdge)
            {
                // simple collision for frozen body
                CheckSegment(origin.goal, neighbor.goal, pos, posNext, velocity, ref collisionBuffer, subframeToAdd); // current edge position
                return;
            }

            // swept collision for body
            LineSegment edge = new LineSegment(origin.x, neighbor.x);
            LineSegment edgeNext = new LineSegment(edge.start + origin.v * timeCoefficientPrediction, edge.end + neighbor.v * timeCoefficientPrediction);

            EdgePointCCDSolver.SolverInput solverInput = new EdgePointCCDSolver.SolverInput(edge, edgeNext, pos, posNext);
            double? ccdCollisionTime = EdgePointCCDSolver.Solve(solverInput);

            if (ccdCollisionTime != null)
            {
                Particle.CCDDebugInfo ccd = GenerateDebugInfo(solverInput, ccdCollisionTime.Value);

                // TODO: use the Rule of conservative impulse to handle this case. Simple reflection rule is not effective here.

                Vector2 velocityEdgeCollisionPoint = origin.v + (neighbor.v - origin.v) * ccd.coordinateOfPointOnEdge;
                Vector2 velocityPointRelativeEdge = velocity - velocityEdgeCollisionPoint;

                // compute velocity reflection relativly moving edge
                Vector2 reflectSurface = ccd.edge.end - ccd.edge.start;
                Vector2 reflectNormal = new Vector2(-reflectSurface.Y, reflectSurface.X);
                if (reflectNormal.Dot(velocityPointRelativeEdge) < 0) reflectNormal = -reflectNormal;
                Vector2 newVelocity = velocityPointRelativeEdge - 2.0 * reflectNormal * (reflectNormal.Dot(velocityPointRelativeEdge) / reflectNormal.LengthSq());
                if (ccdCollisionTime.Value <= 0.0) Testbed.PostMessage(System.Drawing.Color.Red, "timeCoefficient = 0"); // Zero-Distance not allowed // DEBUG
                double newTimeCoefficient = timeCoefficientPrediction * ccdCollisionTime.Value;
                newTimeCoefficient -= epsilon / newVelocity.Length(); // try to prevent Zero-Distances // HACK
                if (newTimeCoefficient < 0.0) newTimeCoefficient = 0.0; // don't move particle toward edge - just reflect velocity
                newVelocity += velocityEdgeCollisionPoint; // newVelocity should be in global coordinates

                subframeToAdd.vParticle = newVelocity;  // TODO: correct newVelocity computation formula - remember about impulse concervation rule!
                //subframeToAdd.vEdgeStart = ; // TODO: implement
                //subframeToAdd.vEdgeEnd = ; // TODO: implement
                subframeToAdd.timeCoefficient = newTimeCoefficient;
                collisionBuffer.Add(subframeToAdd);
                ccds.Add(ccd);

                if (LsmBody.pauseOnBodyBodyCollision)
                    Testbed.Paused = true;
            }
        }

        Particle.CCDDebugInfo GenerateDebugInfo(EdgePointCCDSolver.SolverInput solverInput, double time) // DEBUG
        {
            Particle.CCDDebugInfo ccd = new Particle.CCDDebugInfo(
                solverInput.currentPoint + (solverInput.nextPoint - solverInput.currentPoint) * time,
                new LineSegment(
                    solverInput.currentEdge.start + (solverInput.nextEdge.start - solverInput.currentEdge.start) * time,
                    solverInput.currentEdge.end + (solverInput.nextEdge.end - solverInput.currentEdge.end) * time
                )
            );
            return ccd;
        }

        public override void ApplyImpulse(LsmBody applyBody, Particle applyParticle, LsmBody otherBody, double timeCoefficientPrediction, ref List<CollisionSubframeBuffer> collisionBuffer)
        {
            Debug.Assert(!applyBody.Equals(otherBody)); // don't allow self-collision here

            Vector2 pos = applyParticle.x;
            Vector2 velocity = applyParticle.v;
            Vector2 posNext = pos + velocity * timeCoefficientPrediction;

            LsmBody body = otherBody;
            // iterate all possible edges of body and test them with current subframed point
            foreach (Particle bodyt in body.particles)
            {
                if (bodyt.xPos != null)
                {
                    CheckParticleEdge(
                        body.frozen, ref applyParticle.ccdDebugInfos, bodyt, bodyt.xPos, pos, posNext, velocity, ref collisionBuffer, timeCoefficientPrediction,
                        new CollisionSubframeBuffer(applyParticle, applyParticle.v, new Edge(bodyt, bodyt.xPos), bodyt.v, bodyt.xPos.v, 0.0)
                    );
                }
                if (bodyt.yPos != null)
                {
                    CheckParticleEdge(
                        body.frozen, ref applyParticle.ccdDebugInfos, bodyt, bodyt.yPos, pos, posNext, velocity, ref collisionBuffer, timeCoefficientPrediction,
                        new CollisionSubframeBuffer(applyParticle, applyParticle.v, new Edge(bodyt, bodyt.yPos), bodyt.v, bodyt.yPos.v, 0.0)
                    );
                }
            }
        }
    }
}
