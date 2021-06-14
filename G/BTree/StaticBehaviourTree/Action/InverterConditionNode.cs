using System;
namespace G.StaticBehaviourTree
{
    public class InverterConditionNode<TA> : BTNode<TA>
    {
        private Func<TA, float, bool> FuncCondition;

        public InverterConditionNode(string name, Func<TA, float, bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;

            return FuncCondition(owner, deltaTime) ? eBTStatus.Failure : eBTStatus.Success;
        }
    }


    public class InverterConditionNoTimeNode<TA> : BTNode<TA>
    {
        private Func<TA, bool> FuncCondition;

        public InverterConditionNoTimeNode(string name, Func<TA, bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;

            return FuncCondition(owner) ? eBTStatus.Failure : eBTStatus.Success;
        }
    }


    public class InverterConditionTNode<TA,T> : BTNode<TA>
    {
        private T V;
        private Func<TA, T, bool> FuncCondition;

        public InverterConditionTNode(string name, Func<TA, T, bool> funcCondition, T v) : base(name)
        {
            V = v;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;
            return FuncCondition(owner, V) ? eBTStatus.Failure : eBTStatus.Success;
        }
    }
    public class InverterConditionTNode<TA,T1, T2> : BTNode<TA>
    {
        private T1 V1;
        private T2 V2;
        private Func<TA, T1, T2, bool> FuncCondition;

        public InverterConditionTNode(string name, Func<TA, T1, T2, bool> funcCondition, T1 v1, T2 v2) : base(name)
        {
            V1 = v1;
            V2 = v2;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;
            return FuncCondition(owner, V1, V2) ? eBTStatus.Failure : eBTStatus.Success;
        }
    }


}
