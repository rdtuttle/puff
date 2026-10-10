using System.Collections.Generic;
using UnityEngine;

namespace TabletopMayhem
{
    // Self-contained tabletop encounter. All positions are relative to the placed
    // room anchor, so a fight never changes the pet's validated placement flow.
    public sealed class PuffFight
    {
        sealed class MovingEffect
        {
            public Transform Body;
            public Vector3 Velocity;
            public float Age;
            public float Fuse;
            public float Radius;
            public Color Tint;
            public int Kind; // 0 rival projectile, 1 bomb, 2 rocket, 3 spark, 4 shockwave
        }
        sealed class EnemyAvatar
        {
            public string Name,ShortName;
            public Transform Root,LeftArm,RightArm,Head,LeftLeg,RightLeg,Body;
            public Quaternion BodyRest,HeadRest,LeftArmRest,RightArmRest,LeftLegRest,RightLegRest;
            public float MaxHealth,Speed;
            public Color Spark,Projectile;
        }

        readonly Transform arena, puff, camera;
        readonly RoomWorld room;
        readonly List<MovingEffect> effects=new List<MovingEffect>();
        readonly Transform pipe,pipeRim;
        readonly EnemyAvatar[] opponents=new EnemyAvatar[3];
        EnemyAvatar current;
        Transform enemy { get { return current.Root; } }
        Transform enemyLeft { get { return current.LeftArm; } }
        Transform enemyRight { get { return current.RightArm; } }
        Transform enemyHead { get { return current.Head; } }
        Transform enemyLegLeft { get { return current.LeftLeg; } }
        Transform enemyLegRight { get { return current.RightLeg; } }
        Transform enemyBody { get { return current.Body; } }
        readonly Transform playerLeft,playerRight,playerLegLeft,playerLegRight;
        readonly Quaternion playerLeftRest,playerRightRest,playerLegLeftRest,playerLegRightRest;
        readonly Transform playerVisual;
        readonly LineRenderer outerRing,innerRing,pulseRing;
        readonly List<LineRenderer> arenaGrid=new List<LineRenderer>();
        readonly TextMesh status;
        readonly Transform puffBar,gloomBar,guardBar;
        readonly AudioSource audio;
        readonly AudioClip impact,explosion,whoosh,jumpSound,grabSound,arenaSweep;
        readonly AudioClip[] voices=new AudioClip[3];
        Vector2 move;
        float spawnTime,enemyAttack,enemyMelee,playerCooldown,playerPose,enemyPose,stun,screenTime;
        float enemyHealth=150,playerHealth=100;
        float guardEnergy=100,guardBreak;
        float jumpHeight,jumpVelocity,attackAge,hitPause,comboWindow,enemyWindup,enemyAir,enemyVertical,grabTime;
        int comboCount,wave;
        float arenaRadius;Vector3 arenaCenter;
        bool holdingEnemy,airAttack,enemyRingOut;
        bool shield,grab,finished,strikeReady;
        float strikeDuration,strikeDamage,strikeRange;
        int pose; // 1 punch, 2 kick, 3 block, 4 grab
        public bool Active { get; private set; }
        public float EnemyHealth { get { return enemyHealth; } }
        public float PlayerHealth { get { return playerHealth; } }

