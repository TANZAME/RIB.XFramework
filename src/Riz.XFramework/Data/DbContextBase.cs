
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace Riz.XFramework.Data
{
    /// <summary>
    /// 数据上下文，框架的主入口点，非线程安全
    /// </summary>
    public abstract partial class DbContextBase : IDbContext
    {
        // 结构层次： 
        // IDataContext=>IDbQueryable=>DbQueryTree

        #region 私有字段

        private IDatabase _database = null;
        private string _connString = null;
        private int? _commandTimeout = null;
        private IsolationLevel? _isolationLevel = null;
        private bool _isDebug = false;
        private DbQueryProvider _provider = null;

        /// <summary>
        /// 查询语义集合
        /// </summary>
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
                if (_database == null)
                    _database = new Database(this);
                return _database;
            }
        }

        /// <summary>
        /// 调试模式，模式模式下生成的SQL会有换行
        /// </summary>
        public bool IsDebug
        {
            get { return _isDebug; }
            set { _isDebug = value; }
        }

        /// <summary>
        /// 连接字符串
        /// </summary>
        public string ConnectionString
        {
            get { return _connString; }
            set { _connString = value; }
        }

        /// <summary>
        /// 运行事务超时时间
        /// </summary>
        public int? CommandTimeout
        {
            get { return _commandTimeout; }
            set { _commandTimeout = value; }
        }

        /// <summary>
        /// 事务隔离级别，默认 ReadCommitted
        /// </summary>
        public IsolationLevel? IsolationLevel
        {
            get { return _isolationLevel; }
            set { _isolationLevel = value; }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 <see cref="DbContextBase"/> 类的新实例
        /// <para>
        /// 默认读取 XFrameworkConnString 配置里的连接串
        /// </para>
        /// </summary>
        public DbContextBase()
            : this(Common.GetConnectionString("XFrameworkConnString"))
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
            XFrameworkException.Check.NotNull(connString, nameof(connString));

            _connString = connString;
            _commandTimeout = commandTimeout;
            _isDebug = false;
            _isolationLevel = null;
            _provider = (DbQueryProvider)this.Provider;
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 将给定实体添加到基础上下文中，当调用 SubmitChanges 时，会将该实体插入到数据库中
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="TEntity">要添加的实体</param>
        public virtual void Insert<T>(T TEntity)
        {
            IDbQueryable<T> query = this.GetTable<T>();
            query = query.CreateQuery<T>(DbExpressionType.Insert, Expression.Constant(TEntity));

            _dbQueryables.Add(query);
        }

        /// <summary>
        /// 批量将给定实体集合添加到基础上下文中，当调用 SubmitChanges 时，会将该实体插入到数据库中
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="collection">要添加的实体集合</param>
        public virtual void Insert<T>(IEnumerable<T> collection) => this.Insert<T>(collection, null);

        /// <summary>
        /// 批量将给定实体集合添加到基础上下文中，当调用 SubmitChanges 时，会将该实体插入到数据库中
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="collection">要添加的实体集合</param>
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
            _dbQueryables.Add(bulkList);
        }

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将该实体插入到数据库中
        /// <para>
        /// 实用场景：
        /// Insert Into Table1(Field1,Field2)
        /// Select xx1 AS Field1,xx2 AS Field2
        /// From Table2 a
        /// Join Table2 b ON xx=xx
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="query">要添加的实体查询语义</param>
        public virtual void Insert<T>(IDbQueryable<T> query)
        {
            query = query.CreateQuery<T>(new DbExpression(DbExpressionType.Insert));
            _dbQueryables.Add(query);
        }

        /// <summary>
        /// 将给定实体从基础上下文中删除，当调用 SubmitChanges 时，会将该实体从数据库中删除
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="TEntity">要删除的实体</param>
        public virtual void Delete<T>(T TEntity)
        {
            IDbQueryable<T> query = this.GetTable<T>();
            query = query.CreateQuery<T>(DbExpressionType.Delete, Expression.Constant(TEntity));
            _dbQueryables.Add(query);
        }

        /// <summary>
        /// 基于谓词批量将给定实体从基础上下文中删除，当调用 SubmitChanges 时，会将每一个满足条件的实体从数据库中删除
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="predicate">用于测试每个元素是否满足条件的函数</param>
        public void Delete<T>(Expression<Func<T, bool>> predicate)
        {
            IDbQueryable<T> query = this.GetTable<T>();
            this.Delete<T>(query.Where(predicate));
        }

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体从数据库中删除
        /// <para>
        /// 实用场景：
        /// Delete a 
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="source">要删除的实体查询语义</param>
        public virtual void Delete<T>(IDbQueryable<T> source)
        {
            source = source.CreateQuery<T>(new DbExpression(DbExpressionType.Delete));
            _dbQueryables.Add(source);
        }

        /// <summary>
        /// 将给定实体更新到基础上下文中，当调用 SubmitChanges 时，会将该实体更新到数据库中
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="TEntity">要添加的实体</param>
        public virtual void Update<T>(T TEntity)
        {
            IDbQueryable<T> source = this.GetTable<T>();
            this.Update<T>(Expression.Constant(TEntity), source);
        }

        /// <summary>
        /// 基于谓词批量更新到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="predicate">用于测试每个元素是否满足条件的函数</param>
        public virtual void Update<T>(Expression<Func<T, object>> updateExpression, Expression<Func<T, bool>> predicate)
        {
            IDbQueryable<T> source = this.GetTable<T>();
            this.Update<T>((Expression)updateExpression, source.Where(predicate));
        }

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景：
        /// UPDATE a SET a.Field1=xxx
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T>(Expression<Func<T, object>> updateExpression, IDbQueryable<T> source) => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景，将其它表的字段更新回目标表中
        /// UPDATE a SET a.Field1=b.Field
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// <para>
        /// 使用示例：
        /// context.Update&lt;T,T1&gt;((a,b)=>new { a.Field1=b.Field1 },query)
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <typeparam name="T1">其它实体类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T, T1>(Expression<Func<T, T1, object>> updateExpression, IDbQueryable<T> source) => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景，将其它表的字段更新回目标表中
        /// UPDATE a SET a.Field1=b.Field
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// <para>
        /// 使用示例：
        /// context.Update&lt;T,T1&gt;((a,b)=>new { a.Field1=b.Field1 },query)
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <typeparam name="T1">其它实体类型</typeparam>
        /// <typeparam name="T2">其它实体类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T, T1, T2>(Expression<Func<T, T1, T2, object>> updateExpression, IDbQueryable<T> source) => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景，将其它表的字段更新回目标表中
        /// UPDATE a SET a.Field1=b.Field
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// <para>
        /// 使用示例：
        /// context.Update&lt;T,T1&gt;((a,b)=>new { a.Field1=b.Field1 },query)
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <typeparam name="T1">其它实体类型</typeparam>
        /// <typeparam name="T2">其它实体类型</typeparam>
        /// <typeparam name="T3">其它实体类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T, T1, T2, T3>(Expression<Func<T, T1, T2, T3, object>> updateExpression, IDbQueryable<T> source) => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景，将其它表的字段更新回目标表中
        /// UPDATE a SET a.Field1=b.Field
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// <para>
        /// 使用示例：
        /// context.Update&lt;T,T1&gt;((a,b)=>new { a.Field1=b.Field1 },query)
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <typeparam name="T1">其它实体类型</typeparam>
        /// <typeparam name="T2">其它实体类型</typeparam>
        /// <typeparam name="T3">其它实体类型</typeparam>
        /// <typeparam name="T4">其它实体类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T, T1, T2, T3, T4>(Expression<Func<T, T1, T2, T3, T4, object>> updateExpression, IDbQueryable<T> source)
            => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景，将其它表的字段更新回目标表中
        /// UPDATE a SET a.Field1=b.Field
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// <para>
        /// 使用示例：
        /// context.Update&lt;T,T1&gt;((a,b)=>new { a.Field1=b.Field1 },query)
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <typeparam name="T1">其它实体类型</typeparam>
        /// <typeparam name="T2">其它实体类型</typeparam>
        /// <typeparam name="T3">其它实体类型</typeparam>
        /// <typeparam name="T4">其它实体类型</typeparam>
        /// <typeparam name="T5">其它实体类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T, T1, T2, T3, T4, T5>(Expression<Func<T, T1, T2, T3, T4, T5, object>> updateExpression, IDbQueryable<T> source)
            => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景，将其它表的字段更新回目标表中
        /// UPDATE a SET a.Field1=b.Field
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// <para>
        /// 使用示例：
        /// context.Update&lt;T,T1&gt;((a,b)=>new { a.Field1=b.Field1 },query)
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <typeparam name="T1">其它实体类型</typeparam>
        /// <typeparam name="T2">其它实体类型</typeparam>
        /// <typeparam name="T3">其它实体类型</typeparam>
        /// <typeparam name="T4">其它实体类型</typeparam>
        /// <typeparam name="T5">其它实体类型</typeparam>
        /// <typeparam name="T6">其它实体类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T, T1, T2, T3, T4, T5, T6>(Expression<Func<T, T1, T2, T3, T4, T5, T6, object>> updateExpression, IDbQueryable<T> source)
            => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 批量将实体查询语义添加到基础上下文中，当调用 SubmitChanges 时，会将每一个满足条件的实体更新到数据库中
        /// <para>
        /// 实用场景，将其它表的字段更新回目标表中
        /// UPDATE a SET a.Field1=b.Field
        /// From Table1
        /// Join Table2 b ON xx=xx
        /// </para>
        /// <para>
        /// 使用示例：
        /// context.Update&lt;T,T1&gt;((a,b)=>new { a.Field1=b.Field1 },query)
        /// </para>
        /// </summary>
        /// <typeparam name="T">给定实体的类型</typeparam>
        /// <typeparam name="T1">其它实体类型</typeparam>
        /// <typeparam name="T2">其它实体类型</typeparam>
        /// <typeparam name="T3">其它实体类型</typeparam>
        /// <typeparam name="T4">其它实体类型</typeparam>
        /// <typeparam name="T5">其它实体类型</typeparam>
        /// <typeparam name="T6">其它实体类型</typeparam>
        /// <typeparam name="T7">其它实体类型</typeparam>
        /// <param name="updateExpression">更新表达式，指定要更新的字段以及这些字段的值</param>
        /// <param name="source">要更新的实体查询语义</param>
        public virtual void Update<T, T1, T2, T3, T4, T5, T6, T7>(Expression<Func<T, T1, T2, T3, T4, T5, T6, T7, object>> updateExpression, IDbQueryable<T> source)
            => this.Update<T>((Expression)updateExpression, source);

        /// <summary>
        /// 更新记录
        /// </summary>
        protected void Update<T>(Expression updateExpression, IDbQueryable<T> source)
        {
            source = source.CreateQuery<T>(new DbExpression(DbExpressionType.Update, updateExpression));
            _dbQueryables.Add(source);
        }

        /// <summary>
        /// 添加额外查询
        /// <para>
        /// 例：SELECT FieldName FROM TableName WHERE Condition={0} AND Condition1={1}
        /// </para>
        /// </summary>
        /// <param name="sql">SQL 命令</param>
        /// <param name="args">参数列表</param>
        public void AddQuery(string sql, params object[] args)
        {
            if (!string.IsNullOrEmpty(sql))
            {
                var query = new DbRawSql(this, sql, args);
                _dbQueryables.Add(query);
            }
        }

        /// <summary>
        /// 添加额外查询
        /// </summary>
        /// <param name="query">查询语义</param>
        public void AddQuery(IDbQueryable query) => _dbQueryables.Add(query);

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改
        /// </summary>
        /// <returns></returns>
        public virtual int SubmitChanges()
        {
            List<None> result1 = null;
            List<None> result2 = null;
            List<None> result3 = null;
            List<None> result4 = null;
            List<None> result5 = null;
            List<None> result6 = null;
            List<None> result7 = null;
            return this.SubmitChanges(out result1, out result2, out result3, out result4, out result5, out result6, out result7);
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

            IDataReader reader = null;
            List<int> identitys = new List<int>();
            List<DbRawCommand> sqlList = this.Translate();

            try
            {
                Func<IDbCommand, Task<object>> func = async cmd =>
                {
                    reader = await this.Database.ExecuteReaderAsync(cmd);
                    TypeDeserializer deserializer = new TypeDeserializer(this, reader, null);
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
                this.SetIdentityValue(_dbQueryables, identitys);
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
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改。
        /// 同时返回通过 AddQuery 添加的查询语义对应的实体序列。
        /// 如果通过 AddQuery 添加了多个查询语义，只返回第一个查询语义对应的实体序列。
        /// </summary>
        /// <typeparam name="T">T</typeparam>
        /// <param name="result">通过 AddQuery 添加的第一个查询语义对应的实体序列</param>
        /// <returns></returns>
        public virtual int SubmitChanges<T>(out List<T> result)
        {
            result = new List<T>(0);
            List<None> result2 = null;
            List<None> result3 = null;
            List<None> result4 = null;
            List<None> result5 = null;
            List<None> result6 = null;
            List<None> result7 = null;
            return this.SubmitChanges(out result, out result2, out result3, out result4, out result5, out result6, out result7);
        }

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改。
        /// 同时返回通过 AddQuery 添加的查询语义对应的实体序列。
        /// 如果通过 AddQuery 添加了多个查询语义，只返回第一个和第二个查询语义对应的实体序列。
        /// </summary>
        /// <typeparam name="T1">要返回的元素类型</typeparam>
        /// <typeparam name="T2">要返回的元素类型</typeparam>
        /// <param name="result1">第一个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result2">第二个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <returns></returns>
        public virtual int SubmitChanges<T1, T2>(out List<T1> result1, out List<T2> result2)
        {
            result1 = new List<T1>();
            result2 = new List<T2>();
            List<None> result3 = null;
            List<None> result4 = null;
            List<None> result5 = null;
            List<None> result6 = null;
            List<None> result7 = null;
            return this.SubmitChanges(out result1, out result2, out result3, out result4, out result5, out result6, out result7);
        }

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改。
        /// 同时返回通过 AddQuery 添加的查询语义对应的实体序列。
        /// 如果通过 AddQuery 添加了多个查询语义，只返回第一至第三个查询语义对应的实体序列。
        /// </summary>
        /// <typeparam name="T1">要返回的元素类型</typeparam>
        /// <typeparam name="T2">要返回的元素类型</typeparam>
        /// <typeparam name="T3">要返回的元素类型</typeparam>
        /// <param name="result1">第一个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result2">第二个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result3">第三个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <returns></returns>
        public virtual int SubmitChanges<T1, T2, T3>(out List<T1> result1, out List<T2> result2, out List<T3> result3)
        {
            result1 = new List<T1>();
            result2 = new List<T2>();
            result3 = new List<T3>();
            List<None> result4 = null;
            List<None> result5 = null;
            List<None> result6 = null;
            List<None> result7 = null;
            return this.SubmitChanges(out result1, out result2, out result3, out result4, out result5, out result6, out result7);
        }

        /// <summary>
        /// 计算要插入、更新或删除的已修改对象的集，并执行相应命令以实现对数据库的更改。
        /// 同时返回通过 AddQuery 添加的查询语义对应的实体序列。
        /// 如果通过 AddQuery 添加了多个查询语义，只返回第一至第七个查询语义对应的实体序列。
        /// </summary>
        /// <typeparam name="T1">要返回的元素类型</typeparam>
        /// <typeparam name="T2">要返回的元素类型</typeparam>
        /// <typeparam name="T3">要返回的元素类型</typeparam>
        /// <typeparam name="T4">要返回的元素类型</typeparam>
        /// <typeparam name="T5">要返回的元素类型</typeparam>
        /// <typeparam name="T6">要返回的元素类型</typeparam>
        /// <typeparam name="T7">要返回的元素类型</typeparam>
        /// <param name="result1">第一个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result2">第二个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result3">第三个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result4">第四个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result5">第五个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result6">第六个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <param name="result7">第七个通过 AddQuery 添加的查询语义对应的实体序列</param>
        /// <returns></returns>
        public virtual int SubmitChanges<T1, T2, T3, T4, T5, T6, T7>(out List<T1> result1, out List<T2> result2, out List<T3> result3, out List<T4> result4, out List<T5> result5, out List<T6> result6, out List<T7> result7)
        {
            result1 = new List<T1>();
            result2 = new List<T2>();
            result3 = new List<T3>();
            result4 = new List<T4>();
            result5 = new List<T5>();
            result6 = new List<T6>();
            result7 = new List<T7>();
            int rowCount = _dbQueryables.Count;
            if (rowCount == 0) return 0;

            List<T1> q1 = null;
            List<T2> q2 = null;
            List<T3> q3 = null;
            List<T4> q4 = null;
            List<T5> q5 = null;
            List<T6> q6 = null;
            List<T7> q7 = null;
            IDataReader reader = null;
            List<int> identitys = null;
            List<DbRawCommand> sqlList = this.Translate();
            List<IMapDescriptor> maps = sqlList.ToList(a => a is IMapDescriptor, a => a as IMapDescriptor);

            Func<IDbCommand, object> doExecute = cmd =>
            {
                int index = 1;
                reader = this.Database.ExecuteReader(cmd);
                TypeDeserializer deserializer1 = null;
                TypeDeserializer deserializer2 = null;
                TypeDeserializer deserializer3 = null;
                TypeDeserializer deserializer4 = null;
                TypeDeserializer deserializer5 = null;
                TypeDeserializer deserializer6 = null;
                TypeDeserializer deserializer7 = null;
                do
                {
                    if (index == 1)
                    {
                        // 先查第一个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer1 == null) deserializer1 = new TypeDeserializer(this, reader, maps.Count > 0 ? maps[0] : null);
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
                    else if (index == 2)
                    {
                        // 再查第二个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer2 == null) deserializer2 = new TypeDeserializer(this, reader, maps.Count > 1 ? maps[1] : null);
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
                    else if (index == 3)
                    {
                        // 再查第三个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer3 == null) deserializer3 = new TypeDeserializer(this, reader, maps.Count > 2 ? maps[2] : null);
                        var collection = deserializer3.Deserialize<T3>(out autoIncrements);

                        if (autoIncrements != null)
                        {
                            if (identitys == null) identitys = new List<int>();
                            identitys.AddRange(autoIncrements);
                        }
                        else if (collection != null)
                        {
                            if (q3 == null) q3 = collection;
                        }
                    }
                    else if (index == 4)
                    {
                        // 再查第四个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer4 == null) deserializer4 = new TypeDeserializer(this, reader, maps.Count > 3 ? maps[3] : null);
                        var collection = deserializer4.Deserialize<T4>(out autoIncrements);

                        if (autoIncrements != null)
                        {
                            if (identitys == null) identitys = new List<int>();
                            identitys.AddRange(autoIncrements);
                        }
                        else if (collection != null)
                        {
                            if (q4 == null) q4 = collection;
                        }
                    }
                    else if (index == 5)
                    {
                        // 再查第五个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer5 == null) deserializer5 = new TypeDeserializer(this, reader, maps.Count > 4 ? maps[4] : null);
                        var collection = deserializer5.Deserialize<T5>(out autoIncrements);

                        if (autoIncrements != null)
                        {
                            if (identitys == null) identitys = new List<int>();
                            identitys.AddRange(autoIncrements);
                        }
                        else if (collection != null)
                        {
                            if (q5 == null) q5 = collection;
                        }
                    }
                    else if (index == 6)
                    {
                        // 再查第六个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer6 == null) deserializer6 = new TypeDeserializer(this, reader, maps.Count > 5 ? maps[5] : null);
                        var collection = deserializer6.Deserialize<T6>(out autoIncrements);

                        if (autoIncrements != null)
                        {
                            if (identitys == null) identitys = new List<int>();
                            identitys.AddRange(autoIncrements);
                        }
                        else if (collection != null)
                        {
                            if (q6 == null) q6 = collection;
                        }
                    }
                    else if (index == 7)
                    {
                        // 再查第七个类型集合
                        List<int> autoIncrements = null;
                        if (deserializer7 == null) deserializer7 = new TypeDeserializer(this, reader, maps.Count > 6 ? maps[6] : null);
                        var collection = deserializer7.Deserialize<T7>(out autoIncrements);

                        if (autoIncrements != null)
                        {
                            if (identitys == null) identitys = new List<int>();
                            identitys.AddRange(autoIncrements);
                        }
                        else if (collection != null)
                        {
                            if (q7 == null) q7 = collection;
                        }
                    }

                    index += 1;
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
                result3 = q3 ?? new List<T3>(0);
                result4 = q4 ?? new List<T4>(0);
                result5 = q5 ?? new List<T5>(0);
                result6 = q6 ?? new List<T6>(0);
                result7 = q7 ?? new List<T7>(0);
                this.SetIdentityValue(_dbQueryables, identitys);
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
        /// <typeparam name="T">对象的基类型</typeparam>
        /// <returns></returns>
        public IDbQueryable<T> GetTable<T>()
            => new DbQueryable<T>(this, new List<DbExpression> { new DbExpression(DbExpressionType.GetTable, Expression.Constant(typeof(T))) });

        /// <summary>
        /// 返回特定类型的对象的集合，其中类型由 T 参数定义
        /// <para>
        /// 适用场景：
        /// 多个导航属性类型一致，用此重载可解决 a.Navigation.Property 的表别名定位问题
        /// </para>
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <typeparam name="TNavMember">导航属性类型</typeparam>
        /// <param name="path">导航属性，注意在一组上下文中第一个 GetTable 的这个参数将被自动忽略</param>
        /// <returns></returns>
        public IDbQueryable<TNavMember> GetTable<T, TNavMember>(Expression<Func<T, TNavMember>> path)
        {
            DbQueryable<TNavMember> query = new DbQueryable<TNavMember>(this, new List<DbExpression>
            {
                new DbExpression(DbExpressionType.GetTable, new[]{ Expression.Constant(typeof(TNavMember)), (Expression)path }),
            });
            return query;

            // 保证 c.RecommendationName 和 a.RegRecommendationCode.RecommendationName 输出一致

            //// 在 select 语义保证外键属性不为空，否则在 ko 绑定的时候会产生不可预料的异常
            //var query =
            //    from a in context.GetTable<Client>()
            //        // 最新订单
            //    join b in newestQuery on a.ClientId equals b.ClientId into u_b

            //    // 注册推荐码    
            //    join c in context.GetTable<Client, RecommendationCode>(a => a.RegRecommendationCode) on a.RegRecommendationId equals c.RecommendationId into u_c
            //    // 注册推荐码对应的合作商
            //    from c in u_c.DefaultIfEmpty()
            //    join d in context.GetTable<Partner>() on new { A0 = (int)RecommendationType.ServiceAgent, B0 = c.ServiceAgentId } equals new { A0 = d.PartnerType, B0 = d.PartnerId } into u_d
            //    join e in context.GetTable<Partner>() on new { A0 = (int)RecommendationType.SaleAgent, B0 = c.SaleAgentId } equals new { A0 = e.PartnerType, B0 = e.PartnerId } into u_e

            //    // 推荐码    
            //    join f in context.GetTable<Client, RecommendationCode>(a => a.RecommendationCode) on a.RecommendationId equals f.RecommendationId into u_f
            //    // 推荐码对应的合作商
            //    from f in u_f.DefaultIfEmpty()
            //    join g in context.GetTable<Partner>() on new { A0 = (int)RecommendationType.ServiceAgent, B0 = f.ServiceAgentId } equals new { A0 = g.PartnerType, B0 = g.PartnerId } into u_g
            //    join h in context.GetTable<Partner>() on new { A0 = (int)RecommendationType.SaleAgent, B0 = f.SaleAgentId } equals new { A0 = h.PartnerType, B0 = h.PartnerId } into u_h

            //    from b in u_b.DefaultIfEmpty()
            //    from d in u_d.DefaultIfEmpty()
            //    from e in u_e.DefaultIfEmpty()
            //    from g in u_g.DefaultIfEmpty()
            //    from h in u_h.DefaultIfEmpty()
            //    select new Client(a)
            //    {
            //        PartnerShortName = h.PartnerId != null ? h.ShortName : (g.PartnerId != null ? g.ShortName : (e.PartnerId != null ? e.ShortName : d.ShortName)),
            //        Employee = new Employee { Deletable = canEidtEmployee, EmployeeCode = a.Employee.EmployeeCode, EmployeeName = a.Employee.EmployeeName },
            //        Operation = new Employee { Deletable = canEidtOperation, EmployeeName = a.Operation.EmployeeName },
            //        CS = new Employee { Deletable = canEidtCs, EmployeeName = a.CS.EmployeeName },
            //        Manager = new Employee { EmployeeName = a.Manager.EmployeeName },
            //        Signature = new Employee { EmployeeName = a.Signature.EmployeeName },
            //        Package = new Package { PackageType = a.Package.PackageType, PackageCode = a.Package.PackageCode, PackageName = a.Package.PackageName },
            //        RegRecommendationCode = new RecommendationCode
            //        {
            //            SaleType = c.SaleType,
            //            RecommendationName = c.RecommendationName
            //            //RecommendationName = a.RegRecommendationCode.RecommendationName
            //        },
            //        RecommendationCode = new RecommendationCode
            //        {
            //            SaleType = f.SaleType,
            //            RecommendationName = f.RecommendationName
            //            //RecommendationName = a.RecommendationCode.RecommendationName //f.RecommendationName
            //        },
            //        NewestOrder = new SaleOrder
            //        {
            //            OrderId = b.OrderId,
            //            OrderNo = b.OrderNo,
            //            EmployeeId = b.EmployeeId,
            //            EmployeeName = b.EmployeeName,
            //            PayDate = b.PayDate,
            //            PayAmount = b.PayAmount,
            //            ClientId = b.ClientId,
            //            Currency = b.Currency
            //        }
            //    };
            //query = query.Where(predicate);
            //return query;
        }

        /// <summary>
        /// 转换字符串为特定类型的查询对象，其中类型由 T 参数定义
        /// <para>
        /// 例：SELECT FieldName FROM TableName WHERE Condition={0} AND Condition1={1}
        /// </para>
        /// </summary>
        /// <param name="sql">SQL 命令</param>
        /// <param name="args">参数列表</param>
        public IDbQueryable<T> GetTable<T>(string sql, params object[] args)
        {
            DbQueryable<T> query = new DbQueryable<T>(this, new List<DbExpression>
            {
                new DbExpression(DbExpressionType.GetTable, new[]{ Expression.Constant(sql),Expression.Constant(args,typeof(object[])) }),
            });
            return query;
        }

        /// <summary>
        /// 解析 SQL 命令
        /// </summary>
        public List<DbRawCommand> Translate() => _provider.Translate(this._dbQueryables);

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
        protected void InternalDispose() => this._dbQueryables.Clear();

        #endregion

        #region 私有函数

        /// <summary>
        /// 更新自增列
        /// </summary>
        /// <param name="dbQueryables">查询语义集合</param>
        /// <param name="identitys">自动ID</param>
        protected virtual void SetIdentityValue(List<object> dbQueryables, List<int> identitys)
        {
            if (identitys == null || identitys.Count == 0) return;

            int index = -1;
            foreach (var obj in dbQueryables)
            {
                var dbQuery = obj as IDbQueryable;
                if (dbQuery == null) continue;
                else if (dbQuery.DbExpressions == null) continue;
                else if (dbQuery.DbExpressions.Count == 0) continue;

                var dbExpression = dbQuery.DbExpressions.FirstOrDefault(x => x.DbExpressionType == DbExpressionType.Insert);
                if (dbExpression == null) continue;
                else if (dbExpression.Expressions == null) continue;
                else if (dbExpression.Expressions[0].NodeType != ExpressionType.Constant) continue;

                var entity = (dbExpression.Expressions[0] as ConstantExpression).Value;
                if (entity != null)
                {
                    var typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(entity.GetType());
                    if (typeRuntime.Identity != null)
                    {
                        index += 1;
                        var identity = identitys[index];
                        typeRuntime.Identity.Invoke(entity, identity);
                    }
                }
            }
        }

        #endregion
    }
}
