﻿using System;
using System.Data;
using Bing.CodeGenerator.Entity;
using Bing.CodeGenerator.Extensions;
using Bing.CodeGenerator.Helpers;
using SmartCode;
using SmartCode.Generator.Entity;

namespace Bing.CodeGenerator.Core
{
    /// <summary>
    /// 实体上下文构建器
    /// </summary>
    public class EntityContextBuilder
    {
        /// <summary>
        /// 唯一名称器
        /// </summary>
        private readonly UniqueNamer _namer;

        public EntityContextBuilder()
        {
            _namer = new UniqueNamer();
        }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="context">构建上下文</param>
        public EntityContext Build(BuildContext context)
        {
            var entityContext = new EntityContext();
            entityContext.DatabaseName = context.Project.Parameters["UnitOfWork"].ToString();
            var dataContextName = $"{Str.ToPascalCase(entityContext.DatabaseName)}Context";
            entityContext.ClassName = _namer.UniqueClassName(dataContextName);
            foreach (var schema in context.GetCurrentAllSchema())
            {
                foreach (var table in schema.Tables)
                {
                    GetEntity(entityContext, schema, table);
                }
            }
            return entityContext;
        }

        /// <summary>
        /// 获取实体
        /// </summary>
        /// <param name="entityContext">实体上下文</param>
        /// <param name="schema">架构</param>
        /// <param name="table">表</param>
        /// <param name="processRelationships">是否处理关系</param>
        /// <param name="processMethods">是否处理方法</param>
        private Entity GetEntity(EntityContext entityContext, Schema schema, Table table, bool processRelationships = true, bool processMethods = true)
        {
            var key = $"{schema.Name}.{table.Name}";
            var entity = entityContext.Entities.ByTable(key) ?? CreateEntity(entityContext, schema, table);
            if (!entity.Properties.IsProcessed)
                CreateProperties(entity,table);
            entity.IsProcessed = true;
            return entity;
        }

        /// <summary>
        /// 创建实体
        /// </summary>
        /// <param name="entityContext">实体上下文</param>
        /// <param name="schema">架构</param>
        /// <param name="table">表</param>
        private Entity CreateEntity(EntityContext entityContext, Schema schema, Table table)
        {
            var entity=new Entity()
            {
                FullName = $"{schema.Name}.{table.Name}",
                TableName = table.Name,
                TableSchema = schema.Name,
                Description = table.Description,
                Context = entityContext,
            };

            var className = _namer.UniqueClassName(table.Name);
            var mappingName = _namer.UniqueClassName($"{className}Map");
            var contextName = _namer.UniqueContextName(className);

            entity.ClassName = className;
            entity.ContextName = contextName;
            entity.MappingName = mappingName;

            entityContext.Entities.Add(entity);
            return entity;
        }

        private void CreateProperties(Entity entity, Table table)
        {
            foreach (var column in table.Columns)
            {
                if (column.DbType.Equals("hierarchyid", StringComparison.OrdinalIgnoreCase) ||
                    column.DbType.Equals("sql_variant", StringComparison.OrdinalIgnoreCase))
                    continue;
                var property = entity.Properties.ByColumn(column.Name);
                if (property == null)
                {
                    property = new Property()
                    {
                        ColumnName = column.Name
                    };
                    entity.Properties.Add(property);
                }

                var propertyName = _namer.UniqueName(entity.ClassName, column.Name);

                property.PropertyName = propertyName;
                property.Description = column.Description;

                property.NativeType = column.DbType;
                property.SystemType = Type.GetType(column.LanguageType);

                property.IsPrimaryKey = column.IsPrimaryKey;
                property.IsNullable = column.IsNullable;
                property.IsForeignKey = false;

                property.IsIdentity = column.AutoIncrement;
                property.IsRowVersion = IsRowVersion(column);
                property.IsAutoGenerated = false;
                property.IsProcessed = true;
            }

            entity.Properties.IsProcessed = true;
        }

        /// <summary>
        /// 是否行版本
        /// </summary>
        /// <param name="column">列</param>
        private static bool IsRowVersion(Column column)
        {
            bool isTimeStamp = column.DbType.Equals("timestamp", StringComparison.OrdinalIgnoreCase);
            bool isRowVersion = column.DbType.Equals("rowversion", StringComparison.OrdinalIgnoreCase);
            return (isTimeStamp || isRowVersion);
        }
    }
}
