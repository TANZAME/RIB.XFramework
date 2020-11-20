﻿using System.Linq.Expressions;

namespace Riz.XFramework.Data
{
    /// <summary>
    /// WHERE 表达式解析器
    /// </summary>
    public class WhereExpressionVisitor : LinqExpressionVisitor
    {
        private Expression _expression = null;

        /// <summary>
        /// 初始化 <see cref="WhereExpressionVisitor"/> 类的新实例
        /// </summary>
        /// <param name="aliasGenerator">表别名解析器</param>
        /// <param name="dbExpression">要访问的表达式</param>
        public WhereExpressionVisitor(AliasGenerator aliasGenerator, DbExpression dbExpression)
            : base(aliasGenerator, dbExpression != null && dbExpression.Expressions != null ? dbExpression.Expressions[0] : null)
        {
            _expression = base.Expression;
        }

        /// <summary>
        /// 将表达式所表示的SQL片断写入SQL构造器
        /// </summary>
        /// <param name="builder">SQL 语句生成器</param>
        public override void Write(ISqlBuilder builder)
        {
            if (base.Expression != null)
            {
                base._builder = builder;
                _builder.AppendNewLine();
                _builder.Append("WHERE ");
            }

            this.WriteImpl(builder);
        }

        /// <summary>
        /// 遍历表达式
        /// </summary>
        public override Expression Visit(Expression node)
        {
            if (node == null) return null;

            // 如果初始表达式是 a=>a.Allowused && xxx 这种形式，则将 a.Allowused 解析成 a.Allowused == 1
            if (node == _expression)
            {
                node = Binary(node);
                _expression = node;
            }
            return base.Visit(node);
        }

        /// <summary>
        /// 访问 Lambda 表达式
        /// </summary>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            LambdaExpression lambda = node as LambdaExpression;
            Expression expr = this.Binary(lambda.Body);
            if (expr != lambda.Body) node = Expression.Lambda<T>(expr, lambda.Parameters);

            return base.VisitLambda<T>(node);
        }

        /// <summary>
        /// 写入SQL字符
        /// </summary>
        protected virtual void WriteImpl(ISqlBuilder builder)
        {
            base.Write(builder);
        }
    }
}