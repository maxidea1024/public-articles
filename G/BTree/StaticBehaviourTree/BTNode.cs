// Fluent-Behaviour-Tree 참고해서 작업  https://github.com/codecapers/Fluent-Behaviour-Tree

using System;

namespace G.StaticBehaviourTree
{
    public class BTNode<TA>
    {
		public string Name { get; private set; }

        public BTNode(string name) { Name = name; }

        public virtual eBTStatus Tick(TA owner, float deltaTime) { return eBTStatus.Success; }
    }
}
