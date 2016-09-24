using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BurnTogether
{
	public class Utils
	{
		public static AnimationState[] SetUpAnimation(string animationName, Part part)  //Thanks Majiir!
        {
            var states = new List<AnimationState>();
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states.ToArray();
        }
		
		public float GetFwdSrfVelocity(Vessel v)
		{
			return 1;
		}

		public static Vector3d ProjectOnPlane(Vector3d point, Vector3d planePoint, Vector3d planeNormal)
		{
			planeNormal = planeNormal.normalized;
			
			Plane plane = new Plane(planeNormal, planePoint);
			float distance = plane.GetDistanceToPoint(point);
			
			return point - (distance*planeNormal);
		}

		//from kOS
		public static Vector3d GetTorque(Vessel vessel, float thrust)
		{
			var centerOfMass = vessel.findWorldCenterOfMass();
			var rollaxis = vessel.ReferenceTransform.up;
			rollaxis.Normalize ();
			var pitchaxis = vessel.GetFwdVector ();
			pitchaxis.Normalize ();
			
			float pitch = 0.0f;
			float yaw = 0.0f;
			float roll = 0.0f;

			//reaction wheel torque
			foreach(ModuleReactionWheel wheel in vessel.FindPartModulesImplementing<ModuleReactionWheel>())
			{
				if (wheel == null) continue;
				
				pitch += wheel.PitchTorque;
				yaw += wheel.YawTorque;
				roll += wheel.RollTorque;
			}

			//rcs torque
			if (vessel.ActionGroups [KSPActionGroup.RCS])
			{
				foreach(ModuleRCS rcs in vessel.FindPartModulesImplementing<ModuleRCS>())
				{
					if (rcs == null || !rcs.rcsEnabled) continue;

					var relCoM = rcs.part.Rigidbody.worldCenterOfMass - centerOfMass;

					bool enoughfuel = rcs.propellants.All(p => (int) (p.totalResourceAvailable) != 0);
					if (!enoughfuel) continue;
					foreach (Transform thrustdir in rcs.thrusterTransforms)
					{
						float rcsthrust = rcs.thrusterPower;
						//just counting positive contributions in one direction. This is incorrect for asymmetric thruster placements.
						roll += Mathf.Max(rcsthrust * Vector3.Dot(Vector3.Cross(relCoM, thrustdir.up), rollaxis), 0.0f);
						pitch += Mathf.Max(rcsthrust * Vector3.Dot(Vector3.Cross(Vector3.Cross(relCoM, thrustdir.up), rollaxis), pitchaxis), 0.0f);
						yaw += Mathf.Max(rcsthrust * Vector3.Dot(Vector3.Cross(Vector3.Cross(relCoM, thrustdir.up), rollaxis), Vector3.Cross(rollaxis,pitchaxis)),0.0f);
					}
				}
			}
			
			return new Vector3d(pitch, roll, yaw);
		}
		
		public static double GetThrustTorque(Part p, Vessel vessel)
		{
			//TODO: implement gimbalthrust Torque calculation
			return 0;
		}

		public static Vector3d GetEffectiveInertia(Vessel vessel, Vector3d torque)
		{
			var centerOfMass = vessel.findWorldCenterOfMass();
			var momentOfInertia = vessel.findLocalMOI(centerOfMass);
			var angularVelocity = Quaternion.Inverse(vessel.ReferenceTransform.rotation) * vessel.rigidbody.angularVelocity;
			var angularMomentum = new Vector3d(angularVelocity.x * momentOfInertia.x, angularVelocity.y * momentOfInertia.y, angularVelocity.z * momentOfInertia.z);
			
			var retVar = Vector3d.Scale
				(
					Sign(angularMomentum) * 2.0f,
					Vector3d.Scale(Pow(angularMomentum, 2), Inverse(Vector3d.Scale(torque, momentOfInertia)))
					);
			
			retVar.y *= 10;
			
			return retVar;
		}

		public static Vector3d Sign(Vector3d vector)
		{
			return new Vector3d(Math.Sign(vector.x), Math.Sign(vector.y), Math.Sign(vector.z));
		}

		public static Vector3d Inverse(Vector3d input)
		{
			return new Vector3d(1 / input.x, 1 / input.y, 1 / input.z);
		}

		public static Vector3d Pow(Vector3d vector, float exponent)
		{
			return new Vector3d(Math.Pow(vector.x, exponent), Math.Pow(vector.y, exponent), Math.Pow(vector.z, exponent));
		}

		public static Vector3d Abs(Vector3d vector)
		{
			return new Vector3d(Math.Abs(vector.x), Math.Abs(vector.y), Math.Abs(vector.z));
		}

		public static Vector3d ClampAxes(Vector3d vector, double min, double max)
		{
			return new Vector3d(Clamp(vector.x, min, max), Clamp(vector.y, min, max), Clamp(vector.z, min, max));
		}

		public static double Clamp(double value, double min, double max)
		{
			if(value < min)
			{
				return min;
			}
			if(value > max)
			{
				return max;
			}
			return value;
		}

		//refreshes part action window
		public static void RefreshAssociatedWindows(Part part)
		{
			foreach ( UIPartActionWindow window in GameObject.FindObjectsOfType( typeof( UIPartActionWindow ) ) ) 
			{
				if ( window.part == part )
				{
					window.displayDirty = true;
				}
			}
		}
	}




}

