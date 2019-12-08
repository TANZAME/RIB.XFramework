﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace TZM.XFramework.Data
{
    /// <summary>
    /// 关联表达式解析器
    /// </summary>
    public class JoinExpressionVisitor : ExpressionVisitorBase
    {
        private List<DbExpression> _joins = null;
        private TableAliasCache _aliases = null;
        private IDbQueryProvider _provider = null;

        /// <summary>
        /// 初始化 <see cref="JoinExpressionVisitor"/> 类的新实例
        /// </summary>
        /// <param name="provider">查询语义提供者</param>
        /// <param name="aliases">表别名集合</param>
        /// <param name="joins">JOIN 子句</param>
        public JoinExpressionVisitor(IDbQueryProvider provider, TableAliasCache aliases, List<DbExpression> joins)
            : base(provider, aliases, null)
        {
            _joins = joins;
            _aliases = aliases;
            _provider = provider;
        }

        /// <summary>
        /// 写入SQL片断
        /// </summary>
        /// <param name="builder">SQL 语句生成器</param>
        public override void Write(ISqlBuilder builder)
        {
            base._builder = builder;
            if (base._methodVisitor == null) base._methodVisitor = _provider.CreateMethodVisitor(this);

            foreach (DbExpression qj in _joins)
            {
                builder.AppendNewLine();

                // [INNER/LEFT JOIN]
                if (qj.DbExpressionType == DbExpressionType.GroupJoin || qj.DbExpressionType == DbExpressionType.Join || qj.DbExpressionType == DbExpressionType.GroupRightJoin)
                {
                    JoinType joinType = JoinType.InnerJoin;
                    if (qj.DbExpressionType == DbExpressionType.GroupJoin) joinType = JoinType.LeftJoin;
                    else if (qj.DbExpressionType == DbExpressionType.GroupRightJoin) joinType = JoinType.RightJoin;
                    this.AppendJoinType(builder, joinType);
                    this.AppendLfInJoin(builder, qj, _aliases);
                }
                else if (qj.DbExpressionType == DbExpressionType.SelectMany)
                {
                    this.AppendJoinType(builder, JoinType.CrossJoin);
                    this.AppendCrossJoin(builder, qj, _aliases);
                }
            }
        }

        // LEFT OR INNER JOIN
        private void AppendLfInJoin(ISqlBuilder builder, DbExpression dbExpression, TableAliasCache aliases)
        {
            builder.Append(' ');
            IDbQueryable dbQuery = (IDbQueryable)((dbExpression.Expressions[0] as ConstantExpression).Value);
            if (dbQuery.DbExpressions.Count == 1 && dbQuery.DbExpressions[0].DbExpressionType == DbExpressionType.GetTable)
            {
                Type type = dbExpression.Expressions[0].Type.GetGenericArguments()[0];
                var typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(type);
                builder.AppendMember(typeRuntime.TableName, !typeRuntime.IsTemporary);
            }
            else
            {
                // 嵌套
                var cmd = dbQuery.Resolve(builder.Indent + 1, false, builder.Token);
                builder.Append("(");
                builder.Append(cmd.CommandText);
                builder.AppendNewLine();
                builder.Append(')');
            }


            var left = dbExpression.Expressions[1] as LambdaExpression;
            var right = dbExpression.Expressions[2] as LambdaExpression;

            // t0(t1)
            string alias = !(left.Body.NodeType == ExpressionType.New || left.Body.NodeType == ExpressionType.MemberInit)
                ? aliases.GetTableAlias(dbExpression.Expressions[2])
                : aliases.GetTableAlias(right.Parameters[0]);
            builder.Append(' ');
            builder.Append(alias);
            builder.Append(' ');

            // ON a.Name = b.Name AND a.Id = b.Id
            builder.Append("ON ");

            if (left.Body.NodeType == ExpressionType.New)
            {

                var body1 = left.Body as NewExpression;
                var body2 = right.Body as NewExpression;

                for (int index = 0; index < body1.Arguments.Count; ++index)
                {
                    builder.AppendMember(aliases, body1.Arguments[index]);
                    builder.Append(" = ");
                    builder.AppendMember(aliases, body2.Arguments[index]);
                    if (index < body1.Arguments.Count - 1) builder.Append(" AND ");
                }
            }
            else if (left.Body.NodeType == ExpressionType.MemberInit)
            {
                var body1 = left.Body as MemberInitExpression;
                var body2 = right.Body as MemberInitExpression;

                for (int index = 0; index < body1.Bindings.Count; ++index)
                {
                    builder.AppendMember(aliases, (body1.Bindings[index] as MemberAssignment).Expression);
                    builder.Append(" = ");
                    builder.AppendMember(aliases, (body2.Bindings[index] as MemberAssignment).Expression);
                    if (index < body1.Bindings.Count - 1) builder.Append(" AND ");
                }
            }
            else
            {
                builder.AppendMember(aliases, left.Body.ReduceUnary());
                builder.Append(" = ");
                builder.AppendMember(aliases, right.Body.ReduceUnary());
            }
        }

        // Cross Join
        private void AppendCrossJoin(ISqlBuilder builder, DbExpression exp, TableAliasCache aliases)
        {
            var lambdaExp = exp.Expressions[1] as LambdaExpression;
            Type type = lambdaExp.Parameters[1].Type;
            var typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(type);
            builder.Append(' ');
            builder.AppendMember(typeRuntime.TableName, !typeRuntime.IsTemporary);

            string alias = aliases.GetTableAlias(lambdaExp.Parameters[1]);
            builder.Append(' ');
            builder.Append(alias);
            builder.Append(' ');
        }

        /// <summary>
        /// 写入关联类型
        /// </summary>
        /// <param name="builder">SQL 语句生成器</param>
        /// <param name="joinType">关联类型</param>
        protected void AppendJoinType(ISqlBuilder builder, JoinType joinType)
        {
            switch (joinType)
            {
                case JoinType.InnerJoin:
                    builder.Append("INNER JOIN");
                    break;
                case JoinType.LeftJoin:
                    builder.Append("LEFT JOIN");
                    break;
                case JoinType.RightJoin:
                    builder.Append("RIGHT JOIN");
                    break;
                case JoinType.CrossJoin:
                    builder.Append("CROSS JOIN");
                    break;
            }
        }

        /// <summary>
        /// 关联类型
        /// </summary>
        protected enum JoinType
        {
            /// <summary>
            /// 内关联
            /// </summary>
            InnerJoin,

            /// <summary>
            /// 左关联
            /// </summary>
            LeftJoin,

            /// <summary>
            /// 右关联
            /// </summary>
            RightJoin,

            /// <summary>
            /// 全关联
            /// </summary>
            CrossJoin,
        }
    }
}
