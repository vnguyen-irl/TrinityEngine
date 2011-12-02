using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Tao.OpenGl;

namespace PhysicsTestbed
{
	public class Testbed
	{
		public static World world = World.Singleton;

		public static LsmBody testBody;
        public static Array currentBlueprints = null;

		public static DragForce dragForce;
		public static WallImpulse wallImpulse;
		public static PushForce pushForce;
		public static LockForce lockForce;
		public static GravityForce gravityForce;

		public static bool paused = false;
		public static int updateFrame = 0;

		//[Controllable(Type = ControllableAttribute.ControllerType.Checkbox, Caption="Walls closing in")]
		public static bool wallsClosingIn = false;

		public static bool Paused
		{
			get
			{
				return paused;
			}
			set
			{
				if(paused != value)
				{
					paused = value;
					if(paused)
						Program.window.runCheckBox.Checked = false;
				}
			}
		}

		public static void Initialize()
		{
			dragForce = new DragForce();
			wallImpulse = new WallImpulse(9999, 9999);
			pushForce = new PushForce();
			lockForce = new LockForce();
			gravityForce = new GravityForce();
			world.environmentForces.Add(dragForce);
			world.environmentImpulses.Add(wallImpulse);
			world.environmentForces.Add(pushForce);
			world.environmentForces.Add(lockForce);
			world.environmentForces.Add(gravityForce);

            // SimpleBlueprint.blueprint; RectangleBlueprint.blueprint; HumanBlueprint.blueprint; BuildingBlueprint.blueprint; ChairBlueprint.blueprint;
            //currentBlueprints = new Array[3]{ HumanBlueprint.blueprint, ChairBlueprint.blueprint, BuildingBlueprint.blueprint };
            currentBlueprints = new Array[2] { RectangleBlueprint.blueprint, RectangleBlueprint.blueprint };
            GenerateBodies(currentBlueprints);
        }

        public static void GenerateBodies(Array blueprints)
        {
            testBody = null;
            int verticalOffset = 0;
            foreach (bool[,] blueprint in blueprints)
            {
                LsmBody body = new LsmBody(
                    new Vector2(50, 50 + 150 * verticalOffset), 
                    new Color3(verticalOffset == 1 ? 1 : 0, verticalOffset == 0 ? 1 : 0, verticalOffset == 2 ? 1 : 0)
                );
                world.bodies.Add(body);
                ++verticalOffset;
                body.GenerateFromBlueprint(blueprint);
                if (testBody == null)
                    testBody = body;
            }
        }

		public static void Reset()
		{
            world.bodies.Clear();
            GenerateBodies(currentBlueprints);
		}

		public static void SetModel(int blueprintNo, int modelNo)
		{
			switch(modelNo)
			{
				case 1:
                    currentBlueprints.SetValue(RectangleBlueprint.blueprint, blueprintNo);
					break;
				case 2:
                    currentBlueprints.SetValue(HumanBlueprint.blueprint, blueprintNo);
					break;
				case 3:
                    currentBlueprints.SetValue(ChairBlueprint.blueprint, blueprintNo);
					break;
				case 4:
                    currentBlueprints.SetValue(SimpleBlueprint.blueprint, blueprintNo);
					break;
                case 5:
                    currentBlueprints.SetValue(SingleBlueprint.blueprint, blueprintNo);
                    break;
            }
			Reset();
		}

		public static void Update()
		{
			if(wallsClosingIn)
			{
				wallImpulse.bottom++;
				wallImpulse.top--;
				//wallForce.left++;
				//wallForce.right--;
			}

			world.Update();
		}

