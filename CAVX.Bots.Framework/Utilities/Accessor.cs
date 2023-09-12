using System;
using System.Linq.Expressions;

namespace CAVX.Bots.Framework.Utilities
{
    public class Accessor<T>
    {
        public Accessor(Expression<Func<T>> expression)
        {
            if (expression.Body is not MemberExpression memberExpression)
                throw new ArgumentException("expression must return a field or property");
            var parameterExpression = Expression.Parameter(typeof(T));

            _setter = Expression.Lambda<Action<T>>(Expression.Assign(memberExpression, parameterExpression), parameterExpression).Compile();
            _getter = expression.Compile();
        }

        public void Set(T value) => _setter(value);
        public T Get() => _getter();

        private readonly Action<T> _setter;
        private readonly Func<T> _getter;
    }
}
