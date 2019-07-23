﻿
using System.Data;
using System.Collections.Generic;

namespace TZM.XFramework.Data
{
    /// <summary>
    /// 解析上下文携带参数
    /// </summary>
    public class ParserToken
    {
        /// <summary>
        /// 参数列表
        /// </summary>
        public List<IDbDataParameter> Parameters { get; set; }

        /// <summary>
        /// 表别名
        /// </summary>
        public TableAliasCache TableAlias { get; set; }
    }
}
