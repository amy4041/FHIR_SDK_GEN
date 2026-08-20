# MyFhirSdk Primitive Generation Phase B Handoff

Version 1.1

- 文件狀態：Ready for Phase B implementation
- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9
- Phase A 基準：Completed（A0-A6，A6 merge commit `7cb4159`）
- 上位邊界：`MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`
- 前階段實作指引：`MyFhirSdk_Runtime_Phase_A_Implementation_Guide.md`
- Phase B 實作指引：
  `MyFhirSdk_Primitive_Generation_Phase_B_Implementation_Guide.md`

## 1. 目的

本文件是 Runtime Phase A 到 primitive generation Phase B 的可執行交接契約。Phase B
應依本文件生成薄 primitive wrapper declarations 與 deterministic registry composition，
不重新實作 codec、format validator、FHIR JSON primitive metadata 或 validation pipeline。

本文件只固定目前 Runtime 已驗證的 17 個 primitive。載入完整官方 R5 primitive inventory
後，未列入 matrix 的官方 primitive 必須明確新增 policy 或標示 unsupported；不得猜測 CLR
type、codec 或 validator。

## 2. Phase B 輸出邊界

Phase B 應產生：

1. `public sealed` primitive wrapper declarations。
2. wrapper 對應的 primitive definition composition entries。
3. generation manifest，記錄 FHIR、CodeGen、Runtime contract 與 policy 版本。
4. deterministic source，供 golden、Roslyn compilation 與 Runtime contract tests 驗證。

Phase B 不應產生：

- `IPrimitiveCodec`、`IPrimitiveValidator` 或 validation algorithm；
- Parser、Serializer、Validator engine；
- `IsValid()` public method；
- 可由 SDK 使用者修改的 Runtime registry；
- HTTP Client、IG/Profile validation 或 complex datatype declarations。

## 3. Primitive definition matrix

下表的 codec/validator key 是 generation policy key，不是 public SDK API。Runtime 的
`PrimitiveRegistry` 以 FHIR type name 使用 ordinal comparison，所有 entries 必須唯一並
依 FHIR type name deterministic 排序。

| FHIR type | Wrapper | CLR backing type | FHIR JSON raw token | Codec key | Validator key | Literal preservation |
|---|---|---|---|---|---|---|
| `base64Binary` | `FhirBase64Binary` | `string` | string | `string` | `base64Binary` | No |
| `boolean` | `FhirBoolean` | `bool?` | boolean | `boolean` | `boolean` | No |
| `canonical` | `FhirCanonical` | `string` | string | `string` | `canonical` | No |
| `code` | `FhirCode` | `string` | string | `string` | `code` | No |
| `date` | `FhirDate` | `string` | string | `string` | `date` | No |
| `dateTime` | `FhirDateTime` | `string` | string | `string` | `dateTime` | No |
| `decimal` | `FhirDecimal` | `decimal?` | number | `decimal-literal` | `decimal` | Yes：保留 JSON number raw text |
| `id` | `FhirId` | `string` | string | `string` | `id` | No |
| `instant` | `FhirInstant` | `string` | string | `string` | `instant` | No |
| `integer` | `FhirInteger` | `int?` | number | `integer` | `integer` | No |
| `integer64` | `FhirInteger64` | `long?` | string | `integer64-literal` | `integer64` | Yes：保留 JSON string literal |
| `markdown` | `FhirMarkdown` | `string` | string | `string` | `markdown` | No |
| `positiveInt` | `FhirPositiveInt` | `int?` | number | `integer` | `positiveInt` | No |
| `string` | `FhirString` | `string` | string | `string` | `string` | No |
| `unsignedInt` | `FhirUnsignedInt` | `int?` | number | `integer` | `unsignedInt` | No |
| `uri` | `FhirUri` | `string` | string | `string` | `uri` | No |
| `url` | `FhirUrl` | `string` | string | `string` | `url` | No |

### 3.1 Wrapper declaration contract

每個一般 wrapper 必須符合：

```csharp
public sealed class FhirString : PrimitiveType<string>
{
    public FhirString()
    {
    }

    public FhirString(string? value)
        : base(value)
    {
    }
}
```

