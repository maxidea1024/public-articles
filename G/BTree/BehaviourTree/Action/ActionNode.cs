using System;



namespace G.BehaviourTree
{


    public class ActionNode : BTNode
    {
        private Func<float, eBTStatus> FuncAction;

        public ActionNode(string name, Func<float, eBTStatus> funcAction) : base(name)
        {
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            return FuncAction?.Invoke(deltaTime) ?? eBTStatus.Success;
        }
    }

    public class ActionNNode : BTNode
    {
        private Func<eBTStatus> FuncAction;

        public ActionNNode(string name, Func<eBTStatus> funcAction) : base(name)
        {
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            return FuncAction?.Invoke() ?? eBTStatus.Success;
        }
    }


    public class ActionTNode<T> : BTNode
    {
        private T V;
        private Func<T, eBTStatus> FuncAction;

        public ActionTNode(string name, Func<T, eBTStatus> funcAction, T v) : base(name)
        {
            V = v;
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            return FuncAction?.Invoke(V) ?? eBTStatus.Success;
        }
    }
    public class ActionTNode<T1,T2> : BTNode
    {
        private T1 V1;
        private T2 V2;
        private Func<T1, T2, eBTStatus> FuncAction;

        public ActionTNode(string name, Func<T1, T2, eBTStatus> funcAction, T1 v1, T2 v2) : base(name)
        {
            V1 = v1;
            V2 = v2;
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            return FuncAction?.Invoke(V1, V2) ?? eBTStatus.Success;
        }
    }






    public class ActionExNode : BTNode
    {
        private bool IsRunning = false;

        private Func<float, eBTStatus> FuncAction;
        private Action FuncEnter;
        private Action FuncExit;

        public ActionExNode(string name, Func<float, eBTStatus> funcAction, Action funcEnter = null, Action funcExit = null) : base(name)
        {
            FuncAction = funcAction;
            FuncEnter = funcEnter;
            FuncExit = funcExit;
        }

        public override eBTStatus Tick(float deltaTime)
        {
            eBTStatus result = eBTStatus.Running;
            if (IsRunning == false)
            {
                FuncEnter?.Invoke();
                IsRunning = true;
            }
            if (IsRunning == true)
            {
                result = FuncAction?.Invoke(deltaTime) ?? eBTStatus.Success;

                if (result != eBTStatus.Running)
                {
                    FuncExit?.Invoke();
                    IsRunning = false;
                }
            }

            return result;
        }

    }

}
