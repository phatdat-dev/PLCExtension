# Language

[Vietnamese](README-VI.md) | [English](README.md)

# PLCExtension

`PLCExtension` is a .NET library that maps PLC data to strongly typed C# properties. Instead of manually reading and writing individual PLC addresses, define a class that inherits from `PLCDataContext`, map properties with `[PLCAddress]` or runtime mappings, then load and save data through the object.

The package supports `netstandard2.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`. PLC communication is handled through `McpX`.

## Installation

```bash
dotnet add package PLCExtension
```

## Quick Start

```csharp
using McpXLib;

public class QRScanData : PLCDataContext
{
    public QRScanData(McpX plc) : base(plc) { }

    [PLCAddress("M6011", description: "QR scan command")]
    public bool ScanCommand { get; set; }

    [PLCAddress("M6012", description: "OK status")]
    public bool OkStatus { get; set; }

    [PLCAddress("D6200", length: 100, description: "QR buffer")]
    public string QRBarcode { get; set; } = "";

    [PLCAddress("D6300", description: "Speed")]
    public int RollSpeed { get; set; }

    [PLCAddress("D6310", description: "Length")]
    public float RollLength { get; set; }
}
```

Usage:

```csharp
var data = new QRScanData(plc);

await data.LoadAllSequentialAsync();

if (data.ScanCommand)
{
    data.OkStatus = true;
    data.RollSpeed = 500;
    data.RollLength = 120.5f;

    await data.SaveAsync();
}
```

## How PLCDataContext Works

`PLCDataContext` finds properties that have PLC address metadata, reads the corresponding data from the PLC, converts it to the matching C# type, and sets the value on the object. When writing, it takes the current property value, converts it to the required bit/word representation, and writes it back to the PLC.

PLC address metadata can come from:

- A `[PLCAddress]` attribute directly on the property.
- A dictionary passed to `PLCDataContext(McpX, Dictionary<string, PLCAddressAttribute>)`.

If a property has both an attribute and a runtime mapping, the runtime mapping takes priority. Missing runtime mapping fields fall back to the attribute when available.

## Attribute-Based Mapping

```csharp
public class MachineStatusData : PLCDataContext
{
    public MachineStatusData(McpX plc) : base(plc) { }

    [PLCAddress("M100", description: "Machine is running")]
    public bool IsRunning { get; set; }

    [PLCAddress("D200", length: 20, description: "Product code")]
    public string ProductCode { get; set; } = "";

    [PLCAddress("D230", readOnly: true, description: "Read-only counter")]
    public int TotalCounter { get; set; }
}
```

Attribute parameters:

| Parameter     | Description                                                                                         |
| ------------- | --------------------------------------------------------------------------------------------------- |
| `address`     | PLC address, for example `M100` or `D200`. The first character is parsed as `McpXLib.Enums.Prefix`. |
| `length`      | Number of words to read/write. If `0`, the library calculates the length from the property type.    |
| `description` | Optional description for documenting the mapping.                                                   |
| `readOnly`    | If `true`, `SaveAsync()` and `WriteValueAsync()` skip this property when writing.                   |

## Runtime Mapping

Use runtime mapping when PLC addresses need to be configured outside the codebase or vary by machine, line, or PLC version.

```csharp
public class MachineStatusData : PLCDataContext
{
    public MachineStatusData(McpX plc, Dictionary<string, PLCAddressAttribute> mapping)
        : base(plc, mapping) { }

    public bool IsRunning { get; set; }
    public string ProductCode { get; set; } = "";
    public int TotalCounter { get; set; }
}
```

Example `mapping.json`:

```json
{
  "IsRunning": {
    "Address": "M100",
    "Length": 0,
    "Description": "Machine is running",
    "ReadOnly": false
  },
  "ProductCode": {
    "Address": "D200",
    "Length": 20,
    "Description": "Product code",
    "ReadOnly": false
  },
  "TotalCounter": {
    "Address": "D230",
    "Length": 0,
    "Description": "Read-only counter",
    "ReadOnly": true
  }
}
```

Load the mapping:

```csharp
using Newtonsoft.Json;

var json = File.ReadAllText("mapping.json");
var mapping = JsonConvert.DeserializeObject<Dictionary<string, PLCAddressAttribute>>(json)
              ?? new Dictionary<string, PLCAddressAttribute>();

var data = new MachineStatusData(plc, mapping);
await data.LoadAllSequentialAsync();
```

## Read and Write API

| Method                                                | Description                                                                                                           |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| `LoadAllSequentialAsync()`                            | Reads all mapped properties one by one. This is the safest default option.                                            |
| `LoadAllParallelAsync()`                              | Reads all mapped properties with `Parallel.ForEachAsync`. Use when the PLC/driver can handle concurrent requests.     |
| `LoadAllTasksAsync()`                                 | Creates one task per mapped property and waits with `Task.WhenAll`. This is the highest-concurrency load mode.        |
| `SaveAsync()`                                         | Writes all mapped readable properties. Properties marked `readOnly` are skipped.                                      |
| `ReadValueAsync(string propertyName)`                 | Reads one property from the PLC and returns the value. It does not set the value on the object.                       |
| `WriteValueAsync(string propertyName, object? value)` | Writes a value to the PLC address mapped to the property.                                                             |
| `ToJsonString()`                                      | Serializes the current object with `Newtonsoft.Json`. Internal fields such as the PLC device and mapping are ignored. |

