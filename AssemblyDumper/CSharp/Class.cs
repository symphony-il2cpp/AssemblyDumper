namespace AssemblyDumper.CSharp
{
    internal class Class
    {
        public string Name;
        public string InternalName;
        public string[] Namespace;
        public StaticField[] StaticFields;
        public Field[] Fields;
        public Constructor[] Constructors;
        public Method[] Methods;
    }
}
