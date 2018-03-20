﻿using Impatient.Metadata;
using Impatient.Query.ExpressionVisitors.Generating;
using Impatient.Query.ExpressionVisitors.Utility;
using Impatient.Query.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Impatient.Query
{
    public class DefaultImpatientQueryExecutor : IImpatientQueryExecutor
    {
        public DefaultImpatientQueryExecutor(
            DescriptorSet descriptorSet,
            IImpatientQueryCache queryCache,
            IDbCommandExecutor dbCommandExecutor,
            TranslatabilityAnalyzingExpressionVisitor translatabilityAnalyzingExpressionVisitor,
            IOptimizingExpressionVisitorProvider optimizingExpressionVisitorProvider,
            IComposingExpressionVisitorProvider composingExpressionVisitorProvider,
            ICompilingExpressionVisitorProvider compilingExpressionVisitorProvider,
            IQueryableInliningExpressionVisitorFactory queryInliningExpressionVisitorFactory,
            IQueryTranslatingExpressionVisitorFactory queryTranslatingExpressionVisitorFactory)
        {
            DescriptorSet = descriptorSet;
            QueryCache = queryCache;
            DbCommandExecutor = dbCommandExecutor;
            TranslatabilityAnalyzingExpressionVisitor = translatabilityAnalyzingExpressionVisitor;
            OptimizingExpressionVisitorProvider = optimizingExpressionVisitorProvider;
            ComposingExpressionVisitorProvider = composingExpressionVisitorProvider;
            CompilingExpressionVisitorProvider = compilingExpressionVisitorProvider;
            QueryInliningExpressionVisitorFactory = queryInliningExpressionVisitorFactory;
            QueryTranslatingExpressionVisitorFactory = queryTranslatingExpressionVisitorFactory;
        }

        public DescriptorSet DescriptorSet { get; }

        public IImpatientQueryCache QueryCache { get; }

        public IDbCommandExecutor DbCommandExecutor { get; }

        public TranslatabilityAnalyzingExpressionVisitor TranslatabilityAnalyzingExpressionVisitor { get; }

        public IOptimizingExpressionVisitorProvider OptimizingExpressionVisitorProvider { get; }

        public IComposingExpressionVisitorProvider ComposingExpressionVisitorProvider { get; }

        public ICompilingExpressionVisitorProvider CompilingExpressionVisitorProvider { get; }

        public IQueryableInliningExpressionVisitorFactory QueryInliningExpressionVisitorFactory { get; }

        public IQueryTranslatingExpressionVisitorFactory QueryTranslatingExpressionVisitorFactory { get; }

        public object Execute(IQueryProvider provider, Expression expression)
        {
            try
            {
                // Parameterize the expression by substituting any ConstantExpression
                // that is not a literal constant (such as a closure instance) with a ParameterExpression.

                var constantParameterizingVisitor = new ConstantParameterizingExpressionVisitor();
                expression = constantParameterizingVisitor.Visit(expression);

                // Generate a hash code for the parameterized expression.
                // Because the expression is parameterized, the hash code will be identical
                // for expressions that are structurally equivalent apart from any closure instances.

                var hashingVisitor = new HashingExpressionVisitor();
                expression = hashingVisitor.Visit(expression);

                var parameterMapping = constantParameterizingVisitor.Mapping;

                if (!QueryCache.TryGetValue(hashingVisitor.HashCode, out var compiled))
                {
                    var executionContextParameter = Expression.Parameter(typeof(IDbCommandExecutor), "executor");

                    var processingContext
                        = new QueryProcessingContext(
                            provider,
                            DescriptorSet,
                            parameterMapping,
                            executionContextParameter);

                    // Partially evaluate the expression. In addition to reducing evaluable nodes such 
                    // as `new DateTime(2000, 01, 01)` down to ConstantExpressions, this visitor also expands 
                    // IQueryable-producing expressions such as those found within calls to SelectMany
                    // so that the resulting IQueryable's expression tree will be integrated into the 
                    // current expression tree.

                    expression 
                        = QueryInliningExpressionVisitorFactory
                            .Create(processingContext)
                            .Visit(expression);

                    // Apply all optimizing visitors before each composing visitor and then apply all
                    // optimizing visitors one last time.

                    var composingExpressionVisitors 
                        = ComposingExpressionVisitorProvider
                            .CreateExpressionVisitors(processingContext)
                            .ToArray();

                    var optimizingExpressionVisitors 
                        = OptimizingExpressionVisitorProvider
                            .CreateExpressionVisitors(processingContext)
                            .ToArray();

                    expression
                        = composingExpressionVisitors
                            .SelectMany(c => optimizingExpressionVisitors.Append(c))
                            .Concat(optimizingExpressionVisitors)
                            .Aggregate(expression, (e, v) => v.Visit(e));

                    // Transform the expression by rewriting all composed query expressions into 
                    // executable expressions that make database calls and perform result materialization.

                    expression
                        = expression
                            .VisitWith(CompilingExpressionVisitorProvider
                                .CreateExpressionVisitors(processingContext));

                    // Compile the resulting expression into an executable delegate.

                    var parameters = new ParameterExpression[parameterMapping.Count + 1];

                    parameters[0] = executionContextParameter;

                    parameterMapping.Values.CopyTo(parameters, 1);

                    compiled = Expression.Lambda(expression, parameters).Compile();

                    // Cache the compiled delegate.

                    QueryCache.Add(hashingVisitor.HashCode, compiled);
                }

                // Invoke the compiled delegate.

                var arguments = new object[parameterMapping.Count + 1];

                arguments[0] = DbCommandExecutor;

                parameterMapping.Keys.CopyTo(arguments, 1);

                return compiled.DynamicInvoke(arguments);
            }
            catch (TargetInvocationException targetInvocationException)
            {
                throw targetInvocationException.InnerException;
            }
        }
    }
}
