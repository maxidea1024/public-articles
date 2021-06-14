using System;
using System.Collections.Generic;

namespace G.StaticBehaviourTree
{
    public class BTBuilder<TA>
    {
        private CompositeNode<TA> CurNode;
        private Stack<CompositeNode<TA>> CompositeBTNodeStack = new Stack<CompositeNode<TA>>();


        private void BuildError(string err)
        {


        }
        //-------------------------------------------------------------------------------------------------------
        // Action
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder<TA> Action(string name, Func<TA, float, eBTStatus> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionNode, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionNode<TA>(name, fn));
            return this;
        }
        public BTBuilder<TA> Action(string name, Func<TA, eBTStatus> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionNNode, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionNNode<TA>(name, fn));
            return this;
        }
        public BTBuilder<TA> ActionT<T>(string name, Func<TA, T,eBTStatus> fn, T v)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionTNode<T>, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionTNode<TA, T>(name, fn, v));
            return this;
        }
        public BTBuilder<TA> ActionT<T1,T2>(string name, Func<TA, T1, T2, eBTStatus> fn, T1 v1, T2 v2)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionTNode<T1, T2>, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionTNode<TA, T1, T2>(name, fn, v1, v2));
            return this;
        }
        //-------------------------------------------------------------------------------------------------------


        //-------------------------------------------------------------------------------------------------------
        // Condition
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder<TA> Condition(string name, Func<TA, float, bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild( new ConditionNode<TA>(name, fn) );
            return this;
        }
        public BTBuilder<TA> Condition(string name, Func<TA, bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionNNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new ConditionNNode<TA>(name, fn));
            return this;
        }
        public BTBuilder<TA> ConditionT<T>(string name, Func<TA, T,bool> fn, T v)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionTNode<T>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new ConditionTNode<TA, T>(name, fn, v));
            return this;
        }
        public BTBuilder<TA> ConditionT<T1, T2>(string name, Func<TA, T1, T2, bool> fn, T1 v1, T2 v2)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionTNode<T1, T2>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new ConditionTNode<TA, T1, T2>(name, fn, v1, v2));
            return this;
        }
        //-------------------------------------------------------------------------------------------------------



        //-------------------------------------------------------------------------------------------------------
        // Inverter Condition
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder<TA> InverterCondition(string name, Func<TA, float, bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionNode<TA>(name, fn));
            return this;
        }
        public BTBuilder<TA> InverterCondition(string name, Func<TA, bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionNoTimeNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionNoTimeNode<TA>(name, fn));
            return this;
        }
        public BTBuilder<TA> InverterConditionT<T>(string name, Func<TA, T, bool> fn, T v)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionTNode<T>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionTNode<TA, T>(name, fn, v));
            return this;
        }
        public BTBuilder<TA> InverterConditionT<T1, T2>(string name, Func<TA, T1, T2, bool> fn, T1 v1, T2 v2)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionTNode<T1, T2>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionTNode<TA, T1, T2>(name, fn, v1, v2));
            return this;
        }
        //-------------------------------------------------------------------------------------------------------


        //-------------------------------------------------------------------------------------------------------
        // Composite 
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder<TA> Inverter(string name)
        {
            var inverterNode = new InverterNode<TA>(name);

            if (CompositeBTNodeStack.Count > 0)
            {
                CompositeBTNodeStack.Peek().AddChild(inverterNode);
            }

            CompositeBTNodeStack.Push(inverterNode);
            return this;
        }

        public BTBuilder<TA> Sequence(string name)
        {
            var sequenceNode = new SequenceNode<TA>(name);

            if (CompositeBTNodeStack.Count > 0)
            {
                CompositeBTNodeStack.Peek().AddChild(sequenceNode);
            }

            CompositeBTNodeStack.Push(sequenceNode);
            return this;
        }

        public BTBuilder<TA> Parallel(string name, int numRequiredToFail = 0, int numRequiredToSucceed = 0)
        {
            var parallelNode = new ParallelNode<TA>(name, numRequiredToFail, numRequiredToSucceed);

            if (CompositeBTNodeStack.Count > 0)
            {
                CompositeBTNodeStack.Peek().AddChild(parallelNode);
            }

            CompositeBTNodeStack.Push(parallelNode);
            return this;
        }

        public BTBuilder<TA> Selector(string name)
        {
            var selectorNode = new SelectorNode<TA>(name);

            if (CompositeBTNodeStack.Count > 0)
            {
                CompositeBTNodeStack.Peek().AddChild(selectorNode);
            }

            CompositeBTNodeStack.Push(selectorNode);
            return this;
        }
        //-------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Splice a sub tree into the parent tree.
        /// </summary>
        public BTBuilder<TA> SubTree(BTNode<TA> subTree)
        {
            if (subTree == null)
            {
                //throw new ArgumentNullException("subTree");
                return this;
            }

            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't splice an unnested sub-tree, there must be a parent-tree.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(subTree);
            return this;
        }

        public BTNode<TA> Build()
        {
            if (CurNode == null)
            {
                BuildError("Can't create a behaviour tree with zero nodes");
                return null;
            }
            return CurNode;
        }

        public BTBuilder<TA> End()
        {
            CurNode = CompositeBTNodeStack.Pop();
            return this;
        }
    }
}
