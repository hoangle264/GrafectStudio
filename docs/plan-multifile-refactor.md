# Multi-File Unit Station Codegen Refactor Plan

## Mục tiêu
Chuyển codegen từ xuất một khối lớn/hard-code Auto/Origin/Manual/Error sang kiến trúc trung lập:
- Trung tâm là **Unit Station**
- Flow có controlState trung lập, không khóa PackML
- Có flow đặc biệt orchestrator cho SystemControl
- Export nhiều file: MapIO, SystemControl, Unit_<name>, Error, DeviceManager_<type>
- PackML chỉ là template profile

## Thay đổi model chính

### Flow thường
`	s
controlState?: string;
category?: "normal" | "orchestrator";
orchestratorConfig?: { elements: Array<{ type: string; config: any }> };
`
Mặc định: category="normal", controlState = diagram.mode || "Auto"

### Flow đặc biệt orchestrator
- category="orchestrator", không bắt buộc có step
- Dùng sinh SystemControl.st
- Mở rộng sau qua orchestratorConfig

## Thay đổi codegen

### Không normalize về auto/origin
Giữ nguyên low.controlState thay vì ép.

### Multi-file output contract
`	s
{
  files: [
    { path: "MapIO.st", content: string },
    { path: "SystemControl.st", content: string },
    { path: "Units/Unit_<name>.st", content: string },
    { path: "Error.st", content: string },
    { path: "Devices/DeviceManager_<type>.st", content: string }
  ]
}
`

### Tách generator
- MapIOGenerator
- SystemControlGenerator
- UnitCodeGenerator
- ErrorGenerator
- DeviceManagerGenerator

### Template profile
- 	emplates/simple/ và 	emplates/packml/
- Template nền: MapIO.hbs, SystemControl.hbs, UnitCode.hbs, Error.hbs, DeviceManager.hbs

## Frontend/UI
- Properties: dùng controlState thay mode (vẫn hỗ trợ cũ)
- Thêm option: "Add System Control Flow" (category="orchestrator")
- Export modal: hiển thị nhiều file, preview/copy/download

## Migration
Khi load project cũ:
`	s
diagram.controlState = diagram.mode || "Auto";
diagram.category = "normal";
`

## Test
- Project cũ vẫn generate được
- Nhiều unit → nhiều Unit files
- Nhiều controlState → giữ nguyên state
- Orchestrator flow → sinh SystemControl
- Nhiều device type → sinh DeviceManager

## Thứ tự triển khai
1. Refactor model (thêm fields)
2. Migration project cũ
3. Sửa payload builder
4. Multi-file output contract
5. Tách UnitCodeGenerator trước
6. Tách MapIO, Error, DeviceManager
7. SystemControlGenerator skeleton
8. Cập nhật UI properties
9. Export modal multi-file
10. Template nền
11. Test

## Assumptions
- Không hard-code PackML vào core
- Giai đoạn đầu chỉ cần SystemControl skeleton
- controlState là chính, mode chỉ tương thích
