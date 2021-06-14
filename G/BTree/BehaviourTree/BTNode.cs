// Fluent-Behaviour-Tree 참고해서 작업  https://github.com/codecapers/Fluent-Behaviour-Tree

using System;

namespace G.BehaviourTree
{
    public class BTNode
    {
        protected string Name;

        public BTNode(string name) { Name = name; }

        public virtual eBTStatus Tick(float deltaTime) { return eBTStatus.Success; }
    }
}
