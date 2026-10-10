using UnityEngine;
using TabletopMayhem.Core;
namespace TabletopMayhem
{
    public sealed class PetMotion : MonoBehaviour
    {
        Transform body,leftArm,rightArm,leftLeg,rightLeg,mouth,sleepFace;
        Quaternion bodyRest,leftArmRest,rightArmRest,leftLegRest,rightLegRest;
        Vector3 mouthRest,bodyScale;
        float gait,songGait,exciteGait,mouthLevel;
        int style;
        public void SetStyle(int value){style=Mathf.Clamp(value,0,4);}
        readonly float[] audioSamples=new float[256];
        public void Initialize(Transform model,Transform sleepingFace,Transform mouthNode)
        {
            sleepFace=sleepingFace;mouth=mouthNode;
            foreach(var part in model.GetComponentsInChildren<Transform>(true))
            {if(part.name=="Body")body=part;else if(part.name=="LeftArm")leftArm=part;else if(part.name=="RightArm")rightArm=part;else if(part.name=="LeftLeg")leftLeg=part;else if(part.name=="RightLeg")rightLeg=part;}
            bodyRest=body?body.localRotation:Quaternion.identity;bodyScale=body?body.localScale:Vector3.one;
            leftArmRest=leftArm?leftArm.localRotation:Quaternion.identity;rightArmRest=rightArm?rightArm.localRotation:Quaternion.identity;
            leftLegRest=leftLeg?leftLeg.localRotation:Quaternion.identity;rightLegRest=rightLeg?rightLeg.localRotation:Quaternion.identity;
            mouthRest=mouth?mouth.localScale:Vector3.one;
        }
        public void Pose(PetSimulation pet,float speed,AudioSource singing,float dt)
        {
            dt=Mathf.Min(dt,.04f);gait+=dt*(2+speed*25);
            bool sing=pet.Action==PetAction.Sing,sleep=pet.Action==PetAction.Sleep,celebrate=pet.Action==PetAction.Celebrate;
            bool play=pet.Action==PetAction.Play,attention=pet.Action==PetAction.Attention,angry=pet.Action==PetAction.Angry,curious=pet.Action==PetAction.Curious;
            bool catching=pet.Action==PetAction.Catch,held=pet.Action==PetAction.Held,air=pet.Action==PetAction.Airborne;
            bool petted=pet.Action==PetAction.Petted,sick=pet.Action==PetAction.Sick,dead=pet.Action==PetAction.Dead;
            if(sleepFace)sleepFace.gameObject.SetActive(sleep||dead);
            float breath=Mathf.Sin(Time.time*(sleep?1.6f:3.5f));
            float walk=Mathf.Clamp01(speed/.2f)*Mathf.Sin(gait);
            if(sing)songGait+=dt*7;else songGait=0;
            if(celebrate||catching||play||attention||angry)exciteGait+=dt*(catching?13:angry?8:10);else exciteGait=0;
            float jack=play?.5f+.5f*Mathf.Sin(exciteGait*1.25f):0;
            float swing=sing?Mathf.Sin(songGait)*27:celebrate||catching?Mathf.Sin(exciteGait)*40:play?Mathf.Sin(exciteGait)*34:attention?Mathf.Sin(exciteGait)*55:angry?Mathf.Sin(exciteGait)*11:walk*22;
            float bob=sing?Mathf.Sin(songGait*2)*7:celebrate||catching?Mathf.Sin(exciteGait)*9:play?Mathf.Sin(exciteGait)*7:angry?-9:breath*(sleep?3:2);
            float idleTilt=curious?Mathf.Sin(Time.time*(style==1?2.2f:style==4?.9f:1.5f))*(style==2?8:4):0;
            float lean=catching?-24:attention?-12:angry?12:petted?-13:sick?16:held?-8:air?Mathf.Sin(Time.time*11)*12:idleTilt;
            Turn(body,bodyRest,Quaternion.Euler(bob+lean,curious?Mathf.Sin(Time.time*.8f+style)*5:0,sing?Mathf.Sin(songGait)*8:sleep?12:play?0:attention?Mathf.Sin(exciteGait)*10:angry?Mathf.Sin(exciteGait)*4:curious?Mathf.Sin(Time.time*1.1f+style)*6:walk*6),dt);
            if(body)
            {
                float squash=angry?.91f:catching?1.10f:petted?1.06f:sick?.94f:sleep||dead?.96f:1+breath*.008f;
                body.localScale=Vector3.Lerp(body.localScale,new Vector3(bodyScale.x/squash,bodyScale.y*squash,bodyScale.z/squash),1-Mathf.Exp(-dt*11));
            }
            Turn(leftArm,leftArmRest,Quaternion.Euler(sing?-35+Mathf.Sin(songGait)*24:sleep||dead?25:attention?-66:catching?-48:petted?-40+Mathf.Sin(Time.time*9)*14:held?-55:angry?33:sick?30:play?-15:-swing,0,play?-125*jack:sing?17:attention?22:petted?-18:0),dt);
            Turn(rightArm,rightArmRest,Quaternion.Euler(sing?-52-Mathf.Sin(songGait)*20:sleep||dead?25:attention?-72:catching?-52:petted?-40-Mathf.Sin(Time.time*9)*14:held?-55:angry?33:sick?30:play?-15:swing,0,play?125*jack:sing?-20:attention?-22:petted?18:0),dt);
            Turn(leftLeg,leftLegRest,Quaternion.Euler(sleep||dead?25:catching?-18:walk*18,0,play?-26*jack:0),dt);
            Turn(rightLeg,rightLegRest,Quaternion.Euler(sleep||dead?25:catching?-18:-walk*18,0,play?26*jack:0),dt);
            float target=0;
            if(sing&&singing&&singing.isPlaying)
            {
                singing.GetOutputData(audioSamples,0);float total=0;for(int i=0;i<audioSamples.Length;i++)total+=audioSamples[i]*audioSamples[i];
                target=Mathf.Clamp01(Mathf.Sqrt(total/audioSamples.Length)*8);
            }
            else if(catching)target=.95f;
            else if(celebrate)target=.35f+Mathf.Sin(exciteGait*2)*.2f;
            else if(attention)target=.42f;
            else if(angry)target=.24f;
            else if(petted)target=.28f+Mathf.Sin(Time.time*8)*.08f;
            mouthLevel=Mathf.Lerp(mouthLevel,target,1-Mathf.Exp(-dt*18));
            if(mouth)mouth.localScale=new Vector3(mouthRest.x,mouthRest.y*(.8f+mouthLevel*1.6f),mouthRest.z*(.75f+mouthLevel*.6f));
        }
        static void Turn(Transform joint,Quaternion rest,Quaternion pose,float dt)
        {if(joint)joint.localRotation=Quaternion.Slerp(joint.localRotation,rest*pose,1-Mathf.Exp(-dt*12));}
    }
}
