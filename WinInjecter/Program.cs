using System;
using HookInjecter;

namespace WinInjecter
{
    class Program
    {
        static void Main(string[] args)
        {
            int length = args.Length;
            for (int i = 0; i < length; i++)
            {
                Injecter.Inject(args[i]);
            }
        }
    }
}
