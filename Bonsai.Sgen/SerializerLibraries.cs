namespace Bonsai.Sgen
{
    [Flags]
    internal enum SerializerLibraries
    {
        None = 0x0,
        NewtonsoftJson = 0x1,
        SystemTextJson = 0x2,
        YamlDotNet = 0x4,
    }
}
