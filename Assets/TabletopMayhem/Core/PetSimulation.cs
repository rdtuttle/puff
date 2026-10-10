using System;
namespace TabletopMayhem.Core
{
    public enum FoodKind { Donut, Burger, HotDog, Cookie, Water }
    public enum PetAction { Idle, Wander, Curious, Sleep, Sing, Celebrate, Play, Attention, Angry, Catch, Held, Airborne, Petted, Sick, Dead }
    public struct PetPoint
    {
        public float X,Y,Z;
        public PetPoint(float x,float y,float z){X=x;Y=y;Z=z;}
    }
    public struct PetHit { public PetPoint Position,Normal; }
    public interface IPetWorld
    {
        bool Ground(float x,float z,float fromY,out float height);
        PetPoint Move(PetPoint from,PetPoint to,float radius,float height);
        bool Cast(PetPoint from,PetPoint to,out PetHit hit);
    }
    // Mood is independent of Unity so feeding, rest and persistence are deterministic.
    public sealed class PetSimulation
    {
        public float Hunger {get;private set;} = 24;
        public float Boredom {get;private set;} = 18;
        public float Sleepiness {get;private set;} = 12;
        public float Thirst {get;private set;} = 22;
        public float Health {get;private set;} = 100;
        public double AgeRealSeconds {get;private set;}
        public bool IsDead {get;private set;}
        public bool IsSick {get{return !IsDead&&Health<35;}}
        public float SongTime {get;private set;}
        public float ExcitementTime {get;private set;}
        public float SleepTime {get;private set;}
        public float AgeSeconds {get;private set;}
        public float ReactionTime {get;private set;}
        public FoodKind LastFood {get;private set;}
        public int FoodEaten {get;private set;}
        public PetAction Action {get;private set;} = PetAction.Idle;
        float awakeTime;
        float nextPlay=18,nextAttention=27;
        public float Energy {get{return 100-Sleepiness;}}
        public void Reset()
        {
            Hunger=24;Thirst=22;Health=100;AgeRealSeconds=0;IsDead=false;Boredom=18;Sleepiness=12;SongTime=ExcitementTime=SleepTime=ReactionTime=0;
            AgeSeconds=awakeTime=0;nextPlay=18;nextAttention=27;FoodEaten=0;LastFood=FoodKind.Donut;Action=PetAction.Idle;
        }
        public void Restore(float hunger,float boredom,float sleepiness,double awaySeconds)
        {
            awaySeconds=Math.Max(0,Math.Min(awaySeconds,8*3600));
            Hunger=Clamp(hunger+(float)awaySeconds*.024f);
            Boredom=Clamp(boredom+(float)awaySeconds*.029f);
            Sleepiness=Clamp(sleepiness+(float)awaySeconds*.018f);
        }
        public void LoadProfile(PetProfile profile,double elapsedSeconds)
        {
            if(profile==null)throw new ArgumentNullException("profile");
            Reset();Hunger=Clamp(profile.Hunger);Thirst=Clamp(profile.Thirst);Boredom=Clamp(profile.Boredom);
            Sleepiness=Clamp(profile.Sleepiness);Health=Clamp(profile.Health);
            AgeRealSeconds=Math.Max(0,profile.AgeSeconds);IsDead=profile.Dead||Health<=0;
            AdvanceLife(Math.Max(0,elapsedSeconds));
            Action=IsDead?PetAction.Dead:IsSick?PetAction.Sick:PetAction.Idle;
        }
        public void SaveTo(PetProfile profile)
        {
            if(profile==null)throw new ArgumentNullException("profile");
            profile.Hunger=Hunger;profile.Thirst=Thirst;profile.Boredom=Boredom;profile.Sleepiness=Sleepiness;
            profile.Health=Health;profile.AgeSeconds=AgeRealSeconds;profile.Dead=IsDead;
        }
        public void AdvanceLife(double elapsedSeconds)
        {
            if(IsDead||elapsedSeconds<=0)return;
            // Hourly steps account for the changing starvation/dehydration threshold
            // while remaining cheap even after weeks away from the headset.
            double remaining=Math.Min(elapsedSeconds,366.0*86400);
            while(remaining>0&&!IsDead)
            {
                double step=Math.Min(remaining,3600);
                float midpointH=Clamp(Hunger+(float)(step*25/86400));
                float midpointW=Clamp(Thirst+(float)(step*38/86400));
                float daily=midpointH<35&&midpointW<35&&Boredom<60?3.2f:7f;
                if(midpointH>70)daily+=(midpointH-70)*.45f;
                if(midpointW>70)daily+=(midpointW-70)*.8f;
                if(midpointH>=95)daily+=8;
                if(midpointW>=95)daily+=25;
                double days=AgeRealSeconds/86400;
                if(days>14)daily+=(float)Math.Min(15,(days-14)*.25);
                Hunger=Clamp(Hunger+(float)(step*50/86400));
                Thirst=Clamp(Thirst+(float)(step*76/86400));
                Health=Clamp(Health-(float)(daily*step/86400));
                AgeRealSeconds+=step;remaining-=step;
                if(Health<=0){Health=0;IsDead=true;Action=PetAction.Dead;}
            }
        }
        public bool Poke(float songSeconds)
        {
            if(IsDead||Action==PetAction.Sing)return false;
            Wake();Boredom=Clamp(Boredom-8);awakeTime=0;
            SongTime=Math.Max(.1f,songSeconds);Action=PetAction.Sing;return true;
        }
        public bool Feed(FoodKind food)
        {
            if(food==FoodKind.Water)return Drink();
            if(IsDead||Action==PetAction.Sing)return false;
            float before=Hunger;
            Wake();LastFood=food;FoodEaten++;
            float filling=food==FoodKind.Burger?35:food==FoodKind.HotDog?28:food==FoodKind.Donut?24:15;
            Hunger=Clamp(Hunger-filling);
            Boredom=Clamp(Boredom-(food==FoodKind.Cookie?18:food==FoodKind.Donut?10:7));
            Sleepiness=Clamp(Sleepiness+(food==FoodKind.Burger?3:0));
            if(before>12)Health=Clamp(Health+1);
            ExcitementTime=3;awakeTime=0;Action=PetAction.Celebrate;return true;
        }
        public bool Drink()
        {
            if(IsDead||Action==PetAction.Sing)return false;
            Wake();float before=Thirst;Thirst=Clamp(Thirst-58);
            if(before>12)Health=Clamp(Health+2);
            Boredom=Clamp(Boredom-3);ExcitementTime=2;awakeTime=0;Action=PetAction.Celebrate;return true;
        }
        public bool Pet()
        {
            if(IsDead||Action==PetAction.Sing||Action==PetAction.Held)return false;
            Wake();Boredom=Clamp(Boredom-12);Sleepiness=Clamp(Sleepiness-2);
            Health=Clamp(Health+.7f);ReactionTime=1.8f;Action=PetAction.Petted;return true;
        }
        public bool RewardDailyCare()
        {
            if(IsDead)return false;
            Health=Clamp(Health+3);Boredom=Clamp(Boredom-8);
            ExcitementTime=2;Action=PetAction.Celebrate;return true;
        }
        public bool CatchFood(FoodKind food)
        {
            if(!Feed(food))return false;
            ReactionTime=1.7f;Action=PetAction.Catch;return true;
        }
        public void PickUp(){if(IsDead)return;Wake();Action=PetAction.Held;ReactionTime=0;Boredom=Clamp(Boredom-3);}
        public void Throw(){if(Action==PetAction.Held){Action=PetAction.Airborne;ReactionTime=0;}}
        public void Land(){if(Action==PetAction.Airborne){Action=PetAction.Idle;awakeTime=0;}}
        public void Punch()
        {
            if(Action==PetAction.Sing)return;
            Attacked();
        }
        public void Attacked()
        {
            if(IsDead)return;
            SongTime=0;Wake();Boredom=Clamp(Boredom+5);ReactionTime=2.2f;Action=PetAction.Angry;
        }
        public void Advance(float dt)
        {
            if(dt<=0||IsDead)return;dt=Math.Min(dt,.1f);AgeSeconds+=dt;AdvanceLife(dt);
            if(IsDead)return;
            if(Action==PetAction.Sleep)
            {
                Sleepiness=Clamp(Sleepiness-dt*2.1f);
                SleepTime-=dt;if(SleepTime<=0){SleepTime=0;Action=PetAction.Idle;awakeTime=0;}
                return;
            }
            Sleepiness=Clamp(Sleepiness+dt*.085f);
            Boredom=Clamp(Boredom+dt*(Action==PetAction.Sing?-.4f:.13f));
            if(Action==PetAction.Sing)
            {SongTime-=dt;if(SongTime<=0){SongTime=0;Action=PetAction.Idle;}return;}
            if(Action==PetAction.Held||Action==PetAction.Airborne)return;
            if(Action==PetAction.Play||Action==PetAction.Attention||Action==PetAction.Angry||Action==PetAction.Catch||Action==PetAction.Petted)
            {ReactionTime-=dt;if(ReactionTime<=0){ReactionTime=0;Action=PetAction.Idle;}return;}
            if(Action==PetAction.Celebrate)
            {ExcitementTime-=dt;if(ExcitementTime<=0){ExcitementTime=0;Action=PetAction.Idle;}return;}
            if(IsSick){Action=PetAction.Sick;return;}
            if(Action==PetAction.Sick)Action=PetAction.Idle;
            awakeTime+=dt;
            if(Sleepiness>=75||awakeTime>90)
            {SleepTime=Sleepiness>=75?18:12;Action=PetAction.Sleep;}
            else if(AgeSeconds>=nextAttention&&(Boredom>=50||Hunger>=65))
            {nextAttention=AgeSeconds+25;ReactionTime=3;Action=PetAction.Attention;}
            else if(AgeSeconds>=nextPlay)
            {nextPlay=AgeSeconds+27;ReactionTime=2.8f;Boredom=Clamp(Boredom-3);Action=PetAction.Play;}
        }
        public void SetCurious(bool walking)
        {
            if(IsDead||IsSick)return;
            if(Action==PetAction.Idle||Action==PetAction.Wander||Action==PetAction.Curious)
                Action=walking?PetAction.Wander:PetAction.Curious;
        }
        public void Wake(){SleepTime=0;if(Action==PetAction.Sleep)Action=PetAction.Idle;}
        public void EnterFight()
        {
            if(IsDead)return;
            SongTime=SleepTime=ReactionTime=ExcitementTime=0;
            Action=PetAction.Idle;
        }
        public void PrepareForPlacement()
        {
            if(IsDead){Action=PetAction.Dead;return;}
            // Give a returning pet a short awake window before its first nap.
            SongTime=SleepTime=ReactionTime=ExcitementTime=0;
            Sleepiness=Math.Min(Sleepiness,65);
            awakeTime=0;nextPlay=AgeSeconds+10;nextAttention=AgeSeconds+20;
            Action=IsSick?PetAction.Sick:PetAction.Idle;
        }
        static float Clamp(float v){return Math.Max(0,Math.Min(100,v));}
    }
}
