# PLC Data Context - PLC Data Access/Update Mechanism

## Introduction

This mechanism provides an **easy, type-safe, and clean** way to work with PLC data instead of managing addresses manually.

### What's New

Instead of manually managing PLC addresses, the new mechanism provides:

- **Property-based access** - Access data like normal properties
- **Type-safe** - Automatic IntelliSense completion
- **Auto conversion** - Automatic data type conversion
- **Clean code** - Reduces complexity and potential errors

```csharp
var qrData = new QRScanData1(_plc);
await qrData.LoadAsync();

if (qrData.IsLocked)
{
    qrData.ClearCommand = true;
    qrData.RollID = rollId; // Automatically converted
    await qrData.SaveAsync();
}
```

## How It Works

### Method 1: Using Attributes (Traditional)

#### 1. Define Data Class

```csharp
public class QRScanData : PLCDataContext
{
    public QRScanData(McpX plcDevice) : base(plcDevice) { }
    // Attribute specifies PLC address
    [PLCAddress("M6011", description: "QR Scan Command")]
    public bool ScanCommand { get; set; }

    [PLCAddress("M6012", description: "OK Status")]
    public bool OkStatus { get; set; }

    [PLCAddress("D6200", length: 100, description: "QR Buffer")]
    public string QRBarcode { get; set; } = "";

    [PLCAddress("D6300", length: 2, description: "Speed")]
    public int RollSpeed { get; set; }

    [PLCAddress("D6310", length: 2, description: "Length")]
    public float RollLength { get; set; }
}
```

#### 2. Usage

```csharp
// Initialize object with PLC device
var data = new QRScanData(_plc);

// FIRST, read ALL from PLC into object
await data.LoadAsync();

// Now can access properties normally
bool isOk = data.OkStatus;
string barcode = data.QRBarcode;

// Modify data
data.OkStatus = true;
data.RollSpeed = 500;
data.RollLength = 1000.5f;

// WRITE ALL changes back to PLC
await data.SaveAsync();
```

### Method 2: Using Dynamic JSON Mapping (Advanced)

#### 1. Prepare JSON File

Create `mapping.json` with format:

```json
{
  "ScanCommand": {
    "address": "M6011",
    "length": 1,
    "description": "QR Scan Command"
  },
  "OkStatus": {
    "address": "M6012",
    "length": 1,
    "description": "OK Status"
  }
}
```

#### 2. Define Data Class (No Attributes Needed)

```csharp
public class SendRollMES : PLCDataContext
{
    public SendRollMES(McpX plcDevice) : base(plcDevice) { }
    public SendRollMES(McpX plcDevice, Dictionary<string, PLCAddressAttribute> mappings) : base(plcDevice, mappings) { }

    // Declare properties WITHOUT [PLCAddress] attribute
    public bool ScanCommand { get; set; }
    public bool OkStatus { get; set; }
    public string QRBarcode { get; set; } = "";
    public int RollSpeed { get; set; }
    public float RollLength { get; set; }
}
```

#### 3. Usage With JSON Mapping

```csharp
// Read JSON file
var json = File.ReadAllText("mapping.json");

// Deserialize into List<PLCAddressAttribute>
var fieldMapList = JsonSerializer.Deserialize<Dictionary<string, PLCAddressAttribute>>(json);

// Initialize object with mapping
var context = new SendRollMES(_plc, fieldMapList);

// Read from PLC (using JSON mapping)
await context.LoadAsync();

// Process data
if (context.ScanCommand)
{
    context.OkStatus = true;
    context.RollSpeed = 1000;
    context.RollLength = 500.5f;
}

// Write back to PLC
await context.SaveAsync();
```

#### 4. Benefits of JSON Mapping

- **Customize addresses without recompile** - Just update mapping.json
- **Easy config management** - Can be stored outside codebase
- **Support multiple versions** - Can have mapping.v1.json, mapping.v2.json
- **Easy testing** - Mock mapping for unit tests
- **Easy maintenance** - Single place to manage all PLC addresses

## Supported Data Types

| Type      | Words Used | Example Address |
| --------- | ---------- | --------------- |
| `bool`    | 1          | `M3800`         |
| `string`  | Length     | `D6200`         |
| `int`     | 2          | `D6100`         |
| `float`   | 2          | `D6310`         |
| `short[]` | Length     | `D6000`         |

### Automatic Type Conversion

