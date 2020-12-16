using System.Collections.Generic;

namespace Duotify.EFCore.EntityPartialGenerator
{
    public static class DictionaryExtension
    {
        public static T GetValueOrDefault<T>(this IDictionary<string, T> dictionary, string key)
        {
            if (dictionary.TryGetValue(key, out T value))
            {
                return value;
            }

            return default;
        }
    }
}