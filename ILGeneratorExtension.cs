using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ILUtility
{
    using System;
    using System.Reflection;
    using System.Reflection.Emit;

    public static class ILGeneratorExtension
    {
        // Debug.Log
        private static MethodInfo m_stUnityDebugLogMF = typeof(UnityEngine.Debug).GetMethod("Log", new Type[] {typeof(string)});
        private static MethodInfo m_stUnityDebugLogErrorMF = typeof(UnityEngine.Debug).GetMethod("LogError", new Type[] {typeof(string)});
        public static ILGenerator GenUnityLog(this ILGenerator il, string logstr)
        {
            il.Emit(OpCodes.Ldstr, logstr);
            il.Emit(OpCodes.Call, m_stUnityDebugLogMF);
            return il;
        }

        public static ILGenerator GenUnityError(this ILGenerator il, string logstr)
        {
            il.Emit(OpCodes.Ldstr, logstr);
            il.Emit(OpCodes.Call, m_stUnityDebugLogErrorMF);
            return il;
        }

        /// <summary>
        /// 上面要接入上下文，没用过，暂时不知道能不能用
        /// </summary>
        /// <param name="il"></param>
        /// <returns></returns>
        public static ILGenerator GenUnityLog(this ILGenerator il)
        {
            il.Emit(OpCodes.Call, m_stUnityDebugLogMF);
            return il;
        }

        public static ILGenerator MakeIntAdd(this ILGenerator il, int idVarInt, int addVal)
        {
            il.Emit(OpCodes.Ldloc, idVarInt);
            il.Emit(OpCodes.Ldc_I4, addVal);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, idVarInt);

            return il;
        }

        public static ILGenerator MakeIntSub(this ILGenerator il, int idVarInt, int subVal)
        {
            il.Emit(OpCodes.Ldloc, idVarInt);
            il.Emit(OpCodes.Ldc_I4, subVal);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Stloc, idVarInt);

            return il;
        }

        public static ILGenerator CompareWithNull(this ILGenerator il, Action ctxFc)
        {
            //RecursiveLoadParm0(ilCtxList);
            ctxFc();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ceq);

            return il;
        }

        public static ILGenerator SetIntVal(this ILGenerator il, int idVarInt, int setVal)
        {
            il.Emit(OpCodes.Ldc_I4, setVal);
            il.Emit(OpCodes.Stloc, idVarInt);

            return il;
        }

        /// <summary>
        /// 生成一个For循环
        /// </summary>
        /// <param name="il"></param>
        /// <param name="loopCntFc">需要把loopCnt生成好并放在IL栈上</param>
        /// <param name="forBodyFc"></param>
        /// <param name="localVarInt"></param>
        /// <returns></returns>
        public static ILGenerator GenFor(this ILGenerator il, Action loopCntFc, Action<int> forBodyFc, ref int localVarInt) 
        {
            // 变量
            var idLoopCnt = localVarInt++;
            il.DeclareLocal(typeof(int));
            var idLoopIter = localVarInt++;
            il.DeclareLocal(typeof(int));
            // 标签
            var innerIIsLessCntLabel = il.DefineLabel();
            var innerForLabel = il.DefineLabel();

            // il.Emit(OpCodes.Ldloc, idList0Cnt);
            // il.Emit(OpCodes.Ldloc, idList1Cnt);
            // il.Emit(OpCodes.Sub);
            loopCntFc();
            il.Emit(OpCodes.Stloc, idLoopCnt);
            // i = 0
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc, idLoopIter);
            il.Emit(OpCodes.Br, innerIIsLessCntLabel);

            // for
            il.MarkLabel(innerForLabel);
            {
                // il.Emit(OpCodes.Ldloc, idList0);
                // il.Emit(OpCodes.Ldloc, idList0);
                // il.Emit(OpCodes.Callvirt, listGetCountMethod);
                // il.Emit(OpCodes.Ldc_I4_1);
                // il.Emit(OpCodes.Sub);
                // il.Emit(OpCodes.Callvirt, listRemoveAtMethod);
                forBodyFc(idLoopIter);
            }

            // i++
            {
                il.Emit(OpCodes.Ldloc, idLoopIter);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Stloc, idLoopIter);
            }

            // i < cnt
            il.MarkLabel(innerIIsLessCntLabel);
            {
                il.Emit(OpCodes.Ldloc, idLoopIter);
                il.Emit(OpCodes.Ldloc, idLoopCnt);
                il.Emit(OpCodes.Clt);
                il.Emit(OpCodes.Brtrue, innerForLabel);
            }
            return il;
        }

        public static ILGenerator GenIfThenElse(this ILGenerator il, Action ifBodyFc, Action thenBodyFc, Action elseBodyFc)
        {
            // 变量
            // ...
            // 标签
            var thenLabel = il.DefineLabel();
            var elseLabel = il.DefineLabel();
            var retLabel = il.DefineLabel();
            //RecursiveLoadParm0(ilCtxList);
            //il.Emit(OpCodes.Ldnull);
            //il.Emit(OpCodes.Ceq);
            ifBodyFc();
            il.Emit(OpCodes.Brfalse, elseLabel);

            il.MarkLabel(thenLabel);
            {
                if(thenBodyFc != null)
                {
                    // +1
                    //il.Emit(OpCodes.Ldloc, idClassNullCnt);
                    //il.Emit(OpCodes.Ldc_I4_1);
                    //il.Emit(OpCodes.Add);
                    //il.Emit(OpCodes.Stloc, idClassNullCnt);
                    thenBodyFc();
                }
                il.Emit(OpCodes.Br, retLabel);
            }

            il.MarkLabel(elseLabel);
            {
                if(elseBodyFc != null)
                {
                    elseBodyFc();
                }
            }

            il.MarkLabel(retLabel);

            return il;
        }

        public static ILGenerator CreateOneTypeToStackTop(this ILGenerator il, Type type) 
        {
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

            return il;
        }
    }
}
