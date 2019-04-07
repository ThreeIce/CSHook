using System;
using System.Collections.Generic;
using System.Text;

namespace CSHook
{
    /// <summary>
    /// 当某方法已被Instead时抛出此错误
    /// </summary>
    public class HookInsteadException : Exception
    {
        public HookInsteadException()
        {
        }
    }
}
