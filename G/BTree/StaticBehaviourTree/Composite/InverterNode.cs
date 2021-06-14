using System;
namespace G.StaticBehaviourTree
{
    public class InverterNode<TA> : CompositeNode<TA>
    {
 
        public InverterNode(string name) : base(name)
        {
        }

        public override eBTStatus Tick(TA owner, float deltaTime)
        {
            if (Children.Count < 1)
                return eBTStatus.Success;

            var result = Children[0].Tick(owner, deltaTime);

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
