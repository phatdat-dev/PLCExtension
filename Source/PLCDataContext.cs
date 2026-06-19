using System.Reflection;
using System.Text;
using System.ComponentModel;
using McpXLib;
using McpXLib.Enums;
using Newtonsoft.Json;
using System.Linq.Expressions;

/// <summary>
/// Base class providing automatic read/write mechanism for PLC data based on marked properties.
/// Supports INotifyPropertyChanged for property change tracking.
/// </summary>
public abstract partial class PLCDataContext : INotifyPropertyChanged
{
    [JsonIgnore]
    private McpX PlcDevice { get; set; }
    [JsonIgnore]
    private Dictionary<string, PLCAddressAttribute>? mapping;

    /// <summary>
    /// Event triggered when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Event triggered when a property changes (with old value).
    /// </summary>
    public event Action<string, object?, object?>? PropertyValueChanged;
    public PLCDataContext(McpX _plcDevice) { PlcDevice = _plcDevice; }
    public PLCDataContext(McpX _plcDevice, Dictionary<string, PLCAddressAttribute> _mapping)
    {
        PlcDevice = _plcDevice;
        mapping = _mapping;
    }

    private readonly SemaphoreSlim PlcLock = new(1, 1);

    private IEnumerable<PropertyInfo> Properties => GetType().GetProperties();

    /// <summary>
    /// CallBack for custom logging or debugging. Can be overridden by derived classes to implement specific logging behavior.
    /// </summary>
    /// <param name="message"></param>
    protected virtual string CustomMessage(string message) => message;

    /// <summary>
    /// Gets PLCAddress information from mapping or attribute.
    /// Prioritizes mapping if available, otherwise uses attribute from property.
    /// </summary>
    protected PLCAddressAttribute? GetPLCAddressInfo(PropertyInfo property)
    {
        var attr = property.GetCustomAttribute<PLCAddressAttribute>();

        if (mapping?.TryGetValue(property.Name, out var fieldMap) != true)
            return attr;

        var result = new PLCAddressAttribute(
            fieldMap?.Address ?? attr?.Address ?? string.Empty,
            fieldMap?.Length ?? attr?.Length ?? 0,
            fieldMap?.Description ?? attr?.Description,
            fieldMap?.ReadOnly ?? attr?.ReadOnly ?? false
        );

        return string.IsNullOrEmpty(result.Address) ? null : result;
    }

    /// <summary>
    /// Parses address strings like "D100" or "M3800" into (Prefix, address).
    /// </summary>
    protected (Prefix prefix, string address) ParseAddress(string addressStr)
    {
        if (string.IsNullOrEmpty(addressStr))
            throw new ArgumentException("Address cannot be null or empty");

        char deviceChar = addressStr[0];
        string addressPart = addressStr.Substring(1);

        var prefixes = Enum.GetValues(typeof(Prefix)).Cast<Prefix>().ToList();
        var matchedPrefix = prefixes.FirstOrDefault(p => Enum.GetName(typeof(Prefix), p) == deviceChar.ToString());

        if (matchedPrefix == default)
        {
            var supportedTypes = string.Join(", ", prefixes.Select(p => Enum.GetName(typeof(Prefix), p)));
            throw new ArgumentException($"Unknown device type: {deviceChar}. Supported types: {supportedTypes}");
        }

        return (matchedPrefix, addressPart);
    }