```csharp
// String (uses multiple Words)
[PLCAddress("D6200", length: 100)]
public string QRCode { get; set; } = "";

// Int32 (uses 2 Words)
[PLCAddress("D6300", length: 2)]
public int Speed { get; set; }

// Float (uses 2 Words)
[PLCAddress("D6310", length: 2)]
public float Length { get; set; }

// Boolean (uses 1 Word from M register)
[PLCAddress("M6000")]
public bool IsRunning { get; set; }

// Raw Word Array
[PLCAddress("D6000", length: 10)]
public short[] RawData { get; set; } = Array.Empty<short>();
```

## Detailed API

### Main Methods

```csharp
// Read ALL from PLC into object (supports attributes or JSON mapping)
await data.LoadAsync();

// Write ALL from object to PLC (supports attributes or JSON mapping)
await data.SaveAsync();

// Read specific property from PLC
var value = await data.ReadValueAsync("PropertyName");

// Write specific property to PLC
await data.WriteValueAsync("PropertyName", value);
```

### Extension Methods (Utilities)

```csharp
// Get list of all PLC properties
var props = data.GetPLCProperties();
// Output: List<(PropertyInfo, PLCAddressAttribute)>

// Print detailed mapping
Console.WriteLine(data.GetPLCMapping());

// Clone data from another object
data.CopyValuesFrom(otherData);

// Compare with another object
bool isEqual = data.ValuesEqual(otherData);

// Export to Dictionary (easy serialize)
var dict = data.ExportToDictionary();

// Import from Dictionary
data.ImportFromDictionary(dict);
```

## Property Change Tracking

PLCDataContext supports **INotifyPropertyChanged** for easy property change tracking. There are 3 ways to use it:

### Method 1: Using WhenPropertyChanges (Easiest)

This method is easiest and suitable for most use cases:

#### String Version (Basic)

```csharp
var scanRoll = new HMIScanRollScreenData(_plc);

// Subscribe to property changes
var subscription = scanRoll.WhenPropertyChanges<bool>("ConfirmSubRollButton", (newValue) =>
{
    _logger.LogInformation($"ConfirmSubRollButton changed to: {newValue}");
    if (newValue)
    {
        // Handle button confirm
    }
});

// When no longer needed, unsubscribe
subscription.Dispose();
```

#### Lambda Expression Version (Type-Safe - Recommended)

Use **extension method** - `x` will be type-inferred, IntelliSense works great:

```csharp
var scanRoll = new HMIScanRollScreenData(_plc);

// Subscribe with lambda expression - x is HMIScanRollScreenData, IntelliSense support!
var subscription = scanRoll.WhenPropertyChanges(
    x => x.ConfirmSubRollButton,  // ← x. shows all properties
    (newValue) =>
    {
        _logger.LogInformation($"ConfirmSubRollButton changed to: {newValue}");
        if (newValue)
        {
            // Handle button confirm
        }
    }
);

// With async callback:
var asyncSubscription = scanRoll.WhenPropertyChanges(
    x => x.ConfirmSubRollButton,
    async (newValue) =>  // ← Receives parameter value
    {
        _logger.LogInformation($"ConfirmSubRollButton changed to: {newValue}");
        if (newValue)
        {
            // Async operations
            await ProcessSubRollConfirmAsync();
        }
    }
);

// Nested properties also supported:
var nestedSubscription = scanRoll.WhenPropertyChanges(
    x => x.ConfirmMainRollButton,
    async (value) => await HandleButtonAsync(value)
);

// When no longer needed, unsubscribe
subscription.Dispose();
asyncSubscription.Dispose();
nestedSubscription.Dispose();
```

**With old value (Lambda Expression):**

```csharp
var subscription = scanRoll.WhenPropertyValueChanges(
    x => x.ConfirmSubRollButton,  // ← x. shows properties
    (oldValue, newValue) =>
    {
        _logger.LogInformation($"ConfirmSubRollButton changed from {oldValue} to {newValue}");
    }
);

// With async:
var asyncSubscription = scanRoll.WhenPropertyValueChanges(
    x => x.ConfirmSubRollButton,
    async (oldValue, newValue) =>
    {
        _logger.LogInformation($"ConfirmSubRollButton changed from {oldValue} to {newValue}");
        if (newValue)
            await ProcessAsync();
    }
);
```

**Advantages of Lambda Expression:**

- ✅ Type-safe - Compiler checks property exists
- ✅ Refactor-safe - Rename property automatically updates
- ✅ IntelliSense support - Autocomplete property names
- ✅ Supports nested properties (E.g: `x => x.ScanRollScreen.ConfirmMainRollButton`)

