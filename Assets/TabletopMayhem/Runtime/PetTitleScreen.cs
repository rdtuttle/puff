using UnityEngine;
using TabletopMayhem.Core;

namespace TabletopMayhem
{
    // A light world-space garden card that sits in front of passthrough until play begins.
    public sealed class PetTitleScreen
    {
        readonly Transform root;
        readonly TextMesh prompt;
        readonly GameObject rosterPanel;
        readonly TextMesh slotLabel,profileLabel,actionLabel;
        readonly Transform[] drifting=new Transform[5];
        readonly Vector3[] driftHomes=new Vector3[5];
        readonly Transform halo,hero;
        readonly Transform[] fireflies=new Transform[12];
        readonly Vector3[] fireflyHomes=new Vector3[12];
        float age;

        public bool Visible { get { return root.gameObject.activeSelf; } }
        public bool InRoster { get { return rosterPanel.activeSelf; } }

        public PetTitleScreen(Transform camera)
        {
            root=new GameObject("Puff title screen").transform;
            root.SetParent(camera,false);
            root.localPosition=new Vector3(0,0,1.05f);

            Color cream=new Color(1,.94f,.88f),plum=new Color(.22f,.09f,.23f);
            Color night=new Color(.071f,.055f,.16f),purple=new Color(.22f,.12f,.34f);
            Color coral=new Color(1,.44f,.61f),lilac=new Color(.76f,.60f,1);
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,0,.090f),new Vector3(1.42f,.84f,.018f),new Color(.74f,.52f,.76f),"Luminous title border");
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,0,.075f),new Vector3(1.39f,.81f,.015f),night,"Midnight title canvas");
            PetArt.Shape(root,PrimitiveType.Sphere,new Vector3(0,.05f,.064f),new Vector3(.73f,.67f,.006f),purple,"Indigo light bloom");
            PetArt.Shape(root,PrimitiveType.Sphere,new Vector3(0,-.13f,.058f),new Vector3(.66f,.40f,.006f),new Color(.29f,.12f,.35f),"Warm magenta glow");
            for(int i=0;i<4;i++)
            {
                float y=.36f-i*.014f;
                PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,y,.045f),new Vector3(1.30f-i*.018f,.0025f,.003f),i%2==0?coral:lilac,"Fine title rule");
            }
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2/12;
                fireflies[i]=PetArt.Shape(root,PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.56f,.06f+Mathf.Cos(a)*.24f,.017f),
                    Vector3.one*(i%3==0?.010f:.005f),i%2==0?PetArt.Gold:lilac,"Orbiting star").transform;
                fireflyHomes[i]=fireflies[i].localPosition;
            }
            halo=new GameObject("Orbiting title halo").transform;halo.SetParent(root,false);halo.localPosition=new Vector3(0,.245f,.020f);
            for(int i=0;i<28;i++)
            {
                float a=i*Mathf.PI*2/28;
                PetArt.Shape(halo,PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.20f,Mathf.Cos(a)*.12f,0),Vector3.one*.004f,
                    i%2==0?coral:PetArt.Gold,"Halo bead");
            }
            hero=new GameObject("Puff title mascot").transform;hero.SetParent(root,false);hero.localPosition=new Vector3(0,.245f,-.006f);
            PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(0,-.018f,0),new Vector3(.145f,.128f,.075f),PetArt.Pink,"Puff mascot body");
            for(int s=-1;s<=1;s+=2)
            {
                PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(s*.064f,.066f,.006f),new Vector3(.060f,.071f,.037f),PetArt.Pink,"Soft ear");
                PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(s*.064f,.067f,-.024f),new Vector3(.038f,.050f,.012f),new Color(.62f,.31f,.47f),"Inner ear");
                PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(s*.036f,-.012f,-.066f),new Vector3(.031f,.035f,.011f),new Color(.10f,.23f,.30f),"Wide teal eye");
                PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(s*.042f,-.002f,-.076f),Vector3.one*.007f,cream,"Eye glint");
                PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(s*.067f,-.051f,-.058f),new Vector3(.024f,.012f,.006f),coral,"Blush");
            }
            PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(0,-.047f,-.071f),new Vector3(.016f,.011f,.007f),plum,"Tiny smile");
            PetArt.Shape(hero,PrimitiveType.Sphere,new Vector3(-.020f,.096f,-.009f),new Vector3(.039f,.028f,.028f),PetArt.Pink,"Curled forelock");
            WordArt("PUFF",.015f,.044f,coral);
            var subtitle=PetArt.Text(root,"YOUR LITTLE WORLD. YOUR BIG ADVENTURE.",new Vector3(0,-.137f,-.036f),.0037f,cream);
            subtitle.fontStyle=FontStyle.Bold;
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,-.263f,-.023f),new Vector3(.59f,.080f,.009f),new Color(.40f,.18f,.48f),"Button shadow");
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,-.256f,-.032f),new Vector3(.58f,.072f,.008f),coral,"Start button");
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,-.256f,-.039f),new Vector3(.55f,.052f,.003f),new Color(1,.65f,.71f),"Start button highlight");
            prompt=PetArt.Text(root,"PRESS ANY BUTTON  >",new Vector3(0,-.263f,-.044f),.0049f,plum);
            prompt.fontStyle=FontStyle.Bold;
            PetArt.Text(root,"MIXED REALITY PET  /  FIGHT MODE",new Vector3(0,-.355f,-.025f),.0028f,new Color(.83f,.73f,.88f));
            rosterPanel=new GameObject("Five pet save slots");rosterPanel.transform.SetParent(root,false);
            PetArt.Shape(rosterPanel.transform,PrimitiveType.Cube,new Vector3(0,-.230f,-.039f),new Vector3(.90f,.23f,.012f),new Color(.39f,.22f,.50f),"Save slots card");
            PetArt.Shape(rosterPanel.transform,PrimitiveType.Cube,new Vector3(0,-.230f,-.046f),new Vector3(.87f,.20f,.005f),cream,"Save slots face");
            slotLabel=PetArt.Text(rosterPanel.transform,"<  1   2   3   4   5  >",new Vector3(0,-.151f,-.054f),.0046f,plum);
            slotLabel.fontStyle=FontStyle.Bold;
            profileLabel=PetArt.Text(rosterPanel.transform,"NEW COMPANION",new Vector3(0,-.220f,-.054f),.0046f,plum);
            profileLabel.fontStyle=FontStyle.Bold;
            actionLabel=PetArt.Text(rosterPanel.transform,"A: CREATE   X: NAME",new Vector3(0,-.296f,-.054f),.0032f,plum);
            actionLabel.fontStyle=FontStyle.Bold;
            rosterPanel.SetActive(false);
        }

        void WordArt(string value,float y,float size,Color ink)
        {
            TextMesh depth=PetArt.Text(root,value,new Vector3(.010f,y-.011f,-.020f),size,new Color(.56f,.29f,.48f));
            depth.fontStyle=FontStyle.Bold;
            Vector3[] outline={new Vector3(-.004f,0,0),new Vector3(.004f,0,0),new Vector3(0,-.004f,0),new Vector3(0,.004f,0)};
            foreach(var offset in outline)
            {
                TextMesh edge=PetArt.Text(root,value,new Vector3(offset.x,y+offset.y,-.026f),size,new Color(1,.97f,.87f));
                edge.fontStyle=FontStyle.Bold;
            }
            TextMesh face=PetArt.Text(root,value,new Vector3(0,y,-.035f),size,ink);
            face.fontStyle=FontStyle.BoldAndItalic;
        }

        public void ShowRoster(PetRoster roster,int selected,int nameStyle)
        {
            root.gameObject.SetActive(true);rosterPanel.SetActive(true);prompt.gameObject.SetActive(false);
            UpdateRoster(roster,selected,nameStyle);
        }
        public void UpdateRoster(PetRoster roster,int selected,int nameStyle)
        {
            string slots="";
            for(int i=0;i<PetRoster.Capacity;i++)
                slots+=(i==selected?" ["+(i+1)+"] ":"  "+(i+1)+"  ");
            slotLabel.text=slots;
            PetProfile profile=roster.Get(selected);
            if(profile==null)
            {
                profileLabel.text="NEW: "+NameForStyle(nameStyle);
                actionLabel.text="A: CREATE & PLAY   X: CHANGE NAME";
            }
            else if(profile.Dead)
            {
                profileLabel.text=profile.Name.ToUpperInvariant()+"  /  RESTING IN STARS";
                actionLabel.text="X: NAME   /   HOLD B: NEW PET";
            }
            else
            {
                profileLabel.text=profile.Name.ToUpperInvariant()+"  /  DAY "+(1+(int)(profile.AgeSeconds/86400))+"  /  HEALTH "+Mathf.RoundToInt(profile.Health)+"%";
                actionLabel.text="A: PLAY   /   LEFT-RIGHT: CHOOSE SLOT";
            }
        }
        public static string NameForStyle(int style)
        {
            string[] names={"Puff","Mochi","Bubbles","Peaches","Marshmallow"};
            return names[Mathf.Abs(style)%names.Length];
        }

        void Cloud(float x,float y,float scale)
        {
            Transform cloud=new GameObject("Slow drifting cloud").transform;cloud.SetParent(root,false);
            cloud.localPosition=new Vector3(x,y,.028f);cloud.localScale=Vector3.one*scale;
            Color white=new Color(1,.99f,.96f);
            PetArt.Shape(cloud,PrimitiveType.Sphere,new Vector3(0,0,0),new Vector3(.17f,.045f,.012f),white,"Cloud base");
            PetArt.Shape(cloud,PrimitiveType.Sphere,new Vector3(-.045f,.021f,-.002f),new Vector3(.07f,.066f,.014f),white,"Cloud puff");
            PetArt.Shape(cloud,PrimitiveType.Sphere,new Vector3(.035f,.030f,-.002f),new Vector3(.09f,.079f,.014f),white,"Cloud puff");
        }

        void Tree(float x,float y,float scale,Color trunkColor,Color leafColor)
        {
            Transform tree=new GameObject("Little garden tree").transform;tree.SetParent(root,false);
            tree.localPosition=new Vector3(x,y,-.003f);tree.localScale=Vector3.one*scale;
            PetArt.Shape(tree,PrimitiveType.Cube,new Vector3(0,-.037f,0),new Vector3(.025f,.18f,.015f),trunkColor,"Tree trunk");
            PetArt.Shape(tree,PrimitiveType.Sphere,new Vector3(0,.075f,-.009f),new Vector3(.15f,.17f,.025f),leafColor,"Rounded leafy crown");
            PetArt.Shape(tree,PrimitiveType.Sphere,new Vector3(-.045f,.025f,-.014f),new Vector3(.09f,.11f,.024f),leafColor,"Leaf cluster");
            PetArt.Shape(tree,PrimitiveType.Sphere,new Vector3(.046f,.025f,-.014f),new Vector3(.09f,.11f,.024f),leafColor,"Leaf cluster");
        }

        void Flower(float x,float y,Color petals)
        {
            Transform flower=new GameObject("Garden flower").transform;flower.SetParent(root,false);
            flower.localPosition=new Vector3(x,y,-.014f);
            PetArt.Shape(flower,PrimitiveType.Cube,new Vector3(0,-.009f,0),new Vector3(.003f,.035f,.003f),new Color(.24f,.54f,.35f),"Flower stem");
            for(int p=0;p<5;p++)
            {
                float a=p*Mathf.PI*2/5;
                PetArt.Shape(flower,PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.011f,.012f+Mathf.Cos(a)*.011f,-.002f),new Vector3(.012f,.012f,.006f),petals,"Soft petal");
            }
            PetArt.Shape(flower,PrimitiveType.Sphere,new Vector3(0,.012f,-.005f),new Vector3(.009f,.009f,.006f),PetArt.Gold,"Flower center");
            for(int i=0;i<drifting.Length;i++)if(drifting[i]==null)
            {drifting[i]=flower;driftHomes[i]=flower.localPosition;break;}
        }

        public void Update(float dt)
        {
            age+=dt;
            Color c=prompt.color;c.a=.70f+.30f*(.5f+.5f*Mathf.Sin(age*3.4f));prompt.color=c;
            halo.localRotation=Quaternion.Euler(0,0,Mathf.Sin(age*.55f)*8);
            hero.localPosition=new Vector3(0,.245f+Mathf.Sin(age*1.8f)*.009f,-.006f);
            hero.localRotation=Quaternion.Euler(0,Mathf.Sin(age*.9f)*12,Mathf.Sin(age*1.3f)*5);
            for(int i=0;i<fireflies.Length;i++)if(fireflies[i])
            {
                fireflies[i].localPosition=fireflyHomes[i]+Vector3.up*(Mathf.Sin(age*1.2f+i*2)*.006f);
            }
            for(int i=0;i<drifting.Length;i++)if(drifting[i])
                drifting[i].localPosition=driftHomes[i]+new Vector3(0,Mathf.Sin(age*1.4f+i)*.003f,0);
        }

        public void Hide(){root.gameObject.SetActive(false);}
    }
}
