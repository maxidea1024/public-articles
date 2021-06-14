using System;
namespace G.StaticBehaviourTree
{
    public class SelectorNode<TA> : CompositeNode<TA>
    {
        public SelectorNode(string name) : base(name) { }

        //----------------------------------------------------------------------
        // 자식 노드가 BTStatus.Failure가 아닌것을 반환할 때까지 자식 노드들을 실행합니다.말 그대로 하나를 선택하는 것이죠.
        //----------------------------------------------------------------------
        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            foreach (var child in Children)
            {
                var childStatus = child.Tick(owner, deltaTime);
                if (childStatus != eBTStatus.Failure)
                {
                    return childStatus;
                }
            }

            return eBTStatus.Failure;
        }
        //----------------------------------------------------------------------

    }
}
