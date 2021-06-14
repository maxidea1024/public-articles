using System;
namespace G.BehaviourTree
{
    public class SelectorNode : CompositeNode
    {
        public SelectorNode(string name) : base(name) { }

        //----------------------------------------------------------------------
        // 자식 노드가 BTStatus.Failure가 아닌것을 반환할 때까지 자식 노드들을 실행합니다.말 그대로 하나를 선택하는 것이죠.
        //----------------------------------------------------------------------
        public override eBTStatus Tick(float deltaTime)
        {
            foreach (var child in Children)
            {
                var childStatus = child.Tick(deltaTime);
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
