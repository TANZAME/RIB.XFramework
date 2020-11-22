﻿
using System;
using System.Data;
using System.Collections.Generic;

namespace Riz.XFramework.Data
{
    /// <summary>
    /// 解析SQL命令上下文
    /// </summary>
    internal class TranslateContext : ITranslateContext
    {
        private readonly IDbContext _context = null;
        private readonly IDbQueryProvider _provider = null;
        private readonly DbSQLParser _funcletizer = null;
        //private DbExpressionType? _srcDbExpressionType = null;
        //private bool? _srcIsOutQuery = null;

        /// <summary>
        /// 当前正在翻译的表达式类型
        /// </summary>
        public DbExpressionType? DbExpressionType { get; set; }

        /// <summary>
        /// 当前正在翻译最外层查询。 
        /// SELECT 语义最外层需要区别翻译 MemberInfo.Name 和 ColumnAttribute.Name => ColumnAttribute.Name As [MemberInfo.Name]
        /// </summary>
        public bool? IsOutQuery { get; set; }

        /// <summary>
        /// 参数列表
        /// </summary>
        public IList<IDbDataParameter> Parameters { get; set; }

        /// <summary>
        /// 表别名前缀
        /// <para>用于翻译 IDbQueryable.Contains 语法</para>
        /// </summary>
        public string AliasPrefix { get; set; }

        /// <summary>
        /// 当前查询上下文
        /// </summary>
        public IDbContext DbContext => _context;

        /// <summary>
        ///  查询语义提供者。代理 DbContext 的 Provider
        /// </summary>
        public IDbQueryProvider Provider => _provider;

        /// <summary>
        /// 值转SQL表达式解析器。代理 Provider 的 Funcletizer
        /// </summary>
        public DbSQLParser Funcletizer => _funcletizer;

        /// <summary>
        /// 实例化 <see cref="TranslateContext"/> 类的新实例
        /// </summary>
        /// <param name="context">当前查询上下文</param>
        public TranslateContext(IDbContext context)
        {
            XFrameworkException.Check.NotNull(context, nameof(context));
            _context = context;
            _provider = _context.Provider;
            _funcletizer = _provider.Funcletizer;
        }

        /// <summary>
        /// 复制一个实例，简化创建代码 
        /// </summary>
        /// <param name="newPrefix">指定一个新的表别名前缀，如果不是嵌套，传 null</param>
        /// <returns></returns>
        public virtual ITranslateContext Clone(string newPrefix)
        {
            var context = _provider.CreateTranslateContext(_context);
            context.Parameters = this.Parameters;
            context.AliasPrefix = newPrefix;
            return context;
        }

        ///// <summary>
        ///// 快照
        //public void Capture()
        //{
        //    _srcDbExpressionType = this.DbExpressionType;
        //    _srcIsOutQuery = this.IsOutQuery;
        //}

        ///// <summary>
        ///// 恢复快照时的状态
        ///// <returns></returns>
        //public void Reset()
        //{
        //    this.DbExpressionType = _srcDbExpressionType;
        //    this.IsOutQuery = _srcIsOutQuery;
        //}
    }
}
