﻿using Impatient.Query.Expressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Impatient.Query.ExpressionVisitors
{
    public class GroupingKeyRewritingExpressionVisitor : ExpressionVisitor
    {
        public override Expression Visit(Expression node)
        {
            switch (node)
            {
                case GroupByResultExpression groupByResultExpression:
                {
                    return CreateKeyPlaceholderGrouping(node, groupByResultExpression.ExteriorKeySelector);
                }

                case GroupedRelationalQueryExpression groupedRelationalQueryExpression:
                {
                    return CreateKeyPlaceholderGrouping(node, groupedRelationalQueryExpression.InnerKeySelector);
                }

                default:
                {
                    return base.Visit(node);
                }
            }
        }

        private static MemberInitExpression CreateKeyPlaceholderGrouping(Expression expression, Expression keySelector)
        {
            var groupingType
                = typeof(KeyPlaceholderGrouping<,>)
                    .MakeGenericType(expression.Type.GenericTypeArguments);

            return Expression.MemberInit(
                Expression.New(groupingType),
                Expression.Bind(
                    groupingType.GetRuntimeProperty("Key"),
                    keySelector));
        }

        private class KeyPlaceholderGrouping<TKey, TElement> : IGrouping<TKey, TElement>
        {
            public TKey Key { get; set; }

            public IEnumerator<TElement> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }
    }
}
