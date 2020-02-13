using System.Reflection;

namespace AssemblyDumper.CSharp
{
    internal class Parameter
    {
        public string Name;
        public string Type;

        public static Parameter
            SelectFromParameterInfo(ParameterInfo p, int i) => new Parameter
        {
            Name = p.Name ?? $"param{i}",
            Type = p.ParameterType.GetFullNameOrName()
        };
    }
}
