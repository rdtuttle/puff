using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TabletopMayhem
{
    // Quest spatial-anchor persistence is optional: if the provider cannot
    // relocalize, the normal scanned-surface placement UI remains available.
    public sealed class PuffHomeAnchor
    {
        readonly ARAnchorManager manager;
        ARAnchor live;
        int generation;
        public PuffHomeAnchor(Transform origin)
        {manager=origin.gameObject.AddComponent<ARAnchorManager>();}
        static string Key(int slot){return "PUFF_HOME_ANCHOR_"+slot;}
        async System.Threading.Tasks.Task<bool> Ready()
        {
            for(int i=0;i<120;i++)
            {
                if(manager&&manager.isActiveAndEnabled&&manager.subsystem!=null&&manager.descriptor!=null)return true;
                await Awaitable.NextFrameAsync();
            }
            return false;
        }
        public async void Restore(int slot,Action<Pose> onRestored)
        {
            int request=++generation;
            string saved=PlayerPrefs.GetString(Key(slot),"");
            Guid id;if(!Guid.TryParse(saved,out id))return;
            try
            {
                if(!await Ready()||request!=generation||!manager.descriptor.supportsLoadAnchor)return;
                var result=await manager.TryLoadAnchorAsync(new SerializableGuid(id));
                if(request!=generation)return;
                if(result.status.IsSuccess()&&result.value)
                {
                    live=result.value;
                    onRestored(new Pose(live.transform.position,live.transform.rotation));
                    Debug.Log("PUFF_HOME_RESTORED slot="+slot);
                }
                else Debug.LogWarning("PUFF_HOME_RELOCALIZATION_FAILED slot="+slot);
            }
            catch(Exception e){Debug.LogWarning("PUFF_HOME_RESTORE_UNAVAILABLE "+e.Message);}
        }
        public async void Save(int slot,Pose pose)
        {
            int request=++generation;
            try
            {
                if(!await Ready()||request!=generation||!manager.descriptor.supportsSaveAnchor)return;
                var added=await manager.TryAddAnchorAsync(pose);
                if(request!=generation||!added.status.IsSuccess()||!added.value)return;
                var saved=await manager.TrySaveAnchorAsync(added.value);
                if(request!=generation||!saved.status.IsSuccess())return;
                if(live&&live!=added.value)UnityEngine.Object.Destroy(live.gameObject);
                live=added.value;
                PlayerPrefs.SetString(Key(slot),saved.value.guid.ToString("D"));
                PlayerPrefs.Save();
                Debug.Log("PUFF_HOME_SAVED slot="+slot);
            }
            catch(Exception e){Debug.LogWarning("PUFF_HOME_SAVE_UNAVAILABLE "+e.Message);}
        }
        public void Cancel(){generation++;}
    }
}
