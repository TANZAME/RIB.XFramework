﻿
using System;
using System.Data;
using System.Reflection;
using System.Reflection.Emit;

namespace Riz.XFramework.Data
{
    /// <summary>
    /// <see cref="IDataReader"/> 转实体映射
    /// </summary>
    internal sealed class NpgTypeDeserializerImpl : TypeDeserializerImpl
    {
        static readonly MethodInfo _fromTicks = typeof(TimeSpan).GetMethod("FromTicks", new Type[] { typeof(long) });
        static readonly MethodInfo _getTicks = typeof(DateTime).GetProperty("Ticks", BindingFlags.Public | BindingFlags.Instance).GetGetMethod();

        /// <summary>
        /// 单例实现
        /// </summary>
        public static new NpgTypeDeserializerImpl Instance = new NpgTypeDeserializerImpl();

        /// <summary>
        /// 实例化 <see cref="NpgTypeDeserializerImpl"/> 类的新实例
        /// </summary>
        NpgTypeDeserializerImpl() 
            : base()
        {
        }

#if !netcore

        /// <summary>
        /// 自定义类型转换
        /// </summary>
        /// <param name="il">(MSIL)指令</param>
        /// <param name="from">来源类型</param>
        /// <param name="to">目标类型</param>
        /// <param name="via">来源类型</param>
        /// <returns></returns>
        protected override bool ConvertBoxedStackExtension(ILGenerator il, Type from, Type to, Type via)
        {
            bool isExecuted = base.ConvertBoxedStackExtension(il, from, to, via);
            if (isExecuted) return isExecuted;
            
            bool isTimespan = from == typeof(DateTime) && (to == typeof(TimeSpan) || to == typeof(TimeSpan?));
            if (isTimespan)
            {
                int localIndex = il.DeclareLocal(typeof(DateTime)).LocalIndex;
                il.StoreLocal(localIndex);

                // DateTime.Ticks
                il.LoadLocalAddress(localIndex);
                il.Emit(OpCodes.Call, _getTicks);

                // TimeSpan.FromTicks
                il.Emit(OpCodes.Call, _fromTicks);

                return true;
            }

            //return new DateTimeOffset(oracleTimeStampTZ.Value, oracleTimeStampTZ.GetTimeZoneOffset());

            return false;
        }

#endif

    }
}
