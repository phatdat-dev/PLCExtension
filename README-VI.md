# Ngôn ngữ

[Tiếng Việt](README-VI.md) | [Tiếng Anh](README.md)

# PLCExtension

`PLCExtension` là thư viện .NET giúp ánh xạ dữ liệu PLC thành các property C# có kiểu rõ ràng. Thay vì đọc/ghi từng địa chỉ PLC thủ công, bạn định nghĩa một class kế thừa `PLCDataContext`, gắn địa chỉ bằng `[PLCAddress]` hoặc truyền mapping runtime, rồi dùng object đó để load/save dữ liệu.

Package hiện hỗ trợ `netstandard2.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0` và sử dụng `McpX` để giao tiếp PLC.

## Cài đặt

```bash
dotnet add package PLCExtension
```

## Ví dụ nhanh

```csharp
using McpXLib;

public class QRScanData : PLCDataContext
{
    public QRScanData(McpX plc) : base(plc) { }

    [PLCAddress("M6011", description: "Lệnh scan QR")]
    public bool ScanCommand { get; set; }

    [PLCAddress("M6012", description: "Trạng thái OK")]
    public bool OkStatus { get; set; }

    [PLCAddress("D6200", length: 100, description: "Buffer QR")]
    public string QRBarcode { get; set; } = "";

    [PLCAddress("D6300", description: "Tốc độ")]
    public int RollSpeed { get; set; }

    [PLCAddress("D6310", description: "Chiều dài")]
    public float RollLength { get; set; }
}
```

Sử dụng:

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

## PLCDataContext hoạt động như thế nào

`PLCDataContext` tìm các property có thông tin địa chỉ PLC, đọc dữ liệu từ PLC, convert về đúng kiểu C#, rồi set lại vào object. Khi ghi, thư viện lấy value hiện tại của property, convert thành word/bit tương ứng và ghi về PLC.

Nguồn thông tin địa chỉ PLC có thể đến từ:

- Attribute `[PLCAddress]` gắn trực tiếp trên property.
- Dictionary mapping truyền vào constructor `PLCDataContext(McpX, Dictionary<string, PLCAddressAttribute>)`.

Nếu cùng một property có cả attribute và runtime mapping, runtime mapping được ưu tiên. Các field còn thiếu trong mapping sẽ fallback về attribute nếu có.

## Khai báo địa chỉ bằng attribute

```csharp
public class MachineStatusData : PLCDataContext
{
    public MachineStatusData(McpX plc) : base(plc) { }

    [PLCAddress("M100", description: "Máy đang chạy")]
    public bool IsRunning { get; set; }

    [PLCAddress("D200", length: 20, description: "Mã sản phẩm")]
    public string ProductCode { get; set; } = "";

    [PLCAddress("D230", readOnly: true, description: "Counter chỉ đọc")]
    public int TotalCounter { get; set; }
}
```

Ý nghĩa tham số:

| Tham số       | Ý nghĩa                                                                              |
| ------------- | ------------------------------------------------------------------------------------ |
| `address`     | Địa chỉ PLC, ví dụ `M100`, `D200`. Ký tự đầu được parse sang `McpXLib.Enums.Prefix`. |
| `length`      | Số word cần đọc/ghi. Nếu bằng `0`, thư viện tự tính theo kiểu dữ liệu.               |
| `description` | Mô tả để tài liệu hóa mapping.                                                       |
| `readOnly`    | Nếu `true`, `SaveAsync()` và `WriteValueAsync()` sẽ bỏ qua property này khi ghi.     |

## Mapping runtime

Dùng mapping runtime khi địa chỉ PLC cần cấu hình bên ngoài code hoặc thay đổi theo từng máy/line.

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

Ví dụ file `mapping.json`:

```json
{
  "IsRunning": {
    "Address": "M100",
    "Length": 0,
    "Description": "Máy đang chạy",
    "ReadOnly": false
  },
  "ProductCode": {
    "Address": "D200",
    "Length": 20,
    "Description": "Mã sản phẩm",
    "ReadOnly": false
  },
  "TotalCounter": {
    "Address": "D230",
    "Length": 0,
    "Description": "Counter chỉ đọc",
    "ReadOnly": true
  }
}
```

Load mapping:

```csharp
using Newtonsoft.Json;

var json = File.ReadAllText("mapping.json");
var mapping = JsonConvert.DeserializeObject<Dictionary<string, PLCAddressAttribute>>(json)
              ?? new Dictionary<string, PLCAddressAttribute>();

var data = new MachineStatusData(plc, mapping);
await data.LoadAllSequentialAsync();
```

## API đọc/ghi dữ liệu

| Method                                                | Chức năng                                                                                                   |
| ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `LoadAllSequentialAsync()`                            | Đọc tất cả property có mapping, lần lượt từng property. Đây là lựa chọn an toàn mặc định.                   |
| `LoadAllParallelAsync()`                              | Đọc tất cả property bằng `Parallel.ForEachAsync`. Phù hợp khi PLC/driver chịu được nhiều request đồng thời. |
| `LoadAllTasksAsync()`                                 | Tạo task cho từng property và `Task.WhenAll`. Mức concurrency cao nhất.                                     |
| `SaveAsync()`                                         | Ghi tất cả property có mapping và có thể đọc value. Property `readOnly` sẽ bị bỏ qua.                       |
| `ReadValueAsync(string propertyName)`                 | Đọc một property từ PLC và trả về value. Method này không tự set value vào object.                          |
| `WriteValueAsync(string propertyName, object? value)` | Ghi một value vào địa chỉ PLC của property tương ứng.                                                       |
| `ToJsonString()`                                      | Serialize object hiện tại bằng `Newtonsoft.Json`. Field nội bộ như PLC device và mapping được ignore.       |

