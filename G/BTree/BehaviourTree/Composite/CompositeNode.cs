using System;
using System.Collections.Generic;

namespace G.BehaviourTree
{
    public class CompositeNode : BTNode
    {
        protected List<BTNode> Children = new List<BTNode>();

        public CompositeNode(string name) : base(name){}

        public void AddChild(BTNode child)
        {
            Children.Add(child);
        }

        public override eBTStatus Tick(float deltaTime) { return eBTStatus.Success; }


    }
}
