using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq.Expressions;

namespace W3.TypeExtension
{
    /// <summary>
    /// 每个类的类型方法存储在这个泛型类中
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal static class TypeInnerMethodGenerator<T>
    {
        internal static readonly Func<T, T> TypeCloneWithReturn = TypeUtility.GetTypeCloneWithReturn<T>();
        internal static readonly Func<T, T, T> TypeCloneWithReturnAndTwoParms = TypeUtility.GetTypeCloneWithReturnAndTwoParms<T>();
        internal static readonly Dictionary<Type, Func<T, T, T>> TypeCloneWithReturnAndTwoParms_GenericMap = new Dictionary<Type, Func<T, T, T>>();
        internal static readonly Func<T, T, bool> TypeCmp = TypeUtility.GetTypeCmp<T>();
    }

    internal static class TypeInnerMethodInfo
    {
        internal static readonly MethodInfo CloneWithReturnMethodInfo;
        internal static readonly MethodInfo CloneWithReturnAndTwoParmsMethodInfo;
        internal static readonly MethodInfo CloneWithReturnAndTwoParmsMethodInfo_OfGenericType;
        internal static readonly MethodInfo CmpMethodInfo;

        internal static T CloneWithReturn<T>(T from) 
        {
            return TypeInnerMethodGenerator<T>.TypeCloneWithReturn(from);
        }

        internal static T CloneWithReturnAndTwoParms<T>(T to, T from) 
        {
            return TypeInnerMethodGenerator<T>.TypeCloneWithReturnAndTwoParms(to, from);
        }

        internal static T CloneWithReturnAndTwoParms_OfGenericType<T>(T to, T from)
        {
            if(from == null) 
            {
                return default(T);
            }
            
            var realType = from.GetType();
            if(!typeof(T).IsAssignableFrom(realType))
            {
                throw new Exception($"你传入的类型{realType}不是{typeof(T)}的子类");
            }
            
            var map = TypeInnerMethodGenerator<T>.TypeCloneWithReturnAndTwoParms_GenericMap;
            if(!map.TryGetValue(realType, out var action))
            {
                action = TypeUtility.GetTypeCloneWithReturnAndTwoParmsOfType<T>(realType);
                map.Add(realType, action);
            }
            return action(to, from);
        }

        internal static bool Cmp<T>(T a, T b)
        {
            return TypeInnerMethodGenerator<T>.TypeCmp(a, b);
        } 


        static TypeInnerMethodInfo()
        {
            var bindingFlag = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            CloneWithReturnMethodInfo = typeof(TypeInnerMethodInfo).GetMethod("CloneWithReturn", bindingFlag).GetGenericMethodDefinition();
            CloneWithReturnAndTwoParmsMethodInfo = typeof(TypeInnerMethodInfo).GetMethod("CloneWithReturnAndTwoParms", bindingFlag).GetGenericMethodDefinition();
            CmpMethodInfo = typeof(TypeInnerMethodInfo).GetMethod("Cmp", bindingFlag).GetGenericMethodDefinition();
            CloneWithReturnAndTwoParmsMethodInfo_OfGenericType = typeof(TypeInnerMethodInfo).GetMethod("CloneWithReturnAndTwoParms_OfGenericType", bindingFlag).GetGenericMethodDefinition();
        }
    }
}
