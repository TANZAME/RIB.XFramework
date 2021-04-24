﻿
namespace Riz.XFramework.Data
{
    /// <summary>
    /// 实体映射描述
    /// </summary>
    public interface IMapDescriptor
    {
        /// <summary>
        /// 选择字段范围
        /// </summary>
        /// <remarks>INSERT 表达式可能用这些字段</remarks>
        ColumnDescriptorCollection SelectedColumns { get; }

        /// <summary>
        /// 选中的导航属性描述信息
        /// <para>
        /// 用于实体与 IDataRecord 做映射
        /// </para>
        /// </summary>
        NavDescriptorCollection SelectedNavs { get; }

        /// <summary>
        /// 表达式是否包含 一对多 类型的导航属性
        /// </summary>
        bool HasMany { get; }
    }
}
