using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;


namespace FxEvents.Shared.TypeExtensions
{

    public static class DelegateExtensions
    {
        public static Delegate CreateDelegate(this MethodInfo method, object target)
        {
            bool action = method.ReturnType == typeof(void);
            System.Collections.Generic.IEnumerable<Type> types = method.GetParameters().Select(self => self.ParameterType);

            Func<Type[], Type> functionType;

            if (action)
            {
                functionType = Expression.GetActionType;
            }
            else
            {
                functionType = Expression.GetFuncType;
                types = types.Concat(new[] { method.ReturnType });
            }

            return method.IsStatic
                ? Delegate.CreateDelegate(functionType(types.ToArray()), method)
                : Delegate.CreateDelegate(functionType(types.ToArray()), target, method.Name);
        }
    }
}