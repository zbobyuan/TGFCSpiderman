using System;
using System.Collections.Generic;

namespace taiyuanhitech.TGFCSpiderman.IoC
{
    /// <summary>
    /// The simplest IoC container in the world.
    /// </summary>
    public class IoCContainer
    {//
        private readonly  Dictionary<Type, Func<object>> _type2Creator = new Dictionary<Type, Func<object>>();

        public IoCContainer Register<T>(Func<object> creator)
        {
            _type2Creator.Add(typeof(T), creator);
            return this;
        }

        public T Create<T>()
        {
            return (T)(_type2Creator[typeof (T)]());
        }
    }
}
