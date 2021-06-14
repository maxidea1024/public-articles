using System;
namespace G.BehaviourTree
{
    public class InverterNode : CompositeNode
    {
 
        public InverterNode(string name) : base(name)
        {
        }

        public override eBTStatus Tick(float deltaTime)
        {
            if (Children.Count < 1)
                return eBTStatus.Success;

            var result = Children[0].Tick(deltaTime);

            if (result == eBTStatus.Failure)
            {
                return eBTStatus.Success;
            }
            else if (result == eBTStatus.Success)
            {
                return eBTStatus.Failure;
            }
            else
            {
                return result;
            }
        }



    }
}
