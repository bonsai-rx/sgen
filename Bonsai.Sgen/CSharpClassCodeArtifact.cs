using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal class CSharpClassCodeArtifact : CodeArtifact
    {
        public CSharpClassCodeArtifact(CSharpClassTemplateModel model, ITemplate template) : base(
            model.ClassName,
            model.BaseClassName,
            CodeArtifactType.Class,
            CodeArtifactLanguage.CSharp,
            CodeArtifactCategory.Contract,
            template)
        {
            Model = model;
        }

        public CSharpClassTemplateModel Model { get; }
    }
}
