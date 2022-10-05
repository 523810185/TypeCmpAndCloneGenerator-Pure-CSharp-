using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace W3.TypeExtension
{
    public static class TypeExtension
    {
        public static T Copy<T>(this T from)
        {
            // Clone太容易重名了，甚至和 ICloneable 的 Clone 重名，还是换一个名字好了
            return TypeInnerMethodInfo.CloneWithReturn(from);
        }

        public static T CloneFrom<T>(this T to, T from)
        {
            var cloneMethod = TypeUtility.GetTypeCloneWithReturnAndTwoParms<T>();
            return cloneMethod(to, from);
        }

        public static bool CompareWith<T>(this T a, T b)
        {
            var cmpMethod = TypeUtility.GetTypeCmp<T>();
            return cmpMethod(a, b);
        }
    }
}
