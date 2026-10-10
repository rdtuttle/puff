using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using TabletopMayhem.Core;
using CommonUsages=UnityEngine.XR.CommonUsages;
using XRDevice=UnityEngine.XR.InputDevice;
namespace TabletopMayhem
{
    public sealed class PetGame : MonoBehaviour
    {
        const bool SpawnStabilityMode=true;
        const int FoodLayer=9,PetLayer=10;
        readonly Transform[] foods=new Transform[5];
        readonly Vector3[] foodHomes=new Vector3[5];
        readonly bool[] foodFlying=new bool[5];
        readonly Vector3[] foodVelocity=new Vector3[5];
        readonly float[] foodFlightTime=new float[5];
        readonly Transform[] props=new Transform[5];
        readonly bool[] propFlying=new bool[5];
        readonly Vector3[] propVelocity=new Vector3[5];
        readonly float[] propFlightTime=new float[5];
        readonly float[] propClosest=new float[5];
        readonly List<Projectile> projectiles=new List<Projectile>();
        readonly List<Spark> sparks=new List<Spark>();
        sealed class Spark{public Transform T;public Vector3 Velocity;public float Life;}
        sealed class Projectile{public Transform T;public Vector3 Velocity;public float Life;}
        PetSimulation pet;
        PetCareProgress care;
        PetRoster roster;
        RoomWorld room;
        Transform rig,cameraOffset,rightHand,leftHand,anchor,petRoot,visualPivot,petModel,microphone,packTray,backpack,spawnMarker,spawnBeacon,bondFlower,contactShadow;
        Camera view;
        PetMotion motion;
        PetHud hud;
        PetTitleScreen titleScreen;
        PuffFight fight;
        PuffHomeAnchor homeAnchors;
        PetHand rightVirtualHand,leftVirtualHand;
        TextMesh help,sleepLabel,notice;
        LineRenderer aimLine,grenadeArc;
        AudioSource songSource,squeakSource,moodSource,musicSource;
        AudioClip squeak,moodClip,musicClip,battleMusicClip,stepClip,purrClip;
        bool onQuest,placed,previousTrigger,previousGrip,previousLeftGrip,previousLeftTrigger,previousLeftY,previousReset,previousHelp,headTracked,focusPaused,packOpen;
        bool heldPet,petFlying,haveHandPositions,restingAtThrow;
        bool titleDismissed,titleWasPressed,awaitStartRelease;
        float titleTime;
        int selectedProfile=-1,highlightedSlot,nameStyle,previousSlotAxis;
        bool previousMenuAccept,previousMenuName;
        float replaceHold,leftYHold;
        bool leftYLongFired;
        int aimSign=1;
        int heldFood=-1;
        int heldProp=-1;
        float wanderWait=2,followWait=5,saveWait=30,targetX,targetZ,wanderX,wanderZ,heading,speed,petFlightTime,punchCooldown,pettingCooldown,moodCooldown=10,resetCooldown,pokeCooldown,toolCooldown,stepCooldown;
        float lastRightSeen=-10,lastLeftSeen=-10;
        float nextInputLog;
        float lastCandidateTime=-10,nextSpawnLog,beaconUntil;
        float noticeTime;
        float touchDwell;
        CareAction dailyGoal;
        int dailyStars,personalityStyle;
        bool dailyDone;
        string dailyGreeting;
        float rocketCharge;
        bool previousFightToggle,previousFightPunch,previousFightKick;
        Vector3 previousSwordTip;
        bool haveSwordTip;
        bool walking;
        bool deathAnnounced;
        PetAction lastAnimatedAction=PetAction.Idle;
        Vector3 candidatePoint;
        Vector3 home,petVelocity,lastRightPosition,lastLeftPosition,rightVelocity,leftVelocity;
        PetProfile ActiveProfile {get{return selectedProfile>=0?roster.Get(selectedProfile):null;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(FindFirstObjectByType<PetGame>()==null)new GameObject("Puff").AddComponent<PetGame>();}
        void Awake()
        {
            onQuest=Application.platform==RuntimePlatform.Android;Application.targetFrameRate=90;
            pet=new PetSimulation();care=new PetCareProgress();roster=new PetRoster();LoadRoster();CreateRig();
            anchor=new GameObject("Pet's scanned home").transform;anchor.position=new Vector3(0,.76f,.85f);
            room=gameObject.AddComponent<RoomWorld>();room.Initialize(rig,anchor,onQuest);
            if(onQuest)homeAnchors=new PuffHomeAnchor(rig);
            petRoot=new GameObject("Pink companion").transform;petRoot.SetParent(anchor,false);petRoot.gameObject.layer=PetLayer;
            CreateContactShadow();
            var belly=petRoot.gameObject.AddComponent<SphereCollider>();belly.center=new Vector3(0,.092f,.035f);belly.radius=.078f;
            visualPivot=new GameObject("Animation center").transform;visualPivot.SetParent(petRoot,false);visualPivot.localPosition=new Vector3(0,.102f,0);
            Transform sleepFace,mouth;petModel=PetArt.Pet(visualPivot,out sleepFace,out mouth);petModel.localPosition=new Vector3(0,-.102f,0);
            CreateBondFlower();
            motion=petRoot.gameObject.AddComponent<PetMotion>();motion.Initialize(petModel,sleepFace,mouth);
            spawnBeacon=PetArt.Shape(petRoot,PrimitiveType.Sphere,new Vector3(0,.31f,0),Vector3.one*.035f,PetArt.Gold,"Puff location beacon").transform;
            spawnBeacon.gameObject.SetActive(onQuest&&SpawnStabilityMode);
            microphone=PetArt.Microphone(visualPivot);microphone.localPosition=new Vector3(.075f,.012f,.055f);
            songSource=petRoot.gameObject.AddComponent<AudioSource>();songSource.spatialBlend=onQuest?1:0;songSource.playOnAwake=false;
            songSource.clip=Resources.Load<AudioClip>("Audio/PuffSong");if(!songSource.clip)Debug.LogError("PET_SONG_MISSING");
            squeakSource=petRoot.gameObject.AddComponent<AudioSource>();squeakSource.spatialBlend=onQuest?1:0;squeak=Squeak();
            moodSource=petRoot.gameObject.AddComponent<AudioSource>();moodSource.spatialBlend=onQuest?1:0;moodClip=Resources.Load<AudioClip>("Audio/PuffMood");
            if(!moodClip)Debug.LogError("PET_MOOD_SOUND_MISSING");
            musicSource=gameObject.AddComponent<AudioSource>();musicSource.spatialBlend=0;musicSource.playOnAwake=false;musicSource.loop=true;
            musicSource.volume=.20f;musicClip=CozyMusic();battleMusicClip=BattleMusic();musicSource.clip=musicClip;
            stepClip=SoftStep();purrClip=SleepPurr();
            hud=new PetHud(view.transform);
            help=PetArt.Text(anchor,"",new Vector3(0,.22f,.10f),.0045f,PetArt.Cream);
            sleepLabel=PetArt.Text(petRoot,"Z z z",new Vector3(0,.22f,0),.006f,PetArt.Cream);
            notice=PetArt.Text(petRoot,"",new Vector3(0,.30f,0),.0048f,PetArt.Cream);
            notice.fontStyle=FontStyle.Bold;notice.gameObject.SetActive(false);
            CreatePack();CreateFoods();
            fight=new PuffFight(anchor,petRoot,view.transform,room);
            spawnMarker=new GameObject("Scanned surface placement marker").transform;
            for(int i=0;i<12;i++)
            {float a=i*Mathf.PI*2/12;PetArt.Shape(spawnMarker,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.085f,0,Mathf.Sin(a)*.085f),new Vector3(.017f,.009f,.017f),PetArt.Gold,"Marker dot");}
            PetArt.Shape(spawnMarker,PrimitiveType.Sphere,Vector3.zero,new Vector3(.018f,.009f,.018f),PetArt.Cream,"Marker center");
            spawnMarker.gameObject.SetActive(false);
            var lineObject=new GameObject("Faint placement ray");
            aimLine=lineObject.AddComponent<LineRenderer>();aimLine.useWorldSpace=true;aimLine.positionCount=2;
            aimLine.startWidth=.006f;aimLine.endWidth=.003f;aimLine.numCapVertices=4;
            aimLine.material=new Material(Resources.Load<Shader>("PetAimLine"));
            aimLine.startColor=new Color(.70f,.89f,1,.43f);aimLine.endColor=new Color(1,.80f,.92f,.24f);
            lineObject.SetActive(false);
            var arcObject=new GameObject("Slingshot trajectory preview");
            grenadeArc=arcObject.AddComponent<LineRenderer>();grenadeArc.useWorldSpace=true;grenadeArc.positionCount=18;
            grenadeArc.startWidth=.004f;grenadeArc.endWidth=.001f;
            grenadeArc.material=new Material(Resources.Load<Shader>("PetAimLine"));
            grenadeArc.startColor=new Color(1,.82f,.25f,.55f);grenadeArc.endColor=new Color(1,.42f,.35f,.04f);
            arcObject.SetActive(false);
            if(onQuest&&SpawnStabilityMode)
            {
                petRoot.gameObject.SetActive(false);
                help.transform.SetParent(view.transform,false);help.transform.localPosition=new Vector3(0,-.16f,.7f);help.characterSize=.0025f;
            }
            if(!onQuest){placed=true;home=anchor.position;view.transform.position=new Vector3(0,1.62f,-.5f);view.transform.LookAt(anchor.position+Vector3.up*.12f);}
            titleScreen=new PetTitleScreen(view.transform);
            petRoot.gameObject.SetActive(false);rightHand.gameObject.SetActive(false);leftHand.gameObject.SetActive(false);
            help.gameObject.SetActive(false);hud.Update(pet,care,ActiveProfile,false);
            musicSource.Play();
            PositionPet();
        }
        void CreateRig()
        {
            var old=Camera.main;if(old)Destroy(old.gameObject);
            rig=new GameObject("XR Origin").transform;cameraOffset=new GameObject("Camera Offset").transform;cameraOffset.SetParent(rig,false);
            var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(cameraOffset,false);
            view=cameraObject.AddComponent<Camera>();view.nearClipPlane=.02f;view.farClipPlane=30;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=onQuest?Color.clear:new Color(.035f,.045f,.06f);
            cameraObject.AddComponent<AudioListener>();
            if(onQuest)
            {
                var origin=rig.gameObject.AddComponent<XROrigin>();origin.Camera=view;origin.CameraFloorOffsetObject=cameraOffset.gameObject;origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;
                new GameObject("AR Session",typeof(ARSession),typeof(ARInputManager));cameraObject.AddComponent<ARCameraManager>();
            }
            leftHand=new GameObject("Left controller").transform;leftHand.SetParent(cameraOffset,false);
            rightHand=new GameObject("Right controller").transform;rightHand.SetParent(cameraOffset,false);
            rightVirtualHand=new PetHand(rightHand,true);leftVirtualHand=new PetHand(leftHand,false);
            rightHand.gameObject.SetActive(onQuest);leftHand.gameObject.SetActive(onQuest);
        }
        void CreateFoods()
        {
            for(int i=0;i<foods.Length;i++)
            {
                var food=PetArt.Food(packTray,(FoodKind)i);foods[i]=food;
                foodHomes[i]=new Vector3((i-2)*.096f,.055f,-.025f);
                food.localPosition=foodHomes[i];
            }
            for(int i=0;i<props.Length;i++)
            {props[i]=PetArt.PackProp(packTray,i);props[i].localPosition=PropHome(i);}
        }
        void CreateBondFlower()
        {
            bondFlower=new GameObject("Bond flower accessory").transform;
            bondFlower.SetParent(visualPivot,false);
            bondFlower.localPosition=new Vector3(-.074f,.098f,.042f);
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.PI*2/5;
                PetArt.Shape(bondFlower,PrimitiveType.Sphere,
                    new Vector3(Mathf.Sin(angle)*.012f,Mathf.Cos(angle)*.012f,0),
                    new Vector3(.015f,.015f,.009f),i%2==0?PetArt.Pink:PetArt.Cream,"Bond flower petal");
            }
            PetArt.Shape(bondFlower,PrimitiveType.Sphere,new Vector3(0,0,-.006f),new Vector3(.010f,.010f,.008f),PetArt.Gold,"Bond flower center");
            bondFlower.gameObject.SetActive(care.BondLevel>=2);
        }
        void CreateContactShadow()
        {
            contactShadow=new GameObject("Soft surface contact shadow").transform;
            contactShadow.SetParent(anchor,false);
            const int segments=28;var vertices=new Vector3[segments+1];var colors=new Color[segments+1];var triangles=new int[segments*3];
            vertices[0]=Vector3.zero;colors[0]=Color.white;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                vertices[i+1]=new Vector3(Mathf.Sin(a)*.14f,0,Mathf.Cos(a)*.11f);
                colors[i+1]=new Color(1,1,1,0);
                triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%segments+1;
            }
            var mesh=new Mesh{vertices=vertices,colors=colors,triangles=triangles};mesh.RecalculateNormals();
            contactShadow.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Resources.Load<Shader>("PetShadow"));
            material.color=new Color(.12f,.08f,.17f,.25f);
            contactShadow.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            contactShadow.gameObject.SetActive(false);
        }
        void UpdateContactShadow()
        {
            if(!contactShadow)return;
            bool show=placed&&petRoot.gameObject.activeInHierarchy&&!heldPet;
            contactShadow.gameObject.SetActive(show);
            if(!show)return;
            float floor;if(!room.Ground(petRoot.localPosition.x,petRoot.localPosition.z,petRoot.localPosition.y+.4f,out floor))
                floor=rig.position.y-anchor.position.y;
            contactShadow.localPosition=new Vector3(petRoot.localPosition.x,floor+.004f,petRoot.localPosition.z);
            float height=Mathf.Max(0,petRoot.localPosition.y-floor);
            float size=1+Mathf.Min(height,1)*.4f;
            contactShadow.localScale=new Vector3(size,1,size);
        }
        void CreatePack()
        {
            backpack=new GameObject("Backpack behind head").transform;backpack.SetParent(view.transform,false);
            backpack.localPosition=new Vector3(0,-.17f,-.30f);
            PetArt.Shape(backpack,PrimitiveType.Cube,Vector3.zero,new Vector3(.23f,.24f,.10f),new Color(.79f,.48f,.76f),"Pastel backpack");
            PetArt.Shape(backpack,PrimitiveType.Cube,new Vector3(0,.02f,-.056f),new Vector3(.18f,.11f,.016f),PetArt.Cream,"Pack flap");
            packTray=new GameObject("Backpack inventory tray").transform;packTray.SetParent(view.transform,false);
            packTray.localPosition=new Vector3(.29f,-.20f,.65f);
            packTray.localScale=Vector3.one*.72f;
            PetArt.Shape(packTray,PrimitiveType.Cube,new Vector3(0,0,.022f),new Vector3(.63f,.22f,.018f),new Color(.80f,.55f,.78f),"Inventory backing");
            PetArt.Shape(packTray,PrimitiveType.Cube,new Vector3(0,0,.009f),new Vector3(.61f,.20f,.008f),PetArt.Cream,"Inventory lining");
            PetArt.Text(packTray,"SNACKS  +  TOYS  +  FIGHT",new Vector3(0,.093f,-.008f),.0026f,PetArt.Ink);
            string[] labels={"DONUT","BURGER","HOT DOG","COOKIE","WATER","MIC","GUN","SWORD","BOMB","ROCKET"};
            for(int i=0;i<foods.Length;i++)PetArt.Text(packTray,labels[i],new Vector3((i-2)*.096f,.012f,-.015f),.0018f,PetArt.Ink);
            for(int i=0;i<props.Length;i++)PetArt.Text(packTray,labels[i+5],new Vector3((i-2)*.115f,-.105f,-.015f),.0019f,PetArt.Ink);
            packTray.gameObject.SetActive(false);
        }
        void Update()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(!titleDismissed)
            {
                titleTime+=dt;titleScreen.Update(dt);
                if(awaitStartRelease)
                {
                    if(AnyStartButtonHeld())return;
                    awaitStartRelease=false;return;
                }
                if(titleScreen.InRoster){ReadRosterInput(dt);return;}
                bool pressed=AnyStartButtonHeld();
                if(titleTime>.65f&&pressed&&!titleWasPressed)
                {titleScreen.ShowRoster(roster,highlightedSlot,nameStyle);awaitStartRelease=true;Debug.Log("PET_ROSTER_OPEN");}
                titleWasPressed=pressed;return;
            }
            if(awaitStartRelease)
            {
                if(AnyStartButtonHeld())return;
                awaitStartRelease=false;return;
            }
            if(onQuest)ReadQuest();else ReadDesktop();
            if(focusPaused)return;
            if(onQuest&&SpawnStabilityMode&&!placed)
            {
                pet.AdvanceLife(dt);
                hud.Update(pet,care,ActiveProfile,false);help.gameObject.SetActive(!placed);return;
            }
            if(onQuest&&SpawnStabilityMode&&spawnBeacon.gameObject.activeSelf&&Time.unscaledTime>beaconUntil)spawnBeacon.gameObject.SetActive(false);
            if(fight.Active)pet.AdvanceLife(dt);else pet.Advance(dt);
            if(pet.IsDead&&fight.Active){fight.End();SetFightMusic(false);}
            if(placed&&!fight.Active&&!string.IsNullOrEmpty(dailyGreeting))
            {ShowNotice(dailyGreeting,pet.IsSick?PetArt.Pink:PetArt.Gold);dailyGreeting=null;}
            punchCooldown=Mathf.Max(0,punchCooldown-dt);pettingCooldown=Mathf.Max(0,pettingCooldown-dt);resetCooldown=Mathf.Max(0,resetCooldown-dt);pokeCooldown=Mathf.Max(0,pokeCooldown-dt);toolCooldown=Mathf.Max(0,toolCooldown-dt);
            if(pet.IsDead&&!deathAnnounced)
            {deathAnnounced=true;ShowNotice("GOODBYE, "+ActiveProfile.Name.ToUpperInvariant(),PetArt.Cream);Save();Debug.Log("PET_LIFE_ENDED slot="+selectedProfile+" age_days="+(pet.AgeRealSeconds/86400).ToString("F1"));}
            if(placed&&!musicSource.isPlaying)musicSource.Play();
            musicSource.volume=Mathf.Lerp(musicSource.volume,pet.Action==PetAction.Sing?.025f:fight.Active?.24f:.20f,1-Mathf.Exp(-dt*3));
            if(placed&&!fight.Active){if(petFlying)UpdatePetFlight(dt);else if(!heldPet)MovePet(dt);if(!SpawnStabilityMode)FollowIfNeeded(dt);}
            UpdateFoodFlight(dt);
            UpdatePropFlight(dt);
            UpdateProjectiles(dt);
            if(!onQuest||!SpawnStabilityMode)PositionPet();Animate(dt);fight.Tick(dt);UpdateContactShadow();UpdateSparks(dt);UpdateMood(dt);UpdateNotice(dt);UpdateHud();
            UpdateLittleSounds(dt);
            if(onQuest&&SpawnStabilityMode&&Time.unscaledTime>nextSpawnLog)
            {nextSpawnLog=Time.unscaledTime+8;Debug.Log("PET_VISIBLE active="+petRoot.gameObject.activeInHierarchy+" pos="+petRoot.position+" action="+pet.Action);}
            saveWait-=dt;if(saveWait<=0){saveWait=30;Save();}
        }
        bool AnyStartButtonHeld()
        {
            if(onQuest)
            {
                var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                return StartButton(left)||StartButton(right);
            }
            var keyboard=Keyboard.current;var mouse=Mouse.current;var pad=Gamepad.current;
            return (keyboard!=null&&keyboard.anyKey.isPressed)||(mouse!=null&&(mouse.leftButton.isPressed||mouse.rightButton.isPressed))||
                   (pad!=null&&(pad.buttonSouth.isPressed||pad.buttonEast.isPressed||pad.buttonWest.isPressed||pad.buttonNorth.isPressed||pad.leftTrigger.isPressed||pad.rightTrigger.isPressed));
        }
        static bool StartButton(XRDevice device)
        {
            float trigger,grip;
            return Button(device,CommonUsages.primaryButton)||Button(device,CommonUsages.secondaryButton)||
                   Button(device,CommonUsages.triggerButton)||Button(device,CommonUsages.gripButton)||
                   (device.TryGetFeatureValue(CommonUsages.trigger,out trigger)&&trigger>.45f)||
                   (device.TryGetFeatureValue(CommonUsages.grip,out grip)&&grip>.45f)||
                   Button(device,CommonUsages.primary2DAxisClick)||Button(device,CommonUsages.menuButton);
        }
        void DismissTitle()
        {
            if(titleDismissed)return;
            PetProfile choice=roster.Get(highlightedSlot);
            if(choice==null)
            {roster.Create(highlightedSlot,PetTitleScreen.NameForStyle(nameStyle),DateTimeOffset.UtcNow.ToUnixTimeSeconds(),nameStyle);choice=roster.Get(highlightedSlot);}
            if(choice.Dead)return;
            SelectProfile(highlightedSlot);
            titleDismissed=true;awaitStartRelease=true;titleScreen.Hide();
            petRoot.gameObject.SetActive(placed);hud.Update(pet,care,ActiveProfile,placed);
            if(onQuest&&homeAnchors!=null)homeAnchors.Restore(highlightedSlot,pose=>
            {
                if(placed||selectedProfile!=highlightedSlot)return;
                anchor.SetPositionAndRotation(pose.position,pose.rotation);
                home=anchor.position;petRoot.localPosition=Vector3.zero;
                pet.PrepareForPlacement();petRoot.gameObject.SetActive(true);placed=true;
                spawnBeacon.gameObject.SetActive(true);beaconUntil=Time.unscaledTime+3;
                ShowNotice("WELCOME HOME!",PetArt.Cream);
            });
            Debug.Log("PET_TITLE_START profile="+highlightedSlot+" name="+choice.Name);
        }
        void ReadRosterInput(float dt)
        {
            int axis=0;bool accept=false,name=false,replace=false;
            if(onQuest)
            {
                var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                Vector2 stick;
                if(left.TryGetFeatureValue(CommonUsages.primary2DAxis,out stick))axis=stick.x>.55f?1:stick.x<-.55f?-1:0;
                accept=Button(right,CommonUsages.primaryButton);
                name=Button(left,CommonUsages.primaryButton);
                replace=Button(right,CommonUsages.secondaryButton);
            }
            else
            {
                var keyboard=Keyboard.current;
                if(keyboard!=null)
                {
                    axis=keyboard.rightArrowKey.isPressed?1:keyboard.leftArrowKey.isPressed?-1:0;
                    accept=keyboard.enterKey.isPressed||keyboard.spaceKey.isPressed;
                    name=keyboard.xKey.isPressed;replace=keyboard.bKey.isPressed;
                }
            }
            if(axis!=0&&axis!=previousSlotAxis)
            {highlightedSlot=(highlightedSlot+axis+PetRoster.Capacity)%PetRoster.Capacity;replaceHold=0;titleScreen.UpdateRoster(roster,highlightedSlot,nameStyle);}
            previousSlotAxis=axis;
            if(name&&!previousMenuName&&(roster.Get(highlightedSlot)==null||roster.Get(highlightedSlot).Dead))
            {nameStyle=(nameStyle+1)%5;titleScreen.UpdateRoster(roster,highlightedSlot,nameStyle);}
            previousMenuName=name;
            if(accept&&!previousMenuAccept&&roster.Get(highlightedSlot)?.Dead!=true)DismissTitle();
            previousMenuAccept=accept;
            if(replace&&roster.Get(highlightedSlot)?.Dead==true)
            {
                replaceHold+=dt;
                if(replaceHold>=1.1f)
                {
                    roster.ReplaceDeceased(highlightedSlot,PetTitleScreen.NameForStyle(nameStyle),DateTimeOffset.UtcNow.ToUnixTimeSeconds(),nameStyle);
                    PlayerPrefs.DeleteKey(SlotKey(highlightedSlot,"DAILY_DAY"));
                    PlayerPrefs.DeleteKey(SlotKey(highlightedSlot,"DAILY_DONE"));
                    PlayerPrefs.DeleteKey(SlotKey(highlightedSlot,"DAILY_STARS"));
                    PersistRoster();replaceHold=0;titleScreen.UpdateRoster(roster,highlightedSlot,nameStyle);
                    Debug.Log("PET_NEW_LIFE slot="+highlightedSlot);
                }
            }
            else replaceHold=0;
        }
        void ReadQuest()
        {
            var head=InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            PoseDevice(head,view.transform);PoseDevice(left,leftHand);PoseDevice(right,rightHand);
            float frameDt=Mathf.Max(.008f,Mathf.Min(Time.unscaledDeltaTime,.08f));
            if(haveHandPositions)
            {rightVelocity=Vector3.Lerp(rightVelocity,(rightHand.position-lastRightPosition)/frameDt,.42f);
             leftVelocity=Vector3.Lerp(leftVelocity,(leftHand.position-lastLeftPosition)/frameDt,.42f);}
            lastRightPosition=rightHand.position;lastLeftPosition=leftHand.position;haveHandPositions=true;
            bool tracked;headTracked=head.TryGetFeatureValue(CommonUsages.isTracked,out tracked)&&tracked;
            Vector3 controllerPosition;
            bool rightTracked=right.isValid&&((right.TryGetFeatureValue(CommonUsages.isTracked,out tracked)&&tracked)||right.TryGetFeatureValue(CommonUsages.devicePosition,out controllerPosition));
            bool leftTracked=left.isValid&&((left.TryGetFeatureValue(CommonUsages.isTracked,out tracked)&&tracked)||left.TryGetFeatureValue(CommonUsages.devicePosition,out controllerPosition));
            if(rightTracked)lastRightSeen=Time.unscaledTime;if(leftTracked)lastLeftSeen=Time.unscaledTime;
            rightHand.gameObject.SetActive(Time.unscaledTime-lastRightSeen<.35f);
            leftHand.gameObject.SetActive(Time.unscaledTime-lastLeftSeen<.35f);
            if(!rightTracked)
            {
                if(onQuest&&SpawnStabilityMode&&!placed&&spawnMarker){spawnMarker.gameObject.SetActive(false);aimLine.gameObject.SetActive(false);}
                if(!placed&&Time.time>nextInputLog){nextInputLog=Time.time+5;Debug.Log("PET_INPUT_WAIT head="+headTracked+" rightDevice="+right.isValid+" surfaces="+room.SurfaceCount);}
                return;
            }
            float triggerValue;
            bool trigger=Button(right,CommonUsages.triggerButton)||(right.TryGetFeatureValue(CommonUsages.trigger,out triggerValue)&&triggerValue>.45f);
            bool grip=Button(right,CommonUsages.gripButton),leftGrip=Button(left,CommonUsages.gripButton);
            float leftTriggerValue;
            bool leftTrigger=Button(left,CommonUsages.triggerButton)||(left.TryGetFeatureValue(CommonUsages.trigger,out leftTriggerValue)&&leftTriggerValue>.45f);
            bool reset=Button(right,CommonUsages.secondaryButton);
            bool helpButton=Button(right,CommonUsages.primaryButton);
            bool leftY=Button(left,CommonUsages.secondaryButton);
            rightVirtualHand.Pose(grip,trigger,false,frameDt,aimSign);
            leftVirtualHand.Pose(leftGrip,false,leftTrigger,frameDt);
            if(onQuest&&SpawnStabilityMode&&!placed)
            {
                if(leftY)
                {
                    leftYHold+=frameDt;
                    if(leftYHold>=.85f&&!leftYLongFired)
                    {leftYLongFired=true;ReturnToRoster();previousLeftY=leftY;return;}
                }
                else {leftYHold=0;leftYLongFired=false;}
                ReadStableSpawn(trigger,helpButton,reset,leftGrip);
                previousTrigger=trigger;previousHelp=helpButton;previousReset=reset;previousLeftGrip=leftGrip;previousLeftY=leftY;previousGrip=grip;previousLeftTrigger=leftTrigger;
                return;
            }
            bool fightToggle=Button(left,CommonUsages.primaryButton);
            if(placed&&fightToggle&&!previousFightToggle)
            {
                if(fight.Active){fight.End();SetFightMusic(false);grenadeArc.gameObject.SetActive(false);ShowNotice("BACK TO COZY MODE",PetArt.Cream);}
                else if(!pet.IsDead){heldPet=petFlying=false;walking=false;if(songSource.isPlaying)songSource.Stop();pet.EnterFight();fight.Begin();SetFightMusic(true);ShowNotice("FIGHT MODE!",PetArt.Gold);}
                ClosePack();previousFightToggle=fightToggle;previousTrigger=trigger;previousGrip=grip;
                previousFightPunch=helpButton;previousFightKick=reset;previousLeftTrigger=leftTrigger;return;
            }
            previousFightToggle=fightToggle;
            if(fight.Active)
            {
                ReadFightQuest(left,trigger,grip,leftGrip,leftTrigger,helpButton,reset,frameDt);
                previousTrigger=trigger;previousGrip=grip;previousLeftGrip=leftGrip;
                previousFightPunch=helpButton;previousFightKick=reset;previousLeftTrigger=leftTrigger;return;
            }
            if(onQuest&&SpawnStabilityMode&&placed&&leftY)
            {
                leftYHold+=frameDt;
                if(leftYHold>=.85f&&!leftYLongFired)
                {leftYLongFired=true;ReturnToRoster();previousLeftY=leftY;return;}
            }
            if(onQuest&&SpawnStabilityMode&&placed&&!leftY&&previousLeftY)
            {
                if(!leftYLongFired){if(pet.IsDead)ReturnToRoster();else BeginReselect();}
                leftYHold=0;leftYLongFired=false;previousLeftY=false;previousTrigger=trigger;previousHelp=helpButton;
                previousReset=reset;previousGrip=grip;previousLeftGrip=leftGrip;previousLeftTrigger=leftTrigger;return;
            }
            trigger=trigger||(!placed&&helpButton);
            if(!placed&&Time.time>nextInputLog){nextInputLog=Time.time+5;Debug.Log("PET_INPUT_READY head="+headTracked+" right="+rightTracked+" trigger="+trigger+" surfaces="+room.SurfaceCount);}
            Ray ray=new Ray(rightHand.position,rightHand.forward*aimSign);RaycastHit surface=default(RaycastHit);
            if(!placed)
            {
                RaycastHit forwardSurface,reverseSurface;
                bool forwardValid=room.AimSurface(new Ray(rightHand.position,rightHand.forward),out forwardSurface);
                bool reverseValid=room.AimSurface(new Ray(rightHand.position,-rightHand.forward),out reverseSurface);
                bool valid=forwardValid||reverseValid;
                if(valid)
                {
                    float frontScore=forwardValid?Vector3.Dot((forwardSurface.point-view.transform.position).normalized,view.transform.forward):-2;
                    float reverseScore=reverseValid?Vector3.Dot((reverseSurface.point-view.transform.position).normalized,view.transform.forward):-2;
                    aimSign=reverseScore>frontScore?-1:1;
                    surface=aimSign==1?forwardSurface:reverseSurface;
                    ray=new Ray(rightHand.position,rightHand.forward*aimSign);
                }
                Vector3 flat=view.transform.forward;flat.y=0;flat.Normalize();
                anchor.position=valid?surface.point+Vector3.up*.012f:view.transform.position+flat*.65f-Vector3.up*.45f;
                if(flat.sqrMagnitude>.01f)anchor.rotation=Quaternion.LookRotation(flat);
                if(leftGrip&&!previousLeftGrip)room.RequestScan();
                if(trigger&&!previousTrigger&&valid){placed=true;home=anchor.position;float ground;if(room.Ground(0,0,.5f,out ground))petRoot.localPosition=new Vector3(0,ground,0);Debug.Log("PET_PLACED_ON_ROOM_SURFACE");}
                else if(trigger&&!previousTrigger)Debug.Log("PET_PLACEMENT_MISS scanned="+room.SurfaceCount+" rightForward="+rightHand.forward+" viewForward="+view.transform.forward);
            }
            else
            {
                if(helpButton&&!previousHelp)hud.ToggleControls();
                if(reset&&!previousReset&&resetCooldown<=0){resetCooldown=1;ResetPet();Debug.Log("PET_RESET");}
                if(pet.IsDead)
                {previousTrigger=trigger;previousGrip=grip;previousLeftGrip=leftGrip;previousLeftTrigger=leftTrigger;previousLeftY=leftY;previousReset=reset;previousHelp=helpButton;return;}
                if(grip&&!previousGrip)
                {
                    if(BehindHead(rightHand.position))TogglePack();
                    else if(heldFood<0&&heldProp<0&&packOpen){TryPickFood(ray);if(heldFood<0)TryPickProp(ray);}
                }
                if(heldFood>=0)
                {
                    foods[heldFood].position=rightHand.position+rightHand.forward*aimSign*.075f;
                    foods[heldFood].rotation=rightHand.rotation;
                    if(Vector3.Distance(foods[heldFood].position,MouthWorld())<.13f){FeedHeld();}
                    else if(!grip)ThrowFood();
                }
                if(heldProp>=0)
                {
                    props[heldProp].position=rightHand.position+rightHand.forward*aimSign*.085f;
                    props[heldProp].rotation=heldProp==1?Quaternion.LookRotation(ray.direction,rightHand.up):rightHand.rotation;
                    if(heldProp==2)CheckSwordSwing(frameDt);
                    if(trigger&&!previousTrigger&&toolCooldown<=0)UseHeldProp(ray);
                    if(!grip)ThrowProp();
                }
                else haveSwordTip=false;
                if(trigger&&heldProp<0&&heldFood<0&&pettingCooldown<=0)
                {
                    Vector3 headPoint=petRoot.TransformPoint(new Vector3(0,.158f,.055f));
                    float distance=Vector3.Distance(rightVirtualHand.FingerTip.position,headPoint);
                    touchDwell=distance<.13f?touchDwell+frameDt:Mathf.Max(0,touchDwell-frameDt*2);
                    if(touchDwell>.065f){touchDwell=0;PetPuff();}
                }
                else touchDwell=0;
                if(leftGrip&&!previousLeftGrip)TryPickPet();
                if(heldPet)
                {
                    petRoot.position=leftHand.position+leftHand.forward*.10f;
                    if(!leftGrip)ThrowPet();
                }
                if(leftTrigger&&!previousLeftTrigger&&!heldPet&&punchCooldown<=0&&Vector3.Distance(leftHand.position,petRoot.position+Vector3.up*.10f)<.22f)PunchPet();
                if(leftGrip&&!previousLeftGrip&&pet.Action==PetAction.Sleep)pet.Wake();
            }
            previousTrigger=trigger;previousGrip=grip;previousLeftGrip=leftGrip;previousLeftTrigger=leftTrigger;previousLeftY=leftY;previousReset=reset;previousHelp=helpButton;
        }
        static void PoseDevice(XRDevice device,Transform target)
        {Vector3 p;Quaternion q;if(device.TryGetFeatureValue(CommonUsages.devicePosition,out p))target.localPosition=p;if(device.TryGetFeatureValue(CommonUsages.deviceRotation,out q))target.localRotation=q;}
        static bool Button(XRDevice d,InputFeatureUsage<bool> feature){bool v;return d.TryGetFeatureValue(feature,out v)&&v;}
        void ReadFightQuest(XRDevice left,bool trigger,bool grip,bool leftGrip,bool leftTrigger,bool punch,bool kick,float dt)
        {
            Vector2 stick;if(!left.TryGetFeatureValue(CommonUsages.primary2DAxis,out stick))stick=Vector2.zero;
            bool guarding=trigger&&heldProp<0&&heldFood<0;
            fight.Controls(stick,guarding,leftGrip);
            if(punch&&!previousFightPunch)fight.Punch();
            if(kick&&!previousFightKick)fight.Kick();
            if(leftTrigger&&!previousLeftTrigger){fight.Jump();Haptic(.18f,.06f);}
            if(leftGrip&&!previousLeftGrip){if(fight.Grab())Haptic(.4f,.1f);}
            if(!leftGrip&&previousLeftGrip){fight.Throw(leftVelocity.sqrMagnitude>.12f?leftVelocity:leftHand.forward*1.4f);Haptic(.28f,.08f);}
            Ray ray=new Ray(rightHand.position,rightHand.forward*aimSign);
            if(grip&&!previousGrip)
            {
                if(BehindHead(rightHand.position))TogglePack();
                else if(heldProp<0&&heldFood<0&&packOpen){TryPickFood(ray);if(heldFood<0)TryPickProp(ray);}
            }
            if(heldFood>=0)
            {
                foods[heldFood].position=rightHand.position+ray.direction*.075f;
                foods[heldFood].rotation=rightHand.rotation;
                if(Vector3.Distance(foods[heldFood].position,MouthWorld())<.13f)FeedHeld();
                else if(!grip)ThrowFood();
            }
            if(heldProp>=0)
            {
                props[heldProp].position=rightHand.position+ray.direction*.085f;
                props[heldProp].rotation=rightHand.rotation;
                if(heldProp==1&&trigger&&!previousTrigger&&toolCooldown<=0)
                {toolCooldown=.34f;fight.LaunchRocket(props[1].position,ray.direction,.35f);Haptic(.2f,.07f);}
                if(heldProp==2&&trigger&&!previousTrigger&&toolCooldown<=0)
                {toolCooldown=.55f;fight.Kick();Haptic(.2f,.07f);}
                if(heldProp==4)
                {
                    if(trigger){rocketCharge=Mathf.Min(1,rocketCharge+dt/.9f);ShowGrenadeArc(props[4].position,ray.direction,rocketCharge);}
                    else if(previousTrigger&&rocketCharge>.05f)
                    {fight.LaunchRocket(props[4].position,ray.direction,rocketCharge);rocketCharge=0;grenadeArc.gameObject.SetActive(false);Haptic(.4f,.16f);}
                    else grenadeArc.gameObject.SetActive(false);
                }
                if(!grip)
                {
                    if(heldProp==3)
                    {
                        fight.ThrowBomb(props[3].position,Vector3.ClampMagnitude(rightVelocity,3)+ray.direction*.5f+Vector3.up*.2f);
                        ResetProp(3);heldProp=-1;Haptic(.3f,.1f);
                    }
                    else {int i=heldProp;heldProp=-1;ResetProp(i);}
                    rocketCharge=0;
                    grenadeArc.gameObject.SetActive(false);
                }
            }
            else grenadeArc.gameObject.SetActive(false);
        }
        void ShowGrenadeArc(Vector3 origin,Vector3 direction,float charge)
        {
            grenadeArc.gameObject.SetActive(true);
            Vector3 velocity=direction.normalized*(2.4f+3.1f*charge);
            for(int i=0;i<grenadeArc.positionCount;i++)
            {
                float t=i*.085f;
                grenadeArc.SetPosition(i,origin+velocity*t+Physics.gravity*(.06f*t*t));
            }
        }
        void ReadStableSpawn(bool trigger,bool placeButton,bool reset,bool leftGrip)
        {
            if(leftGrip&&!previousLeftGrip)room.RequestScan();
            RaycastHit forward,backward;
            bool f=room.AimSurface(new Ray(rightHand.position,rightHand.forward),out forward)&&UsableSpawnHit(forward);
            bool b=room.AimSurface(new Ray(rightHand.position,-rightHand.forward),out backward)&&UsableSpawnHit(backward);
            bool valid=f||b;
            RaycastHit chosen=default(RaycastHit);
            string source="controller";
            if(valid)
            {
                float fs=f?Vector3.Dot((forward.point-view.transform.position).normalized,view.transform.forward):-2;
                float bs=b?Vector3.Dot((backward.point-view.transform.position).normalized,view.transform.forward):-2;
                aimSign=bs>fs?-1:1;chosen=aimSign==1?forward:backward;
            }
            else
            {
                RaycastHit gaze;
                if(room.AimSurface(new Ray(view.transform.position,view.transform.forward),out gaze)&&UsableSpawnHit(gaze))
                {chosen=gaze;valid=true;source="gaze";}
                else
                {
                    Vector3 flat=view.transform.forward;flat.y=0;
                    if(flat.sqrMagnitude>.001f)
                    {
                        Vector3 above=view.transform.position+flat.normalized*.85f+Vector3.up*.20f;
                        if(room.AimSurface(new Ray(above,Vector3.down),out gaze)&&UsableSpawnHit(gaze))
                        {chosen=gaze;valid=true;source="forward floor";}
                    }
                }
            }
            if(valid)
            {
                aimSign=Vector3.Dot(rightHand.forward,chosen.point-rightHand.position)>=0?1:-1;
                candidatePoint=chosen.point;lastCandidateTime=Time.unscaledTime;
                Vector3 markerPoint=candidatePoint+Vector3.up*.018f;
                if(!spawnMarker.gameObject.activeSelf)spawnMarker.position=markerPoint;
                else spawnMarker.position=Vector3.Lerp(spawnMarker.position,markerPoint,1-Mathf.Exp(-Time.unscaledDeltaTime*18));
                spawnMarker.gameObject.SetActive(true);
            }
            else if(Time.unscaledTime-lastCandidateTime>.25f)spawnMarker.gameObject.SetActive(false);
            aimLine.gameObject.SetActive(trigger&&valid);
            if(trigger&&valid){aimLine.SetPosition(0,rightHand.position);aimLine.SetPosition(1,candidatePoint+Vector3.up*.018f);}
            help.text=room.Ready?"LOOK AT A FLAT SURFACE  /  HOLD TRIGGER TO AIM, RELEASE TO PLACE":"ALLOW SPATIAL DATA  /  LEFT GRIP TO SCAN ROOM";
            if((!trigger&&previousTrigger)||(placeButton&&!previousHelp))
            {
                if(!valid){Debug.Log("PET_SPAWN_MISS surfaces="+room.SurfaceCount+" headForward="+view.transform.forward);return;}
                Vector3 flat=view.transform.forward;flat.y=0;
                anchor.position=candidatePoint+Vector3.up*.012f;
                if(flat.sqrMagnitude>.001f)anchor.rotation=Quaternion.LookRotation(flat.normalized);
                home=anchor.position;petRoot.localPosition=Vector3.zero;
                pet.PrepareForPlacement();petRoot.gameObject.SetActive(true);spawnMarker.gameObject.SetActive(false);aimLine.gameObject.SetActive(false);
                spawnBeacon.gameObject.SetActive(true);beaconUntil=Time.unscaledTime+4;
                wanderX=targetX=0;wanderZ=targetZ=0;wanderWait=3;
                placed=true;Debug.Log("PET_SPAWN_LOCKED source="+source+" surface="+candidatePoint+" normal="+chosen.normal+" head="+view.transform.position+" view="+view.WorldToViewportPoint(candidatePoint+Vector3.up*.12f));
                if(homeAnchors!=null&&selectedProfile>=0)homeAnchors.Save(selectedProfile,new Pose(anchor.position,anchor.rotation));
            }
        }
        void BeginReselect()
        {
            if(fight.Active)fight.End();
            SetFightMusic(false);
            grenadeArc.gameObject.SetActive(false);
            if(homeAnchors!=null)homeAnchors.Cancel();
            heldPet=petFlying=false;heldFood=heldProp=-1;ClosePack();
            ClearProjectiles();haveSwordTip=false;
            for(int i=0;i<foods.Length;i++)ResetFood(i);
            for(int i=0;i<props.Length;i++)ResetProp(i);
            if(songSource)songSource.Stop();pet.Wake();
            placed=false;petRoot.gameObject.SetActive(false);spawnMarker.gameObject.SetActive(false);aimLine.gameObject.SetActive(false);
            Debug.Log("PET_SPAWN_RESELECT");
        }
        void ReturnToRoster()
        {
            Save();BeginReselect();selectedProfile=-1;titleDismissed=false;awaitStartRelease=true;
            titleScreen.ShowRoster(roster,highlightedSlot,nameStyle);hud.Update(pet,care,null,false);
            Debug.Log("PET_ROSTER_RETURN");
        }
        bool UsableSpawnHit(RaycastHit hit)
        {
            if(hit.normal.y<.82f)return false;
            Vector3 target=hit.point+Vector3.up*.12f;
            Vector3 local=view.transform.InverseTransformPoint(target);
            if(local.z<.35f||local.z>5f)return false;
            return Mathf.Abs(local.x)<local.z*.82f&&Mathf.Abs(local.y)<local.z*.70f;
        }
        bool BehindHead(Vector3 hand)
        {var p=view.transform.InverseTransformPoint(hand);return Mathf.Abs(p.x)<.37f&&p.y>-.55f&&p.y<.22f&&p.z<-.09f&&p.z>-.65f;}
        void TogglePack()
        {if(heldFood>=0||heldProp>=0)return;packOpen=!packOpen;packTray.gameObject.SetActive(packOpen);Debug.Log(packOpen?"PET_PACK_OPEN":"PET_PACK_CLOSED");}
        void ClosePack(){packOpen=false;packTray.gameObject.SetActive(false);}
        bool AimedAtPet(Ray ray)
        {
            Vector3 belly=petRoot.TransformPoint(new Vector3(0,.09f,.055f));
            if(onQuest)
            {
                if(Vector3.Distance(rightVirtualHand.FingerTip.position,belly)<.15f||Vector3.Distance(rightHand.position,belly)<.20f)return true;
                Vector3 toward=belly-rightHand.position;
                if(toward.magnitude<6f)
                {
                    // Quest's controller pose can be reversed by the active hand profile.
                    // Angular aiming also makes a small pet selectable across a room.
                    float aim=Mathf.Abs(Vector3.Dot(toward.normalized,rightHand.forward));
                    if(aim>.978f)return true;
                    Vector3 fromEye=belly-view.transform.position;
                    if(fromEye.sqrMagnitude<36f&&Vector3.Dot(fromEye.normalized,view.transform.forward)>.99f)return true;
                }
            }
            Physics.SyncTransforms();RaycastHit hit;
            if(Physics.Raycast(ray,out hit,6,1<<PetLayer,QueryTriggerInteraction.Ignore))return true;
            return onQuest&&Physics.Raycast(new Ray(rightHand.position,-ray.direction),out hit,6,1<<PetLayer,QueryTriggerInteraction.Ignore);
        }
        void TryPickFood(Ray ray)
        {
            int best=-1;float distance=.11f;
            for(int i=0;i<foods.Length;i++)
            {
                float d=Vector3.Distance(rightHand.position,foods[i].position);
                if(d<distance){distance=d;best=i;}
            }
            if(best<0){RaycastHit hit;if(Physics.Raycast(ray,out hit,1.2f,1<<FoodLayer,QueryTriggerInteraction.Ignore))
                for(int i=0;i<foods.Length;i++)if(hit.transform==foods[i]){best=i;break;}}
            if(best>=0){heldFood=best;foodFlying[best]=false;foods[best].SetParent(null,true);ClosePack();Debug.Log("PET_PACK_GRABBED_"+(FoodKind)best);}
        }
        void FeedHeld()
        {
            if(heldFood<0)return;int food=heldFood;ReturnFood();
            if(fight.Active)fight.HealPlayer(food==(int)FoodKind.Water?10:food==(int)FoodKind.Burger?20:12);
            if(food==(int)FoodKind.Water)
            {if(pet.Drink()){squeakSource.PlayOneShot(squeak,.55f);Burst(new Color(.49f,.82f,1),10);Haptic(.22f,.08f);RecordCare(CareAction.Water,"REFRESHED!");Debug.Log("PET_DRANK_WATER");}}
            else if(pet.Feed((FoodKind)food)){squeakSource.PlayOneShot(squeak,.65f);Burst(PetArt.Gold,10);Haptic(.38f,.12f);RecordCare(CareAction.Feed,"YUM!");Debug.Log("PET_FED_"+(FoodKind)food);}
            if(fight.Active)pet.EnterFight();
        }
        void ReturnFood(){if(heldFood<0)return;int i=heldFood;heldFood=-1;ResetFood(i);}
        void ResetFood(int i)
        {foodFlying[i]=false;foodFlightTime[i]=0;foodVelocity[i]=Vector3.zero;foods[i].SetParent(packTray,false);foods[i].localPosition=foodHomes[i];foods[i].localRotation=Quaternion.identity;}
        void ThrowFood()
        {
            if(heldFood<0)return;int i=heldFood;heldFood=-1;
            foodFlying[i]=true;foodFlightTime[i]=0;
            foodVelocity[i]=Vector3.ClampMagnitude(rightVelocity,2.4f)+rightHand.forward*aimSign*.40f+Vector3.up*.11f;
            Debug.Log("PET_FOOD_THROWN_"+(FoodKind)i);
        }
        void TryPickProp(Ray ray)
        {
            int best=-1;float distance=.12f;
            for(int i=0;i<props.Length;i++)
            {float d=Vector3.Distance(rightHand.position,props[i].position);if(d<distance){distance=d;best=i;}}
            if(best<0){RaycastHit hit;if(Physics.Raycast(ray,out hit,1.2f,1<<FoodLayer,QueryTriggerInteraction.Ignore))
                for(int i=0;i<props.Length;i++)if(hit.transform==props[i]){best=i;break;}}
            if(best>=0){heldProp=best;propFlying[best]=false;props[best].SetParent(null,true);ClosePack();Debug.Log("PET_PACK_GRABBED_PROP_"+best);}
        }
        void ResetProp(int i)
        {propFlying[i]=false;propFlightTime[i]=0;propVelocity[i]=Vector3.zero;propClosest[i]=100;props[i].SetParent(packTray,false);props[i].localPosition=PropHome(i);props[i].localRotation=Quaternion.identity;if(i==2)haveSwordTip=false;}
        static Vector3 PropHome(int i){return new Vector3((i-2)*.115f,-.056f,-.025f);}
        void ThrowProp()
        {
            if(heldProp<0)return;int i=heldProp;heldProp=-1;
            haveSwordTip=false;
            propFlying[i]=true;propFlightTime[i]=0;propClosest[i]=100;
            propVelocity[i]=Vector3.ClampMagnitude(rightVelocity,2.7f)+rightHand.forward*aimSign*.48f+Vector3.up*.12f;
            Debug.Log("PET_PROP_THROWN_"+i);
        }
        void UseHeldProp(Ray ray)
        {
            if(heldProp==1)
            {
                toolCooldown=.30f;FireProjectile(ray);Haptic(.18f,.08f);
            }
            else if(heldProp==2)
            {
                Vector3 tip=props[2].TransformPoint(new Vector3(0,.15f,0));
                if(Vector3.Distance(tip,petRoot.position+Vector3.up*.10f)<.15f)
                {toolCooldown=.7f;HitPet(tip,.55f,"SWORD");}
            }
        }
        void CheckSwordSwing(float dt)
        {
            Vector3 tip=props[2].TransformPoint(new Vector3(0,.15f,0));
            if(haveSwordTip&&toolCooldown<=0)
            {
                float speed=Vector3.Distance(tip,previousSwordTip)/Mathf.Max(dt,.008f);
                if(speed>.28f&&SegmentDistance(petRoot.position+Vector3.up*.10f,previousSwordTip,tip)<.15f)
                {toolCooldown=.7f;HitPet(tip,Mathf.Clamp(speed*.26f,.38f,.75f),"SWORD");}
            }
            previousSwordTip=tip;haveSwordTip=true;
        }
        void FireProjectile(Ray ray)
        {
            Vector3 direction=ray.direction.normalized;
            Vector3 muzzle=props[1].position+direction*.09f;
            var pellet=PetArt.Shape(null,PrimitiveType.Sphere,muzzle,Vector3.one*.025f,PetArt.Gold,"Soft blaster projectile").transform;
            projectiles.Add(new Projectile{T=pellet,Velocity=direction*3.2f,Life=1.6f});
            Burst(PetArt.Gold,4);squeakSource.PlayOneShot(squeak,.25f);
            Debug.Log("PET_BLASTER_FIRED");
        }
        void UpdateProjectiles(float dt)
        {
            for(int i=projectiles.Count-1;i>=0;i--)
            {
                var shot=projectiles[i];Vector3 start=shot.T.position,next=start+shot.Velocity*dt;
                bool petHit=placed&&SegmentDistance(petRoot.position+Vector3.up*.10f,start,next)<.105f;
                bool roomHit=false;RaycastHit hit=default(RaycastHit);Vector3 step=next-start;
                if(step.sqrMagnitude>.000001f)roomHit=Physics.SphereCast(start,.012f,step.normalized,out hit,step.magnitude,RoomWorld.RoomMask,QueryTriggerInteraction.Ignore);
                if(petHit&&(!roomHit||Vector3.Distance(start,petRoot.position)<hit.distance+.12f))
                {HitPet(start,1.05f,"BLASTER");Destroy(shot.T.gameObject);projectiles.RemoveAt(i);continue;}
                shot.Life-=dt;
                if(roomHit||shot.Life<=0){Destroy(shot.T.gameObject);projectiles.RemoveAt(i);continue;}
                shot.T.position=next;
            }
        }
        void ClearProjectiles(){foreach(var shot in projectiles)if(shot.T)Destroy(shot.T.gameObject);projectiles.Clear();}
        void HitPet(Vector3 source,float push,string tool)
        {
            if(!placed||pet.IsDead||!petRoot.gameObject.activeInHierarchy)return;
            if(songSource.isPlaying)songSource.Stop();
            heldPet=false;petFlying=true;petFlightTime=0;walking=false;pet.Attacked();
            Vector3 away=petRoot.position-source;away.y=0;
            if(away.sqrMagnitude<.001f)away=-view.transform.forward;
            away.Normalize();petVelocity=away*push+Vector3.up*.30f;
            if(moodClip)moodSource.PlayOneShot(moodClip,.8f);
            moodCooldown=8;Burst(PetArt.Pink,9);Haptic(.22f,.09f);
            ShowNotice("HEY!",PetArt.Pink);
            Debug.Log("PET_HIT_"+tool+" impulse="+petVelocity);
        }
        void UpdatePropFlight(float dt)
        {
            for(int i=0;i<props.Length;i++)if(propFlying[i])
            {
                propFlightTime[i]+=dt;Vector3 start=props[i].position;
                propVelocity[i]+=Physics.gravity*(dt*.72f);Vector3 next=start+propVelocity[i]*dt;
                if(i==0&&placed)
                {
                    Vector3 mouth=petRoot.position+Vector3.up*.12f;
                    float near=Vector3.Distance(start,mouth);propClosest[i]=Mathf.Min(propClosest[i],near);
                    if(near<.9f)
                    {Vector3 desired=(mouth-start).normalized*2.1f;propVelocity[i]=Vector3.Lerp(propVelocity[i],desired,1-Mathf.Exp(-dt*5));next=start+propVelocity[i]*dt;}
                }
                if(i==0&&placed&&SegmentDistance(petRoot.position+Vector3.up*.12f,start,next)<.28f)
                {ResetProp(i);if(!fight.Active)Poke();Debug.Log("PET_CAUGHT_MICROPHONE");continue;}
                if(i>0&&placed&&SegmentDistance(petRoot.position+Vector3.up*.1f,start,next)<.16f)
                {Burst(i==1?PetArt.Gold:PetArt.Pink,12);squeakSource.PlayOneShot(squeak,.45f);ResetProp(i);Debug.Log("PET_CAUGHT_TOY_"+i);continue;}
                RaycastHit hit;Vector3 step=next-start;
                if(step.sqrMagnitude>.000001f&&Physics.SphereCast(start,.025f,step.normalized,out hit,step.magnitude,RoomWorld.RoomMask))
                {props[i].position=hit.point+hit.normal*.03f;propVelocity[i]=Vector3.Reflect(propVelocity[i],hit.normal)*.32f;}
                else props[i].position=next;
                props[i].Rotate(Vector3.right,dt*310,Space.World);
                if(propFlightTime[i]>4||Vector3.Distance(props[i].position,anchor.position)>3f)
                {if(i==0)Debug.Log("PET_MIC_MISS closest="+propClosest[i].ToString("F2"));ResetProp(i);}
            }
        }
        void TryPickPet()
        {
            if(heldPet||pet.IsDead)return;
            bool near=Vector3.Distance(leftHand.position,petRoot.position+Vector3.up*.1f)<.18f;
            RaycastHit hit;bool aimed=Physics.Raycast(new Ray(leftHand.position,leftHand.forward),out hit,1.5f,1<<PetLayer)||
                Physics.Raycast(new Ray(leftHand.position,-leftHand.forward),out hit,1.5f,1<<PetLayer);
            if(!near&&!aimed)return;
            heldPet=true;petFlying=false;walking=false;pet.PickUp();Haptic(.24f,.12f);Debug.Log("PET_PICKED_UP");
            RecordCare(CareAction.Cuddle,"WHEE!");
        }
        void ThrowPet()
        {
            heldPet=false;petFlying=true;petFlightTime=0;pet.Throw();
            petVelocity=Vector3.ClampMagnitude(leftVelocity,2.2f)+leftHand.forward*.30f+Vector3.up*.15f;
            Debug.Log("PET_TOSSED");
        }
        void PunchPet()
        {
            if(pet.Action==PetAction.Sing)return;
            punchCooldown=.75f;heldPet=false;petFlying=true;petFlightTime=0;pet.Punch();
            Vector3 away=petRoot.position-leftHand.position;away.y=0;if(away.sqrMagnitude<.001f)away=view.transform.forward;away.Normalize();
            petVelocity=away*.48f+Vector3.up*.45f+Vector3.ClampMagnitude(leftVelocity*.30f,.35f);
            if(moodClip)moodSource.PlayOneShot(moodClip,.75f);
            moodCooldown=12;Burst(PetArt.Pink,7);Debug.Log("PET_PUNCHED_REACTION");
        }
        void ResetPet()
        {
            heldPet=petFlying=restingAtThrow=false;heldFood=heldProp=-1;petVelocity=Vector3.zero;petFlightTime=0;ClosePack();
            ClearProjectiles();haveSwordTip=false;
            for(int i=0;i<foods.Length;i++)ResetFood(i);
            for(int i=0;i<props.Length;i++)ResetProp(i);
            if(songSource)songSource.Stop();pet.Wake();
            if(placed)anchor.position=home;
            petRoot.localPosition=Vector3.zero;petRoot.localRotation=Quaternion.identity;
            targetX=targetZ=wanderX=wanderZ=speed=0;wanderWait=2;heading=0;Save();
            Burst(PetArt.Gold,16);Haptic(.35f,.14f);
            ShowNotice("BACK HOME",PetArt.Cream);
        }
        void UpdatePetFlight(float dt)
        {
            petFlightTime+=dt;Vector3 start=petRoot.position;
            petVelocity+=Physics.gravity*(dt*.72f);Vector3 step=petVelocity*dt;
            RaycastHit hit;
            if(step.sqrMagnitude>.000001f&&Physics.SphereCast(start+Vector3.up*.095f,.055f,step.normalized,out hit,step.magnitude,RoomWorld.RoomMask))
            {
                if(hit.normal.y>.55f)
                {
                    LandPet(hit.point.y);
                }
                else{petRoot.position=start+hit.normal*.015f;petVelocity=Vector3.Reflect(petVelocity,hit.normal)*.35f;}
            }
            else petRoot.position=start+step;
            // Probe from above the room: a cast beginning inside a table/floor mesh
            // may miss its top. The floor-mode XR origin is the final safety plane.
            if(petFlying&&petVelocity.y<=0)
            {
                float ground=rig.position.y;
                RaycastHit below;
                Vector3 probe=new Vector3(petRoot.position.x,Mathf.Max(start.y+.30f,rig.position.y+.5f),petRoot.position.z);
                if(Physics.Raycast(probe,Vector3.down,out below,6f,RoomWorld.RoomMask,QueryTriggerInteraction.Ignore)&&below.normal.y>.55f&&below.point.y<=start.y+.06f)
                    ground=Mathf.Max(ground,below.point.y);
                if(petRoot.position.y<=ground+.018f)LandPet(ground);
            }
            if(petFlying&&petFlightTime>4)LandPet(rig.position.y);
        }
        void LandPet(float worldY)
        {
            var p=petRoot.position;p.y=Mathf.Max(rig.position.y,worldY)+.012f;petRoot.position=p;
            petFlying=false;restingAtThrow=true;pet.Land();petVelocity=Vector3.zero;SetWanderCenter();Debug.Log("PET_LANDED height="+p.y.ToString("F2"));
        }
        void SetWanderCenter(){var p=petRoot.localPosition;wanderX=targetX=p.x;wanderZ=targetZ=p.z;wanderWait=2;}
        static float SegmentDistance(Vector3 point,Vector3 a,Vector3 b)
        {Vector3 d=b-a;float t=d.sqrMagnitude>.000001f?Mathf.Clamp01(Vector3.Dot(point-a,d)/d.sqrMagnitude):0;return Vector3.Distance(point,a+d*t);}
        void UpdateFoodFlight(float dt)
        {
            for(int i=0;i<foods.Length;i++)if(foodFlying[i])
            {
                foodFlightTime[i]+=dt;Vector3 start=foods[i].position;
                foodVelocity[i]+=Physics.gravity*(dt*.72f);Vector3 step=foodVelocity[i]*dt;
                Vector3 next=start+step;
                if(placed&&SegmentDistance(MouthWorld(),start,next)<.105f&&pet.CatchFood((FoodKind)i))
                {
                    ResetFood(i);squeakSource.PlayOneShot(squeak,.85f);Burst(PetArt.Gold,15);
                    if(fight.Active)fight.HealPlayer(i==(int)FoodKind.Water?10:i==(int)FoodKind.Burger?20:12);
                    if(i==(int)FoodKind.Water){RecordCare(CareAction.Water,"REFRESHED!");Debug.Log("PET_CAUGHT_WATER");}
                    else{RecordCare(CareAction.Feed,"GREAT CATCH!");Debug.Log("PET_CAUGHT_FOOD_"+(FoodKind)i);}
                    if(fight.Active)pet.EnterFight();continue;
                }
                RaycastHit hit;
                if(step.sqrMagnitude>.000001f&&Physics.SphereCast(start,.020f,step.normalized,out hit,step.magnitude,RoomWorld.RoomMask))
                {foods[i].position=hit.point+hit.normal*.023f;foodVelocity[i]=Vector3.Reflect(foodVelocity[i],hit.normal)*.35f;}
                else foods[i].position=next;
                foods[i].Rotate(Vector3.up,dt*180,Space.World);
                if(foodFlightTime[i]>4||Vector3.Distance(foods[i].position,anchor.position)>2.8f)ResetFood(i);
            }
        }
        void Poke()
        {
            if(songSource.clip&&pet.Poke(songSource.clip.length))
            {pokeCooldown=.7f;songSource.Stop();songSource.Play();Burst(PetArt.Pink,12);Haptic(.32f,.1f);RecordCare(CareAction.Sing,"SING ALONG!");Debug.Log("PET_SONG_STARTED");}
        }
        void Haptic(float strength,float seconds){if(onQuest)InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0,strength,seconds);}
        Vector3 MouthWorld(){return petRoot.TransformPoint(new Vector3(0,.105f,.082f));}
        void ReadDesktop()
        {
            var k=Keyboard.current;if(k==null)return;
            if(k.tabKey.wasPressedThisFrame){ReturnToRoster();return;}
            if(k.fKey.wasPressedThisFrame)
            {if(fight.Active){fight.End();SetFightMusic(false);}else if(placed&&!pet.IsDead){pet.EnterFight();walking=false;fight.Begin();SetFightMusic(true);}}
            if(fight.Active)
            {
                fight.Controls(new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0)),k.spaceKey.isPressed,k.gKey.isPressed);
                if(k.jKey.wasPressedThisFrame)fight.Punch();
                if(k.kKey.wasPressedThisFrame)fight.Kick();
                if(k.gKey.wasPressedThisFrame)fight.Grab();
                if(k.gKey.wasReleasedThisFrame)fight.Throw(view.transform.forward);
                if(k.leftShiftKey.wasPressedThisFrame)fight.Jump();
                if(k.digit6Key.wasPressedThisFrame)fight.ThrowBomb(petRoot.position+Vector3.up*.2f,view.transform.forward*1.4f+Vector3.up*.5f);
                if(k.digit7Key.wasPressedThisFrame)fight.LaunchRocket(petRoot.position+Vector3.up*.2f,view.transform.forward,1);
                return;
            }
            if(k.mKey.wasPressedThisFrame)Poke();
            if(k.hKey.wasPressedThisFrame)hud.ToggleControls();
            if(k.digit1Key.wasPressedThisFrame)FeedDesktop(FoodKind.Donut);
            if(k.digit2Key.wasPressedThisFrame)FeedDesktop(FoodKind.Burger);
            if(k.digit3Key.wasPressedThisFrame)FeedDesktop(FoodKind.HotDog);
            if(k.digit4Key.wasPressedThisFrame)FeedDesktop(FoodKind.Cookie);
            if(k.digit5Key.wasPressedThisFrame)FeedDesktop(FoodKind.Water);
            if(k.pKey.wasPressedThisFrame)PetPuff();
            if(k.rKey.wasPressedThisFrame)ResetPet();
        }
        void FeedDesktop(FoodKind kind){if(pet.Feed(kind)){squeakSource.PlayOneShot(squeak,.65f);Burst(PetArt.Gold,8);RecordCare(kind==FoodKind.Water?CareAction.Water:CareAction.Feed,kind==FoodKind.Water?"REFRESHED!":"YUM!");}}
        void PetPuff()
        {
            if(!placed||pettingCooldown>0||!pet.Pet())return;
            pettingCooldown=.9f;squeakSource.PlayOneShot(squeak,.42f);Burst(PetArt.Pink,5);Haptic(.12f,.06f);
            ShowNotice("PURR...",PetArt.Cream);Save();Debug.Log("PET_GENTLY_PETTED");
        }
        void MovePet(float dt)
        {
            if(heldPet||petFlying||pet.Action==PetAction.Sing||pet.Action==PetAction.Sleep||pet.Action==PetAction.Celebrate||pet.Action==PetAction.Catch||pet.Action==PetAction.Angry||pet.Action==PetAction.Attention||pet.Action==PetAction.Play||pet.Action==PetAction.Petted||pet.Action==PetAction.Sick||pet.Action==PetAction.Dead){walking=false;speed=0;return;}
            wanderWait-=dt;
            if(wanderWait<=0)
            {
                wanderWait=UnityEngine.Random.Range(2.5f,5.5f)*(personalityStyle==1?.75f:personalityStyle==4?1.25f:1);
                targetX=wanderX+UnityEngine.Random.Range(-.30f,.30f);targetZ=wanderZ+UnityEngine.Random.Range(-.25f,.25f);
                float ground;if(!GroundForPet(targetX,targetZ,.6f,out ground))targetX=petRoot.localPosition.x;
            }
            var pos=petRoot.localPosition;float dx=targetX-pos.x,dz=targetZ-pos.z,distance=Mathf.Sqrt(dx*dx+dz*dz);
            walking=distance>.035f;if(!walking){speed=0;pet.SetCurious(false);return;}
            float desired=(pet.Boredom>55?.20f:.13f)*(personalityStyle==1?1.15f:personalityStyle==4?.80f:1);
            speed=Mathf.Lerp(speed,desired,1-Mathf.Exp(-dt*7));
            float step=Mathf.Min(speed*dt,distance);var from=new PetPoint(pos.x,pos.y,pos.z);
            var to=room.Move(from,new PetPoint(pos.x+dx/distance*step,pos.y,pos.z+dz/distance*step),.05f,.17f);
            float floor;if(GroundForPet(to.X,to.Z,pos.y+.3f,out floor)&&Mathf.Abs(floor-pos.y)<.11f)
                petRoot.localPosition=new Vector3(to.X,Mathf.Lerp(pos.y,floor,1-Mathf.Exp(-dt*10)),to.Z);
            else{wanderWait=0;walking=false;speed=0;}
            pet.SetCurious(walking);
        }
        void FollowIfNeeded(float dt)
        {
            if(!onQuest||heldFood>=0||heldPet||petFlying||restingAtThrow||pet.Action==PetAction.Sing)return;
            followWait-=dt;if(followWait>0)return;followWait=5;
            if(Vector3.Distance(view.transform.position,petRoot.position)<2.2f)return;
            Vector3 forward=view.transform.forward;forward.y=0;forward.Normalize();
            RaycastHit surface;var ray=new Ray(view.transform.position+forward*.7f+Vector3.up*.1f,Vector3.down);
            if(room.AimSurface(ray,out surface)&&surface.normal.y>.62f)
            {anchor.position=surface.point+Vector3.up*.012f;float h;if(room.Ground(0,0,.5f,out h))petRoot.localPosition=new Vector3(0,h,0);home=anchor.position;Burst(PetArt.Pink,8);}
        }
        void PositionPet()
        {
            if(!placed){petRoot.localPosition=Vector3.zero;return;}
            if(heldPet||petFlying)return;
            float floor;if(GroundForPet(petRoot.localPosition.x,petRoot.localPosition.z,petRoot.localPosition.y+.25f,out floor)&&pet.Action!=PetAction.Sing&&Mathf.Abs(floor-petRoot.localPosition.y)<.11f)
            {var p=petRoot.localPosition;p.y=Mathf.Lerp(p.y,floor,1-Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime,.05f)*6));petRoot.localPosition=p;}
        }
        bool GroundForPet(float x,float z,float fromY,out float height)
        {
            if(room.Ground(x,z,fromY,out height))return true;
            height=rig.position.y-anchor.position.y;
            return true;
        }
        void Animate(float dt)
        {
            float lift=0,pitch=0,roll=0,danceYaw=0;
            Vector3 toViewer=view.transform.position-petRoot.position;toViewer.y=0;
            float yaw=toViewer.sqrMagnitude>.001f?Quaternion.LookRotation(toViewer).eulerAngles.y:heading;
            heading=Mathf.LerpAngle(heading,yaw,1-Mathf.Exp(-dt*7));
            if(pet.Action==PetAction.Sing&&songSource.clip)
            {
                float t=songSource.time%8f;
                lift=.012f+Mathf.Max(0,Mathf.Sin(songSource.time*9))*.026f;
                if(t>=2&&t<2.9f){float q=(t-2)/.9f;roll=360*q;lift+=Mathf.Sin(q*Mathf.PI)*.075f;}
                if(t>=5&&t<5.9f){float q=(t-5)/.9f;pitch=360*q;lift+=Mathf.Sin(q*Mathf.PI)*.09f;}
                danceYaw=Mathf.Sin(songSource.time*2)*12;
            }
            else if(pet.Action==PetAction.Celebrate)lift=Mathf.Abs(Mathf.Sin(Time.time*12))*.07f;
            else if(pet.Action==PetAction.Catch){lift=Mathf.Abs(Mathf.Sin(Time.time*16))*.062f;pitch=-16;}
            else if(pet.Action==PetAction.Play){lift=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Time.time*9)),2)*.055f;roll=0;}
            else if(pet.Action==PetAction.Attention){lift=.015f+Mathf.Abs(Mathf.Sin(Time.time*7))*.012f;roll=Mathf.Sin(Time.time*5)*10;}
            else if(pet.Action==PetAction.Angry){lift=-.008f;roll=Mathf.Sin(Time.time*13)*5;}
            else if(pet.Action==PetAction.Petted){lift=.008f;roll=Mathf.Sin(Time.time*6)*9;}
            else if(pet.Action==PetAction.Sick){lift=-.012f;roll=18;}
            else if(pet.Action==PetAction.Dead){lift=-.018f;roll=88;}
            else if(pet.Action==PetAction.Sleep){roll=86;lift=-.010f;}
            else if(walking)lift=Mathf.Abs(Mathf.Sin(Time.time*12))*.009f;
            if(!fight.Active)petRoot.rotation=Quaternion.Euler(0,heading+danceYaw,0);
            visualPivot.localPosition=new Vector3(0,.102f+lift,0);visualPivot.localRotation=Quaternion.Euler(pitch,0,roll);
            microphone.gameObject.SetActive(pet.Action==PetAction.Sing);
            sleepLabel.gameObject.SetActive(pet.Action==PetAction.Sleep);
            motion.Pose(pet,speed,songSource,dt);
        }
        void UpdateHud()
        {
            bool show=placed||!onQuest;hud.Update(pet,care,ActiveProfile,show);hud.SetDaily(dailyStars,DailyName(dailyGoal),dailyDone);help.gameObject.SetActive(!placed);
            help.transform.localPosition=new Vector3(0,.18f,.18f);
            if(!placed)help.text=room.Ready?"AIM AT A SCANNED DESK OR FLOOR + RIGHT TRIGGER / A":"ALLOW SPATIAL DATA / LEFT GRIP TO SCAN YOUR ROOM";
            else help.text="";
            help.transform.rotation=sleepLabel.transform.rotation=view.transform.rotation;
        }
        void Burst(Color color,int count)
        {
            for(int i=0;i<count&&sparks.Count<50;i++)
            {var go=PetArt.Shape(anchor,PrimitiveType.Sphere,petRoot.localPosition+new Vector3(0,.10f,0),Vector3.one*.012f,color,"Happy sparkle");
             sparks.Add(new Spark{T=go.transform,Velocity=UnityEngine.Random.insideUnitSphere*.12f+Vector3.up*.2f,Life=1});}
        }
        void UpdateSparks(float dt)
        {
            for(int i=sparks.Count-1;i>=0;i--)
            {var s=sparks[i];s.Life-=dt;if(s.Life<=0){Destroy(s.T.gameObject);sparks.RemoveAt(i);continue;}s.T.localPosition+=s.Velocity*dt;s.T.localScale=Vector3.one*.012f*s.Life;}
        }
        void UpdateMood(float dt)
        {
            if(fight.Active)return;
            moodCooldown-=dt;
            if(moodCooldown>0||!placed||pet.Action==PetAction.Sleep||pet.Action==PetAction.Sing||!moodClip)return;
            if(pet.Action==PetAction.Attention||pet.Action==PetAction.Angry||pet.Boredom>58||pet.Hunger>75)
            {
                moodSource.PlayOneShot(moodClip,.7f);moodCooldown=UnityEngine.Random.Range(14f,24f);
                if(pet.Action==PetAction.Attention)Burst(PetArt.Pink,4);
                Debug.Log("PET_MOOD_SOUND");
            }
            else moodCooldown=5;
        }
        static AudioClip Squeak()
        {
            int n=22050/3;var data=new float[n];for(int i=0;i<n;i++){float t=(float)i/n;data[i]=Mathf.Sin(i*(620+500*t)*2*Mathf.PI/22050)*Mathf.Sin(t*Mathf.PI)*.25f;}
            var clip=AudioClip.Create("Happy little squeak",n,1,22050,false);clip.SetData(data,0);return clip;
        }
        static AudioClip CozyMusic()
        {
            const int rate=22050;var data=new float[rate*8];
            int[] notes={0,4,7,12,7,4,2,7,5,9,12,9,7,5,4,2};
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,beat=t*2,local=(beat-Mathf.Floor(beat))*.5f;
                float frequency=261.63f*Mathf.Pow(2,notes[(int)beat%notes.Length]/12f);
                float envelope=(1-Mathf.Exp(-local*42))*Mathf.Exp(-local*7);
                data[i]=(Mathf.Sin(local*frequency*2*Mathf.PI)*.65f+Mathf.Sin(local*frequency*4*Mathf.PI)*.12f)*envelope*.15f;
            }
            var clip=AudioClip.Create("Cozy original music loop",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        static AudioClip BattleMusic()
        {
            const int rate=22050;var data=new float[rate*8];int[] bass={0,0,3,0,5,3,7,5};
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,beat=t*3,phase=beat-Mathf.Floor(beat);
                float hz=110*Mathf.Pow(2,bass[(int)beat%bass.Length]/12f);
                float kick=Mathf.Sin(t*6.28318f*(62-48*phase))*Mathf.Exp(-phase*14);
                float note=(Mathf.Sin(t*hz*6.28318f)+.3f*Mathf.Sin(t*hz*12.56636f))*Mathf.Exp(-phase*4);
                float hat=Mathf.Sin(t*8100*6.28318f)*Mathf.Exp(-((beat*2)%1)*25);
                data[i]=(kick*.28f+note*.14f+hat*.018f);
            }
            var clip=AudioClip.Create("Puff battle loop",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void SetFightMusic(bool active)
        {
            if(!musicSource)return;
            AudioClip desired=active?battleMusicClip:musicClip;
            if(musicSource.clip==desired)return;
            musicSource.Stop();musicSource.clip=desired;if(placed)musicSource.Play();
        }
        static AudioClip SoftStep()
        {
            const int rate=22050;var data=new float[rate/10];
            for(int i=0;i<data.Length;i++)
            {float t=(float)i/rate;data[i]=Mathf.Sin(t*240*2*Mathf.PI)*Mathf.Exp(-t*38)*.09f;}
            var clip=AudioClip.Create("Tiny soft step",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        static AudioClip SleepPurr()
        {
            const int rate=22050;var data=new float[rate*3/5];
            for(int i=0;i<data.Length;i++)
            {float t=(float)i/rate;data[i]=Mathf.Sin(t*105*2*Mathf.PI)*Mathf.Sin(t/.6f*Mathf.PI)*.055f;}
            var clip=AudioClip.Create("Sleepy little purr",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void UpdateLittleSounds(float dt)
        {
            if(!placed||fight.Active)return;
            if(pet.Action!=lastAnimatedAction)
            {
                if(pet.Action==PetAction.Play){squeakSource.PlayOneShot(squeak,.36f);ShowNotice("PLAY TIME!",PetArt.Gold);Debug.Log("PET_JUMPING_JACKS");}
                if(pet.Action==PetAction.Sleep){squeakSource.PlayOneShot(purrClip,.55f);ShowNotice("SWEET DREAMS",PetArt.Cream);}
                lastAnimatedAction=pet.Action;
            }
            stepCooldown-=dt;
            if(walking&&stepCooldown<=0){stepCooldown=.48f;squeakSource.PlayOneShot(stepClip,.32f);}
        }
        void RecordCare(CareAction action,string feedback)
        {
            CareResult result=care.Record(action);
            bool awarded=false;
            if(!dailyDone&&selectedProfile>=0&&action==dailyGoal)
            {
                dailyDone=true;dailyStars++;awarded=pet.RewardDailyCare();
                PlayerPrefs.SetInt(SlotKey(selectedProfile,"DAILY_DONE"),1);
                PlayerPrefs.SetInt(SlotKey(selectedProfile,"DAILY_STARS"),dailyStars);
                Burst(PetArt.Gold,16);
                Debug.Log("PUFF_DAILY_COMPLETE slot="+selectedProfile+" stars="+dailyStars);
            }
            if(result==CareResult.BondLevelUp)
            {
                bondFlower.gameObject.SetActive(true);
                ShowNotice("BOND LEVEL "+care.BondLevel+"!",PetArt.Gold);
                Burst(PetArt.Gold,24);squeakSource.PlayOneShot(squeak,.85f);
                Debug.Log("PET_BOND_LEVEL_UP level="+care.BondLevel);
            }
            else ShowNotice(awarded?"DAILY STAR +1!":feedback,result==CareResult.TaskCompleted||awarded?PetArt.Gold:PetArt.Cream);
            if(result!=CareResult.AlreadyDone)
            {
                Debug.Log("PET_CARE_TASK "+action+" count="+care.TasksCompleted);
                Save();
            }
        }
        void ShowNotice(string message,Color color)
        {
            notice.text=message;notice.color=color;noticeTime=1.7f;
            notice.transform.localPosition=new Vector3(0,.30f,0);
            notice.gameObject.SetActive(true);
        }
        void UpdateNotice(float dt)
        {
            if(noticeTime<=0)return;
            noticeTime-=dt;
            if(noticeTime<=0){notice.gameObject.SetActive(false);return;}
            notice.transform.localPosition=new Vector3(0,.30f+(1.7f-noticeTime)*.025f,0);
            notice.transform.rotation=view.transform.rotation;
            Color c=notice.color;c.a=Mathf.Clamp01(noticeTime/.45f);notice.color=c;
        }
        static long UtcNow(){return DateTimeOffset.UtcNow.ToUnixTimeSeconds();}
        static string SlotKey(int index,string field){return "PET_SLOT_"+index+"_"+field;}
        static string DailyName(CareAction action)
        {return action==CareAction.Feed?"FEED":action==CareAction.Water?"WATER":action==CareAction.Sing?"SING":"CUDDLE";}
        void LoadRoster()
        {
            long now=UtcNow();
            if(PlayerPrefs.GetInt("PET_ROSTER_VERSION",0)<2)
            {
                if(PlayerPrefs.HasKey("PET_LAST_UTC"))
                {
                    roster.Create(0,"Puff",now);
                    var migrated=roster.Get(0);
                    migrated.Hunger=Mathf.Min(60,PlayerPrefs.GetFloat("PET_HUNGER",24));
                    migrated.Boredom=Mathf.Min(60,PlayerPrefs.GetFloat("PET_BOREDOM",18));
                    migrated.Sleepiness=Mathf.Min(65,PlayerPrefs.GetFloat("PET_SLEEPINESS",12));
                    migrated.BondLevel=PlayerPrefs.GetInt("PET_BOND_LEVEL",1);
                    int oldMask=PlayerPrefs.GetInt("PET_CARE_MASK",0);
                    migrated.CareMask=(oldMask&1)|((oldMask&2)<<1)|((oldMask&4)<<1);
                    migrated.CareRounds=PlayerPrefs.GetInt("PET_CARE_ROUNDS",0);
                    Debug.Log("PET_PROFILE_MIGRATED slot=0");
                }
                PersistRoster();return;
            }
            for(int i=0;i<PetRoster.Capacity;i++)if(PlayerPrefs.GetInt(SlotKey(i,"USED"),0)==1)
            {
                long born,last;long.TryParse(PlayerPrefs.GetString(SlotKey(i,"BORN"),"0"),out born);
                long.TryParse(PlayerPrefs.GetString(SlotKey(i,"SAVED"),"0"),out last);
                var profile=new PetProfile{
                    Name=PetRoster.SafeName(PlayerPrefs.GetString(SlotKey(i,"NAME"),"Puff")),
                    BornUtc=born>0?born:now,LastSavedUtc=last>0?last:now,
                    Hunger=PlayerPrefs.GetFloat(SlotKey(i,"HUNGER"),24),Thirst=PlayerPrefs.GetFloat(SlotKey(i,"THIRST"),22),
                    Boredom=PlayerPrefs.GetFloat(SlotKey(i,"BOREDOM"),18),Sleepiness=PlayerPrefs.GetFloat(SlotKey(i,"SLEEP"),12),
                    Health=PlayerPrefs.GetFloat(SlotKey(i,"HEALTH"),100),
                    AgeSeconds=PlayerPrefs.GetFloat(SlotKey(i,"AGE"),0),
                    Dead=PlayerPrefs.GetInt(SlotKey(i,"DEAD"),0)==1,
                    BondLevel=PlayerPrefs.GetInt(SlotKey(i,"BOND"),1),CareMask=PlayerPrefs.GetInt(SlotKey(i,"CARE"),0),
                    CareRounds=PlayerPrefs.GetInt(SlotKey(i,"ROUNDS"),0),Style=PlayerPrefs.GetInt(SlotKey(i,"STYLE"),0)
                };
                var aging=new PetSimulation();aging.LoadProfile(profile,Math.Max(0,now-profile.LastSavedUtc));
                aging.SaveTo(profile);profile.LastSavedUtc=now;roster.Put(i,profile);
            }
            PersistRoster();
        }
        void SelectProfile(int index)
        {
            if(selectedProfile>=0)Save();
            PetProfile profile=roster.Get(index);if(profile==null||profile.Dead)return;
            selectedProfile=index;long now=UtcNow();
            pet.LoadProfile(profile,Math.Max(0,now-profile.LastSavedUtc));
            deathAnnounced=pet.IsDead;lastAnimatedAction=pet.Action;
            care.Restore(profile.BondLevel,profile.CareMask,profile.CareRounds);
            profile.LastSavedUtc=now;pet.SaveTo(profile);
            bondFlower.gameObject.SetActive(care.BondLevel>=2);
            personalityStyle=profile.Style;motion.SetStyle(personalityStyle);
            int today=(int)(now/86400);
            dailyStars=PlayerPrefs.GetInt(SlotKey(index,"DAILY_STARS"),0);
            int lastDay=PlayerPrefs.GetInt(SlotKey(index,"DAILY_DAY"),-1);
            dailyDone=lastDay==today&&PlayerPrefs.GetInt(SlotKey(index,"DAILY_DONE"),0)==1;
            dailyGoal=(CareAction)((today+index)%4);
            if(lastDay!=today)
            {
                PlayerPrefs.SetInt(SlotKey(index,"DAILY_DAY"),today);
                PlayerPrefs.SetInt(SlotKey(index,"DAILY_DONE"),0);
                dailyGreeting=pet.IsSick?"PUFF NEEDS FOOD + WATER":"NEW DAILY: "+DailyName(dailyGoal)+"!";
            }
            else if(pet.IsSick)dailyGreeting="PUFF NEEDS FOOD + WATER";
            ApplyProfileStyle(profile.Style);PersistRoster();
        }
        void ApplyProfileStyle(int style)
        {
            Color[] tints={Color.white,new Color(1,.93f,.98f),new Color(.93f,.97f,1),new Color(1,.93f,.88f),new Color(.95f,1,.92f)};
            var block=new MaterialPropertyBlock();block.SetColor("_Tint",tints[Mathf.Clamp(style,0,tints.Length-1)]);
            foreach(var renderer in petModel.GetComponentsInChildren<MeshRenderer>(true))renderer.SetPropertyBlock(block);
        }
        void PersistRoster()
        {
            PlayerPrefs.SetInt("PET_ROSTER_VERSION",2);
            for(int i=0;i<PetRoster.Capacity;i++)
            {
                PetProfile profile=roster.Get(i);PlayerPrefs.SetInt(SlotKey(i,"USED"),profile==null?0:1);
                if(profile==null)continue;
                PlayerPrefs.SetString(SlotKey(i,"NAME"),profile.Name);
                PlayerPrefs.SetString(SlotKey(i,"BORN"),profile.BornUtc.ToString());
                PlayerPrefs.SetString(SlotKey(i,"SAVED"),profile.LastSavedUtc.ToString());
                PlayerPrefs.SetFloat(SlotKey(i,"HUNGER"),profile.Hunger);PlayerPrefs.SetFloat(SlotKey(i,"THIRST"),profile.Thirst);
                PlayerPrefs.SetFloat(SlotKey(i,"BOREDOM"),profile.Boredom);PlayerPrefs.SetFloat(SlotKey(i,"SLEEP"),profile.Sleepiness);
                PlayerPrefs.SetFloat(SlotKey(i,"HEALTH"),profile.Health);PlayerPrefs.SetFloat(SlotKey(i,"AGE"),(float)profile.AgeSeconds);
                PlayerPrefs.SetInt(SlotKey(i,"DEAD"),profile.Dead?1:0);
                PlayerPrefs.SetInt(SlotKey(i,"BOND"),profile.BondLevel);PlayerPrefs.SetInt(SlotKey(i,"CARE"),profile.CareMask);
                PlayerPrefs.SetInt(SlotKey(i,"ROUNDS"),profile.CareRounds);PlayerPrefs.SetInt(SlotKey(i,"STYLE"),profile.Style);
            }
            PlayerPrefs.Save();
        }
        void Save()
        {
            if(roster==null)return;
            if(selectedProfile>=0)
            {
                PetProfile profile=roster.Get(selectedProfile);
                if(profile!=null)
                {
                    pet.SaveTo(profile);profile.LastSavedUtc=UtcNow();
                    profile.BondLevel=care.BondLevel;profile.CareMask=care.CompletedMask;profile.CareRounds=care.CompletedRounds;
                }
            }
            PersistRoster();
        }
        void OnApplicationPause(bool paused)
        {focusPaused=paused;if(paused){Save();if(songSource&&songSource.isPlaying)songSource.Pause();if(musicSource&&musicSource.isPlaying)musicSource.Pause();}else{ResumeLife();if(songSource&&pet!=null&&pet.Action==PetAction.Sing)songSource.UnPause();if(musicSource&&placed)musicSource.UnPause();}}
        void OnApplicationFocus(bool focus){if(!focus)Save();else ResumeLife();focusPaused=!focus;}
        void ResumeLife()
        {
            PetProfile profile=ActiveProfile;if(profile==null)return;
            long now=UtcNow();double elapsed=Math.Max(0,now-profile.LastSavedUtc);
            if(elapsed<1)return;
            pet.AdvanceLife(elapsed);profile.LastSavedUtc=now;Save();
        }
        void OnDestroy(){Save();ClearProjectiles();if(squeak)Destroy(squeak);if(musicClip)Destroy(musicClip);if(battleMusicClip)Destroy(battleMusicClip);if(stepClip)Destroy(stepClip);if(purrClip)Destroy(purrClip);}
    }
}
