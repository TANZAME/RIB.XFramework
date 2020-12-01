﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riz.XFramework.Data
{
    /// <summary>
    /// 拦截器
    /// </summary>
    public static class DbInterception
    {
        static List<IDbCommandInterceptor> _interceptors = new List<IDbCommandInterceptor>();
        /// <summary>
        /// 拦截器集合
        /// </summary>
        public static List<IDbCommandInterceptor> Interceptors => _interceptors;

        /// <summary>
        /// 注册拦截器
        /// </summary>
        public static void Add(IDbCommandInterceptor interceptor) => _interceptors.Add(interceptor);

        /// <summary>
        /// 移除拦截器
        /// </summary>
        public static void Remove(IDbCommandInterceptor interceptor) => _interceptors.Remove(interceptor);

        /// <summary>
        /// 执行 SQL 前
        /// </summary>
        internal static void OnExecuting(IDbCommand command)
        {
            foreach (var interceptor in _interceptors) 
                interceptor.OnDbCommandExecuting(command);
        }

        /// <summary>
        /// 执行 SQL 后
        /// </summary>
        internal static void OnExecuted(IDbCommand command)
        {
            foreach (var interceptor in _interceptors) 
                interceptor.OnDbCommandExecuted(command);
        }

        /// <summary>
        /// 执行 SQL 异常
        /// </summary>
        internal static void OnException(DbCommandException e)
        {
            foreach (var interceptor in _interceptors) 
                interceptor.OnDbCommandException(e);
        }
    }
}
