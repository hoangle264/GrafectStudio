# Siemens TIA Openness V20 Quick Setup

Checklist ngan gon de day XML tu GrafcetStudio vao TIA Portal V20. V19 va V20 chay song song: moi version mot bridge exe rieng, chon version trong modal sinh code.

> Trang thai: bridge V20 da co code nhung **chua build va chua test tren may co TIA V20** (xem S0/S3 trong `docs/Plan_TIA_V20_Mitsubishi.md`). Namespace SimaticML cho V20 tam dung giong V19 (`FlgNet/v5`, `Interface/v5`, `StructuredText/v4`), chi doi `Engineering version="V20"`. Can doi chieu voi XML export tu V20 that.

## 1. Kiem tra TIA Portal V20 va PublicAPI

- Cai `TIA Portal V20` + feature `TIA Openness` (khuyen nghi Update 4 tro len).
- Thu muc PublicAPI mac dinh:
  - `C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll`
- Neu DLL nam cho khac, set:

```powershell
$env:GRAFCETSTUDIO_TIA_OPENNESS_DIR_V20 = "<thu-muc-chua>\Siemens.Engineering.dll"
```

## 2. Quyen Openness

Giong V19: them user Windows vao group `Siemens TIA Openness` cua V20, sign out/sign in, mo TIA Portal V20 va chap nhan prompt Openness lan dau.

## 3. Build bridge V20

Bridge phai build tren may co Openness V20:

```powershell
dotnet build src\GrafcetStudio.TiaBridge.V20\GrafcetStudio.TiaBridge.V20.csproj
```

- Khong tim thay `Siemens.Engineering.dll` V20 thi build ra **stub** kem warning. Stub luon tra `TiaNotInstalled` voi thong bao "built without TIA Portal V20 Openness". Gap thong bao nay thi build lai tren may co V20.
- `Siemens.Engineering.dll` khong duoc copy canh exe (`Private=false`). Bridge nap DLL luc chay theo thu tu: `GRAFCETSTUDIO_TIA_OPENNESS_DIR_V20` -> thu muc cai mac dinh -> `GRAFCETSTUDIO_TIA_OPENNESS_DIR`. DLL sai major version bi bo qua.
- Neu V20 doi API Openness, dat phan khac biet sau `#if TIA_V20` trong `src/GrafcetStudio.TiaBridge.Shared/`.

Chay tay de kiem tra (TIA V20 dang mo project):

```powershell
src\GrafcetStudio.TiaBridge.V20\bin\Debug\net48\GrafcetStudio.TiaBridge.V20.exe --request C:\Temp\request.json
```

`request.json` phai co `"TiaVersion": "V20"`. Bridge V20 tu choi request V19 va nguoc lai.

## 4. Chon V20 trong GrafcetStudio

1. Mo modal sinh code, chon target `siemens-lad` hoac `siemens-db`.
2. O dong `Siemens TIA mode`, chon `TIA: V20`. Lua chon nay ap dung cho ca Preview XML va Push, va duoc luu vao config.
3. Version chon o day ghi de `TIA_VERSION` trong template `.lad.hbs`.

App tim `GrafcetStudio.TiaBridge.V20.exe` theo thu tu:

1. `GRAFCETSTUDIO_TIA_BRIDGE_PATH_V20` (duong dan exe)
2. `GRAFCETSTUDIO_TIA_BRIDGE_PATH` (duong dan exe, hoac thu muc chua exe)
3. Canh `GrafcetStudio.App.exe`
4. `tia-bridge\` canh app (ban portable)
5. `src\GrafcetStudio.TiaBridge.V20\bin\Debug\net48\` trong repo

## 5. Test

Chay `docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md` cho ca V19 va V20. Lam manual XML import vao V20 truoc, on roi moi direct push.

## 6. Loi thuong gap rieng cho V20

- `Request targets TIA Portal V19 but this is the TIA Portal V20 bridge`: `GRAFCETSTUDIO_TIA_BRIDGE_PATH` dang tro vao exe cua version khac. Dung bien `_V20` hoac tro bien chung vao thu muc.
- `TIA Openness V20 assemblies could not be loaded`: sai `GRAFCETSTUDIO_TIA_OPENNESS_DIR_V20` hoac chua cai Openness V20.
- Import loi schema/version: XML sinh ra chua khop V20, xem ghi chu Trang thai o dau tai lieu.

## Tai lieu lien quan

- `docs/SIEMENS_TIA_V19_QUICK_SETUP.md`
- `docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md`
- `docs/Plan_TIA_V20_Mitsubishi.md`
