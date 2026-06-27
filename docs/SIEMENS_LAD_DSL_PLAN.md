# Siemens LAD DSL + TIA Openness Plan

## Muc tieu

Bo sung luong gencode Siemens cho GrafcetStudio theo huong:

- User chinh template LAD bang file JSON DSL, khong can recompile app/TiaBridge.
- App sinh LAD network bang `SimaticML` thay vi sinh file mnemonic/AWL trung gian.
- Giai doan dau xuat Siemens Openness XML de preview/import thu cong.
- Giai doan sau dung TIA Openness de push/import truc tiep vao project TIA Portal dang mo hoac project duoc chon.

## Trang thai hien tai

Da co nen tang ban dau:

- Project `src/SimaticML` da duoc them vao solution.
- `GrafcetStudio.App` reference `src/SimaticML/SimaticML.csproj`.
- Generator moi `siemens-lad` da duoc dang ky trong DI.
- File DSL mac dinh nam tai `templates/siemens-lad/default.lad.json`.
- Generator hien tai doc JSON DSL, map parameter sang PLC tag theo render context, build expression de quy va xuat Siemens Openness XML.
- `LoadTemplate()` tim custom template theo thu tu ro rang va bao loi kem tat ca path da thu.
- JSON DSL duoc validate sau khi load voi loi kem network id va JSON path logic.

Cac file chinh:

- `src/GrafcetStudio.App/Generators/Siemens/SiemensLadDslGenerator.cs`
- `templates/siemens-lad/default.lad.json`
- `src/GrafcetStudio.App/App.xaml.cs`
- `src/GrafcetStudio.App/GrafcetStudio.App.csproj`
- `src/GrafcetStudio.App.Tests/GeneratorSmokeTests.cs`
- `src/GrafcetStudio.App.Tests/CodegenPayloadTestAdapter.cs`
- `.gitignore`

## Kien truc mong muon

```text
GrafcetStudio project payload
    -> Siemens LAD JSON DSL template
    -> Parameter resolver
    -> BuildExpression() recursive tree builder
    -> SimaticML LAD segment / FC / FB XML
    -> TIA Openness import/push service
    -> TIA Portal project block folder
```

## DSL JSON Design

### Metadata

```json
{
  "version": "1.0",
  "platform": "siemens-lad",
  "tiaVersion": 17,
  "blockName": "{{unit.name}}_Grafcet_LAD",
  "blockNumber": 1
}
```

### Parameters

`parameters[]` khai bao cac bien hop le ma network duoc phep dung.

Vi du:

```json
{
  "key": "exec",
  "source": "step.execAddress",
  "default": "Exec",
  "scope": "global",
  "dataType": "Bool"
}
```

Cac source hien ho tro:

- `step.execAddress`
- `step.doneAddress`
- `transition.condition`
- `action.address`
- literal fallback qua `default`

Scope hien ho tro:

- `global` -> `SimaticGlobalVariable`
- `local` hoac unknown -> `SimaticLocalVariable`
- `input` / `output` / `inout` / `temp` / `constant` -> them vao interface FC

### Expression

Mapping logic:

- `(a & b)` -> `{ "type": "AND", "nodes": [...] }`
- `(a | b)` -> `{ "type": "OR", "nodes": [...] }`
- `!a` -> `{ "type": "NOT", "node": { "type": "TAG", "ref": "a" } }`
- `a` -> `{ "type": "TAG", "ref": "a" }`

Vi du:

```json
{
  "type": "AND",
  "nodes": [
    { "type": "TAG", "ref": "prevDone" },
    {
      "type": "OR",
      "nodes": [
        { "type": "TAG", "ref": "condition" },
        { "type": "NOT", "node": { "type": "TAG", "ref": "feedback" } }
      ]
    }
  ]
}
```

### Output

```json
{
  "type": "coil",
  "ref": "exec"
}
```

Cac output type hien ho tro:

- `coil`
- `set_coil`
- `reset_coil`

## Milestone 1 - DSL Generator on dinh

Muc tieu: generator `siemens-lad` sinh XML dung va co the preview/import thu cong.

Checklist:

- [x] Them `SiemensLadDslGenerator`.
- [x] Dang ky platform `siemens-lad`.
- [x] Them template JSON mac dinh.
- [x] Build pass voi `dotnet build src\GrafcetStudio.App\GrafcetStudio.App.csproj -v:minimal`.
- [x] Them option `siemens-lad` vao UI select trong `src/web/js/codegen/modal.js`.
- [ ] Render output preview voi extension `.xml` ro rang.
- [x] Tao unit test smoke cho generator sinh XML co `SW.Blocks.FC`, `FlgNet`, `Part Name="Contact"`, `Part Name="Coil"`.
- [x] Test custom `TemplateRootPath` verifies `<TemplateRootPath>/siemens-lad.json` overrides `<TemplateRootPath>/default.lad.json`.
- [x] `LoadTemplate()` search order is explicit and missing-template errors include every attempted path.
- [x] Test project compile blocker da xu ly bang C# test adapter toi thieu cho `GrafcetStudioCodegenPayload`/project payload contract; `dotnet test src\GrafcetStudio.App.Tests\GrafcetStudio.App.Tests.csproj -v:minimal` pass 20/20.
- [x] Chuan hoa validation/error message cho JSON DSL: platform, parameters, networks, output refs, expression refs/nodes/NOT.

