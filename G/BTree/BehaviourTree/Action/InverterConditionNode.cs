using System;
namespace G.BehaviourTree
{
    public class InverterConditionNode : BTNode
    {
        private Func<float, bool> FuncCondition;

        public InverterConditionNode(string name, Func<float, bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;

            return FuncCondition(deltaTime) ? eBTStatus.Failure : eBTStatus.Success;
        }
    }


    public class InverterConditionNoTimeNode : BTNode
    {
        private Func<bool> FuncCondition;

        public InverterConditionNoTimeNode(string name, Func<bool> funcCondition) : base(name)
        {
            FuncCondition = funcCondition;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;

            return FuncCondition() ? eBTStatus.Failure : eBTStatus.Success;
        }
    }


    public class InverterConditionTNode<T> : BTNode
    {
        private T V;
        private Func<T, bool> FuncCondition;

        public InverterConditionTNode(string name, Func<T, bool> funcCondition, T v) : base(name)
        {
            V = v;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;
            return FuncCondition(V) ? eBTStatus.Failure : eBTStatus.Success;
        }
    }
    public class InverterConditionTNode<T1, T2> : BTNode
    {
        private T1 V1;
        private T2 V2;
        private Func<T1, T2, bool> FuncCondition;

        public InverterConditionTNode(string name, Func<T1, T2, bool> funcCondition, T1 v1, T2 v2) : base(name)
        {
            V1 = v1;
            V2 = v2;
            FuncCondition = funcCondition;
        }
        public override eBTStatus Tick(float deltaTime)
        {
            if (FuncCondition == null) return eBTStatus.Failure;
            return FuncCondition(V1, V2) ? eBTStatus.Failure : eBTStatus.Success;
        }
    }


}
