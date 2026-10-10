using UnityEngine;
namespace TabletopMayhem
{
    // Clean white glove silhouettes attached to controller poses.
    public sealed class PetHand
    {
        static Material white,shadow;
        readonly Transform root,thumb;
        readonly bool isRight;
        readonly Transform[] fingers=new Transform[4];
        readonly float[] fingerLengths={.057f,.065f,.058f,.049f};
        public Transform FingerTip {get;private set;}
        static GameObject GloveShape(Transform parent,Vector3 position,Vector3 scale,bool edge,string name)
        {
            if(!white)
            {
                var shader=Resources.Load<Shader>("PetGlove");
                white=new Material(shader);white.color=new Color(.99f,.99f,1);
                shadow=new Material(shader);shadow.color=new Color(.84f,.88f,.93f);
            }
            var go=PetArt.Shape(parent,PrimitiveType.Sphere,position,scale,Color.white,name);
            go.GetComponent<Renderer>().sharedMaterial=edge?shadow:white;return go;
        }
        public PetHand(Transform controller,bool right)
        {
            isRight=right;
            root=new GameObject(right?"Right white glove":"Left white glove").transform;
            root.SetParent(controller,false);root.localPosition=new Vector3(0,-.018f,.035f);
            float side=right?1:-1;
            GloveShape(root,new Vector3(0,-.011f,-.016f),new Vector3(.079f,.035f,.084f),false,"One-piece palm");
            GloveShape(root,new Vector3(0,-.014f,-.049f),new Vector3(.060f,.032f,.055f),false,"Connected wrist");
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.019f*side;
                fingers[i]=new GameObject("Finger "+i).transform;fingers[i].SetParent(root,false);
                fingers[i].localPosition=new Vector3(x,-.008f,.020f);
                float length=fingerLengths[i];
                GloveShape(fingers[i],new Vector3(0,0,length*.43f),new Vector3(.017f,.018f,length*.94f),false,"White finger");
                GloveShape(fingers[i],new Vector3(0,0,length*.86f),new Vector3(.017f,.018f,.020f),false,"Rounded fingertip");
            }
            FingerTip=new GameObject("Index poke point").transform;FingerTip.SetParent(fingers[0],false);
            FingerTip.localPosition=new Vector3(0,0,fingerLengths[0]*.99f);
            thumb=new GameObject("Thumb").transform;thumb.SetParent(root,false);
            thumb.localPosition=new Vector3(-side*.032f,-.013f,-.005f);
            thumb.localRotation=Quaternion.Euler(0,-side*30,0);
            GloveShape(thumb,new Vector3(0,0,.014f),new Vector3(.025f,.021f,.044f),false,"Connected thumb");
            GloveShape(root,new Vector3(0,.007f,-.050f),new Vector3(.058f,.004f,.008f),true,"Subtle cuff seam");
        }
        public void Pose(bool grip,bool poke,bool fist,float dt,int aimSign=1)
        {
            float blend=1-Mathf.Exp(-dt*17);
            // Visual glove orientation must not follow the raycast's forward/reverse
            // choice. Both controller poses use the same finger-forward basis.
            root.localRotation=Quaternion.Slerp(root.localRotation,Quaternion.identity,blend);
            for(int i=0;i<fingers.Length;i++)
            {
                float curl=fist||grip?72:poke&&i==0?2:poke?57:24;
                fingers[i].localRotation=Quaternion.Slerp(fingers[i].localRotation,Quaternion.Euler(curl,0,0),blend);
            }
            thumb.localRotation=Quaternion.Slerp(thumb.localRotation,Quaternion.Euler(fist||grip?25:0,isRight?-30:30,0),blend);
        }
    }
}
