using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reactive.Linq;
using Bonsai.Expressions;

namespace Bonsai.Sgen.Tests
{
    static class WorkflowTestHelper
    {
        public static Expression Return(Type type, Expression value)
        {
            return Expression.Call(typeof(Observable), nameof(Observable.Return), [type], Expression.Convert(value, type));
        }

        public static void SetTypeMapping(ExpressionBuilder builder, string typeName)
        {
            var property = TypeDescriptor.GetProperties(builder)["Type"]!;
            var context = new PropertyDescriptorContext(builder, property);
            property.SetValue(builder, property.Converter.ConvertFrom(context, CultureInfo.InvariantCulture, typeName));
        }

        public static IObservable<object> Select(object value, string selector)
        {
            var source = Return(value.GetType(), Expression.Constant(value));
            return BuildObservable<object>(source, new MemberSelectorBuilder { Selector = selector });
        }

        public static IObservable<TResult> BuildObservable<TResult>(Expression source, params ExpressionBuilder[] builders)
        {
            var workflow = new ExpressionBuilderGraph();
            var node = workflow.Add(new SourceExpressionBuilder(source));
            foreach (var builder in builders.Append(new WorkflowOutputBuilder()))
            {
                var successor = workflow.Add(builder);
                workflow.AddEdge(node, successor, new ExpressionBuilderArgument());
                node = successor;
            }
            return workflow.BuildObservable<TResult>();
        }

        class SourceExpressionBuilder(Expression expression) : ZeroArgumentExpressionBuilder
        {
            public override Expression Build(IEnumerable<Expression> arguments)
            {
                return expression;
            }
        }

        class PropertyDescriptorContext(object instance, PropertyDescriptor property) : ITypeDescriptorContext
        {
            public IContainer? Container => null;

            public object Instance => instance;

            public PropertyDescriptor PropertyDescriptor => property;

            public object? GetService(Type serviceType)
            {
                return null;
            }

            public bool OnComponentChanging()
            {
                return true;
            }

            public void OnComponentChanged()
            {
            }
        }
    }
}