### Method 2: Using PropertyChanged Event (Standard .NET)

This approach follows MVVM pattern and .NET standards:

```csharp
var scanRoll = new HMIScanRollScreenData(_plc);

// Subscribe to PropertyChanged event
scanRoll.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == "ConfirmSubRollButton")
    {
        _logger.LogInformation($"ConfirmSubRollButton changed");
    }
};
```

### Method 3: Using SetProperty Method (In Subclass)

When defining a subclass, can use SetProperty helper method:

```csharp
private bool _confirmSubRollButton;

public bool ConfirmSubRollButton
{
    get => _confirmSubRollButton;
    set => SetProperty(ref _confirmSubRollButton, value, nameof(ConfirmSubRollButton));
}
```

### Comparison of Methods

| Method                       | Advantages                        | Disadvantages | Use Case                     |
| ---------------------------- | --------------------------------- | ------------- | ---------------------------- |
| WhenPropertyChanges (String) | Simple, runtime flexible          | Not type-safe | Quick prototyping            |
| WhenPropertyChanges (Lambda) | Type-safe, IntelliSense, refactor | Longer syntax | Production code, recommended |
| PropertyChanged Event        | Standard .NET, MVVM pattern       | Need handler  | MVVM app, complex binding    |
| SetProperty                  | Clean, reusable                   | Need subclass | Property with backing field  |

## Real-World Examples

### Example 1: QR Scan (Using Attributes)

```csharp
private async Task HandleQRScanAsync()
{
    var qrData = new QRScanData1(_plc);

    // Read scan status
    await qrData.LoadAsync();

    if (qrData.ScanCommand)
    {
        _logger.LogInformation($"Barcode: {qrData.QRBarcode}");

        // Call API to process
        var result = await _api.ProcessBarcodeAsync(qrData.QRBarcode);

        if (result.IsValid)
        {
            // Update status
            qrData.OkStatus = true;
            qrData.RollID = result.RollId;
            qrData.IsLocked = false;

            // Write back
            await qrData.SaveAsync();
        }
    }
}
```

### Example 2: Using JSON Mapping

```csharp
private async Task InitializeWithMappingAsync()
{
    // Load mapping from JSON file
    var json = File.ReadAllText("config/plc-mapping.json");
    var fieldMaps = JsonSerializer.Deserialize<Dictionary<string, PLCAddressAttribute>>(json);

    // Initialize with mapping
    var mesData = new SendRollMES(_plc, fieldMaps);
    var scanData = new QRScanData(_plc, fieldMaps);

    // Read data
    await mesData.LoadAsync();
    await scanData.LoadAsync();

    // Process...
    await mesData.SaveAsync();
    await scanData.SaveAsync();
}
```

### Example 3: Managing Multiple Data Objects (Parallel)

```csharp
private async Task MainLoopAsync()
{
    var qrData1 = new QRScanData1(_plc);
    var qrData2 = new QRScanData2(_plc);
    var jobData = new JobData(_plc);
    var control = new ControlData(_plc);

    while (!stoppingToken.IsCancellationRequested)
    {
        // Read data in parallel
        await Task.WhenAll(
            qrData1.LoadAsync(),
            qrData2.LoadAsync(),
            jobData.LoadAsync(),
            control.LoadAsync()
        );

        // Process logic
        await ProcessScan1Async(qrData1);
        await ProcessScan2Async(qrData2);
        await ProcessJobAsync(jobData);

        // Write data in parallel
        await Task.WhenAll(
            qrData1.SaveAsync(),
            qrData2.SaveAsync(),
            jobData.SaveAsync(),
            control.SaveAsync()
        );

        await Task.Delay(50);
    }
}
```

### Example 4: Export/Import Data

```csharp
// Export data from PLC (storage/transmission)
var qrData = new QRScanData1(_plc);
await qrData.LoadAsync();

// Get individual property values
var scanCmd = qrData.ScanCommand;
var barcode = qrData.QRBarcode;

// Or serialize entire object
var jsonData = JsonSerializer.Serialize(qrData);
await _db.SaveHistoryAsync(jsonData);
```

### Example 5: Property Change Tracking

This example shows how to track button presses and auto-trigger handlers:

