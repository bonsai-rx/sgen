using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using Bonsai.Expressions;

namespace Bonsai.Sgen.Tests
{
    internal static class SerializerTestHelper
    {
        public static IObservable<string> Serialize(Assembly assembly, string serializerTypeName, Type modelType, object value)
        {
            var serializer = new CombinatorBuilder { Combinator = Activator.CreateInstance(GetGeneratedType(assembly, serializerTypeName))! };
            var source = WorkflowTestHelper.Return(modelType, Expression.Constant(value));
            return WorkflowTestHelper.BuildObservable<string>(source, serializer);
        }

        public static IObservable<object> Deserialize(Assembly assembly, string deserializerTypeName, Type modelType, string text)
        {
            var deserializer = (ExpressionBuilder)Activator.CreateInstance(GetGeneratedType(assembly, deserializerTypeName))!;
            WorkflowTestHelper.SetTypeMapping(deserializer, modelType.Name);
            var source = Expression.Constant(Observable.Return(text));
            return WorkflowTestHelper.BuildObservable<object>(source, deserializer);
        }

        public static Type GetGeneratedType(Assembly assembly, string typeName)
        {
            return assembly.GetTypes().SingleOrDefault(type => type.Name == typeName)
                ?? throw new InvalidOperationException($"The generated type {typeName} was not found.");
        }
    }
}
