using System.Reflection;
using System.Text;
/// <summary>
/// Extension methods for PLCDataContext to ease PLC data handling.
/// </summary>
public static class PLCDataExtensions
{
    /// <summary>
    /// Gets all properties marked with PLCAddress.
    /// </summary>
    public static List<(PropertyInfo Property, PLCAddressAttribute Address)> GetPLCProperties(this PLCDataContext obj)
    {
        var result = new List<(PropertyInfo, PLCAddressAttribute)>();
        var properties = obj.GetType().GetProperties();

        foreach (var prop in properties)
        {
            var attr = prop.GetCustomAttribute<PLCAddressAttribute>();
            if (attr != null)
                result.Add((prop, attr));
        }
        return result;
    }

    /// <summary>
    /// Creates a detailed report of properties and their PLC addresses.
    /// </summary>
    public static string GetPLCMapping(this PLCDataContext obj)
    {
        var sb = new StringBuilder();
        var props = obj.GetPLCProperties();

        sb.AppendLine($"=== PLC Data Mapping for {obj.GetType().Name} ===");
        sb.AppendLine();

        foreach (var (prop, addr) in props)
        {
            var value = prop.GetValue(obj);
            var displayValue = value?.ToString() ?? "(null)";
            if (value is short[] arr)
                displayValue = $"[{string.Join(", ", arr)}]";

            sb.AppendLine($"Property: {prop.Name}");
            sb.AppendLine($"  Address: {addr.Address}");
            sb.AppendLine($"  Length: {addr.Length}");
            sb.AppendLine($"  Type: {prop.PropertyType.Name}");
            sb.AppendLine($"  Value: {displayValue}");
            if (!string.IsNullOrEmpty(addr.Description))
                sb.AppendLine($"  Description: {addr.Description}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Clones values from one PLC data object to another (of the same type).
    /// </summary>
    public static void CopyValuesFrom(this PLCDataContext destination, PLCDataContext source)
    {
        if (destination.GetType() != source.GetType())
            throw new ArgumentException("Source and destination must be of the same type");

        var props = destination.GetPLCProperties();
        foreach (var (prop, _) in props)
        {
            if (prop.CanRead && prop.CanWrite)
            {
                var value = prop.GetValue(source);
                prop.SetValue(destination, value);
            }
        }
    }

    /// <summary>
    /// Compares two PLC data objects.
    /// </summary>
    public static bool ValuesEqual(this PLCDataContext obj1, PLCDataContext obj2)
    {
        if (obj1.GetType() != obj2.GetType()) return false;

        var props = obj1.GetPLCProperties();
        foreach (var (prop, _) in props)
        {
            var val1 = prop.GetValue(obj1);
            var val2 = prop.GetValue(obj2);

            if (!Equals(val1, val2))
            {
                // Special handling for arrays
                if (val1 is short[] arr1 && val2 is short[] arr2)
                    if (!arr1.SequenceEqual(arr2)) return false;
                    else return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Exports PLC data to a Dictionary.
    /// </summary>
    public static Dictionary<string, object?> ExportToDictionary(this PLCDataContext obj)
    {
        var result = new Dictionary<string, object?>();
        var props = obj.GetPLCProperties();

        foreach (var (prop, attr) in props)
        {
            var value = prop.GetValue(obj);
            result[attr.Address] = value;
        }

        return result;
    }

    /// <summary>
    /// Imports PLC data from a Dictionary.
    /// </summary>
    public static void ImportFromDictionary(this PLCDataContext obj, Dictionary<string, object?> data)
    {
        var props = obj.GetPLCProperties();

        foreach (var (prop, attr) in props)
        {
            if (data.TryGetValue(attr.Address, out var value))
            {
                try { prop.SetValue(obj, value); }
                catch { }
            }
        }
    }
}
