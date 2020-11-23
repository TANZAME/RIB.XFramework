﻿
using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data.SQLite;

namespace Riz.XFramework.Data
{
    /// <summary>
    /// 上下文适配器
    /// </summary>
    public sealed partial class SQLiteDatabase : Database
    {
        static MemberAccessorBase _disposedAccessor = new FieldAccessor(_disposed);
        static FieldInfo _disposed = typeof(SQLiteConnection).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// 初始化 <see cref="SQLiteDatabase"/> 类的新实例
        /// </summary>
        /// <param name="context">当前查询上下文</param>
        public SQLiteDatabase(IDbContext context)
            : base(context)
        {
        }

        /// <summary>
        /// 强制释放所有资源
        /// </summary>
        /// <param name="disposing">指示释放资源</param>
        protected override void Dispose(bool disposing)
        {
            // SQLiteConnection 连续调用 Dispose 方法会抛异常
            var connection = base.Connection;
            if (connection == null) base.Dispose(disposing);
            else
            {
                bool disposed = (bool)_disposedAccessor.Invoke(connection);
                base.Dispose(!disposed);
            }
        }
    }
}
