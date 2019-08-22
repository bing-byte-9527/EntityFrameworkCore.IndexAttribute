﻿using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toolbelt.ComponentModel.DataAnnotations.Schema;
using Toolbelt.ComponentModel.DataAnnotations.Schema.Internals;
using Toolbelt.EntityFrameworkCore.Metadata.Builders;

namespace Toolbelt.ComponentModel.DataAnnotations
{
    public static class AttributedIndexBuilderExtension
    {
        internal class IndexBuilderArgument
        {
            public string[] PropertyNames { get; }

            public string IndexName { get; }

            public bool IsUnique { get; }

            public bool IsClustered { get; }

            public IndexBuilderArgument(IndexAttribute indexAttr, params string[] propertyNames)
            {
                this.PropertyNames = propertyNames;
                this.IndexName = indexAttr.Name;
                this.IsUnique = indexAttr.IsUnique;
                this.IsClustered = indexAttr.IsClustered;
            }

            public IndexBuilderArgument(PrimaryKeyAttribute primaryKeyAttr, params string[] propertyNames)
            {
                this.PropertyNames = propertyNames;
                this.IndexName = primaryKeyAttr.Name;
                this.IsClustered = primaryKeyAttr.IsClustered;
            }
        }

        public static void BuildIndexesFromAnnotations(this ModelBuilder modelBuilder)
        {
            modelBuilder.BuildIndexesFromAnnotations(
                postProcessForIndex: null,
                postProcessForPrimaryKey: null);
        }

        internal static void BuildIndexesFromAnnotations(
            this ModelBuilder modelBuilder,
            Action<IndexBuilder, IndexBuilderArgument> postProcessForIndex,
            Action<KeyBuilder, IndexBuilderArgument> postProcessForPrimaryKey
        )
        {
            AnnotationBasedModelBuilder.Build<IndexAttribute, IndexBuilderArgument>(
                modelBuilder,
                (props) => CreateBuilderArguments(props, (a, n) => new IndexBuilderArgument(a, n)),
                (b1, b2, arg) => BuildIndex(b1, b2, arg, postProcessForIndex));
            AnnotationBasedModelBuilder.Build<PrimaryKeyAttribute, IndexBuilderArgument>(
                modelBuilder,
                (props) => CreateBuilderArguments(props, (a, n) => new IndexBuilderArgument(a, n)),
                (b1, b2, arg) => BuildPrimaryKey(b1, b2, arg, postProcessForPrimaryKey));
        }

        private static IndexBuilderArgument[] CreateBuilderArguments<TAttr>(
            AnnotatedProperty<TAttr>[] annotatedProperties,
            Func<TAttr, string[], IndexBuilderArgument> createBuilderArgInstance
        )
            where TAttr : Attribute, INameAndOrder
        {
            var unnamedIndexArgs = annotatedProperties
                .Where(prop => prop.Attribute.Name == "")
                .Select(prop => createBuilderArgInstance(prop.Attribute, new[] { prop.Name }));

            var namedIndexArgs = annotatedProperties
                .Where(prop => prop.Attribute.Name != "")
                .GroupBy(prop => prop.Attribute.Name)
                .Select(g => createBuilderArgInstance(
                    g.First().Attribute,
                    g.OrderBy(item => item.Attribute.Order).Select(item => item.Name).ToArray())
                );

            var indexBuilderArgs = unnamedIndexArgs.Concat(namedIndexArgs).ToArray();
            return indexBuilderArgs;
        }

        private static void BuildIndex(EntityTypeBuilder builder1, ReferenceOwnershipBuilder builder2, IndexBuilderArgument builderArg, Action<IndexBuilder, IndexBuilderArgument> postProcess)
        {
            var indexBuilder = builder1?.HasIndex(builderArg.PropertyNames) ?? builder2.HasIndex(builderArg.PropertyNames);
            indexBuilder.IsUnique(builderArg.IsUnique);
            if (builderArg.IndexName != "")
            {
                indexBuilder.HasName(builderArg.IndexName);
            }
            postProcess?.Invoke(indexBuilder, builderArg);
        }

        private static void BuildPrimaryKey(EntityTypeBuilder builder1, ReferenceOwnershipBuilder builder2, IndexBuilderArgument builderArg, Action<KeyBuilder, IndexBuilderArgument> postProcess)
        {
            if (builder1 == null) throw new NotSupportedException("Annotate primary key to owned entity isn't supported. If you want to do it, you have to implement it by Fluent API in DbContext.OnModelCreating() with EF Core v.2.2 or after.");

            var keyBuilder = builder1.HasKey(builderArg.PropertyNames);
            if (builderArg.IndexName != "")
            {
                keyBuilder.HasName(builderArg.IndexName);
            }
            postProcess?.Invoke(keyBuilder, builderArg);
        }
    }
}