Read or write a single property:

```csharp
var current = await data.ReadValueAsync(nameof(MachineStatusData.TotalCounter));
await data.WriteValueAsync(nameof(MachineStatusData.ProductCode), "ROLL-001");
```

## Supported Data Types

`PLCDataContext` directly reads and writes the following types:

| C# type   |      Default words | Notes                                                                                 |
| --------- | -----------------: | ------------------------------------------------------------------------------------- |
| `bool`    |                  1 | Uses `Read<bool>` and `Write<bool>`.                                                  |
| `string`  | 1 if not specified | Declare `length` explicitly to define the buffer size. Each word stores 2 characters. |
| `int`     |                  2 | Converted through 2 words using `BitConverter` little-endian layout.                  |
| `float`   |                  2 | Read values are rounded to 2 decimal places.                                          |
| `double`  |                  4 | Converted through 4 words.                                                            |
| `decimal` |                  8 | Converted through 8 words.                                                            |
| `short[]` |      From `length` | Raw word buffer.                                                                      |

`PLCTypeHelper` defines default lengths for additional types such as `short`, `long`, and other arrays, but `PLCDataContext.ReadValueAsync` and `WriteValueAsync` currently handle only the types listed above directly.

## Property Change Tracking

`PLCDataContext` implements `INotifyPropertyChanged`. When `LoadAllSequentialAsync()`, `LoadAllParallelAsync()`, or `LoadAllTasksAsync()` reads a value that differs from the current property value, the object will:

- Set the new property value.
- Raise `PropertyChanged`.
- Raise `PropertyValueChanged` with `oldValue` and `newValue`.
- Print a log in the format `[CHANGE] Class.Property: old -> new`.

Subscribe by property name:

```csharp
var subscription = data.WhenPropertyChanges<bool>(
    nameof(MachineStatusData.IsRunning),
    isRunning =>
    {
        Console.WriteLine($"IsRunning = {isRunning}");
    });

// Unsubscribe when it is no longer needed.
subscription.Dispose();
```

Subscribe with old/new values:

```csharp
data.WhenPropertyChanges<int>(
    nameof(MachineStatusData.TotalCounter),
    (oldValue, newValue) =>
    {
        Console.WriteLine($"Counter: {oldValue} -> {newValue}");
    });
```

Use an async handler:

```csharp
data.WhenPropertyChanges<string>(
    nameof(MachineStatusData.ProductCode),
    async code =>
    {
        await SendProductCodeToMesAsync(code);
    });
```

Debounce noisy PLC updates to avoid repeated API calls:

```csharp
data.WhenPropertyChangesDebounced<string>(
    nameof(MachineStatusData.ProductCode),
    debounceMs: 300,
    async code =>
    {
        await SendProductCodeToMesAsync(code);
    });
```

## Extension Methods

`PLCDataExtensions` provides helper methods for objects that inherit from `PLCDataContext`:

| Method                       | Description                                                                                        |
| ---------------------------- | -------------------------------------------------------------------------------------------------- |
| `GetPLCProperties()`         | Gets properties with a `[PLCAddress]` attribute. Note: this method does not read runtime mappings. |
| `CopyValuesFrom(source)`     | Copies values between two objects of the same type, based on attributed properties.                |
| `ValuesEqual(other)`         | Compares values between two objects of the same type. Includes special handling for `short[]`.     |
| `ExportToDictionary()`       | Exports values to a dictionary where keys are PLC addresses.                                       |
| `ImportFromDictionary(data)` | Imports values from a dictionary where keys are PLC addresses.                                     |

Example:

```csharp
var snapshot = data.ExportToDictionary();

var other = new QRScanData(plc);
other.CopyValuesFrom(data);
```

## Custom Logging

Override `CustomMessage` to add a prefix or customize error/change logs:

```csharp
public class MachineStatusData : PLCDataContext
{
    public MachineStatusData(McpX plc) : base(plc) { }

    protected override string CustomMessage(string message)
        => $"[Line A] {message}";
}
```

## Implementation Notes

- Each low-level read/write operation uses an internal `SemaphoreSlim` to lock access to `McpX`.
- Addresses must start with a prefix defined in `McpXLib.Enums.Prefix`, such as `M` or `D`.
- `ReadValueAsync(string)` returns the value only; it does not update the property on the object.
- `SaveAsync()` writes all mapped properties except those marked `readOnly`.
- For `string` and `short[]`, declare `length` explicitly to avoid reading or writing incomplete data.
