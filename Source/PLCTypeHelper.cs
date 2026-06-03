using System.Reflection;

/// <summary>
/// Utility class for handling PLC data types.
/// Automatically calculates the number of Words required based on data type.
/// </summary>
public static class PLCTypeHelper
{
    /// <summary>
    /// Data type mapping table to required number of Words.
    /// 
    /// Reference:
    /// - bit: 1 device bit = 1 Word
    /// - short / int16: 16-bit = 1 Word
    /// - int32: 32-bit = 2 Words
    /// - float32: 32-bit = 2 Words
    /// - double: 64-bit = 4 Words
    /// - string: depends on length, default = 1 Word/character
    /// </summary>
    public static ushort GetWordLength(Type propertyType)
    {
        if (propertyType == null)
            throw new ArgumentNullException(nameof(propertyType));

        // Primitive types
        if (propertyType == typeof(bool))
            return 1; // 1 device bit

        if (propertyType == typeof(short) || propertyType == typeof(ushort) || propertyType == typeof(char))
            return 1; // 16-bit = 1 Word

        if (propertyType == typeof(int) || propertyType == typeof(uint) || propertyType == typeof(float))
            return 2; // 32-bit = 2 Words

        if (propertyType == typeof(decimal))
            return 8; // 128-bit = 8 Words

        if (propertyType == typeof(double) || propertyType == typeof(long) || propertyType == typeof(ulong))
            return 4; // 64-bit = 4 Words

        // String: default 1 Word (measured in characters)
        if (propertyType == typeof(string))
            return 1; // Will be overridden by Length in attribute if needed

        // Array types
        if (propertyType == typeof(bool[]))
            return 1; // Arrays will be handled separately

        if (propertyType == typeof(short[]) || propertyType == typeof(ushort[]) || propertyType == typeof(char[]))
            return 1; // Arrays will be handled separately

        if (propertyType == typeof(int[]) || propertyType == typeof(uint[]) || propertyType == typeof(float[]))
            return 2; // Arrays will be handled separately

        // Default: 1 Word
        return 1;
    }

    /// <summary>
    /// Gets the effective length of a property, automatically calculating if Length = 0 (auto).
    /// </summary>
    public static ushort GetEffectiveLength(PropertyInfo property, PLCAddressAttribute attr)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        if (attr == null)
            throw new ArgumentNullException(nameof(attr));

        // If Length is explicitly specified (> 0), use that value
        if (attr.Length > 0) return attr.Length;

        // Automatically calculate based on data type
        return GetWordLength(property.PropertyType);
    }

    /// <summary>
    /// Checks if a data type requires explicit Length specification.
    /// - String: yes (to know buffer size)
    /// - Array: yes (to know element count)
    /// - Other types: no
    /// </summary>
    public static bool NeedsExplicitLength(Type propertyType)
    {
        return propertyType == typeof(string) || (propertyType.IsArray && propertyType != typeof(bool[]));
    }
}
