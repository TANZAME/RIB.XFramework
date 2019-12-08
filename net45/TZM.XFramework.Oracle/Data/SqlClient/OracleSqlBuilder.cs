﻿

namespace TZM.XFramework.Data.SqlClient
{
    /// <summary>
    /// SQL 语句构造器
    /// </summary>
    internal class OracleSqlBuilder : SqlBuilder
    {
        /// <summary>
        /// 是否使用双引号，ORACLE 只有最外层才需要区分大小
        /// </summary>
        internal bool UseQuote { get; set; }

        /// <summary>
        /// 整个上下文大小写敏感
        /// </summary>
        internal bool CaseSensitive { get; set; }

        /// <summary>
        /// 实例化 <see cref="OracleSqlBuilder"/> 类的新实例
        /// </summary>
        /// <param name="provider">提供者</param>
        /// <param name="token">参数列表，NULL 或者 Parameters=NULL 时表示不使用参数化</param>
        public OracleSqlBuilder(IDbQueryProvider provider, ResolveToken token)
            : base(provider, token)
        {

        }

        /// <summary>
        /// 追加成员名称
        /// </summary>
        /// <param name="name">成员名称</param>
        /// <param name="quote">使用安全符号括起来，临时表不需要括</param>
        /// <returns></returns>
        public override ISqlBuilder AppendMember(string name, bool quote)
        {
            if (this.CaseSensitive) _innerBuilder.Append(_escCharLeft);
            _innerBuilder.Append(name);
            if (this.CaseSensitive) _innerBuilder.Append(_escCharRight);
            return this;
        }

        /// <summary>
        /// 在此实例的结尾追加 AS
        /// </summary>
        public override ISqlBuilder AppendAs(string name)
        {
            _innerBuilder.Append(" AS ");
            if (this.CaseSensitive || this.UseQuote) _innerBuilder.Append(_escCharLeft);
            _innerBuilder.Append(name);
            if (this.CaseSensitive || this.UseQuote) _innerBuilder.Append(_escCharRight);
            return this;
        }
    }
}
