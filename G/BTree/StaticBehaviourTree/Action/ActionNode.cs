using System;



namespace G.StaticBehaviourTree
{
	public interface IBTActionSuccessAi<TA>
	{
		void AddSuccessActionNode(BTNode<TA> actionNode);
	}


    public class ActionNode<TA> : BTNode<TA>
    {
        private Func<TA,float, eBTStatus> FuncAction;

        public ActionNode(string name, Func<TA, float, eBTStatus> funcAction) : base(name)
        {
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
			if( FuncAction == null ) return eBTStatus.Success;

			eBTStatus re = FuncAction(owner, deltaTime);
			if( re == eBTStatus.Success )
				(owner as IBTActionSuccessAi<TA>)?.AddSuccessActionNode(this);

			return re;
        }
    }

    public class ActionNNode<TA> : BTNode<TA>
    {
        private Func<TA, eBTStatus> FuncAction;

        public ActionNNode(string name, Func<TA, eBTStatus> funcAction) : base(name)
        {
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
			if (FuncAction == null) return eBTStatus.Success;

			eBTStatus re = FuncAction(owner);
			if (re == eBTStatus.Success)
				(owner as IBTActionSuccessAi<TA>)?.AddSuccessActionNode(this);

			return re;
        }
    }


    public class ActionTNode<TA,T> : BTNode<TA>
    {
        private T V;
        private Func<TA, T, eBTStatus> FuncAction;

        public ActionTNode(string name, Func<TA, T, eBTStatus> funcAction, T v) : base(name)
        {
            V = v;
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
			if (FuncAction == null) return eBTStatus.Success;

			eBTStatus re = FuncAction(owner, V);
			if (re == eBTStatus.Success)
				(owner as IBTActionSuccessAi<TA>)?.AddSuccessActionNode(this);

			return re;
        }
    }
    public class ActionTNode<TA,T1,T2> : BTNode<TA>
    {
        private T1 V1;
        private T2 V2;
        private Func<TA, T1, T2, eBTStatus> FuncAction;

        public ActionTNode(string name, Func<TA, T1, T2, eBTStatus> funcAction, T1 v1, T2 v2) : base(name)
        {
            V1 = v1;
            V2 = v2;
            FuncAction = funcAction;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
			if (FuncAction == null) return eBTStatus.Success;

			eBTStatus re = FuncAction(owner, V1, V2);
			if (re == eBTStatus.Success)
				(owner as IBTActionSuccessAi<TA>)?.AddSuccessActionNode(this);

			return re;
        }
    }






    public class ActionExNode<TA> : BTNode<TA>
    {
        private bool IsRunning = false;

        private Func<TA, float, eBTStatus> FuncAction;
        private Action FuncEnter;
        private Action FuncExit;

        public ActionExNode(string name, Func<TA, float, eBTStatus> funcAction, Action funcEnter = null, Action funcExit = null) : base(name)
        {
            FuncAction = funcAction;
            FuncEnter = funcEnter;
            FuncExit = funcExit;
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            eBTStatus result = eBTStatus.Running;
            if (IsRunning == false)
            {
                FuncEnter?.Invoke();
                IsRunning = true;
            }
            if (IsRunning == true)
            {
				if (FuncAction == null) return eBTStatus.Success;

				result = FuncAction(owner, deltaTime);
				if (result == eBTStatus.Success)
					(owner as IBTActionSuccessAi<TA>)?.AddSuccessActionNode(this);

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
