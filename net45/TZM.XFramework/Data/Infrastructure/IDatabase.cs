﻿
using System;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;

namespace TZM.XFramework.Data
{
    /// <summary>
    /// 定义数据库连接会话
    /// </summary>
    public partial interface IDatabase : IDisposable
    {
        /// <summary>
        /// 数据源类的提供程序
        /// </summary>
        DbProviderFactory DbProviderFactory { get; }

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        string ConnectionString { get; }

        /// <summary>
        /// 执行命令超时时间
        /// </summary>
        int? CommandTimeout { get; set; }

        /// <summary>
        /// 事务隔离级别
        /// </summary>
        IsolationLevel? IsolationLevel { get; set; }

        /// <summary>
        /// 实体转换映射委托生成器
        /// </summary>
        TypeDeserializerImpl TypeDeserializerImpl { get; }

        /// <summary>
        /// 批次执行的SQL查询语句数量
        /// </summary>
        int CommanExecuteSize { get; }

        /// <summary>
        /// 获取当前连接会话
        /// </summary>
        IDbConnection Connection { get; }

        /// <summary>
        /// 获取或者设置当前会话事务
        /// </summary>
        IDbTransaction Transaction { get; set; }

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        /// <param name="isOpen">是否打开连接</param>
        /// <returns></returns>
        IDbConnection CreateConnection(bool isOpen);

        /// <summary>
        /// 开启新事务
        /// </summary>
        IDbTransaction BeginTransaction();

        /// <summary>
        /// 创建 SQL 命令
        /// </summary>
        /// <param name="cmd">命令描述</param>
        /// <returns></returns>
        IDbCommand CreateCommand(Command cmd);

        /// <summary>
        /// 创建 SQL 命令
        /// </summary>
        /// <param name="sql">SQL 语句</param>
        /// <param name="commandType">命令类型</param>
        /// <param name="parameters">参数</param>
        /// <returns></returns>
        IDbCommand CreateCommand(string sql, CommandType? commandType = null, IEnumerable<IDbDataParameter> parameters = null);

        /// <summary>
        /// 创建命令参数
        /// </summary>
        /// <returns></returns>
        IDbDataParameter CreateParameter();

        /// <summary>
        /// 创建命令参数
        /// </summary>
        /// <param name="name">参数名称</param>
        /// <param name="value">参数值</param>
        /// <param name="dbType">数据类型</param>
        /// <param name="size">参数大小</param>
        /// <param name="precision">精度</param>
        /// <param name="scale">小数位</param>
        /// <param name="direction">方向</param>
        /// <returns></returns>
        IDbDataParameter CreateParameter(string name, object value, DbType? dbType = null, int? size = null, int? precision = null, int? scale = null, ParameterDirection? direction = null);

        /// <summary>
        /// 执行 SQL 语句，并返回受影响的行数
        /// </summary>
        /// <param name="query">查询语义</param>
        int ExecuteNonQuery(IDbQueryable query);

        /// <summary>
        /// 执行 SQL 语句，并返回受影响的行数
        /// </summary>
        /// <param name="sql">SQL 命令</param>
        int ExecuteNonQuery(string sql);

        /// <summary>
        /// 执行 SQL 语句，并返回受影响的行数
        /// </summary>
        /// <param name="sqlList">SQL 命令</param>
        int ExecuteNonQuery(List<Command> sqlList);

        /// <summary>
        /// 执行 SQL 语句，并返回受影响的行数
        /// </summary>
        /// <param name="command">SQL 命令</param>
        /// <returns></returns>
        int ExecuteNonQuery(IDbCommand command);

        /// <summary>
        /// 执行SQL 语句，并返回查询所返回的结果集中第一行的第一列。忽略额外的列或行
        /// </summary>
        /// <param name="query">查询语义</param>
        object ExecuteScalar(IDbQueryable query);

        /// <summary>
        /// 执行SQL 语句，并返回查询所返回的结果集中第一行的第一列。忽略额外的列或行
        /// </summary>
        /// <param name="sql">SQL 命令</param>
        object ExecuteScalar(string sql);

        /// <summary>
        /// 执行SQL 语句，并返回查询所返回的结果集中第一行的第一列。忽略额外的列或行
        /// </summary>
        /// <param name="sqlList">SQL 命令</param>
        /// <returns></returns>
        object ExecuteScalar(List<Command> sqlList);

        /// <summary>
        /// 执行SQL 语句，并返回查询所返回的结果集中第一行的第一列。忽略额外的列或行
        /// </summary>
        /// <param name="command">SQL 命令</param>
        /// <returns></returns>
        object ExecuteScalar(IDbCommand command);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="IDataReader"/> 对象
        /// </summary>
        /// <param name="query">查询语义</param>
        /// <returns></returns>
        IDataReader ExecuteReader(IDbQueryable query);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="IDataReader"/> 对象
        /// </summary>
        /// <param name="sql">SQL 命令</param>
        /// <returns></returns>
        IDataReader ExecuteReader(string sql);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="IDataReader"/> 对象
        /// </summary>
        /// <param name="sqlList">SQL 命令</param>
        /// <returns></returns>
        IDataReader ExecuteReader(List<Command> sqlList);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="IDataReader"/> 对象
        /// </summary>
        /// <param name="command">SQL 命令</param>
        /// <returns></returns>
        IDataReader ExecuteReader(IDbCommand command);

