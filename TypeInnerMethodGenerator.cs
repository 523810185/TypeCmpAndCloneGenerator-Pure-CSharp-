using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq.Expressions;

namespace W3.TypeExtension
{
    internal static class TypeInnerMethodGenerator<T>
    {
        internal static readonly Func<T, T> TypeCloneWithReturn = TypeUtility.GetTypeCloneWithReturn<T>();
        internal static readonly Func<T, T, T> TypeCloneWithReturnAndTwoParms = TypeUtility.GetTypeCloneWithReturnAndTwoParms<T>();
        internal static readonly Func<T, T, bool> TypeCmp = TypeUtility.GetTypeCmp<T>();
    }

    internal static class TypeInnerMethodInfo
    {
        public static readonly MethodInfo CloneWithReturnMethodInfo;
        public static readonly MethodInfo CloneWithReturnAndTwoParmsMethodInfo;
        public static readonly MethodInfo CmpMethodInfo;

        public static T CloneWithReturn<T>(T from) 
        {
            return TypeInnerMethodGenerator<T>.TypeCloneWithReturn(from);
        }

        public static T CloneWithReturnAndTwoParms<T>(T to, T from) 
        {
            return TypeInnerMethodGenerator<T>.TypeCloneWithReturnAndTwoParms(to, from);
        }

        public static bool Cmp<T>(T a, T b)
        {
            return TypeInnerMethodGenerator<T>.TypeCmp(a, b);
        } 


        static TypeInnerMethodInfo()
        {
            CloneWithReturnMethodInfo = typeof(TypeInnerMethodInfo).GetMethod("CloneWithReturn").GetGenericMethodDefinition();
            CloneWithReturnAndTwoParmsMethodInfo = typeof(TypeInnerMethodInfo).GetMethod("CloneWithReturnAndTwoParms").GetGenericMethodDefinition();
            CmpMethodInfo = typeof(TypeInnerMethodInfo).GetMethod("Cmp").GetGenericMethodDefinition();
        }
    }
}
