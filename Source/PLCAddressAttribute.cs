/// <summary>
/// Marks a property corresponding to a PLC memory address.
/// Supports automatic length calculation based on data type.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class PLCAddressAttribute : Attribute
{
    /// <summary>
    /// PLC address (example: "D100", "M3800")
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Data length (measured in Words).
    /// - 0 (default): automatically calculated based on data type
    /// - > 0: explicitly specified value
    /// 
    /// Auto size reference table:
    /// - bool: 1 Word (1 device bit)
    /// - short/int16: 1 Word (16-bit)
    /// - int32: 2 Words (32-bit)
    /// - float32: 2 Words (32-bit)
    /// - double/int64: 4 Words (64-bit)
    /// - string: 1 Word/character (must be specified explicitly)
    /// </summary>
    public ushort Length { get; set; } = 0;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    public PLCAddressAttribute(string address, ushort length = 0, string? description = null)
    {
        Address = address;
        Length = length;
        Description = description;
    }
}
