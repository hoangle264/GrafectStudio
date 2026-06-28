# Siemens LAD XML Manual Test Checklist

Checklist nay dung de xac nhan thuc te Siemens LAD XML/direct push voi TIA Portal. Neu may hien tai khong co TIA Portal hoac khong co project mau, dung checklist nay lam bien ban test thu cong va khong danh dau milestone la pass.

## Moi truong test

- [ ] TIA Portal version: `V__`.
- [ ] TIA Openness da duoc cai cung TIA Portal.
- [ ] Windows user nam trong group Siemens TIA Openness tuong ung version dang test.
- [ ] Da sign out/sign in hoac restart sau khi them user vao group.
- [ ] GrafcetStudio chay cung Windows user da duoc cap quyen.
- [ ] PLC project mau co CPU S7-1200/S7-1500 va PLC software compile duoc truoc khi import.
- [ ] Global tags duoc tao san cho cac bien scope `global` ma XML tham chieu.

## Chuan bi GrafcetStudio

- [ ] Chon generator `siemens-lad`.
- [ ] Generate preview Siemens LAD XML thanh cong.
- [ ] Luu XML voi extension `.xml`.
- [ ] Kiem tra XML co `SW.Blocks.FC`, `FlgNet`, `Part Name="Contact"`, `Part Name="Coil"`.
- [ ] Kiem tra network test co du ca Contact/Coil/AND/OR theo Grafcet sample.

## Import XML FC thu cong

- [ ] Mo TIA Portal project mau.
- [ ] Vao PLC software > Program blocks hoac folder dich.
- [ ] Chon import external source/XML theo flow cua TIA version dang test.
- [ ] Import XML FC sinh boi GrafcetStudio.
- [ ] Neu block da ton tai, chon overwrite/rename theo case dang test.
- [ ] Mo block vua import va xac nhan TIA hien thi LAD/FBD network hop le.
- [ ] Kiem tra Contact/Coil/OR/AND dung voi logic Grafcet sample.
- [ ] Compile PLC software va ghi ket qua.

## Push direct vao TIA

- [ ] Set `GRAFCETSTUDIO_TIA_IMPORT_MODE=bridge` hoac bo trong de dung mac dinh bridge-first.
- [ ] Neu bridge exe khong nam canh app, set `GRAFCETSTUDIO_TIA_BRIDGE_PATH` toi `GrafcetStudio.TiaBridge.V19.exe`.
- [ ] Neu can test reflection legacy, set them `GRAFCETSTUDIO_TIA_OPENNESS_DIR` toi PublicAPI version dung.
- [ ] Neu can test reflection legacy, set them `GRAFCETSTUDIO_TIA_OPENNESS_DIR` toi PublicAPI version dung.
- [ ] Mo project trong TIA Portal hoac cung cap `ProjectPath` hop le de bridge mo project, sau do bam Push to TIA tu GrafcetStudio.
- [ ] Xac nhan ket qua push tra ve success va block xuat hien trong folder dich.
- [ ] Mo block vua push va xac nhan Contact/Coil/OR/AND dung voi preview XML.
- [ ] Compile PLC software va ghi ket qua.

## Bien ban ket qua

| Ngay | TIA version | Test case | Ket qua | Ghi chu |
| --- | --- | --- | --- | --- |
| 2026-06-27 | V19 installed, not executed | Environment discovery | Prepared only | Tim thay Portal V19 va PublicAPI V16-V19 tren may nay; chua co project mau/GUI confirmation nen chua xac nhan import/compile pass. |

## Loi thuong gap va cach xu ly

| Loi | Dau hieu | Cach xu ly |
| --- | --- | --- |
| Openness permission | Push direct bi access denied, security exception, hoac `TiaOpennessUnavailable`. | Cai Openness feature, them Windows user vao Siemens TIA Openness group dung version, sign out/sign in hoac restart, chay GrafcetStudio cung user do. |
| Namespace/version mismatch | Import XML thu cong bao schema/namespace khong duoc ho tro. | Doi `tiaVersion`/SimaticML namespace theo XML export mau tu TIA version dang test; uu tien export mot FC LAD tu TIA roi diff voi XML sinh boi generator. |
| Block overwrite | Import/push that bai vi block da ton tai. | Test ro `FailIfExists`, `Overwrite`, `Rename`; voi direct push dung `OverwriteMode=Overwrite` khi chap nhan ghi de. |
| Missing global tags | Compile loi unknown tag/global variable not found. | Tao PLC tag table truoc, doi parameter scope sang local/input/output neu phu hop, hoac bo sung generator tag table trong milestone sau. |
| Wrong target path | Direct push tra `DeviceNotFound`, `PlcNotFound`, hoac `TargetFolderNotFound`. | Copy dung ten device/PLC/folder tu project tree TIA; dung folder path tu Program blocks, vi du `Program blocks/Grafcet`. |




