﻿
using System;
using System.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace TZM.XFramework.Data.SqlClient
{
    /// <summary>
    /// <see cref="MySqlMethodCallExressionVisitor"/> 表达式访问器
    /// </summary>
    public class MySqlMethodCallExressionVisitor : MethodCallExressionVisitor
    {
        // https://dev.mysql.com/doc/refman/8.0/en/charset-national.html
        //SELECT N'some text';
        //SELECT n'some text';
        //SELECT _utf8'some text';

        private ISqlBuilder _builder = null;
        private IDbQueryProvider _provider = null;
        private ExpressionVisitorBase _visitor = null;
        private MemberVisitedMark _visitedMark = null;

        #region 构造函数

        /// <summary>
        /// 实例化 <see cref="SqlServerMethodCallExressionVisitor"/> 类的新实例
        /// </summary>
        public MySqlMethodCallExressionVisitor(IDbQueryProvider provider, ExpressionVisitorBase visitor)
            : base(provider, visitor)
        {
            _provider = provider;
            _visitor = visitor;
            _builder = visitor.SqlBuilder;
            _visitedMark = _visitor.VisitedMark;
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 访问表示 null 合并运算的节点 a ?? b
        /// </summary>
        public override Expression VisitCoalesce(BinaryExpression b)
        {
            // 例： a.Name ?? "TAN" => ISNULL(a.Name,'TAN')

            _builder.Append("IFNULL(");
            _visitor.Visit(b.Left is ConstantExpression ? b.Right : b.Left);
            _builder.Append(',');
            _visitor.Visit(b.Left is ConstantExpression ? b.Left : b.Right);
            _builder.Append(')');


            return b;
        }

        /// <summary>
        /// 访问 ToString 方法
        /// </summary>
        protected override Expression VisitToString(MethodCallExpression m)
        {
            _builder.Append("CAST(");
            _visitor.Visit(m.Object != null ? m.Object : m.Arguments[0]);
            _builder.Append(" AS CHAR)");

            return m;
        }

        /// <summary>
        /// 访问 string.Contains 方法
        /// </summary>
        protected override Expression VisitStringContains(MethodCallExpression m)
        {
            if (m != null)
            {
                _visitor.Visit(m.Object);
                _builder.Append(" LIKE ");
                if (m.Arguments[0].CanEvaluate())
                {
                    bool unicode = true;
                    string value = this.GetSqlValue(m.Arguments[0].Evaluate(), ref unicode);

                    if (_builder.Parameterized)
                    {
                        _builder.Append("CONCAT('%',");
                        _builder.Append(value);
                        _builder.Append(",'%')");
                    }
                    else
                    {
                        if (unicode) _builder.Append('N');
                        _builder.Append("'%");
                        _builder.Append(value);
                        _builder.Append("%'");
                    }
                }
                else
                {
                    _builder.Append("CONCAT('%',");
                    _visitor.Visit(m.Arguments[0]);
                    _builder.Append(",'%')");
                }
            }

            return m;
        }

        /// <summary>
        /// 访问 StartWidth 方法
        /// </summary>
        protected override Expression VisitStartsWith(MethodCallExpression m)
        {
            if (m != null)
            {
                _visitor.Visit(m.Object);
                _builder.Append(" LIKE ");
                if (m.Arguments[0].CanEvaluate())
                {
                    bool unicode = true;
                    string value = this.GetSqlValue(m.Arguments[0].Evaluate(), ref unicode);

                    if (_builder.Parameterized)
                    {
                        _builder.Append("CONCAT(");
                        _builder.Append(value);
                        _builder.Append(",'%')");
                    }
                    else
                    {
                        if (unicode) _builder.Append('N');
                        _builder.Append("'");
                        _builder.Append(value);
                        _builder.Append("%'");
                    }
                }
                else
                {
                    _builder.Append("CONCAT(");
                    _visitor.Visit(m.Arguments[0]);
                    _builder.Append(",'%')");
                }
            }

            return m;
        }

        /// <summary>
        /// 访问 EndWidth 方法
        /// </summary>
        protected override Expression VisitEndsWith(MethodCallExpression m)
        {
            if (m != null)
            {
                _visitor.Visit(m.Object);
                _builder.Append(" LIKE ");
                if (m.Arguments[0].CanEvaluate())
                {
                    bool unicode = true;
                    string value = this.GetSqlValue(m.Arguments[0].Evaluate(), ref unicode);

                    if (_builder.Parameterized)
                    {
                        _builder.Append("CONCAT(");
                        _builder.Append("'%',");
                        _builder.Append(value);
                        _builder.Append(')');
                    }
                    else
                    {
                        if (unicode) _builder.Append('N');
                        _builder.Append("'%");
                        _builder.Append(value);
                        _builder.Append("'");
                    }
                }
                else
                {
                    _builder.Append("CONCAT('%',");
                    _visitor.Visit(m.Arguments[0]);
                    _builder.Append(")");
                }
            }

            return m;
        }

        /// <summary>
        /// 访问 SubString 方法
        /// </summary>
        protected override Expression VisitSubstring(MethodCallExpression m)
        {
            if (m != null)
            {
                List<Expression> args = new List<Expression>(m.Arguments);
                if (m.Object != null) args.Insert(0, m.Object);

                _builder.Append("SUBSTRING(");
                _visitor.Visit(args[0]);
                _builder.Append(',');

                if (args[1].CanEvaluate())
                {
                    ConstantExpression c = args[1].Evaluate();
                    int index = Convert.ToInt32(c.Value);
                    index += 1;
                    string value = _provider.DbValue.GetSqlValue(index, _builder.Token);
                    _builder.Append(value);
                    _builder.Append(',');
                }
                else
                {
                    _visitor.Visit(args[1]);
                    _builder.Append(" + 1,");
                }

                if (args.Count == 3) _visitor.Visit(args[2]);
                else
                {
                    _builder.Append("CHAR_LENGTH(");
                    _visitor.Visit(args[0]);
                    _builder.Append(')');
                }
                _builder.Append(')');
            }

            return m;
        }

        /// <summary>
        /// 访问 Concat 方法
        /// </summary>
        protected override Expression VisitConcat(BinaryExpression b)
        {
            if (b != null)
            {
                _builder.Append("CONCAT(");
                _visitor.Visit(b.Left);
                _builder.Append(',');
                _visitor.Visit(b.Right);
                _builder.Append(')');
            }

            _visitedMark.Clear();
            return b;
        }

        /// <summary>
        /// 访问 Concat 方法
        /// </summary>
        protected override Expression VisitConcat(MethodCallExpression m)
        {
            if (m != null && m.Arguments != null && m.Arguments.Count > 0)
            {
                if (m.Arguments.Count == 1) _visitor.Visit(m.Arguments[0]);
                else
                {
                    _builder.Append("CONCAT(");
                    for (int i = 0; i < m.Arguments.Count; i++)
                    {
                        _visitor.Visit(m.Arguments[i]);
                        if (i < m.Arguments.Count - 1) _builder.Append(",");
                    }
                    _builder.Append(')');
                }
            }

            _visitedMark.Clear();
            return m;
        }

        /// <summary>
        /// 访问 Math.Truncate 方法
        /// </summary>
        protected override Expression VisitTruncate(MethodCallExpression b)
        {
            if (b != null)
            {
                _builder.Append("TRUNCATE(");
                _visitor.Visit(b.Arguments[0]);
                _builder.Append(",0)");
            }

            _visitedMark.Clear();
            return b;
        }

        /// <summary>
        /// 访问 Length 属性
        /// </summary>
        protected override Expression VisitLength(MemberExpression m)
        {
            _builder.Append("CHAR_LENGTH(");
            _visitor.Visit(m.Expression);
            _builder.Append(')');

            return m;
        }

        /// <summary>
        /// 访问 new Guid 方法
        /// </summary>
        protected override Expression VisitNewGuid(MethodCallExpression m)
        {
            _builder.Append("UUID()");
            return m;
        }

        /// <summary>
        /// 访问 IDbQueryable.Contains 方法
        /// </summary>
        protected override Expression VisitQueryableContains(MethodCallExpression m)
        {
            IDbQueryable query = m.Arguments[0].Evaluate().Value as IDbQueryable;
            ResolveToken token = _builder.Token;
            bool isDelete = token != null && token.Extendsions != null && token.Extendsions.ContainsKey("MySqlDelete");

            var cmd = query.Resolve(_builder.Indent + 1, false, token != null ? new ResolveToken
            {
                Parameters = token.Parameters,
                TableAliasName = "s",
                IsDebug = token.IsDebug
            } : null);
            Column column = ((MappingCommand)cmd).Columns.First();
            _builder.Append("EXISTS(");

            if (isDelete)
            {
                _builder.Append("SELECT 1 FROM(");
                _builder.Append(cmd.CommandText);
                _builder.Append(") ");
                _builder.Append(column.TableAlias);
                _builder.Append(" WHERE ");

                _builder.AppendMember(column.TableAlias, column.Name);
                _builder.Append(" = ");
                _visitor.Visit(m.Arguments[1]);
                _builder.Append(")");
            }
            else
            {
                _builder.Append(cmd.CommandText);
                if (((MappingCommand)cmd).WhereFragment.Length > 0)
                    _builder.Append(" AND ");
                else
                    _builder.Append("WHERE ");

                _builder.AppendMember(column.TableAlias, column.Name);
                _builder.Append(" = ");
                _visitor.Visit(m.Arguments[1]);
                _builder.Append(")");
            }

            return m;
        }

        #endregion
    }
}