```csharp
private async Task SetupPropertyTrackingAsync()
{
    var scanRoll = new HMIScanRollScreenData(_plc);
    await scanRoll.LoadAsync();

    // ===== Method 1: WhenPropertyChanges + Lambda Expression (Recommended) =====
    // Lambda expression - Type-safe and easy to refactor
    var subRollSubscription = scanRoll.WhenPropertyChanges(
        x => x.ConfirmSubRollButton,
        async (newValue) =>
        {
            if (newValue)  // When button is pressed
            {
                _logger.LogInformation("Sub Roll Button Confirmed!");
                // Handle logic
                await ProcessSubRollConfirmAsync(scanRoll);
            }
        }
    );

    // Subscribe with old value - Lambda Expression
    var mainRollSubscription = scanRoll.WhenPropertyValueChanges(
        x => x.ConfirmMainRollButton,
        async (oldValue, newValue) =>
        {
            _logger.LogInformation($"MainRoll: {oldValue} -> {newValue}");
            if (newValue)
                await HandleMainRollChangeAsync();
        }
    );

    // ===== Method 1B: WhenPropertyChanges + String (If runtime flexibility needed) =====
    // String-based - Flexible but not type-safe
    // var subscription = scanRoll.WhenPropertyChanges<bool>("Button", newValue => { ... });

    // ===== Method 2: PropertyChanged Event (MVVM Pattern) =====
    scanRoll.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == "IsMainRollValid" && s is HMIScanRollScreenData data)
        {
            _logger.LogInformation($"MainRoll Valid: {data.IsMainRollValid}");
        }
    };

    // Main loop
    while (!stoppingToken.IsCancellationRequested)
    {
        await scanRoll.LoadAsync();
        await Task.Delay(100);

        // Subscriptions will auto-trigger when property changes
    }

    // Cleanup
    subRollSubscription?.Dispose();
    mainRollSubscription?.Dispose();
}

private async Task ProcessSubRollConfirmAsync(HMIScanRollScreenData scanRoll)
{
    var qrCode = scanRoll.SubRollQRCode;
    _logger.LogInformation($"Processing QR: {qrCode}");

    // Call API to process
    var result = await _api.ValidateQRCodeAsync(qrCode);

    if (result.IsValid)
    {
        scanRoll.IsSubRollValid = true;
    }

    await scanRoll.SaveAsync();
}
```

**Benefits of Property Change Tracking:**

- ✅ Auto-trigger handler when property changes
- ✅ No need for polling or manual checks
- ✅ Type-safe callbacks
- ✅ Easy cleanup with `.Dispose()`
- ✅ Support both old/new values

## Performance Optimization

### Selective Read/Write (Performance)

Instead of reading/writing all data, can read/write only needed properties:

```csharp
// Only read Scan property
var scanCmd = await data.ReadValueAsync("ScanCommand");

// Only write OkStatus property
await data.WriteValueAsync("OkStatus", true);
```

### Batch Operations (Faster)

When working with multiple data objects, use `Task.WhenAll()` to read/write in parallel:

```csharp
// Read data in parallel
await Task.WhenAll(
    data1.LoadAsync(),
    data2.LoadAsync(),
    data3.LoadAsync()
);

// Write data in parallel
await Task.WhenAll(
    data1.SaveAsync(),
    data2.SaveAsync(),
    data3.SaveAsync()
);
```

## Troubleshooting

### Error: "Unknown device type"

Ensure address starts with `M` or `D`:

```csharp
[PLCAddress("M100")]  // M-register: Boolean
[PLCAddress("D100")]  // D-register: Word-based

// Or in JSON mapping:
{
    "MyFlag": {
        "address": "M100",
        "length": 1
    }
}
```

### Error: Data Not Changing

Must call `SaveAsync()` after changing a property:

```csharp
data.Speed = 500;
await data.SaveAsync(); // Required!
```

### Error: Mapping Not Found

When using JSON mapping, ensure the keys in JSON match property names:

```json
{
  "ScanCommand": {
    "address": "M6011",
    "length": 1
  }
}
```

```csharp
public class MyData : PLCDataContext
{
    public bool ScanCommand { get; set; }  // Must match "ScanCommand" in JSON
}
```

### Error: Properties Not Loaded

Check:

1. Does property have `[PLCAddress]` attribute or declared in JSON mapping?
2. Called `LoadAsync()` yet?

```csharp
// ✗ Wrong: Not reading from PLC
var data = new MyData(_plc);
var value = data.MyProperty; // Value is default (0, false, "")

// ✓ Correct: Read from PLC first
var data = new MyData(_plc);
await data.LoadAsync();
var value = data.MyProperty; // Value from PLC
```
