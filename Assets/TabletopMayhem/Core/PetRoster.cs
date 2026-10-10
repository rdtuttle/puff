using System;

namespace TabletopMayhem.Core
{
    public sealed class PetProfile
    {
        public string Name;
        public long BornUtc;
        public long LastSavedUtc;
        public float Hunger=24;
        public float Thirst=22;
        public float Boredom=18;
        public float Sleepiness=12;
        public float Health=100;
        public bool Dead;
        public int BondLevel=1;
        public int CareMask;
        public int CareRounds;
        public int Style;
        public double AgeSeconds;
    }

    public sealed class PetRoster
    {
        public const int Capacity=5;
        readonly PetProfile[] slots=new PetProfile[Capacity];
        public PetProfile Get(int index){return index>=0&&index<Capacity?slots[index]:null;}
        public int OccupiedCount
        {
            get{int count=0;foreach(var slot in slots)if(slot!=null)count++;return count;}
        }
        public bool Put(int index,PetProfile profile)
        {
            if(index<0||index>=Capacity||profile==null)return false;
            slots[index]=profile;return true;
        }
        public bool Create(int index,string name,long nowUtc,int style=0)
        {
            if(index<0||index>=Capacity||slots[index]!=null)return false;
            slots[index]=new PetProfile{Name=SafeName(name),BornUtc=nowUtc,LastSavedUtc=nowUtc,Style=Math.Max(0,Math.Min(4,style))};
            return true;
        }
        public bool ReplaceDeceased(int index,string name,long nowUtc,int style=0)
        {
            if(index<0||index>=Capacity||slots[index]==null||!slots[index].Dead)return false;
            slots[index]=null;return Create(index,name,nowUtc,style);
        }
        public static string SafeName(string name)
        {
            if(string.IsNullOrWhiteSpace(name))return "Puff";
            name=name.Trim();return name.Length>16?name.Substring(0,16):name;
        }
    }
}
