namespace Bonsai.Sgen
{
    [Flags]
    public enum SerializerLibraries
    {
        None = 0x0,
        NewtonsoftJson = 0x1,
        YamlDotNet = 0x4,
    }
}
