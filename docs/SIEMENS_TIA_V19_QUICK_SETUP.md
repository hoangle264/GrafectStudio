# Siemens TIA Openness V19 Quick Setup

Tai lieu nay la checklist ngan gon de setup Siemens TIA Openness V19 cho GrafcetStudio tren may Windows co TIA Portal V19.

## 1. Kiem tra TIA Portal V19 va PublicAPI

Dam bao may da cai:

- `TIA Portal V19`
- feature `TIA Openness`
- thu muc PublicAPI ton tai tai:
  - `C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19`

Neu khong co `PublicAPI\V19`, can mo installer cua TIA Portal va bo sung feature Openness.

## 2. Cap quyen Openness cho user Windows

1. Mo `Computer Management` hoac `lusrmgr.msc`.
2. Vao `Local Users and Groups` -> `Groups`.
3. Tim group Siemens TIA Openness cua version V19.
4. Them user Windows hien tai vao group nay.
5. `Sign out/sign in` lai Windows, hoac restart may.

Khong co buoc nay thi GrafcetStudio co the bi `TiaOpennessUnavailable`, access denied, hoac security exception.

## 3. Mo TIA Portal lan dau

1. Dang nhap bang dung user vua duoc cap quyen.
2. Mo `TIA Portal V19`.
3. Neu co prompt bao mat/Openness, bam chap nhan.
4. Mo hoac tao mot sample project co PLC software.

Nen dung mot project test rieng, khong dung project san xuat.

## 4. Set environment variables cho GrafcetStudio

Khuyen nghi dung bridge mode:

```powershell
$env:GRAFCETSTUDIO_TIA_IMPORT_MODE = "bridge"
```

Thong thuong bridge mode khong can set `GRAFCETSTUDIO_TIA_OPENNESS_DIR`.

Chi set them bien duoi day neu ban can debug bang legacy reflection mode:

```powershell
$env:GRAFCETSTUDIO_TIA_IMPORT_MODE = "reflection"
$env:GRAFCETSTUDIO_TIA_OPENNESS_DIR = "C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19"
```

Neu bridge executable khong nam canh app, set them:

```powershell
$env:GRAFCETSTUDIO_TIA_BRIDGE_PATH = "<duong-dan-toi>\GrafcetStudio.TiaBridge.V19.exe"
```

Neu muon dung lau dai cho moi terminal, them cac bien nay vao User Environment Variables cua Windows.

## 5. Test theo thu tu an toan

### Cach 1: Manual XML import truoc

1. Generate XML bang generator `siemens-lad`.
2. Mo TIA project test.
3. Tao truoc global tags neu XML dang tham chieu global tags.
4. Import file XML vao `Program blocks` hoac folder dich.
5. Mo block vua import de kiem tra LAD render dung.
6. Compile PLC software va ghi lai warning/error.

Manual import on dinh roi moi chuyen sang direct push.

### Cach 2: Direct push tu GrafcetStudio

Trong GrafcetStudio, nhap day du:

- `ProjectPath`
- `DeviceName`
- `PlcName`
- `TargetFolderPath`
- `BlockName`
- `OverwriteMode`

Sau do bam `Push to TIA`.

Nen test lan luot:

- `FailIfExists`
- `Overwrite`
- `Rename`

Hay test tren project copy de tranh ghi de block dang dung.

## 6. Gia tri mau cho V19

Gia tri tham khao:

- `GRAFCETSTUDIO_TIA_IMPORT_MODE=bridge`
- `GRAFCETSTUDIO_TIA_OPENNESS_DIR=C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19`
- `TargetFolderPath=Program blocks/Grafcet`

Ten `DeviceName` va `PlcName` phai copy dung theo cay project trong TIA Portal.

## 7. Loi thuong gap

- `TiaOpennessUnavailable`: chua cai Openness, chua them user vao group, hoac chua sign out/sign in.
- `ProjectNotFound`: sai `ProjectPath`.
- `DeviceNotFound`: sai `DeviceName`.
- `PlcNotFound`: sai `PlcName`.
- `TargetFolderNotFound`: sai `TargetFolderPath`.
- Import XML loi schema/version: can doi chieu XML voi version TIA dang dung.
- Compile loi missing tags: can tao truoc PLC tags hoac doi scope bien.

## 8. Thu tu khuyen nghi de chot setup

1. Cai TIA Portal V19 + Openness.
2. Them user vao Siemens TIA Openness group.
3. Restart hoac sign in lai.
4. Mo TIA Portal va chap nhan prompt.
5. Chay GrafcetStudio o `bridge` mode.
6. Test manual XML import.
7. Test direct push tren project copy.

## Tai lieu lien quan

- `docs/SIEMENS_TIA_OPENNESS_SETUP.md`
- `docs/SIEMENS_TIA_OPENNESS_IMPORT.md`
- `docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md`
