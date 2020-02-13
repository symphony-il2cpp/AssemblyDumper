using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AssemblyDumper
{
    internal static class Extensions
    {
        public static IEnumerable<Regex> ToRegexes(
            this IEnumerable<string> self) =>
            self.SelectMany(p =>
            {
                try
                {
                    return new[] {new Regex(p)};
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    return new Regex[0];
                }
            });

        public static IEnumerable<MethodInfo> GetMeaningfulMethods(
            this Type self) =>
            self.GetRuntimeMethods()
                .Where(m => !m.Attributes.HasFlag(MethodAttributes.Assembly));

        public static bool IsCompilerGenerated(this MemberInfo self) =>
            Attribute.GetCustomAttribute(self,
                typeof(CompilerGeneratedAttribute)) != null;

        public static string GetFullNameOrName(this Type self) =>
            self.FullName ?? self.Name;

        public static bool
            ContainsType(this HashSet<string> self, string type) =>
            self.Contains(type) ||
            type.EndsWith("*") ||
            type.EndsWith("[]") && self.Contains(type[..^2]);
    }
}
