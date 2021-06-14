using System;
namespace G.StaticBehaviourTree
{
    public class ParallelNode<TA> : CompositeNode<TA>
    {
        private int NumRequiredToSucceed;
        private int NumRequiredToFail;

        public ParallelNode(string name, int numRequiredToSucceed, int numRequiredToFail) : base(name)
        {
            NumRequiredToSucceed = numRequiredToSucceed;
            NumRequiredToFail = numRequiredToFail;
        }

        //----------------------------------------------------------------------
        // 자식 노드가 BTStatus.Failure가 아닌것을 반환할 때까지 자식 노드들을 실행합니다.말 그대로 하나를 선택하는 것이죠.
        //----------------------------------------------------------------------
        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            var numChildrenSuceeded = 0;
            var numChildrenFailed = 0;

            foreach (var child in Children)
            {
                var childStatus = child.Tick(owner, deltaTime);
                switch (childStatus)
                {
                    case eBTStatus.Success: ++numChildrenSuceeded; break;
                    case eBTStatus.Failure: ++numChildrenFailed; break;
                }
            }

            if (NumRequiredToSucceed > 0 && numChildrenSuceeded >= NumRequiredToSucceed)
            {
                return eBTStatus.Success;
            }

            if (NumRequiredToFail > 0 && numChildrenFailed >= NumRequiredToFail)
            {
                return eBTStatus.Failure;
            }

            return eBTStatus.Running;
        }
        //----------------------------------------------------------------------

    }
}