- namespace 為 generation policy 指定的 model namespace；目前相容值為
  `MyFhirSdk.Primitives`。
- wrapper 只包含型別身分、無參數 constructor、CLR value constructor 與文件。
- wrapper 不實作 codec、regex、validation algorithm、registry lookup 或 `IsValid()`。
- wrapper 透過 public/protected `PrimitiveType<T>` contract 取得 `Value`、`HasValue`、
  primitive `id` 與 extension metadata 行為。

### 3.2 Literal-preserving wrapper contract

`decimal` 與 `integer64` 除一般 constructor 外，必須提供：

```text
public Wrapper(string? literal)
public string? Literal { get; }
```

Runtime `LiteralPrimitiveCodec` 會以 string constructor 建立 wrapper，並讀取 public
`Literal` property 保留原始 representation。這是 Phase B 必須生成的 wrapper shape；
不得將 trailing zero、exponent 或 integer64 string representation 正規化後再輸出。

### 3.3 B0 核准的 public API 相容成員

3.1 的薄 wrapper 原則不授權 Phase B 無聲移除已進入 public API snapshot 的 declarative
constants 或 presentation-only `ToString()` behavior。Generated wrappers 必須保留下列
B0 compatibility contract：

| Wrapper | Compatibility member / behavior |
|---|---|
| `FhirString` | `MaxLength = 1048576` |
| `FhirMarkdown` | `MaxLength = 1048576` |
| `FhirDecimal` | `MaxIntegerDigits = 18`、`MaxFractionDigits = 17`、`MaxExponentDigits = 9` |
| `FhirBoolean` | `ToString()` 輸出 lowercase `true`/`false`，null 輸出空字串 |
| `FhirInteger`、`FhirPositiveInt`、`FhirUnsignedInt` | `ToString()` 使用 invariant value representation |
| `FhirDecimal`、`FhirInteger64` | `ToString()` 優先輸出 `Literal`，否則使用 invariant value representation |

這些成員只提供 public API 與字串呈現相容性，不得執行 JSON token selection、registry
lookup、format validation 或產生 validation issue。Policy 應使用封閉的 behavior key 與
結構化 constant data 表達，不得注入任意 C# source。未來若要移除，必須另有明確的
breaking API decision 並核准 public API snapshot 差異。

## 4. Generation policy schema

Phase B 應把目前散落於 `CSharpTypeMapper` 與 `PrimitiveRegistry` 的 primitive 決策整理成
單一、版本化、可驗證的 policy model。建議最小 schema：

```yaml
schemaVersion: 1
policyVersion: 1.0.0
fhirVersion: 5.0.0
runtimeContractVersion: phase-a-v1
primitiveNamespace: MyFhirSdk.Primitives
primitives:
  - fhirTypeName: decimal
    canonical: http://hl7.org/fhir/StructureDefinition/decimal
    wrapperName: FhirDecimal
    clrValueType: decimal?
    jsonToken: number
    codecKey: decimal-literal
    validatorKey: decimal
    preserveLiteral: true
    literalConstructor: true
    literalPropertyName: Literal
    supportStatus: supported
```

每筆 policy 至少包含：

- `fhirTypeName`、StructureDefinition canonical 與 FHIR version；
- wrapper name、namespace、CLR backing type；
- JSON raw token；
- Runtime codec key、validator key；
- literal preservation 與額外 constructor/property shape；
- `supported` 或具理由的 `unsupported` 狀態。

### 4.1 Policy validation rules

Generation 開始前必須驗證：

1. FHIR type name、canonical 與 wrapper name 使用 ordinal uniqueness。
2. 官方 primitive inventory 每一筆都有 supported/unsupported 決策。
3. codec/validator key 必須來自 Runtime 支援的封閉 key set；未知 key 直接失敗。
4. `decimal-literal` 必須搭配 JSON number、`decimal?` 與 literal preservation。
5. `integer64-literal` 必須搭配 JSON string、`long?` 與 literal preservation。
6. 非 literal codec 不得要求 `Literal` property。
7. output 依 FHIR type name ordinal 排序，重複或缺漏不得 fallback。