    /// <summary>
    /// Reads all properties from PLC into the object.
    /// Sequential version that reads properties one by one. Suitable for small number of properties or when order matters.
    /// </summary>
    public virtual async Task LoadAllSequentialAsync()
    {
        foreach (var property in Properties)
            await LoadPropertyAsync(property);
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Reads all properties from PLC into the object in parallel.
    /// Uses Parallel.ForEachAsync to read multiple properties concurrently, improving performance for large number of properties. Be cautious of PLC read limits and ensure thread safety in PLC access.
    /// </summary>
    /// <returns></returns>
    public virtual async Task LoadAllParallelAsync()
    {
        await Parallel.ForEachAsync(Properties, async (property, ct) => await LoadPropertyAsync(property));
    }
#endif

    /// <summary>
    /// Reads all properties from PLC into the object using Task.WhenAll for maximum concurrency.
    /// </summary>
    /// <returns></returns>
    public virtual async Task LoadAllTasksAsync()
    {
        var tasks = Properties.Select(async property => await LoadPropertyAsync(property));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Writes all properties from object to PLC.
    /// Supports dynamic JSON mapping or property attributes.
    /// </summary>
    public virtual async Task SaveAllSequentialAsync()
    {
        foreach (var property in Properties)
            await SavePropertyAsync(property);
    }

#if NET6_0_OR_GREATER
    /// <summary>    /// Writes all properties from object to PLC in parallel.
    /// Uses Parallel.ForEachAsync for concurrent writes, improving performance for large number of properties. Be cautious of PLC write limits and ensure thread safety in PLC access.
    /// </summary>
    public virtual async Task SaveAllParallelAsync()
    {
        await Parallel.ForEachAsync(Properties, async (property, ct) => await SavePropertyAsync(property));
    }
#endif

    /// <summary>
    /// Writes all properties from object to PLC using Task.WhenAll for maximum concurrency.
    /// </summary>
    /// <returns></returns>
    public virtual async Task SaveAllTasksAsync()
    {
        var tasks = Properties.Select(async property => await SavePropertyAsync(property));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Reads a specific property from PLC and updates the property value.
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    public async Task LoadPropertyAsync(PropertyInfo property)
    {
        try
        {
            var attr = GetPLCAddressInfo(property);
            if (attr == null) return;
            var value = await ReadValueAsync(property, attr);
            SetPropertyValue(property, value);
        }
        catch (Exception ex)
        {
            Printt.Red(CustomMessage($"Error loading property {property.Name}: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    /// Writes a specific property value to PLC.
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    public async Task SavePropertyAsync(PropertyInfo property)
    {
        try
        {
            var attr = GetPLCAddressInfo(property);
            if (attr == null) return;
            var value = property.GetValue(this);
            await WriteValueAsync(property, attr, value);
        }
        catch (Exception ex)
        {
            Printt.Red(CustomMessage($"Error saving property {property.Name}: {ex.Message}"));
            throw;
        }
    }

    public async Task SavePropertyAsync(string propertyName)
    {
        var property = GetType().GetProperty(propertyName);
        if (property == null) return;
        await SavePropertyAsync(property);
    }

    /// <summary>
    /// Reads a specific property from PLC.
    /// </summary>
    public async Task<object?> ReadValueAsync(string propertyName)
    {
        var property = GetType().GetProperty(propertyName);
        if (property == null) return null;

        var attr = GetPLCAddressInfo(property);
        if (attr == null) return null;

        return await ReadValueAsync(property, attr);
    }

    /// <summary>
    /// Writes a specific property to PLC.
    /// </summary>
    public async Task WriteValueAsync(string propertyName, object? value)
    {
        var property = GetType().GetProperty(propertyName);
        if (property == null) return;

        var attr = GetPLCAddressInfo(property);
        if (attr == null) return;

        await WriteValueAsync(property, attr, value);
    }

    /// <summary>
    /// Internal: Reads value from PLC based on data type.
    /// </summary>
    public async Task<object?> ReadValueAsync(PropertyInfo property, PLCAddressAttribute attr)
    {
        var propertyType = property.PropertyType;
        var effectiveLength = PLCTypeHelper.GetEffectiveLength(property, attr);

        if (propertyType == typeof(bool))
        {
            return await ReadBoolAsync(attr.Address);
        }
        else if (propertyType == typeof(string))
        {
            var words = await ReadWordsAsync(attr.Address, effectiveLength);
            return WordsToString(words);
        }
        else if (propertyType == typeof(int))
        {
            var words = await ReadWordsAsync(attr.Address, effectiveLength);
            if (words.Length >= 2)
                return WordsToInt32(words[0], words[1]);
            return 0;
        }
        else if (propertyType == typeof(float))
        {
            var words = await ReadWordsAsync(attr.Address, effectiveLength);
            if (words.Length >= 2)
                return WordsToFloat(words[0], words[1]);
            return 0f;
        }
        else if (propertyType == typeof(double))
        {
            var words = await ReadWordsAsync(attr.Address, effectiveLength);
            if (words.Length >= 4)
                return WordsToDouble(words);
            return 0d;
        }
        else if (propertyType == typeof(decimal))
        {
            var words = await ReadWordsAsync(attr.Address, effectiveLength);
            if (words.Length >= 8)
                return WordsToDecimal(words);
            return 0m;
        }
        else if (propertyType == typeof(short[]))
        {
            return await ReadWordsAsync(attr.Address, effectiveLength);
        }

        return null;
    }

    /// <summary>
    /// Internal: Writes value to PLC based on data type.
    /// </summary>
    public async Task WriteValueAsync(PropertyInfo property, PLCAddressAttribute attr, object? value)
    {
        if (attr.ReadOnly) return; // Skip read-only properties
        var propertyType = property.PropertyType;
        var effectiveLength = PLCTypeHelper.GetEffectiveLength(property, attr);

        if (propertyType == typeof(bool) && value is bool boolValue)
        {
            await WriteBoolAsync(attr.Address, boolValue);
        }
        else if (propertyType == typeof(string) && value is string strValue)
        {
            var words = StringToWords(strValue, effectiveLength);
            await WriteWordsAsync(attr.Address, words);
        }
        else if (propertyType == typeof(int) && value is int intValue)
        {
            var words = Int32ToWords(intValue);
            await WriteWordsAsync(attr.Address, words);
        }
        // numeric types: float (2 words), double (4 words), decimal (8 words)
        else if (propertyType == typeof(float) && value is float floatValue)
        {
            var words = FloatToWords(floatValue);
            await WriteWordsAsync(attr.Address, words);
        }
        else if (propertyType == typeof(double) && value is double doubleValue)
        {
            var words = DoubleToWords(doubleValue);
            await WriteWordsAsync(attr.Address, words);
        }
        else if (propertyType == typeof(decimal) && value is decimal decimalValue)
        {
            var words = DecimalToWords(decimalValue);
            await WriteWordsAsync(attr.Address, words);
        }
        else if (propertyType == typeof(short[]) && value is short[] arrValue)
        {
            await WriteWordsAsync(attr.Address, arrValue);
        }
    }

    public string ToJsonString() => JsonConvert.SerializeObject(this);

    #region ==================== HELPERS ====================

    private static string ExtractPropertyName<T>(
        Expression<Func<PLCDataContext, T>> expression)
    {
        var names = new Stack<string>();
        Expression? current = expression.Body;

        while (current is MemberExpression member)
        {
            names.Push(member.Member.Name);
            current = member.Expression;
        }

        return string.Join(".", names);
    }

    /// <summary>
    /// Raises PropertyChanged event when property changes.
    /// Used by derived classes to notify that a property has changed.
    /// </summary>
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises PropertyValueChanged event with old and new values.
    /// Used by derived classes to notify changes with old and new values.
    /// </summary>
    protected void OnPropertyValueChanged(string propertyName, object? oldValue, object? newValue)
    {
        PropertyValueChanged?.Invoke(propertyName, oldValue, newValue);
    }

    /// <summary>
    /// Helper method to set property value and raise events.
    /// </summary>
    private void SetPropertyValue(PropertyInfo property, object? newValue)
    {
        var oldValue = property.GetValue(this);
        if (ValuesAreEqual(oldValue, newValue)) return;

        property.SetValue(this, newValue);

        // Raise events
        OnPropertyChanged(property.Name);
        OnPropertyValueChanged(property.Name, oldValue, newValue);
        Printt.Default(CustomMessage($"[CHANGE] {GetType().Name}.{property.Name}: {oldValue} → {newValue}"));
    }

    private static bool ValuesAreEqual(object? oldValue, object? newValue)
    {
        if (oldValue is short[] oldArray && newValue is short[] newArray)
            return oldArray.SequenceEqual(newArray);

        return Equals(oldValue, newValue);
    }

    #endregion
}

/// <summary>
/// Low-level PLC read/write operations based on data type and address parsing.
/// </summary>
public abstract partial class PLCDataContext
{
    /// <summary>
    /// Reads a bool value from the PLC.
    /// </summary>
    private async Task<bool?> ReadBoolAsync(string address)
    {
        await PlcLock.WaitAsync();
        try
        {
            var (prefix, addr) = ParseAddress(address);
            return PlcDevice.Read<bool>(prefix, addr);
        }
        catch (Exception ex)
        {
            Printt.Red(CustomMessage($"Error ReadBoolAsync at {address}: {ex.Message}"));
            throw;
        }
        finally { PlcLock.Release(); }
    }

    /// <summary>
    /// Writes a bool value to the PLC.
    /// </summary>
    private async Task WriteBoolAsync(string address, bool value)
    {
        await PlcLock.WaitAsync();
        try
        {
            var (prefix, addr) = ParseAddress(address);
            PlcDevice.Write<bool>(prefix, addr, value);
        }
        catch (Exception ex)
        {
            Printt.Red(CustomMessage($"Error writing bool to PLC at {address}: {ex.Message}"));
            throw;
        }
        finally { PlcLock.Release(); }
    }

    /// <summary>
    /// Reads a Word array from the PLC.
    /// </summary>
    private async Task<short[]> ReadWordsAsync(string address, ushort length)
    {
        await PlcLock.WaitAsync();
        try
        {
            var (prefix, addr) = ParseAddress(address);
            return PlcDevice.BatchRead<short>(prefix, addr, length);
        }
        catch
        {
            Printt.Red(CustomMessage($"Error ReadWordsAsync at {address} with length {length}"));
            throw;
        }
        finally { PlcLock.Release(); }
    }

    /// <summary>
    /// Writes a Word array to the PLC.
    /// </summary>
    private async Task WriteWordsAsync(string address, short[] data)
    {
        await PlcLock.WaitAsync();
        try
        {
            var (prefix, addr) = ParseAddress(address);
            PlcDevice.BatchWrite<short>(prefix, addr, data);
        }
        catch (Exception ex)
        {
            Printt.Red(CustomMessage($"Error writing words to PLC at {address}: {ex.Message}"));
            throw;
        }
        finally { PlcLock.Release(); }
    }

    /// <summary>
    /// Converts Word array to string.
    /// </summary>
    private static string WordsToString(short[] words)
    {
        if (words == null || words.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (short w in words)
        {
            byte low = (byte)(w & 0xFF);
            byte high = (byte)((w >> 8) & 0xFF);
            if (low != 0) sb.Append((char)low);
            if (high != 0) sb.Append((char)high);
        }
        return sb.ToString().Trim().Replace("\0", "");
    }

    /// <summary>
    /// Converts string to Word array.
    /// </summary>
    private static short[] StringToWords(string text, int maxWords = 6)
    {
        text = (text ?? "").PadRight(maxWords * 2).Substring(0, maxWords * 2);
        var result = new short[maxWords];
        for (int i = 0; i < maxWords; i++)
        {
            char c1 = text[i * 2];
            char c2 = text[i * 2 + 1];
            result[i] = (short)(c1 | (c2 << 8));
        }
        return result;
    }

    /// <summary>
    /// Converts 2 Words to Float.
    /// </summary>
    private static float WordsToFloat(short low, short high)
    {
        var bytes = new byte[4];
        BitConverter.GetBytes(low).CopyTo(bytes, 0);
        BitConverter.GetBytes(high).CopyTo(bytes, 2);
        return (float)Math.Round(BitConverter.ToSingle(bytes, 0), 2);
    }

    /// <summary>
    /// Converts Float to 2 Words.
    /// </summary>
    private static short[] FloatToWords(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new short[] { BitConverter.ToInt16(bytes, 0), BitConverter.ToInt16(bytes, 2) };
    }

    /// <summary>
    /// Converts 4 Words to Double.
    /// </summary>
    private static double WordsToDouble(short[] words)
    {
        if (words == null || words.Length < 4) return 0d;
        var bytes = new byte[8];
        BitConverter.GetBytes(words[0]).CopyTo(bytes, 0);
        BitConverter.GetBytes(words[1]).CopyTo(bytes, 2);
        BitConverter.GetBytes(words[2]).CopyTo(bytes, 4);
        BitConverter.GetBytes(words[3]).CopyTo(bytes, 6);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Converts Double to 4 Words.
    /// </summary>
    private static short[] DoubleToWords(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new short[] {
            BitConverter.ToInt16(bytes, 0),
            BitConverter.ToInt16(bytes, 2),
            BitConverter.ToInt16(bytes, 4),
            BitConverter.ToInt16(bytes, 6)
        };
    }

    /// <summary>
    /// Converts 8 Words to Decimal.
    /// </summary>
    private static decimal WordsToDecimal(short[] words)
    {
        if (words == null || words.Length < 8) return 0m;
        var bytes = new byte[16];
        for (int i = 0; i < 8; i++)
            BitConverter.GetBytes(words[i]).CopyTo(bytes, i * 2);

        var ints = new int[4];
        for (int i = 0; i < 4; i++)
            ints[i] = BitConverter.ToInt32(bytes, i * 4);

        return new decimal(ints);
    }

    /// <summary>
    /// Converts Decimal to 8 Words.
    /// </summary>
    private static short[] DecimalToWords(decimal value)
    {
        var bits = decimal.GetBits(value);
        var bytes = new byte[16];
        for (int i = 0; i < 4; i++)
            BitConverter.GetBytes(bits[i]).CopyTo(bytes, i * 4);

        var result = new short[8];
        for (int i = 0; i < 8; i++)
            result[i] = BitConverter.ToInt16(bytes, i * 2);

        return result;
    }

    /// <summary>
    /// Converts 2 Words to Int32.
    /// </summary>
    private static int WordsToInt32(short low, short high)
    {
        var bytes = new byte[4];
        BitConverter.GetBytes(low).CopyTo(bytes, 0);
        BitConverter.GetBytes(high).CopyTo(bytes, 2);
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// Converts Int32 to 2 Words.
    /// </summary>
    private static short[] Int32ToWords(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new short[] { BitConverter.ToInt16(bytes, 0), BitConverter.ToInt16(bytes, 2) };
    }
}

/// <summary>
/// Provides methods to subscribe to property changes with various signatures (sync/async, with/without old value).
/// </summary>
public abstract partial class PLCDataContext
{
    public IDisposable WhenPropertyChanges(string propertyName, Action<object?> onChanged)
    {
        return SubscribePropertyChanged(
            propertyName,
            value => onChanged?.Invoke(value));
    }

    public IDisposable WhenPropertyChanges<T>(string propertyName, Action<T> onChanged)
    {
        return SubscribePropertyChanged(
            propertyName,
            value => { if (value is T typed) onChanged?.Invoke(typed); });
    }

    public IDisposable WhenPropertyChanges<T>(string propertyName, Func<T, Task> onChanged)
    {
        return SubscribePropertyChanged(
            propertyName,
            async value => { if (value is T typed) await onChanged(typed); });
    }

    public IDisposable WhenPropertyChanges(string propertyName, Action<object?, object?> onChanged)
    {
        return SubscribePropertyChanged(
            propertyName,
            (oldValue, newValue) => onChanged?.Invoke(oldValue, newValue));
    }

    public IDisposable WhenPropertyChanges<T>(string propertyName, Action<T, T> onChanged)
    {
        return SubscribePropertyChanged(
            propertyName,
            (oldValue, newValue) =>
            {
                if (oldValue is T oldTyped && newValue is T newTyped)
                    onChanged?.Invoke(oldTyped, newTyped);
            });
    }

    public IDisposable WhenPropertyChanges<T>(string propertyName, Func<T, T, Task> onChanged)
    {
        return SubscribePropertyChanged(
            propertyName,
            async (oldValue, newValue) =>
            {
                if (oldValue is T oldTyped && newValue is T newTyped)
                    await onChanged(oldTyped, newTyped);
            });
    }

    // ---------- EXPRESSION BASED ----------

    public IDisposable WhenPropertyChanges<T>(Expression<Func<PLCDataContext, T>> selector, Action<T> onChanged)
    {
        return WhenPropertyChanges(ExtractPropertyName(selector), onChanged);
    }

    public IDisposable WhenPropertyChanges<T>(Expression<Func<PLCDataContext, T>> selector, Func<T, Task> onChanged)
    {
        return WhenPropertyChanges(ExtractPropertyName(selector), onChanged);
    }

    public IDisposable WhenPropertyChanges<T>(Expression<Func<PLCDataContext, T>> selector, Action<T, T> onChanged)
    {
        return WhenPropertyChanges(ExtractPropertyName(selector), onChanged);
    }

    public IDisposable WhenPropertyChanges<T>(Expression<Func<PLCDataContext, T>> selector, Func<T, T, Task> onChanged)
    {
        return WhenPropertyChanges(ExtractPropertyName(selector), onChanged);
    }

    /// <summary>
    /// **ANTI-SPAM PATTERN**
    /// 
    /// Debounces property changes to avoid spam API calls.
    /// Very important for QR scanner (often written multiple times) or noisy sensors.
    /// 
    /// Example:
    /// ```
    /// _controlPLC.ScanRollScreen.WhenPropertyChangesDebounced(
    ///     nameof(MainRollQRCode),
    ///     debounceMs: 300,
    ///     async (qr) => await HandleScanQRAsync(qr));
    /// ```
    /// </summary>
    public IDisposable WhenPropertyChangesDebounced<T>(string propertyName, int debounceMs, Func<T, Task> handler)
    {
        CancellationTokenSource? cts = null;
        var property = GetType().GetProperty(propertyName);

        PropertyChangedEventHandler eventHandler = async (_, e) =>
        {
            if (e.PropertyName != propertyName || property == null) return;

            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            try
            {
                await Task.Delay(debounceMs, token);
                if (!token.IsCancellationRequested && property.GetValue(this) is T value)
                    await handler(value);
            }
            catch (OperationCanceledException) { }
        };

        PropertyChanged += eventHandler;
        return new PropertyChangeSubscription(() =>
        {
            PropertyChanged -= eventHandler;
            cts?.Dispose();
        });
    }

    /// <summary>
    /// Debounced property change detection with old/new values.
    /// </summary>
    public IDisposable WhenPropertyChangesDebounced<T>(string propertyName, int debounceMs, Func<T, T, Task> handler)
    {
        CancellationTokenSource? cts = null;

        return SubscribePropertyChanged(
            propertyName,
            async (oldValue, newValue) =>
            {
                cts?.Cancel();
                cts = new CancellationTokenSource();
                var token = cts.Token;

                try
                {
                    await Task.Delay(debounceMs, token);
                    if (!token.IsCancellationRequested && oldValue is T oldT && newValue is T newT)
                        await handler(oldT, newT);
                }
                catch (OperationCanceledException) { }
            });
    }

    private IDisposable SubscribePropertyChanged(string propertyName, Action<object?> handler)
    {
        var property = GetType().GetProperty(propertyName);

        PropertyChangedEventHandler eventHandler = (_, e) =>
        {
            if (e.PropertyName != propertyName || property == null) return;
            handler(property.GetValue(this));
        };

        PropertyChanged += eventHandler;
        return new PropertyChangeSubscription(() => PropertyChanged -= eventHandler);
    }

    private IDisposable SubscribePropertyChanged(string propertyName, Func<object?, Task> handler)
    {
        var property = GetType().GetProperty(propertyName);

        PropertyChangedEventHandler eventHandler = async (_, e) =>
        {
            if (e.PropertyName != propertyName || property == null) return;
            await handler(property.GetValue(this));
        };

        PropertyChanged += eventHandler;
        return new PropertyChangeSubscription(() => PropertyChanged -= eventHandler);
    }

    private IDisposable SubscribePropertyChanged(string propertyName, Action<object?, object?> handler)
    {
        Action<string, object?, object?> eventHandler =
            (name, oldValue, newValue) =>
            {
                if (name != propertyName) return;
                handler(oldValue, newValue);
            };

        PropertyValueChanged += eventHandler;
        return new PropertyChangeSubscription(() => PropertyValueChanged -= eventHandler);
    }

    private IDisposable SubscribePropertyChanged(string propertyName, Func<object?, object?, Task> handler)
    {
        Action<string, object?, object?> eventHandler =
            async (name, oldValue, newValue) =>
            {
                if (name != propertyName) return;
                await handler(oldValue, newValue);
            };

        PropertyValueChanged += eventHandler;
        return new PropertyChangeSubscription(() => PropertyValueChanged -= eventHandler);
    }
}

/// <summary>
/// Helper class to unsubscribe from property changes.
/// </summary>
internal class PropertyChangeSubscription : IDisposable
{
    private Action? _unsubscribe;

    public PropertyChangeSubscription(Action? unsubscribe)
    {
        _unsubscribe = unsubscribe;
    }

    public void Dispose()
    {
        _unsubscribe?.Invoke();
        _unsubscribe = null;
        GC.SuppressFinalize(this);
    }
}