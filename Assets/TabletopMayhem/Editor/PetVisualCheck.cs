using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TabletopMayhem.Core;
namespace TabletopMayhem.Editor
{
    [InitializeOnLoad]
    public static class PetVisualCheck
    {
        const string Key="Pet.VisualCheck";
        static double start;
        static PetVisualCheck(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Capture()
        {
            EditorSceneManager.OpenScene("Assets/TabletopMayhem/Scenes/TabletopMayhem.unity");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Take;}
        }
        static void Take()
        {
            if(EditorApplication.timeSinceStartup-start<3)return;
            EditorApplication.update-=Take;SessionState.SetBool(Key,false);
            try
            {
                var game=UnityEngine.Object.FindFirstObjectByType<PetGame>();
                var title=GameObject.Find("Puff title screen");
                var hiddenPet=Array.Find(Resources.FindObjectsOfTypeAll<GameObject>(),g=>g.name=="Pink companion"&&g.scene.IsValid());
                if(!game||!title||!hiddenPet||hiddenPet.activeSelf)throw new Exception("Title screen did not hide gameplay on launch");
                var camera=Camera.main;var rt=new RenderTexture(1600,1000,24);
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
                File.WriteAllBytes("Art/Blender/unity-title-preview.png",texture.EncodeToPNG());
                var gameFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                var titleScreen=(PetTitleScreen)typeof(PetGame).GetField("titleScreen",gameFlags).GetValue(game);
                var roster=(PetRoster)typeof(PetGame).GetField("roster",gameFlags).GetValue(game);
                titleScreen.ShowRoster(roster,0,0);
                camera.Render();texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
                File.WriteAllBytes("Art/Blender/unity-roster-preview.png",texture.EncodeToPNG());
                game.SendMessage("DismissTitle");
                if(title.activeSelf||!hiddenPet.activeSelf)throw new Exception("Starting did not reveal normal gameplay");
                var hud=GameObject.Find("Cozy pet status card");
                var model=GameObject.Find("Puff model");
                if(!game||!hud||!model)throw new Exception("Pet, HUD or model bootstrap failed");
                var careRibbon=Array.Find(Resources.FindObjectsOfTypeAll<GameObject>(),g=>g.name=="Care progress ribbon"&&g.scene.IsValid());
                if(!careRibbon||!careRibbon.activeInHierarchy)throw new Exception("Care progress HUD ribbon missing");
                if(hud.transform.localScale.x>.7f||hud.transform.localPosition.x>-.34f||hud.transform.localPosition.y<.2f)
                    throw new Exception("Pet HUD did not move to a subtle upper-left position");
                var music=(AudioSource)typeof(PetGame).GetField("musicSource",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
                if(!music||!music.clip||!music.loop||!music.isPlaying)throw new Exception("Cozy background music did not start");
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    if(!renderer.sharedMaterial||!renderer.sharedMaterial.shader)throw new Exception("Missing pet material");
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
                File.WriteAllBytes("Art/Blender/unity-pet-preview.png",texture.EncodeToPNG());
                var anchor=GameObject.Find("Pet's scanned home").transform;
                var companion=GameObject.Find("Pink companion").transform;
                anchor.rotation=Quaternion.Euler(0,80,0);
                for(int i=0;i<45;i++)game.SendMessage("Animate",.05f);
                Vector3 toward=camera.transform.position-companion.position;toward.y=0;
                if(Vector3.Dot(companion.forward,toward.normalized)<.9f)throw new Exception("Pet did not face the viewer after anchor rotation");
                var tray=GameObject.Find("Backpack inventory tray");
                if(tray)throw new Exception("Backpack should start closed");
                game.SendMessage("TogglePack");
                tray=GameObject.Find("Backpack inventory tray");
                if(!tray||tray.GetComponentsInChildren<Transform>().Length<12)throw new Exception("Backpack items missing");
                var mic=GameObject.Find("Throwable microphone");
                if(!mic)throw new Exception("Throwable microphone missing");
                mic.transform.SetParent(null,true);mic.transform.position=companion.position+Vector3.up*.1f;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                ((bool[])typeof(PetGame).GetField("propFlying",flags).GetValue(game))[0]=true;
                game.SendMessage("UpdatePropFlight",.02f);
                if(!companion.GetComponent<AudioSource>().isPlaying)throw new Exception("Thrown microphone did not start the song");
                var care=(PetCareProgress)typeof(PetGame).GetField("care",flags).GetValue(game);
                if(!care.HasCompleted(CareAction.Sing))throw new Exception("Microphone song did not advance the care loop");
                var right=Array.Find(Resources.FindObjectsOfTypeAll<GameObject>(),g=>g.name=="Right controller"&&g.scene.IsValid());
                var left=Array.Find(Resources.FindObjectsOfTypeAll<GameObject>(),g=>g.name=="Left controller"&&g.scene.IsValid());
                if(!right||!left)throw new Exception("Controller glove roots missing");
                right.SetActive(true);left.SetActive(true);
                right.transform.position=camera.transform.position+camera.transform.right*.20f-camera.transform.up*.18f+camera.transform.forward*.43f;
                left.transform.position=camera.transform.position-camera.transform.right*.20f-camera.transform.up*.18f+camera.transform.forward*.43f;
                right.transform.rotation=left.transform.rotation=camera.transform.rotation;
                var rightGlove=(PetHand)typeof(PetGame).GetField("rightVirtualHand",flags).GetValue(game);
                var leftGlove=(PetHand)typeof(PetGame).GetField("leftVirtualHand",flags).GetValue(game);
                for(int i=0;i<8;i++){rightGlove.Pose(false,false,false,.1f,-1);leftGlove.Pose(false,false,false,.1f);}
                Vector3 rightFinger=rightGlove.FingerTip.parent.forward;
                Vector3 leftFinger=leftGlove.FingerTip.parent.forward;
                if(Vector3.Dot(rightFinger,leftFinger)<.95f)throw new Exception("Right and left glove fingers do not point in the same direction");
                game.SendMessage("TogglePack");
                var pack=GameObject.Find("Backpack behind head").transform;
                if(pack.parent!=camera.transform||tray.activeInHierarchy)throw new Exception("Backpack did not stow behind the viewer");
                left.transform.position=camera.transform.TransformPoint(new Vector3(-.14f,-.16f,-.30f));
                var grabPack=typeof(PetGame).GetMethod("TryGrabPack",flags);
                var movePack=typeof(PetGame).GetMethod("UpdatePackPose",flags);
                if(!(bool)grabPack.Invoke(game,new object[]{left.transform,1}))throw new Exception("Left reach behind shoulder did not grab backpack");
                left.transform.position=camera.transform.TransformPoint(new Vector3(-.18f,-.10f,.55f));
                for(int i=0;i<12;i++)movePack.Invoke(game,new object[]{.025f,false,true});
                if(pack.parent||!tray.activeInHierarchy)throw new Exception("Pulling backpack forward did not open world inventory");
                Vector3 placedPack=pack.position;
                movePack.Invoke(game,new object[]{.025f,false,false});
                if(Vector3.Distance(pack.position,placedPack)>.001f||!tray.activeInHierarchy)throw new Exception("Released backpack did not remain where placed");
                left.transform.position=pack.TransformPoint(new Vector3(0,.14f,-.015f));
                if(!(bool)grabPack.Invoke(game,new object[]{left.transform,1}))throw new Exception("Placed backpack could not be grabbed again by its handle");
                movePack.Invoke(game,new object[]{.025f,false,false});
                var foods=(Transform[])typeof(PetGame).GetField("foods",flags).GetValue(game);
                right.transform.position=foods[0].position;
                game.SendMessage("TryPickFood",new Ray(right.transform.position,camera.transform.forward));
                if((int)typeof(PetGame).GetField("heldFood",flags).GetValue(game)!=0||!tray.activeInHierarchy)
                    throw new Exception("Taking an item stowed the placed backpack");
                game.SendMessage("ReturnFood");
                camera.Render();texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
                File.WriteAllBytes("Art/Blender/unity-glove-preview.png",texture.EncodeToPNG());
                var pet=(PetSimulation)typeof(PetGame).GetField("pet",flags).GetValue(game);
                var props=(Transform[])typeof(PetGame).GetField("props",flags).GetValue(game);
                Vector3 target=companion.position+Vector3.up*.10f;
                props[1].position=target-Vector3.forward*.36f;
                typeof(PetGame).GetField("heldProp",flags).SetValue(game,1);
                game.SendMessage("UseHeldProp",new Ray(props[1].position,Vector3.forward));
                for(int i=0;i<8;i++)game.SendMessage("UpdateProjectiles",.02f);
                if(pet.Action!=PetAction.Angry||!(bool)typeof(PetGame).GetField("petFlying",flags).GetValue(game))
                    throw new Exception("Blaster projectile did not hit and knock back Puff");
                pet.PrepareForPlacement();typeof(PetGame).GetField("petFlying",flags).SetValue(game,false);
                props[2].position=target-Vector3.up*.15f;
                typeof(PetGame).GetField("heldProp",flags).SetValue(game,2);
                game.SendMessage("UseHeldProp",new Ray(props[2].position,Vector3.forward));
                if(pet.Action!=PetAction.Angry)throw new Exception("Foam sword did not make Puff angry");
                companion.position=new Vector3(companion.position.x,-.2f,companion.position.z);
                typeof(PetGame).GetField("petFlying",flags).SetValue(game,true);
                typeof(PetGame).GetField("petVelocity",flags).SetValue(game,Vector3.zero);
                game.SendMessage("UpdatePetFlight",.02f);
                if(companion.position.y<-.001f)throw new Exception("Pet fell below the floor fallback");
                typeof(PetGame).GetField("placed",flags).SetValue(game,false);
                typeof(PetGame).GetField("previousTrigger",flags).SetValue(game,false);
                companion.gameObject.SetActive(false);
                right.transform.position=new Vector3(0,1.5f,-.35f);
                right.transform.rotation=Quaternion.LookRotation(new Vector3(0,.76f,.85f)-right.transform.position);
                var spawn=typeof(PetGame).GetMethod("ReadStableSpawn",flags);
                spawn.Invoke(game,new object[]{false,false,false,false});
                var marker=GameObject.Find("Scanned surface placement marker");
                if(!marker||companion.gameObject.activeSelf)throw new Exception("Placement preview showed the pet instead of a marker");
                spawn.Invoke(game,new object[]{true,false,false,false});
                var ray=GameObject.Find("Faint placement ray");
                if(!ray||companion.gameObject.activeSelf)throw new Exception("Holding trigger did not show the placement ray before spawning");
                typeof(PetGame).GetField("previousTrigger",flags).SetValue(game,true);
                spawn.Invoke(game,new object[]{false,false,false,false});
                if(!companion.gameObject.activeSelf||marker.activeSelf||Mathf.Abs(companion.position.y-.772f)>.06f)
                    throw new Exception("Stable scanned-surface placement failed");
                game.SendMessage("UpdateContactShadow");
                var shadow=GameObject.Find("Soft surface contact shadow");
                if(!shadow||!shadow.activeInHierarchy)throw new Exception("Puff contact shadow missing after placement");
                var fight=(PuffFight)typeof(PetGame).GetField("fight",flags).GetValue(game);
                game.SendMessage("ClosePack");
                right.SetActive(false);left.SetActive(false);
                fight.Begin();
                for(int i=0;i<95;i++)fight.Tick(.02f);
                if(!fight.Active||!GameObject.Find("Karate Comet"))throw new Exception("Fight mode failed to spawn the first rival");
                var ring=GameObject.Find("Blue holographic arena ring");
                if(!ring)throw new Exception("Fight arena ring missing");
                game.SendMessage("UpdateHud");
                var ringLine=ring.GetComponent<LineRenderer>();
                if(ringLine.GetPosition(0).magnitude<.05f)throw new Exception("Arena ring did not expand during the entrance");
                camera.Render();texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
                File.WriteAllBytes("Art/Blender/unity-fight-preview.png",texture.EncodeToPNG());
                var rival=GameObject.Find("Karate Comet").transform;
                companion.position=rival.position+rival.forward*.18f;
                fight.Jump();for(int i=0;i<7;i++)fight.Tick(.02f);
                if(companion.position.y<=rival.position.y+.02f)throw new Exception("Fight jump did not lift Puff");
                for(int i=0;i<40;i++)fight.Tick(.02f);
                fight.Punch();for(int i=0;i<16;i++)fight.Tick(.02f);
                if(fight.EnemyHealth>=150)throw new Exception("Animated punch never reached the rival");
                companion.position=rival.position+rival.forward*.18f;
                for(int i=0;i<25;i++)fight.Tick(.02f);
                if(!fight.Grab())throw new Exception("Close grab did not hold the rival");
                float beforeThrow=fight.EnemyHealth;
                fight.Throw(companion.forward);
                if(fight.EnemyHealth>=beforeThrow)throw new Exception("Throw did not damage the rival");
                fight.ThrowBomb(companion.position+Vector3.up*.25f,Vector3.up*.5f);
                fight.LaunchRocket(companion.position+Vector3.up*.25f,camera.transform.forward,1);
                for(int i=0;i<260;i++)fight.Tick(.02f);
                fight.End();
                if(fight.Active)throw new Exception("Fight mode did not exit cleanly");
                fight.Begin();
                for(int round=1;round<3;round++)
                {
                    for(int i=0;i<70;i++)fight.Tick(.02f);
                    typeof(PuffFight).GetField("enemyHealth",flags).SetValue(fight,0f);
                    fight.Tick(.02f);
                    for(int i=0;i<130;i++)fight.Tick(.02f);
                    string next=round==1?"Clawstorm Cat":"Meatball Marauder";
                    if(!GameObject.Find(next))throw new Exception("Fight did not advance to "+next);
                    for(int i=0;i<95;i++)fight.Tick(.02f);
                    camera.Render();texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
                    File.WriteAllBytes(round==1?"Art/Blender/unity-cat-fight-preview.png":"Art/Blender/unity-meatball-fight-preview.png",texture.EncodeToPNG());
                }
                fight.End();
                var dayGoal=(CareAction)typeof(PetGame).GetField("dailyGoal",flags).GetValue(game);
                typeof(PetGame).GetField("dailyDone",flags).SetValue(game,false);
                int stars=(int)typeof(PetGame).GetField("dailyStars",flags).GetValue(game);
                var record=typeof(PetGame).GetMethod("RecordCare",flags);
                record.Invoke(game,new object[]{dayGoal,"DAILY TEST"});
                record.Invoke(game,new object[]{dayGoal,"DAILY TEST"});
                if((int)typeof(PetGame).GetField("dailyStars",flags).GetValue(game)!=stars+1)
                    throw new Exception("Daily care goal did not award exactly one star");
                camera.targetTexture=null;RenderTexture.active=null;
                UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
                Debug.Log("PET_VISUAL_OK Puff title + care HUD + stable spawn + draggable pack + holographic arena + three fight rounds + jump + punch + kick + throw");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
