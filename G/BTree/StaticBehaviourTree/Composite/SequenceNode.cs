﻿using System;
namespace G.StaticBehaviourTree
{
    public class SequenceNode<TA> : CompositeNode<TA>
    {
        public SequenceNode(string name) : base(name){}

        //----------------------------------------------------------------------
        //자식 노드가 Failure를 반환할 때까지 자식 노드들을 실행합니다. 말 그대로 순차적 실행입니다.
        //----------------------------------------------------------------------
        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            foreach (var child in Children)
            {
                var childStatus = child.Tick(owner, deltaTime);

                if (childStatus != eBTStatus.Success)
                {
                    return childStatus;
                }
            }
            return eBTStatus.Success;
        }
        //----------------------------------------------------------------------

    }
}
