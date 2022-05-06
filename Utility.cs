using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HTMLBuilder
{
    public static class Parse
    {
        public static T Flags<T>(string? input, out List<string> failed) where T : Enum
        {
            int result = 0;
            failed = new();
            if (input == null || input == string.Empty) return (T)(object)result;
            string[] flags = input.Split("", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (string flag in flags)
            {
                try
                {
                    result |= (int)Enum.Parse(typeof(T), flag, true);
                }
                catch (ArgumentException)
                {
                    failed.Add(flag);
                }
            }
            return (T)(object)result;
        }
        public static T Flags<T>(Arguments.Argument[] args, int startIndex) where T : Enum
        {
            int result = 0;
            for (int i = startIndex; i < args.Length; i++)
            {
                if (!args[i].IsOption) throw new ArgumentException($"Unexpected token '{args[i].Value}'. Expected reference options.");

                try
                {
                    result |= (int)Enum.Parse(typeof(T), args[i].Value, true);
                }
                catch (ArgumentException)
                {
                    throw new ArgumentException($"ERROR: Unknown reference option '{args[i].Value}'.");
                }
            }
            return (T)(object)result;
        }

        public static V Key<T, V>(Dictionary<T, V> map, T? key, string errorMessage) where T : notnull
        {
            if (key == null) throw new ArgumentException(errorMessage);
            if (map.TryGetValue(key, out V? value))
            {
                return value;
            }
            throw new ArgumentException(errorMessage);
        }
    }

    public static class Validate
    {
        public static T Value<T>(T? value, string errorMessage)
        {
            if (value == null) throw new ArgumentException(errorMessage);
            return value;
        }
        public static T Key<T, V>(Dictionary<T,V> map, T? key, string errorMessage) where T : notnull
        {
            if (key == null) throw new ArgumentException(errorMessage);
            if (!map.ContainsKey(key)) throw new ArgumentException(errorMessage);
            return key;
        }
        public static T Key<T>(HashSet<T> map, T? key, string errorMessage)
        {
            if (key == null) throw new ArgumentException(errorMessage);
            if (!map.Contains(key)) throw new ArgumentException(errorMessage);
            return key;
        }
        public static T Key<T, V>(Dictionary<T,V> map, T? key, Func<T?, string> errorGetter) where T : notnull
        {
            return Key(map, key, errorGetter(key));
        }
        public static T Key<T>(HashSet<T> map, T? key, Func<T?, string> errorGetter) where T : notnull
        {
            return Key(map, key, errorGetter(key));
        }
    }
}
