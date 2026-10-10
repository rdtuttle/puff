using System;
using TabletopMayhem.Core;
static class PetTests
{
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static void Step(PetSimulation p,float seconds){for(int i=0;i<(int)(seconds*20);i++)p.Advance(.05f);}
    static void Main()
    {
        var p=new PetSimulation();
        Check(p.Hunger==24&&p.Boredom==18&&p.Sleepiness==12,"initial needs");
        Step(p,20);
        Check(p.Hunger>24&&p.Sleepiness>12&&p.Boredom>=0,"needs progress");
        p.Restore(90,80,70,36000);
        Check(p.Hunger==100&&p.Boredom==100&&p.Sleepiness==100,"offline needs capped");
        Check(p.Feed(FoodKind.Burger),"burger accepted");
        Check(p.Action==PetAction.Celebrate&&p.Hunger<70,"burger fills and excites");
        Check(p.FoodEaten==1&&p.LastFood==FoodKind.Burger,"food recorded");
        Step(p,3.5f);
        Check(p.Action!=PetAction.Celebrate,"celebration finishes");
        Check(p.Poke(2),"belly poke starts song");
        Check(!p.Poke(2)&&!p.Feed(FoodKind.Donut),"singing cannot restart or eat");
        Step(p,2.1f);
        Check(p.Action!=PetAction.Sing,"song ends");
        Check(p.Feed(FoodKind.Cookie),"cookie accepted after song");
        Check(p.Boredom<100&&p.FoodEaten==2,"cookie reduces boredom");
        var tired=new PetSimulation();tired.Restore(10,10,74.9f,0);Step(tired,2);
        Check(tired.Action==PetAction.Sleep,"sleep when tired");
        float before=tired.Sleepiness;Step(tired,10);
        Check(tired.Sleepiness<before,"nap restores energy");
        tired.Wake();Check(tired.Action==PetAction.Idle,"wake from nap");
        tired.SetCurious(true);Check(tired.Action==PetAction.Wander,"exploring state");
        tired.SetCurious(false);Check(tired.Action==PetAction.Curious,"curious idle state");
        tired.PickUp();Check(tired.Action==PetAction.Held,"pickup state");
        tired.Throw();Check(tired.Action==PetAction.Airborne,"throw state");
        tired.Land();Check(tired.Action==PetAction.Idle,"land state");
        tired.Punch();Check(tired.Action==PetAction.Angry,"punch reaction");
        Step(tired,2.3f);Check(tired.Action!=PetAction.Angry,"angry reaction ends");
        tired.Poke(40);tired.Attacked();Check(tired.Action==PetAction.Angry&&tired.SongTime==0,"toy hit interrupts singing and makes pet angry");
        Check(tired.CatchFood(FoodKind.HotDog),"catch food");
        Check(tired.Action==PetAction.Catch&&tired.LastFood==FoodKind.HotDog,"catch animation state");
        tired.Reset();Check(tired.Hunger==24&&tired.Boredom==18&&tired.Sleepiness==12&&tired.Action==PetAction.Idle,"reset restores pet");
        Check(tired.Energy==88,"energy mirrors sleepiness");
        tired.Poke(40);tired.Reset();Check(tired.Action==PetAction.Idle&&tired.SongTime==0,"reset cancels song");
        tired.Poke(40);float fightHunger=tired.Hunger;tired.EnterFight();
        Check(tired.Action==PetAction.Idle&&tired.SongTime==0&&tired.Hunger==fightHunger,"fight transition clears pose without changing care");
        tired.Restore(30,30,100,0);tired.Poke(40);tired.PrepareForPlacement();
        Check(tired.Action==PetAction.Idle&&tired.SongTime==0&&tired.Sleepiness<=65,"placement restores an awake pet without a stale song");
        Step(tired,10.2f);Check(tired.Action==PetAction.Play,"pet begins playful exercise after placement");
        var care=new PetCareProgress();
        Check(care.BondLevel==1&&care.TasksCompleted==0&&care.NextTask==CareAction.Feed,"new care round starts empty");
        Check(care.Record(CareAction.Feed)==CareResult.TaskCompleted&&care.TasksCompleted==1,"feeding advances care round");
        Check(care.Record(CareAction.Feed)==CareResult.AlreadyDone&&care.TasksCompleted==1,"repeating one task cannot farm bond");
        Check(care.Record(CareAction.Water)==CareResult.TaskCompleted&&care.TasksCompleted==2,"water advances care round");
        Check(care.Record(CareAction.Sing)==CareResult.TaskCompleted&&care.NextTask==CareAction.Cuddle,"song advances care round");
        Check(care.Record(CareAction.Cuddle)==CareResult.BondLevelUp&&care.BondLevel==2&&care.CompletedRounds==1&&care.TasksCompleted==0,"all four tasks raise bond and begin another round");
        care.Record(CareAction.Sing);var saved=new PetCareProgress();saved.Restore(care.BondLevel,care.CompletedMask,care.CompletedRounds);
        Check(saved.BondLevel==2&&saved.HasCompleted(CareAction.Sing)&&saved.NextTask==CareAction.Feed,"care progress restores across launches");
        saved.Restore(-5,255,-1);Check(saved.BondLevel==1&&saved.CompletedRounds==0&&saved.TasksCompleted==0,"invalid saved care progress is bounded");
        var roster=new PetRoster();
        for(int i=0;i<PetRoster.Capacity;i++)Check(roster.Create(i,"Puff "+(i+1),1000+i,i),"create each of five pet slots");
        Check(roster.OccupiedCount==5&&!roster.Create(0,"Extra",2000)&&!roster.Create(5,"Extra",2000),"roster cannot exceed five pets");
        Check(!roster.ReplaceDeceased(0,"New",2000),"living pet cannot be replaced");
        roster.Get(0).Dead=true;Check(roster.ReplaceDeceased(0,"New Puff",2000)&&roster.Get(0).Name=="New Puff","deceased slot can start a new pet");
        var life=new PetSimulation();var profile=new PetProfile{Name="Mochi",BornUtc=1000,LastSavedUtc=1000};
        life.LoadProfile(profile,0);Check(life.Health==100&&life.Thirst==22&&!life.IsDead,"new pet life state");
        life.AdvanceLife(2.8*86400);Check(life.IsDead&&life.Action==PetAction.Dead,"unfed and unwatered pet dies within a few days");
        var cared=new PetSimulation();cared.LoadProfile(profile,0);
        for(int day=0;day<16;day++){cared.AdvanceLife(43200);cared.Feed(FoodKind.Burger);cared.Drink();cared.AdvanceLife(43200);cared.Feed(FoodKind.Burger);cared.Drink();}
        Check(!cared.IsDead&&cared.Health>0&&cared.AgeRealSeconds>=16*86400,"consistent care extends life beyond fourteen days");
        var thirsty=new PetSimulation();thirsty.LoadProfile(profile,86400);
        float thirstBefore=thirsty.Thirst;Check(thirsty.Drink()&&thirsty.Thirst<thirstBefore,"water restores thirst");
        float boredomBefore=thirsty.Boredom;Check(thirsty.Pet()&&thirsty.Action==PetAction.Petted&&thirsty.Boredom<boredomBefore,"petting makes pet happier");
        float rewardHealth=thirsty.Health;float rewardBoredom=thirsty.Boredom;
        Check(thirsty.RewardDailyCare()&&thirsty.Health>=rewardHealth&&thirsty.Boredom<=rewardBoredom,"daily care reward improves pet health and mood");
        thirsty.SaveTo(profile);var resumed=new PetSimulation();resumed.LoadProfile(profile,0);
        Check(Math.Abs(resumed.AgeRealSeconds-thirsty.AgeRealSeconds)<1&&Math.Abs(resumed.Health-thirsty.Health)<.01,"life state saves and restores");
        Console.WriteLine("Passed "+checks+" virtual pet checks.");
    }
}
