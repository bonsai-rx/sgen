using System;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Bonsai.Expressions;
using Bonsai.Reactive;

// Reads a document from standard input and writes it back to standard output after
// a workflow deserializes and serializes it with the generated operators.
// Usage: Runner <Json|Yaml> <fully qualified type name>
static class Runner
{
    static async Task Main(string[] args)
    {
        string text;
        var encoding = new UTF8Encoding(false);
        using (var reader = new StreamReader(Console.OpenStandardInput(), encoding))
        {
            text = await reader.ReadToEndAsync();
        }

        var format = args[0];
        var modelType = Type.GetType(args[1], throwOnError: true);
        var deserializer = (ExpressionBuilder)Activator.CreateInstance(Type.GetType(modelType.Namespace + ".DeserializeFrom" + format, throwOnError: true));
        var typeMapping = Activator.CreateInstance(typeof(TypeMapping<>).MakeGenericType(modelType));
        TypeDescriptor.GetProperties(deserializer)["Type"].SetValue(deserializer, typeMapping);
        var serializer = new CombinatorBuilder { Combinator = Activator.CreateInstance(Type.GetType(modelType.Namespace + ".SerializeTo" + format, throwOnError: true)) };
        var result = await RunRoundTripTestAsync(text, deserializer, serializer);

        using (var writer = new StreamWriter(Console.OpenStandardOutput(), encoding))
        {
            await writer.WriteAsync(result);
        }
    }

    static async Task<string> RunRoundTripTestAsync(string text, ExpressionBuilder deserializer, ExpressionBuilder serializer)
    {
        var workflow = new ExpressionBuilderGraph();
        var source = workflow.Add(new CombinatorBuilder { Combinator = new StringProperty { Value = text } });
        var take = workflow.Add(new CombinatorBuilder { Combinator = new Take { Count = 1 } });
        var deserialize = workflow.Add(deserializer);
        var serialize = workflow.Add(serializer);
        var output = workflow.Add(new WorkflowOutputBuilder());
        workflow.AddEdge(source, take, new ExpressionBuilderArgument());
        workflow.AddEdge(take, deserialize, new ExpressionBuilderArgument());
        workflow.AddEdge(deserialize, serialize, new ExpressionBuilderArgument());
        workflow.AddEdge(serialize, output, new ExpressionBuilderArgument());
        return await workflow.BuildObservable<string>();
    }
}
