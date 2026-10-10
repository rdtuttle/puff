using UnityEngine;
using TabletopMayhem.Core;
namespace TabletopMayhem
{
    // A head-following world-space status card, sized to stay legible in passthrough.
    public sealed class PetHud
    {
        readonly Transform root;
        readonly Transform[] fills=new Transform[5];
        readonly TextMesh[] values=new TextMesh[5];
        readonly TextMesh header;
        readonly TextMesh mood;
        readonly TextMesh bond,careGoal,daily;
        readonly GameObject controls;
        bool controlsVisible;
        readonly Color[] colors={new Color(1,.72f,.25f),new Color(.27f,.67f,1),new Color(.92f,.43f,.91f),new Color(.21f,.84f,.94f),new Color(.39f,.78f,.50f)};
        public PetHud(Transform camera)
        {
            root=new GameObject("Cozy pet status card").transform;root.SetParent(camera,false);
            root.localPosition=new Vector3(-.39f,.24f,.82f);
            root.localScale=Vector3.one*.66f;
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,-.015f,.005f),new Vector3(.298f,.356f,.004f),new Color(.98f,.89f,.78f),"Cream rounded card");
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,-.015f,0),new Vector3(.288f,.346f,.003f),new Color(1,.969f,.913f),"Ivory face");
            header=PetArt.Text(root,"PUFF  /  DAY 1",new Vector3(0,.128f,-.005f),.0030f,new Color(.49f,.23f,.36f));
            header.fontStyle=FontStyle.Bold;
            string[] labels={"HUNGER","THIRST","BOREDOM","ENERGY","HEALTH"};
            string[] icons={"H","W","B","E","+"};
            Color[] pale={new Color(1,.86f,.64f),new Color(.75f,.88f,1),new Color(.96f,.77f,.96f),new Color(.73f,.93f,1),new Color(.79f,.94f,.81f)};
            for(int i=0;i<5;i++)
            {
                float y=.084f-i*.044f;
                PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,y-.004f,-.003f),new Vector3(.276f,.040f,.002f),pale[i],labels[i]+" band");
                PetArt.Shape(root,PrimitiveType.Sphere,new Vector3(-.117f,y-.004f,-.007f),new Vector3(.031f,.031f,.003f),colors[i],labels[i]+" badge");
                PetArt.Text(root,icons[i],new Vector3(-.117f,y-.010f,-.011f),.0028f,Color.white);
                var title=PetArt.Text(root,labels[i],new Vector3(-.066f,y+.004f,-.010f),.0025f,new Color(.40f,.21f,.32f));
                title.anchor=TextAnchor.MiddleLeft;title.fontStyle=FontStyle.Bold;
                values[i]=PetArt.Text(root,"0%",new Vector3(.110f,y+.004f,-.010f),.0028f,new Color(.40f,.21f,.32f));
                values[i].fontStyle=FontStyle.Bold;
                PetArt.Shape(root,PrimitiveType.Cube,new Vector3(.008f,y-.015f,-.008f),new Vector3(.174f,.009f,.002f),new Color(.72f,.67f,.70f),"Empty bar");
                fills[i]=PetArt.Shape(root,PrimitiveType.Cube,new Vector3(-.079f,y-.015f,-.011f),new Vector3(.001f,.008f,.002f),colors[i],"Filled bar").transform;
            }
            mood=PetArt.Text(root,"HANGING OUT",new Vector3(0,-.145f,-.009f),.0028f,new Color(.56f,.26f,.43f));
            var settings=PetArt.Text(root,"STATUS  /  CARE  /  DAILY",new Vector3(0,-.176f,-.009f),.0022f,new Color(.48f,.28f,.44f));
            settings.fontStyle=FontStyle.Bold;
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,-.258f,.005f),new Vector3(.298f,.097f,.004f),new Color(.98f,.89f,.78f),"Care progress ribbon");
            PetArt.Shape(root,PrimitiveType.Cube,new Vector3(0,-.258f,0),new Vector3(.288f,.087f,.003f),new Color(1,.969f,.913f),"Care ribbon face");
            bond=PetArt.Text(root,"BOND 1  /  CARE 0 OF 4",new Vector3(0,-.226f,-.009f),.0026f,new Color(.52f,.24f,.42f));
            bond.fontStyle=FontStyle.Bold;
            careGoal=PetArt.Text(root,"NEXT: GIVE A SNACK",new Vector3(0,-.256f,-.009f),.0023f,new Color(.48f,.28f,.44f));
            daily=PetArt.Text(root,"TODAY: FEED  /  STARS 0",new Vector3(0,-.286f,-.009f),.00215f,new Color(.63f,.28f,.44f));
            controls=new GameObject("Toggleable controls settings");controls.transform.SetParent(root,false);
            PetArt.Shape(controls.transform,PrimitiveType.Cube,new Vector3(.305f,-.015f,.006f),new Vector3(.298f,.25f,.004f),new Color(.98f,.89f,.78f),"Controls card");
            PetArt.Shape(controls.transform,PrimitiveType.Cube,new Vector3(.305f,-.015f,0),new Vector3(.287f,.238f,.003f),new Color(1,.969f,.913f),"Controls face");
            var guide=PetArt.Text(controls.transform,
                "CONTROLS\n\nCare: feed, water, sing, cuddle\nGrip behind shoulder: grab backpack\nPull forward; release to place\nGrip pack handle to move it again\nRight grip: hold / throw item\nRight trigger near Puff: pet her\nMic to Puff: sing\nBlaster trigger: fire   Sword: swing\nLeft grip: pick up Puff\nA: help   B: home\nY tap: move Puff   Y hold: pets\nX: enter / exit Fight Mode\nFight: stick move, A punch, B kick\nLeft trigger: jump   Right trigger: shield\nLeft grip: grab; release to throw\nBomb: throw; rocket: hold / release",
                new Vector3(.305f,-.022f,-.009f),.0022f,new Color(.40f,.21f,.32f));
            guide.anchor=TextAnchor.MiddleCenter;
            controls.SetActive(false);
        }
        public void ToggleControls(){controlsVisible=!controlsVisible;controls.SetActive(controlsVisible);}
        public void SetDaily(int stars,string goal,bool done)
        {daily.text=done?"TODAY: DONE!  /  STARS "+stars:"TODAY: "+goal+"  /  STARS "+stars;}
        public void Update(PetSimulation pet,PetCareProgress progress,PetProfile profile,bool visible)
        {
            root.gameObject.SetActive(visible);
            if(!visible)return;
            header.text=(profile!=null?profile.Name.ToUpperInvariant():"PUFF")+"  /  DAY "+(1+(int)(pet.AgeRealSeconds/86400));
            bond.text="BOND "+progress.BondLevel+"  /  CARE "+progress.TasksCompleted+" OF 4";
            careGoal.text=progress.NextTask==CareAction.Feed?"NEXT: GIVE A SNACK":
                progress.NextTask==CareAction.Water?"NEXT: GIVE WATER":
                progress.NextTask==CareAction.Sing?"NEXT: SING WITH PUFF":"NEXT: PICK UP PUFF";
            float[] levels={pet.Hunger,pet.Thirst,pet.Boredom,pet.Energy,pet.Health};
            for(int i=0;i<5;i++)
            {
                float amount=Mathf.Clamp01(levels[i]/100f),width=.174f*amount;
                fills[i].localScale=new Vector3(Mathf.Max(.001f,width),.008f,.002f);
                fills[i].localPosition=new Vector3(-.079f+width*.5f,.084f-i*.044f-.015f,-.011f);
                values[i].text=Mathf.RoundToInt(levels[i])+"%";
            }
            switch(pet.Action)
            {
                case PetAction.Sing:mood.text="SINGING A SONG!";break;
                case PetAction.Sleep:mood.text="SWEET DREAMS";break;
                case PetAction.Catch:mood.text="WHAT A CATCH!";break;
                case PetAction.Angry:mood.text="HEY! THAT'S RUDE";break;
                case PetAction.Attention:mood.text="PLAY WITH ME?";break;
                case PetAction.Play:mood.text="FEELING SILLY";break;
                case PetAction.Held:mood.text="UP WE GO!";break;
                case PetAction.Airborne:mood.text="WHEEEEE!";break;
                case PetAction.Celebrate:mood.text="YUM! THANK YOU";break;
                case PetAction.Petted:mood.text="PURR... MORE PLEASE";break;
                case PetAction.Sick:mood.text="NOT FEELING WELL";break;
                case PetAction.Dead:mood.text="RESTING IN STARS";break;
                default:mood.text="HANGING OUT";break;
            }
        }
    }
}
