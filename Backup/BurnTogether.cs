using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BurnTogether
{
	public class BurnTogether : PartModule
	{
		public bool isLeader = false;
		public bool isFollowing = false;
		public bool hasLeader = false;
		//public float throttleLimitFactor = 1;
		public bool roverMode = false;

		/*
		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Atmo Mode"), 
		 UI_Toggle(disabledText = "Off", enabledText = "On")]
		 */
		public bool atmosphericMode = false;

		Dictionary<Vessel, Vector3> warpFollowers; //follower vessel, follower relative position
		public List<BurnTogether> followers = new List<BurnTogether>();
		
		
		public float followerThrottle;
		float throttleLimit = 1;
		private BurnTogether leader;
		private AnimationState[] indicatorStates;
		private bool beginWarp = true;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string statusGui = "Off";
		[KSPField(isPersistant = false, guiActive = true, guiName = "AG Mimic")]
		public bool mimicAG = false;


		string debugString = string.Empty;

		double prevYawAngle;
		double yawAngVel;
		double prevPitchAngle;
		double pitchAngVel;
		double prevRollAngle;
		double rollAngVel;


		[KSPField(isPersistant = true, guiActive = true, guiName = "Damper")]
		public string damperDebug;
		//[KSPField(isPersistant = true, guiActive = true, guiName = "CA")]
		//public string caDebug;
		//[KSPField(isPersistant = true, guiActive = true, guiName = "MoI")]
		//public string moiDebug;

		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Pitch Damper"),
		 UI_FloatRange(minValue = 0, maxValue = 800, stepIncrement = 1f, scene = UI_Scene.All)]
		public float cPitchDamper = 500;
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Roll Damper"),
		 UI_FloatRange(minValue = 0, maxValue = 800, stepIncrement = 1f, scene = UI_Scene.All)]
		public float cRollDamper = 350;
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Yaw Damper"),
		 UI_FloatRange(minValue = 0, maxValue = 800, stepIncrement = 1f, scene = UI_Scene.All)]
		public float cYawDamper = 500;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Custom "), 
		 UI_Toggle(disabledText = "Damping Off", enabledText = "Damping On")]
		public bool customDamping = false;
		bool displayingDamper = true;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Overdrive"), 
		 UI_Toggle(disabledText = "Off", enabledText = "On")]
		public bool torqueOverdrive = false;

		public LineRenderer foof; //debug
        
		#region GUIButtons
		//===============GUI Buttons===================
		[KSPEvent(guiActive = true, guiName = "Set as Leader")]
		public void SetAsLeader()
		{
			SetOff ();
			//Events["ToggleAGM"].active = true;
			isLeader = true;
			ScreenMessages.PostScreenMessage(this.vessel.vesselName+" set as leader", 5, ScreenMessageStyle.UPPER_CENTER);
			statusGui = "Leading";
			
			this.vessel.OnFlyByWire += new FlightInputCallback(LimitLeaderThrottle);
			
			if(indicatorStates.Length > 0)
			{
				foreach(AnimationState anim in indicatorStates)
				{
					anim.normalizedTime = 0.5f;
				}
			}
		}
		
		
		[KSPEvent(guiActive = true, guiName = "Set as Follower")]
		public void SetAsFollower()
		{
			SetOff ();
			isFollowing = true;
			this.vessel.ActionGroups.groups[3] = true; //enable rcs
			this.vessel.ActionGroups.groups[4] = false; //disable sas

			foreach(Vessel v in FlightGlobals.Vessels)
			{
				if(!v.packed)
				{
					Debug.Log ("Checking if leader: "+v.vesselName);
					foreach(BurnTogether pp in v.FindPartModulesImplementing<BurnTogether>())
					{
						//Debug.Log ("Found BurnTogether module");
						if(pp.isLeader)
						{
							leader = pp;
							hasLeader = true;
							//Debug.Log ("Found Leader.");
							if(vessel == FlightGlobals.ActiveVessel)
							{
								ScreenMessages.PostScreenMessage("Following "+leader.vessel.vesselName, 5, ScreenMessageStyle.UPPER_CENTER);
							}
							statusGui = "Following "+leader.vessel.vesselName;
							if(indicatorStates.Length > 0)
							{
								foreach(AnimationState anim in indicatorStates)
								{
									anim.normalizedTime = 1;
								}
							}
							
							if(!pp.followers.Contains(this))
							{
								pp.followers.Add(this);
							}

							//copy rotation
							vessel.OnFlyByWire += new FlightInputCallback(FollowLeader);

							//RCS kill relative v code
							this.vessel.OnFlyByWire += new FlightInputCallback(RCSKillVelocity);
							
							break;
						}
					}
				}
			}
			

			if(!hasLeader)//if leader not found
			{
				Debug.Log ("Could not find leader.");
				ScreenMessages.PostScreenMessage("Could not find a leader.", 5, ScreenMessageStyle.UPPER_CENTER);
				SetOff ();
			}
		}
		
		
		[KSPEvent(guiActive = true, guiName = "All Follow Me")]
		public void AllFollow()
		{
			if(isFollowing && leader != null)  //turn off other leader bt first
			{
				leader.SetOff();
			}
			
			SetAsLeader ();
			foreach(Vessel v in FlightGlobals.Vessels)
			{
				if(!v.packed)
				{
					foreach(BurnTogether pp in v.FindPartModulesImplementing<BurnTogether>())
					{
						if(!v.Equals(this.vessel))
						{
							pp.SetAsFollower();
							ScreenMessages.PostScreenMessage("Acquiring Follower: "+v.vesselName);
						}
					}
				}
			}
		}
		
		
		[KSPEvent(guiActive = true, guiName = "BT Off")]
		public void SetOff()
		{
			if(indicatorStates.Length > 0)
			{
				foreach(AnimationState anim in indicatorStates)
				{
					anim.normalizedTime = 0;
				}
			}
			if(isFollowing)
			{


				vessel.OnFlyByWire -= new FlightInputCallback(FollowLeader);
				this.vessel.OnFlyByWire -= new FlightInputCallback(RCSKillVelocity);
				if(roverMode)
				{
					this.vessel.OnFlyByWire -= new FlightInputCallback(RoverControl);
					roverMode = false;
				}
				if(atmosphericMode)
				{
					Debug.Log ("atmosphericMode disabled");
					atmosphericMode = false;
				}

				//reset sas
				vessel.Autopilot.SAS.DisconnectFlyByWire();

				this.vessel.ActionGroups.groups[4] = true; 
			}
			if(isLeader)
			{
				ScreenMessages.PostScreenMessage("Releasing Followers", 5, ScreenMessageStyle.UPPER_CENTER);
			
				this.vessel.OnFlyByWire -= new FlightInputCallback(LimitLeaderThrottle);
	

				foreach(BurnTogether fBt in followers)
				{
					if(fBt && fBt.isFollowing)
					{
						fBt.SetOff();
						ScreenMessages.PostScreenMessage("Releasing Follower: "+fBt.vessel.vesselName);
					}
				}

				followers.Clear();
			}
			//Events["ToggleAGM"].active = false;
			mimicAG = false;
			isLeader = false;
			isFollowing = false;
			hasLeader = false;
			leader = null;
			statusGui = "Off";
			//throttleLimitFactor = 1;
		}
		
		
		[KSPEvent (guiActive = true, guiName = "Toggle AG Mimic", active = true)]
		public void ToggleAGM()
		{
			mimicAG = !mimicAG;	
		}
		#endregion

		#region ActionGroups
		//=============Action Groups===================
		
		[KSPAction("Set as Leader")]
		public void AGSetAsLeader(KSPActionParam param)
		{
			SetAsLeader();
		}
		
		[KSPAction("Set as Follower")]
		public void AGSetAsFollower(KSPActionParam param)
		{
			SetAsFollower();
		}
		
		[KSPAction("All Follow Me")]
		public void AGAllFollowMe(KSPActionParam param)
		{
			AllFollow();
		}
		
		[KSPAction("Off")]
		public void AGOff(KSPActionParam param)
		{
			SetOff();
		}
		
		
		
		[KSPAction("AG1 Command", KSPActionGroup.Custom01)]
		public void FireAG1(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether fBt in followers)
				{
					if(fBt && fBt.isFollowing)
					fBt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom01);	
				}
			}
		}
		
		
		[KSPAction("AG2 Command", KSPActionGroup.Custom02)]
		public void FireAG2(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom02);
					}
				}
			}
		}
		
		[KSPAction("AG3 Command", KSPActionGroup.Custom03)]
		public void FireAG3(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom03);	
					}
				}

			}
		}
		
		[KSPAction("AG4 Command", KSPActionGroup.Custom04)]
		public void FireAG4(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{

				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom04);	
					}
				}

			}
		}
		
		[KSPAction("AG5 Command", KSPActionGroup.Custom05)]
		public void FireAG5(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom05);	
					}
				}
			}
		}
		
		[KSPAction("AG6 Command", KSPActionGroup.Custom06)]
		public void FireAG6(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom06);	
					}
				}
			}
		}
		
		[KSPAction("AG7 Command", KSPActionGroup.Custom07)]
		public void FireAG7(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom07);	
					}
				}
			}
		}
		
		[KSPAction("AG8 Command", KSPActionGroup.Custom08)]
		public void FireAG8(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom08);	
					}
				}
			}
		}
		
		[KSPAction("AG9 Command", KSPActionGroup.Custom09)]
		public void FireAG9(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom09);	
					}
				}
			}
		}
		
		[KSPAction("AG10 Command", KSPActionGroup.Custom10)]
		public void FireAG10(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom10);	
					}
				}
			}
		}
		
		[KSPAction("Abort Command", KSPActionGroup.Abort)]
		public void FireAbort(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt && bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Abort);	
					}
				}
			}
		}
		
		
		#endregion
		
		//===========PartModule Overrides==============
		
		
		public override void OnStart(PartModule.StartState state)
		{
			//Debug.Log("BT Start");
			
			indicatorStates = Utils.SetUpAnimation ("indicatorLight", this.part);
			
			
			SetOff ();
			
			/*
			foof = gameObject.AddComponent<LineRenderer>(); //debug
			foof.SetWidth(0.5f, 0.1f);
			foof.SetVertexCount(6);
			*/
			
			part.OnJustAboutToBeDestroyed += new Callback(SetOff);


		}
		
		
		public override void OnUpdate()
		{
			ShowHideCustomDamper();

			if(HighLogic.LoadedSceneIsFlight)
			{
				/*
				if(!vessel.IsControllable && TimeWarp.CurrentRate == 1) //turn off when vessel is uncontrollable
				{
					SetOff ();
				}
				*/


				if(isLeader)
				{
					MoveWarpFollowers();
				}
				
				
				else if(isFollowing && hasLeader && leader!=null && !vessel.packed) //following leader
				{
					if(TimeWarp.WarpMode == TimeWarp.Modes.LOW || TimeWarp.CurrentRate == 1)
					{
						/*
						if((leader.vessel.GetWorldPos3D()-vessel.GetWorldPos3D()).sqrMagnitude > Vessel.unloadDistance*Vessel.unloadDistance)
						{
							SetOff ();
						}
						*/


						mimicAG = leader.mimicAG;	//activate/deactivate mimicAG on followers

						if(vessel.checkLanded())  //===================================================================Rover Mode===============
						{
							if(!roverMode)
							{
								Debug.Log ("roverMode enabled");
								roverMode = true;
								this.vessel.OnFlyByWire += new FlightInputCallback(RoverControl);
								if(atmosphericMode)
								{
									Debug.Log ("atmosphericMode disabled");
									atmosphericMode = false;
								}
							}
						}
						else
						{
							if(roverMode)
							{
								Debug.Log ("roverMode disabled");
								roverMode = false;
								this.vessel.OnFlyByWire -= new FlightInputCallback(RoverControl);
							}
						}
						
						//action group mimic -- Togglables(gear, lights, rover brakes) handled here.  The rest are in the ActiongGroups section.
						if(mimicAG)
						{
							if(leader.vessel.ActionGroups.groups[1])
							{
								this.vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
							}
							else
							{
								this.vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false);
							}
							
							if(leader.vessel.ActionGroups.groups[2])
							{
								this.vessel.ActionGroups.SetGroup(KSPActionGroup.Light, true);
							}
							else
							{
								this.vessel.ActionGroups.SetGroup(KSPActionGroup.Light, false);
							}
						}
						if(mimicAG || roverMode)  //brake toggles mimiced only in rover mode or agmimic.
						{
							if(leader.vessel.ActionGroups.groups[5])
							{
								this.vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
							}
							else
							{
								this.vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);
							}
						}

					}
				}
			}
		}

		void FixedUpdate()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(isFollowing && hasLeader && leader!=null)
				{


					//roll
					Vector3d referenceForwardRoll = vessel.ReferenceTransform.forward;
					Vector3d referenceRightRoll = vessel.ReferenceTransform.right;
					
					Vector3d leaderDirectionRoll = Utils.ProjectOnPlane(leader.vessel.ReferenceTransform.forward, Vector3d.zero, vessel.ReferenceTransform.up);
					double angleRoll = Vector3d.Angle(leaderDirectionRoll, referenceForwardRoll);
					double signRoll = -Math.Sign(Vector3d.Dot (leaderDirectionRoll, referenceRightRoll));
					double finalAngleRoll = signRoll*angleRoll;
					
					rollAngVel = (finalAngleRoll-prevRollAngle)*Time.fixedDeltaTime;
					prevRollAngle = finalAngleRoll;

					
					
					Vector3 referenceForward = vessel.ReferenceTransform.up;

					//yaw
					Vector3d referenceRightYaw = vessel.ReferenceTransform.right;
					Vector3d leaderDirectionYaw = Utils.ProjectOnPlane(leader.vessel.ReferenceTransform.up, Vector3d.zero, vessel.ReferenceTransform.forward);
					double angleYaw = Vector3d.Angle(leaderDirectionYaw, referenceForward);
					double signYaw = Math.Sign (Vector3d.Dot (leaderDirectionYaw, referenceRightYaw));
					double finalAngleYaw = signYaw*angleYaw;


					yawAngVel = (finalAngleYaw-prevYawAngle)*Time.fixedDeltaTime;
					prevYawAngle = finalAngleYaw;

					
					//pitch
					Vector3d referenceRightPitch = -vessel.ReferenceTransform.forward;
					Vector3d leaderDirectionPitch = Utils.ProjectOnPlane(leader.vessel.ReferenceTransform.up, Vector3d.zero, vessel.ReferenceTransform.right);
					double anglePitch = Vector3d.Angle(leaderDirectionPitch, referenceForward);
					double signPitch = Math.Sign (Vector3d.Dot (leaderDirectionPitch, referenceRightPitch));
					double finalAnglePitch = signPitch*anglePitch;
					
					pitchAngVel = (finalAnglePitch-prevPitchAngle)*Time.fixedDeltaTime;
					prevPitchAngle = finalAnglePitch;
					


				}
			}
		}
		

		public override void OnInactive()
		{
			SetOff ();	
		}

		void MoveWarpFollowers()
		{
			//leader warp handling
			if(TimeWarp.CurrentRate>1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
			{
				if(beginWarp)
				{
					beginWarp = false;

					warpFollowers = new Dictionary<Vessel, Vector3>();
					foreach(BurnTogether bt in followers)
					{
						if(bt && bt.isFollowing && (bt.vessel.obt_velocity-vessel.obt_velocity).sqrMagnitude < 0.1f && !warpFollowers.ContainsKey(bt.vessel))
						{
							warpFollowers.Add(bt.vessel, bt.vessel.transform.position-vessel.transform.position);
						}
					}

					Debug.Log ("Going into warp with "+warpFollowers.Count+" locked followers");
				}



				foreach(var wFollower in warpFollowers)
				{
					wFollower.Key.SetPosition(vessel.transform.position+wFollower.Value);
					wFollower.Key.obt_velocity = vessel.obt_velocity;
				}

			}
			else
			{

				//ending warp. realign followers
				if(!beginWarp)
				{
					foreach(var wFollower in warpFollowers)
					{
						Vector3d newPosition = vessel.transform.position+wFollower.Value;

						wFollower.Key.SetPosition(vessel.transform.position+wFollower.Value);
						wFollower.Key.obt_velocity = vessel.obt_velocity;
						wFollower.Key.SetWorldVelocity(vessel.obt_velocity);

						wFollower.Key.orbit.UpdateFromStateVectors(newPosition.xzy, vessel.obt_velocity.xzy, vessel.mainBody, Planetarium.GetUniversalTime());
					}

					Debug.Log ("Coming out of warp with "+warpFollowers.Count+" locked followers");
				}

				beginWarp = true;

			}
			//end leader warp handling
		}
		
		
		//=======Flight Inputs============

		//prototype
		public void FollowLeader(FlightCtrlState s)
		{
			if(leader!=null && s!=null)
			{
				double maxControl = torqueOverdrive ? 1.5 : 1.0;
				Vector3d damper = Vector3d.zero;
				var centerOfMass = vessel.findWorldCenterOfMass();
				Vector3 momentOfInertia = vessel.findLocalMOI(centerOfMass);

				//automatic damping (needs improvement)
				if(!customDamping)
				{
					Vector3d torque = Utils.GetTorque(vessel, 0);
					var effectiveInertia = Utils.GetEffectiveInertia(vessel, torque);
					Vector3d controlAuthority = Vector3d.Scale(torque, Utils.Inverse(momentOfInertia));
					damper = 4500 * Utils.Inverse(Utils.Abs(controlAuthority)+Vector3d.one);

					//test: increased roll damping
					damper = Vector3d.Scale(damper, new Vector3d(1, 1.2, 1));

					damper = Utils.ClampAxes(damper, 180, 750);
				}
				else
				{
					damper = new Vector3(cPitchDamper, cRollDamper, cYawDamper);
				}

				damperDebug = ((float)damper.x).ToString("0")+", "+((float)damper.y).ToString("0")+", "+((float)damper.z).ToString("0");

				Vector3d steerMult = Vector3d.one;
				if(atmosphericMode)
				{
					steerMult *= 1.5;
					damper = new Vector3d(650,350,650);
				}

				double damperPitch = Math.Abs(damper.x);
				double damperRoll = Math.Abs(damper.y);
				double damperYaw = Math.Abs(damper.z);

				float pitch = (float)Utils.Clamp((steerMult.x*prevPitchAngle)+(damperPitch*pitchAngVel), -maxControl, maxControl);
				float roll = (float)Utils.Clamp((steerMult.y*prevRollAngle)+(damperRoll*rollAngVel), -maxControl, maxControl);
				float yaw = (float)Utils.Clamp((steerMult.z*prevYawAngle)+(damperYaw*yawAngVel), -maxControl, maxControl);


				//limit angular momentum
				Vector3d localAngMomentum = Vector3d.Scale(momentOfInertia, new Vector3d(pitchAngVel, rollAngVel, yawAngVel));
				double maxAngMomentum = .075f;
				if((int)Mathf.Sign(pitch) != Math.Sign(pitchAngVel) && Math.Abs(localAngMomentum.x) > maxAngMomentum)
				{
					pitch = 0;
				}
				if((int)Mathf.Sign(roll) != Math.Sign(rollAngVel) && Math.Abs(localAngMomentum.y) > maxAngMomentum)
				{
					roll = 0;
				}
				if((int)Mathf.Sign(yaw) != Math.Sign(yawAngVel) && Math.Abs(localAngMomentum.z) > maxAngMomentum)
				{
					yaw = 0;
				}

				//finally set control state inputs
				s.pitch = pitch;
				s.roll = roll;
				s.yaw = yaw;

				s.mainThrottle = followerThrottle;
			}
		}
		
		public void RCSKillVelocity(FlightCtrlState s)
		{
			if(leader!=null && s!=null)
			{
				Vector3 killVector = leader.vessel.GetObtVelocity() - this.vessel.GetObtVelocity();
				Quaternion rotAdjust = Quaternion.Inverse (vessel.ReferenceTransform.rotation); //changed from vessel transform to reference transform

				killVector = rotAdjust * killVector;
				
				float rcsFactor;
				//rcsFactor = 1/(2*this.vessel.GetTotalMass());
				rcsFactor = 2;
				
				s.X = Mathf.Clamp (-rcsFactor*killVector.x, -1, 1);
				s.Y = Mathf.Clamp (-rcsFactor*killVector.z, -1, 1);
				s.Z = Mathf.Clamp (-rcsFactor*killVector.y, -1, 1);
			}
		}
			
		
		public void RoverControl(FlightCtrlState s)
		{
			Vector3 killVector = leader.vessel.GetObtVelocity() - this.vessel.GetObtVelocity();
			Quaternion rotAdjust = Quaternion.Inverse (vessel.ReferenceTransform.rotation);
			
			killVector = rotAdjust * killVector;
			
			float wheelThrottleFactor = 7;
			float wheelSteerFactor = 0.3f;
			
			if(Mathf.Abs (killVector.y) > Mathf.Abs (killVector.z))
			{
				s.wheelThrottle = Mathf.Clamp (wheelThrottleFactor*killVector.y, -1, 1);
			}
			else
			{
				s.wheelThrottle = Mathf.Clamp (wheelThrottleFactor*killVector.z, -1, 1);
			}
			s.wheelSteer = Mathf.Clamp(-wheelSteerFactor*killVector.x, -1, 1);

		}
		
		
		
		public void LimitLeaderThrottle(FlightCtrlState s) 
		{
			float TWR = GetThrustToWeight(vessel);
			float newThrottleLimit = 1;
			foreach(var follower in followers)
			{
				if(follower && follower.isFollowing && follower.leader == this)
				{
					float fTWR = GetThrustToWeight(follower.vessel);

					float throttleFactor = fTWR/TWR;
					if(throttleFactor < throttleLimit)
					{
						throttleLimit = throttleFactor;
					}

					if(throttleFactor < newThrottleLimit)
					{
						newThrottleLimit = throttleFactor;
					}

					//limit throttle if follower twr is lower
					if(this.vessel.ctrlState.mainThrottle>throttleLimit)
					{
						s.mainThrottle = throttleLimit-0.01f;
						vessel.ctrlState.mainThrottle = throttleLimit-0.01f;
					}

					if(vessel.ctrlState.mainThrottle > 0 && GetFinalThrustToWeight(vessel) > 0)
					{
						follower.followerThrottle = Mathf.Clamp01(vessel.ctrlState.mainThrottle * (TWR/fTWR));
					}
					else
					{
						follower.followerThrottle = 0;
					}
				}
			}

			//set new throttle limit if followers gain higher twr
			if(newThrottleLimit > throttleLimit)
			{
				throttleLimit = newThrottleLimit;
			}

		}

	
		
		//Utils
		
		private float GetThrustToWeight(Vessel v)
		{
			float totalThrust = 0;
			float vesselMass = v.GetTotalMass();
			foreach(Part p in v.parts)
			{
				foreach (ModuleEngines me in p.FindModulesImplementing<ModuleEngines>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += (me.maxThrust * me.thrustPercentage * 100);
					}
				}
				
				foreach (ModuleEnginesFX me in p.FindModulesImplementing<ModuleEnginesFX>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += (me.maxThrust * me.thrustPercentage * 100);
					}
				}
			}
			return totalThrust/vesselMass;
		}
		
		
		private float GetFinalThrustToWeight(Vessel v)
		{
			float totalThrust = 0;
			float vesselMass = v.GetTotalMass();
			foreach(Part p in v.parts)
			{
				foreach (ModuleEngines me in p.FindModulesImplementing<ModuleEngines>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += me.finalThrust;
					}
				}
				foreach (ModuleEnginesFX me in p.FindModulesImplementing<ModuleEnginesFX>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += me.finalThrust;
					}
				}
			}
			return totalThrust/vesselMass;
			
		}

		void ShowHideCustomDamper()
		{
			if(customDamping && !displayingDamper)
			{
				displayingDamper = true;
				Fields["cPitchDamper"].guiActive = true;
				Fields["cPitchDamper"].guiActiveEditor = true;
				Fields["cRollDamper"].guiActive = true;
				Fields["cRollDamper"].guiActiveEditor = true;
				Fields["cYawDamper"].guiActive = true;
				Fields["cYawDamper"].guiActiveEditor = true;
				Utils.RefreshAssociatedWindows(part);
			}
			else if(!customDamping && displayingDamper)
			{
				displayingDamper = false;
				Fields["cPitchDamper"].guiActive = false;
				Fields["cPitchDamper"].guiActiveEditor = false;
				Fields["cRollDamper"].guiActive = false;
				Fields["cRollDamper"].guiActiveEditor = false;
				Fields["cYawDamper"].guiActive = false;
				Fields["cYawDamper"].guiActiveEditor = false;
				Utils.RefreshAssociatedWindows(part);
			}
		}


	}
}

