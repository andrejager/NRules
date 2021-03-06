﻿using System.Collections.Generic;
using NRules.Aggregators;
using NRules.RuleModel;

namespace NRules.Rete
{
    internal class AggregateNode : BinaryBetaNode
    {
        private readonly IAggregatorFactory _aggregatorFactory;
        private readonly bool _isSubnetJoin;

        public string Name { get; }
        public ExpressionMap ExpressionMap { get; }

        public AggregateNode(ITupleSource leftSource, IObjectSource rightSource, string name, ExpressionMap expressionMap, IAggregatorFactory aggregatorFactory, bool isSubnetJoin)
            : base(leftSource, rightSource)
        {
            Name = name;
            ExpressionMap = expressionMap;
            _aggregatorFactory = aggregatorFactory;
            _isSubnetJoin = isSubnetJoin;
        }

        public override void PropagateAssert(IExecutionContext context, IList<Tuple> tuples)
        {
            var joinedSets = JoinedSets(context, tuples);
            var aggregation = new Aggregation();
            foreach (var set in joinedSets)
            {
                var matchingFacts = new List<Fact>();
                foreach (var fact in set.Facts)
                {
                    if (MatchesConditions(context, set.Tuple, fact))
                        matchingFacts.Add(fact);
                }
                IAggregator aggregator = CreateAggregator(set.Tuple);
                var results = aggregator.Add(set.Tuple, matchingFacts);
                aggregation.Add(set.Tuple, results);
            }
            PropagateAggregation(context, aggregation);
        }

        public override void PropagateUpdate(IExecutionContext context, IList<Tuple> tuples)
        {
            var toUpdate = new TupleFactList();
            var joinedSets = JoinedSets(context, tuples);
            foreach (var set in joinedSets)
            {
                if (_isSubnetJoin && HasRightFacts(context, set))
                {
                    //Update already propagated from the right
                    continue;
                }

                IAggregator aggregator = GetAggregator(set.Tuple);
                foreach (var aggregate in aggregator.Aggregates)
                {
                    Fact aggregateFact = ToAggregateFact(context, aggregate);
                    toUpdate.Add(set.Tuple, aggregateFact);
                }
            }
            MemoryNode.PropagateUpdate(context, toUpdate);
        }

        public override void PropagateRetract(IExecutionContext context, IList<Tuple> tuples)
        {
            var toRetract = new TupleFactList();
            foreach (var tuple in tuples)
            {
                IAggregator aggregator = GetAggregator(tuple);
                foreach (var aggregate in aggregator.Aggregates)
                {
                    Fact aggregateFact = ToAggregateFact(context, aggregate);
                    toRetract.Add(tuple, aggregateFact);
                }
            }
            MemoryNode.PropagateRetract(context, toRetract);
        }

        public override void PropagateAssert(IExecutionContext context, IList<Fact> facts)
        {
            var joinedSets = JoinedSets(context, facts);
            var aggregation = new Aggregation();
            foreach (var set in joinedSets)
            {
                if (set.Facts.Count == 0) continue;
                var matchingFacts = new List<Fact>();
                foreach (var fact in set.Facts)
                {
                    if (MatchesConditions(context, set.Tuple, fact))
                        matchingFacts.Add(fact);
                }
                if (matchingFacts.Count > 0)
                {
                    IAggregator aggregator = GetAggregator(set.Tuple);
                    var results = aggregator.Add(set.Tuple, matchingFacts);
                    aggregation.Add(set.Tuple, results);
                }
            }
            PropagateAggregation(context, aggregation);
        }

        public override void PropagateUpdate(IExecutionContext context, IList<Fact> facts)
        {
            var joinedSets = JoinedSets(context, facts);
            var aggregation = new Aggregation();
            foreach (var set in joinedSets)
            {
                if (set.Facts.Count == 0) continue;
                var matchingFacts = new List<Fact>();
                foreach (var fact in set.Facts)
                {
                    if (MatchesConditions(context, set.Tuple, fact))
                        matchingFacts.Add(fact);
                }
                if (matchingFacts.Count > 0)
                {
                    IAggregator aggregator = GetAggregator(set.Tuple);
                    var results = aggregator.Modify(set.Tuple, matchingFacts);
                    aggregation.Add(set.Tuple, results);
                }
            }
            PropagateAggregation(context, aggregation);
        }

