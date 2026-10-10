using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TabletopMayhem.Editor
{
    public static class PetContentCheck
    {
        public static void Verify()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TabletopMayhem/Resources/Fighters/Puff.fbx");
            if(!model)throw new Exception("Pet FBX missing");
            var names=model.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
            foreach(var expected in new[]{"Body","LeftArm","RightArm","LeftLeg","RightLeg","Mouth","SleepFace"})
                if(!names.Contains(expected))throw new Exception("Pet rig part missing: "+expected+"; found "+string.Join(",",names));
            var song=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TabletopMayhem/Resources/Audio/PuffSong.mp3");
            if(!song||song.length<35||song.length>45)throw new Exception("Expected a 40-second pet song");
            var mood=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TabletopMayhem/Resources/Audio/PuffMood.mp3");
            if(!mood||mood.length<.4f||mood.length>1.5f)throw new Exception("Expected the short Puff mood sound");
            if(!AssetDatabase.LoadAssetAtPath<Shader>("Assets/TabletopMayhem/Resources/PetGlove.shader"))throw new Exception("White glove shader missing");
            if(!AssetDatabase.LoadAssetAtPath<Shader>("Assets/TabletopMayhem/Resources/PetAimLine.shader"))throw new Exception("Placement-ray shader missing");
            int renderers=model.GetComponentsInChildren<MeshRenderer>(true).Length;
            if(renderers<7)throw new Exception("Pet mesh parts missing");
            foreach(var rival in new[]{"KarateComet","Clawstorm","MeatballMarauder"})
            {
                var enemy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TabletopMayhem/Resources/Fighters/"+rival+".fbx");
                if(!enemy)throw new Exception("Rival FBX missing: "+rival);
                var parts=enemy.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
                foreach(var expected in new[]{"Body","LeftArm","RightArm","LeftLeg","RightLeg"})
                    if(!parts.Contains(expected))throw new Exception(rival+" missing animated part: "+expected);
            }
            Debug.Log("PET_CONTENT_OK rig_parts="+renderers+" song_seconds="+song.length+" mood_seconds="+mood.length);
        }
    }
}
