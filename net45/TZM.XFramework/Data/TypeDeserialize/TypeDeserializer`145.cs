﻿
//#if netcore

//using System;
//using System.Data;
//using System.Linq;
//using System.Text;
//using System.Reflection;
//using System.Reflection.Emit;
//using System.Collections.Generic;

//namespace TZM.XFramework.Data
//{
//    /// <summary>
//    /// 单个实体反序列化
//    /// </summary>
//    internal class TypeDeserializer<T>
//    {
//        private IDataRecord _reader = null;
//        private SelectDbCommandDefinition _definition = null;
//        private IDictionary<string, Func<IDataRecord, object>> _deserializers = null;
//        private Func<IDataRecord, object> _modelDeserializer = null;
//        private Dictionary<string, HashSet<string>> _listNavigationKeys = null;
//        private int? _listNavigationCount = null;

//        /// <summary>
//        /// 实例化<see cref="TypeDeserializer"/> 类的新实例
//        /// </summary>
//        /// <param name="reader">DataReader</param>
//        /// <param name="definition">SQL 命令描述</param>
//        internal TypeDeserializer(IDataReader reader, SelectDbCommandDefinition definition)
//        {
//            _reader = reader;
//            _definition = definition;
//            _deserializers = new Dictionary<string, Func<IDataRecord, object>>(8);
//            _listNavigationKeys = new Dictionary<string, HashSet<string>>(8);
//        }

//        /// <summary>
//        /// 将 <see cref="IDataRecord"/> 上的当前行反序列化为实体
//        /// </summary>
//        /// <param name="prevModel">前一行数据</param>
//        /// <param name="isSameLine">是否同行数据</param>
//        internal T Deserialize(object prevModel, out bool isSameLine)
//        {
//            isSameLine = false;

//            #region 基元类型

//            string name = _reader.GetName(0);
//            if (TypeUtils.IsPrimitiveType(typeof(T)) || name == Constant.AUTOINCREMENTNAME)
//            {
//                if (_reader.IsDBNull(0)) return default(T);

//                var obj = _reader.GetValue(0);
//                if (obj.GetType() != typeof(T))
//                {
//                    // fix#Nullable<T> issue
//                    if (!typeof(T).IsGenericType) obj = Convert.ChangeType(obj, typeof(T));
//                    else
//                    {
//                        Type g = typeof(T).GetGenericTypeDefinition();
//                        if (g != typeof(Nullable<>)) throw new NotSupportedException(string.Format("type {0} not suppored.", g.FullName));
//                        obj = Convert.ChangeType(obj, Nullable.GetUnderlyingType(typeof(T)));
//                    }
//                }

//                return (T)obj;
//            }

//            #endregion

//            #region 匿名类型

//            TypeRuntimeInfo typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo<T>();
//            if (typeRuntime.IsAnonymousType)
//            {
//                // 直接跑SQL,则不解析导航属性
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader, _definition.Columns, 0);
//                return (T)_modelDeserializer(_reader);
//            }

//            #endregion

//            #region 实体类型

//            T model = default(T);
//            if (_definition == null)
//            {
//                // 直接跑SQL,则不解析导航属性
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader);
//                model = (T)_modelDeserializer(_reader);
//            }
//            else if (_definition.Navigations != null && _definition.Navigations.Count == 0)
//            {
//                // 直接跑SQL,则不解析导航属性
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader, _definition.Columns, 0);
//                model = (T)_modelDeserializer(_reader);
//            }
//            else
//            {
//                // 第一层
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader, _definition.Columns, 0, _definition.Navigations.MinIndex);
//                model = (T)_modelDeserializer(_reader);
//                // 若有 1:n 的导航属性，判断当前行数据与上一行数据是否相同
//                if (prevModel != null && _definition.HaveListNavigation)
//                {
//                    isSameLine = true;
//                    foreach (var key in typeRuntime.KeyInvokers)
//                    {
//                        var invoker = key.Value;
//                        var value1 = invoker.Invoke(prevModel);
//                        var value2 = invoker.Invoke(model);
//                        isSameLine = isSameLine && value1.Equals(value2);
//                        if (!isSameLine)
//                        {
//                            // fix issue#换行时清空上一行的导航键缓存
//                            _listNavigationKeys.Clear();
//                            break;
//                        }
//                    }
//                }

//                // 递归导航属性
//                this.Deserialize_Navigation(isSameLine ? prevModel : null, model, string.Empty, isSameLine);
//            }

//            return model;

//            #endregion
//        }

//        // 反序列化导航属性
//        // @prevLine 前一行数据
//        // @isLine   是否同一行数据<同一父级>
//        void Deserialize_Navigation(object prevModel, object model, string typeName, bool isSameLine)
//        {
//            // CRM_SaleOrder.Client 
//            // CRM_SaleOrder.Client.AccountList
//            Type type = model.GetType();
//            TypeRuntimeInfo typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(type);
//            if (string.IsNullOrEmpty(typeName)) typeName = type.Name;

//            foreach (var kvp in _definition.Navigations)
//            {
//                int start = -1;
//                int end = -1;
//                var descriptor = kvp.Value;
//                if (descriptor.FieldCount > 0)
//                {
//                    start = descriptor.Start;
//                    end = descriptor.Start + descriptor.FieldCount;
//                }

//                string keyName = typeName + "." + descriptor.Name;
//                if (keyName != kvp.Key) continue;

//                var navInvoker = typeRuntime.GetInvoker(descriptor.Name);
//                if (navInvoker == null) continue;

//                Type navType = navInvoker.DataType;
//                Func<IDataRecord, object> deserializer = null;
//                TypeRuntimeInfo navTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navType);
//                object navCollection = null;
//                //if (navType.IsGenericType && navTypeRuntime.GenericTypeDefinition == typeof(List<>))
//                if (TypeUtils.IsCollectionType(navType))
//                {
//                    // 1：n关系，导航属性为 List<T>
//                    navCollection = navInvoker.Invoke(model);
//                    if (navCollection == null)
//                    {
//                        // new 一个列表类型，如果导航属性定义为接口，则默认使用List<T>来实例化
//                        TypeRuntimeInfo navTypeRuntime2 = navType.IsInterface
//                            ? TypeRuntimeInfoCache.GetRuntimeInfo(typeof(List<>).MakeGenericType(navTypeRuntime.GenericArguments[0]))
//                            : navTypeRuntime;
//                        navCollection = navTypeRuntime2.ConstructInvoker.Invoke();
//                        navInvoker.Invoke(model, navCollection);
//                    }
//                }

//                if (!_deserializers.TryGetValue(keyName, out deserializer))
//                {
//                    deserializer = InternalTypeDeserializer.GetTypeDeserializer(navType.IsGenericType ? navTypeRuntime.GenericArguments[0] : navType, _reader, _definition.Columns, start, end);
//                    _deserializers[keyName] = deserializer;
//                }

//                // 如果整个导航链中某一个导航属性为空，则跳出递归
//                object navModel = deserializer(_reader);
//                if (navModel != null)
//                {
//                    if (navCollection == null)
//                    {
//                        // 非集合型导航属性
//                        navInvoker.Invoke(model, navModel);
//                        //
//                        //
//                        // 
//                    }
//                    else
//                    {
//                        // 集合型导航属性
//                        if (prevModel != null && isSameLine)
//                        {
//                            #region 合并列表

//                            // 判断如果属于同一个主表，则合并到上一行的当前明细列表
//                            // 例：CRM_SaleOrder.Client.AccountList
//                            string[] keys = keyName.Split('.');
//                            TypeRuntimeInfo curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo<T>();
//                            Type curType = curTypeRuntime.Type;
//                            MemberInvokerBase curInvoker = null;
//                            object curModel = prevModel;

//                            for (int i = 1; i < keys.Length; i++)
//                            {
//                                curInvoker = curTypeRuntime.GetInvoker(keys[i]);
//                                curModel = curInvoker.Invoke(curModel);
//                                if (curModel == null) continue;

//                                curType = curModel.GetType();
//                                curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(curType);

//                                // <<<<<<<<<<< 一对多对多关系 >>>>>>>>>>
//                                if (curType.IsGenericType && i != keys.Length - 1)
//                                {
//                                    curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(curType);
//                                    //if (curTypeRuntime.GenericTypeDefinition == typeof(List<>))
//                                    if (TypeUtils.IsCollectionType(curType))
//                                    {
//                                        var invoker = curTypeRuntime.GetInvoker("get_Count");
//                                        int count = Convert.ToInt32(invoker.Invoke(curModel));      // List.Count
//                                        if (count > 0)
//                                        {
//                                            var invoker2 = curTypeRuntime.GetInvoker("get_Item");
//                                            curModel = invoker2.Invoke(curModel, count - 1);        // List[List.Count-1]
//                                            curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(curModel.GetType());
//                                        }
//                                        else
//                                        {
//                                            // user.Roles.RoleFuncs=>Roles 列表有可能为空
//                                            curModel = null;
//                                            break;
//                                        }
//                                    }
//                                }
//                            }


//                            if (curModel != null)
//                            {
//                                // 如果有两个以上的一对多关系的导航属性，那么在加入列表之前就需要剔除重复的实体


//                                bool isAny = false;
//                                if (_definition.Navigations.Count > 1)
//                                {
//                                    if (_listNavigationCount == null) _listNavigationCount = _definition.Navigations.Count(x => CheckCollectionNavigation(x.Value.Member));
//                                    if (_listNavigationCount != null && _listNavigationCount.Value > 1)
//                                    {
//                                        if (!_listNavigationKeys.ContainsKey(keyName)) _listNavigationKeys[keyName] = new HashSet<string>();
//                                        curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navModel.GetType());
//                                        StringBuilder keyBuilder = new StringBuilder(64);

//                                        foreach (var key in curTypeRuntime.KeyInvokers)
//                                        {
//                                            var invoker = key.Value;
//                                            var value = invoker.Invoke(navModel);
//                                            keyBuilder.AppendFormat("{0}={1};", key.Key, (value ?? string.Empty).ToString());
//                                        }
//                                        string hash = keyBuilder.ToString();
//                                        if (_listNavigationKeys[keyName].Contains(hash))
//                                        {
//                                            isAny = true;
//                                        }
//                                        else
//                                        {
//                                            _listNavigationKeys[keyName].Add(hash);
//                                        }
//                                    }
//                                }

//                                if (!isAny)
//                                {
//                                    // 如果列表中不存在，则添加到上一行的相同导航列表中去
//                                    var myAddMethod = navTypeRuntime.GetInvoker("Add");
//                                    myAddMethod.Invoke(curModel, navModel);
//                                }
//                            }

//                            #endregion
//                        }
//                        else
//                        {
//                            // 此时的 navTypeRuntime 是 List<> 类型的运行时
//                            // 先添加 List 列表
//                            var myAddMethod = navTypeRuntime.GetInvoker("Add");
//                            myAddMethod.Invoke(navCollection, navModel);

//                            var curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navModel.GetType());
//                            StringBuilder keyBuilder = new StringBuilder(64);

//                            foreach (var key in curTypeRuntime.KeyInvokers)
//                            {
//                                var wrapper = key.Value;
//                                var value = wrapper.Invoke(navModel);
//                                keyBuilder.AppendFormat("{0}={1};", key.Key, (value ?? string.Empty).ToString());
//                            }
//                            string hash = keyBuilder.ToString();
//                            if (!_listNavigationKeys.ContainsKey(keyName)) _listNavigationKeys[keyName] = new HashSet<string>();
//                            if (!_listNavigationKeys[keyName].Contains(hash)) _listNavigationKeys[keyName].Add(hash);
//                        }
//                    }

//                    //if (navTypeRuntime.GenericTypeDefinition == typeof(List<>)) navTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navTypeRuntime.GenericArguments[0]);
//                    if (TypeUtils.IsCollectionType(navTypeRuntime.Type)) navTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navTypeRuntime.GenericArguments[0]);
//                    if (navTypeRuntime.NavInvokers.Count > 0) Deserialize_Navigation(prevModel, navModel, keyName, isSameLine);
//                }
//            }
//        }

//        // 检查当前成员是否是1：N关系的导航属性
//        static bool CheckCollectionNavigation(MemberInfo info)
//        {
//            if (info == null) return false;

//            // 仅支持属性和字段***
//            Type type = null;
//            if (info.MemberType == MemberTypes.Property) type = ((PropertyInfo)info).PropertyType;
//            else if (info.MemberType == MemberTypes.Field) type = ((FieldInfo)info).FieldType;
//            return type == null ? false : TypeUtils.IsCollectionType(type);
//        }

//        /// <summary>
//        /// 内部类型反序列化器
//        /// </summary>
//        internal class InternalTypeDeserializer
//        {
//            // 这个类的代码在 Dapper.NET 的基础上修改的，实体映射确实强悍得一匹
//            // https://github.com/StackExchange/Dapper

//            const string _linqBinaryName = "System.Data.Linq.Binary";
//            static readonly MethodInfo _enumParse = typeof(Enum).GetMethod("Parse", new Type[] { typeof(Type), typeof(string), typeof(bool) });
//            static readonly MethodInfo _getValue = typeof(IDataRecord).GetMethod("GetValue", new Type[] { typeof(int) });
//            static MethodInfo _isDBNull = typeof(IDataRecord).GetMethod("IsDBNull", new Type[] { typeof(int) });
//            static MethodInfo _readChar = typeof(InternalTypeDeserializer).GetMethod("ReadChar", BindingFlags.Static | BindingFlags.NonPublic);
//            static MethodInfo _readNullChar = typeof(InternalTypeDeserializer).GetMethod("ReadNullableChar", BindingFlags.Static | BindingFlags.NonPublic);
//            static MethodInfo _throwException = typeof(InternalTypeDeserializer).GetMethod("ThrowDataException", BindingFlags.Static | BindingFlags.NonPublic);
//            static MethodInfo _typeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
//            static MethodInfo _changeType = typeof(Convert).GetMethod("ChangeType", new Type[] { typeof(object), typeof(Type) });

//            static ConstructorInfo _ctorGuid_string = typeof(Guid).GetConstructor(new[] { typeof(string) });
//            static ConstructorInfo _ctorGuid_bytes = typeof(Guid).GetConstructor(new[] { typeof(byte[]) });
//            //static ConstructorInfo _ctorXmlReader = typeof(XmlTextReader).GetConstructor(new[] { typeof(string), typeof(XmlNodeType), typeof(XmlParserContext) });
//            //static ConstructorInfo _ctorSqlXml = typeof(System.Data.SqlTypes.SqlXml).GetConstructor(new[] { typeof(System.Xml.XmlTextReader) });

//            internal static Func<IDataRecord, object> GetTypeDeserializer(Type type, IDataRecord reader, IDictionary<string, Column> columns = null, int start = 0, int? end = null)
//            {
//                TypeRuntimeInfo typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(type);
//                DynamicMethod method = new DynamicMethod(string.Format("Deserialize{0}", Guid.NewGuid()), typeof(object), new Type[] { typeof(IDataRecord) }, true);
//                ILGenerator il = method.GetILGenerator();

//                il.DeclareLocal(typeof(int));
//                il.DeclareLocal(type);
//                il.DeclareLocal(typeof(object));

//                il.Emit(OpCodes.Ldc_I4_0);
//                il.Emit(OpCodes.Stloc_0);
//                il.Emit(OpCodes.Ldnull);
//                il.Emit(OpCodes.Stloc_2);

//                ConstructorInfo specializedConstructor = null;
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Ldloca_S, (byte)1);
//                    il.Emit(OpCodes.Initobj, type);
//                }
//                else
//                {
//                    var ctor = typeRuntime.ConstructInvoker.Constructor;
//                    if (ctor.GetParameters().Length == 0)
//                    {
//                        il.Emit(OpCodes.Newobj, ctor);
//                        il.Emit(OpCodes.Stloc_1);
//                    }
//                    else
//                        specializedConstructor = ctor;
//                }

//                // try #####
//                il.BeginExceptionBlock();
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Ldloca_S, (byte)1);         // [target]
//                }
//                else if (specializedConstructor == null)
//                {
//                    il.Emit(OpCodes.Ldloc_1);                   // [target]
//                }

//                // stack is now [target]
//                Label finishLabel = il.DefineLabel();
//                Label loadNullLabel = il.DefineLabel();
//                int enumDeclareLocal = -1;
//                if (end == null) end = reader.FieldCount;
//                for (int index = start; index < end; index++)
//                {
//                    // 找出对应DataReader中的字段名
//                    string memberName = reader.GetName(index);
//                    if (columns != null)
//                    {
//                        Column column = null;
//                        columns.TryGetValue(memberName, out column);
//                        memberName = column != null ? column.Name : string.Empty;
//                    }

//                    // 本地变量赋值
//                    il.Emit(OpCodes.Ldc_I4, index);             // [target][index]
//                    il.Emit(OpCodes.Stloc_0);                   // [target]
//                    il.Emit(OpCodes.Ldnull);                    // [target][null]
//                    il.Emit(OpCodes.Stloc_2);                   // [target]

//                    // 如果导航属性分割列=DbNull，那么此导航属性赋空值
//                    if (memberName == Constant.NAVIGATIONSPLITONNAME)
//                    {
//                        il.Emit(OpCodes.Ldarg_0);
//                        il.Emit(OpCodes.Ldc_I4, index);
//                        il.Emit(OpCodes.Callvirt, _isDBNull);
//                        il.Emit(OpCodes.Brtrue_S, loadNullLabel);
//                    }

//                    var invoker = typeRuntime.GetInvoker(memberName);
//                    if (invoker == null) continue;

//                    if (specializedConstructor == null) il.Emit(OpCodes.Dup);   // stack is now [target][target]

//                    Label isDbNullLabel = il.DefineLabel();
//                    Label nextLoopLabel = il.DefineLabel();

//                    il.Emit(OpCodes.Ldarg_0);                   // stack is now [target][target][reader]
//                    il.EmitInt32(index);                        // stack is now [target][target][reader][index]                    
//                    il.Emit(OpCodes.Callvirt, _getValue);       // stack is now [target][target][value-as-object]
//                    il.Emit(OpCodes.Dup);                       // stack is now [target][target][value-as-object][value-as-object]
//                    il.Emit(OpCodes.Stloc_2);                   // stack is now [target][target][value-as-object]

//                    Type columnType = reader.GetFieldType(index);
//                    Type memberType = invoker.DataType;

//                    if (memberType == typeof(char) || memberType == typeof(char?))
//                    {
//                        il.EmitCall(OpCodes.Call, memberType == typeof(char) ? _readChar : _readNullChar, null);    // stack is now [target][target][typed-value]
//                    }
//                    else
//                    {
//                        il.Emit(OpCodes.Dup);                       // stack is now [target][target][value][value]
//                        il.Emit(OpCodes.Isinst, typeof(DBNull));    // stack is now [target][target][value-as-object][DBNull or null]
//                        il.Emit(OpCodes.Brtrue_S, isDbNullLabel);   // stack is now [target][target][value-as-object]

//                        // unbox nullable enums as the primitive, i.e. byte etc
//                        var nullUnderlyingType = Nullable.GetUnderlyingType(memberType);
//                        var unboxType = nullUnderlyingType != null && nullUnderlyingType.IsEnum ? nullUnderlyingType : memberType;

//                        if (unboxType.IsEnum)
//                        {
//                            Type numericType = Enum.GetUnderlyingType(unboxType);
//                            if (columnType == typeof(string))
//                            {
//                                if (enumDeclareLocal == -1)
//                                {
//                                    enumDeclareLocal = il.DeclareLocal(typeof(string)).LocalIndex;
//                                }
//                                il.Emit(OpCodes.Castclass, typeof(string));         // stack is now [target][target][string]
//                                il.StoreLocal(enumDeclareLocal);                    // stack is now [target][target]
//                                il.Emit(OpCodes.Ldtoken, unboxType);                // stack is now [target][target][enum-type-token]
//                                il.EmitCall(OpCodes.Call, _typeFromHandle, null);   // stack is now [target][target][enum-type]
//                                il.LoadLocal(enumDeclareLocal);                     // stack is now [target][target][enum-type][string]
//                                il.Emit(OpCodes.Ldc_I4_1);                          // stack is now [target][target][enum-type][string][true]
//                                il.EmitCall(OpCodes.Call, _enumParse, null);        // stack is now [target][target][enum-as-object]
//                                il.Emit(OpCodes.Unbox_Any, unboxType);              // stack is now [target][target][typed-value]
//                            }
//                            else
//                            {
//                                BoxConvert(il, columnType, unboxType, numericType);
//                            }

//                            if (nullUnderlyingType != null)
//                            {
//                                il.Emit(OpCodes.Newobj, memberType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]
//                            }
//                        }
//                        else if (memberType.FullName == _linqBinaryName)
//                        {
//                            il.Emit(OpCodes.Unbox_Any, typeof(byte[])); // stack is now [target][target][byte-array]
//                            il.Emit(OpCodes.Newobj, memberType.GetConstructor(new Type[] { typeof(byte[]) }));      // stack is now [target][target][binary]
//                        }
//                        else
//                        {
//                            TypeCode dataTypeCode = Type.GetTypeCode(columnType), unboxTypeCode = Type.GetTypeCode(unboxType);
//                            if ((columnType == unboxType || dataTypeCode == unboxTypeCode || dataTypeCode == Type.GetTypeCode(nullUnderlyingType)) &&
//                               !((nullUnderlyingType ?? unboxType) == typeof(Guid) && columnType == typeof(byte[])) // Fix# oracle guid
//                                )
//                            {
//                                // il.Emit(OpCodes.Unbox_Any, unboxType);// stack is now [target][target][typed-value]
//                                il.Emit(OpCodes.Unbox_Any, nullUnderlyingType ?? unboxType);    // stack is now [target][target][typed-value]
//                            }
//                            else
//                            {
//                                // not a direct match; need to tweak the unbox
//                                BoxConvert(il, columnType, nullUnderlyingType ?? unboxType, null);
//                            }

//                            if (nullUnderlyingType != null)
//                            {
//                                il.Emit(OpCodes.Newobj, unboxType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]
//                            }
//                        }
//                    }
//                    if (specializedConstructor == null)
//                    {
//                        // Store the value in the property/field
//                        if (invoker.MemberType == MemberTypes.Property)
//                        {
//                            if (type.IsValueType) il.Emit(OpCodes.Call, (invoker as PropertyInvoker).SetMethod);    // stack is now [target]
//                            else il.Emit(OpCodes.Callvirt, (invoker as PropertyInvoker).SetMethod);                 // stack is now [target]
//                        }
//                        else
//                        {
//                            il.Emit(OpCodes.Stfld, invoker.Member as FieldInfo);                                    // stack is now [target]
//                        }
//                    }

//                    il.Emit(OpCodes.Br_S, nextLoopLabel);   // stack is now [target]

//                    il.MarkLabel(isDbNullLabel);            // incoming stack: [target][target][value]
//                    if (specializedConstructor != null)
//                    {
//                        il.Emit(OpCodes.Pop);
//                        if (invoker.DataType.IsValueType)
//                        {
//                            int localIndex = il.DeclareLocal(invoker.DataType).LocalIndex;
//                            il.LoadLocalAddress(localIndex);
//                            il.Emit(OpCodes.Initobj, invoker.DataType);
//                            il.LoadLocal(localIndex);
//                        }
//                        else
//                        {
//                            il.Emit(OpCodes.Ldnull);
//                        }
//                    }
//                    else
//                    {
//                        il.Emit(OpCodes.Pop); // stack is now [target][target]
//                        il.Emit(OpCodes.Pop); // stack is now [target]
//                    }

//                    il.MarkLabel(nextLoopLabel);

//                }
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Pop);
//                }
//                else
//                {
//                    if (specializedConstructor != null)
//                    {
//                        il.Emit(OpCodes.Newobj, specializedConstructor);
//                    }
//                    il.Emit(OpCodes.Stloc_1); // stack is empty
//                }

//                // 直接跳到结束标签返回实体
//                il.Emit(OpCodes.Br, finishLabel);

//                // 将 null 赋值给实体
//                il.MarkLabel(loadNullLabel);
//                il.Emit(OpCodes.Pop);
//                il.Emit(OpCodes.Ldnull);
//                il.Emit(OpCodes.Stloc_1);


//                il.MarkLabel(finishLabel);
//                il.BeginCatchBlock(typeof(Exception));  // stack is Exception
//                il.Emit(OpCodes.Ldloc_0);               // stack is Exception, index
//                il.Emit(OpCodes.Ldloc_2);               // stack is Exception, index, value
//                il.Emit(OpCodes.Ldarg_0);               // stack is Exception, index, reader
//                il.EmitCall(OpCodes.Call, _throwException, null);
//                il.EndExceptionBlock();

//                il.Emit(OpCodes.Ldloc_1); // stack is [rval]
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Box, type);
//                }
//                il.Emit(OpCodes.Ret);

//                return (Func<IDataRecord, object>)method.CreateDelegate(typeof(Func<IDataRecord, object>));
//            }

//            static MethodInfo GetOperator(Type from, Type to)
//            {
//                if (to == null) return null;
//                MethodInfo[] fromMethods, toMethods;
//                return ResolveOperator(fromMethods = from.GetMethods(BindingFlags.Static | BindingFlags.Public), from, to, "op_Implicit")
//                    ?? ResolveOperator(toMethods = to.GetMethods(BindingFlags.Static | BindingFlags.Public), from, to, "op_Implicit")
//                    ?? ResolveOperator(fromMethods, from, to, "op_Explicit")
//                    ?? ResolveOperator(toMethods, from, to, "op_Explicit");

//            }

//            static MethodInfo ResolveOperator(MethodInfo[] methods, Type from, Type to, string name)
//            {
//                for (int i = 0; i < methods.Length; i++)
//                {
//                    if (methods[i].Name != name || methods[i].ReturnType != to) continue;
//                    var args = methods[i].GetParameters();
//                    if (args.Length != 1 || args[0].ParameterType != from) continue;
//                    return methods[i];
//                }
//                return null;
//            }

//            static char ReadChar(object value)
//            {
//                if (value == null || value is DBNull) throw new ArgumentNullException("value");
//                string s = value as string;
//                if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
//                return s[0];
//            }

//            static char? ReadNullableChar(object value)
//            {
//                if (value == null || value is DBNull) return null;
//                string s = value as string;
//                if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
//                return s[0];
//            }

//            // 类型转换
//            static void BoxConvert(ILGenerator il, Type from, Type to, Type via)
//            {
//                MethodInfo op;
//                if (from == (via ?? to))
//                {
//                    il.Emit(OpCodes.Unbox_Any, to); // stack is now [target][target][typed-value]
//                }
//                else if ((op = GetOperator(from, to)) != null)
//                {
//                    // this is handy for things like decimal <===> double
//                    il.Emit(OpCodes.Unbox_Any, from);   // stack is now [target][target][data-typed-value]
//                    il.Emit(OpCodes.Call, op);          // stack is now [target][target][typed-value]
//                }
//                else if (from == typeof(byte[]) && to == typeof(Guid))
//                {
//                    // byte[] => guid
//                    il.Emit(OpCodes.Castclass, typeof(byte[]));
//                    il.Emit(OpCodes.Newobj, _ctorGuid_bytes);
//                }
//                else if (from == typeof(string) && to == typeof(Guid))
//                {
//                    // string => guid
//                    il.Emit(OpCodes.Castclass, typeof(string));
//                    il.Emit(OpCodes.Newobj, _ctorGuid_string);
//                }
//                //else if (from == typeof(string) && to == typeof(System.Data.SqlTypes.SqlXml))
//                //{
//                //    // string => SqlXml
//                //    il.Emit(OpCodes.Castclass, typeof(string));
//                //    il.Emit(OpCodes.Ldc_I4, 9);
//                //    il.Emit(OpCodes.Ldnull);
//                //    il.Emit(OpCodes.Newobj, _ctorXmlReader);
//                //    il.Emit(OpCodes.Newobj, _ctorSqlXml);
//                //}
//                else
//                {
//                    bool handled = false;
//                    OpCode opCode = default(OpCode);
//                    switch (Type.GetTypeCode(from))
//                    {
//                        case TypeCode.Boolean:
//                        case TypeCode.Byte:
//                        case TypeCode.SByte:
//                        case TypeCode.Int16:
//                        case TypeCode.UInt16:
//                        case TypeCode.Int32:
//                        case TypeCode.UInt32:
//                        case TypeCode.Int64:
//                        case TypeCode.UInt64:
//                        case TypeCode.Single:
//                        case TypeCode.Double:
//                            handled = true;
//                            switch (Type.GetTypeCode(via ?? to))
//                            {
//                                case TypeCode.Byte:
//                                    opCode = OpCodes.Conv_Ovf_I1_Un; break;
//                                case TypeCode.SByte:
//                                    opCode = OpCodes.Conv_Ovf_I1; break;
//                                case TypeCode.UInt16:
//                                    opCode = OpCodes.Conv_Ovf_I2_Un; break;
//                                case TypeCode.Int16:
//                                    opCode = OpCodes.Conv_Ovf_I2; break;
//                                case TypeCode.UInt32:
//                                    opCode = OpCodes.Conv_Ovf_I4_Un; break;
//                                case TypeCode.Boolean: // boolean is basically an int, at least at this level
//                                case TypeCode.Int32:
//                                    opCode = OpCodes.Conv_Ovf_I4; break;
//                                case TypeCode.UInt64:
//                                    opCode = OpCodes.Conv_Ovf_I8_Un; break;
//                                case TypeCode.Int64:
//                                    opCode = OpCodes.Conv_Ovf_I8; break;
//                                case TypeCode.Single:
//                                    opCode = OpCodes.Conv_R4; break;
//                                case TypeCode.Double:
//                                    opCode = OpCodes.Conv_R8; break;
//                                default:
//                                    handled = false;
//                                    break;
//                            }
//                            break;
//                    }
//                    if (handled)
//                    {
//                        il.Emit(OpCodes.Unbox_Any, from);                   // stack is now [target][target][col-typed-value]
//                        il.Emit(opCode);                                    // stack is now [target][target][typed-value]
//                        if (to == typeof(bool))
//                        {
//                            // compare to zero; I checked "csc" - this is the trick it uses; nice
//                            il.Emit(OpCodes.Ldc_I4_0);
//                            il.Emit(OpCodes.Ceq);
//                            il.Emit(OpCodes.Ldc_I4_0);
//                            il.Emit(OpCodes.Ceq);
//                        }
//                    }
//                    else
//                    {
//                        il.Emit(OpCodes.Ldtoken, via ?? to);                // stack is now [target][target][value][member-type-token]
//                        il.EmitCall(OpCodes.Call, _typeFromHandle, null);   // stack is now [target][target][value][member-type]
//                        il.EmitCall(OpCodes.Call, _changeType, null);       // stack is now [target][target][boxed-member-type-value]
//                        il.Emit(OpCodes.Unbox_Any, to);                     // stack is now [target][target][typed-value]
//                    }
//                }
//            }

//            static void ThrowDataException(Exception ex, int index, object val, IDataRecord reader)
//            {
//                Exception newException;

//                string name = "(n/a)", value = "(n/a)";
//                if (reader != null && index >= 0 && index < reader.FieldCount) name = reader.GetName(index);
//                if (val == null || val is DBNull)
//                {
//                    value = "<null>";
//                }
//                else
//                {
//                    value = System.Convert.ToString(val) + " - " + Type.GetTypeCode(val.GetType());
//                }

//                newException = new DataException(string.Format("Error parsing column {0} ({1}={2})", index, name, value), ex);
//                throw newException;
//            }
//        }
//    }

//}

//#endif
//#if netcore

//using System;
//using System.Data;
//using System.Linq;
//using System.Text;
//using System.Reflection;
//using System.Reflection.Emit;
//using System.Collections.Generic;

//namespace TZM.XFramework.Data
//{
//    /// <summary>
//    /// 单个实体反序列化
//    /// </summary>
//    internal class TypeDeserializer<T>
//    {
//        private IDataRecord _reader = null;
//        private SelectDbCommandDefinition _definition = null;
//        private IDictionary<string, Func<IDataRecord, object>> _deserializers = null;
//        private Func<IDataRecord, object> _modelDeserializer = null;
//        private Dictionary<string, HashSet<string>> _listNavigationKeys = null;
//        private int? _listNavigationCount = null;

//        /// <summary>
//        /// 实例化<see cref="TypeDeserializer"/> 类的新实例
//        /// </summary>
//        /// <param name="reader">DataReader</param>
//        /// <param name="definition">SQL 命令描述</param>
//        internal TypeDeserializer(IDataReader reader, SelectDbCommandDefinition definition)
//        {
//            _reader = reader;
//            _definition = definition;
//            _deserializers = new Dictionary<string, Func<IDataRecord, object>>(8);
//            _listNavigationKeys = new Dictionary<string, HashSet<string>>(8);
//        }

//        /// <summary>
//        /// 将 <see cref="IDataRecord"/> 上的当前行反序列化为实体
//        /// </summary>
//        /// <param name="prevModel">前一行数据</param>
//        /// <param name="isSameLine">是否同行数据</param>
//        internal T Deserialize(object prevModel, out bool isSameLine)
//        {
//            isSameLine = false;

//            #region 基元类型

//            string name = _reader.GetName(0);
//            if (TypeUtils.IsPrimitiveType(typeof(T)) || name == Constant.AUTOINCREMENTNAME)
//            {
//                if (_reader.IsDBNull(0)) return default(T);

//                var obj = _reader.GetValue(0);
//                if (obj.GetType() != typeof(T))
//                {
//                    // fix#Nullable<T> issue
//                    if (!typeof(T).IsGenericType) obj = Convert.ChangeType(obj, typeof(T));
//                    else
//                    {
//                        Type g = typeof(T).GetGenericTypeDefinition();
//                        if (g != typeof(Nullable<>)) throw new NotSupportedException(string.Format("type {0} not suppored.", g.FullName));
//                        obj = Convert.ChangeType(obj, Nullable.GetUnderlyingType(typeof(T)));
//                    }
//                }

//                return (T)obj;
//            }

//            #endregion

//            #region 匿名类型

//            TypeRuntimeInfo typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo<T>();
//            if (typeRuntime.IsAnonymousType)
//            {
//                // 直接跑SQL,则不解析导航属性
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader, _definition.Columns, 0);
//                return (T)_modelDeserializer(_reader);
//            }

//            #endregion

//            #region 实体类型

//            T model = default(T);
//            if (_definition == null)
//            {
//                // 直接跑SQL,则不解析导航属性
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader);
//                model = (T)_modelDeserializer(_reader);
//            }
//            else if (_definition.Navigations != null && _definition.Navigations.Count == 0)
//            {
//                // 直接跑SQL,则不解析导航属性
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader, _definition.Columns, 0);
//                model = (T)_modelDeserializer(_reader);
//            }
//            else
//            {
//                // 第一层
//                if (_modelDeserializer == null) _modelDeserializer = InternalTypeDeserializer.GetTypeDeserializer(typeof(T), _reader, _definition.Columns, 0, _definition.Navigations.MinIndex);
//                model = (T)_modelDeserializer(_reader);
//                // 若有 1:n 的导航属性，判断当前行数据与上一行数据是否相同
//                if (prevModel != null && _definition.HaveListNavigation)
//                {
//                    isSameLine = true;
//                    foreach (var key in typeRuntime.KeyInvokers)
//                    {
//                        var invoker = key.Value;
//                        var value1 = invoker.Invoke(prevModel);
//                        var value2 = invoker.Invoke(model);
//                        isSameLine = isSameLine && value1.Equals(value2);
//                        if (!isSameLine)
//                        {
//                            // fix issue#换行时清空上一行的导航键缓存
//                            _listNavigationKeys.Clear();
//                            break;
//                        }
//                    }
//                }

//                // 递归导航属性
//                this.Deserialize_Navigation(isSameLine ? prevModel : null, model, string.Empty, isSameLine);
//            }

//            return model;

//            #endregion
//        }

//        // 反序列化导航属性
//        // @prevLine 前一行数据
//        // @isLine   是否同一行数据<同一父级>
//        void Deserialize_Navigation(object prevModel, object model, string typeName, bool isSameLine)
//        {
//            // CRM_SaleOrder.Client 
//            // CRM_SaleOrder.Client.AccountList
//            Type type = model.GetType();
//            TypeRuntimeInfo typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(type);
//            if (string.IsNullOrEmpty(typeName)) typeName = type.Name;

//            foreach (var kvp in _definition.Navigations)
//            {
//                int start = -1;
//                int end = -1;
//                var descriptor = kvp.Value;
//                if (descriptor.FieldCount > 0)
//                {
//                    start = descriptor.Start;
//                    end = descriptor.Start + descriptor.FieldCount;
//                }

//                string keyName = typeName + "." + descriptor.Name;
//                if (keyName != kvp.Key) continue;

//                var navInvoker = typeRuntime.GetInvoker(descriptor.Name);
//                if (navInvoker == null) continue;

//                Type navType = navInvoker.DataType;
//                Func<IDataRecord, object> deserializer = null;
//                TypeRuntimeInfo navTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navType);
//                object navCollection = null;
//                //if (navType.IsGenericType && navTypeRuntime.GenericTypeDefinition == typeof(List<>))
//                if (TypeUtils.IsCollectionType(navType))
//                {
//                    // 1：n关系，导航属性为 List<T>
//                    navCollection = navInvoker.Invoke(model);
//                    if (navCollection == null)
//                    {
//                        // new 一个列表类型，如果导航属性定义为接口，则默认使用List<T>来实例化
//                        TypeRuntimeInfo navTypeRuntime2 = navType.IsInterface
//                            ? TypeRuntimeInfoCache.GetRuntimeInfo(typeof(List<>).MakeGenericType(navTypeRuntime.GenericArguments[0]))
//                            : navTypeRuntime;
//                        navCollection = navTypeRuntime2.ConstructInvoker.Invoke();
//                        navInvoker.Invoke(model, navCollection);
//                    }
//                }

//                if (!_deserializers.TryGetValue(keyName, out deserializer))
//                {
//                    deserializer = InternalTypeDeserializer.GetTypeDeserializer(navType.IsGenericType ? navTypeRuntime.GenericArguments[0] : navType, _reader, _definition.Columns, start, end);
//                    _deserializers[keyName] = deserializer;
//                }

//                // 如果整个导航链中某一个导航属性为空，则跳出递归
//                object navModel = deserializer(_reader);
//                if (navModel != null)
//                {
//                    if (navCollection == null)
//                    {
//                        // 非集合型导航属性
//                        navInvoker.Invoke(model, navModel);
//                        //
//                        //
//                        // 
//                    }
//                    else
//                    {
//                        // 集合型导航属性
//                        if (prevModel != null && isSameLine)
//                        {
//                            #region 合并列表

//                            // 判断如果属于同一个主表，则合并到上一行的当前明细列表
//                            // 例：CRM_SaleOrder.Client.AccountList
//                            string[] keys = keyName.Split('.');
//                            TypeRuntimeInfo curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo<T>();
//                            Type curType = curTypeRuntime.Type;
//                            MemberInvokerBase curInvoker = null;
//                            object curModel = prevModel;

//                            for (int i = 1; i < keys.Length; i++)
//                            {
//                                curInvoker = curTypeRuntime.GetInvoker(keys[i]);
//                                curModel = curInvoker.Invoke(curModel);
//                                if (curModel == null) continue;

//                                curType = curModel.GetType();
//                                curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(curType);

//                                // <<<<<<<<<<< 一对多对多关系 >>>>>>>>>>
//                                if (curType.IsGenericType && i != keys.Length - 1)
//                                {
//                                    curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(curType);
//                                    //if (curTypeRuntime.GenericTypeDefinition == typeof(List<>))
//                                    if (TypeUtils.IsCollectionType(curType))
//                                    {
//                                        var invoker = curTypeRuntime.GetInvoker("get_Count");
//                                        int count = Convert.ToInt32(invoker.Invoke(curModel));      // List.Count
//                                        if (count > 0)
//                                        {
//                                            var invoker2 = curTypeRuntime.GetInvoker("get_Item");
//                                            curModel = invoker2.Invoke(curModel, count - 1);        // List[List.Count-1]
//                                            curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(curModel.GetType());
//                                        }
//                                        else
//                                        {
//                                            // user.Roles.RoleFuncs=>Roles 列表有可能为空
//                                            curModel = null;
//                                            break;
//                                        }
//                                    }
//                                }
//                            }


//                            if (curModel != null)
//                            {
//                                // 如果有两个以上的一对多关系的导航属性，那么在加入列表之前就需要剔除重复的实体


//                                bool isAny = false;
//                                if (_definition.Navigations.Count > 1)
//                                {
//                                    if (_listNavigationCount == null) _listNavigationCount = _definition.Navigations.Count(x => CheckCollectionNavigation(x.Value.Member));
//                                    if (_listNavigationCount != null && _listNavigationCount.Value > 1)
//                                    {
//                                        if (!_listNavigationKeys.ContainsKey(keyName)) _listNavigationKeys[keyName] = new HashSet<string>();
//                                        curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navModel.GetType());
//                                        StringBuilder keyBuilder = new StringBuilder(64);

//                                        foreach (var key in curTypeRuntime.KeyInvokers)
//                                        {
//                                            var invoker = key.Value;
//                                            var value = invoker.Invoke(navModel);
//                                            keyBuilder.AppendFormat("{0}={1};", key.Key, (value ?? string.Empty).ToString());
//                                        }
//                                        string hash = keyBuilder.ToString();
//                                        if (_listNavigationKeys[keyName].Contains(hash))
//                                        {
//                                            isAny = true;
//                                        }
//                                        else
//                                        {
//                                            _listNavigationKeys[keyName].Add(hash);
//                                        }
//                                    }
//                                }

//                                if (!isAny)
//                                {
//                                    // 如果列表中不存在，则添加到上一行的相同导航列表中去
//                                    var myAddMethod = navTypeRuntime.GetInvoker("Add");
//                                    myAddMethod.Invoke(curModel, navModel);
//                                }
//                            }

//                            #endregion
//                        }
//                        else
//                        {
//                            // 此时的 navTypeRuntime 是 List<> 类型的运行时
//                            // 先添加 List 列表
//                            var myAddMethod = navTypeRuntime.GetInvoker("Add");
//                            myAddMethod.Invoke(navCollection, navModel);

//                            var curTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navModel.GetType());
//                            StringBuilder keyBuilder = new StringBuilder(64);

//                            foreach (var key in curTypeRuntime.KeyInvokers)
//                            {
//                                var wrapper = key.Value;
//                                var value = wrapper.Invoke(navModel);
//                                keyBuilder.AppendFormat("{0}={1};", key.Key, (value ?? string.Empty).ToString());
//                            }
//                            string hash = keyBuilder.ToString();
//                            if (!_listNavigationKeys.ContainsKey(keyName)) _listNavigationKeys[keyName] = new HashSet<string>();
//                            if (!_listNavigationKeys[keyName].Contains(hash)) _listNavigationKeys[keyName].Add(hash);
//                        }
//                    }

//                    //if (navTypeRuntime.GenericTypeDefinition == typeof(List<>)) navTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navTypeRuntime.GenericArguments[0]);
//                    if (TypeUtils.IsCollectionType(navTypeRuntime.Type)) navTypeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(navTypeRuntime.GenericArguments[0]);
//                    if (navTypeRuntime.NavInvokers.Count > 0) Deserialize_Navigation(prevModel, navModel, keyName, isSameLine);
//                }
//            }
//        }

//        // 检查当前成员是否是1：N关系的导航属性
//        static bool CheckCollectionNavigation(MemberInfo info)
//        {
//            if (info == null) return false;

//            // 仅支持属性和字段***
//            Type type = null;
//            if (info.MemberType == MemberTypes.Property) type = ((PropertyInfo)info).PropertyType;
//            else if (info.MemberType == MemberTypes.Field) type = ((FieldInfo)info).FieldType;
//            return type == null ? false : TypeUtils.IsCollectionType(type);
//        }

//        /// <summary>
//        /// 内部类型反序列化器
//        /// </summary>
//        internal class InternalTypeDeserializer
//        {
//            // 这个类的代码在 Dapper.NET 的基础上修改的，实体映射确实强悍得一匹
//            // https://github.com/StackExchange/Dapper

//            const string _linqBinaryName = "System.Data.Linq.Binary";
//            static readonly MethodInfo _enumParse = typeof(Enum).GetMethod("Parse", new Type[] { typeof(Type), typeof(string), typeof(bool) });
//            static readonly MethodInfo _getValue = typeof(IDataRecord).GetMethod("GetValue", new Type[] { typeof(int) });
//            static MethodInfo _isDBNull = typeof(IDataRecord).GetMethod("IsDBNull", new Type[] { typeof(int) });
//            static MethodInfo _readChar = typeof(InternalTypeDeserializer).GetMethod("ReadChar", BindingFlags.Static | BindingFlags.NonPublic);
//            static MethodInfo _readNullChar = typeof(InternalTypeDeserializer).GetMethod("ReadNullableChar", BindingFlags.Static | BindingFlags.NonPublic);
//            static MethodInfo _throwException = typeof(InternalTypeDeserializer).GetMethod("ThrowDataException", BindingFlags.Static | BindingFlags.NonPublic);
//            static MethodInfo _typeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
//            static MethodInfo _changeType = typeof(Convert).GetMethod("ChangeType", new Type[] { typeof(object), typeof(Type) });

//            static ConstructorInfo _ctorGuid_string = typeof(Guid).GetConstructor(new[] { typeof(string) });
//            static ConstructorInfo _ctorGuid_bytes = typeof(Guid).GetConstructor(new[] { typeof(byte[]) });
//            //static ConstructorInfo _ctorXmlReader = typeof(XmlTextReader).GetConstructor(new[] { typeof(string), typeof(XmlNodeType), typeof(XmlParserContext) });
//            //static ConstructorInfo _ctorSqlXml = typeof(System.Data.SqlTypes.SqlXml).GetConstructor(new[] { typeof(System.Xml.XmlTextReader) });

//            internal static Func<IDataRecord, object> GetTypeDeserializer(Type type, IDataRecord reader, IDictionary<string, Column> columns = null, int start = 0, int? end = null)
//            {
//                TypeRuntimeInfo typeRuntime = TypeRuntimeInfoCache.GetRuntimeInfo(type);
//                DynamicMethod method = new DynamicMethod(string.Format("Deserialize{0}", Guid.NewGuid()), typeof(object), new Type[] { typeof(IDataRecord) }, true);
//                ILGenerator il = method.GetILGenerator();

//                il.DeclareLocal(typeof(int));
//                il.DeclareLocal(type);
//                il.DeclareLocal(typeof(object));

//                il.Emit(OpCodes.Ldc_I4_0);
//                il.Emit(OpCodes.Stloc_0);
//                il.Emit(OpCodes.Ldnull);
//                il.Emit(OpCodes.Stloc_2);

//                ConstructorInfo specializedConstructor = null;
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Ldloca_S, (byte)1);
//                    il.Emit(OpCodes.Initobj, type);
//                }
//                else
//                {
//                    var ctor = typeRuntime.ConstructInvoker.Constructor;
//                    if (ctor.GetParameters().Length == 0)
//                    {
//                        il.Emit(OpCodes.Newobj, ctor);
//                        il.Emit(OpCodes.Stloc_1);
//                    }
//                    else
//                        specializedConstructor = ctor;
//                }

//                // try #####
//                il.BeginExceptionBlock();
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Ldloca_S, (byte)1);         // [target]
//                }
//                else if (specializedConstructor == null)
//                {
//                    il.Emit(OpCodes.Ldloc_1);                   // [target]
//                }

//                // stack is now [target]
//                Label finishLabel = il.DefineLabel();
//                Label loadNullLabel = il.DefineLabel();
//                int enumDeclareLocal = -1;
//                if (end == null) end = reader.FieldCount;
//                for (int index = start; index < end; index++)
//                {
//                    // 找出对应DataReader中的字段名
//                    string memberName = reader.GetName(index);
//                    if (columns != null)
//                    {
//                        Column column = null;
//                        columns.TryGetValue(memberName, out column);
//                        memberName = column != null ? column.Name : string.Empty;
//                    }

//                    // 本地变量赋值
//                    il.Emit(OpCodes.Ldc_I4, index);             // [target][index]
//                    il.Emit(OpCodes.Stloc_0);                   // [target]
//                    il.Emit(OpCodes.Ldnull);                    // [target][null]
//                    il.Emit(OpCodes.Stloc_2);                   // [target]

//                    // 如果导航属性分割列=DbNull，那么此导航属性赋空值
//                    if (memberName == Constant.NAVIGATIONSPLITONNAME)
//                    {
//                        il.Emit(OpCodes.Ldarg_0);
//                        il.Emit(OpCodes.Ldc_I4, index);
//                        il.Emit(OpCodes.Callvirt, _isDBNull);
//                        il.Emit(OpCodes.Brtrue_S, loadNullLabel);
//                    }

//                    var invoker = typeRuntime.GetInvoker(memberName);
//                    if (invoker == null) continue;

//                    if (specializedConstructor == null) il.Emit(OpCodes.Dup);   // stack is now [target][target]

//                    Label isDbNullLabel = il.DefineLabel();
//                    Label nextLoopLabel = il.DefineLabel();

//                    il.Emit(OpCodes.Ldarg_0);                   // stack is now [target][target][reader]
//                    il.EmitInt32(index);                        // stack is now [target][target][reader][index]                    
//                    il.Emit(OpCodes.Callvirt, _getValue);       // stack is now [target][target][value-as-object]
//                    il.Emit(OpCodes.Dup);                       // stack is now [target][target][value-as-object][value-as-object]
//                    il.Emit(OpCodes.Stloc_2);                   // stack is now [target][target][value-as-object]

//                    Type columnType = reader.GetFieldType(index);
//                    Type memberType = invoker.DataType;

//                    if (memberType == typeof(char) || memberType == typeof(char?))
//                    {
//                        il.EmitCall(OpCodes.Call, memberType == typeof(char) ? _readChar : _readNullChar, null);    // stack is now [target][target][typed-value]
//                    }
//                    else
//                    {
//                        il.Emit(OpCodes.Dup);                       // stack is now [target][target][value][value]
//                        il.Emit(OpCodes.Isinst, typeof(DBNull));    // stack is now [target][target][value-as-object][DBNull or null]
//                        il.Emit(OpCodes.Brtrue_S, isDbNullLabel);   // stack is now [target][target][value-as-object]

//                        // unbox nullable enums as the primitive, i.e. byte etc
//                        var nullUnderlyingType = Nullable.GetUnderlyingType(memberType);
//                        var unboxType = nullUnderlyingType != null && nullUnderlyingType.IsEnum ? nullUnderlyingType : memberType;

//                        if (unboxType.IsEnum)
//                        {
//                            Type numericType = Enum.GetUnderlyingType(unboxType);
//                            if (columnType == typeof(string))
//                            {
//                                if (enumDeclareLocal == -1)
//                                {
//                                    enumDeclareLocal = il.DeclareLocal(typeof(string)).LocalIndex;
//                                }
//                                il.Emit(OpCodes.Castclass, typeof(string));         // stack is now [target][target][string]
//                                il.StoreLocal(enumDeclareLocal);                    // stack is now [target][target]
//                                il.Emit(OpCodes.Ldtoken, unboxType);                // stack is now [target][target][enum-type-token]
//                                il.EmitCall(OpCodes.Call, _typeFromHandle, null);   // stack is now [target][target][enum-type]
//                                il.LoadLocal(enumDeclareLocal);                     // stack is now [target][target][enum-type][string]
//                                il.Emit(OpCodes.Ldc_I4_1);                          // stack is now [target][target][enum-type][string][true]
//                                il.EmitCall(OpCodes.Call, _enumParse, null);        // stack is now [target][target][enum-as-object]
//                                il.Emit(OpCodes.Unbox_Any, unboxType);              // stack is now [target][target][typed-value]
//                            }
//                            else
//                            {
//                                BoxConvert(il, columnType, unboxType, numericType);
//                            }

//                            if (nullUnderlyingType != null)
//                            {
//                                il.Emit(OpCodes.Newobj, memberType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]
//                            }
//                        }
//                        else if (memberType.FullName == _linqBinaryName)
//                        {
//                            il.Emit(OpCodes.Unbox_Any, typeof(byte[])); // stack is now [target][target][byte-array]
//                            il.Emit(OpCodes.Newobj, memberType.GetConstructor(new Type[] { typeof(byte[]) }));      // stack is now [target][target][binary]
//                        }
//                        else
//                        {
//                            TypeCode dataTypeCode = Type.GetTypeCode(columnType), unboxTypeCode = Type.GetTypeCode(unboxType);
//                            if ((columnType == unboxType || dataTypeCode == unboxTypeCode || dataTypeCode == Type.GetTypeCode(nullUnderlyingType)) &&
//                               !((nullUnderlyingType ?? unboxType) == typeof(Guid) && columnType == typeof(byte[])) // Fix# oracle guid
//                                )
//                            {
//                                // il.Emit(OpCodes.Unbox_Any, unboxType);// stack is now [target][target][typed-value]
//                                il.Emit(OpCodes.Unbox_Any, nullUnderlyingType ?? unboxType);    // stack is now [target][target][typed-value]
//                            }
//                            else
//                            {
//                                // not a direct match; need to tweak the unbox
//                                BoxConvert(il, columnType, nullUnderlyingType ?? unboxType, null);
//                            }

//                            if (nullUnderlyingType != null)
//                            {
//                                il.Emit(OpCodes.Newobj, unboxType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]
//                            }
//                        }
//                    }
//                    if (specializedConstructor == null)
//                    {
//                        // Store the value in the property/field
//                        if (invoker.MemberType == MemberTypes.Property)
//                        {
//                            if (type.IsValueType) il.Emit(OpCodes.Call, (invoker as PropertyInvoker).SetMethod);    // stack is now [target]
//                            else il.Emit(OpCodes.Callvirt, (invoker as PropertyInvoker).SetMethod);                 // stack is now [target]
//                        }
//                        else
//                        {
//                            il.Emit(OpCodes.Stfld, invoker.Member as FieldInfo);                                    // stack is now [target]
//                        }
//                    }

//                    il.Emit(OpCodes.Br_S, nextLoopLabel);   // stack is now [target]

//                    il.MarkLabel(isDbNullLabel);            // incoming stack: [target][target][value]
//                    if (specializedConstructor != null)
//                    {
//                        il.Emit(OpCodes.Pop);
//                        if (invoker.DataType.IsValueType)
//                        {
//                            int localIndex = il.DeclareLocal(invoker.DataType).LocalIndex;
//                            il.LoadLocalAddress(localIndex);
//                            il.Emit(OpCodes.Initobj, invoker.DataType);
//                            il.LoadLocal(localIndex);
//                        }
//                        else
//                        {
//                            il.Emit(OpCodes.Ldnull);
//                        }
//                    }
//                    else
//                    {
//                        il.Emit(OpCodes.Pop); // stack is now [target][target]
//                        il.Emit(OpCodes.Pop); // stack is now [target]
//                    }

//                    il.MarkLabel(nextLoopLabel);

//                }
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Pop);
//                }
//                else
//                {
//                    if (specializedConstructor != null)
//                    {
//                        il.Emit(OpCodes.Newobj, specializedConstructor);
//                    }
//                    il.Emit(OpCodes.Stloc_1); // stack is empty
//                }

//                // 直接跳到结束标签返回实体
//                il.Emit(OpCodes.Br, finishLabel);

//                // 将 null 赋值给实体
//                il.MarkLabel(loadNullLabel);
//                il.Emit(OpCodes.Pop);
//                il.Emit(OpCodes.Ldnull);
//                il.Emit(OpCodes.Stloc_1);


//                il.MarkLabel(finishLabel);
//                il.BeginCatchBlock(typeof(Exception));  // stack is Exception
//                il.Emit(OpCodes.Ldloc_0);               // stack is Exception, index
//                il.Emit(OpCodes.Ldloc_2);               // stack is Exception, index, value
//                il.Emit(OpCodes.Ldarg_0);               // stack is Exception, index, reader
//                il.EmitCall(OpCodes.Call, _throwException, null);
//                il.EndExceptionBlock();

//                il.Emit(OpCodes.Ldloc_1); // stack is [rval]
//                if (type.IsValueType)
//                {
//                    il.Emit(OpCodes.Box, type);
//                }
//                il.Emit(OpCodes.Ret);

//                return (Func<IDataRecord, object>)method.CreateDelegate(typeof(Func<IDataRecord, object>));
//            }

//            static MethodInfo GetOperator(Type from, Type to)
//            {
//                if (to == null) return null;
//                MethodInfo[] fromMethods, toMethods;
//                return ResolveOperator(fromMethods = from.GetMethods(BindingFlags.Static | BindingFlags.Public), from, to, "op_Implicit")
//                    ?? ResolveOperator(toMethods = to.GetMethods(BindingFlags.Static | BindingFlags.Public), from, to, "op_Implicit")
//                    ?? ResolveOperator(fromMethods, from, to, "op_Explicit")
//                    ?? ResolveOperator(toMethods, from, to, "op_Explicit");

//            }

//            static MethodInfo ResolveOperator(MethodInfo[] methods, Type from, Type to, string name)
//            {
//                for (int i = 0; i < methods.Length; i++)
//                {
//                    if (methods[i].Name != name || methods[i].ReturnType != to) continue;
//                    var args = methods[i].GetParameters();
//                    if (args.Length != 1 || args[0].ParameterType != from) continue;
//                    return methods[i];
//                }
//                return null;
//            }

//            static char ReadChar(object value)
//            {
//                if (value == null || value is DBNull) throw new ArgumentNullException("value");
//                string s = value as string;
//                if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
//                return s[0];
//            }

//            static char? ReadNullableChar(object value)
//            {
//                if (value == null || value is DBNull) return null;
//                string s = value as string;
//                if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
//                return s[0];
//            }

//            // 类型转换
//            static void BoxConvert(ILGenerator il, Type from, Type to, Type via)
//            {
//                MethodInfo op;
//                if (from == (via ?? to))
//                {
//                    il.Emit(OpCodes.Unbox_Any, to); // stack is now [target][target][typed-value]
//                }
//                else if ((op = GetOperator(from, to)) != null)
//                {
//                    // this is handy for things like decimal <===> double
//                    il.Emit(OpCodes.Unbox_Any, from);   // stack is now [target][target][data-typed-value]
//                    il.Emit(OpCodes.Call, op);          // stack is now [target][target][typed-value]
//                }
//                else if (from == typeof(byte[]) && to == typeof(Guid))
//                {
//                    // byte[] => guid
//                    il.Emit(OpCodes.Castclass, typeof(byte[]));
//                    il.Emit(OpCodes.Newobj, _ctorGuid_bytes);
//                }
//                else if (from == typeof(string) && to == typeof(Guid))
//                {
//                    // string => guid
//                    il.Emit(OpCodes.Castclass, typeof(string));
//                    il.Emit(OpCodes.Newobj, _ctorGuid_string);
//                }
//                //else if (from == typeof(string) && to == typeof(System.Data.SqlTypes.SqlXml))
//                //{
//                //    // string => SqlXml
//                //    il.Emit(OpCodes.Castclass, typeof(string));
//                //    il.Emit(OpCodes.Ldc_I4, 9);
//                //    il.Emit(OpCodes.Ldnull);
//                //    il.Emit(OpCodes.Newobj, _ctorXmlReader);
//                //    il.Emit(OpCodes.Newobj, _ctorSqlXml);
//                //}
//                else
//                {
//                    bool handled = false;
//                    OpCode opCode = default(OpCode);
//                    switch (Type.GetTypeCode(from))
//                    {
//                        case TypeCode.Boolean:
//                        case TypeCode.Byte:
//                        case TypeCode.SByte:
//                        case TypeCode.Int16:
//                        case TypeCode.UInt16:
//                        case TypeCode.Int32:
//                        case TypeCode.UInt32:
//                        case TypeCode.Int64:
//                        case TypeCode.UInt64:
//                        case TypeCode.Single:
//                        case TypeCode.Double:
//                            handled = true;
//                            switch (Type.GetTypeCode(via ?? to))
//                            {
//                                case TypeCode.Byte:
//                                    opCode = OpCodes.Conv_Ovf_I1_Un; break;
//                                case TypeCode.SByte:
//                                    opCode = OpCodes.Conv_Ovf_I1; break;
//                                case TypeCode.UInt16:
//                                    opCode = OpCodes.Conv_Ovf_I2_Un; break;
//                                case TypeCode.Int16:
//                                    opCode = OpCodes.Conv_Ovf_I2; break;
//                                case TypeCode.UInt32:
//                                    opCode = OpCodes.Conv_Ovf_I4_Un; break;
//                                case TypeCode.Boolean: // boolean is basically an int, at least at this level
//                                case TypeCode.Int32:
//                                    opCode = OpCodes.Conv_Ovf_I4; break;
//                                case TypeCode.UInt64:
//                                    opCode = OpCodes.Conv_Ovf_I8_Un; break;
//                                case TypeCode.Int64:
//                                    opCode = OpCodes.Conv_Ovf_I8; break;
//                                case TypeCode.Single:
//                                    opCode = OpCodes.Conv_R4; break;
//                                case TypeCode.Double:
//                                    opCode = OpCodes.Conv_R8; break;
//                                default:
//                                    handled = false;
//                                    break;
//                            }
//                            break;
//                    }
//                    if (handled)
//                    {
//                        il.Emit(OpCodes.Unbox_Any, from);                   // stack is now [target][target][col-typed-value]
//                        il.Emit(opCode);                                    // stack is now [target][target][typed-value]
//                        if (to == typeof(bool))
//                        {
//                            // compare to zero; I checked "csc" - this is the trick it uses; nice
//                            il.Emit(OpCodes.Ldc_I4_0);
//                            il.Emit(OpCodes.Ceq);
//                            il.Emit(OpCodes.Ldc_I4_0);
//                            il.Emit(OpCodes.Ceq);
//                        }
//                    }
//                    else
//                    {
//                        il.Emit(OpCodes.Ldtoken, via ?? to);                // stack is now [target][target][value][member-type-token]
//                        il.EmitCall(OpCodes.Call, _typeFromHandle, null);   // stack is now [target][target][value][member-type]
//                        il.EmitCall(OpCodes.Call, _changeType, null);       // stack is now [target][target][boxed-member-type-value]
//                        il.Emit(OpCodes.Unbox_Any, to);                     // stack is now [target][target][typed-value]
//                    }
//                }
//            }

//            static void ThrowDataException(Exception ex, int index, object val, IDataRecord reader)
//            {
//                Exception newException;

//                string name = "(n/a)", value = "(n/a)";
//                if (reader != null && index >= 0 && index < reader.FieldCount) name = reader.GetName(index);
//                if (val == null || val is DBNull)
//                {
//                    value = "<null>";
//                }
//                else
//                {
//                    value = System.Convert.ToString(val) + " - " + Type.GetTypeCode(val.GetType());
//                }

//                newException = new DataException(string.Format("Error parsing column {0} ({1}={2})", index, name, value), ex);
//                throw newException;
//            }
//        }
//    }

//}

//#endif