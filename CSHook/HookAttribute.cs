using System;
using System.Collections.Generic;
using System.Text;

namespace CSHook
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor)]
    public class HookAttribute : Attribute
    {
    }
}
