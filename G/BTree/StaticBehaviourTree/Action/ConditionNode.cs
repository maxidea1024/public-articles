using System;
namespace G.StaticBehaviourTree
{
    public class ConditionNode<TA> : BTNode<TA>
    {
        private Func<TA, float, bool> FuncCondition;

        public ConditionNode(string name, Func<TA, float, bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if( FuncCondition == null ) return eBTStatus.Success;

            return FuncCondition(owner, deltaTime) ? eBTStatus.Success : eBTStatus.Failure;
        }
    }

    public class ConditionNNode<TA> : BTNode<TA>
    {
        private Func<TA,bool> FuncCondition;

        public ConditionNNode(string name, Func<TA,bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Success;
            return FuncCondition(owner) ? eBTStatus.Success : eBTStatus.Failure;
        }
    }

    public class ConditionTNode<TA,T> : BTNode<TA>
    {
        private T V;
        private Func<TA,T,bool> FuncCondition;

        public ConditionTNode(string name, Func<TA,T,bool> funcCondition, T v) : base(name)
        {
            V = v;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Success;
            return FuncCondition(owner,V) ? eBTStatus.Success : eBTStatus.Failure;
        }
    }
    public class ConditionTNode<TA,T1,T2> : BTNode<TA>
    {
        private T1 V1;
        private T2 V2;
        private Func<TA,T1, T2, bool> FuncCondition;

        public ConditionTNode(string name, Func<TA,T1, T2, bool> funcCondition, T1 v1, T2 v2) : base(name)
        {
            V1 = v1;
            V2 = v2;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Success;
            return FuncCondition(owner,V1,V2) ? eBTStatus.Success : eBTStatus.Failure;
        }
    }

}
