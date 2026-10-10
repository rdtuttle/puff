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
            public int Kind; // 0 slime, 1 bomb, 2 rocket, 3 burst
        }

        readonly Transform arena, puff, camera;
        readonly RoomWorld room;
        readonly List<MovingEffect> effects=new List<MovingEffect>();
        readonly Transform enemy,pipe,pipeRim,enemyLeft,enemyRight,enemyHead,enemyLegLeft,enemyLegRight,enemyBody;
        readonly Transform playerLeft,playerRight,playerLegLeft,playerLegRight;
        readonly TextMesh status;
        readonly Transform puffBar,gloomBar,guardBar;
        readonly AudioSource audio;
        readonly AudioClip impact,explosion,whoosh,jumpSound,grabSound,beanVoice;
        Vector2 move;
        float spawnTime,enemyAttack,enemyMelee,playerCooldown,playerPose,enemyPose,stun,screenTime;
        float enemyHealth=150,playerHealth=100;
        float guardEnergy=100,guardBreak;
        float jumpHeight,jumpVelocity,attackAge,hitPause,comboWindow,enemyWindup,enemyAir,enemyVertical,grabTime;
        int comboCount;
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
            enemy=new GameObject("The Bean Revenant").transform;enemy.SetParent(arena,false);
            var bean=new Color(.42f,.16f,.10f);var warm=new Color(.67f,.32f,.20f);var flesh=new Color(.74f,.47f,.34f);var shadow=new Color(.16f,.08f,.08f);var eyes=new Color(.98f,.66f,.25f);
            enemyBody=PetArt.Shape(enemy,PrimitiveType.Sphere,new Vector3(0,.265f,0),new Vector3(.24f,.36f,.19f),bean,"Glossy kidney bean body").transform;
            PetArt.Shape(enemyBody,PrimitiveType.Sphere,new Vector3(-.30f,.22f,-.12f),new Vector3(.52f,.62f,.32f),warm,"Bean highlight");
            enemyHead=new GameObject("Human face").transform;enemyHead.SetParent(enemy,false);enemyHead.localPosition=new Vector3(0,.405f,.105f);
            PetArt.Shape(enemyHead,PrimitiveType.Sphere,Vector3.zero,new Vector3(.126f,.148f,.095f),flesh,"Bald head");
            PetArt.Shape(enemyHead,PrimitiveType.Sphere,new Vector3(0,-.077f,.062f),new Vector3(.094f,.045f,.046f),shadow,"Dark beard");
            PetArt.Shape(enemyHead,PrimitiveType.Sphere,new Vector3(0,-.018f,.087f),new Vector3(.029f,.037f,.028f),flesh,"Nose");
            PetArt.Shape(enemyHead,PrimitiveType.Sphere,new Vector3(0,-.080f,.10f),new Vector3(.052f,.011f,.009f),new Color(.29f,.09f,.08f),"Creepy frown");
            for(int s=-1;s<=1;s+=2)
            {
                PetArt.Shape(enemyHead,PrimitiveType.Sphere,new Vector3(s*.048f,.021f,.071f),new Vector3(.026f,.013f,.010f),shadow,"Deep eye socket");
                PetArt.Shape(enemyHead,PrimitiveType.Sphere,new Vector3(s*.048f,.020f,.080f),new Vector3(.010f,.008f,.006f),eyes,"Glowing amber eye");
                PetArt.Shape(enemyHead,PrimitiveType.Sphere,new Vector3(s*.12f,-.012f,-.005f),new Vector3(.026f,.043f,.024f),flesh,"Ear");
            }
            enemyLeft=new GameObject("Left bean arm").transform;enemyLeft.SetParent(enemy,false);enemyLeft.localPosition=new Vector3(-.112f,.27f,0);
            enemyRight=new GameObject("Right bean arm").transform;enemyRight.SetParent(enemy,false);enemyRight.localPosition=new Vector3(.112f,.27f,0);
            PetArt.Shape(enemyLeft,PrimitiveType.Capsule,new Vector3(-.065f,-.075f,.01f),new Vector3(.038f,.105f,.04f),flesh,"Left arm");
            PetArt.Shape(enemyRight,PrimitiveType.Capsule,new Vector3(.065f,-.075f,.01f),new Vector3(.038f,.105f,.04f),flesh,"Right arm");
            PetArt.Shape(enemyRight,PrimitiveType.Sphere,new Vector3(.073f,-.145f,.015f),Vector3.one*.044f,new Color(.35f,.91f,.27f),"Slime fist");
            enemyLegLeft=new GameObject("Left bean leg").transform;enemyLegLeft.SetParent(enemy,false);enemyLegLeft.localPosition=new Vector3(-.065f,.09f,0);
            enemyLegRight=new GameObject("Right bean leg").transform;enemyLegRight.SetParent(enemy,false);enemyLegRight.localPosition=new Vector3(.065f,.09f,0);
            PetArt.Shape(enemyLegLeft,PrimitiveType.Capsule,new Vector3(0,-.047f,0),new Vector3(.043f,.092f,.05f),flesh,"Left little leg");
            PetArt.Shape(enemyLegRight,PrimitiveType.Capsule,new Vector3(0,-.047f,0),new Vector3(.043f,.092f,.05f),flesh,"Right little leg");
            PetArt.Shape(enemyLegLeft,PrimitiveType.Sphere,new Vector3(0,-.090f,.024f),new Vector3(.076f,.027f,.10f),flesh,"Left foot");
            PetArt.Shape(enemyLegRight,PrimitiveType.Sphere,new Vector3(0,-.090f,.024f),new Vector3(.076f,.027f,.10f),flesh,"Right foot");
            playerLeft=Find(puff,"LeftArm");playerRight=Find(puff,"RightArm");
            playerLegLeft=Find(puff,"LeftLeg");playerLegRight=Find(puff,"RightLeg");
            var card=new GameObject("Combat score").transform;card.SetParent(camera,false);card.localPosition=new Vector3(.38f,.25f,.82f);card.localScale=Vector3.one*.58f;
            PetArt.Shape(card,PrimitiveType.Cube,Vector3.zero,new Vector3(.43f,.11f,.006f),new Color(.13f,.08f,.19f),"Combat score backing");
            status=PetArt.Text(card,"PUFF 100   VS   BEAN 100",new Vector3(0,.020f,-.008f),.0037f,PetArt.Cream);
            status.fontStyle=FontStyle.Bold;
            PetArt.Shape(card,PrimitiveType.Cube,new Vector3(-.105f,-.029f,-.006f),new Vector3(.17f,.016f,.003f),new Color(.32f,.27f,.39f),"Puff health track");
            PetArt.Shape(card,PrimitiveType.Cube,new Vector3(.105f,-.029f,-.006f),new Vector3(.17f,.016f,.003f),new Color(.32f,.27f,.39f),"Bean health track");
            puffBar=PetArt.Shape(card,PrimitiveType.Cube,new Vector3(-.105f,-.029f,-.010f),new Vector3(.17f,.014f,.003f),PetArt.Pink,"Puff fight health").transform;
            gloomBar=PetArt.Shape(card,PrimitiveType.Cube,new Vector3(.105f,-.029f,-.010f),new Vector3(.17f,.014f,.003f),new Color(.65f,.30f,.93f),"Bean fight health").transform;
            PetArt.Shape(card,PrimitiveType.Cube,new Vector3(0,-.050f,-.006f),new Vector3(.38f,.006f,.003f),new Color(.26f,.25f,.37f),"Shield energy track");
            guardBar=PetArt.Shape(card,PrimitiveType.Cube,new Vector3(0,-.050f,-.010f),new Vector3(.38f,.005f,.003f),new Color(.38f,.88f,1),"Shield energy").transform;
            audio=pipe.gameObject.AddComponent<AudioSource>();audio.spatialBlend=.6f;
            impact=Tone("Soft combat impact",155,.19f);
            explosion=Tone("Silly explosion",85,.42f);
            whoosh=Tone("Fast swipe",460,.13f);jumpSound=Tone("Jump pop",580,.18f);
            grabSound=Tone("Grab and throw",210,.27f);beanVoice=Tone("Bean growl",95,.32f);
            pipe.gameObject.SetActive(false);enemy.gameObject.SetActive(false);card.gameObject.SetActive(false);
        }
        static Transform Find(Transform root,string name)
        {foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
        static AudioClip Tone(string name,float hz,float seconds)
        {
            const int rate=22050;int n=(int)(rate*seconds);float[] samples=new float[n];
            for(int i=0;i<n;i++){float t=(float)i/rate;float e=Mathf.Exp(-t*12);samples[i]=(Mathf.Sin(t*hz*6.28318f*(1-t*.55f))*.7f+Mathf.Sin(t*hz*2.01f*6.28318f)*.3f)*e*.24f;}
            var c=AudioClip.Create(name,n,1,rate,false);c.SetData(samples,0);return c;
        }
        public void Begin()
        {
            if(Active)return;
            Active=true;finished=false;enemyHealth=150;playerHealth=100;
            spawnTime=0;enemyAttack=2.8f;enemyMelee=1.3f;stun=0;pose=0;playerPose=0;guardEnergy=100;guardBreak=0;
            jumpHeight=jumpVelocity=enemyAir=enemyVertical=hitPause=enemyWindup=grabTime=0;
            holdingEnemy=enemyRingOut=airAttack=false;comboCount=0;comboWindow=0;
            move=Vector2.zero;shield=grab=false;
            strikeReady=false;
            Vector3 forward=camera.forward;forward.y=0;
            if(forward.sqrMagnitude<.001f)forward=arena.forward;
            Vector3 local=arena.InverseTransformDirection(forward.normalized);
            Vector3 chosen=puff.localPosition+local*.25f;
            for(float radius=.60f;radius>=.25f;radius-=.05f)
            {
                float x=puff.localPosition.x+local.x*radius,z=puff.localPosition.z+local.z*radius,h;
                if(room.Ground(x,z,puff.localPosition.y+.35f,out h)&&Mathf.Abs(h-puff.localPosition.y)<.08f)
                {chosen=new Vector3(x,h,z);break;}
            }
            pipe.localPosition=chosen;
            enemy.localPosition=pipe.localPosition+Vector3.down*.45f;
            enemy.gameObject.SetActive(true);pipe.gameObject.SetActive(true);status.transform.parent.gameObject.SetActive(true);
            status.text="THE BEAN REVENANT APPEARS!";
            Debug.Log("PUFF_FIGHT_BEGIN");
        }
        public void End()
        {
            if(!Active)return;Active=false;move=Vector2.zero;shield=false;strikeReady=false;holdingEnemy=false;
            pipe.gameObject.SetActive(false);enemy.gameObject.SetActive(false);status.transform.parent.gameObject.SetActive(false);
            foreach(var e in effects)if(e.Body)Object.Destroy(e.Body.gameObject);effects.Clear();
            if(playerLeft)playerLeft.localRotation=Quaternion.identity;
            if(playerRight)playerRight.localRotation=Quaternion.identity;
            if(playerLegLeft)playerLegLeft.localRotation=Quaternion.identity;
            if(playerLegRight)playerLegRight.localRotation=Quaternion.identity;
            Debug.Log("PUFF_FIGHT_END");
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
                Burst(enemy.position+Vector3.up*.30f,new Color(.86f,.44f,1),7);audio.PlayOneShot(impact,.7f);
                audio.PlayOneShot(beanVoice,.35f);
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
            var ball=PetArt.Shape(null,kind==2?PrimitiveType.Capsule:PrimitiveType.Sphere,origin,Vector3.one*size,color,kind==1?"Five second bomb":kind==2?"Slingshot rocket":"Slime").transform;
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
        void Explode(Vector3 at,float radius)
        {
            Burst(at,new Color(1,.59f,.18f),20);audio.PlayOneShot(explosion,.9f);
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
                    if(Vector2.Distance(new Vector2(local.x,local.z),new Vector2(pipe.localPosition.x,pipe.localPosition.z))<1.15f&&
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
                    if(!supported&&p.y<pipe.localPosition.y-.25f)
                    {enemyHealth=0;enemyRingOut=true;Burst(pipe.position+Vector3.up*.12f,PetArt.Gold,18);Debug.Log("PUFF_FIGHT_RING_OUT");}
                }
                Vector3 toward=puff.position-enemy.position;toward.y=0;
                float distance=toward.magnitude;
                if(toward.sqrMagnitude>.001f)
                {
                    enemy.rotation=Quaternion.Slerp(enemy.rotation,Quaternion.LookRotation(toward),1-Mathf.Exp(-dt*8));
                    if(distance>.22f&&stun<=0&&!holdingEnemy&&enemyAir<=0)
                    {
                        Vector3 next=enemy.position+toward.normalized*dt*.13f;
                        var local=arena.InverseTransformPoint(next);float h;
                        if(room.Ground(local.x,local.z,pipe.localPosition.y+.5f,out h)&&Mathf.Abs(h-pipe.localPosition.y)<.10f)enemy.localPosition=new Vector3(local.x,h,local.z);
                    }
                }
                enemyAttack-=dt;enemyMelee-=dt;
                if(enemyAttack<=0&&enemyWindup<=0&&stun<=0&&!holdingEnemy)
                {enemyWindup=.48f;enemyPose=.48f;audio.PlayOneShot(beanVoice,.55f);Debug.Log("PUFF_ENEMY_TELEGRAPH");}
                if(enemyWindup>0)
                {
                    enemyWindup-=dt;
                    if(enemyWindup<=0&&stun<=0&&!holdingEnemy)
                    {
                        enemyAttack=Random.Range(2.8f,4.2f);
                        Vector3 from=enemy.position+Vector3.up*.30f;
                        Vector3 aim=(puff.position+Vector3.up*(.12f+jumpHeight)-from).normalized;
                        Spawn(from,aim*1.18f,2.3f,0,new Color(.33f,.89f,.32f),.037f);
                        audio.PlayOneShot(whoosh,.55f);Debug.Log("PUFF_ENEMY_SLIME");
                    }
                }
                if(distance<.20f&&enemyMelee<=0&&stun<=0&&!holdingEnemy&&jumpHeight<.08f)
                {enemyMelee=1.45f;enemyPose=.34f;DamagePlayer(8,toward.normalized);}
                if(enemyHealth<=0||playerHealth<=0)
                {finished=true;screenTime=3.5f;status.text=enemyHealth<=0?(enemyRingOut?"RING OUT!  PUFF WINS!":"PUFF WINS!"):"PUFF NEEDS A BREAK";Debug.Log("PUFF_FIGHT_RESULT "+(enemyHealth<=0?"WIN":"LOSS"));}
            }
            if(finished)screenTime-=dt;
            if(!finished)status.text=holdingEnemy?"GRABBED! RELEASE GRIP TO THROW":"PUFF "+Mathf.CeilToInt(playerHealth)+"   VS   BEAN "+Mathf.CeilToInt(enemyHealth);
            float pWidth=.17f*Mathf.Clamp01(playerHealth/100f),eWidth=.17f*Mathf.Clamp01(enemyHealth/150f);
            puffBar.localScale=new Vector3(Mathf.Max(.001f,pWidth),.014f,.003f);
            puffBar.localPosition=new Vector3(-.19f+pWidth*.5f,-.029f,-.010f);
            gloomBar.localScale=new Vector3(Mathf.Max(.001f,eWidth),.014f,.003f);
            gloomBar.localPosition=new Vector3(.02f+eWidth*.5f,-.029f,-.010f);
            float gWidth=.38f*guardEnergy/100f;
            guardBar.localScale=new Vector3(Mathf.Max(.001f,gWidth),.005f,.003f);
            guardBar.localPosition=new Vector3(-.19f+gWidth*.5f,-.050f,-.010f);
            if(shield&&playerPose<=0){pose=3;playerPose=.1f;}
            Vector3 face=enemy.position-puff.position;face.y=0;
            if(face.sqrMagnitude>.001f)puff.rotation=Quaternion.Slerp(puff.rotation,Quaternion.LookRotation(face),1-Mathf.Exp(-dt*12));
            float duration=pose==1||pose==2?Mathf.Max(.01f,strikeDuration):.45f;
            float normalized=playerPose>0?Mathf.Clamp01(1-playerPose/duration):1;
            float phase=normalized<.30f?-.30f*Mathf.Sin(normalized/.30f*Mathf.PI*.5f):normalized<.64f?Mathf.Sin((normalized-.30f)/.34f*Mathf.PI*.5f):Mathf.Cos((normalized-.64f)/.36f*Mathf.PI*.5f);
            phase=playerPose>0?phase:0;
            if(playerRight)playerRight.localRotation=Quaternion.Euler(pose==1?-125*phase:pose==3?-48:pose==4?-80:pose==5?-30:0,0,pose==2?75*phase:0);
            if(playerLeft)playerLeft.localRotation=Quaternion.Euler(pose==2?-42*phase:pose==3?-48:pose==4?-80:pose==5?-30:0,0,pose==2?-65*phase:0);
            if(playerLegRight)playerLegRight.localRotation=Quaternion.Euler(pose==2?-120*phase:pose==5?-35:0,0,pose==2?28*phase:0);
            if(playerLegLeft)playerLegLeft.localRotation=Quaternion.Euler(pose==2?35*phase:pose==5?35:0,0,0);
            enemyRight.localRotation=Quaternion.Euler(enemyPose>0?-65*Mathf.Sin(enemyPose*7):0,0,0);
            enemyLeft.localRotation=Quaternion.Euler(enemyPose>0?45*Mathf.Sin(enemyPose*7):0,0,0);
            enemyHead.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.time*2)*5+enemyPose*15);
            enemyBody.localRotation=Quaternion.Euler(enemyPose>0?enemyPose*14:0,0,Mathf.Sin(Time.time*2.1f)*2);
            enemyLegLeft.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*8)*5,0,0);
            enemyLegRight.localRotation=Quaternion.Euler(-Mathf.Sin(Time.time*8)*5,0,0);
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
                    {DamagePlayer(12,(puff.position-next).normalized);Burst(next,new Color(.42f,1,.32f),8);e.Fuse=0;}
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