        /// <summary>
        /// 执行SQL 语句，并返回单个实体对象
        /// </summary>
        /// <param name="sql">查询语句</param>
        /// <returns></returns>
        T Execute<T>(string sql);

        /// <summary>
        /// 执行SQL 语句，并返回单个实体对象
        /// </summary>
        /// <param name="query">查询语句</param>
        /// <returns></returns>
        T Execute<T>(IDbQueryable<T> query);

        /// <summary>
        /// 执行SQL 语句，并返回单个实体对象
        /// <para>使用第一个 <see cref="IMapper"/> 做为实体反序列化描述</para>
        /// </summary>
        /// <param name="sqlList">查询语句</param>
        /// <returns></returns>
        T Execute<T>(List<Command> sqlList);

        /// <summary>
        /// 执行SQL 语句，并返回单个实体对象
        /// </summary>
        T Execute<T>(List<Command> sqlList, Func<IDbCommand, T> func);

        /// <summary>
        /// 执行SQL 语句，并返回单个实体对象
        /// </summary>
        /// <param name="command">SQL 命令</param>
        /// <returns></returns>
        T Execute<T>(IDbCommand command);

        /// <summary>
        /// 执行 SQL 语句，并返回两个实体集合
        /// </summary>
        /// <param name="query1">SQL 命令</param>
        /// <param name="query2">SQL 命令</param>
        Tuple<List<T1>, List<T2>> ExecuteMultiple<T1, T2>(IDbQueryable<T1> query1, IDbQueryable<T2> query2);

        /// <summary>
        /// 执行 SQL 语句，并返回两个实体集合
        /// </summary>
        /// <param name="query1">SQL 命令</param>
        /// <param name="query2">SQL 命令</param>
        /// <param name="query3">SQL 命令</param>
        Tuple<List<T1>, List<T2>, List<T3>> ExecuteMultiple<T1, T2, T3>(IDbQueryable<T1> query1, IDbQueryable<T2> query2, IDbQueryable<T3> query3);

        /// <summary>
        /// 执行 SQL 语句，并返回多个实体集合
        /// </summary>
        /// <param name="command">SQL 命令</param>
        Tuple<List<T1>, List<T2>, List<T3>, List<T4>, List<T5>, List<T6>, List<T7>> ExecuteMultiple<T1, T2, T3, T4, T5, T6, T7>(IDbCommand command);

        /// <summary>
        /// 执行SQL 语句，并返回并返回单结果集集合
        /// </summary>
        /// <param name="sql">查询语句</param>
        /// <returns></returns>
        List<T> ExecuteList<T>(string sql);

        /// <summary>
        /// 执行SQL 语句，并返回并返回单结果集集合
        /// <para>使用第一个 <see cref="IMapper"/> 做为实体反序列化描述</para>
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="query">SQL 命令</param>
        /// <returns></returns>
        List<T> ExecuteList<T>(IDbQueryable<T> query);

        /// <summary>
        /// 执行SQL 语句，并返回并返回单结果集集合
        /// </summary>
        /// <param name="sqlList">SQL 命令</param>
        /// <returns></returns>
        List<T> ExecuteList<T>(List<Command> sqlList);

        /// <summary>
        /// 执行SQL 语句，并返回并返回单结果集集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="command">SQL 命令</param>
        /// <returns></returns>
        List<T> ExecuteList<T>(IDbCommand command);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="DataTable"/> 对象
        /// </summary>
        /// <param name="sql">SQL 命令</param>
        /// <returns></returns>
        DataTable ExecuteDataTable(string sql);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="DataTable"/> 对象
        /// </summary>
        /// <param name="query">SQL 命令</param>
        /// <returns></returns>
        DataTable ExecuteDataTable(IDbQueryable query);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="DataTable"/> 对象
        /// </summary>
        /// <param name="sqlList">SQL 命令</param>
        /// <returns></returns>
        DataTable ExecuteDataTable(List<Command> sqlList);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="DataTable"/> 对象
        /// </summary>
        /// <param name="command">SQL 命令</param>
        /// <returns></returns>
        DataTable ExecuteDataTable(IDbCommand command);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="DataSet"/> 对象
        /// </summary>
        /// <param name="sql">SQL 命令</param>
        /// <returns></returns>
        DataSet ExecuteDataSet(string sql);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="DataSet"/> 对象
        /// </summary>
        /// <param name="sqlList">SQL 命令</param>
        /// <returns></returns>
        DataSet ExecuteDataSet(List<Command> sqlList);

        /// <summary>
        /// 执行SQL 语句，并返回 <see cref="DataSet"/> 对象
        /// </summary>
        /// <param name="command">SQL 命令</param>
        /// <returns></returns>
        DataSet ExecuteDataSet(IDbCommand command);
    }
}
