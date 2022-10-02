using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace W3.TypeExtension
{
    using System;
    using System.Reflection;
    using System.Reflection.Emit;
    using ILUtility;
    using System.Runtime.Serialization;

    public static class TypeExtension
    {
        // TODO.. 参照一下Odin
        private static List<Type> BASIC_TYPE_LIST = new List<Type>()
        {
            typeof(float),
            typeof(double),
            typeof(sbyte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(byte),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(decimal),
            typeof(char),
            typeof(bool),
        };

        /// <summary>
        /// 返回是否是基本类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsBasicType(this Type type)
        {
            return BASIC_TYPE_LIST.Contains(type);
        }

        /// <summary>
        /// 返回是否是浮点数类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsFloatType(this Type type) 
        {
            return type == typeof(float) || type == typeof(double);
        }

        /// <summary>
        /// 返回是否是UnityObject的子类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsUnityObjectType(this Type type) 
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        public static bool IsUnityType(this Type type) 
        {
            return type.Assembly.GetName().Name == "UnityEngine.CoreModule";
        }

        public static bool IsList(this Type type) 
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        /// <summary>
        /// 返回是否是数组类型。
        /// 注意：最好以后改成Odin的ImplementsOpenGenericInterface方法；否则一个实现了List<T>的类型会返回false；
        /// 另外这个方法不支持高维数组
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsListOrArray(this Type type) 
        {
            return type.IsList() || type.IsArray;
        }

        public static bool IsStructClass(this Type type) 
        {
            return type.IsValueType && !type.IsBasicType();
        }

        /// <summary>
        /// 返回 这个类型参数为 T Equals(T) 的方法
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static MethodInfo GetCurTypeEqualsMethodInfo(this Type type)
        {
            // 注意：这里需要加上 DeclaredOnly 来保证只在本类中找
            // 不知道为什么，如果这里不加会自动去找父类，但是 "op_Equality" 不加默认不会去找父类，可能是静态或者重载操作符的关系？
            return type.GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new Type[]{type}, null);
        }

        /// <summary>
        /// 返回当前Type "op_Equality" 方法，如果本类中没有，去父类中找
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static MethodInfo GetCurTypeOpEqualMethodInfoIncludeParent(this Type type) 
        {
            // 注意：这里 BindingFlags 不能加 DeclaredOnly，否则只会找本类
            // 同时，需要 FlattenHierarchy 去找父类的方法
            return type.GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy, null, new Type[]{type, type}, null);
        }

        /// <summary>
        /// 返回一个数组类型的元素类型。
        /// 注意：可能并不支持高维数组。
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetListElementType(this Type type) 
        {
            if(!type.IsListOrArray()) 
            {
                Debug.LogErrorFormat(" GetListElementType 中传入了一个不是数组类型的type：{0}", type);
            }
            else 
            {
                if(type.IsArray) 
                {
                    return type.GetElementType();
                }
                else 
                {
                    foreach (var item in type.GetGenericArguments())
                    {
                        return item;
                    }
                }
            }

            return null;
        }
    }

    public static class TypeUtility
    {
        private struct ILCtxItem 
        {
            public OpCode opCodes;
            public FieldInfo fi;
            public MethodInfo mi, miex;
            public int varID0, varID1;
        }
        private static Dictionary<Type, object> m_mapTypeCmpCache = new Dictionary<Type, object>();
        /// <summary>
        /// 返回一个类型的深比较器，可以自动递归比较public的字段。
        /// 注意：暂不支持 List<List<T>>, T[][], Dictionary 类型。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Func<T, T, bool> GetTypeCmp<T>()
        {
            object cmpObj = null;
            Type type = typeof(T);
            if (m_mapTypeCmpCache.TryGetValue(type, out cmpObj)) 
            {
                return cmpObj as Func<T, T, bool>;
            }

            var dm = new DynamicMethod("", typeof(bool), new Type[]{type, type});
            var il = dm.GetILGenerator();
            Label lbFalse = il.DefineLabel();
            Label lbRet = il.DefineLabel();
            int localVarInt = 0; // 记录局部变量使用的id
            var idForAns = localVarInt++;
            LocalBuilder localBool = il.DeclareLocal(typeof(bool)); // For ans

            /// <summary>
            /// 加载参数0
            /// </summary>
            void LoadParm0()
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            /// <summary>
            /// 加载参数1
            /// </summary>
            void LoadParm1()
            {
                il.Emit(OpCodes.Ldarg_1);
            }
            /// <summary>
            /// 递归加载参数0
            /// </summary>
            /// <param name="ilCtxList"></param>
            void RecursiveLoadParm0(List<ILCtxItem> ilCtxList)
            {
                if(ilCtxList != null) 
                {
                    int lastLoadLocInt = -1;
                    for(int i=ilCtxList.Count-1;i>=0;i--) 
                    {
                        if(ilCtxList[i].opCodes == OpCodes.Ldloc) 
                        {
                            lastLoadLocInt = i;
                            break;
                        }
                    }
                    if(lastLoadLocInt == -1) 
                    {
                        LoadParm0();
                        foreach (var item in ilCtxList)
                        {
                            if(item.opCodes == OpCodes.Ldfld) 
                            {
                                // Debug.Log("Ldfld, " + item.fi.Name);
                                il.Emit(OpCodes.Ldfld, item.fi); 
                            }
                            else 
                            {
                                Debug.LogErrorFormat("RecursiveLoadParm0 上下文item中混入了 无法解析的OpCodes：{0}", item.opCodes);
                            }
                        }
                    }
                    else 
                    {
                        for(int i=lastLoadLocInt;i<ilCtxList.Count;i++)
                        {
                            var item = ilCtxList[i];
                            if(item.opCodes == OpCodes.Ldfld) 
                            {
                                il.Emit(OpCodes.Ldfld, item.fi); 
                            }
                            else if(item.opCodes == OpCodes.Ldloc) 
                            {
                                il.Emit(item.opCodes, item.varID0);
                            }
                            else 
                            {
                                Debug.LogErrorFormat("RecursiveLoadParm0 上下文item中混入了 无法解析的OpCodes：{0}", item.opCodes);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// 递归加载参数1
            /// </summary>
            /// <param name="ilCtxList"></param>
            void RecursiveLoadParm1(List<ILCtxItem> ilCtxList)
            {
                if(ilCtxList != null) 
                {
                    // 加载参数1，并获取对应field
                    int lastLoadLocInt = -1;
                    for(int i=ilCtxList.Count-1;i>=0;i--) 
                    {
                        if(ilCtxList[i].opCodes == OpCodes.Ldloc) 
                        {
                            lastLoadLocInt = i;
                            break;
                        }
                    }
                    if(lastLoadLocInt == -1) 
                    {
                        LoadParm1();
                        foreach (var item in ilCtxList)
                        {
                            if(item.opCodes == OpCodes.Ldfld) 
                            {
                                il.Emit(OpCodes.Ldfld, item.fi); 
                            }
                            else 
                            {
                                Debug.LogErrorFormat("RecursiveLoadParm1 上下文item中混入了 无法解析的OpCodes：{0}", item.opCodes);
                            }
                        }
                    }
                    else 
                    {
                        for(int i=lastLoadLocInt;i<ilCtxList.Count;i++)
                        {
                            var item = ilCtxList[i];
                            if(item.opCodes == OpCodes.Ldfld) 
                            {
                                il.Emit(OpCodes.Ldfld, item.fi); 
                            }
                            else if(item.opCodes == OpCodes.Ldloc) 
                            {
                                il.Emit(item.opCodes, item.varID1);
                            }
                            else 
                            {
                                Debug.LogErrorFormat("RecursiveLoadParm1 上下文item中混入了 无法解析的OpCodes：{0}", item.opCodes);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// 比较某一个field，这个field是基本类型的
            /// </summary>
            void GenerateBasicType(List<ILCtxItem> ilCtxList)
            {
                // 加载参数0，并获取对应field
                RecursiveLoadParm0(ilCtxList);
                // 加载参数1，并获取对应field
                RecursiveLoadParm1(ilCtxList);
                // 作比较
                il.Emit(OpCodes.Ceq);
                // 如果不同，则跳到lbFalse
                il.Emit(OpCodes.Brfalse, lbFalse);
            }
            /// <summary>
            /// 比较某一个field，这个field中包含或者父类中包含 op_Equality
            /// </summary>
            /// <param name="nowType"></param>
            /// <param name="ilCtxList"></param>
            void GenerateHaveOpEqualType(Type nowType, List<ILCtxItem> ilCtxList)
            {
                // 加载参数0，并获取对应field
                RecursiveLoadParm0(ilCtxList);
                // 加载参数1，并获取对应field
                RecursiveLoadParm1(ilCtxList);
                // 获取比较函数
                var opMethod = nowType.GetCurTypeOpEqualMethodInfoIncludeParent();
                il.Emit(OpCodes.Call, opMethod);
                // 比较结果和true作比较
                il.Emit(OpCodes.Ldc_I4_1);
                // 作比较
                il.Emit(OpCodes.Ceq);
                // 如果不同，则跳到lbFalse
                il.Emit(OpCodes.Brfalse, lbFalse);
            }
            /// <summary>
            /// 比较某一个field，这个field中重写了 T Equals(T)
            /// </summary>
            /// <param name="nowType"></param>
            /// <param name="ilCtxList"></param>
            void GenerateCanEqualType(Type nowType, List<ILCtxItem> ilCtxList)
            {
                // 加载参数0，并获取对应field
                RecursiveLoadParm0(ilCtxList);
                // 加载参数1，并获取对应field
                RecursiveLoadParm1(ilCtxList);
                // 获取比较函数
                var opMethod = nowType.GetCurTypeEqualsMethodInfo();
                il.Emit(OpCodes.Callvirt, opMethod);
                // 比较结果和true作比较
                il.Emit(OpCodes.Ldc_I4_1);
                // 作比较
                il.Emit(OpCodes.Ceq);
                // 如果不同，则跳到lbFalse
                il.Emit(OpCodes.Brfalse, lbFalse);
            }

            /// <summary>
            /// 比较一个class类型的field
            /// </summary>
            void GenerateClass(Type nowType, List<ILCtxItem> ilCtxList) 
            {
                // 标签
                var endLabel = il.DefineLabel();
                // 变量
                // ...
                // 先检查是否class是否有null的
                if(!nowType.IsStructClass())
                {
                    il.GenIfThenElse(
                        // if
                        () =>
                        {
                            il.CompareWithNull(() => { RecursiveLoadParm0(ilCtxList); });
                        },
                        // then
                        () =>
                        {
                            // 第一个参数为null
                            il.GenIfThenElse(
                                // if 
                                () =>
                                {
                                    il.CompareWithNull(() => { RecursiveLoadParm1(ilCtxList); });
                                },
                                // then
                                () =>
                                {
                                    // 第二个参数也为null，认为直接相同
                                    il.Emit(OpCodes.Br, endLabel);
                                },
                                // else
                                () =>
                                {
                                    // 第一个参数为null，第二个参数不为null
                                    il.Emit(OpCodes.Br, lbFalse);
                                });
                        },
                        // else
                        () =>
                        {
                            // 第一个参数不为null
                            il.GenIfThenElse(
                                // if 
                                () =>
                                {
                                    il.CompareWithNull(() => { RecursiveLoadParm1(ilCtxList); });
                                },
                                // then
                                () =>
                                {
                                    // 第一个参数不为null，第二个参数为null
                                    il.Emit(OpCodes.Br, lbFalse);
                                },
                                // else
                                () =>
                                {
                                    // 两个都不为null
                                    // do nothing...
                                });
                        });
                }

                // 这里开始没有任何一个为null

                foreach (var field in nowType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var ilCtxItem = new ILCtxItem(); ilCtxItem.opCodes = OpCodes.Ldfld; ilCtxItem.fi = field;
                    ilCtxList.Add(ilCtxItem);
                    GenerateField(field.FieldType, ilCtxList);
                    ilCtxList.RemoveAt(ilCtxList.Count - 1);
                }

                // 结束标签
                il.MarkLabel(endLabel);
            }
            /// <summary>
            /// 生成一个List或者Array类型的field
            /// </summary>
            /// <param name="listType"></param>
            /// <param name="ilCtxList"></param>
            void GenerateList(Type listType, List<ILCtxItem> ilCtxList) 
            {
                var itemType = listType.GetListElementType();
                if(itemType == null) 
                {
                    Debug.LogErrorFormat(" GetTypeCmp 的 GenerateList 中传入了一个无法生成的List类型：{0}", listType);
                    return;
                }
                
                // 定义变量
                var idList0 = localVarInt++;
                il.DeclareLocal(listType);
                var idList1 = localVarInt++;
                il.DeclareLocal(listType);
                var idListNullCnt = localVarInt++;
                il.DeclareLocal(typeof(int));
                var idList0Cnt = localVarInt++;
                il.DeclareLocal(typeof(int));
                var idList1Cnt = localVarInt++;
                il.DeclareLocal(typeof(int));
                var idItem0 = localVarInt++;
                il.DeclareLocal(itemType);
                var idItem1 = localVarInt++;
                il.DeclareLocal(itemType);
                // 定义标签
                var endLabel = il.DefineLabel();
                var cmpSecondListNullLabel = il.DefineLabel();
                var endFinishCmpListNullLabel = il.DefineLabel();

                // 初始化变量
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stloc, idListNullCnt);

                // 加载参数0，并获取对应field
                RecursiveLoadParm0(ilCtxList);
                il.Emit(OpCodes.Stloc, idList0);
                // 加载参数1，并获取对应field
                RecursiveLoadParm1(ilCtxList);
                il.Emit(OpCodes.Stloc, idList1);

                // 比较是否是null
                // 比较第一个List是否为null
                il.Emit(OpCodes.Ldloc, idList0);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Brfalse, cmpSecondListNullLabel);

                // 为list null计数器+1
                il.Emit(OpCodes.Ldloc, idListNullCnt);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Stloc, idListNullCnt);

                // 比较第二个List是否为null
                il.MarkLabel(cmpSecondListNullLabel);
                il.Emit(OpCodes.Ldloc, idList1);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Brfalse, endFinishCmpListNullLabel);

                // 为list null计数器+1
                il.Emit(OpCodes.Ldloc, idListNullCnt);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Stloc, idListNullCnt);

                // 结束比较List是否为null
                il.MarkLabel(endFinishCmpListNullLabel);
                il.Emit(OpCodes.Ldloc, idListNullCnt);
                il.Emit(OpCodes.Ldc_I4_2);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Brtrue, endLabel); // 如果都为null，就表示相同，直接结束了比较List

                // 如果恰好只有一个为null，那么说明不同，直接结束整个比较
                il.Emit(OpCodes.Ldloc, idListNullCnt);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Brtrue, lbFalse);

                // 到这里说明两个List都不为null
                // 开始比较列表个数是否相同
                var listGetCountMethod = listType.IsArray ? listType.GetMethod("get_Length") : listType.GetMethod("get_Count");

                il.Emit(OpCodes.Ldloc, idList0);
                il.Emit(OpCodes.Callvirt, listGetCountMethod);
                il.Emit(OpCodes.Stloc, idList0Cnt);

                il.Emit(OpCodes.Ldloc, idList1);
                il.Emit(OpCodes.Callvirt, listGetCountMethod);
                il.Emit(OpCodes.Stloc, idList1Cnt);

                il.Emit(OpCodes.Ldloc, idList0Cnt);
                il.Emit(OpCodes.Ldloc, idList1Cnt);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Brfalse, lbFalse); // 不相同直接结束cmp

                // 开始for递归比较
                il.GenFor(
                    () => {
                        il.Emit(OpCodes.Ldloc, idList0Cnt);
                    }, 
                    (idLoopIter) => {
                        var listGetItemMethod = listType.IsArray ? listType.GetMethod("Get") : listType.GetMethod("get_Item");
                        // 存储item0
                        il.Emit(OpCodes.Ldloc, idList0);
                        il.Emit(OpCodes.Ldloc, idLoopIter);
                        il.Emit(OpCodes.Callvirt, listGetItemMethod);
                        il.Emit(OpCodes.Stloc, idItem0);
                        // 存储item1
                        il.Emit(OpCodes.Ldloc, idList1);
                        il.Emit(OpCodes.Ldloc, idLoopIter);
                        il.Emit(OpCodes.Callvirt, listGetItemMethod);
                        il.Emit(OpCodes.Stloc, idItem1);
                        // 构建item上下文
                        var ilCtxItem = new ILCtxItem(); ilCtxItem.opCodes = OpCodes.Ldloc; ilCtxItem.varID0 = idItem0; ilCtxItem.varID1 = idItem1;
                        ilCtxList.Add(ilCtxItem);
                        // 递归解析
                        GenerateField(itemType, ilCtxList);
                        ilCtxList.RemoveAt(ilCtxList.Count - 1);
                    },
                    ref localVarInt);

                // 结束标签
                il.MarkLabel(endLabel);

            }

            /// <summary>
            /// 比较一个field
            /// </summary>
            /// <param name="fiList"></param>
            void GenerateField(Type nowType, List<ILCtxItem> ilCtxList) 
            {
                if(nowType.IsListOrArray())
                {
                    // Debug.Log(" IsListType " + nowType);
                    GenerateList(nowType, ilCtxList);
                }
                // 基本类型
                else if(nowType.IsBasicType() || nowType.IsEnum)
                {
                    // Debug.Log(" IsBasicType " + nowType);
                    GenerateBasicType(ilCtxList);
                }
                // Unity Type, 使用 op_Equality
                else if(nowType.IsUnityObjectType()) 
                {
                    // Debug.Log(" Unity Type " + nowType);
                    GenerateHaveOpEqualType(nowType, ilCtxList);
                }
                // 其他一些带有 op_Equality 的（例如string，Vector3）
                else if(nowType.GetCurTypeOpEqualMethodInfoIncludeParent() != null)
                {
                    // Debug.Log(" op_Equality ==== " + nowType);
                    GenerateHaveOpEqualType(nowType, ilCtxList);
                }
                // 重写了 T Equal(T)
                else if(nowType.GetCurTypeEqualsMethodInfo() != null)
                {
                    // Debug.Log(" Equals ==== " + nowType);
                    GenerateCanEqualType(nowType, ilCtxList);
                }
                else // 递归生成
                {
                    // Debug.Log(" 递归生成 " + nowType);
                    // TODO.. 这里现在是给编辑器的序列化数据使用，所以class不会为null
                    GenerateClass(nowType, ilCtxList);
                }
            }

            GenerateField(type, new List<ILCtxItem>());

            // 默认压入true作为返回值
            {
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stloc, idForAns);
                // 直接往lbRet跳，以免被赋值false
                il.Emit(OpCodes.Br, lbRet);
            }
            // 表示不同
            {
                il.MarkLabel(lbFalse);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stloc, idForAns);
                il.Emit(OpCodes.Br, lbRet);
            }
            // 退出代码
            {
                il.MarkLabel(lbRet);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ret);
            }

            var cmp = dm.CreateDelegate(typeof(Func<T, T, bool>)) as Func<T, T, bool>;
            m_mapTypeCmpCache.Add(type, cmp);
            return cmp;
        }

        private static Dictionary<Type, object> m_mapTypeCloneCache = new Dictionary<Type, object>();
        /// <summary>
        /// 返回一个类型的深拷贝器，可以自动递归复制public的字段。
        /// 注意：暂不支持 List<List<T>>, T[][], Dictionary 类型。
        /// 注意：由于无法显示定义Action<ref T, T>形式的泛型中带ref的类型，因此，这个方法的深拷贝需要保证参数不为null；否则，建议使用<seealso cref="GetTypeCloneWithReturn"/>作为替代，但是会牺牲一定性能。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [Obsolete("由于不支持带出第一个参数的值给外端，所以建议使用 GetTypeCloneWithReturnAndTwoParms 作为替代。")]
        public static Action<T, T> GetTypeClone<T>()
        {
            Type type = typeof(T);
            object cloneObj = null;
            if(m_mapTypeCloneCache.TryGetValue(type, out cloneObj)) 
            {
                return cloneObj as Action<T, T>;
            }

            var dm = new DynamicMethod("", null, new Type[]{type, type});
            var il = dm.GetILGenerator();
            Label lbRet = il.DefineLabel();
            int localVarInt = 0; // 记录局部变量使用的id

            GenCloneInner(il, type, ref localVarInt, -1, 2);

            // 退出代码
            {
                il.MarkLabel(lbRet);
                il.Emit(OpCodes.Ret);
            }

            var clone = dm.CreateDelegate(typeof(Action<T, T>)) as Action<T, T>;
            m_mapTypeCloneCache.Add(type, clone);
            return clone;
        }

        private static void GenCloneInner(ILGenerator il, Type type, ref int localVarIntFromCtx, int ctxParm0ID = -1/*-1表示不是一个局部变量，而是一个原函数的参数*/, 
            int parmCnt = 2/*默认传入的参数为两个*/)
        {
            // 事先检查一下上下文参数合法性
            if(parmCnt < 1 || parmCnt > 2) 
            {
                Debug.LogErrorFormat("GenCloneInner 上下文参数必须为1个或者2个！当前为 {0} 个。", parmCnt);
                return;
            }
            if(parmCnt == 1 && ctxParm0ID == -1) 
            {
                Debug.LogErrorFormat("GenCloneInner 当上下文参数为1个的时候，必须第一个变量必须要有上下文变量id");
                return;
            }

            // 因为不能在本地函数中直接使用ref过来的int，只能先使用一份拷贝，最后再赋值回去
            var localVarInt = localVarIntFromCtx;
            var lbRet = il.DefineLabel();
            /// <summary>
            /// 加载参数0
            /// </summary>
            void LoadParm0()
            {
                if(ctxParm0ID == -1) 
                {
                    il.Emit(type.IsStructClass() ? OpCodes.Ldarga : OpCodes.Ldarg, 0);       
                }
                else 
                {
                    il.Emit(type.IsStructClass() ? OpCodes.Ldloca : OpCodes.Ldloc, ctxParm0ID);
                }
            }
            /// <summary>
            /// 加载参数1
            /// </summary>
            void LoadParm1()
            {
                il.Emit(OpCodes.Ldarg, parmCnt - 1);
            }
            /// <summary>
            /// 为普通field赋值
            /// </summary>
            /// <param name="fi"></param>
            void MakeSetField(FieldInfo fi) 
            {
                if(fi != null) 
                {
                    il.Emit(OpCodes.Ldfld, fi);
                    il.Emit(OpCodes.Stfld, fi);
                }
            }
            /// <summary>
            /// 为List或者数组的item赋值
            /// </summary>
            /// <param name="getItemMI"></param>
            /// <param name="setItemMI"></param>
            void MakeSetItem(MethodInfo getItemMI, MethodInfo setItemMI) 
            {
                il.Emit(OpCodes.Callvirt, getItemMI);
                il.Emit(OpCodes.Callvirt, setItemMI);
            }
            /// <summary>
            /// 递归加载参数0
            /// </summary>
            /// <param name="ilCtxList"></param>
            /// <param name="ignoreLast"></param>
            void RecursiveLoadParm0(List<ILCtxItem> ilCtxList, bool ignoreLast = true)
            {
                LoadParm0();
                if(ilCtxList != null && ilCtxList.Count > 0) 
                {
                    // 加载参数0，并获取对应field
                    var cnt = ilCtxList.Count - (ignoreLast ? 1 : 0);
                    for (int i = 0; i < cnt; i++)
                    {
                        var item = ilCtxList[i];
                        if (item.opCodes == OpCodes.Ldfld || item.opCodes == OpCodes.Ldflda)
                        {
                            il.Emit(item.opCodes, item.fi);
                        }
                        else if (item.opCodes == OpCodes.Ldloc)
                        {
                            il.Emit(OpCodes.Ldloc, item.varID0);
                        }
                        else if (item.opCodes == OpCodes.Callvirt)
                        {
                            il.Emit(OpCodes.Callvirt, item.mi);
                        }
                        else
                        {
                            Debug.LogErrorFormat("RecursiveLoadParm0 上下文item中混入了 无法解析的OpCodes：{0}", item.opCodes);
                        }
                    }
                }
            }
            /// <summary>
            /// 递归加载参数1
            /// </summary>
            /// <param name="ilCtxList"></param>
            /// <param name="ignoreLast"></param>
            void RecursiveLoadParm1(List<ILCtxItem> ilCtxList, bool ignoreLast = true)
            {
                LoadParm1();
                if(ilCtxList != null && ilCtxList.Count > 0) 
                {
                    var cnt = ilCtxList.Count - (ignoreLast ? 1 : 0);
                    for (int i = 0; i < cnt; i++)
                    {
                        var item = ilCtxList[i];
                        if (item.opCodes == OpCodes.Ldfld || item.opCodes == OpCodes.Ldflda)
                        {
                            il.Emit(OpCodes.Ldfld, item.fi);
                        }
                        else if (item.opCodes == OpCodes.Ldloc)
                        {
                            il.Emit(OpCodes.Ldloc, item.varID1);
                        }
                        else if (item.opCodes == OpCodes.Callvirt)
                        {
                            il.Emit(OpCodes.Callvirt, item.mi);
                        }
                        else
                        {
                            Debug.LogErrorFormat("RecursiveLoadParm1 上下文item中混入了 无法解析的OpCodes：{0}", item.opCodes);
                        }
                    }
                }
            }
            /// <summary>
            /// 生成一个可以直接 用 = 赋值 的 field
            /// </summary>
            /// <param name="ilCtxList"></param>
            void GenerateStraightSetType(List<ILCtxItem> ilCtxList)
            {
                if(ilCtxList != null && ilCtxList.Count > 0) 
                {
                    // 加载参数0，并获取对应field
                    RecursiveLoadParm0(ilCtxList);
                    // 加载参数1，并获取对应field
                    RecursiveLoadParm1(ilCtxList);
                    // Set
                    var lastItem = ilCtxList[ilCtxList.Count - 1];
                    // 如果最后一个上下文是Ldfld，说明是为一个field赋值
                    if(lastItem.opCodes == OpCodes.Ldfld || lastItem.opCodes == OpCodes.Ldflda) 
                    {
                        MakeSetField(lastItem.fi);
                    }
                    // 否则如果最后一个上下文是Callvirt，说明是为数组中的某一个item赋值
                    else if(lastItem.opCodes == OpCodes.Callvirt)
                    {
                        MakeSetItem(lastItem.mi, lastItem.miex);
                    }
                    else 
                    {
                        Debug.LogErrorFormat("GenerateStraightSetType 中 传入了无法解析的 OpCodes {0}。", lastItem.opCodes);
                    }
                }
                else 
                {
                    // 没有上下文，说明需要的Clone就是直接拷贝
                    // 这里代表的是最顶层的直接赋值（无意义），TODO.. 通过加上ref来使得参数能被正确赋值
                    // il.Emit(OpCodes.Ldarg_1);
                    // il.Emit(OpCodes.Starg, 0);
                    // fix Bug At 2022.9.24: 上面的有问题，可能参数只有1个；而且也需要考虑可能赋值给的对象是自己new出来的局部变量的情况
                    // 这里的需求本质是，将对象参数赋值给原参数（可能是调用层的对象，也可能是自己的局部变量）
                    LoadParm1();
                    if(ctxParm0ID == -1) 
                    {
                        il.Emit(OpCodes.Starg, 0);
                    }
                    else 
                    {
                        il.Emit(OpCodes.Stloc, ctxParm0ID);
                    }
                }
            }
            /// <summary>
            /// 比较一个class类型的field
            /// </summary>
            void GenerateClass(Type nowType, List<ILCtxItem> ilCtxList) 
            {
                var endLabel = il.DefineLabel();
                var beginLogicLabel = il.DefineLabel();
                var ctor = nowType.GetConstructor(new Type[] { });

                // 先检查是否有一个参数为空
                if(!nowType.IsStructClass())
                {
                    il.GenIfThenElse(
                        // if
                        () =>
                        {
                            il.CompareWithNull(() => { RecursiveLoadParm0(ilCtxList, false); });
                        },
                        // then
                        () =>
                        {
                            // 第一个参数 == null
                            // 判断一下第二个参数是不是null
                            il.GenIfThenElse(
                                // if
                                () =>
                                {
                                    il.CompareWithNull(() => { RecursiveLoadParm1(ilCtxList, false); });
                                },
                                // then
                                () =>
                                {
                                    // 第二个参数也为null，那直接结束这个class field的操作
                                    il.Emit(OpCodes.Br, endLabel);
                                },
                                // else
                                () =>
                                {
                                    // 第二个参数不为null，
                                    // 为第一个参数new一个class
                                    if(ilCtxList == null || ilCtxList.Count == 0)
                                    {
                                        // class 是 最顶层
                                        if(ctxParm0ID == -1) 
                                        {
                                            il.GenUnityError("第一个参数为class，为null，而第二个参数不为null，没法为其拷贝。");
                                            il.Emit(OpCodes.Br, lbRet);
                                        }
                                        else 
                                        {
                                            // 这里不需要创建，因为在上下文中应该为第一个参数new好了（或者说不会跑到）
                                        }
                                    }
                                    else
                                    {
                                        RecursiveLoadParm0(ilCtxList);
                                        il.Emit(OpCodes.Newobj, ctor);

                                        var lastItem = ilCtxList[ilCtxList.Count - 1];
                                        // 如果最后一个上下文是Ldfld，那么说明不是List的内部成员
                                        if (lastItem.opCodes == OpCodes.Ldfld)
                                        {
                                            il.Emit(OpCodes.Stfld, lastItem.fi);
                                        }
                                        // 是List成员
                                        else if(lastItem.opCodes == OpCodes.Callvirt)
                                        {
                                            // set item
                                            il.Emit(OpCodes.Callvirt, lastItem.miex);
                                        }
                                        // 进入正式逻辑
                                        il.Emit(OpCodes.Br, beginLogicLabel);
                                    }
                                });
                        },
                        // else
                        () =>
                        {
                            // 第一个参数不为null
                            // 判断一下第二个参数是不是null
                            il.GenIfThenElse(
                                // if
                                () =>
                                {
                                    il.CompareWithNull(() => { RecursiveLoadParm1(ilCtxList, false); });
                                },
                                // then
                                () =>
                                {
                                    // 第二个参数为null，那么把第一个参数变为null，然后结束
                                    if (ilCtxList == null || ilCtxList.Count == 0)
                                    {
                                        // class 是 最顶层
                                        if(ctxParm0ID == -1) 
                                        {
                                            il.GenUnityError("第一个参数为class，不为null，而第二个参数为null，没法为其拷贝。");
                                            il.Emit(OpCodes.Br, lbRet);
                                        }
                                        else 
                                        {
                                            // 这里也不需要逻辑，因为这种情况在上下文处就应该处理完毕了，这里是不会跑到的
                                        }
                                    }
                                    else
                                    {
                                        RecursiveLoadParm0(ilCtxList);
                                        il.Emit(OpCodes.Ldnull);

                                        var lastItem = ilCtxList[ilCtxList.Count - 1];
                                        // 如果最后一个上下文是Ldfld，那么说明不是List的内部成员
                                        if (lastItem.opCodes == OpCodes.Ldfld)
                                        {
                                            il.Emit(OpCodes.Stfld, lastItem.fi);
                                        }
                                        // 是List成员
                                        else if (lastItem.opCodes == OpCodes.Callvirt)
                                        {
                                            // set item
                                            il.Emit(OpCodes.Callvirt, lastItem.miex);
                                        }
                                        // 把第一个参数变为null以后就可以结束了，因为null算是已经拷贝完了
                                        il.Emit(OpCodes.Br, endLabel);
                                    }
                                },
                                // else
                                () =>
                                {
                                    // 第二个参数不为null，那么往后执行即可
                                    il.Emit(OpCodes.Br, beginLogicLabel);
                                });
                        });
                }

                il.MarkLabel(beginLogicLabel);
                foreach (var field in nowType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Debug.Log(nowFi.Name + " 中的 " + field.Name);
                    var ilCtxItem = new ILCtxItem(); ilCtxItem.opCodes = field.FieldType.IsStructClass() ? OpCodes.Ldflda : OpCodes.Ldfld; ilCtxItem.fi = field;
                    ilCtxList.Add(ilCtxItem);
                    GenerateField(field.FieldType, ilCtxList, nowType);
                    ilCtxList.RemoveAt(ilCtxList.Count - 1);
                }
                
                var properties = nowType.GetProperties(); // 注意：这里只会拿出public的
                foreach (var property in properties)
                {
                    if(!property.CanRead || !property.CanWrite) 
                    {
                        continue;
                    }
                    var getMi = property.GetMethod;
                    var setMi = property.SetMethod;
                    if(getMi == null || setMi == null) 
                    {
                        continue;
                    }

                    var ilCtxItem = new ILCtxItem(); ilCtxItem.opCodes = OpCodes.Callvirt; ilCtxItem.mi = getMi; ilCtxItem.miex = setMi;
                    ilCtxList.Add(ilCtxItem);  
                    GenerateField(property.PropertyType, ilCtxList, nowType);
                    ilCtxList.RemoveAt(ilCtxList.Count - 1);
                }

                il.MarkLabel(endLabel);
            }
            /// <summary>
            /// 生成一个List或者数组类型的field
            /// </summary>
            /// <param name="listType"></param>
            /// <param name="ilCtxList"></param>
            void GenerateList(Type listType, List<ILCtxItem> ilCtxList) 
            {
                var itemType = listType.GetListElementType();
                if(itemType == null) 
                {
                    Debug.LogErrorFormat(" GetTypeClone 的 GenerateList 中传入了一个无法生成的List类型：{0}", listType);
                    return;
                }

                // 定义变量
                var idList0 = localVarInt++;
                il.DeclareLocal(listType);
                var idList1 = localVarInt++;
                il.DeclareLocal(listType);
                var idList0IsNull = localVarInt++;
                il.DeclareLocal(typeof(bool));
                var idList1IsNull = localVarInt++;
                il.DeclareLocal(typeof(bool));
                var idList0Cnt = localVarInt++;
                il.DeclareLocal(typeof(int));
                var idList1Cnt = localVarInt++;
                il.DeclareLocal(typeof(int));
                var idForI = localVarInt++;
                il.DeclareLocal(typeof(int));
                var idItem0 = localVarInt++;
                il.DeclareLocal(itemType);
                var idItem1 = localVarInt++;
                il.DeclareLocal(itemType);
                // 定义标签
                var endLabel = il.DefineLabel();
                var list0IsNullLabel = il.DefineLabel();
                var beginSetLabel = il.DefineLabel(); // 开始设置List
                var beginForLabel = il.DefineLabel(); // 开始for
                // method
                var listGetCountMethod = listType.GetMethod("get_Count");
                var listRemoveAtMethod = listType.GetMethod("RemoveAt", BindingFlags.Public | BindingFlags.Instance, null, new Type[]{typeof(int)}, null);
                var listAddMethod = listType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, new Type[]{itemType}, null);
                var listGetItemMethod = listType.GetMethod("get_Item");
                var listSetItemMethod = listType.GetMethod("set_Item");

                if(listType.IsArray)
                {
                    listGetCountMethod = listType.GetMethod("get_Length");
                    listGetItemMethod = listType.GetMethod("Get");
                    listSetItemMethod = listType.GetMethod("Set");
                }

                // FieldInfo nowField = null;
                // if(ilCtxList != null && ilCtxList.Count > 0) 
                // {
                //     nowField = ilCtxList[ilCtxList.Count - 1].fi;
                // }
                ILCtxItem lastCtxListItem = ilCtxList.LastOrDefault();
                void SetToFirstList()
                {
                    if(lastCtxListItem.fi != null) 
                    {
                        il.Emit(OpCodes.Stfld, lastCtxListItem.fi);
                    }
                    else if(lastCtxListItem.miex != null)
                    {
                        // 调用Set
                        il.Emit(OpCodes.Callvirt, lastCtxListItem.miex);
                    }
                    else 
                    {
                        Debug.LogError($"尝试对一个没有上下文的FirstList调用Set");
                    }
                }
                // TODO.. 这里没有像class一样考虑到上一层为List的情况；只考虑了上一层为class和为最顶层的情况
                if(ilCtxList.Count == 0 || lastCtxListItem.fi == null && lastCtxListItem.miex == null) 
                {
                    // Debug.LogErrorFormat("暂不支持最外层是List的结构，Type = {0}", listType);
                    // TODO.. 为参数0带上ref标记，使得即使其在null相关的操作时能被正确赋值
                    // 特殊逻辑（最外层就是List）
                    // var _cmpSecondParmIsNullLabel = il.DefineLabel();
                    // LoadParm0();
                    // il.Emit(OpCodes.Ldnull);
                    // il.Emit(OpCodes.Ceq);
                    // il.Emit(OpCodes.Brfalse, _cmpSecondParmIsNullLabel);

                    // // 第一个参数是null
                    // {
                    //     il.GenUnityError("第一个参数是null，clone没有意义！");
                    //     il.Emit(OpCodes.Br, lbRet);
                    // }

                    // // 第一个参数不是null
                    // il.MarkLabel(_cmpSecondParmIsNullLabel);
                    // {
                    //     LoadParm1();
                    //     il.Emit(OpCodes.Ldnull);
                    //     il.Emit(OpCodes.Ceq);
                    //     // 第二个也不是null，进入正式比较
                    //     il.Emit(OpCodes.Brfalse, beginSetLabel);
                    // }

                    // // 第二个参数是null
                    // {
                    //     il.GenUnityError("第二个参数是null，clone没有意义！");
                    //     il.Emit(OpCodes.Br, lbRet);
                    // }

                    il.GenIfThenElse(
                        // if
                        () => 
                        {
                            il.CompareWithNull(() => {LoadParm0();});
                        },
                        // then
                        () => 
                        {
                            // 第一个参数为null
                            il.GenIfThenElse(
                                // if
                                () => 
                                {
                                    il.CompareWithNull(() => {LoadParm1();});
                                },
                                // then
                                () => 
                                {
                                    // 1.2参数都为null
                                    if(ctxParm0ID == -1) 
                                    {
                                        il.Emit(OpCodes.Br, lbRet);
                                    }
                                    else 
                                    {
                                        // 不会跑到
                                    }
                                },
                                // else
                                () => 
                                {
                                    // 1为null，2不为null
                                    if(ctxParm0ID == -1) 
                                    {
                                        il.GenUnityError("第一个参数是null，clone没有意义！");
                                        il.Emit(OpCodes.Br, lbRet);
                                    }
                                    else 
                                    {
                                        il.Emit(OpCodes.Br, beginSetLabel);
                                    }
                                }
                            );
                        },
                        // else
                        () => 
                        {
                            // 第一个参数不为null
                            il.GenIfThenElse(
                                // if
                                () => 
                                {
                                    il.CompareWithNull(() => {LoadParm1();});
                                },
                                // then
                                () => 
                                {
                                    // 1不为null，2为null
                                    if(ctxParm0ID == -1) 
                                    {
                                        il.GenUnityError("第二个参数是null，clone没有意义！");
                                        il.Emit(OpCodes.Br, lbRet);
                                    }
                                    else 
                                    {
                                        // 不会跑到
                                    }
                                },
                                // else
                                () => 
                                {
                                    // 1不为null，2不为null
                                    if(ctxParm0ID == -1) 
                                    {
                                        il.Emit(OpCodes.Br, beginSetLabel);
                                    }
                                    else 
                                    {
                                        il.Emit(OpCodes.Br, beginSetLabel);
                                    }
                                }
                            );
                        }
                    );
                }
                else 
                {
                    // 初始化变量
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stloc, idList0IsNull);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stloc, idList1IsNull);

                    // 加载参数0，并获取对应field
                    RecursiveLoadParm0(ilCtxList, false);
                    il.Emit(OpCodes.Stloc, idList0);
                    // 加载参数1，并获取对应field
                    RecursiveLoadParm1(ilCtxList, false);
                    il.Emit(OpCodes.Stloc, idList1);

                    // 比较是否是null
                    // 比较第一个List是否为null
                    il.Emit(OpCodes.Ldloc, idList0);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Stloc, idList0IsNull);

                    // 比较第二个List是否为null
                    il.Emit(OpCodes.Ldloc, idList1);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Stloc, idList1IsNull);

                    // 判断第一个List是否为null
                    il.Emit(OpCodes.Ldloc, idList0IsNull);
                    il.Emit(OpCodes.Ldc_I4_1);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Brtrue, list0IsNullLabel);

                    // 第一个不是null
                    {
                        // 判断第二个List是不是null
                        il.Emit(OpCodes.Ldloc, idList1IsNull);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Ceq);
                        // 第二个不是null，进入正式赋值阶段
                        // Debug.Log(" Brfalse " + beginSetLabel);
                        il.Emit(OpCodes.Brfalse, beginSetLabel);
                    }

                    // 第一个不是null，第二个为null
                    {
                        // 为第一个set为null
                        RecursiveLoadParm0(ilCtxList);
                        il.Emit(OpCodes.Ldnull);
                        // il.Emit(OpCodes.Stfld, nowField);
                        SetToFirstList();
                        il.Emit(OpCodes.Br, endLabel);
                    }

                    // 第一个为null的情况
                    il.MarkLabel(list0IsNullLabel);
                    {
                        // 判断第二个List是不是null
                        il.Emit(OpCodes.Ldloc, idList1IsNull);
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(OpCodes.Ceq);
                        // 如果第二个也是null，表示双方是相同的，直接退出了
                        il.Emit(OpCodes.Brtrue, endLabel);
                        // 否则，第一个List新建一个
                        RecursiveLoadParm0(ilCtxList);
                        // var listctor = listType.GetConstructor(new Type[]{});
                        // il.Emit(OpCodes.Newobj, listctor);
                        il.CreateOneTypeToStackTop(listType);
                        // il.Emit(OpCodes.Stfld, nowField);
                        SetToFirstList();
                        il.Emit(OpCodes.Br, beginSetLabel);
                    }

                }

                // 正式set部分
                il.MarkLabel(beginSetLabel);
                {
                    // 如果是T[]而不是List，直接new一个长度相同的
                    if(listType.IsArray) 
                    {
                        RecursiveLoadParm0(ilCtxList);
                        RecursiveLoadParm1(ilCtxList, false);
                        il.Emit(OpCodes.Ldlen);
                        il.Emit(OpCodes.Conv_I4);
                        il.Emit(OpCodes.Newarr, itemType);
                        SetToFirstList();
                    }
                    
                    // 到这里说明两个List都不为null
                    // 使得两个list的数目相同
                    var itemctor = itemType.GetConstructor(new Type[]{});
                    // 注意，不能直接拿原来的变量，因为可能之前的局部变量是null，现在新建过了，因此现在要重新获取list
                    RecursiveLoadParm0(ilCtxList, false);
                    il.Emit(OpCodes.Stloc, idList0);
                    RecursiveLoadParm1(ilCtxList, false);
                    il.Emit(OpCodes.Stloc, idList1);

                    il.Emit(OpCodes.Ldloc, idList0);
                    il.Emit(OpCodes.Callvirt, listGetCountMethod);
                    il.Emit(OpCodes.Stloc, idList0Cnt);

                    il.Emit(OpCodes.Ldloc, idList1);
                    il.Emit(OpCodes.Callvirt, listGetCountMethod);
                    il.Emit(OpCodes.Stloc, idList1Cnt);

                    il.Emit(OpCodes.Ldloc, idList0Cnt);
                    il.Emit(OpCodes.Ldloc, idList1Cnt);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Brtrue, beginForLabel); // 数目相同的情况直接进入for

                    // 数目不同
                    if(!listType.IsArray)
                    {
                        // 标签
                        var list0CntGreatlist1CntLabel = il.DefineLabel();

                        il.Emit(OpCodes.Ldloc, idList0Cnt);
                        il.Emit(OpCodes.Ldloc, idList1Cnt);
                        il.Emit(OpCodes.Clt); // < 
                        il.Emit(OpCodes.Brfalse, list0CntGreatlist1CntLabel);

                        // list0的cnt < list1的cnt
                        {
                            il.GenFor(
                                () => {
                                    il.Emit(OpCodes.Ldloc, idList1Cnt);
                                    il.Emit(OpCodes.Ldloc, idList0Cnt);
                                    il.Emit(OpCodes.Sub);
                                }, 
                                (idLoopIter) => {
                                    il.Emit(OpCodes.Ldloc, idList0);
                                    if(itemctor == null) 
                                    {
                                        // il.Emit(OpCodes.Newobj, itemctor);
                                        // 没有item构造器，暂且认为是基本类型，压入一个0，TODO.. 后面可以考虑压入default(T)
                                        if(itemType.IsBasicType())
                                        {
                                            if(itemType.IsFloatType()) 
                                            {
                                                il.Emit(OpCodes.Ldc_R4, 0f);
                                            }
                                            else 
                                            {
                                                il.Emit(OpCodes.Ldc_I4_0);
                                            }
                                        }
                                        else if(itemType.IsStructClass()) 
                                        {
                                            // struct --> default(T)
                                            var idLocalStruct = localVarInt++;
                                            il.DeclareLocal(itemType);
                                            il.Emit(OpCodes.Ldloca, idLocalStruct); // 加载地址
                                            il.Emit(OpCodes.Initobj, itemType); // 在指定地址使用 default(T)
                                            il.Emit(OpCodes.Ldloc, idLocalStruct);
                                            // TODO.. class 能使用上面的作为 default(T) 么？待验证
                                        }
                                        else
                                        {
                                            il.Emit(OpCodes.Ldnull);
                                        }
                                        il.Emit(OpCodes.Callvirt, listAddMethod);
                                    }
                                    else 
                                    {
                                        if(!itemType.IsUnityObjectType())
                                        {
                                            // TODO.. 这里其实也可以只传入一个null，等到下层去操作，因为可能来源的这个位置是个null，那就白new了
                                            il.Emit(OpCodes.Newobj, itemctor);
                                        }
                                        else 
                                        {
                                            // UnityObject的情况下，不能new（例如GameObject），设置一个null即可
                                            il.Emit(OpCodes.Ldnull);
                                        }
                                        il.Emit(OpCodes.Callvirt, listAddMethod);
                                    }
                                }, 
                                ref localVarInt);
                        }

                        // list0的cnt > list1的cnt
                        il.MarkLabel(list0CntGreatlist1CntLabel);
                        {
                            il.GenFor(
                                () => {
                                    il.Emit(OpCodes.Ldloc, idList0Cnt);
                                    il.Emit(OpCodes.Ldloc, idList1Cnt);
                                    il.Emit(OpCodes.Sub);
                                },
                                (idLoopIter) => {
                                    il.Emit(OpCodes.Ldloc, idList0);
                                    il.Emit(OpCodes.Ldloc, idList0);
                                    il.Emit(OpCodes.Callvirt, listGetCountMethod);
                                    il.Emit(OpCodes.Ldc_I4_1);
                                    il.Emit(OpCodes.Sub);
                                    il.Emit(OpCodes.Callvirt, listRemoveAtMethod);
                                },
                                ref localVarInt);
                        }
                    }

                    // for阶段
                    il.MarkLabel(beginForLabel);
                    {
                        il.GenFor(
                            () => {
                                il.Emit(OpCodes.Ldloc, idList0);
                                il.Emit(OpCodes.Callvirt, listGetCountMethod);
                            }, 
                            (idLoopIter) => {
                                // 说实话，这里因为IL数组赋值的代码特殊性，刚好可以构成和普通field赋值相似的结构（加载上下文时忽略最后一句，并把最后一句拿出来做操作），运气很好=。=
                                var ilCtxItem1 = new ILCtxItem(); ilCtxItem1.opCodes = OpCodes.Ldloc; ilCtxItem1.varID0 = idLoopIter; ilCtxItem1.varID1 = idLoopIter;
                                ilCtxList.Add(ilCtxItem1);
                                var ilCtxItem2 = new ILCtxItem(); ilCtxItem2.opCodes = OpCodes.Callvirt; ilCtxItem2.mi = listGetItemMethod; ilCtxItem2.miex = listSetItemMethod;
                                ilCtxList.Add(ilCtxItem2);
                                {
                                    GenerateField(itemType, ilCtxList, listType);
                                }
                                ilCtxList.RemoveAt(ilCtxList.Count - 1);
                                ilCtxList.RemoveAt(ilCtxList.Count - 1);
                            }, 
                            ref localVarInt);
                    }
                }

                // 结束标签
                il.MarkLabel(endLabel);

            }

            /// <summary>
            /// 比较一个field
            /// </summary>
            /// <param name="fiList"></param>
            void GenerateField(Type nowType, List<ILCtxItem> ilCtxList, Type parentType) 
            {
                // Unity Object Type
                if(nowType.IsUnityObjectType()) 
                {
                    // Debug.Log(" Unity Type " + nowType);
                    GenerateStraightSetType(ilCtxList);
                }
                // 在Unity类型下的T[]，例如AnimationCurve的keys的类型Keyframe[]
                // 此时，Unity的get方法一般会返回数组的拷贝，遍历item拷贝是没有意义的，那么此时只要整个数组拷贝过去即可
                // 不过这里有个bug，正确流程应该是先get获得原数组的拷贝，然后拷贝目标数组，再把这个原数组的拷贝赋值回去，TODO.. 后面优化这个
                else if(parentType != null && parentType.IsUnityType() && nowType.IsArray) 
                {
                    GenerateStraightSetType(ilCtxList);
                }
                // List or Array
                else if(nowType.IsListOrArray())
                {
                    // Debug.Log(" IsListType " + nowType);
                    GenerateList(nowType, ilCtxList);
                }
                // 基本类型或者值类型
                else if(nowType.IsStructClass() || nowType.IsBasicType() || nowType.IsEnum)
                {
                    // Debug.Log(" IsBasicType " + nowType);
                    GenerateStraightSetType(ilCtxList);
                }
                // 其他一些带有 op_Equality 的（例如string，Vector3）
                else if(nowType.GetCurTypeOpEqualMethodInfoIncludeParent() != null)
                {
                    // Debug.Log(" op_Equality ==== " + nowType);
                    GenerateStraightSetType(ilCtxList);
                }
                // // 重写了 T Equal(T)
                // else if(nowType.GetCurTypeEqualsMethodInfo() != null)
                // {
                //     Debug.Log(" Equals ==== " + nowType);
                //     GenerateCanEqualType(nowType, ilCtxList);
                // }
                else // 递归生成
                {
                    // Debug.Log(" 递归生成 " + nowType);
                    // TODO.. 这里现在是给编辑器的序列化数据使用，所以class不会为null
                    GenerateClass(nowType, ilCtxList);
                }
            }

            GenerateField(type, new List<ILCtxItem>(), null);

            il.MarkLabel(lbRet);
            // 最后把变量id传回去
            localVarIntFromCtx = localVarInt;
        }

        private static Dictionary<Type, object> m_mapTypeCloneWithReturnCache = new Dictionary<Type, object>();
        /// <summary>
        /// 返回一个类型的深拷贝器，可以自动递归复制public的字段。
        /// 注意：暂不支持 List<List<T>>, T[][], Dictionary 类型。
        /// 注意：这个方法总是会新建一个目标对象，具有一定开销，因此，如果确定参数不会为null，可以使用<seealso cref="GetTypeClone"/>作为替代来提升性能。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [Obsolete("由于这个方法总是需要new出一个新对象，所以建议使用 GetTypeCloneWithReturnAndTwoParms 作为替代。")]
        public static Func<T, T> GetTypeCloneWithReturn<T>()
        {
            Type type = typeof(T);
            object cloneObj = null;
            if(m_mapTypeCloneWithReturnCache.TryGetValue(type, out cloneObj)) 
            {
                return cloneObj as Func<T, T>;
            }

            var dm = new DynamicMethod("", type, new Type[]{type});
            var il = dm.GetILGenerator();
            Label lbRet = il.DefineLabel();
            int localVarInt = 0; // 记录局部变量使用的id
            var idForRetAns = localVarInt++;
            il.DeclareLocal(type);
            
            /// <summary>
            /// 加载参数
            /// </summary>
            void LoadParm()
            {
                il.Emit(OpCodes.Ldarg_0);
            }

            if(type.IsBasicType()) 
            {
                // 基本类型，把参数直接储存
                LoadParm();
                il.Emit(OpCodes.Stloc, idForRetAns);
            }
            else if(type.IsStructClass()) 
            {
                // struct类型，不会为null --> default(T)
                var idLocalStruct = localVarInt++;
                il.DeclareLocal(type);
                il.Emit(OpCodes.Ldloca, idForRetAns); // 加载地址
                il.Emit(OpCodes.Initobj, type); // 在指定地址使用 default(T)

                GenCloneInner(il, type, ref localVarInt, idForRetAns, 1);
            }
            else if(type.IsListOrArray() || type.IsClass)
            {
                // 可以为null的类型
                il.GenIfThenElse(
                    // if
                    () => 
                    {
                        il.CompareWithNull(() => {LoadParm();});
                    },
                    // then
                    () => 
                    {
                        // 参数为null，那么返回一个null即可
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Stloc, idForRetAns);
                    },
                    // else
                    () =>
                    {
                        // 参数不为null，为ret新建一个
                        // TODO.. 突然发现是不是要必须获取public的？，不然可能new不出来
                        // TODO.. T[]有构造器么？
                        var ctor = type.GetConstructor(Type.EmptyTypes);
                        if(ctor == null) 
                        {
                            il.Emit(OpCodes.Ldtoken, type);
                            il.Emit(OpCodes.Call, typeof(System.Type).GetMethod("GetTypeFromHandle"));
                            il.Emit(OpCodes.Call, typeof(System.Runtime.Serialization.FormatterServices).GetMethod("GetUninitializedObject"));
                        }
                        else 
                        {
                            il.Emit(OpCodes.Newobj, ctor);
                        }
                        il.Emit(OpCodes.Stloc, idForRetAns);

                        GenCloneInner(il, type, ref localVarInt, idForRetAns, 1);
                    }
                );
            }

            il.MarkLabel(lbRet);
            {
                il.Emit(OpCodes.Ldloc, idForRetAns);
                il.Emit(OpCodes.Ret);
            }

            Func<T, T> clone = dm.CreateDelegate(typeof(Func<T, T>)) as Func<T, T>;
            m_mapTypeCloneWithReturnCache.Add(type, clone);
            return clone;
        }

        private static Dictionary<Type, object> m_mapTypeCloneWithReturnAndTwoParmsCache = new Dictionary<Type, object>();
        /// <summary>
        /// 返回一个类型的深拷贝器，可以自动递归复制public的字段。
        /// 注意：暂不支持 List<List<T>>, T[][], Dictionary 类型。
        /// 这个方法类似 a = clone(a, b); 这样调用，来把b深拷贝给a
        /// 调用端示例：
        /// var clone = TypeUtility.GetTypeCloneWithReturnAndTwoParms<MyClass>();
        /// MyClass a, b;
        /// a = clone(a, b);
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Func<T, T, T> GetTypeCloneWithReturnAndTwoParms<T>()
        {
            Type type = typeof(T);
            object cloneObj = null;
            if(m_mapTypeCloneWithReturnAndTwoParmsCache.TryGetValue(type, out cloneObj)) 
            {
                return cloneObj as Func<T, T, T>;
            }

            var dm = new DynamicMethod("", type, new Type[]{type, type});
            var il = dm.GetILGenerator();
            Label lbRet = il.DefineLabel();
            int localVarInt = 0; // 记录局部变量使用的id
            var idForRetAns = localVarInt++;
            il.DeclareLocal(type);
            
            /// <summary>
            /// 加载参数0
            /// </summary>
            void LoadParm0()
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            /// <summary>
            /// 加载参数1
            /// </summary>
            void LoadParm1()
            {
                il.Emit(OpCodes.Ldarg_1);
            }

            if(type.IsBasicType()) 
            {
                // 基本类型，把参数直接储存
                LoadParm1();
                il.Emit(OpCodes.Stloc, idForRetAns);
            }
            else if(type.IsStructClass()) 
            {
                // struct类型，不会为null
                LoadParm0();
                il.Emit(OpCodes.Stloc, idForRetAns);
                GenCloneInner(il, type, ref localVarInt, idForRetAns, 2);
            }
            else if(type.IsListOrArray() || type.IsClass)
            {
                // 可以为null的类型
                il.GenIfThenElse(
                    // if（尝试比较第二个参数，也就是来源是否为null）
                    () => 
                    {
                        il.CompareWithNull(() => {LoadParm1();});
                    },
                    // then
                    () => 
                    {
                        // 来源为null，那么返回一个null即可
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Stloc, idForRetAns);
                    },
                    // else
                    () =>
                    {
                        // 来源不为null，那么需要保证第一个参数不为null
                        // TODO.. 突然发现是不是要必须获取public的？，不然可能new不出来
                        // TODO.. T[]有构造器么？
                        // 现存储第一个参数到ret上
                        LoadParm0();
                        il.Emit(OpCodes.Stloc, idForRetAns);
                        il.GenIfThenElse(
                            // if
                            () => 
                            {
                                il.CompareWithNull(() => {il.Emit(OpCodes.Ldloc, idForRetAns);});
                            },
                            // then
                            () =>
                            {
                                // 为null，需要创建
                                // var ctor = type.GetConstructor(Type.EmptyTypes);
                                // if(ctor == null) 
                                // {
                                //     il.Emit(OpCodes.Ldtoken, type);
                                //     il.Emit(OpCodes.Call, typeof(System.Type).GetMethod("GetTypeFromHandle"));
                                //     il.Emit(OpCodes.Call, typeof(System.Runtime.Serialization.FormatterServices).GetMethod("GetUninitializedObject"));
                                // }
                                // else 
                                // {
                                //     il.Emit(OpCodes.Newobj, ctor);
                                // }
                                il.CreateOneTypeToStackTop(type);
                                il.Emit(OpCodes.Stloc, idForRetAns);
                            },
                            // else
                            null
                        );

                        GenCloneInner(il, type, ref localVarInt, idForRetAns, 2);
                    }
                );
            }

            il.MarkLabel(lbRet);
            {
                il.Emit(OpCodes.Ldloc, idForRetAns);
                il.Emit(OpCodes.Ret);
            }

            Func<T, T, T> clone = dm.CreateDelegate(typeof(Func<T, T, T>)) as Func<T, T, T>;
            m_mapTypeCloneWithReturnAndTwoParmsCache.Add(type, clone);
            return clone;
        }

        private static Dictionary<Type, object> m_mapTypeCreateNewObjCache = new Dictionary<Type, object>();
        /// <summary>
        /// 好像没啥用，没有反射，不用IL也行
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Func<T> GetTypeCreateNewObj<T>()
        {
            Type type = typeof(T);
            object ctorObj = null;
            if(m_mapTypeCreateNewObjCache.TryGetValue(type, out ctorObj)) 
            {
                return ctorObj as Func<T>;
            }

            var dm = new DynamicMethod("", type, Type.EmptyTypes);
            var il = dm.GetILGenerator();
            Label lbRet = il.DefineLabel();
            int localVarInt = 0; // 记录局部变量使用的id

            var idRetAns = localVarInt++;
            il.DeclareLocal(type); 

            var noParmCtor = type.GetConstructor(Type.EmptyTypes);
            if(noParmCtor == null) 
            {
                if(type.IsBasicType())
                {
                    if(type.IsFloatType()) 
                    {
                        il.Emit(OpCodes.Ldc_R4, 0f);
                    }
                    else 
                    {
                        il.Emit(OpCodes.Ldc_I4_0);
                    }
                }
                else if(type.IsStructClass()) 
                {
                    // struct --> default(T)
                    var idLocalStruct = localVarInt++;
                    il.DeclareLocal(type);
                    il.Emit(OpCodes.Ldloca, idLocalStruct); // 加载地址
                    il.Emit(OpCodes.Initobj, type); // 在指定地址使用 default(T)
                    il.Emit(OpCodes.Ldloc, idLocalStruct);
                    // TODO.. class 能使用上面的作为 default(T) 么？待验证
                }
                else
                {
                    il.Emit(OpCodes.Ldtoken, type);
                    il.Emit(OpCodes.Call, typeof(System.Type).GetMethod("GetTypeFromHandle"));
                    il.Emit(OpCodes.Call, typeof(System.Runtime.Serialization.FormatterServices).GetMethod("GetUninitializedObject"));
                }
            }
            else 
            {
                il.Emit(OpCodes.Newobj, noParmCtor);
            }
            // 存储返回值
            il.Emit(OpCodes.Stloc, idRetAns);

            il.MarkLabel(lbRet);
            {
                il.Emit(OpCodes.Ldloc, idRetAns);
                il.Emit(OpCodes.Ret);
            }

            var ctor = dm.CreateDelegate(typeof(Func<T>)) as Func<T>;
            m_mapTypeCreateNewObjCache.Add(type, ctor);
            return ctor;
        }
    }
}