## Milestone 2 - Map dung Grafcet runtime

Muc tieu: thay vi lay step/action/transition dau tien, generator sinh network theo tung step/transition/action that.

Checklist:

- [x] Thiet ke context lap theo flow/step/transition/action.
- [ ] Bo sung placeholder template:
  - `{{project.name}}`
  - `{{unit.name}}`
  - [x] `{{flow.name}}`
  - [x] `{{step.number}}`
  - [x] `{{step.label}}`
  - [x] `{{transition.label}}`
  - [x] `{{action.variable}}`
- [x] DSL ho tro network `repeat`:
  - [x] `steps`
  - [x] `transitions`
  - [x] `actions`
- [x] Parameter resolver nhan context hien tai thay vi chi payload toan cuc.
- [x] Sinh FC mot network cho moi activation/done/action that.
- [ ] Kiem tra duplicate tag/address.
- [ ] Xu ly step initial, macro step, macro port neu can.

## Milestone 3 - DSL validation/schema

Muc tieu: user sua JSON de nhung loi phai ro.

Checklist:

- [x] Tao JSON schema cho `templates/siemens-lad/*.json`.
- [x] Validate bat buoc:
  - [x] `version`
  - [x] `platform = siemens-lad`
  - [x] `parameters[].key` khong rong va khong trung
  - [x] `networks[].id`
  - [x] `networks[].expression`
  - [x] `networks[].output.ref` ton tai trong parameters
- [x] Validate expression:
  - [x] `AND/OR` phai co `nodes` >= 1.
  - [x] `NOT` phai co `node` va hien chi ho tro node type `TAG`.
  - [x] `TAG` phai co `ref` nam trong parameters.
- [x] Bao loi kem network id va path JSON logic.
- [x] Them unit tests cho template loi pho bien.
- [x] Them doc vi du DSL trong `docs/`.

## Milestone 4 - TIA Openness push/import service

Muc tieu: push truc tiep vao project TIA, khong can user thao tac file trung gian.

De xuat abstraction:

```csharp
public interface ISiemensTiaProjectService
{
    Task<SiemensPushResult> PushBlockXmlAsync(SiemensPushRequest request, CancellationToken cancellationToken = default);
}
```

Request nen co:

- `ProjectPath`
- `DeviceName`
- `PlcName`
- `TargetFolderPath`
- `BlockName`
- `XmlContent`
- `OverwriteMode`

Checklist:

- [x] Tao `ISiemensTiaProjectService` abstraction cho push/import XML.
- [x] Tao `SiemensPushRequest`/`SiemensPushResult` voi project path, device/plc, folder, block, XML content/path va overwrite mode.
- [x] Dang ky `UnavailableSiemensTiaProjectService` mac dinh de app khong crash khi may khong cai TIA.
- [x] Ghi extension point cho implementation dung reflection hoac bridge rieng, khong reference cung Siemens TIA assemblies trong app chinh.
- [x] Them tai lieu cau hinh quyen TIA Openness tai `docs/SIEMENS_TIA_OPENNESS_IMPORT.md`.
- [x] Them tai lieu van hanh/checklist tai `docs/SIEMENS_TIA_OPENNESS_SETUP.md` va `docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md`.
- [x] Them mock/integration notes vi moi truong hien tai khong co TIA Portal.
- [x] Them unit tests cho unavailable service.
- [x] Xac dinh version TIA Portal can ho tro ban dau: V17/V18/V19/V20; may hien tai phat hien TIA Portal V19 va PublicAPI V16-V19, chua xac nhan import/compile pass.
- [x] Xac dinh vi tri Siemens Openness assemblies qua `GRAFCETSTUDIO_TIA_OPENNESS_DIR` tro toi PublicAPI folder.
- [x] Tao service wrapper de tranh app chinh phu thuoc cung vao TIA DLL neu may khong cai TIA.
- [x] Them config path cho TIA Portal / project.
- [x] Them `ReflectionSiemensTiaProjectService` import XML vao block folder bang TIA Openness reflection runtime load.
- [x] Ho tro overwrite mode best-effort qua TIA `ImportOptions` (`Override`/`Overwrite`, `Rename`, default).
- [x] Tra loi ro neu TIA Openness chua duoc cau hinh trong implementation mac dinh.
- [x] Log import result/error qua typed `SiemensPushResult` va `Trace.WriteLine`.

## Milestone 5 - UI/UX cho Siemens direct push

Muc tieu: user chon Siemens LAD, chinh template va bam push.

Checklist:

- [x] Them target `Siemens LAD XML` hoac `Siemens LAD Direct` vao modal codegen.
- [x] Them path/config:
  - TIA project path
  - PLC target
  - block folder
  - template folder