        public override void PropagateRetract(IExecutionContext context, IList<Fact> facts)
        {
            var joinedSets = JoinedSets(context, facts);
            var aggregation = new Aggregation();
            foreach (var set in joinedSets)
            {
                if (set.Facts.Count == 0) continue;
                var matchingFacts = new List<Fact>();
                foreach (var fact in set.Facts)
                {
                    if (MatchesConditions(context, set.Tuple, fact))
                        matchingFacts.Add(fact);
                }
                if (matchingFacts.Count > 0)
                {
                    IAggregator aggregator = GetAggregator(set.Tuple);
                    var results = aggregator.Remove(set.Tuple, matchingFacts);
                    aggregation.Add(set.Tuple, results);
                }
            }
            PropagateAggregation(context, aggregation);
        }

        public override void Accept<TContext>(TContext context, ReteNodeVisitor<TContext> visitor)
        {
            visitor.VisitAggregateNode(context, this);
        }

        private void PropagateAggregation(IExecutionContext context, Aggregation aggregation)
        {
            PropagateAggregateRetracts(context, aggregation);
            PropagateAggregateAsserts(context, aggregation);
            PropagateAggregateUpdates(context, aggregation);
        }

        private void PropagateAggregateAsserts(IExecutionContext context, Aggregation aggregation)
        {
            var asserts = new TupleFactList();
            foreach (var assert in aggregation.Asserts)
            {
                var fact = ToAggregateFact(context, assert.ResultObject);
                asserts.Add(assert.Tuple, fact);
            }
            if (asserts.Count > 0)
            {
                MemoryNode.PropagateAssert(context, asserts);
            }
        }

        private void PropagateAggregateUpdates(IExecutionContext context, Aggregation aggregation)
        {
            var updates = new TupleFactList();
            foreach (var update in aggregation.Updates)
            {
                var fact = ToAggregateFact(context, update.ResultObject);
                updates.Add(update.Tuple, fact);
            }
            if (updates.Count > 0)
            {
                MemoryNode.PropagateUpdate(context, updates);
            }
        }

        private void PropagateAggregateRetracts(IExecutionContext context, Aggregation aggregation)
        {
            var retracts = new TupleFactList();
            foreach (var retract in aggregation.Retracts)
            {
                var fact = ToAggregateFact(context, retract.ResultObject);
                retracts.Add(retract.Tuple, fact);
            }
            if (retracts.Count > 0)
            {
                MemoryNode.PropagateRetract(context, retracts);
                var enumerator = retracts.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    context.WorkingMemory.RemoveInternalFact(this, enumerator.CurrentFact);
                }
            }
        }

        private IAggregator GetAggregator(Tuple tuple)
        {
            var aggregator = tuple.GetState<IAggregator>(this);
            return aggregator;
        }

        private IAggregator CreateAggregator(Tuple tuple)
        {
            var aggregator = _aggregatorFactory.Create();
            tuple.SetState(this, aggregator);
            return aggregator;
        }

        private Fact ToAggregateFact(IExecutionContext context, object aggregate)
        {
            Fact fact = context.WorkingMemory.GetInternalFact(this, aggregate);
            if (fact == null)
            {
                fact = new Fact(aggregate);
                context.WorkingMemory.AddInternalFact(this, fact);
            }
            else if (!ReferenceEquals(fact.RawObject, aggregate))
            {
                fact.RawObject = aggregate;
                context.WorkingMemory.UpdateInternalFact(this, fact);
            }
            return fact;
        }

        private bool HasRightFacts(IExecutionContext context, TupleFactSet set)
        {
            foreach (var fact in set.Facts)
            {
                if (MatchesConditions(context, set.Tuple, fact))
                {
                    return true;
                }
            }
            return false;
        }
    }
}