using System;
using System.Collections.Generic;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CSHook;
using System.Linq;
using System.Reflection;
using System.IO;

namespace HookInjecter
{
    /// <summary>
    /// 注入模式，Member表示注入有特性的成员，Class表示注入有特性的类内所有成员，Assembly表示注入所有类的所有成员
    /// </summary>
    public enum InjectMode
    {
        Member,Class,Assembly
    }
    /// <summary>
    /// 要注入的成员
    /// </summary>
    public class Injecter
    {
        /// <summary>
        /// 注入模式
        /// </summary>
        public static InjectMode mode = InjectMode.Member;
        /// <summary>
        /// 不注入的成员列表
        /// </summary>
        public static int Dont_Inject = 0;
        /// <summary>
        /// 不注入对象方法
        /// </summary>
        public const int Method = 1;
        /// <summary>
        /// 不注入静态方法
        /// </summary>
        public const int StaticMethod = 2;
        /// <summary>
        /// 不注入构造函数
        /// </summary>
        public const int Ctor = 4;
        /// <summary>
        /// 不注入属性
        /// </summary>
        public const int Property = 8;
        public static void Inject(string path)
        {
            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
            {

                using (var assembly = AssemblyDefinition.ReadAssembly(fs, new ReaderParameters() { ReadSymbols = true }))//ReadSymbols+WriteSymbols让注入后调试时pdb能正确加载，非常重要
                {
                    Console.WriteLine("开始注入");
                    //获得所有类型
                    var types = new List<TypeDefinition>();
                    types.AddRange(assembly.MainModule.GetTypes());
                    //获得两个特性名
                    string name1 = typeof(HookClassAttribute).FullName, name2 = typeof(HookAttribute).FullName;
                    //遍历所有类型
                    int len = types.Count;
                    Console.WriteLine(len);
                    for (int i = 0; i < len; i++)
                    {
                        //如果该类有HookClass特性或者注入整个程序集，遍历其所有方法和属性
                        if (mode == InjectMode.Assembly||types[i].CustomAttributes.Any(a => a.AttributeType.FullName == name1))
                        {
                            Console.WriteLine("发现一个类");
                            //遍历方法
                            var methods = types[i].Methods;
                            int length = methods.Count;
                            for (int j = 0; j < length; j++)
                            {

                                //如果该方法有Hook特性或者要注入所有成员，将其注入
                                if ( mode != InjectMode.Member||methods[j].CustomAttributes.Any(a => a.AttributeType.FullName == name2))
                                {
                                    Console.WriteLine("发现一个方法");
                                    Inject(types[i], methods[j]);
                                }
                            }
                            //遍历属性
                            var proper = types[i].Properties;
                            length = proper.Count;
                            for (int j = 0; j < length; j++)
                            {
                                //如果有Hook特性或者注入所有成员，将其注入
                                if (mode != InjectMode.Member || proper[j].CustomAttributes.Any(a => a.AttributeType.FullName == name2))
                                {
                                    Console.WriteLine("发现一个属性");
                                    Inject(types[i], proper[j].GetMethod);
                                    Inject(types[i], proper[j].SetMethod);
                                }
                            }
                        }
                    }
                    Console.WriteLine("注入完毕");
                    assembly.Dispose();
                    //保存更改
                    assembly.Write(new WriterParameters() { WriteSymbols = true });
                }
            }
        }
        private static void Inject(TypeDefinition type, MethodDefinition method)
        {
            if (method.IsStatic && !((Dont_Inject & StaticMethod) == StaticMethod))//检测是否是静态方法并且要注入静态方法
            {
                InjectStaticMethod(type, method);
                Console.WriteLine("已成功注入静态方法：" + method.FullName);
            }
            else if (method.IsConstructor && !((Dont_Inject & Ctor) == Ctor))//检测是否是构造函数以及是否要注入
            {
                InjectCtor(type, method);
                Console.WriteLine("已成功注入构造函数：" + method.FullName);
            }
            else if (!((Dont_Inject & Method) == Method))//检测是否要注入普通方法
            {
                //注入普通方法
                InjectMethod(type, method);
                Console.WriteLine("已成功注入方法：" + method.FullName);
            }
        }
        private static void InjectCtor(TypeDefinition type, MethodDefinition method)
        {
            //新建一个用于储存原方法内容的方法，该方法为普通方法，不是构造函数
            MethodDefinition original = new MethodDefinition("Ctor_Original",
                method.Attributes - Mono.Cecil.MethodAttributes.SpecialName - Mono.Cecil.MethodAttributes.RTSpecialName , type.Module.TypeSystem.Void);
            type.Methods.Add(original);
            //将原方法的内容拷贝过来
            int length = method.Parameters.Count;
            //拷贝参数
            for (int i = 0; i < length; i++)
            {
                original.Parameters.Add(method.Parameters[i]);
            }
            //拷贝变量
            var mv = method.Body.Variables;
            var ov = original.Body.Variables;
            length = mv.Count;
            for (int i = 0; i < length; i++)
            {
                ov.Add(mv[i]);
            }
            //清空原方法变量
            mv.Clear();
            //拷贝代码
            var mi = method.Body.Instructions;
            var oi = original.Body.Instructions;
            length = mi.Count;
            for (int i = 0; i < length; i++)
            {
                oi.Add(mi[i]);
            }
            //清空原方法代码
            mi.Clear();
            //新建一个封装原方法的函数
            MethodDefinition odelegate = new MethodDefinition("Ctor_Delegate",
                method.Attributes - Mono.Cecil.MethodAttributes.SpecialName - Mono.Cecil.MethodAttributes.RTSpecialName, type.Module.TypeSystem.Void);
            type.Methods.Add(odelegate);
            //参数为HookArgs，返回值为void，用于传给HookArgs的构造函数
            odelegate.Parameters.Add(new ParameterDefinition("args", Mono.Cecil.ParameterAttributes.None
                , type.Module.ImportReference(typeof(HookArgs))));
            //odelegate具体内容
            //注入器
            var IL = odelegate.Body.GetILProcessor();
            //函数首行当然为nop了
            IL.Emit(OpCodes.Nop);
            //获取参数数量
            length = original.Parameters.Count;
            //获取参数列表
            var p = original.Parameters;
            //如果有返回值，先将HookArgs入栈，为后面给HookArgs.ReturnValue赋值做准备
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                IL.Emit(OpCodes.Ldarg_1);
            }
            //将方法的对象入栈
            IL.Emit(OpCodes.Ldarg_0);//0是this，非静态方法参数从1开始
            //将所有参数挨个压栈
            for (int i = 0; i < length; i++)
            {

                //先把HookArgs入栈
                IL.Emit(OpCodes.Ldarg_1);
                //将参数索引入栈
                IL.Emit(OpCodes.Ldstr, p[i].Name);
                //取出参数
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(HookArgs).GetMethod("get_Item")));
                //值类型和引用类型分开处理
                if (p[i].ParameterType.IsValueType)
                {
                    //拆箱并入栈
                    IL.Emit(OpCodes.Unbox_Any, p[i].ParameterType);
                }
                else
                {
                    //转换并入栈
                    IL.Emit(OpCodes.Castclass, p[i].ParameterType);
                }
            }
            //调用方法
            IL.Emit(OpCodes.Call, original);
            //如果有返回值，将返回值赋值给HookArgs
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                //如果是值类型，要先装箱
                if (method.ReturnType.IsValueType)
                {
                    IL.Emit(OpCodes.Box, method.ReturnType);
                }
                //赋值
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(HookArgs).GetProperty("ReturnValue").SetMethod));
            }
            //返回
            IL.Emit(OpCodes.Ret);
            //让原方法调用Excute函数
            IL = method.Body.GetILProcessor();
            //方法首行当然是Nop了
            IL.Emit(OpCodes.Nop);
            //调用Hook.Excute
            //先生成第一个参数对象
            IL.Emit(OpCodes.Ldarg_0);
            //当前方法名
            IL.Emit(OpCodes.Ldstr, GetMethodName(method));
            //创建MethodSign参数
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(MethodSign).GetConstructor(new Type[] { typeof(object), typeof(string) })));
            //生成第二个参数对象
            //将方法对象载出作为第一个参数
            IL.Emit(OpCodes.Ldarg_0);
            //获取原方法委托
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldftn, odelegate);
            //创建Action<HookArgs>参数
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(Action<HookArgs>).GetConstructors()[0]));
            //创建Dictionary<string,object>参数，并把方法参数挨个放入
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(Dictionary<string, object>).GetConstructor(new Type[0])));
            //挨个放入方法参数
            length = method.Parameters.Count;
            var mp = method.Parameters;
            for (int i = 0; i < length; i++)
            {
                //先把Dictionary<string,object>在栈上复制一份，因为每次调用完方法都会弹出
                IL.Emit(OpCodes.Dup);
                //载入参数名
                IL.Emit(OpCodes.Ldstr, mp[i].Name);
                //载入参数
                IL.Emit(OpCodes.Ldarg, i + 1);//arg0是this，需+1
                //如果是值类型，先装箱
                if (mp[i].ParameterType.IsValueType)
                {
                    IL.Emit(OpCodes.Box, mp[i].ParameterType);
                }
                //将参数加入列表
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(Dictionary<string, object>).GetMethod("Add")));
            }
            //创建HookArgs
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(HookArgs).GetConstructor(new Type[] {
                    typeof(object),typeof(Action<HookArgs>),typeof(Dictionary<string,object>)
                })));
            //执行Execute方法
            IL.Emit(OpCodes.Call,
                type.Module.ImportReference(typeof(Hook).GetMethod("Execute")));
            //如果有返回值，返回返回值
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                //值类型拆箱，引用类型转换
                if (method.ReturnType.IsValueType)
                {
                    IL.Emit(OpCodes.Unbox_Any, method.ReturnType);

                }
                else
                {
                    IL.Emit(OpCodes.Castclass, method.ReturnType);
                }
            }
            else
            {
                //没有的话把Execute的返回值弹出栈
                IL.Emit(OpCodes.Pop);
            }
            //返回
            IL.Emit(OpCodes.Ret);
            //注入完毕
        }

        private static void InjectMethod(TypeDefinition type, MethodDefinition method)
        {
            //新建一个用于储存原方法内容的方法
            MethodDefinition original = new MethodDefinition(method.Name + "_Original",
                method.Attributes, method.ReturnType);
            type.Methods.Add(original);
            //将原方法的内容拷贝过来
            int length = method.Parameters.Count;
            //拷贝参数
            for (int i = 0; i < length; i++)
            {
                original.Parameters.Add(method.Parameters[i]);
            }
            //拷贝变量
            var mv = method.Body.Variables;
            var ov = original.Body.Variables;
            length = mv.Count;
            for (int i = 0; i < length; i++)
            {
                ov.Add(mv[i]);
            }
            //清空原方法变量
            mv.Clear();
            //拷贝代码
            var mi = method.Body.Instructions;
            var oi = original.Body.Instructions;
            length = mi.Count;
            for (int i = 0; i < length; i++)
            {
                oi.Add(mi[i]);
            }
            //清空原方法代码
            mi.Clear();
            //新建一个封装原方法的函数
            MethodDefinition odelegate = new MethodDefinition(method.Name + "_Delegate",
                method.Attributes, type.Module.TypeSystem.Void);
            type.Methods.Add(odelegate);
            //参数为HookArgs，返回值为void，用于传给HookArgs的构造函数
            odelegate.Parameters.Add(new ParameterDefinition("args", Mono.Cecil.ParameterAttributes.None
                , type.Module.ImportReference(typeof(HookArgs))));
            //odelegate具体内容
            //注入器
            var IL = odelegate.Body.GetILProcessor();
            //函数首行当然为nop了
            IL.Emit(OpCodes.Nop);
            //获取参数数量
            length = original.Parameters.Count;
            //获取参数列表
            var p = original.Parameters;
            //如果有返回值，先将HookArgs入栈，为后面给HookArgs.ReturnValue赋值做准备
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                IL.Emit(OpCodes.Ldarg_1);
            }
            //将方法的对象入栈
            IL.Emit(OpCodes.Ldarg_0);//0是this，非静态方法参数从1开始
            //将所有参数挨个压栈
            for (int i = 0; i < length; i++)
            {

                //先把HookArgs入栈
                IL.Emit(OpCodes.Ldarg_1);
                //将参数索引入栈
                IL.Emit(OpCodes.Ldstr,p[i].Name);
                //取出参数
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(HookArgs).GetMethod("get_Item")));
                //值类型和引用类型分开处理
                if (p[i].ParameterType.IsValueType)
                {
                    //拆箱并入栈
                    IL.Emit(OpCodes.Unbox_Any, p[i].ParameterType);
                }
                else
                {
                    //转换并入栈
                    IL.Emit(OpCodes.Castclass, p[i].ParameterType);
                }
            }
            //调用方法
            IL.Emit(OpCodes.Call, original);
            //如果有返回值，将返回值赋值给HookArgs
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                //如果是值类型，要先装箱
                if (method.ReturnType.IsValueType)
                {
                    IL.Emit(OpCodes.Box, method.ReturnType);
                }
                //赋值
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(HookArgs).GetProperty("ReturnValue").SetMethod));
            }
            //返回
            IL.Emit(OpCodes.Ret);
            //让原方法调用Excute函数
            IL = method.Body.GetILProcessor();
            //方法首行当然是Nop了
            IL.Emit(OpCodes.Nop);
            //调用Hook.Excute
            //先生成第一个参数对象
            IL.Emit(OpCodes.Ldarg_0);
            //当前方法名
            IL.Emit(OpCodes.Ldstr, GetMethodName(method));
            //创建MethodSign参数，因为是值类型，用Initobj
            IL.Emit(OpCodes.Newobj, 
                type.Module.ImportReference(typeof(MethodSign).GetConstructor(new Type[]{typeof(object),typeof(string)})));
            //生成第二个参数对象
            //将方法对象载出作为第一个参数
            IL.Emit(OpCodes.Ldarg_0);
            //获取原方法委托
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldftn,odelegate);
            //创建Action<HookArgs>参数
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(Action<HookArgs>).GetConstructors()[0]));
            //创建Dictionary<string,object>参数，并把方法参数挨个放入
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(Dictionary<string,object>).GetConstructor(new Type[0])));
            //挨个放入方法参数
            length = method.Parameters.Count;
            var mp = method.Parameters;
            for (int i = 0; i < length; i++)
            {
                //先把Dictionary<string,object>在栈上复制一份，因为每次调用完方法都会弹出
                IL.Emit(OpCodes.Dup);
                //载入参数名
                IL.Emit(OpCodes.Ldstr, mp[i].Name);
                //载入参数
                IL.Emit(OpCodes.Ldarg, i + 1);//arg0是this，需+1
                //如果是值类型，先装箱
                if (mp[i].ParameterType.IsValueType)
                {
                    IL.Emit(OpCodes.Box, mp[i].ParameterType);
                }
                //将参数加入列表
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(Dictionary<string,object>).GetMethod("Add")));
            }
            //创建HookArgs
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(HookArgs).GetConstructor(new Type[] {
                    typeof(object),typeof(Action<HookArgs>),typeof(Dictionary<string,object>)
                })));
            //执行Execute方法
            IL.Emit(OpCodes.Call,
                type.Module.ImportReference(typeof(Hook).GetMethod("Execute")));
            //如果有返回值，返回返回值
            if(method.ReturnType != type.Module.TypeSystem.Void)
            {
                //值类型拆箱，引用类型转换
                if (method.ReturnType.IsValueType)
                {
                    IL.Emit(OpCodes.Unbox_Any, method.ReturnType);

                }
                else
                {
                    IL.Emit(OpCodes.Castclass, method.ReturnType);
                }
            }
            else
            {
                //没有的话把Execute的返回值弹出栈
                IL.Emit(OpCodes.Pop);
            }
            //返回
            IL.Emit(OpCodes.Ret);
            //注入完毕
        }
        private static void InjectStaticMethod(TypeDefinition type, MethodDefinition method)
        {
            //新建一个用于储存原方法内容的方法
            MethodDefinition original = new MethodDefinition(method.Name + "_Original",
                method.Attributes, method.ReturnType);
            type.Methods.Add(original);
            //将原方法的内容拷贝过来
            int length = method.Parameters.Count;
            //拷贝参数
            for (int i = 0; i < length; i++)
            {
                original.Parameters.Add(method.Parameters[i]);
            }
            //拷贝变量
            var mv = method.Body.Variables;
            var ov = original.Body.Variables;
            length = mv.Count;
            for (int i = 0; i < length; i++)
            {
                ov.Add(mv[i]);
            }
            //清空原方法变量
            mv.Clear();
            //拷贝代码
            var mi = method.Body.Instructions;
            var oi = original.Body.Instructions;
            length = mi.Count;
            for (int i = 0; i < length; i++)
            {
                oi.Add(mi[i]);
            }
            //清空原方法代码
            mi.Clear();
            //新建一个封装原方法的函数
            MethodDefinition odelegate = new MethodDefinition(method.Name + "_Delegate",
                method.Attributes, type.Module.TypeSystem.Void);
            type.Methods.Add(odelegate);
            //参数为HookArgs，返回值为void，用于传给HookArgs的构造函数
            odelegate.Parameters.Add(new ParameterDefinition("args", Mono.Cecil.ParameterAttributes.None
                , type.Module.ImportReference(typeof(HookArgs))));
            //odelegate具体内容
            //注入器
            var IL = odelegate.Body.GetILProcessor();
            //函数首行当然为nop了
            IL.Emit(OpCodes.Nop);
            //获取参数数量
            length = original.Parameters.Count;
            //获取参数列表
            var p = original.Parameters;
            //如果有返回值，先将HookArgs入栈，为后面给HookArgs.ReturnValue赋值做准备
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                IL.Emit(OpCodes.Ldarg_0);
            }
            //将所有参数挨个压栈
            for (int i = 0; i < length; i++)
            {

                //先把HookArgs入栈
                IL.Emit(OpCodes.Ldarg_0);
                //将参数索引入栈
                IL.Emit(OpCodes.Ldstr, p[i].Name);
                //取出参数
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(HookArgs).GetMethod("get_Item")));
                //值类型和引用类型分开处理
                if (p[i].ParameterType.IsValueType)
                {
                    //拆箱并入栈
                    IL.Emit(OpCodes.Unbox_Any, p[i].ParameterType);
                }
                else
                {
                    //转换并入栈
                    IL.Emit(OpCodes.Castclass, p[i].ParameterType);
                }
            }
            //调用方法
            IL.Emit(OpCodes.Call, original);
            //如果有返回值，将返回值赋值给HookArgs
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                //如果是值类型，要先装箱
                if (method.ReturnType.IsValueType)
                {
                    IL.Emit(OpCodes.Box, method.ReturnType);
                }
                //赋值
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(HookArgs).GetProperty("ReturnValue").SetMethod));
            }
            //返回
            IL.Emit(OpCodes.Ret);
            //让原方法调用Excute函数
            IL = method.Body.GetILProcessor();
            //方法首行当然是Nop了
            IL.Emit(OpCodes.Nop);
            //调用Hook.Excute，先生成第一个参数对象(因为是静态方法，方法对象传null)
            IL.Emit(OpCodes.Ldnull);
            //当前方法名
            IL.Emit(OpCodes.Ldstr, GetMethodName(method));
            //创建MethodSign参数，因为是值类型，用Initobj
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(MethodSign).GetConstructor(new Type[] { typeof(object), typeof(string) })));
            //生成第二个参数对象
            //将方法对象载出作为第一个参数
            IL.Emit(OpCodes.Ldnull);
            //获取原方法委托
            IL.Emit(OpCodes.Ldnull);
            IL.Emit(OpCodes.Ldftn, odelegate);
            //创建Action<HookArgs>参数
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(Action<HookArgs>).GetConstructors()[0]));
            //创建Dictionary<string,object>参数，并把方法参数挨个放入
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(Dictionary<string, object>).GetConstructor(new Type[0])));
            //挨个放入方法参数
            length = method.Parameters.Count;
            var mp = method.Parameters;
            for (int i = 0; i < length; i++)
            {
                //先把Dictionary<string,object>在栈上复制一份，因为每次调用完方法都会弹出
                IL.Emit(OpCodes.Dup);
                //载入参数名
                IL.Emit(OpCodes.Ldstr, mp[i].Name);
                //载入参数
                IL.Emit(OpCodes.Ldarg, i);
                //如果是值类型，先装箱
                if (mp[i].ParameterType.IsValueType)
                {
                    IL.Emit(OpCodes.Box, mp[i].ParameterType);
                }
                //将参数加入列表
                IL.Emit(OpCodes.Call,
                    type.Module.ImportReference(typeof(Dictionary<string, object>).GetMethod("Add")));
            }
            //创建HookArgs
            IL.Emit(OpCodes.Newobj,
                type.Module.ImportReference(typeof(HookArgs).GetConstructor(new Type[] {
                    typeof(object),typeof(Action<HookArgs>),typeof(Dictionary<string,object>)
                })));
            //执行Execute方法
            IL.Emit(OpCodes.Call,
                type.Module.ImportReference(typeof(Hook).GetMethod("Execute")));
            //如果有返回值，返回返回值
            if (method.ReturnType != type.Module.TypeSystem.Void)
            {
                //值类型拆箱，引用类型转换
                if (method.ReturnType.IsValueType)
                {
                    IL.Emit(OpCodes.Unbox_Any, method.ReturnType);

                }
                else
                {
                    IL.Emit(OpCodes.Castclass, method.ReturnType);
                }
            }
            else
            {
                //没有的话把Execute的返回值弹出栈
                IL.Emit(OpCodes.Pop);
            }
            //返回
            IL.Emit(OpCodes.Ret);
            //注入完毕
        }
        /// <summary>
        /// 使用Mono.cecil的MethoDefinition获得方法名函数
        /// </summary>
        /// <param name="method">方法</param>
        /// <returns>方法名</returns>
        public static string GetMethodName(MethodDefinition method)
        {
            //节省性能
            StringBuilder sb = new StringBuilder(method.ReturnType.ToString());
            sb.Append("_");
            //方法名
            sb.Append(method.Name);
            //参数类型
            var param = method.Parameters;
            int length = param.Count;
            for (int i = 0; i < length; i++)
            {
                sb.Append("_");
                sb.Append(param[i].ParameterType.FullName);
            }
            return sb.ToString();
        }
    }
}
