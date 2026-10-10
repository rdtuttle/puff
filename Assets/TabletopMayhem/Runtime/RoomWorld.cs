using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.Features.Meta;
using TabletopMayhem.Core;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace TabletopMayhem
{
    // Only layer 8 contains sensed room surfaces. The pet, food and UI never enter these queries.
    public sealed class RoomWorld : MonoBehaviour, IPetWorld
    {
        public const int RoomLayer = 8, RoomMask = 1 << RoomLayer;
        public string Status { get; private set; } = "Loading room scan...";
        public int SurfaceCount { get; private set; }
        public bool Ready { get { return SurfaceCount > 0; } }
        Transform anchor;
        bool quest, permitted;
        ARBoundingBoxManager boxes;
        ARMeshManager meshes;
        GameObject meshTemplate;
        Material occluder;
        float refresh;
        readonly RaycastHit[] surfaceHits = new RaycastHit[64];
        readonly Dictionary<TrackableId, GameObject> boxObjects = new Dictionary<TrackableId, GameObject>();
        readonly Dictionary<TrackableId, float> boxLastSeen = new Dictionary<TrackableId, float>();
        public void Initialize(Transform origin, Transform combatAnchor, bool isQuest)
        {
            anchor = combatAnchor; quest = isQuest;
            occluder = new Material(Resources.Load<Shader>("RoomOcclusion"));
            if (!quest) { CreateTestRoom(); return; }
            origin.gameObject.SetActive(false);
            boxes = origin.gameObject.AddComponent<ARBoundingBoxManager>(); boxes.enabled = false;
            var meshObject = new GameObject("Room mesh provider"); meshObject.transform.SetParent(origin, false);
            meshes = meshObject.AddComponent<ARMeshManager>(); meshes.enabled = false;
            meshTemplate = new GameObject("Room mesh collision template"); meshTemplate.layer = RoomLayer;
            var filter = meshTemplate.AddComponent<MeshFilter>();
            var meshRenderer = meshTemplate.AddComponent<MeshRenderer>();meshRenderer.sharedMaterial = occluder;meshRenderer.enabled = false;
            meshTemplate.AddComponent<MeshCollider>(); meshTemplate.SetActive(false);
            meshes.meshPrefab = filter; meshes.density = .6f; meshes.normals = false;
            origin.gameObject.SetActive(true);
        }
        void Start()
        {
            if (!quest) return;
#if UNITY_ANDROID
            const string permission = "com.oculus.permission.USE_SCENE";
            if (Permission.HasUserAuthorizedPermission(permission)) EnableScene();
            else
            {
                Status = "Allow Spatial Data to use your real room";
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => EnableScene();
                callbacks.PermissionDenied += _ => Status = "Spatial Data denied. Enable it in Quest app permissions.";
                Permission.RequestUserPermission(permission, callbacks);
            }
#endif
        }
        void EnableScene()
        {
            Debug.Log("PET_SCENE_PERMISSION_GRANTED"); permitted = true; boxes.enabled = true; meshes.enabled = true;
            Status = "Loading saved room. Left grip: scan room";
        }
        public void RequestScan()
        {
            if (!quest || !permitted) return;
            var sessions = new List<XRSessionSubsystem>(); SubsystemManager.GetSubsystems(sessions);
            foreach (var session in sessions)
                if (session is MetaOpenXRSessionSubsystem meta && meta.running)
                { Status = meta.TryRequestSceneCapture() ? "Complete Space Setup, then return here" : "Open Quest Settings > Physical Space > Space Setup"; return; }
            Status = "Room service starting. Try left grip again.";
        }
        void Update()
        {
            if (!quest || !permitted) return;
            refresh -= Time.unscaledDeltaTime; if (refresh > 0) return; refresh = .5f;
            var live = new HashSet<TrackableId>();
            // Quest scene entities can share an ID across plane and furniture types.
            // Use boxes plus scene meshes to avoid duplicate AR Foundation trackable registration.
            foreach(var b in boxes.trackables)
            {
                live.Add(b.trackableId);GameObject go;
                if(!boxObjects.TryGetValue(b.trackableId,out go)||!go)
                {
                    go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Real object / "+b.classifications;go.layer=RoomLayer;
                    // Keep collision proxies alive during brief scene-provider dropouts.
                    go.transform.SetParent(transform,true);
                    var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=occluder;renderer.enabled=false;
                    boxObjects[b.trackableId]=go;
                }
                go.transform.SetPositionAndRotation(b.transform.position,b.transform.rotation);
                go.transform.localScale=b.size;
                boxLastSeen[b.trackableId]=Time.unscaledTime;
            }
            RemoveStale(live);
            int count=boxObjects.Count+meshes.meshes.Count;
            if(count!=SurfaceCount)Debug.Log("PET_ROOM_SURFACES "+count);
            SurfaceCount=count;
            if(Ready)Status="REAL ROOM / "+SurfaceCount+" scanned surfaces";
            Physics.SyncTransforms();
        }
        void RemoveStale(HashSet<TrackableId> live)
        {
            var remove=new List<TrackableId>();
            foreach(var pair in boxObjects)
                if(!live.Contains(pair.Key)&&Time.unscaledTime-boxLastSeen[pair.Key]>3f)remove.Add(pair.Key);
            foreach(var id in remove){if(boxObjects[id])Destroy(boxObjects[id]);boxObjects.Remove(id);boxLastSeen.Remove(id);}
        }
        public bool AimSurface(Ray ray,out RaycastHit hit)
        {
            // A room box or wall can be in front of the table. Search every hit and
            // choose the nearest upward-facing surface rather than stopping there.
            int count=Physics.RaycastNonAlloc(ray,surfaceHits,8,RoomMask,QueryTriggerInteraction.Ignore);
            float nearest=float.MaxValue;hit=default(RaycastHit);bool found=false;
            for(int i=0;i<count;i++)
                if(surfaceHits[i].normal.y>.62f&&surfaceHits[i].distance<nearest)
                {nearest=surfaceHits[i].distance;hit=surfaceHits[i];found=true;}
            return found;
        }
        public bool Ground(float x,float z,float fromY,out float height)
        {
            RaycastHit hit;var start=anchor.TransformPoint(new Vector3(x,fromY+.005f,z));
            if(Physics.Raycast(start,Vector3.down,out hit,6,RoomMask,QueryTriggerInteraction.Ignore)&&hit.normal.y>.5f)
            {height=anchor.InverseTransformPoint(hit.point).y;return true;}
            height=0;return false;
        }
        public PetPoint Move(PetPoint from,PetPoint to,float radius,float height)
        {
            Vector3 start=ToWorld(from),remaining=ToWorld(to)-start;
            for(int i=0;i<3&&remaining.sqrMagnitude>.00000001f;i++)
            {
                RaycastHit hit;
                var bottom=start+Vector3.up*(radius+.009f);var top=start+Vector3.up*Mathf.Max(radius+.01f,height-radius);
                if(Physics.CapsuleCast(bottom,top,radius,remaining.normalized,out hit,remaining.magnitude+.003f,RoomMask,QueryTriggerInteraction.Ignore))
                {
                    float travel=Mathf.Max(0,hit.distance-.004f);var step=remaining.normalized*Mathf.Min(travel,remaining.magnitude);start+=step;
                    remaining=Vector3.ProjectOnPlane(remaining-step,hit.normal);
                }
                else{start+=remaining;break;}
            }
            return ToLocal(start);
        }
        public bool Cast(PetPoint from,PetPoint to,out PetHit result)
        {
            RaycastHit hit;var start=ToWorld(from);var delta=ToWorld(to)-start;
            if(delta.sqrMagnitude>.0000001f&&Physics.Raycast(start,delta.normalized,out hit,delta.magnitude,RoomMask,QueryTriggerInteraction.Ignore))
            {result=new PetHit{Position=ToLocal(hit.point),Normal=DirectionLocal(hit.normal)};return true;}
            result=default(PetHit);return false;
        }
        Vector3 ToWorld(PetPoint p){return anchor.TransformPoint(new Vector3(p.X,p.Y,p.Z));}
        PetPoint ToLocal(Vector3 p){var v=anchor.InverseTransformPoint(p);return new PetPoint(v.x,v.y,v.z);}
        PetPoint DirectionLocal(Vector3 p){var v=anchor.InverseTransformDirection(p);return new PetPoint(v.x,v.y,v.z);}
        void CreateTestRoom()
        {
            TestBox("Editor table",new Vector3(0,-.05f,0),new Vector3(2.6f,.1f,1.9f),new Color(.18f,.22f,.26f));
            TestBox("Editor real-object proxy / book",new Vector3(-.62f,.04f,.22f),new Vector3(.30f,.08f,.4f),new Color(.25f,.40f,.34f));
            TestBox("Editor real-object proxy / box",new Vector3(.63f,.12f,.35f),new Vector3(.27f,.24f,.27f),new Color(.45f,.26f,.14f));
            TestBox("Editor floor",new Vector3(0,-.8f,0),new Vector3(8,.1f,8),new Color(.08f,.10f,.13f));
            SurfaceCount=4;Status="EDITOR / collision test room";Physics.SyncTransforms();
        }
        void TestBox(string name,Vector3 position,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=RoomLayer;
            go.transform.position=anchor.TransformPoint(position);go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=PetArt.Mat(color);
            go.transform.SetParent(transform,true);
        }
        void OnDestroy(){if(meshTemplate)Destroy(meshTemplate);if(occluder)Destroy(occluder);}
    }
}
