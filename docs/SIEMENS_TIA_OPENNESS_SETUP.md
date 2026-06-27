# Siemens TIA Openness Setup

Tai lieu nay mo ta cach van hanh Siemens LAD XML/direct push cua GrafcetStudio voi TIA Portal Openness. Generator `siemens-lad` sinh SimaticML XML; direct push la tuy chon runtime va khong bat buoc tren may khong cai TIA.

## Trang thai moi truong hien tai

Kiem tra ngay 2026-06-27 tren may workspace nay:

- TIA Portal V19 duoc phat hien tai `C:\Program Files\Siemens\Automation\Portal V19`.
- PublicAPI duoc phat hien tai `C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V16` den `V19`.
- Bien moi truong `GRAFCETSTUDIO_TIA_OPENNESS_MODE` va `GRAFCETSTUDIO_TIA_OPENNESS_DIR` chua duoc set trong shell hien tai.
- Chua thuc hien import/compile thuc te vi can project TIA mau va xac nhan GUI trong TIA Portal.
- Trang thai test duoc ghi la prepared only; chua duoc tinh la pass Milestone 6.

## Cau hinh runtime

GrafcetStudio chi load Siemens assemblies khi duoc bat bang bien moi truong:

```powershell
$env:GRAFCETSTUDIO_TIA_OPENNESS_MODE = "reflection"
$env:GRAFCETSTUDIO_TIA_OPENNESS_DIR = "C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19"
```

Chon dung folder `PublicAPI\V__` khop voi TIA Portal/project dang test. Neu muon bat cho terminal moi, set User/Machine environment variables trong Windows thay vi chi set trong session PowerShell.

## Quyen Openness

Truoc khi push direct:

1. Cai TIA Portal kem feature TIA Openness.
2. Them Windows user hien tai vao local group Siemens TIA Openness cua version dang dung.
3. Sign out/sign in hoac restart de group membership co hieu luc.
4. Mo TIA Portal mot lan bang user do va chap nhan prompt/security neu co.
5. Chay GrafcetStudio bang cung user.

Neu thieu quyen, GrafcetStudio co the tra `TiaOpennessUnavailable`, access denied, security exception, hoac TIA Portal tu choi attach/open project.

## Manual import XML FC

Dung flow nay de xac nhan XML truoc khi dung direct push:

1. Generate Siemens LAD XML tu platform `siemens-lad` va luu file `.xml`.
2. Mo TIA Portal project mau co PLC software compile duoc.
3. Tao san cac global tags ma XML tham chieu, hoac sua template de dung local/input/output scope.
4. Import XML FC vao `Program blocks` hoac folder dich.
5. Mo block vua import va xac nhan network hien thi Contact/Coil/OR/AND dung nhu Grafcet sample.
6. Compile PLC software va ghi lai loi/warning.

Checklist chi tiet nam tai `docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md`.

## Direct push vao project

Dung flow nay sau khi manual import da on dinh:

1. Set `GRAFCETSTUDIO_TIA_OPENNESS_MODE=reflection`.
2. Set `GRAFCETSTUDIO_TIA_OPENNESS_DIR` toi folder PublicAPI dung version.
3. Mo project trong TIA Portal hoac dien `ProjectPath` de adapter mo project.
4. Trong GrafcetStudio, dien `DeviceName`, `PlcName`, `TargetFolderPath`, `BlockName`, `OverwriteMode`.
5. Bam Push to TIA.
6. Xac nhan block xuat hien trong folder dich, mo block de xem LAD, roi compile PLC software.

Nen test lan luot `FailIfExists`, `Overwrite`, va `Rename` tren project copy de tranh ghi de block san xuat.

## Loi thuong gap

| Loi | Dau hieu | Cach xu ly |
| --- | --- | --- |
| Openness permission | Direct push bi access denied hoac `TiaOpennessUnavailable`. | Cai Openness, them user vao group dung version, sign out/sign in hoac restart, chay GrafcetStudio cung user. |
| Namespace/version mismatch | Import XML bao sai schema/namespace/version. | Export mot FC LAD mau tu TIA version dang test, diff namespace/attribute voi XML sinh boi generator, cap nhat `tiaVersion`/SimaticML builder. |
| Block overwrite | Import/push loi vi block da ton tai. | Chon `OverwriteMode=Overwrite` khi chap nhan ghi de, hoac `Rename`; voi test an toan dung project copy. |
| Missing global tags | Compile loi tag khong ton tai. | Tao PLC tag table truoc, doi scope parameter sang local/input/output, hoac them generator tag table o milestone sau. |
| Wrong device/plc/folder | `DeviceNotFound`, `PlcNotFound`, `TargetFolderNotFound`. | Copy dung ten tu TIA project tree; kiem tra `TargetFolderPath` bat dau tu Program blocks/folder con. |

## Ghi version da test

| TIA version | XML manual import | Direct push | Compile PLC software | Ghi chu |
| --- | --- | --- | --- | --- |
| V19 | Not executed | Not executed | Not executed | Portal/PublicAPI detected on 2026-06-27; can thuc hien voi project mau va GUI confirmation. |
| V17/V18/V20 | Not tested | Not tested | Not tested | Can test tren may co version tuong ung. |

## De xuat cai tien tiep theo

- Them command/test harness sinh XML sample on dinh tu fixture Grafcet de tester import vao TIA nhanh.
- Them export/diff workflow: export FC LAD mau tu TIA V17/V18/V19/V20 va luu namespace/version compatibility notes.
- Them generator PLC tag table cho parameters scope `global` de giam loi missing global tags.
- Them nut validate target de kiem tra device/PLC/folder truoc khi import block.
- Them log chi tiet cho direct push gom TIA version, PublicAPI path, project/device/plc/folder, overwrite mode.
