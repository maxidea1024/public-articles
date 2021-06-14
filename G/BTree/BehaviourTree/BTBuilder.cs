using System;
using System.Collections.Generic;

namespace G.BehaviourTree
{
    public class BTBuilder
    {
        private CompositeNode CurNode;
        private Stack<CompositeNode> CompositeBTNodeStack = new Stack<CompositeNode>();


        private void BuildError(string err)
        {


        }
        //-------------------------------------------------------------------------------------------------------
        // Action
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder Action(string name, Func<float, eBTStatus> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionNode, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionNode(name, fn));
            return this;
        }
        public BTBuilder Action(string name, Func<eBTStatus> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionNNode, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionNNode(name, fn));
            return this;
        }
        public BTBuilder ActionT<T>(string name, Func<T,eBTStatus> fn, T v)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionTNode<T>, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionTNode<T>(name, fn, v));
            return this;
        }
        public BTBuilder ActionT<T1,T2>(string name, Func<T1, T2, eBTStatus> fn, T1 v1, T2 v2)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ActionTNode<T1, T2>, it must be a leaf node.");
                return this;
            }

            CompositeBTNodeStack.Peek().AddChild(new ActionTNode<T1, T2>(name, fn, v1, v2));
            return this;
        }
        //-------------------------------------------------------------------------------------------------------


        //-------------------------------------------------------------------------------------------------------
        // Condition
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder Condition(string name, Func<float, bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild( new ConditionNode(name, fn) );
            return this;
        }
        public BTBuilder Condition(string name, Func<bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionNNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new ConditionNNode(name, fn));
            return this;
        }
        public BTBuilder ConditionT<T>(string name, Func<T,bool> fn, T v)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionTNode<T>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new ConditionTNode<T>(name, fn, v));
            return this;
        }
        public BTBuilder ConditionT<T1, T2>(string name, Func<T1, T2, bool> fn, T1 v1, T2 v2)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested ConditionTNode<T1, T2>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new ConditionTNode<T1, T2>(name, fn, v1, v2));
            return this;
        }
        //-------------------------------------------------------------------------------------------------------



        //-------------------------------------------------------------------------------------------------------
        // Inverter Condition
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder InverterCondition(string name, Func<float, bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionNode(name, fn));
            return this;
        }
        public BTBuilder InverterCondition(string name, Func<bool> fn)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionNoTimeNode, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionNoTimeNode(name, fn));
            return this;
        }
        public BTBuilder InverterConditionT<T>(string name, Func<T, bool> fn, T v)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionTNode<T>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionTNode<T>(name, fn, v));
            return this;
        }
        public BTBuilder InverterConditionT<T1, T2>(string name, Func<T1, T2, bool> fn, T1 v1, T2 v2)
        {
            if (CompositeBTNodeStack.Count <= 0)
            {
                BuildError("Can't create an unnested InverterConditionTNode<T1, T2>, it must be a leaf node.");
                return this;
            }
            CompositeBTNodeStack.Peek().AddChild(new InverterConditionTNode<T1, T2>(name, fn, v1, v2));
            return this;
        }
        //-------------------------------------------------------------------------------------------------------


        //-------------------------------------------------------------------------------------------------------
        // Composite 
        //-------------------------------------------------------------------------------------------------------
        public BTBuilder Inverter(string name)
        {
            var inverterNode = new InverterNode(name);

            if (CompositeBTNodeStack.Count > 0)
            {
                CompositeBTNodeStack.Peek().AddChild(inverterNode);
            }

            CompositeBTNodeStack.Push(inverterNode);
            return this;
        }

        public BTBuilder Sequence(string name)
        {
            var sequenceNode = new SequenceNode(name);

            if (CompositeBTNodeStack.Count > 0)
            {
                CompositeBTNodeStack.Peek().AddChild(sequenceNode);
            }

            CompositeBTNodeStack.Push(sequenceNode);
            return this;
        }

        public BTBuilder Parallel(string name, int numRequiredToFail = 0, int numRequiredToSucceed = 0)
        {
            var parallelNode = new ParallelNode(name, numRequiredToFail, numRequiredToSucceed);

            if (CompositeBTNodeStack.Count > 0)
            {
                CompositeBTNodeStack.Peek().AddChild(parallelNode);
            }

            CompositeBTNodeStack.Push(parallelNode);
            return this;
        }

        public BTBuilder Selector(string name)
        {
            var selectorNode = new SelectorNode(name);

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
        public BTBuilder SubTree(BTNode subTree)
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

        public BTNode Build()
        {
            if (CurNode == null)
            {
                BuildError("Can't create a behaviour tree with zero nodes");
                return null;
            }
            return CurNode;
        }

        public BTBuilder End()
        {
            CurNode = CompositeBTNodeStack.Pop();
            return this;
        }
    }
}
