using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSHook;

namespace Example
{
    [HookClass]
    class Program
    {
        [Hook]
        public static int Say(string words)
        {
            Console.WriteLine("words");
            return 1;
        }
        static void Main()
        {
            Program p = new Program();
            Console.WriteLine("注入前");
            Console.WriteLine("返回值为" + Say("HelloWorld").ToString());
            Console.WriteLine("注入后");
            Hook.HookBefore(((Func<string, int>)Say).Method,(args) => {
                Console.WriteLine("参数内容为" + args["words"]);
                Console.WriteLine("是否阻断[Y/N]");
                if(Console.ReadLine() == "Y")
                {
                    Console.WriteLine("已阻断");
                    args.Return(2,true);
                }
            });
            Hook.HookInstead(((Func<string, int>)Say).Method, (args) => {
                Console.WriteLine("替代成功");
                args.Return(3);
            });
            Hook.HookAfter(((Func<string, int>)Say).Method, (args) => {
                Console.WriteLine("返回值为" + (int)args.ReturnValue);
                args.Return((int)args.ReturnValue + 1);

            });
            Console.WriteLine("返回值为" + Say("HelloWorld").ToString());
        }
    }
}
