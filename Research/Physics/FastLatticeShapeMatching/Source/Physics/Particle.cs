using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Diagnostics;

namespace PhysicsTestbed
{
    public class CollisionSubframe
    {
        public Vector2 v;				        // Velocity before collision
        public double timeCoefficient = 0.0;    // Part of time step before collision - [0, 1]

        public CollisionSubframe(Vector2 v, double timeCoefficient)
        {
            //Debug.Assert(timeCoefficient > 0.0);
            this.v = v;
            this.timeCoefficient = timeCoefficient;
        }
    }

	public class Particle
	{
        public LsmBody body;
		public LatticePoint latticePoint;
		public Chunk chunk;
        public bool locked = false;					// Whether I have been locked in place
        public Particle xPos, yPos, xNeg, yNeg;		// Neighbors
		public ManagedList<SmoothingRegion> parentRegions = new ManagedList<SmoothingRegion>();

		// Physical properties
        public double mass = 1.0;
        public Vector2 x;							// Position
		public Vector2 v;							// Velocity
        public Vector2 x0;							// Material position
        public Vector2 fExt;						// External force

        // continues collision detection and impulse integration
        public List<CollisionSubframe> collisionSubframes = new List<CollisionSubframe>();

        public class CCDDebugInfo
        {
            public Vector2 point;
            public LineSegment edge;
            public double coordinateOfPointOnEdge;
            public CCDDebugInfo(Vector2 point, LineSegment edge)
            { 
                this.point = point;
                this.edge = edge;
                this.coordinateOfPointOnEdge = (point - edge.start).Dot(edge.end - edge.start) / (edge.end - edge.start).LengthSq();
            }
        }
        public CCDDebugInfo ccdDebugInfo = null; // DEBUG

		// Shape matching
        public Vector2 goal;
        public Matrix2x2 R;							// Average of all parent regions' Rs - used in fracturing

		// Damping
		public Vector2 dv;

		public double PerRegionMass					// m~ in [Rivers and James 2007]
		{
			get { return mass / parentRegions.Count; }
		}

		public bool DoFracture()
		{
			bool somethingBroke = false;
			if(xPos != null)
				somethingBroke = somethingBroke || DoFracture(ref xPos, ref xPos.xNeg);
			if(yPos != null)
				somethingBroke = somethingBroke || DoFracture(ref yPos, ref yPos.yNeg);
			return somethingBroke;
		}

		public bool DoFracture(ref Particle other, ref Particle me)
		{
			bool somethingBroke = false;

			double len = (other.goal - goal).Length();
			double rest = (other.x0 - x0).Length();
			double off = Math.Abs((len / rest) - 1.0);
			if (off > LsmBody.fractureLengthTolerance)
			{
				somethingBroke = true;
				Testbed.PostMessage("Length fracture: Rest = " + rest + ", actual = " + len);
			}

			if (!somethingBroke)
			{
				Vector2 a = new Vector2(other.R[0, 0], other.R[1, 0]);
				Vector2 b = new Vector2(R[0, 0], R[1, 0]);
				a.Normalize();
				b.Normalize();
				double angleDifference = Math.Acos(a.Dot(b));
				if (angleDifference > LsmBody.fractureAngleTolerance)
				{
					somethingBroke = true;
					Testbed.PostMessage("Angle fracture: angle difference = " + angleDifference);
				}
			}

			if (somethingBroke)
			{
				Particle saved = other;
				me = null;
				other = null;

				// Check if the chunks are still connected
				Queue<Particle> edge = new Queue<Particle>();
				List<Particle> found = new List<Particle>();
				edge.Enqueue(this);
				bool connected = false;
				while (edge.Count > 0)
				{
					Particle p = edge.Dequeue();
					if (!found.Contains(p))
					{
						found.Add(p);
						if (p == saved)
						{
							// Connected
							connected = true;
							break;
						}
						if (p.xPos != null)
							edge.Enqueue(p.xPos);
						if (p.xNeg != null)
							edge.Enqueue(p.xNeg);
						if (p.yPos != null)
							edge.Enqueue(p.yPos);
						if (p.yNeg != null)
							edge.Enqueue(p.yNeg);
					}
				}
				if (connected == false)
				{
					// The chunks broke - there are now two separate chunks (maximally connected subgraphs)
					chunk.particles.Clear();
					chunk.particles.AddRange(found);
					chunk.CalculateInvariants();

					Chunk newChunk = new Chunk();

					edge.Clear();
					found.Clear();
					edge.Enqueue(saved);
					while (edge.Count > 0)
					{
						Particle p = edge.Dequeue();
						if (!found.Contains(p))
						{
							found.Add(p);
							p.chunk = newChunk;
							if (p.xPos != null)
								edge.Enqueue(p.xPos);
							if (p.xNeg != null)
								edge.Enqueue(p.xNeg);
							if (p.yPos != null)
								edge.Enqueue(p.yPos);
							if (p.yNeg != null)
								edge.Enqueue(p.yNeg);
						}
					}

					newChunk.particles.AddRange(found);
					newChunk.CalculateInvariants();
					body.chunks.Add(newChunk);

					Testbed.PostMessage("Chunk broken: the original chunk now has " + chunk.particles.Count + " particles, the new chunk has " + newChunk.particles.Count + " particles.");
					Testbed.PostMessage("Number of chunks / particles: " + body.chunks.Count + " / " + body.particles.Count);
				}
			}

			return somethingBroke;
		}
	}
}
