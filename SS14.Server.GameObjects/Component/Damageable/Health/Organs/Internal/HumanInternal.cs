﻿

namespace SS14.Server.GameObjects.Organs.Human
{

    public class Brain : InternalOrgan
    {
        public Brain(HumanHealthComponent _owner, ExternalOrgan _organ)
        {
            Name = "Brain";
            Damage = 0;
            Parent = _organ;
            
        }
    }


    public class Heart : InternalOrgan
    {
        public Heart(HumanHealthComponent _owner, ExternalOrgan _organ)
        {
            Name = "Heart";
            Damage = 0;
            Parent = _organ;
        }
    }


    public class Lungs : InternalOrgan
    {
        public Lungs(HumanHealthComponent _owner, ExternalOrgan _organ)
        {
            Name = "Lungs";
            Damage = 0;
            Parent = _organ;
        }
    }


    public class Liver : InternalOrgan
    {
        public Liver(HumanHealthComponent _owner, ExternalOrgan _organ)
        {
            Name = "Liver";
            Damage = 0;
            Parent = _organ;
        }
    }






}