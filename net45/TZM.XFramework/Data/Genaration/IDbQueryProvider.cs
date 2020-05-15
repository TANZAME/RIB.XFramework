﻿
using System.Data;
using System.Data.Common;
using System.Collections.Generic;

namespace TZM.XFramework.Data
{
    /// <summary>
    /// 查询语义提供者，用来构建、解析查询语义
    /// </summary>
    public interface IDbQueryProvider
    {
        /// <summary>
        /// <see cref="IDbQueryProvider"/> 实例的名称
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 获取起始字符或字符指定其名称包含空格或保留的标记等字符的数据库对象 （例如，表或列） 时使用
        /// </summary>
        string QuotePrefix { get; }

        /// <summary>
        /// 获取结束字符或字符指定其名称包含空格或保留的标记等字符的数据库对象 （例如，表或列） 时使用。
        /// </summary>
        string QuoteSuffix { get; }

        /// <summary>
        /// 字符串单引号
        /// </summary>
        string SingleQuoteChar { get; }

        /// <summary>
        /// 命令参数前缀
        /// </summary>
        string ParameterPrefix { get; }

        /// <summary>
        /// 表示一组方法，这些方法用于创建提供程序对数据源类的实现的实例。
        /// </summary>
        DbProviderFactory DbProviderFactory { get; }

        /// <summary>
        /// SQL值片断生成器
        /// </summary>
        DbValue DbValue { get; }

        /// <summary>
        /// 解析 SQL 命令
        /// </summary>
        /// <param name="dbQueryables">查询语句</param>
        List<RawCommand> Resolve(List<object> dbQueryables);


        /// <summary>
        /// 创建 SQL 命令
        /// </summary>
        /// <param name="dbQueryable">查询语句</param>
        RawCommand Resolve<T>(IDbQueryable<T> dbQueryable);

        /// <summary>
        /// 创建 SQL 命令
        /// </summary>
        /// <param name="dbQueryable">查询语句</param>
        /// <param name="indent">缩进</param>
        /// <param name="isOuter">是否最外层，内层查询不需要结束符(;)</param>
        /// <param name="token">解析上下文参数</param>
        /// <returns></returns>
        RawCommand Resolve<T>(IDbQueryable<T> dbQueryable, int indent, bool isOuter, ResolveToken token);

        /// <summary>
        /// 创建 SQL 构造器
        /// </summary>
        /// <param name="token">参数列表，NULL 或者 Parameters=NULL 时表示不使用参数化</param>
        /// <returns></returns>
        ISqlBuilder CreateSqlBuilder(ResolveToken token);

        /// <summary>
        /// 创建方法表达式访问器
        /// </summary>
        /// <param name="visitor">表达式解析器（装饰）</param>
        /// <returns></returns>
        MethodCallExpressionVisitor CreateMethodVisitor(ExpressionVisitorBase visitor);
    }
}
