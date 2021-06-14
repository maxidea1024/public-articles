using System;
      
namespace G.BehaviourTree
{
    public class BehaviourTreeBuilderTests
    {
        BTBuilder testObject;
        #pragma warning disable 0219

        void Init()
        {
            testObject = new BTBuilder();
        }

        
        public void cant_create_a_behaviour_tree_with_zero_nodes()
        {
            Init();

            testObject.Build();
        }

        
        public void cant_create_an_unested_action_node()
        {
            Init();

            testObject
                 .Action("some-node-1", t => eBTStatus.Running)
                 .Build();
        }

        
        public void can_create_inverter_node()
        {
            Init();

            var node = testObject
                .Inverter("some-inverter")
                    .Action("some-node", t =>eBTStatus.Success)
                .End()
                .Build();

            //Assert.IsType<InverterNode>(node);
            //Assert.Equal(BTStatus.Failure, node.Tick(new TimeData()));
        }

        
        public void cant_create_an_unbalanced_behaviour_tree()
        {
            Init();

            testObject
                .Inverter("some-inverter")
                .Action("some-node", t => eBTStatus.Success)
            .Build();
        }

        
        public void condition_is_syntactic_sugar_for_do()
        {
            Init();

            var node = testObject
                .Inverter("some-inverter")
                    .Condition("some-node", t => true)
                .End()
                .Build();

            //Assert.IsType<InverterNode>(node);
            //Assert.Equal(BTStatus.Failure, node.Tick(new TimeData()));
        }

        
        public void can_invert_an_inverter()
        {
            Init();

            var node = testObject
                .Inverter("some-inverter")
                    .Inverter("some-other-inverter")
                        .Action("some-node", t => eBTStatus.Success)
                    .End()
                .End()
                .Build();

            //Assert.IsType<InverterNode>(node);
            //Assert.Equal(BTStatus.Success, node.Tick(new TimeData()));
        }

        
        public void adding_more_than_a_single_child_to_inverter_throws_exception()
        {
            Init();

            testObject
                .Inverter("some-inverter")
                    .Action("some-node", t => eBTStatus.Success)
                    .Action("some-other-node", t => eBTStatus.Success)
                .End()
                .Build();
        }

        
        public void can_create_a_sequence()
        {
            Init();

            var invokeCount = 0;

            var sequence = testObject
                .Sequence("some-sequence")
                    .Action("some-action-1", t => 
                    {
                        ++invokeCount;
                        return eBTStatus.Success;
                    })
                    .Action("some-action-2", t =>
                    {
                        ++invokeCount;
                        return eBTStatus.Success;
                    })
                .End()
                .Build();

            //Assert.IsType<SequenceNode>(sequence);
            //Assert.Equal(BTStatus.Success, sequence.Tick(new TimeData()));
            //Assert.Equal(2, invokeCount);
        }

        
        public void can_create_parallel()
        {
            Init();

            var invokeCount = 0;

            var parallel = testObject
                .Parallel("some-parallel", 2, 2)
                    .Action("some-action-1", t =>
                    {
                        ++invokeCount;
                        return eBTStatus.Success;
                    })
                    .Action("some-action-2", t =>
                    {
                        ++invokeCount;
                        return eBTStatus.Success;
                    })
                .End()
                .Build();

            //Assert.IsType<ParallelNode>(parallel);
            //Assert.Equal(BTStatus.Success, parallel.Tick(new TimeData()));
            //Assert.Equal(2, invokeCount);
        }

        
        public void can_create_selector()
        {
            Init();

            var invokeCount = 0;

            var parallel = testObject
                .Selector("some-selector")
                    .Action("some-action-1", t =>
                    {
                        ++invokeCount;
                        return eBTStatus.Failure;
                    })
                    .Action("some-action-2", t =>
                    {
                        ++invokeCount;
                        return eBTStatus.Success;
                    })
                .End()
                .Build();

            //Assert.IsType<SelectorNode>(parallel);
            //Assert.Equal(BTStatus.Success, parallel.Tick(new TimeData()));
            //Assert.Equal(2, invokeCount);
        }

        
        public void can_splice_sub_tree()
        {
            Init();

            var invokeCount = 0;

            var spliced = testObject
                .Sequence("spliced")
                    .Action("test", t =>
                    {
                        ++invokeCount;
                        return eBTStatus.Success;
                    })
                .End()
                .Build();

            var tree = testObject
                .Sequence("parent-tree")
                    .SubTree(spliced)                    
                .End()
                .Build();

            //tree.Tick(new TimeData());
            //Assert.Equal(1, invokeCount);
        }

        
        public void splicing_an_unnested_sub_tree_throws_exception()
        {
            Init();

            var invokeCount = 0;

            var spliced = testObject
                .Sequence("spliced")
                    .Action("test", t =>
                    {
                        ++invokeCount;
                        return eBTStatus.Success;
                    })
                .End()
                .Build();

            //Assert.Throws<ApplicationException>(() =>
            //{
            //    testObject
            //        .Splice(spliced);
            //});
        }

        #pragma warning restore 0219

    }
}