- [x] Co 2 mode:
  - Preview XML
  - Push to TIA
- [x] Hien thi trang thai push/import.
- [ ] Cho phep mo/chinh `default.lad.json` tu UI template editor.
- [x] Luu config vao config service hien co.
- [x] Noi WebView host bridge `PUSH_SIEMENS_LAD`: UI gui raw codegen payload + TIA target config, C# host tu generate `siemens-lad` XML roi goi `ISiemensTiaProjectService`.
- [x] Them unit test boundary cho Siemens LAD push orchestrator generate XML truoc khi goi TIA service.

## Milestone 6 - Test thuc te voi TIA Portal

Muc tieu: xac nhan block import compile duoc trong TIA.

Checklist:

- [x] Kiem tra moi truong hien tai: phat hien TIA Portal V19 tai `C:\Program Files\Siemens\Automation\Portal V19` va PublicAPI V16-V19.
- [x] Tao checklist manual test khi chua the xac nhan GUI/import/compile: `docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md`.
- [ ] Tao project TIA sample.
- [ ] Import XML FC sinh ra tu `siemens-lad` thu cong trong TIA Portal.
- [ ] Push direct vao project bang TIA Openness reflection adapter.
- [ ] Kiem tra network hien thi dung LAD Contact/Coil/OR/AND.
- [ ] Kiem tra tag global/local dung scope.
- [ ] Compile PLC software khong loi.
- [ ] Test voi TIA V19 tren may hien tai; sau do lap lai voi V17/V18/V20 neu can ho tro chinh thuc.
- [ ] Ghi lai cac namespace/version khac nhau cua SimaticML neu co.

Ket qua moi nhat:

- 2026-06-27: TIA Portal V19/PublicAPI V16-V19 available, nhung chua thuc hien import XML/direct push/compile vi can project TIA sample va xac nhan GUI. Trang thai Milestone 6 la prepared only, chua pass.

Loi thuong gap can kiem trong test:

- Openness permission: user chua nam trong Siemens TIA Openness group hoac chua restart/sign-in lai.
- Namespace/version mismatch: XML SimaticML khong khop schema cua TIA version dang import.
- Block overwrite: block da ton tai va overwrite mode khong phu hop.
- Missing global tags: scope `global` tham chieu PLC tags chua ton tai.

## Rui ro ky thuat

- TIA Openness yeu cau cai TIA Portal dung version va cap quyen cho user/app.
- Siemens assemblies thuong khong phu hop de reference truc tiep neu may khong cai TIA.
- `SimaticML` hien ho tro LAD segment co ban; network phuc tap nhu timer/call/move co the can mo rong DSL.
- `NOT` nested expression hien bi gioi han: chi ho tro `NOT` cho `TAG`; can De Morgan neu can logic phuc tap.
- Scope `global` chi tao access toi tag da ton tai; neu muon tu tao tag table can them generator tag table rieng.

## Quyet dinh da chot

- Chon strategy reflection runtime load `Siemens.Engineering.dll` cho direct TIA push de khong pha build CI/dev khi may khong cai TIA; enable bang `GRAFCETSTUDIO_TIA_OPENNESS_MODE=reflection` va `GRAFCETSTUDIO_TIA_OPENNESS_DIR`.
- Khong thay platform `siemens` cu de tranh pha flow `.awl` hien tai.
- Tao platform moi `siemens-lad`.
- JSON DSL thay HBS cho LAD structure.
- `BuildExpression()` xu ly object tree, khong parse chuoi logic.
- Template mac dinh nam trong repo tai `templates/siemens-lad/default.lad.json`.
- Push truc tiep vao TIA se la phase rieng sau khi XML generation on dinh.

## Lenh kiem tra

Build app:

```powershell
dotnet build src\GrafcetStudio.App\GrafcetStudio.App.csproj -v:minimal
```

Test project:

```powershell
dotnet test src\GrafcetStudio.App.Tests\GrafcetStudio.App.Tests.csproj -v:minimal
```

Kiem tra file thay doi:

```powershell
git status --short
```

Tim Siemens generator:

```powershell
rg -n "siemens-lad|SiemensLadDslGenerator|BuildExpression" src templates docs
```

## Ghi chu cho phien lam viec sau

Khi bat dau phien moi, nen lam theo thu tu:

1. Doc file nay.
2. Chay `git status --short` de biet thay doi chua commit.
3. Chay build app.
4. Chon milestone tiep theo.
5. Chi sua dung pham vi milestone dang lam.

Uu tien de xuat cho phien tiep theo:

1. Chay manual import XML FC tren TIA Portal V19 voi project sample va ghi ket qua vao `docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md`.
2. Chay direct push bang `GRAFCETSTUDIO_TIA_OPENNESS_MODE=reflection` va PublicAPI V19, sau do compile PLC software.
3. Export mot FC LAD mau tu TIA V19 de diff namespace/SimaticML voi XML generator.
4. Them generator PLC tag table cho parameters scope `global` de giam loi missing global tags.
5. Render output preview voi extension `.xml` ro rang.