        public PuffFight(Transform anchor,Transform pet,Transform view,RoomWorld world)
        {
            arena=anchor;puff=pet;camera=view;room=world;
            pipe=new GameObject("Shadow gate warp pipe").transform;pipe.SetParent(arena,false);
            PetArt.Shape(pipe,PrimitiveType.Cylinder,new Vector3(0,.035f,0),new Vector3(.16f,.04f,.16f),new Color(.11f,.09f,.19f),"Gate base");
            pipeRim=PetArt.Shape(pipe,PrimitiveType.Cylinder,new Vector3(0,.07f,0),new Vector3(.185f,.016f,.185f),new Color(.48f,.23f,.69f),"Glowing gate rim").transform;
            PetArt.Shape(pipe,PrimitiveType.Cylinder,new Vector3(0,.078f,0),new Vector3(.13f,.004f,.13f),new Color(.035f,.015f,.075f),"Portal center");
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                PetArt.Shape(pipe,PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.095f,.084f,Mathf.Cos(a)*.095f),
                    Vector3.one*.013f,new Color(.77f,.27f,.98f),"Gate rune");
            }
            opponents[0]=CreateAvatar("KarateComet","Karate Comet","COMET",150,.17f,new Color(1,.57f,.28f),new Color(1,.66f,.25f));
            opponents[1]=CreateAvatar("Clawstorm","Clawstorm Cat","CAT",130,.23f,new Color(1,.79f,.37f),new Color(.98f,.45f,.20f));
            opponents[2]=CreateAvatar("MeatballMarauder","Meatball Marauder","MEATBALL",165,.11f,new Color(1,.40f,.25f),new Color(.95f,.18f,.14f));
            current=opponents[0];
            playerLeft=Find(puff,"LeftArm");playerRight=Find(puff,"RightArm");
            playerLegLeft=Find(puff,"LeftLeg");playerLegRight=Find(puff,"RightLeg");
            playerLeftRest=playerLeft?playerLeft.localRotation:Quaternion.identity;
            playerRightRest=playerRight?playerRight.localRotation:Quaternion.identity;
            playerLegLeftRest=playerLegLeft?playerLegLeft.localRotation:Quaternion.identity;
            playerLegRightRest=playerLegRight?playerLegRight.localRotation:Quaternion.identity;
            playerVisual=Find(puff,"Animation center");
            outerRing=MakeRing("Blue holographic arena ring",.007f,new Color(.18f,.70f,1,.90f));
            innerRing=MakeRing("Arena inner ring",.003f,new Color(.30f,.88f,1,.55f));
            pulseRing=MakeRing("Expanding fight start pulse",.014f,new Color(.48f,.92f,1,.85f));
            for(int i=0;i<14;i++)arenaGrid.Add(MakeRing("Arena grid line "+i,.0017f,new Color(.24f,.69f,1,.27f),2));
            var card=new GameObject("Neon combat score").transform;card.SetParent(camera,false);card.localPosition=new Vector3(.30f,.25f,.82f);card.localScale=Vector3.one*.62f;
            PetArt.Shape(card,PrimitiveType.Cube,Vector3.zero,new Vector3(.53f,.13f,.007f),new Color(.23f,.72f,1),"Neon score edge");
            PetArt.Shape(card,PrimitiveType.Cube,new Vector3(0,0,-.005f),new Vector3(.52f,.12f,.004f),new Color(.06f,.07f,.16f),"Dark translucent score face");
            status=PetArt.Text(card,"ROUND 1   PUFF 100  /  COMET 150",new Vector3(0,.027f,-.011f),.0032f,PetArt.Cream);
            status.fontStyle=FontStyle.Bold;
            PetArt.Shape(card,PrimitiveType.Cube,new Vector3(-.125f,-.017f,-.009f),new Vector3(.21f,.018f,.003f),new Color(.24f,.27f,.39f),"Puff health track");
            PetArt.Shape(card,PrimitiveType.Cube,new Vector3(.125f,-.017f,-.009f),new Vector3(.21f,.018f,.003f),new Color(.24f,.27f,.39f),"Rival health track");
            puffBar=PetArt.Shape(card,PrimitiveType.Cube,new Vector3(-.125f,-.017f,-.013f),new Vector3(.21f,.016f,.003f),PetArt.Pink,"Puff fight health").transform;
            gloomBar=PetArt.Shape(card,PrimitiveType.Cube,new Vector3(.125f,-.017f,-.013f),new Vector3(.21f,.016f,.003f),new Color(.96f,.37f,.41f),"Rival fight health").transform;
            PetArt.Shape(card,PrimitiveType.Cube,new Vector3(0,-.049f,-.009f),new Vector3(.46f,.008f,.003f),new Color(.18f,.24f,.34f),"Shield energy track");
            guardBar=PetArt.Shape(card,PrimitiveType.Cube,new Vector3(0,-.049f,-.013f),new Vector3(.46f,.007f,.003f),new Color(.38f,.88f,1),"Shield energy").transform;
            audio=pipe.gameObject.AddComponent<AudioSource>();audio.spatialBlend=.6f;
            impact=Noise("Punch impact",135,.22f,.55f);
            explosion=Noise("Arcade explosion",78,.55f,.82f);
            whoosh=Noise("Fast swipe",420,.19f,.68f);jumpSound=Tone("Jump pop",580,.18f);
            grabSound=Tone("Grab and throw",210,.27f);arenaSweep=Tone("Arena sweep",360,.55f);
            voices[0]=Tone("Karate shout",210,.22f);voices[1]=Tone("Cat yowl",450,.30f);voices[2]=Tone("Meatball rumble",115,.35f);
            pipe.gameObject.SetActive(false);foreach(var fighter in opponents)fighter.Root.gameObject.SetActive(false);
            card.gameObject.SetActive(false);SetArenaVisible(false);
        }
        static Transform Find(Transform root,string name)
        {foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
        EnemyAvatar CreateAvatar(string resource,string name,string shortName,float health,float speed,Color spark,Color projectile)
        {
            var root=PetArt.Fighter(arena,resource,name);
            var avatar=new EnemyAvatar{Root=root,Name=name,ShortName=shortName,MaxHealth=health,Speed=speed,Spark=spark,Projectile=projectile,
                Body=Find(root,"Body"),Head=Find(root,"Head"),LeftArm=Find(root,"LeftArm"),RightArm=Find(root,"RightArm"),
                LeftLeg=Find(root,"LeftLeg"),RightLeg=Find(root,"RightLeg")};
            avatar.BodyRest=avatar.Body?avatar.Body.localRotation:Quaternion.identity;
            avatar.HeadRest=avatar.Head?avatar.Head.localRotation:Quaternion.identity;
            avatar.LeftArmRest=avatar.LeftArm?avatar.LeftArm.localRotation:Quaternion.identity;
            avatar.RightArmRest=avatar.RightArm?avatar.RightArm.localRotation:Quaternion.identity;
            avatar.LeftLegRest=avatar.LeftLeg?avatar.LeftLeg.localRotation:Quaternion.identity;
            avatar.RightLegRest=avatar.RightLeg?avatar.RightLeg.localRotation:Quaternion.identity;
            return avatar;
        }
        LineRenderer MakeRing(string name,float width,Color color,int points=72)
        {
            var go=new GameObject(name);go.transform.SetParent(arena,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=points;line.loop=points>2;
            line.widthMultiplier=width;line.numCornerVertices=2;line.numCapVertices=2;
            line.material=new Material(Resources.Load<Shader>("PetAimLine"));line.startColor=line.endColor=color;
            return line;
        }
        void SetArenaVisible(bool show)
        {
            outerRing.gameObject.SetActive(show);innerRing.gameObject.SetActive(show);pulseRing.gameObject.SetActive(show);
            foreach(var line in arenaGrid)line.gameObject.SetActive(show);
        }
        void DrawRing(LineRenderer line,float radius,float y)
        {
            for(int i=0;i<line.positionCount;i++)
            {
                float a=i*2*Mathf.PI/line.positionCount;
                line.SetPosition(i,new Vector3(arenaCenter.x+Mathf.Cos(a)*radius,y,arenaCenter.z+Mathf.Sin(a)*radius));
            }
        }
        float FindArenaRadius()
        {
            for(float radius=.70f;radius>=.34f;radius-=.045f)
            {
                bool supported=true;
                for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8;float h;
                    if(!room.Ground(arenaCenter.x+Mathf.Cos(a)*radius,arenaCenter.z+Mathf.Sin(a)*radius,arenaCenter.y+.45f,out h)||Mathf.Abs(h-arenaCenter.y)>.09f)
                    {supported=false;break;}
                }
                if(supported)return radius;
            }
            return .35f;
        }
        void SetArenaGrid()
        {
            for(int i=0;i<arenaGrid.Count;i++)
            {
                bool alongX=i<7;int index=i%7-3;float offset=index*arenaRadius/4f;
                float reach=Mathf.Sqrt(Mathf.Max(0,arenaRadius*arenaRadius-offset*offset));
                var line=arenaGrid[i];float y=arenaCenter.y+.012f;
                line.SetPosition(0,new Vector3(arenaCenter.x+(alongX?-reach:offset),y,arenaCenter.z+(alongX?offset:-reach)));
                line.SetPosition(1,new Vector3(arenaCenter.x+(alongX?reach:offset),y,arenaCenter.z+(alongX?offset:reach)));
            }
        }
        void UpdateArena()
        {
            float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01(spawnTime/.85f));
            float y=arenaCenter.y+.014f;
            DrawRing(outerRing,Mathf.Max(.004f,arenaRadius*reveal),y);
            DrawRing(innerRing,Mathf.Max(.004f,arenaRadius*.69f*reveal),y+.003f);
            float pulse=Mathf.Clamp01(spawnTime/.70f);
            DrawRing(pulseRing,Mathf.Max(.004f,arenaRadius*pulse),y+.006f);
            Color c=new Color(.53f,.91f,1,.8f*(1-pulse));pulseRing.startColor=pulseRing.endColor=c;
            foreach(var line in arenaGrid)line.enabled=spawnTime>.45f;
        }
        static AudioClip Tone(string name,float hz,float seconds)
        {
            const int rate=22050;int n=(int)(rate*seconds);float[] samples=new float[n];
            for(int i=0;i<n;i++){float t=(float)i/rate;float e=Mathf.Exp(-t*12);samples[i]=(Mathf.Sin(t*hz*6.28318f*(1-t*.55f))*.7f+Mathf.Sin(t*hz*2.01f*6.28318f)*.3f)*e*.24f;}
            var c=AudioClip.Create(name,n,1,rate,false);c.SetData(samples,0);return c;
        }
        static AudioClip Noise(string name,float hz,float seconds,float noiseMix)
        {
            const int rate=22050;int count=(int)(seconds*rate);float[] samples=new float[count];uint seed=2463534242u;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate,p=(float)i/count;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;
                float hiss=(seed&65535)/32768f-1f;
                float body=Mathf.Sin(t*hz*6.28318f*(1-p*.75f));
                float envelope=Mathf.Pow(1-p,2.2f)*(name.Contains("swipe")?Mathf.Min(1,p*22):1);
                samples[i]=Mathf.Clamp((body*(1-noiseMix)+hiss*noiseMix)*envelope*.45f,-1,1);
            }
            var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void Begin()
        {
            if(Active)return;
            Active=true;finished=false;wave=0;current=opponents[wave];enemyHealth=current.MaxHealth;playerHealth=100;
            spawnTime=0;enemyAttack=2.8f;enemyMelee=1.3f;stun=0;pose=0;playerPose=0;guardEnergy=100;guardBreak=0;
            jumpHeight=jumpVelocity=enemyAir=enemyVertical=hitPause=enemyWindup=grabTime=0;
            holdingEnemy=enemyRingOut=airAttack=false;comboCount=0;comboWindow=0;
            move=Vector2.zero;shield=grab=false;
            strikeReady=false;
            Vector3 side=camera.right;side.y=0;
            if(side.sqrMagnitude<.001f)side=arena.right;
            Vector3 local=arena.InverseTransformDirection(side.normalized);
            Vector3 chosen=puff.localPosition+local*.25f;
            for(float radius=.60f;radius>=.25f;radius-=.05f)
            {
                float x=puff.localPosition.x+local.x*radius,z=puff.localPosition.z+local.z*radius,h;
                if(room.Ground(x,z,puff.localPosition.y+.35f,out h)&&Mathf.Abs(h-puff.localPosition.y)<.08f)
                {chosen=new Vector3(x,h,z);break;}
            }
            pipe.localPosition=chosen;
            pipe.localScale=Vector3.one;
            arenaCenter=(puff.localPosition+pipe.localPosition)*.5f;arenaCenter.y=pipe.localPosition.y;
            arenaRadius=FindArenaRadius();SetArenaGrid();SetArenaVisible(true);UpdateArena();
            audio.PlayOneShot(arenaSweep,.75f);
            enemy.localPosition=pipe.localPosition+Vector3.down*.45f;
            enemy.gameObject.SetActive(true);pipe.gameObject.SetActive(true);status.transform.parent.gameObject.SetActive(true);
            status.text="ROUND 1  /  KARATE COMET";
            Debug.Log("PUFF_FIGHT_BEGIN");
        }
        public void End()
        {
            if(!Active)return;Active=false;move=Vector2.zero;shield=false;strikeReady=false;holdingEnemy=false;
            pipe.gameObject.SetActive(false);foreach(var fighter in opponents)fighter.Root.gameObject.SetActive(false);
            SetArenaVisible(false);status.transform.parent.gameObject.SetActive(false);
            foreach(var e in effects)if(e.Body)Object.Destroy(e.Body.gameObject);effects.Clear();
            if(playerLeft)playerLeft.localRotation=playerLeftRest;
            if(playerRight)playerRight.localRotation=playerRightRest;
            if(playerLegLeft)playerLegLeft.localRotation=playerLegLeftRest;
            if(playerLegRight)playerLegRight.localRotation=playerLegRightRest;
            if(playerVisual){playerVisual.localRotation=Quaternion.identity;playerVisual.localScale=Vector3.one;}
            Debug.Log("PUFF_FIGHT_END");
        }
        void NextWave()
        {
            if(wave>=opponents.Length-1)return;
            enemy.gameObject.SetActive(false);wave++;current=opponents[wave];enemyHealth=current.MaxHealth;
            playerHealth=Mathf.Min(100,playerHealth+22);guardEnergy=100;
            spawnTime=0;enemyAttack=2.8f;enemyMelee=1.3f;enemyWindup=enemyAir=enemyVertical=0;
            pipe.localScale=Vector3.one;
            stun=0;finished=false;holdingEnemy=false;enemyRingOut=false;strikeReady=false;
            enemy.localPosition=pipe.localPosition+Vector3.down*.45f;enemy.rotation=Quaternion.identity;
            enemy.gameObject.SetActive(true);status.text="ROUND "+(wave+1)+"  /  "+current.Name.ToUpperInvariant();
            foreach(var e in effects)if(e.Body)Object.Destroy(e.Body.gameObject);effects.Clear();
            audio.PlayOneShot(arenaSweep,.75f);Burst(pipe.position+Vector3.up*.15f,current.Spark,16);
            Debug.Log("PUFF_FIGHT_ROUND "+(wave+1)+" "+current.Name);
        }
        public void Controls(Vector2 movement,bool guard,bool grabHeld)
        {
            move=Vector2.ClampMagnitude(movement,1);shield=guard&&spawnTime>1.2f&&!finished&&guardEnergy>0&&guardBreak<=0&&!holdingEnemy;
            grab=grabHeld;
            if(shield){pose=3;playerPose=.15f;}
        }
        public void Jump()
        {
            if(!Active||finished||spawnTime<1.2f||jumpHeight>.002f||jumpVelocity>.001f||holdingEnemy)return;
            jumpVelocity=1.35f;playerPose=.30f;pose=5;audio.PlayOneShot(jumpSound,.55f);Debug.Log("PUFF_FIGHT_JUMP");
        }
        public void Punch(){Attack(1,comboWindow>0?15:12,.34f);}
        public void Kick(){Attack(2,21,.41f);}
        void Attack(int kind,float damage,float range)
        {
            if(!Active||finished||spawnTime<1.2f||playerCooldown>0||shield)return;
            if(kind==1){comboCount=comboWindow>0?(comboCount%3)+1:1;comboWindow=.65f;}
            else {comboCount=0;comboWindow=0;}
            pose=kind;strikeDuration=kind==1?(comboCount==3?.43f:.33f):.51f;playerPose=strikeDuration;attackAge=0;
            playerCooldown=strikeDuration+(kind==1?.035f:.12f);strikeDamage=damage+(comboCount==3?5:0);strikeRange=range;
            strikeReady=true;airAttack=jumpHeight>.035f;audio.PlayOneShot(whoosh,kind==1?.42f:.66f);
        }
        void ResolveStrike()
        {
            strikeReady=false;
            Vector3 delta=enemy.position-puff.position;delta.y=0;
            if(delta.magnitude<strikeRange&&enemyHealth>0)
            {
                enemyHealth=Mathf.Max(0,enemyHealth-strikeDamage);
                Vector3 push=delta.sqrMagnitude>.001f?delta.normalized:arena.forward;
                enemy.position+=push*(pose==1?.045f:.095f);stun=pose==1?.30f:.46f;enemyPose=.36f;
                enemyVertical=pose==2||comboCount==3?.85f:.34f;hitPause=.045f;
                Burst(enemy.position+Vector3.up*.16f,current.Spark,pose==2?12:7);
                Shockwave(enemy.position+Vector3.up*.16f,pose==2?.20f:.13f,current.Spark);
                audio.PlayOneShot(impact,pose==2?.9f:.7f);
                audio.PlayOneShot(voices[wave],.35f);
                Debug.Log("PUFF_FIGHT_HIT "+pose+" enemy="+enemyHealth);
            }
            else Debug.Log("PUFF_FIGHT_WHIFF "+pose);
        }
        public bool Grab()
        {
            if(!Active||finished||spawnTime<1.2f||holdingEnemy||playerCooldown>0)return false;
            Vector3 d=enemy.position-puff.position;d.y=0;
            if(d.magnitude>.31f||enemyAir>.08f)return false;
            holdingEnemy=true;grabTime=0;stun=2.5f;playerCooldown=.25f;pose=4;playerPose=.45f;
            Burst(enemy.position+Vector3.up*.30f,PetArt.Gold,8);audio.PlayOneShot(grabSound,.65f);
            Debug.Log("PUFF_FIGHT_GRAB");return true;
        }
        public void Throw(Vector3 direction)
        {
            if(!holdingEnemy)return;
            holdingEnemy=false;grabTime=0;enemyHealth=Mathf.Max(0,enemyHealth-14);
            Vector3 flat=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            if(flat.sqrMagnitude<.01f)flat=puff.forward;
            enemy.position=puff.position+flat*.42f+Vector3.up*.12f;
            enemyVertical=1.4f;enemyAir=.12f;stun=1.25f;playerPose=.35f;pose=4;
            Burst(enemy.position+Vector3.up*.25f,PetArt.Gold,12);audio.PlayOneShot(grabSound,.85f);
            Debug.Log("PUFF_FIGHT_THROW enemy="+enemyHealth);
        }
        public void ThrowBomb(Vector3 origin,Vector3 velocity){Spawn(origin,velocity,5,1,new Color(.17f,.16f,.21f),.055f);}
        public void HealPlayer(float amount)
        {
            if(!Active||finished)return;
            playerHealth=Mathf.Min(100,playerHealth+amount);
            Burst(puff.position+Vector3.up*.18f,new Color(.54f,1,.61f),6);
            Debug.Log("PUFF_FIGHT_SNACK_HEAL health="+playerHealth);
        }
        public void LaunchRocket(Vector3 origin,Vector3 direction,float charge)
        {
            Spawn(origin,direction.normalized*(2.4f+3.1f*Mathf.Clamp01(charge)),2.2f,2,PetArt.Gold,.035f);
            Debug.Log("PUFF_ROCKET_LAUNCHED charge="+charge.ToString("F2"));
        }
        void Spawn(Vector3 origin,Vector3 velocity,float fuse,int kind,Color color,float size)
        {
            var ball=PetArt.Shape(null,kind==2?PrimitiveType.Capsule:PrimitiveType.Sphere,origin,Vector3.one*size,color,
                kind==1?"Five second bomb":kind==2?"Slingshot rocket":wave==2?"Spicy sauce orb":"Karate energy blast").transform;
            if(kind==1)PetArt.Shape(ball,PrimitiveType.Sphere,new Vector3(0,.6f,0),Vector3.one*.23f,PetArt.Gold,"Lit bomb fuse");
            if(kind==2)
            {
                PetArt.Shape(ball,PrimitiveType.Sphere,new Vector3(0,.62f,0),new Vector3(.7f,.55f,.7f),PetArt.Cream,"Rocket nose");
                PetArt.Shape(ball,PrimitiveType.Sphere,new Vector3(0,-.64f,0),new Vector3(.55f,.8f,.55f),PetArt.Pink,"Rocket flame");
            }
            effects.Add(new MovingEffect{Body=ball,Velocity=velocity,Fuse=fuse,Kind=kind});
        }
        void Burst(Vector3 at,Color color,int count)
        {
            for(int i=0;i<count&&effects.Count<64;i++)
            {
                var p=PetArt.Shape(null,PrimitiveType.Sphere,at,Vector3.one*.013f,color,"Combat spark").transform;
                effects.Add(new MovingEffect{Body=p,Velocity=Random.insideUnitSphere*.55f+Vector3.up*.25f,Fuse=.45f,Kind=3});
            }
        }
        void Shockwave(Vector3 at,float radius,Color color)
        {
            var root=new GameObject("Combat shockwave").transform;root.position=at;
            var line=root.gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=36;
            line.widthMultiplier=.007f;line.material=new Material(Resources.Load<Shader>("PetAimLine"));
            line.startColor=line.endColor=color;
            effects.Add(new MovingEffect{Body=root,Fuse=.30f,Radius=radius,Tint=color,Kind=4});
        }
        void Explode(Vector3 at,float radius)
        {
            Burst(at,new Color(1,.59f,.18f),26);Shockwave(at,radius,new Color(1,.65f,.25f,.90f));audio.PlayOneShot(explosion,.9f);
            if(Vector3.Distance(at,enemy.position+Vector3.up*.2f)<radius)
            {enemyHealth=Mathf.Max(0,enemyHealth-36);stun=.8f;enemyPose=.5f;enemy.position+=(enemy.position-at).normalized*.13f;}
            if(Vector3.Distance(at,puff.position+Vector3.up*.1f)<radius*.7f)
                DamagePlayer(16,(puff.position-at).normalized);
            Debug.Log("PUFF_FIGHT_EXPLOSION enemy="+enemyHealth);
        }
        void DamagePlayer(float damage,Vector3 away)
        {
            if(shield)
            {
                guardEnergy=Mathf.Max(0,guardEnergy-22);
                Burst(puff.position+Vector3.up*.13f,new Color(.46f,.89f,1),5);
                if(guardEnergy<=0){shield=false;guardBreak=1.5f;Debug.Log("PUFF_SHIELD_BROKEN");}
                return;
            }
            playerHealth=Mathf.Max(0,playerHealth-damage);
            puff.position+=new Vector3(away.x,0,away.z)*.055f;
            playerPose=.3f;pose=2;audio.PlayOneShot(impact,.55f);
            Debug.Log("PUFF_FIGHT_PLAYER_HIT health="+playerHealth);
        }
        public void Tick(float dt)
        {
            if(!Active)return;
            dt=Mathf.Min(dt,.05f);
            if(hitPause>0){hitPause-=dt;dt*=.15f;}
            spawnTime+=dt;playerCooldown=Mathf.Max(0,playerCooldown-dt);playerPose=Mathf.Max(0,playerPose-dt);
            pipe.localScale=Vector3.one*Mathf.Lerp(1,.02f,Mathf.Clamp01((spawnTime-1.15f)/.55f));
            UpdateArena();
            attackAge+=dt;comboWindow=Mathf.Max(0,comboWindow-dt);
            enemyPose=Mathf.Max(0,enemyPose-dt);stun=Mathf.Max(0,stun-dt);
            guardBreak=Mathf.Max(0,guardBreak-dt);
            if(jumpVelocity!=0||jumpHeight>0)
            {
                jumpHeight+=jumpVelocity*dt;jumpVelocity-=3.4f*dt;
                if(jumpHeight<=0){jumpHeight=0;jumpVelocity=0;audio.PlayOneShot(jumpSound,.25f);Burst(puff.position,PetArt.Cream,4);}
            }
            guardEnergy=Mathf.Clamp(guardEnergy+(shield?-23f:guardBreak>0?0:12f)*dt,0,100);
            if(shield&&guardEnergy<=0){shield=false;guardBreak=1.5f;Debug.Log("PUFF_SHIELD_BROKEN");}
            if(strikeReady&&attackAge>=strikeDuration*(pose==1?.43f:.51f)&&!finished)ResolveStrike();
            if(spawnTime<1.2f)
            {enemy.localPosition=pipe.localPosition+Vector3.up*(-.45f+.45f*Mathf.SmoothStep(0,1,spawnTime/1.2f));}
            else if(!finished)
            {
                Vector3 forward=camera.forward;forward.y=0;forward.Normalize();
                Vector3 right=camera.right;right.y=0;right.Normalize();
                Vector3 travel=forward*move.y+right*move.x;
                if(travel.sqrMagnitude>.0001f)
                {
                    Vector3 target=puff.position+travel.normalized*dt*.42f;
                    float h;Vector3 local=arena.InverseTransformPoint(target);
                    if(Vector2.Distance(new Vector2(local.x,local.z),new Vector2(arenaCenter.x,arenaCenter.z))<arenaRadius-.05f&&
                       room.Ground(local.x,local.z,pipe.localPosition.y+.5f,out h)&&Mathf.Abs(h-pipe.localPosition.y)<.10f)
                        puff.localPosition=new Vector3(local.x,h+jumpHeight,local.z);
                }
                Vector3 groundedPuff=puff.localPosition;float puffGround;
                if(room.Ground(groundedPuff.x,groundedPuff.z,pipe.localPosition.y+.5f,out puffGround)&&Mathf.Abs(puffGround-pipe.localPosition.y)<.10f)
                    groundedPuff.y=puffGround+jumpHeight;
                else groundedPuff.y=pipe.localPosition.y+jumpHeight;
                puff.localPosition=groundedPuff;
                if(holdingEnemy)
                {
                    grabTime+=dt;enemy.position=puff.position+puff.forward*.22f+Vector3.up*.20f;
                    enemy.rotation=Quaternion.Slerp(enemy.rotation,Quaternion.LookRotation(-puff.forward),1-Mathf.Exp(-dt*12));
                    if(grabTime>2.2f)Throw(puff.forward);
                }
                else if(enemyAir>0||enemyVertical>0)
                {
                    enemyAir+=enemyVertical*dt;enemyVertical-=3.1f*dt;
                    Vector3 p=enemy.localPosition;float h;
                    bool supported=room.Ground(p.x,p.z,pipe.localPosition.y+.5f,out h)&&Mathf.Abs(h-pipe.localPosition.y)<.10f;
                    if(enemyAir<=0&&supported){enemyAir=0;enemyVertical=0;p.y=h;audio.PlayOneShot(impact,.23f);}
                    else p.y=(supported?h:pipe.localPosition.y)+enemyAir;
                    enemy.localPosition=p;
                    Vector2 offset=new Vector2(p.x-arenaCenter.x,p.z-arenaCenter.z);
                    if((!supported&&p.y<pipe.localPosition.y-.25f)||offset.magnitude>arenaRadius+.15f)
                    {enemyHealth=0;enemyRingOut=true;Burst(pipe.position+Vector3.up*.12f,PetArt.Gold,18);Debug.Log("PUFF_FIGHT_RING_OUT");}
                }
                Vector3 toward=puff.position-enemy.position;toward.y=0;
                float distance=toward.magnitude;
                if(toward.sqrMagnitude>.001f)
                {
                    enemy.rotation=Quaternion.Slerp(enemy.rotation,Quaternion.LookRotation(toward),1-Mathf.Exp(-dt*8));
                    if(distance>.22f&&stun<=0&&!holdingEnemy&&enemyAir<=0)
                    {
                        Vector3 next=enemy.position+toward.normalized*dt*current.Speed;
                        var local=arena.InverseTransformPoint(next);float h;
                        if(Vector2.Distance(new Vector2(local.x,local.z),new Vector2(arenaCenter.x,arenaCenter.z))<arenaRadius-.06f&&
                           room.Ground(local.x,local.z,pipe.localPosition.y+.5f,out h)&&Mathf.Abs(h-pipe.localPosition.y)<.10f)
                            enemy.localPosition=new Vector3(local.x,h,local.z);
                    }
                }
                enemyAttack-=dt;enemyMelee-=dt;
                if(enemyAttack<=0&&enemyWindup<=0&&stun<=0&&!holdingEnemy)
                {enemyWindup=wave==1?.36f:wave==2?.68f:.48f;enemyPose=enemyWindup;audio.PlayOneShot(voices[wave],.55f);Debug.Log("PUFF_ENEMY_TELEGRAPH "+current.Name);}
                if(enemyWindup>0)
                {
                    enemyWindup-=dt;
                    if(enemyWindup<=0&&stun<=0&&!holdingEnemy)
                    {
                        enemyAttack=wave==1?Random.Range(2.1f,3.2f):wave==2?Random.Range(3.1f,4.5f):Random.Range(2.5f,3.7f);
                        Vector3 from=enemy.position+Vector3.up*.30f;
                        Vector3 aim=(puff.position+Vector3.up*(.12f+jumpHeight)-from).normalized;
                        if(wave==1)
                        {
                            enemyVertical=1.65f;enemyAir=.02f;enemy.position+=toward.normalized*Mathf.Min(.25f,distance*.45f);
                            Burst(enemy.position+Vector3.up*.10f,current.Spark,7);audio.PlayOneShot(jumpSound,.66f);
                            Debug.Log("PUFF_CAT_POUNCE");
                        }
                        else
                        {
                            Spawn(from,aim*(wave==2?1.05f:1.50f),2.3f,0,current.Projectile,wave==2?.055f:.037f);
                            audio.PlayOneShot(whoosh,.55f);Debug.Log(wave==2?"PUFF_MEATBALL_SAUCE":"PUFF_KARATE_BLAST");
                        }
                    }
                }
                if(distance<.20f&&enemyMelee<=0&&stun<=0&&!holdingEnemy&&jumpHeight<.08f)
                {enemyMelee=wave==1?.88f:wave==2?1.70f:1.28f;enemyPose=.34f;DamagePlayer(wave==2?12:wave==1?7:9,toward.normalized);}
                if(enemyHealth<=0||playerHealth<=0)
                {finished=true;screenTime=2.5f;status.text=enemyHealth<=0?(enemyRingOut?"RING OUT!":"K.O.!  "+current.Name.ToUpperInvariant()):"PUFF NEEDS A BREAK";
                 Burst(enemyHealth<=0?enemy.position:puff.position,current.Spark,18);audio.PlayOneShot(explosion,.55f);
                 Debug.Log("PUFF_FIGHT_RESULT "+(enemyHealth<=0?"WIN":"LOSS")+" round="+(wave+1));}
            }
            if(finished)
            {
                screenTime-=dt;
                if(screenTime<=0&&enemyHealth<=0&&wave<opponents.Length-1)NextWave();
                else if(screenTime<=0&&enemyHealth<=0)status.text="PUFF CLEARED THE ARENA!";
            }
            if(!finished&&spawnTime>=1.2f)status.text=holdingEnemy?"GRABBED! RELEASE GRIP TO THROW":
                "ROUND "+(wave+1)+"   PUFF "+Mathf.CeilToInt(playerHealth)+"  /  "+current.ShortName+" "+Mathf.CeilToInt(enemyHealth);
            float pWidth=.21f*Mathf.Clamp01(playerHealth/100f),eWidth=.21f*Mathf.Clamp01(enemyHealth/current.MaxHealth);
            puffBar.localScale=new Vector3(Mathf.Max(.001f,pWidth),.016f,.003f);
            puffBar.localPosition=new Vector3(-.23f+pWidth*.5f,-.017f,-.013f);
            gloomBar.localScale=new Vector3(Mathf.Max(.001f,eWidth),.016f,.003f);
            gloomBar.localPosition=new Vector3(.02f+eWidth*.5f,-.017f,-.013f);
            float gWidth=.46f*guardEnergy/100f;
            guardBar.localScale=new Vector3(Mathf.Max(.001f,gWidth),.007f,.003f);
            guardBar.localPosition=new Vector3(-.23f+gWidth*.5f,-.049f,-.013f);
            if(shield&&playerPose<=0){pose=3;playerPose=.1f;}
            Vector3 face=enemy.position-puff.position;face.y=0;
            if(face.sqrMagnitude>.001f)puff.rotation=Quaternion.Slerp(puff.rotation,Quaternion.LookRotation(face),1-Mathf.Exp(-dt*12));
            float duration=pose==1||pose==2?Mathf.Max(.01f,strikeDuration):.45f;
            float normalized=playerPose>0?Mathf.Clamp01(1-playerPose/duration):1;
            float phase=normalized<.30f?-.30f*Mathf.Sin(normalized/.30f*Mathf.PI*.5f):normalized<.64f?Mathf.Sin((normalized-.30f)/.34f*Mathf.PI*.5f):Mathf.Cos((normalized-.64f)/.36f*Mathf.PI*.5f);
            phase=playerPose>0?phase:0;
            if(playerRight)playerRight.localRotation=playerRightRest*Quaternion.Euler(pose==1?-125*phase:pose==3?-48:pose==4?-80:pose==5?-30:0,0,pose==2?75*phase:0);
            if(playerLeft)playerLeft.localRotation=playerLeftRest*Quaternion.Euler(pose==2?-42*phase:pose==3?-48:pose==4?-80:pose==5?-30:0,0,pose==2?-65*phase:0);
            if(playerLegRight)playerLegRight.localRotation=playerLegRightRest*Quaternion.Euler(pose==2?-120*phase:pose==5?-35:0,0,pose==2?28*phase:0);
            if(playerLegLeft)playerLegLeft.localRotation=playerLegLeftRest*Quaternion.Euler(pose==2?35*phase:pose==5?35:0,0,0);
            if(playerVisual)
            {
                float lean=pose==1?-17*phase:pose==2?-28*phase:pose==3?11:pose==4?-12:0;
                float twist=pose==2?28*phase:pose==1?12*phase:0;
                playerVisual.localRotation=Quaternion.Slerp(playerVisual.localRotation,Quaternion.Euler(lean,twist,-8*phase),1-Mathf.Exp(-dt*19));
                float squash=pose==2?1+.13f*phase:pose==1?1+.07f*phase:1;
                playerVisual.localScale=Vector3.Lerp(playerVisual.localScale,new Vector3(1/squash,squash,1/squash),1-Mathf.Exp(-dt*16));
            }
            if(enemyRight)enemyRight.localRotation=current.RightArmRest*Quaternion.Euler(enemyPose>0?-85*Mathf.Sin(enemyPose*8):0,0,wave==1?-22:0);
            if(enemyLeft)enemyLeft.localRotation=current.LeftArmRest*Quaternion.Euler(enemyPose>0?65*Mathf.Sin(enemyPose*8):0,0,wave==1?22:0);
            if(enemyHead)enemyHead.localRotation=current.HeadRest*Quaternion.Euler(0,0,Mathf.Sin(Time.time*2)*5+enemyPose*15);
            if(enemyBody)enemyBody.localRotation=current.BodyRest*Quaternion.Euler(enemyPose>0?enemyPose*22:0,0,Mathf.Sin(Time.time*(wave==1?4:2.1f))*3);
            if(enemyLegLeft)enemyLegLeft.localRotation=current.LeftLegRest*Quaternion.Euler(Mathf.Sin(Time.time*(wave==1?13:8))*9,0,0);
            if(enemyLegRight)enemyLegRight.localRotation=current.RightLegRest*Quaternion.Euler(-Mathf.Sin(Time.time*(wave==1?13:8))*9,0,0);
            pipeRim.localScale=new Vector3(.185f*(1+Mathf.Sin(Time.time*6)*.06f),.016f,.185f*(1+Mathf.Sin(Time.time*6)*.06f));
            UpdateEffects(dt);
        }
        void UpdateEffects(float dt)
        {
            for(int i=effects.Count-1;i>=0;i--)
            {
                var e=effects[i];e.Age+=dt;e.Fuse-=dt;
                if(e.Kind==3)
                {e.Body.position+=e.Velocity*dt;e.Velocity+=Physics.gravity*dt*.3f;e.Body.localScale=Vector3.one*.013f*Mathf.Clamp01(e.Fuse/.45f);}
                else if(e.Kind==4)
                {
                    var line=e.Body.GetComponent<LineRenderer>();float p=Mathf.Clamp01(e.Age/.30f),r=e.Radius*p;
                    Color fade=e.Tint;fade.a*=1-p;line.startColor=line.endColor=fade;
                    for(int j=0;j<line.positionCount;j++)
                    {float a=j*2*Mathf.PI/line.positionCount;line.SetPosition(j,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}
                }
                else
                {
                    Vector3 old=e.Body.position;e.Velocity+=Physics.gravity*dt*(e.Kind==2?.12f:.55f);
                    Vector3 next=old+e.Velocity*dt;RaycastHit hit;Vector3 step=next-old;
                    if(step.sqrMagnitude>.000001f&&Physics.SphereCast(old,.02f,step.normalized,out hit,step.magnitude,RoomWorld.RoomMask))
                    {
                        next=hit.point+hit.normal*.025f;
                        if(e.Kind==2)e.Fuse=0;
                        else e.Velocity=Vector3.Reflect(e.Velocity,hit.normal)*.38f;
                    }
                    e.Body.position=next;
                    if(e.Kind==0&&Vector3.Distance(next,puff.position+Vector3.up*.10f)<.13f)
                    {DamagePlayer(wave==2?15:11,(puff.position-next).normalized);Burst(next,current.Projectile,10);e.Fuse=0;}
                    if(e.Kind==2&&e.Velocity.sqrMagnitude>.001f)e.Body.rotation=Quaternion.FromToRotation(Vector3.up,e.Velocity.normalized);
                    if(e.Kind==1)for(int c=0;c<e.Body.childCount;c++)if(c==0)
                        e.Body.GetChild(c).localScale=Vector3.one*(.20f+.1f*Mathf.Sin(Time.time*18));
                }
                if(e.Fuse<=0)
                {
                    if(e.Kind==1||e.Kind==2)Explode(e.Body.position,e.Kind==1?.46f:.38f);
                    Object.Destroy(e.Body.gameObject);effects.RemoveAt(i);
                }
            }
        }
    }
}