目前 `CSharpTypeMapper.PrimitiveTypeNames` 是 MVP 過渡 mapping，且未涵蓋完整官方 R5
primitive inventory。Phase B 應由 validated policy 取代這份 mapping，不在 mapper 中再增加
第二套 primitive 決策來源。

## 5. Registry composition 與 assembly 過渡策略

Runtime primitive contracts 維持 `internal`，不得為了讓 generated models 直接建立 codec
或 validator 而改成 public。Phase B 初期採以下策略：

1. wrapper source 與 generated primitive registry composition source 都產生到 repository
   管理的 generated output directory。
2. 在目前單一 SDK assembly 內編譯 generated output，使 composition source 可連接 internal
   Runtime codec/validator key。
3. 手寫 wrappers 保留為 regression oracle；generated output 全部 contract tests 通過後，
   再以一次可回復的切換移除對應手寫 declarations。
4. Runtime/R5 Models 實體 assembly 拆分不屬於 Phase B；不得使用
   `InternalsVisibleTo` 或公開 codec/validator 作為臨時捷徑。

若 Phase C/D 要拆成獨立 `MyFhirSdk.Runtime` 與 `MyFhirSdk.R5.Models` assemblies，必須先以
ADR 決定跨 assembly composition seam，例如獨立 facade/composition assembly 或 build-time
manifest。該決策不得形成 Runtime 反向依賴 Models 的循環。

Phase A 已讓 Parser、Serializer、Validator 透過 internal `PrimitiveRegistry` injection
進行 contract test；public constructors 仍使用 immutable default registry，SDK 使用者無法
替換內建 primitive behavior。

## 6. Bootstrap debt register

下列項目目前保留在 Runtime assembly 是為了維持 base hierarchy 與序列化相容，不代表
最終 owner 已確定為 Runtime：

| Debt | Current owner | 原因 | Target owner / phase | Exit criterion |
|---|---|---|---|---|
| `Element.Id`、`Element.Extension` | Runtime bootstrap | primitive metadata 與所有 datatype 共用 | Phase C R5 Models shape | base model/metadata seam ADR 完成且無循環依賴 |
| `BackboneType.ModifierExtension`、`BackboneElement.ModifierExtension` | Runtime bootstrap | generated backbone 必須繼承現有 shape | Phase C R5 Models shape | generated base shape 通過 Parser/Serializer/Validator contract |
| `Resource.Id`、`Meta`、`ImplicitRules`、`Language` | Runtime bootstrap | Resource base contract 目前直接包含 R5 properties | Phase C，分類 contract 與 R5 shape | Resource base property ownership ADR 與 migration tests 完成 |
| `DomainResource.Text`、`Contained`、`Extension`、`ModifierExtension` | Runtime bootstrap | concrete R5 types 直接出現在共同 base class | Phase C R5 Models shape | generated DomainResource base shape 無 Runtime→Models 循環 |
| `Extension` declaration | Runtime bootstrap | primitive metadata、extension value[x] 流程直接依賴 | Phase C 或明確 foundational Runtime contract | 完整 R5 generation 後做 ownership ADR |
| `Meta` declaration | Runtime bootstrap | `Resource.Meta` 與 datatype inference 直接依賴 | Phase C R5 Models | generated Meta 與 metadata provider entries 通過回歸 |
| `Narrative` declaration | Runtime bootstrap | `DomainResource.Text` 直接依賴 | Phase C R5 Models | generated Narrative/xhtml policy 與回歸完成 |

在 exit criterion 完成前，不得只為了目錄或 assembly 純化直接移除這些 public members。

## 7. Phase B 必須沿用的驗收測試

- `PublicApiSnapshotTests`：Runtime public contract 未意外改變。
- `RuntimeContractCompilationTests`：external generated wrapper 只依賴 public contract。
- `RuntimeContractAccessibilityTests`：codec、validator、registry 維持 internal。
- `RuntimeModelDependencyTests`：Runtime engine 不出現 concrete wrapper 類別分支。
- `PrimitiveRuntimeContractTests`：definition matrix、codec token、round-trip、validator、
  duplicate/missing registration。
