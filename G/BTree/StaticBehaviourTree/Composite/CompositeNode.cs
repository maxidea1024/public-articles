using System;
using System.Collections.Generic;

namespace G.StaticBehaviourTree
{
    public class CompositeNode<TA> : BTNode<TA>
    {
        protected List<BTNode<TA>> Children = new List<BTNode<TA>>();

        public CompositeNode(string name) : base(name){}

        public void AddChild(BTNode<TA> child)
        {
            Children.Add(child);
        }

        public override eBTStatus Tick(TA owner, float deltaTime) { return eBTStatus.Success; }


    }
}
