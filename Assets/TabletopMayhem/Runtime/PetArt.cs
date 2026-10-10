using System.Collections.Generic;
using UnityEngine;
using TabletopMayhem.Core;
namespace TabletopMayhem
{
    public static class PetArt
    {
        static readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        static Material modelMaterial;
        public static readonly Color Pink=new Color(1,.44f,.66f),Cream=new Color(1,.86f,.72f),Gold=new Color(1,.72f,.25f),Ink=new Color(.14f,.08f,.13f);
        public static Material Mat(Color color)
        {
            Material m;if(!materials.TryGetValue(color,out m)||!m){m=new Material(Resources.Load<Shader>("MayhemToon"));m.color=color;m.enableInstancing=true;materials[color]=m;}return m;
        }
        public static GameObject Shape(Transform parent,PrimitiveType kind,Vector3 position,Vector3 scale,Color color,string name="Detail")
        {
            var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=Mat(color);Object.Destroy(go.GetComponent<Collider>());return go;
        }
        public static TextMesh Text(Transform parent,string value,Vector3 position,float size,Color color)
        {
            var go=new GameObject("Pet label");go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var tm=go.AddComponent<TextMesh>();tm.text=value;tm.fontSize=64;tm.characterSize=size;
            tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=tm.font.material;
            tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;return tm;
        }
        public static Transform Pet(Transform parent,out Transform sleepingFace,out Transform mouth)
        {
            var asset=Resources.Load<GameObject>("Fighters/Puff");if(!asset){Debug.LogError("PET_MODEL_MISSING");sleepingFace=mouth=null;return new GameObject("Missing pink pet").transform;}
            var model=Object.Instantiate(asset,parent,false).transform;model.name="Puff model";
            if(!modelMaterial){modelMaterial=new Material(Resources.Load<Shader>("MayhemVertex"));modelMaterial.enableInstancing=true;}
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>()){var slots=new Material[renderer.sharedMaterials.Length];for(int i=0;i<slots.Length;i++)slots[i]=modelMaterial;renderer.sharedMaterials=slots;}
            sleepingFace=null;mouth=null;foreach(var t in model.GetComponentsInChildren<Transform>()){if(t.name=="SleepFace")sleepingFace=t;if(t.name=="Mouth")mouth=t;}
            if(sleepingFace)sleepingFace.gameObject.SetActive(false);
            if(!mouth||!sleepingFace)Debug.LogError("PET_FACE_NODES_MISSING");
            return model;
        }
        public static Transform Fighter(Transform parent,string resource,string displayName)
        {
            var root=new GameObject(displayName).transform;root.SetParent(parent,false);
            var asset=Resources.Load<GameObject>("Fighters/"+resource);
            if(!asset){Debug.LogError("FIGHTER_MODEL_MISSING "+resource);return root;}
            var model=Object.Instantiate(asset,root,false).transform;model.name=resource+" model";
            if(!modelMaterial){modelMaterial=new Material(Resources.Load<Shader>("MayhemVertex"));modelMaterial.enableInstancing=true;}
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {var slots=new Material[renderer.sharedMaterials.Length];for(int i=0;i<slots.Length;i++)slots[i]=modelMaterial;renderer.sharedMaterials=slots;}
            return root;
        }
        public static Transform Microphone(Transform parent)
        {
            var mic=new GameObject("Singing microphone").transform;mic.SetParent(parent,false);mic.localPosition=new Vector3(.075f,.11f,-.055f);mic.localRotation=Quaternion.Euler(25,0,-16);
            Shape(mic,PrimitiveType.Cylinder,new Vector3(0,-.025f,0),new Vector3(.010f,.062f,.010f),Ink,"Grip");
            Shape(mic,PrimitiveType.Sphere,new Vector3(0,.040f,0),new Vector3(.026f,.031f,.026f),new Color(.74f,.82f,.87f),"Silver grille");
            for(int i=0;i<3;i++)Shape(mic,PrimitiveType.Cylinder,new Vector3(0,.025f+i*.011f,0),new Vector3(.0265f,.0018f,.0265f),new Color(.42f,.48f,.53f),"Grille ring");
            mic.gameObject.SetActive(false);return mic;
        }
        public static Transform PackProp(Transform parent,int kind)
        {
            Transform root;
            if(kind==0){root=Microphone(parent);root.gameObject.SetActive(true);root.name="Throwable microphone";}
            else
            {
                root=new GameObject(kind==1?"Confetti blaster":kind==2?"Foam sword":kind==3?"Countdown bomb":"Slingshot rocket launcher").transform;root.SetParent(parent,false);
                if(kind==1)
                {
                    Shape(root,PrimitiveType.Cube,new Vector3(0,0,.025f),new Vector3(.035f,.032f,.09f),new Color(.25f,.71f,.95f),"Toy blaster body");
                    Shape(root,PrimitiveType.Cylinder,new Vector3(0,-.035f,-.004f),new Vector3(.014f,.038f,.014f),Cream,"Handle");
                    Shape(root,PrimitiveType.Cylinder,new Vector3(0,0,.075f),new Vector3(.021f,.014f,.021f),Gold,"Confetti nozzle");
                }
                else if(kind==2)
                {
                    Shape(root,PrimitiveType.Cylinder,new Vector3(0,-.052f,0),new Vector3(.011f,.053f,.011f),new Color(.40f,.28f,.62f),"Soft handle");
                    Shape(root,PrimitiveType.Cube,new Vector3(0,-.004f,0),new Vector3(.065f,.014f,.024f),Gold,"Soft guard");
                    Shape(root,PrimitiveType.Capsule,new Vector3(0,.079f,0),new Vector3(.027f,.155f,.020f),new Color(.58f,.85f,1),"Foam blade");
                }
                else if(kind==3)
                {
                    Shape(root,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.075f,new Color(.16f,.14f,.22f),"Bomb shell");
                    Shape(root,PrimitiveType.Sphere,new Vector3(0,.038f,0),Vector3.one*.026f,Gold,"Bomb fuse");
                    Shape(root,PrimitiveType.Sphere,new Vector3(0,0,-.039f),new Vector3(.018f,.012f,.004f),Pink,"Friendly bomb badge");
                }
                else
                {
                    Shape(root,PrimitiveType.Capsule,new Vector3(0,-.02f,0),new Vector3(.025f,.105f,.025f),new Color(.48f,.25f,.68f),"Launcher handle");
                    Shape(root,PrimitiveType.Capsule,new Vector3(-.044f,.050f,0),new Vector3(.022f,.090f,.022f),Gold,"Left slingshot fork");
                    Shape(root,PrimitiveType.Capsule,new Vector3(.044f,.050f,0),new Vector3(.022f,.090f,.022f),Gold,"Right slingshot fork");
                    Shape(root,PrimitiveType.Cube,new Vector3(0,.084f,0),new Vector3(.105f,.008f,.009f),Pink,"Elastic band");
                    Shape(root,PrimitiveType.Sphere,new Vector3(0,.07f,0),Vector3.one*.037f,new Color(1,.52f,.20f),"Loaded rocket");
                }
            }
            root.gameObject.layer=9;
            var collider=root.gameObject.AddComponent<SphereCollider>();collider.radius=kind==2?.09f:.065f;
            return root;
        }
        public static Transform Food(Transform parent,FoodKind kind)
        {
            var root=new GameObject(kind.ToString()).transform;root.SetParent(parent,false);root.gameObject.layer=9;
            var collider=root.gameObject.AddComponent<SphereCollider>();collider.radius=.045f;
            if(kind==FoodKind.Donut)
            {
                const int major=24,minor=10;var verts=new Vector3[major*minor];var tris=new int[major*minor*6];
                for(int i=0;i<major;i++)for(int j=0;j<minor;j++)
                {float a=i*Mathf.PI*2/major,b=j*Mathf.PI*2/minor;float r=.026f+.011f*Mathf.Cos(b);verts[i*minor+j]=new Vector3(Mathf.Sin(a)*r,.011f*Mathf.Sin(b),Mathf.Cos(a)*r);
                 int n=(i*minor+j)*6,u=((i+1)%major)*minor+j,v=i*minor+(j+1)%minor,w=((i+1)%major)*minor+(j+1)%minor;tris[n]=u;tris[n+1]=v;tris[n+2]=i*minor+j;tris[n+3]=u;tris[n+4]=w;tris[n+5]=v;}
                var mesh=new Mesh{vertices=verts,triangles=tris};mesh.RecalculateNormals();var go=new GameObject("Iced donut");go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Mat(new Color(.93f,.55f,.33f));
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Shape(root,PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.026f,.008f,Mathf.Cos(a)*.026f),Vector3.one*.006f,i%2==0?Pink:Gold,"Sprinkle");}
            }
            else if(kind==FoodKind.Burger)
            {
                Shape(root,PrimitiveType.Sphere,new Vector3(0,-.018f,0),new Vector3(.074f,.018f,.065f),Gold,"Bottom bun");
                Shape(root,PrimitiveType.Cylinder,new Vector3(0,-.005f,0),new Vector3(.070f,.012f,.060f),new Color(.27f,.10f,.04f),"Patty");
                Shape(root,PrimitiveType.Sphere,new Vector3(0,.004f,0),new Vector3(.077f,.007f,.069f),new Color(.26f,.70f,.18f),"Lettuce");
                Shape(root,PrimitiveType.Sphere,new Vector3(0,.024f,0),new Vector3(.075f,.032f,.067f),new Color(.95f,.63f,.27f),"Top bun");
                for(int i=0;i<5;i++)Shape(root,PrimitiveType.Sphere,new Vector3((i-2)*.011f,.049f,((i%2)-.5f)*.013f),Vector3.one*.004f,Cream,"Sesame");
            }
            else if(kind==FoodKind.HotDog)
            {
                Shape(root,PrimitiveType.Sphere,new Vector3(0,-.008f,0),new Vector3(.16f,.035f,.045f),Gold,"Roll");
                Shape(root,PrimitiveType.Sphere,new Vector3(0,.010f,0),new Vector3(.15f,.027f,.032f),new Color(.67f,.18f,.11f),"Sausage");
                for(int i=-2;i<=2;i++)Shape(root,PrimitiveType.Sphere,new Vector3(i*.020f,.030f,(i%2)*.006f),Vector3.one*.007f,new Color(.96f,.76f,.12f),"Mustard");
            }
            else if(kind==FoodKind.Water)
            {
                Shape(root,PrimitiveType.Cylinder,new Vector3(0,0,0),new Vector3(.040f,.073f,.040f),new Color(.38f,.76f,.96f),"Water bottle");
                Shape(root,PrimitiveType.Cylinder,new Vector3(0,.041f,0),new Vector3(.029f,.015f,.029f),Cream,"Bottle cap");
                Shape(root,PrimitiveType.Sphere,new Vector3(0,-.012f,-.031f),new Vector3(.025f,.020f,.005f),new Color(.76f,.94f,1),"Water drop label");
            }
            else
            {
                Shape(root,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.064f,.010f,.064f),new Color(.73f,.43f,.20f),"Cookie");
                for(int i=0;i<7;i++){float a=i*2.40f;Shape(root,PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.018f,.011f,Mathf.Cos(a)*.018f),Vector3.one*.009f,new Color(.22f,.09f,.06f),"Chocolate chip");}
            }
            root.localScale=Vector3.one*(kind==FoodKind.Donut?1:kind==FoodKind.HotDog?.48f:kind==FoodKind.Water?.8f:.58f);
            return root;
        }
    }
}