- `PhaseBPrimitiveHandoffTests`：薄 wrapper 可 serialize、parse、validate。
- Parser/Serializer JSON fixtures：metadata-only、array alignment、decimal/integer64 literal。
- CodeGen golden/Roslyn tests：generated source deterministic 且可編譯。

## 8. Phase B Definition of Done

- 官方 primitive inventory 與 versioned policy 一一對應，unsupported 項目有明確理由。
- 17 個既有 wrappers 的 generated declarations 與 public API snapshot 相容。
- generated registry entries 與本文件 matrix 完全一致且 deterministic。
- 所有 Phase A contract tests、Parser/Serializer/Validation/CodeGen tests 通過。
- generated output 連續執行兩次 byte-for-byte 相同。
- generated wrappers 通過後才移除手寫 declarations；不得同時保留重複型別。
- 未公開 internal codec/validator/registry，未新增 primitive `IsValid()`。
- 文件記錄 FHIR、policy、CodeGen 與 Runtime contract versions。

## 9. Phase A cleanup disposition

| Item | A6 disposition | Reason / next owner |
|---|---|---|
| `PrimitiveRegistry.TryGet` | Removed | 無 production caller；required lookup 已提供明確 missing failure |
| Parser/Serializer static registry fields | Removed | 改為 instance-held internal injection，public constructor仍使用 immutable default |
| Validator/PrimitiveFormatRule static registry | Removed | registry 由 `FhirValidator` composition root 傳入 |
| `PrimitiveRegistry.Default` | Retained intentionally | 唯一 immutable default composition root；Phase B 取代 entries，不開放使用者 mutation |
| `Literal` reflection in literal codec/validator | Retained as contract | `decimal`/`integer64` 跨 generated wrapper 的 literal-preservation mechanism；由本文件 3.2 固定 shape |
| `R5ModelMetadataProvider` assembly scan | Retained with Phase C owner | 已隔離於 `ModelMetadata/R5`，不在 Runtime engine；Phase C generated provider 取代 |
| `CSharpTypeMapper.PrimitiveTypeNames` | Retained with Phase B owner | MVP generator 尚在使用；Phase B validated policy 必須取代，不能再擴充第二套 mapping |
| `CSharpTypeMapper` complex whitelist | Retained with Phase C owner | MVP datatype scope仍需要；完整 definition inventory 後移除 |

A6 audit 未發現其他可安全移除且無 owner 的 transitional adapter、舊 primitive 類別名稱
分支或 model-specific Runtime engine registry。Architecture tests 持續作為 CI gate。

## 10. Phase B review checklist

### Runtime

- [ ] Generated wrapper 只依賴 approved public Core contract。
- [ ] Codec、validator、definition、registry 仍為 internal，沒有 `IsValid()` public API。
- [ ] Generated definition matrix 與本文件 17 筆相符，duplicate/missing 明確失敗。
- [ ] Public API snapshot 的任何差異都有明確相容性決策。

### Serialization

- [ ] Parser/Serializer 不引用 concrete wrapper 類別或 wrapper name。
- [ ] 每個 codec 的 accepted/rejected JSON token matrix 通過。
- [ ] primitive raw/metadata array alignment 與 metadata-only cases 通過。
- [ ] `decimal`、`integer64` serialize-parse-serialize 保持原始 literal。

### Validation

- [ ] 使用者只經 `FhirValidator.Validate(Resource)` 取得 primitive issue。
- [ ] 每個 validator key 有 valid/invalid cases，issue path/message 維持相容。
- [ ] generated wrapper 使用 Runtime validator，wrapper 本身沒有 validation algorithm。
- [ ] Resource/Element `id` 仍使用同一 `id` definition。

### CodeGen

- [ ] 官方 primitive inventory 每筆都有 supported/unsupported policy。
- [ ] `CSharpTypeMapper` primitive dictionary 已由單一 versioned policy 取代。
- [ ] wrapper 與 registry composition output deterministic 且 Roslyn compilation 通過。
- [ ] manifest 記錄 FHIR、policy、CodeGen 與 Runtime contract version。
- [ ] 連續兩次 generation byte-for-byte 相同，且不覆寫手寫 Runtime source。
