using System;

namespace TabletopMayhem.Core
{
    public enum CareAction { Feed, Water, Sing, Cuddle }
    public enum CareResult { AlreadyDone, TaskCompleted, BondLevelUp }

    // Four different kinds of care complete a round; progress survives app restarts.
    public sealed class PetCareProgress
    {
        const int AllTasks=15;
        public int BondLevel { get; private set; } = 1;
        public int CompletedMask { get; private set; }
        public int CompletedRounds { get; private set; }
        public int TasksCompleted
        {
            get
            {
                int count=0;
                for(int i=0;i<4;i++)if((CompletedMask&(1<<i))!=0)count++;
                return count;
            }
        }
        public CareAction NextTask
        {
            get
            {
                for(int i=0;i<4;i++)if((CompletedMask&(1<<i))==0)return (CareAction)i;
                return CareAction.Feed;
            }
        }
        public bool HasCompleted(CareAction action){return (CompletedMask&(1<<(int)action))!=0;}
        public CareResult Record(CareAction action)
        {
            int bit=1<<(int)action;
            if((CompletedMask&bit)!=0)return CareResult.AlreadyDone;
            CompletedMask|=bit;
            if(CompletedMask!=AllTasks)return CareResult.TaskCompleted;
            CompletedMask=0;
            if(BondLevel<999)BondLevel++;
            if(CompletedRounds<int.MaxValue)CompletedRounds++;
            return CareResult.BondLevelUp;
        }
        public void Restore(int level,int mask,int rounds)
        {
            BondLevel=Math.Max(1,Math.Min(999,level));
            CompletedMask=mask&AllTasks;
            if(CompletedMask==AllTasks)CompletedMask=0;
            CompletedRounds=Math.Max(0,rounds);
        }
    }
}
