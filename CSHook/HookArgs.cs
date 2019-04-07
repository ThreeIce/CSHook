using System;
using System.Collections.Generic;
using System.Text;

namespace CSHook
{
    public class HookArgs
    {
        /// <summary>
        /// 方法的对象，静态方法为null
        /// </summary>
        public object Obj { get => obj; }
        /// <summary>
        /// 方法的返回值，当注入在调用前和代替时或方法没有返回值、返回值本身为null时，此值为null
        /// </summary>
        public object ReturnValue { get => returnValue; set => returnValue = value; }
        public Action Original;
        /// <summary>
        /// 方法的参数
        /// </summary>
        private Dictionary<string,object> Params;
        /// <summary>
        /// 参数是否被修改过
        /// </summary>
        public bool haschanged { get; private set; }
        /// <summary>
        /// 是否返回
        /// </summary>
        public bool IsReturn { get; private set; }
        /// <summary>
        /// 如果返回，是否立刻返回
        /// </summary>
        public bool IsReturnNow { get; private set; }
        private object obj;
        private object returnValue;

        public object this[string index]
        {
            get
            {
                return Params[index];
            }
            set
            {
                //检测类型是否相同，如果不同，抛出错误，相同就赋值
                if (value.GetType() != Params[index].GetType())
                {
                    throw new ArgumentException();
                }
                Params[index] = value;
                //标识已修改过参数
                haschanged = true;
            }
        }
        public HookArgs(object _obj, Action<HookArgs> _original,Dictionary<string,object> param)
        {
            obj = _obj;
            returnValue = null;
            Original = delegate { _original(this); };
            Params = param;
            haschanged = false;
            IsReturn = false;
            IsReturnNow = false;
        }
        /// <summary>
        /// 结束函数执行
        /// </summary>
        /// <param name="returnValue">返回值，当为null时，将返回ReturnValue</param>
        /// <param name="isNow">是否立刻返回，如果为false，将执行调用后执行的Hook方法，如果为true，不执行任何注入After的函数直接返回</param>
        public void Return(object returnvalue,bool isNow = false)
        {
            //计算返回值
            ReturnValue = returnvalue == null ? ReturnValue : returnvalue;
            //计算返回状态
            IsReturn = true;
            IsReturnNow = isNow;
        }
    }
}
