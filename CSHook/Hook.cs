using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace CSHook
{
    public struct MethodSign
    {
        public int obj;
        public string method;
        //当obj为null时，标识为0
        public MethodSign(int obj,string method)
        {
            this.obj = obj;
            this.method = method;
        }
        public MethodSign(object obj, string method) : this(obj == null ? 0 : obj.GetHashCode(), method)
        {

        }
        public MethodSign(object obj,MethodInfo method)
        {
            this.obj = obj == null ? 0 : obj.GetHashCode();
            this.method = GetMethodName(method);
        }

        public MethodSign(MethodInfo method) :
            this(0 , method)
        {

        }
        /// <summary>
        /// 获得方法名，为给每个重载方法都有独特名称，大概格式为ReturnType_MethodName_Param1Type_Param2Type……
        /// </summary>
        /// <param name="method">方法</param>
        /// <returns>方法名</returns>
        public static string GetMethodName(MethodInfo method)
        {
            //节省性能
            StringBuilder sb = new StringBuilder(method.ReturnType.ToString());
            sb.Append("_");
            //方法名
            sb.Append(method.Name);
            //参数类型
            ParameterInfo[] param = method.GetParameters();
            int length = param.Length;
            for(int i = 0; i < length; i++)
            {
                sb.Append("_");
                sb.Append(param[i].ParameterType.FullName);
            }
            return sb.ToString();
        }
    }
    public static class Hook
    {
        private static Dictionary<MethodSign, List<Action<HookArgs>>> Before = new Dictionary<MethodSign, List<Action<HookArgs>>>();
        private static Dictionary<MethodSign, List<Action<HookArgs>>> After = new Dictionary<MethodSign, List<Action<HookArgs>>>();
        private static Dictionary<MethodSign, Action<HookArgs>> Instead = new Dictionary<MethodSign, Action<HookArgs>>();
        /// <summary>
        /// 注入代码于方法执行前
        /// </summary>
        /// <param name="obj">目标对象，为null则注入所有对象的该方法或注入静态方法</param>
        /// <param name="method">目标方法对象，可将目标方法转为委托后通过Method属性获得</param>
        /// <param name="action">要注入的方法</param>
        public static void HookBefore(object obj,MethodInfo method,Action<HookArgs> action)
        {
            MethodSign m = new MethodSign(obj, method);
            if (!Before.ContainsKey(m))
            {
                //如果不存在映射列表，创建列表
                Before.Add(m, new List<Action<HookArgs>>());
            }
            Before[m].Add(action);
        }
        /// <summary>
        /// 注入代码于属性执行前
        /// </summary>
        /// <param name="obj">目标对象，为null则注入所有对象的该方法或注入静态属性</param>
        /// <param name="property">目标属性对象，可以通过目标对象的Type中的GetProperty获得</param>
        /// <param name="action">要注入的方法</param>
        /// <param name="isGet">是否是Get访问器</param>
        public static void HookBefore(object obj,PropertyInfo property,Action<HookArgs> action,bool isGet)
        {
            MethodSign m;
            if (isGet)
            {
                //注入Get方法
                //当obj为null，标识为0
                m = new MethodSign(obj,property.GetGetMethod());
            }
            else
            {
                //注入Set方法
                m = new MethodSign(obj, property.GetSetMethod());
            }
            if (!Before.ContainsKey(m))
            {
                //如果不存在映射列表，创建列表
                Before.Add(m, new List<Action<HookArgs>>());
            }
            Before[m].Add(action);
        }
        /// <summary>
        /// 注入代码于静态方法或构造函数执行前
        /// </summary>
        /// <param name="method">目标方法</param>
        /// <param name="action">要注入的方法</param>
        public static void HookBefore(MethodInfo method,Action<HookArgs> action)
        {
            HookBefore(null, method, action);
        }
        /// <summary>
        /// 注入代码于方法执行后
        /// </summary>
        /// <param name="obj">目标对象，为null则注入所有对象的该方法或注入静态方法</param>
        /// <param name="method">目标方法对象，可将目标方法转为委托后通过Method属性获得</param>
        /// <param name="action">要注入的方法</param>
        public static void HookAfter(object obj, MethodInfo method, Action<HookArgs> action)
        {
            
            MethodSign m = new MethodSign(obj, method);
            if (!After.ContainsKey(m))
            {
                //如果不存在映射列表，创建列表
                After.Add(m, new List<Action<HookArgs>>());
            }
            After[m].Add(action);
        }
        /// <summary>
        /// 注入代码于静态方法或构造函数执行后
        /// </summary>
        /// <param name="method">目标方法</param>
        /// <param name="action">要注入的方法</param>
        public static void HookAfter(MethodInfo method, Action<HookArgs> action)
        {
            HookAfter(null, method, action);
        }
        /// <summary>
        /// 注入代码于属性执行后
        /// </summary>
        /// <param name="obj">目标对象，为null则注入所有对象的该方法或注入静态属性</param>
        /// <param name="property">目标属性对象，可以通过目标对象的Type中的GetProperty获得</param>
        /// <param name="action">要注入的方法</param>
        /// <param name="isGet">是否是Get访问器</param>
        public static void HookAfter(object obj, PropertyInfo property, Action<HookArgs> action, bool isGet)
        {
            MethodSign m;
            if (isGet)
            {
                //注入Get方法
                
                m = new MethodSign(obj, property.GetGetMethod());
            }
            else
            {
                //注入Set方法
                m = new MethodSign(obj, property.GetSetMethod());
            }
            if (!After.ContainsKey(m))
            {
                //如果不存在映射列表，创建列表
                After.Add(m, new List<Action<HookArgs>>());
            }
            After[m].Add(action);
        }
        /// <summary>
        /// 注入代码顶替原先方法
        /// </summary>
        /// <param name="obj">目标对象，为null则注入所有对象的该方法或注入静态方法</param>
        /// <param name="method">目标方法对象，可将目标方法转为委托后通过Method属性获得</param>
        /// <param name="action">要注入的方法</param>
        public static void HookInstead(object obj, MethodInfo method, Action<HookArgs> action)
        {
            
            MethodSign m = new MethodSign(obj, method);
            if (Instead.ContainsKey(m))
                //当已被替代时，抛出此错误
                throw new HookInsteadException();
            Instead.Add(m,action);
        }
        /// <summary>
        /// 注入代码顶替原静态方法或构造函数
        /// </summary>
        /// <param name="method">目标方法</param>
        /// <param name="action">要注入的方法</param>
        public static void HookInstead(MethodInfo method, Action<HookArgs> action)
        {
            HookInstead(null, method, action);
        }
        /// <summary>
        /// 注入代码顶替原先方法
        /// </summary>
        /// <param name="obj">目标对象，为null则注入所有对象的该方法或注入静态属性</param>
        /// <param name="property">目标属性对象，可以通过目标对象的Type中的GetProperty获得</param>
        /// <param name="action">要注入的方法</param>
        /// <param name="isGet">是否是Get访问器</param>
        public static void HookInstead(object obj, PropertyInfo property, Action<HookArgs> action, bool isGet)
        {
            MethodSign m;
            if (isGet)
            {
                //注入Get方法
                m = new MethodSign(obj, property.GetGetMethod());
            }
            else
            {
                //注入Set方法
                m = new MethodSign(obj, property.GetSetMethod());
            }
            if (Instead.ContainsKey(m))
                //当已被替代时，抛出此错误
                throw new HookInsteadException();
            Instead.Add(m, action);
        }
        
        /// <summary>
        /// 删除钩子
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="method">目标方法</param>
        /// <param name="action">要删除的钩子</param>
        public static void DeHookBefore(object obj, MethodInfo method, Action<HookArgs> action)
        {
            MethodSign m = new MethodSign(obj, method);
            //检测是否存在，不存在则抛出错误
            if (!Before[m].Contains(action))
            {
                throw new ArgumentException("要删除的钩子不存在", "action");
            }
            Before[m].Remove(action);
        }
        /// <summary>
        /// 删除静态方法或构造函数钩子
        /// </summary>
        /// <param name="method">目标静态方法或构造函数</param>
        /// <param name="action">要删除的钩子</param>
        public static void DeHookBefore(MethodInfo method, Action<HookArgs> action)
        {
           
 DeHookBefore(null, method, action);
        }
        /// <summary>
        /// 删除属性钩子
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="proper">目标属性</param>
        /// <param name="action">要删除的钩子</param>
        /// <param name="isGet">是否是Get方法</param>
        public static void DeHookBefore(object obj, PropertyInfo proper, Action<HookArgs> action,bool isGet)
        {
            if (isGet)
            {
                DeHookBefore(obj, proper.GetMethod, action);
            }
            else
            {
                DeHookBefore(obj, proper.SetMethod, action);
            }
        }
        /// <summary>
        /// 删除钩子
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="method">目标方法</param>
        /// <param name="action">要删除的钩子</param>
        public static void DeHookAfter(object obj, MethodInfo method, Action<HookArgs> action)
        {
            MethodSign m = new MethodSign(obj, method);
            //检测是否存在，不存在则抛出错误
            if (!After[m].Contains(action))
            {
                throw new ArgumentException("要删除的钩子不存在", "action");
            }
            After[m].Remove(action);
        }
        /// <summary>
        /// 删除静态方法或构造函数钩子
        /// </summary>
        /// <param name="method">目标静态方法或构造函数</param>
        /// <param name="action">要删除的钩子</param>
        public static void DeHookAfter(MethodInfo method, Action<HookArgs> action)
        {
            DeHookAfter(null, method, action);
        }
        /// <summary>
        /// 删除属性钩子
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="proper">目标属性</param>
        /// <param name="action">要删除的钩子</param>
        /// <param name="isGet">是否是Get方法</param>
        public static void DeHookAfter(object obj, PropertyInfo proper, Action<HookArgs> action, bool isGet)
        {
            if (isGet)
            {
                DeHookAfter(obj, proper.GetMethod, action);
            }
            else
            {
                DeHookAfter(obj, proper.SetMethod, action);
            }
        }
        /// <summary>
        /// 删除钩子
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="method">目标方法</param>
        /// <param name="action">要删除的钩子</param>
        public static void DeHookInstead(object obj, MethodInfo method, Action<HookArgs> action)
        {
            Instead.Remove(new MethodSign(obj, method));
        }
        /// <summary>
        /// 删除静态方法或构造函数钩子
        /// </summary>
        /// <param name="method">目标静态方法或构造函数</param>
        /// <param name="action">要删除的钩子</param>
        public static void DeHookInstead(MethodInfo method, Action<HookArgs> action)
        {
            DeHookInstead(null, method, action);
        }
        /// <summary>
        /// 删除属性钩子
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="proper">目标属性</param>
        /// <param name="action">要删除的钩子</param>
        /// <param name="isGet">是否是Get方法</param>
        public static void DeHookInstead(object obj, PropertyInfo proper, Action<HookArgs> action, bool isGet)
        {
            if (isGet)
            {
                DeHookInstead(obj, proper.GetMethod, action);
            }
            else
            {
                DeHookInstead(obj, proper.SetMethod, action);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="method">方法标识</param>
        /// <param name="args">方法信息</param>
        /// <returns>方法返回值</returns>
        public static object Execute(MethodSign method,HookArgs args)
        {
            //检查是否存在注入Before的函数
            if (Before.ContainsKey(method))
            {
                //注入before的函数列表
                var list = Before[method];
                //长度
                int length = list.Count;
                //遍历执行
                for (int i = 0; i < length; i++)
                {
                    list[i](args);
                    //检测是否要停止执行函数
                    if (args.IsReturn)
                    {
                        //如果是立即停止，跳转到Return
                        if (args.IsReturnNow)
                        {
                            goto Return;
                        }
                        else
                        {
                            //否则执行注入After的内容
                            goto After;
                        }
                    }
                }
            }
            //如果该方法是对象方法，检测是否存在注入所有对象的该方法before的函数
            if (method.obj != 0 && Before.ContainsKey(new MethodSign(0, method.method)))
            {
                //注入before的函数列表
                var list = Before[new MethodSign(0, method.method)];
                //长度
                int length = list.Count;
                //遍历执行
                for (int i = 0; i < length; i++)
                {
                    list[i](args);
                    //检测是否要停止执行函数
                    if (args.IsReturn)
                    {
                        //如果是立即停止，跳转到Return
                        if (args.IsReturnNow)
                        {
                            goto Return;
                        }
                        else
                        {
                            //否则执行注入After的内容
                            goto After;
                        }
                    }
                }
            }
            //检测原方法是否被代替
            if (Instead.ContainsKey(method))
            {
                Instead[method](args);
                //检测是否直接返回，跳过注入After的函数的执行
                if (args.IsReturnNow)
                    goto Return;
            }else if(method.obj != 0 && Instead.ContainsKey(new MethodSign(0, method.method)))
            {
                Instead[new MethodSign(0,method.method)](args);
                //检测是否直接返回，跳过注入After的函数的执行
                if (args.IsReturnNow)
                    goto Return;
            }
            else
            {
                args.Original();
            }
            After:
            //检查是否存在注入After的函数
            if (After.ContainsKey(method))
            {
                //注入After的函数列表
                var list = After[method];
                //长度
                int length = list.Count;
                //遍历执行
                for (int i = 0; i < length; i++)
                {
                    list[i](args);
                    //检测是否要直接返回
                    if (args.IsReturnNow)
                    {
                        goto Return;
                    }
                }
            }
            //同理
            if (method.obj != 0 && After.ContainsKey(new MethodSign(0, method.method)))
            {
                //注入After的函数列表
                var list = After[new MethodSign(0, method.method)];
                //长度
                int length = list.Count;
                //遍历执行
                for (int i = 0; i < length; i++)
                {
                    list[i](args);
                    //检测是否要直接返回
                    if (args.IsReturnNow)
                    {
                        goto Return;
                    }
                }
            }
            Return:
            return args.ReturnValue;
        }
    }
}