Ví dụ đọc/ghi một property:

```csharp
var current = await data.ReadValueAsync(nameof(MachineStatusData.TotalCounter));
await data.WriteValueAsync(nameof(MachineStatusData.ProductCode), "ROLL-001");
```

## Kiểu dữ liệu hỗ trợ

`PLCDataContext` hiện đọc/ghi trực tiếp các kiểu sau:

| Kiểu C#   |     Số word mặc định | Ghi chú                                                                  |
| --------- | -------------------: | ------------------------------------------------------------------------ |
| `bool`    |                    1 | Đọc/ghi bằng `Read<bool>` và `Write<bool>`.                              |
| `string`  | 1 nếu không khai báo | Nên khai báo `length` rõ ràng để xác định buffer. Mỗi word chứa 2 ký tự. |
| `int`     |                    2 | Convert qua 2 word, little-endian theo `BitConverter`.                   |
| `float`   |                    2 | Khi đọc, giá trị được làm tròn 2 chữ số thập phân.                       |
| `double`  |                    4 | Convert qua 4 word.                                                      |
| `decimal` |                    8 | Convert qua 8 word.                                                      |
| `short[]` |        Theo `length` | Dùng cho raw word buffer.                                                |

`PLCTypeHelper` có khai báo length mặc định cho thêm một số kiểu như `short`, `long`, array khác, nhưng `PLCDataContext.ReadValueAsync` và `WriteValueAsync` chỉ xử lý trực tiếp các kiểu trong bảng trên.

## Theo dõi thay đổi property

`PLCDataContext` implement `INotifyPropertyChanged`. Khi `LoadAllSequentialAsync()`, `LoadAllParallelAsync()` hoặc `LoadAllTasksAsync()` đọc được giá trị mới khác giá trị hiện tại, object sẽ:

- Set value mới vào property.
- Raise `PropertyChanged`.
- Raise `PropertyValueChanged` với `oldValue` và `newValue`.
- In log dạng `[CHANGE] Class.Property: old -> new`.

Subscribe bằng tên property:

```csharp
var subscription = data.WhenPropertyChanges<bool>(
    nameof(MachineStatusData.IsRunning),
    isRunning =>
    {
        Console.WriteLine($"IsRunning = {isRunning}");
    });

// Hủy subscribe khi không dùng nữa
subscription.Dispose();
```

Subscribe có old/new value:

```csharp
data.WhenPropertyChanges<int>(
    nameof(MachineStatusData.TotalCounter),
    (oldValue, newValue) =>
    {
        Console.WriteLine($"Counter: {oldValue} -> {newValue}");
    });
```

Handler async:

```csharp
data.WhenPropertyChanges<string>(
    nameof(MachineStatusData.ProductCode),
    async code =>
    {
        await SendProductCodeToMesAsync(code);
    });
```

Debounce để tránh gọi API quá nhiều khi PLC ghi liên tục:

```csharp
data.WhenPropertyChangesDebounced<string>(
    nameof(MachineStatusData.ProductCode),
    debounceMs: 300,
    async code =>
    {
        await SendProductCodeToMesAsync(code);
    });
```

## Extension methods

`PLCDataExtensions` cung cấp một số helper cho object kế thừa `PLCDataContext`:

| Method                       | Chức năng                                                                                  |
| ---------------------------- | ------------------------------------------------------------------------------------------ |
| `GetPLCProperties()`         | Lấy các property có attribute `[PLCAddress]`. Lưu ý: method này không đọc runtime mapping. |
| `CopyValuesFrom(source)`     | Copy value giữa hai object cùng kiểu, dựa trên property có attribute.                      |
| `ValuesEqual(other)`         | So sánh value giữa hai object cùng kiểu. Có xử lý riêng cho `short[]`.                     |
| `ExportToDictionary()`       | Export value ra dictionary với key là địa chỉ PLC.                                         |
| `ImportFromDictionary(data)` | Import value từ dictionary có key là địa chỉ PLC.                                          |

Ví dụ:

```csharp
var snapshot = data.ExportToDictionary();

var other = new QRScanData(plc);
other.CopyValuesFrom(data);
```

## Logging tùy chỉnh

Kế thừa `CustomMessage` nếu muốn thêm prefix hoặc đổi nội dung log lỗi/thay đổi:

```csharp
public class MachineStatusData : PLCDataContext
{
    public MachineStatusData(McpX plc) : base(plc) { }

    protected override string CustomMessage(string message)
        => $"[Line A] {message}";
}
```

## Lưu ý triển khai

- Mỗi thao tác đọc/ghi cấp thấp dùng `SemaphoreSlim` nội bộ để khóa truy cập `McpX`.
- Địa chỉ phải bắt đầu bằng prefix có trong `McpXLib.Enums.Prefix`, ví dụ `M`, `D`.
- `ReadValueAsync(string)` chỉ trả value, không cập nhật property trong object.
- `SaveAsync()` ghi tất cả property có mapping, trừ property `readOnly`.
- Với `string` và `short[]`, nên khai báo `length` rõ ràng để tránh đọc/ghi thiếu dữ liệu.
