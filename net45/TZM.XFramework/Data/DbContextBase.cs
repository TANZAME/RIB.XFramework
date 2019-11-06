﻿
using System;
using System.Data;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace TZM.XFramework.Data
{
    /// <summary>
    /// 数据上下文基类入口
    /// </summary>
    public abstract partial class DbContextBase : IDbContext
    {
        // 结构层次： 
        // IDataContext=>IDbQueryable=>IDbQueryableInfo

        #region 私有字段

        private IDatabase _database = null;
        private string _connString = null;
        private int? _commandTimeout = null;
        private readonly object _oLock = new object();
        protected readonly List<object> _dbQueryables = new List<object>();

        #endregion

        #region 公开属性

        /// <summary>
        /// 查询语义提供者
        /// </summary>
        public abstract IDbQueryProvider Provider { get; }

        /// <summary>
        /// 数据库对象，持有当前上下文的会话
        /// </summary>
        public virtual IDatabase Database
        {
            get
            {
                if (_database == null) _database = new Database(this.Provider.DbProviderFactory, _connString)
                {
                    CommandTimeout = _commandTimeout
                };
                return _database;
            }
        }

        /// <summary>
        /// 调试模式，模式模式下生成的SQL会有换行
        /// </summary>
        public bool IsDebug { get; set; }

        /// <summary>
        /// 事务隔离级别
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 <see cref="DbContextBase"/> 类的新实例
        /// <para>
        /// 默认读取 XFrameworkConnString 配置里的连接串
        /// </para>
        /// </summary>
        public DbContextBase()
            : this(XfwCommon.GetConnString("XFrameworkConnString"))
        {
        }

        /// <summary>
        /// 初始化 <see cref="DbContextBase"/> 类的新实例
        /// <param name="connString">数据库连接字符串</param>
        /// </summary>
        public DbContextBase(string connString)
            : this(connString, null)
        {
        }

        /// <summary>
        /// 初始化 <see cref="DbContextBase"/> 类的新实例
        /// <param name="connString">数据库连接字符串</param>
        /// <param name="commandTimeout">执行命令超时时间</param>
        /// </summary>
        public DbContextBase(string connString, int? commandTimeout)
        {
            _connString = connString;
            _commandTimeout = commandTimeout;
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 新增记录
        /// </summary>
        public virtual void Insert<T>(T TEntity)
        {
            IDbQueryable<T> query = this.GetTable<T>();
            query = query.CreateQuery<T>(DbExpressionType.Insert, Expression.Constant(TEntity));

            lock (this._oLock)
                _dbQueryables.Add(query);
        }

        /// <summary>
        /// 批量新增记录
        /// </summary>
        public virtual void Insert<T>(IEnumerable<T> collection)
        {
            this.Insert<T>(collection, null);
        }

        /// <summary>
        /// 批量新增记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection">批量插入列表</param>
        /// <param name="entityColumns">指定插入的列</param>
        public virtual void Insert<T>(IEnumerable<T> collection, IList<Expression> entityColumns)
        {
            List<IDbQueryable> bulkList = new List<IDbQueryable>();
            foreach (T value in collection)
            {
                IDbQueryable<T> query = this.GetTable<T>();
                var expressions = entityColumns != null ? new[] { Expression.Constant(value), Expression.Constant(entityColumns) } : new[] { Expression.Constant(value) };
                query = query.CreateQuery<T>(new DbExpression(DbExpressionType.Insert, expressions));

                bulkList.Add(query);
            }

            lock (this._oLock)
                _dbQueryables.Add(bulkList);
        }

        /// <summary>
        /// 批量新增记录
        /// </summary>
        public virtual void Insert<T>(IDbQueryable<T> query)
        {
            query = query.CreateQuery<T>(new DbExpression(DbExpressionType.Insert));
            lock (this._oLock)
                _dbQueryables.Add(query);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        public void Delete<T>(T TEntity)
        {
            IDbQueryable<T> query = this.GetTable<T>();
            query = query.CreateQuery<T>(DbExpressionType.Delete, Expression.Constant(TEntity));

            lock (this._oLock)
                _dbQueryables.Add(query);
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        public void Delete<T>(Expression<Func<T, bool>> predicate)
        {
            IDbQueryable<T> query = this.GetTable<T>();
            this.Delete<T>(query.Where(predicate));
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        public void Delete<T>(IDbQueryable<T> query)
        {
            query = query.CreateQuery<T>(new DbExpression(DbExpressionType.Delete));
            lock (this._oLock)
                _dbQueryables.Add(query);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        public void Update<T>(T TEntity)
        {
            IDbQueryable<T> source = this.GetTable<T>();
            this.Update<T>(Expression.Constant(TEntity), source);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        public virtual void Update<T>(Expression<Func<T, object>> updateExpression, Expression<Func<T, bool>> predicate)
        {
            IDbQueryable<T> source = this.GetTable<T>();
            this.Update<T>((Expression)updateExpression, source.Where(predicate));
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        public virtual void Update<T>(Expression<Func<T, object>> updateExpression, IDbQueryable<T> source)
        {
            this.Update<T>((Expression)updateExpression, source);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        public virtual void Update<T, TSource>(Expression<Func<T, TSource, object>> updateExpression, IDbQueryable<T> source)
        {
            this.Update<T>((Expression)updateExpression, source);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        public virtual void Update<T, TSource1, TSource2>(Expression<Func<T, TSource1, TSource2, object>> updateExpression, IDbQueryable<T> source)
        {
            this.Update<T>((Expression)updateExpression, source);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        public virtual void Update<T, TSource1, TSource2, TSource3>(Expression<Func<T, TSource1, TSource2, TSource3, object>> updateExpression, IDbQueryable<T> source)
        {
            this.Update<T>((Expression)updateExpression, source);
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        protected void Update<T>(Expression updateExpression, IDbQueryable<T> source)
        {
            source = source.CreateQuery<T>(new DbExpression(DbExpressionType.Update, updateExpression));
            lock (this._oLock)
                _dbQueryables.Add(source);
        }

        /// <summary>
        /// 附加查询项
        /// </summary>
        public void AddQuery(string sql, params object[] args)
        {
            if (!string.IsNullOrEmpty(sql))
            {
                var query = new RawSql(sql, args);
                lock (this._oLock)
                    _dbQueryables.Add(query);
            }
        }

        /// <summary>
        /// 附加查询项
        /// </summary>
        public void AddQuery(IDbQueryable query)
        {
            lock (this._oLock)
                _dbQueryables.Add(query);
        }

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改
        /// </summary>
        /// <returns></returns>
        public virtual int SubmitChanges()
        {
            int rowCount = _dbQueryables.Count;
            if (rowCount == 0) return 0;

            IDataReader reader = null;
            List<int> identitys = null;
            List<Command> sqlList = this.Provider.Resolve(_dbQueryables);

            Func<IDbCommand, object> doExecute = cmd =>
            {
                reader = this.Database.ExecuteReader(cmd);
                TypeDeserializer deserializer = new TypeDeserializer(this.Database, reader, null);
                do
                {
                    List<int> autoIncrements = null;
                    deserializer.Deserialize<object>(out autoIncrements);
                    if (autoIncrements != null && autoIncrements.Count > 0)
                    {
                        if (identitys == null) identitys = new List<int>();
                        identitys.AddRange(autoIncrements);
                    }
                }
                while (reader.NextResult());

                // 释放当前的reader
                if (reader != null) reader.Dispose();
                return null;
            };

            try
            {
                this.Database.Execute<object>(sqlList, doExecute);
                this.SetAutoIncrementValue(_dbQueryables, identitys);
                return rowCount;
            }
            finally
            {
                if (reader != null) reader.Dispose();
                this.InternalDispose();
            }
        }

#if !net40

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改
        /// </summary>
        /// <returns></returns>
        public virtual async Task<int> SubmitChangesAsync()
        {
            int rowCount = _dbQueryables.Count;
            if (rowCount == 0) return 0;

            List<Command> sqlList = this.Provider.Resolve(_dbQueryables);
            List<int> identitys = new List<int>();
            IDataReader reader = null;

            try
            {
                Func<IDbCommand, Task<object>> func = async cmd =>
                {
                    reader = await this.Database.ExecuteReaderAsync(cmd);
                    TypeDeserializer deserializer = new TypeDeserializer(this.Database, reader, null);
                    do
                    {
                        List<int> result = null;
                        deserializer.Deserialize<int>(out result);
                        if (result != null && result.Count > 0) identitys.AddRange(result);
                    }
                    while (reader.NextResult());

                    // 释放当前的reader
                    if (reader != null) reader.Dispose();

                    return null;
                };

                await this.Database.ExecuteAsync<object>(sqlList, func);
                this.SetAutoIncrementValue(_dbQueryables, identitys);
                return rowCount;
            }
            finally
            {
                if (reader != null) reader.Dispose();
                this.InternalDispose();
            }
        }

#endif

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改
        /// </summary>
        /// <typeparam name="T">T</typeparam>
        /// <param name="result">提交更改并查询数据</param>
        /// <returns></returns>
        public virtual int SubmitChanges<T>(out List<T> result)
        {
            result = new List<T>();
            int rowCount = _dbQueryables.Count;
            if (rowCount == 0) return 0;

            List<T> q1 = null;
            IDataReader reader = null;
            List<int> identitys = null;
            List<Command> sqlList = this.Provider.Resolve(_dbQueryables);

            Func<IDbCommand, object> doExecute = cmd =>
            {
                reader = this.Database.ExecuteReader(cmd);
                TypeDeserializer deserializer = new TypeDeserializer(this.Database, reader, null);
                do
                {
                    List<int> autoIncrements = null;
                    var collection = deserializer.Deserialize<T>(out autoIncrements);
                    if (autoIncrements != null)
                    {
                        if (identitys == null) identitys = new List<int>();
                        identitys.AddRange(autoIncrements);
                    }
                    else if (collection != null)
                    {
                        if (q1 == null) q1 = collection;
                    }
                }
                while (reader.NextResult());

                // 释放当前的reader
                if (reader != null) reader.Dispose();
                return null;
            };

            try
            {
                this.Database.Execute<object>(sqlList, doExecute);
                result = q1 ?? new List<T>(0);
                this.SetAutoIncrementValue(_dbQueryables, identitys);
                return rowCount;
            }
            finally
            {
                if (reader != null) reader.Dispose();
                this.InternalDispose();
            }
        }

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改
        /// </summary>
        /// <typeparam name="T1">T</typeparam>
        /// <typeparam name="T2">T</typeparam>
        /// <param name="result1">提交更改并查询数据</param>
        /// <returns></returns>
        public virtual int SubmitChanges<T1, T2>(out List<T1> result1, out List<T2> result2)
        {
            result1 = new List<T1>();
            result2 = new List<T2>();
            int rowCount = _dbQueryables.Count;
            if (rowCount == 0) return 0;

            List<T1> q1 = null;
            List<T2> q2 = null;
            IDataReader reader = null;
            List<int> identitys = null;
            List<Command> sqlList = this.Provider.Resolve(_dbQueryables);
            List<IMapping> maps = sqlList.ToList(x => x as IMapping, x => x is IMapping);

            Func<IDbCommand, object> doExecute = cmd =>
            {
                reader = this.Database.ExecuteReader(cmd);
                TypeDeserializer deserializer1 = null;
                TypeDeserializer deserializer2 = null;
                do
                {
                    if (q1 == null)
                    {
                        // 先查第一个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer1 == null) deserializer1 = new TypeDeserializer(this.Database, reader, maps.Count > 0 ? maps[0] : null);
                        var collection = deserializer1.Deserialize<T1>(out autoIncrements);

                        if (autoIncrements != null)
                        {
                            if (identitys == null) identitys = new List<int>();
                            identitys.AddRange(autoIncrements);
                        }
                        else if (collection != null)
                        {
                            q1 = collection;
                        }
                    }
                    else
                    {
                        // 再查第二个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer2 == null) deserializer2 = new TypeDeserializer(this.Database, reader, maps.Count > 1 ? maps[1] : null);
                        var collection = deserializer2.Deserialize<T2>(out autoIncrements);

                        if (autoIncrements != null)
                        {
                            if (identitys == null) identitys = new List<int>();
                            identitys.AddRange(autoIncrements);
                        }
                        else if (collection != null)
                        {
                            if (q2 == null) q2 = collection;
                        }
                    }

                }
                while (reader.NextResult());

                // 释放当前的reader
                if (reader != null) reader.Dispose();
                return null;
            };

            try
            {
                this.Database.Execute<object>(sqlList, doExecute);
                result1 = q1 ?? new List<T1>(0);
                result2 = q2 ?? new List<T2>(0);
                this.SetAutoIncrementValue(_dbQueryables, identitys);
                return rowCount;
            }
            finally
            {
                if (reader != null) reader.Dispose();
                this.InternalDispose();
            }
        }

        /// <summary>
        /// 返回特定类型的对象的集合，其中类型由 T 参数定义
        /// </summary>
        public IDbQueryable<T> GetTable<T>()
        {
            DbQueryable<T> query = new DbQueryable<T>(this, new List<DbExpression> { new DbExpression(DbExpressionType.GetTable, Expression.Constant(typeof(T))) });
            return query;
        }

        /// <summary>
        /// 释放由 <see cref="DbContextBase"/> 类的当前实例占用的所有资源
        /// </summary>
        public void Dispose()
        {
            this.InternalDispose();
            if (this.Database != null) this.Database.Dispose();
        }

        /// <summary>
        /// 释放由 <see cref="DbContextBase"/> 类的当前实例占用的所有资源
        /// </summary>
        protected void InternalDispose()
        {
            lock (this._oLock) this._dbQueryables.Clear();
        }

        #endregion

        #region 私有函数

        // 更新自增列
        protected virtual void SetAutoIncrementValue(List<object> dbQueryables, List<int> identitys)
        {
            if (identitys == null || identitys.Count == 0) return;

            int index = -1;
            foreach (var obj in dbQueryables)
            {
                IDbQueryable query = obj as IDbQueryable;
                if (query == null) continue;

                var info = query.DbQueryInfo as IDbQueryableInfo_Insert;
                if (info != null && info.Entity != null && info.AutoIncrement != null)
                {
                    index += 1;
                    var identity = identitys[index];
                    info.AutoIncrement.Invoke(info.Entity, identity);
                }
            }
        }

        #endregion
    }
}
