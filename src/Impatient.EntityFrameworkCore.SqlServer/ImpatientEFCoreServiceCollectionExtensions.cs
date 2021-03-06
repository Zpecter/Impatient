﻿using Impatient.EntityFrameworkCore.SqlServer.Infrastructure;
using Impatient.Query.ExpressionVisitors.Utility;
using Impatient.Query.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Impatient.EntityFrameworkCore.SqlServer
{
    public static class ImpatientEFCoreServiceCollectionExtensions
    {
        public static IServiceCollection AddImpatientEFCoreQueryCompiler(this IServiceCollection services)
        {
            foreach (var descriptor in services.Where(s => s.ServiceType == typeof(IQueryCompiler)).ToArray())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IImpatientQueryCache, DefaultImpatientQueryCache>();

            services.AddSingleton<ModelExpressionProvider>();

            services.AddSingleton<DescriptorSetCache>();

            services.AddSingleton<ModelQueryExpressionCache>();

            services.AddSingleton<TranslatabilityAnalyzingExpressionVisitor>();

            services.AddSingleton<IQueryTranslatingExpressionVisitorFactory, DefaultQueryTranslatingExpressionVisitorFactory>();

            services.AddSingleton<IReadValueExpressionFactoryProvider, DefaultReadValueExpressionFactoryProvider>();

            services.AddSingleton<ITypeMappingProvider, EFCoreTypeMappingProvider>();

            services.AddSingleton<IQueryFormattingProvider, SqlServerQueryFormattingProvider>();

            services.AddScoped<IOptimizingExpressionVisitorProvider, DefaultOptimizingExpressionVisitorProvider>();

            services.AddScoped<IComposingExpressionVisitorProvider, EFCoreComposingExpressionVisitorProvider>();

            services.AddScoped<IRewritingExpressionVisitorProvider, EFCoreRewritingExpressionVisitorProvider>();

            services.AddScoped<ICompilingExpressionVisitorProvider, EFCoreCompilingExpressionVisitorProvider>();

            services.AddScoped<IQueryableInliningExpressionVisitorFactory, EFCoreQueryableInliningExpressionVisitorFactory>();

            services.AddScoped<IDbCommandExecutorFactory, EFCoreDbCommandExecutorFactory>();

            services.AddScoped<IQueryProcessingContextFactory, EFCoreQueryProcessingContextFactory>();

            services.AddScoped<IImpatientQueryProcessor, DefaultImpatientQueryProcessor>();

            services.AddScoped<IQueryCompiler, ImpatientQueryCompiler>();

            services.AddScoped(provider =>
            {
                var cache = provider.GetRequiredService<DescriptorSetCache>();
                var context = provider.GetRequiredService<ICurrentDbContext>().Context;

                return cache.GetDescriptorSet(context);
            });

            return services;
        }
    }
}
