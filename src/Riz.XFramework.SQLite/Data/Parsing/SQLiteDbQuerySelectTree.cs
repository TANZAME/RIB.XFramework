﻿
namespace Riz.XFramework.Data
{
    /// <summary>
    /// 提供对数据类型未知的特定数据源进行 &lt;查&gt; 操作的语义表示
    /// </summary>
    public class SQLiteDbQuerySelectTree : DbQuerySelectTree, IWithRowId
    {
        /// <summary>
        /// 初始化 <see cref="SQLiteDbQuerySelectTree"/> 类的新实例
        /// </summary>
        public SQLiteDbQuerySelectTree(DbQuerySelectTree source)
            : base()
        {
            this.Joins = source.Joins;
            this.OrderBys = source.OrderBys;
            this.Includes = source.Includes;
            this.HasDistinct = source.HasDistinct;
            this.HasAny = source.HasAny;
            this.SelectHasMany = source.SelectHasMany;
            this.Skip = source.Skip;
            this.Take = source.Take;
            this.From = source.From;
            this.Select = source.Select;
            this.Wheres = source.Wheres;
            this.Havings = source.Havings;
            this.Aggregate = source.Aggregate;
            this.GroupBy = source.GroupBy;
            this.Subquery = source.Subquery;
            this.Unions = source.Unions;
        }
    }
}