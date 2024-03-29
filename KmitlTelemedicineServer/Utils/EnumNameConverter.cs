﻿using System.Reflection;
using System.Text.Json;
using Google.Cloud.Firestore;

namespace KmitlTelemedicineServer.Utils;

internal sealed class EnumNameConverter<T> : IFirestoreConverter<T>
    where T : struct, Enum
{
    private const string UnknownFieldName = "unknown";

    private static readonly Dictionary<string, T> NameToValueDic;
    private static readonly Dictionary<T, string> ValueToNameDic;
    private static readonly string? UnknownNameFallback;
    private static readonly T? UnknownValueFallback;

    static EnumNameConverter()
    {
        NameToValueDic = new Dictionary<string, T>();
        ValueToNameDic = new Dictionary<T, string>();

        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(field.Name);
            var value = (T)field.GetValue(null)!;

            if (name == UnknownFieldName)
            {
                UnknownNameFallback = UnknownFieldName;
                UnknownValueFallback = value;
            }
            else
            {
                NameToValueDic[name] = value;
                ValueToNameDic[value] = name;
            }
        }

        if (UnknownValueFallback.HasValue &&
            ValueToNameDic.TryGetValue(UnknownValueFallback.Value, out var fallback))
            UnknownNameFallback = fallback;
    }

    public T FromFirestore(object name)
    {
        return Parse((string)name);
    }

    public object ToFirestore(T value)
    {
        return GetStringValue(value);
    }

    public static T Parse(string value)
    {
        return TryParse(value, true)
               ?? throw new ArgumentException($"Unknown name '{value}' for enum {typeof(T).FullName}");
    }

    public static T? TryParse(string? value, bool allowUnknownFallback = false)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (NameToValueDic.TryGetValue(value, out var result)) return result;
        if (allowUnknownFallback) return UnknownValueFallback;
        return null;
    }

    public static string GetStringValue(T value)
    {
        if (ValueToNameDic.TryGetValue(value, out var result)) return result;

        return UnknownNameFallback
               ?? throw new ArgumentException($"Unknown value '{value}' for enum {typeof(T).FullName}");
    }
}