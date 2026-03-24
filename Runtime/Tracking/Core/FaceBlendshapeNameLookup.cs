using System;
using System.Collections.Generic;

namespace Star67
{
    public static class FaceBlendshapeNameLookup
    {
        private static readonly IReadOnlyDictionary<string, FaceBlendshapeLocation> camelCase =
            CreateLookup(useCamelCase: true);

        private static readonly IReadOnlyDictionary<string, FaceBlendshapeLocation> pascalCase =
            CreateLookup(useCamelCase: false);

        public static IReadOnlyDictionary<string, FaceBlendshapeLocation> CamelCase => camelCase;
        public static IReadOnlyDictionary<string, FaceBlendshapeLocation> PascalCase => pascalCase;

        private static IReadOnlyDictionary<string, FaceBlendshapeLocation> CreateLookup(bool useCamelCase)
        {
            var lookup = new Dictionary<string, FaceBlendshapeLocation>(StringComparer.Ordinal);
            Array values = Enum.GetValues(typeof(FaceBlendshapeLocation));

            for (int i = 0; i < values.Length; i++)
            {
                FaceBlendshapeLocation location = (FaceBlendshapeLocation)values.GetValue(i);
                string name = location.ToString();
                lookup[useCamelCase ? ToCamelCase(name) : name] = location;
            }

            return lookup;
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length == 1)
            {
                return char.ToLowerInvariant(value[0]).ToString();
            }

            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}
