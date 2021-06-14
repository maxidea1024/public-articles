using System;
namespace G.BehaviourTree
{
    public class ConditionNode : BTNode
    {
        private Func<float, bool> FuncCondition;

        public ConditionNode(string name, Func<float, bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            if( FuncCondition == null ) return eBTStatus.Success;

            return FuncCondition(deltaTime) ? eBTStatus.Success : eBTStatus.Failure;
        }
    }

    public class ConditionNNode : BTNode
    {
        private Func<bool> FuncCondition;

        public ConditionNNode(string name, Func<bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Success;
            return FuncCondition() ? eBTStatus.Success : eBTStatus.Failure;
        }
    }

    public class ConditionTNode<T> : BTNode
    {
        private T V;
        private Func<T,bool> FuncCondition;

        public ConditionTNode(string name, Func<T,bool> funcCondition, T v) : base(name)
        {
            V = v;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Success;
            return FuncCondition(V) ? eBTStatus.Success : eBTStatus.Failure;
        }
    }
    public class ConditionTNode<T1,T2> : BTNode
    {
        private T1 V1;
        private T2 V2;
        private Func<T1, T2, bool> FuncCondition;

        public ConditionTNode(string name, Func<T1, T2, bool> funcCondition, T1 v1, T2 v2) : base(name)
        {
            V1 = v1;
            V2 = v2;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Success;
            return FuncCondition(V1,V2) ? eBTStatus.Success : eBTStatus.Failure;
        }
    }

}