		public static void Render()
		{
			foreach (LsmBody body in world.bodies)
			{
				// Draw normal particles
                Gl.glColor3d(body.Color.R, body.Color.G, body.Color.B);
				Gl.glPointSize(4.0f);
				Gl.glBegin(Gl.GL_POINTS);
				foreach (Particle t in body.particles)
				{
                    Gl.glVertex2d(t.goal.X, t.goal.Y);
				}
				Gl.glEnd();

				// Redraw locked particles
				Gl.glColor3d(1, 0, 0);
				Gl.glPointSize(6.0f);
				Gl.glBegin(Gl.GL_POINTS);
				foreach (Particle t in body.particles)
				{
					if (t.locked)
                        Gl.glVertex2d(t.goal.X, t.goal.Y);
				}
				Gl.glEnd();

                // Redraw collisioned particles
				Gl.glColor3d(0, 0, 0);
				Gl.glPointSize(4.0f);
				foreach (Particle t in body.particles)
				{
                    if (t.vHistory.Count > 0)
                    {
                        Gl.glBegin(Gl.GL_POINTS);
                        Gl.glVertex2d(t.goal.X, t.goal.Y);
                        Gl.glEnd();

                        Vector2 hPosition = t.goal;
                        Gl.glBegin(Gl.GL_LINE_STRIP);
                        Gl.glVertex2d(hPosition.X, hPosition.Y);
                        foreach (PreCollisionHistory history in t.vHistory)
                        {
                            hPosition += history.v * history.timeCoefficient;
                            Gl.glVertex2d(hPosition.X, hPosition.Y);
                        }
                        Gl.glEnd();
                    }
				}
			}

			// Draw the drag force
			if (dragForce.Dragging)
			{
				// Draw line between knobs
				Gl.glLineWidth(1.0f);
				Gl.glColor4f(0.0f, 0.0f, 0.0f, 1.0f);
				Gl.glBegin(Gl.GL_LINES);
				{
                    Gl.glVertex2i(dragForce.mouse.X, dragForce.mouse.Y);
                    Gl.glVertex2d(dragForce.selected.goal.X, dragForce.selected.goal.Y);
				}
				Gl.glEnd();

				// Draw source knob
				//   Outline
				Gl.glPointSize(12.0f);
				Gl.glColor4f(0.0f, 0.0f, 0.0f, 1.0f);
				Gl.glBegin(Gl.GL_POINTS);
				{
                    Gl.glVertex2d(dragForce.selected.goal.X, dragForce.selected.goal.Y);
                }
				Gl.glEnd();

				//   Knob
				Gl.glPointSize(9.0f);
				Gl.glColor4f(1.0f, 0.0f, 0.0f, 1.0f);
				Gl.glBegin(Gl.GL_POINTS);
				{
                    Gl.glVertex2d(dragForce.selected.goal.X, dragForce.selected.goal.Y);
                }
				Gl.glEnd();

				// Draw dest knob
				//   Outline
				Gl.glPointSize(12.0f);
				Gl.glColor4f(0.0f, 0.0f, 0.0f, 1.0f);
				Gl.glBegin(Gl.GL_POINTS);
				{
					Gl.glVertex2i(dragForce.mouse.X, dragForce.mouse.Y);
				}
				Gl.glEnd();

				//   Knob
				Gl.glPointSize(9.0f);
				Gl.glColor4f(0.0f, 1.0f, 0.0f, 1.0f);
				Gl.glBegin(Gl.GL_POINTS);
				{
					Gl.glVertex2i(dragForce.mouse.X, dragForce.mouse.Y);
				}
				Gl.glEnd();
			}

			// Draw the push force
			if (pushForce.Pushing)
			{
				double dist = pushForce.PushDistance;
				Point pos = pushForce.PushPosition;
				double angle;
				Gl.glLineWidth(1.0f);
				Gl.glColor4f(0.0f, 0.0f, 0.0f, 1.0f);
				Gl.glBegin(Gl.GL_LINES);
				for (angle = 0; angle < Math.PI * 2; angle += 0.1)
				{
					double angle2 = Math.Min(angle + 0.1, Math.PI * 2);
					double x1, y1, x2, y2;
					x1 = pos.X + Math.Cos(angle) * dist;
					y1 = pos.Y + Math.Sin(angle) * dist;
					x2 = pos.X + Math.Cos(angle2) * dist;
					y2 = pos.Y + Math.Sin(angle2) * dist;
					Gl.glVertex2d(x1, y1);
					Gl.glVertex2d(x2, y2);
				}
				Gl.glEnd();
			}

            // Draw wall ranges
            Gl.glLineWidth(1.0f);
            Gl.glColor4f(0.5f, 0.5f, 0.5f, 1.0f);
            Gl.glBegin(Gl.GL_LINES);
            {
                Gl.glVertex2d(wallImpulse.left + wallImpulse.border, wallImpulse.top);
                Gl.glVertex2d(wallImpulse.left + wallImpulse.border, wallImpulse.bottom);
                Gl.glVertex2d(wallImpulse.right - wallImpulse.border, wallImpulse.top);
                Gl.glVertex2d(wallImpulse.right - wallImpulse.border, wallImpulse.bottom);
                Gl.glVertex2d(wallImpulse.left, wallImpulse.top - wallImpulse.border);
                Gl.glVertex2d(wallImpulse.right, wallImpulse.top - wallImpulse.border);
                Gl.glVertex2d(wallImpulse.left, wallImpulse.bottom + wallImpulse.border);
                Gl.glVertex2d(wallImpulse.right, wallImpulse.bottom + wallImpulse.border);
            }
            Gl.glEnd();
		}

		public static void PauseStep()
		{
			paused = false;
			Update();
			paused = true;
		}

		public static void PostMessage(string message)
		{
			Program.window.statusBox.PostMessage(Color.Black, message);
		}

		public static void PostMessage(Color color, string message)
		{
			Program.window.statusBox.PostMessage(color, message);
		}
	}
}
