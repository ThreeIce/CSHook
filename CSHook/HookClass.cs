using System;
using System.Collections.Generic;
using System.Text;

namespace CSHook
{
    [AttributeUsage(AttributeTargets.Class,Inherited = true)]
    public class HookClassAttribute : Attribute
    {
    }
}
