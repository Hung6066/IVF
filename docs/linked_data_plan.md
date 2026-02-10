# Linked Data Between Forms Across Categories — Implementation Plan

## 1. Problem Statement

Currently, form templates in the IVF system are isolated silos. A patient's data entered in one form (e.g., "Initial Consultation" in the Clinical category) cannot automatically flow to another form (e.g., "Follicle Monitoring" in the Lab category). Clinicians must re-enter the same information — height, weight, blood type, AMH level — every time a new form is opened.

The **Concept** system already provides _semantic_ linking (two fields across forms can reference the same `ConceptId`), but there is **no runtime data propagation** — no mechanism to pre-populate, reference, or aggregate values across forms.

## 2. Goals

| # | Goal | Description |
|---|------|-------------|
| G1 | **Cross-form pre-population** | When opening a form for a patient, auto-fill fields whose `ConceptId` matches data already captured in another form |
| G2 | **Linked field references** | A field in Form B can explicitly reference a field in Form A and display/copy its latest value |
| G3 | **Patient data timeline** | Aggregate all values for a Concept across forms/responses to show longitudinal data (e.g., E2 levels over 5 visits) |
| G4 | **Cycle-scoped data sharing** | Within a TreatmentCycle, share data across all forms attached to that cycle |
| G5 | **Category-aware data flow** | Define data flow rules between categories (e.g., Clinical → Lab → Outcome) |

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    DOMAIN LAYER                          │
│                                                          │
│  FormField ──── ConceptId ────► Concept                  │
│      │                            │                      │
│      │  NEW                       │                      │
│      ▼                            ▼                      │
│  LinkedFieldSource          ConceptMapping               │
│  (SourceTemplateId,         (SNOMED, LOINC)              │
│   SourceFieldId,                                         │
│   LinkType)                                              │
│                                                          │
│  DataFlowRule  ◄─── NEW                                  │
│  (SourceCategoryId,                                      │
│   TargetCategoryId,                                      │
│   ConceptId,                                             │
│   FlowType, Priority)                                    │
│                                                          │
│  PatientConceptSnapshot ◄─── NEW                         │
│  (PatientId, ConceptId,                                  │
│   LatestValue, ResponseId,                               │
│   CycleId, CapturedAt)                                   │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                 APPLICATION LAYER                         │
│                                                          │
│  Query: GetLinkedDataForPatient(PatientId, TemplateId)   │
│    → Returns pre-fill values for every field with a      │
│      ConceptId, sourced from latest responses             │
│                                                          │
│  Query: GetConceptTimeline(PatientId, ConceptId)         │
│    → Returns all values over time for a concept          │
│                                                          │
│  Command: SubmitFormResponse (ENHANCED)                   │
│    → After saving, update PatientConceptSnapshot          │
│                                                          │
│  Command: ConfigureDataFlowRule                          │
│    → Admin defines which categories feed data to others  │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                   API / CLIENT LAYER                     │
│                                                          │
│  GET /api/forms/linked-data/{templateId}                 │
│      ?patientId=...&cycleId=...                          │
│    → Returns map of fieldId → suggested value + source   │
│                                                          │
│  GET /api/forms/concept-timeline/{conceptId}             │
│      ?patientId=...                                      │
│    → Returns value history                               │
│                                                          │
│  Form Renderer: auto-fills linked fields on load         │
│  Form Builder: configure linked field sources            │
│  Admin UI: manage data flow rules between categories     │
└─────────────────────────────────────────────────────────┘
```

## 4. New Domain Entities

### 4.1 `PatientConceptSnapshot` — Materialized Latest Values

The core performance optimization: a denormalized table that stores the **latest known value** for each (Patient, Concept) pair, updated on every form submission.

```csharp
public class PatientConceptSnapshot : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid ConceptId { get; private set; }
    public Guid FormResponseId { get; private set; }     // Which response captured this
    public Guid FormFieldId { get; private set; }         // Which field captured this
    public Guid? CycleId { get; private set; }            // Treatment cycle scope (nullable)

    // Polymorphic value storage (mirrors FormFieldValue)
    public string? TextValue { get; private set; }
    public decimal? NumericValue { get; private set; }
    public DateTime? DateValue { get; private set; }
    public bool? BooleanValue { get; private set; }
    public string? JsonValue { get; private set; }

    public DateTime CapturedAt { get; private set; }      // When the source response was submitted

    // Navigation
    public Patient Patient { get; private set; } = null!;
    public Concept Concept { get; private set; } = null!;
    public FormResponse FormResponse { get; private set; } = null!;
    public FormField FormField { get; private set; } = null!;
    public TreatmentCycle? Cycle { get; private set; }
}
```

**Index strategy:**
- Unique composite: `(PatientId, ConceptId, CycleId)` — one latest value per concept per patient per cycle
- Index: `(PatientId)` — fast lookup for "all known data for patient"
- Index: `(ConceptId, PatientId)` — fast lookup for concept timeline

### 4.2 `DataFlowRule` — Category-to-Category Data Flow Configuration

Defines which categories can feed data to which other categories, and controls the flow.

```csharp
public class DataFlowRule : BaseEntity
{
    public Guid SourceCategoryId { get; private set; }
    public Guid TargetCategoryId { get; private set; }
    public Guid? ConceptId { get; private set; }          // null = all concepts flow
    public DataFlowType FlowType { get; private set; }    // AutoFill, Suggest, Reference
    public int Priority { get; private set; }             // Higher = preferred source
    public bool IsActive { get; private set; } = true;

    // Navigation
    public FormCategory SourceCategory { get; private set; } = null!;
    public FormCategory TargetCategory { get; private set; } = null!;
    public Concept? Concept { get; private set; }
}

public enum DataFlowType
{
    AutoFill = 1,    // Auto-populate field with latest value (editable)
    Suggest = 2,     // Show as suggestion, user must confirm
    Reference = 3,   // Read-only reference display, not editable
    Copy = 4         // Copy value into new response on submit
}
```

### 4.3 `LinkedFieldSource` — Explicit Field-to-Field Link

Allows a form builder to explicitly wire one field to another (even without a shared Concept).

```csharp
public class LinkedFieldSource : BaseEntity
{
    public Guid TargetFieldId { get; private set; }       // The field that receives data
    public Guid SourceTemplateId { get; private set; }    // Source form template
    public Guid SourceFieldId { get; private set; }       // Source field within that template
    public DataFlowType LinkType { get; private set; }
    public string? TransformExpression { get; private set; } // Optional: e.g., "value * 2.54"

    // Navigation
    public FormField TargetField { get; private set; } = null!;
    public FormTemplate SourceTemplate { get; private set; } = null!;
    public FormField SourceField { get; private set; } = null!;
}
```

## 5. Enhanced Existing Entities

### 5.1 `FormField` — New Properties

```csharp
// Add to FormField:
public DataFlowType? LinkedDataBehavior { get; private set; }  // How to handle linked data for this field
public bool AcceptsLinkedData { get; private set; } = true;     // Can be disabled per-field
```

### 5.2 `FormCategory` — New Properties

```csharp
// Add to FormCategory:
public int DataFlowPriority { get; private set; }  // Default priority when this category is a data source
```

## 6. Implementation Phases

### Phase 1: Foundation — PatientConceptSnapshot (Sprint 1, ~5 days)

| Task | Effort | Description |
|------|--------|-------------|
| 1.1 Create `PatientConceptSnapshot` entity | 2h | Entity, EF config, migration |
| 1.2 Update `SubmitFormResponseCommand` | 4h | After saving a response for a patient, upsert snapshots for every field that has a `ConceptId` |
| 1.3 Backfill service | 3h | One-time job to scan all existing `FormResponse` + `FormFieldValue` and build initial snapshots |
| 1.4 Create `GetLinkedDataQuery` | 4h | Given a `(PatientId, TemplateId, CycleId?)`, return a `Dictionary<fieldId, LinkedDataValue>` with latest known values for each concept-linked field |
| 1.5 API endpoint | 2h | `GET /api/forms/linked-data/{templateId}?patientId=...&cycleId=...` |
| 1.6 Frontend: auto-fill on form load | 6h | Renderer calls linked-data API, pre-fills matching fields, shows provenance indicator |
| 1.7 Unit tests | 4h | Snapshot upsert, query resolution, conflict resolution |

**Deliverable:** When a nurse opens "Follicle Monitoring" for Patient X, fields like Weight, BMI, Blood Type are auto-filled from the last "Initial Consultation" response.

### Phase 2: Data Flow Rules — Category-Level Configuration (Sprint 2, ~4 days)

| Task | Effort | Description |
|------|--------|-------------|
| 2.1 Create `DataFlowRule` entity | 2h | Entity, EF config, migration |
| 2.2 Create `DataFlowType` enum | 0.5h | Add to FormEnums.cs |
| 2.3 CRUD commands/queries | 4h | Create/Update/Delete/List DataFlowRules |
| 2.4 Enhance `GetLinkedDataQuery` | 4h | Apply DataFlowRules: filter which sources are eligible, respect FlowType and Priority |
| 2.5 API endpoints | 2h | CRUD for data flow rules, admin-only |
| 2.6 Admin UI | 8h | Visual category-to-category flow editor (drag-drop or matrix) |
| 2.7 Frontend: flow type UI | 4h | Show AutoFill vs Suggest vs Reference differently in the renderer |

**Deliverable:** Admin configures "Clinical → Lab: AutoFill all concepts" and "Lab → Outcome: Suggest". The renderer respects these rules.

### Phase 3: Explicit Field Links — Builder Integration (Sprint 3, ~4 days)

| Task | Effort | Description |
|------|--------|-------------|
| 3.1 Create `LinkedFieldSource` entity | 2h | Entity, EF config, migration |
| 3.2 CRUD commands | 3h | Create/Delete linked field sources |
| 3.3 Builder UI: "Link to field" panel | 8h | Select source template → source field → link type, inside field properties |
| 3.4 Enhance pre-fill logic | 4h | Resolve both Concept-based and explicit field links, prefer explicit over concept-based |
| 3.5 Transform expressions | 4h | Simple formula engine for unit conversion (e.g., cm→inches) |
| 3.6 Tests | 3h | Explicit links, priority resolution, transform |

**Deliverable:** A form builder can wire "BMI" field in Form B directly to the "BMI" field in Form A, even if neither has a ConceptId.

### Phase 4: Patient Concept Timeline — Longitudinal View (Sprint 4, ~3 days)

| Task | Effort | Description |
|------|--------|-------------|
| 4.1 Create `GetConceptTimelineQuery` | 3h | Query all snapshots for (PatientId, ConceptId), ordered by CapturedAt |
| 4.2 API endpoint | 1h | `GET /api/forms/concept-timeline/{conceptId}?patientId=...` |
| 4.3 Timeline component (Angular) | 8h | Chart/list view showing value changes over time with source form labels |
| 4.4 Integration into renderer | 4h | Click a concept-linked field to see its history in a slide-over panel |
| 4.5 Dashboard widgets | 4h | "Key metrics over time" dashboard for a patient |

**Deliverable:** Clicking the "E2 Level" field shows a timeline chart of all E2 measurements across 10 visits.

### Phase 5: Cycle-Scoped Sharing & Advanced Features (Sprint 5, ~3 days)

| Task | Effort | Description |
|------|--------|-------------|
| 5.1 Cycle-scoped resolution | 4h | When CycleId is present, prefer snapshots from the same cycle |
| 5.2 Staleness indicators | 3h | Show "Last updated 45 days ago" warning for old data |
| 5.3 Conflict resolution UI | 4h | When multiple sources provide the same concept, let user choose or show all |
| 5.4 Audit trail | 3h | Log when pre-filled data is accepted, modified, or rejected |
| 5.5 Bulk operations | 2h | "Apply latest data from Patient record to all draft forms" |
| 5.6 Export/analytics | 4h | Cross-form analytics: "Show all patients where FSH > 10 AND AMH < 1" |

## 7. Data Resolution Algorithm

When resolving linked data for a field, the system uses this priority chain:

```
1. Explicit LinkedFieldSource (highest priority)
   └── Check if TargetField has a LinkedFieldSource
       └── Find latest FormFieldValue from SourceField for the patient
           └── Apply TransformExpression if present

2. DataFlowRule + ConceptId matching
   └── Field has a ConceptId?
       └── Find DataFlowRules where TargetCategory = this form's category
           └── Filter by(SourceCategory, ConceptId or all)
               └── Sort by Priority DESC
                   └── Query PatientConceptSnapshot
                       └── If CycleId present: prefer same-cycle data
                       └── Fallback: latest across all cycles

3. Global Concept matching (no rules configured)
   └── Field has ConceptId but no DataFlowRules exist?
       └── Query PatientConceptSnapshot for (PatientId, ConceptId)
           └── Return latest value, FlowType = Suggest (conservative)
```

**Response DTO:**

```csharp
public record LinkedDataValue(
    string FieldId,           // Target field to fill
    string? TextValue,
    decimal? NumericValue,
    DateTime? DateValue,
    bool? BooleanValue,
    string? JsonValue,
    DataFlowType FlowType,    // How to present this to the user
    string SourceFormName,     // "Initial Consultation"
    string SourceFieldLabel,   // "Body Weight"
    DateTime CapturedAt,       // When was this data captured
    string? ConceptDisplay,    // "Body Weight (LOINC: 29463-7)"
    bool IsStale               // Older than configurable threshold
);
```

## 8. Database Schema (New Tables)

```sql
-- Materialized latest values for fast cross-form lookup
CREATE TABLE patient_concept_snapshots (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id      UUID NOT NULL REFERENCES patients(id),
    concept_id      UUID NOT NULL REFERENCES concepts(id),
    form_response_id UUID NOT NULL REFERENCES form_responses(id),
    form_field_id   UUID NOT NULL REFERENCES form_fields(id),
    cycle_id        UUID REFERENCES treatment_cycles(id),
    text_value      TEXT,
    numeric_value   DECIMAL,
    date_value      TIMESTAMPTZ,
    boolean_value   BOOLEAN,
    json_value      JSONB,
    captured_at     TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX ix_snapshot_patient_concept_cycle
    ON patient_concept_snapshots(patient_id, concept_id, COALESCE(cycle_id, '00000000-0000-0000-0000-000000000000'))
    WHERE NOT is_deleted;
CREATE INDEX ix_snapshot_patient ON patient_concept_snapshots(patient_id) WHERE NOT is_deleted;
CREATE INDEX ix_snapshot_concept ON patient_concept_snapshots(concept_id, patient_id) WHERE NOT is_deleted;

-- Category-to-category data flow configuration
CREATE TABLE data_flow_rules (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_category_id  UUID NOT NULL REFERENCES form_categories(id),
    target_category_id  UUID NOT NULL REFERENCES form_categories(id),
    concept_id          UUID REFERENCES concepts(id),
    flow_type           INTEGER NOT NULL, -- 1=AutoFill, 2=Suggest, 3=Reference, 4=Copy
    priority            INTEGER NOT NULL DEFAULT 0,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ,
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX ix_flow_rules_target ON data_flow_rules(target_category_id) WHERE NOT is_deleted AND is_active;

-- Explicit field-to-field links
CREATE TABLE linked_field_sources (
    id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    target_field_id      UUID NOT NULL REFERENCES form_fields(id) ON DELETE CASCADE,
    source_template_id   UUID NOT NULL REFERENCES form_templates(id),
    source_field_id      UUID NOT NULL REFERENCES form_fields(id),
    link_type            INTEGER NOT NULL, -- Same as DataFlowType
    transform_expression TEXT,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ,
    is_deleted           BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX ix_linked_field_target ON linked_field_sources(target_field_id) WHERE NOT is_deleted;
```

## 9. API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/forms/linked-data/{templateId}?patientId=&cycleId=` | Get pre-fill values for all concept-linked fields |
| `GET` | `/api/forms/concept-timeline/{conceptId}?patientId=` | Get value history for a concept |
| `GET` | `/api/forms/data-flow-rules?targetCategoryId=` | List data flow rules |
| `POST` | `/api/forms/data-flow-rules` | Create a data flow rule |
| `PUT` | `/api/forms/data-flow-rules/{id}` | Update a data flow rule |
| `DELETE` | `/api/forms/data-flow-rules/{id}` | Delete a data flow rule |
| `POST` | `/api/forms/fields/{fieldId}/linked-source` | Create explicit field link |
| `DELETE` | `/api/forms/fields/{fieldId}/linked-source/{id}` | Remove explicit field link |
| `GET` | `/api/forms/patient/{patientId}/data-summary` | All known concept data for a patient |

## 10. Frontend Components

### 10.1 Form Renderer — Pre-fill Integration

```
┌─────────────────────────────────────────────┐
│  Weight (kg)                                │
│  ┌───────────────────────────────────────┐  │
│  │ 65                                    │  │
│  └───────────────────────────────────────┘  │
│  ℹ️ Auto-filled from "Khám ban đầu"        │
│     (12/01/2026) — Click to see history     │
└─────────────────────────────────────────────┘
```

- Fields with linked data show a provenance badge
- `AutoFill` fields are pre-filled and editable
- `Suggest` fields show a pill with "Accept" / "Dismiss"
- `Reference` fields are read-only with a lock icon
- Clicking the provenance badge opens the concept timeline

### 10.2 Form Builder — Link Configuration

```
┌──────────────────────────────────────────────┐
│  🔗 Liên kết dữ liệu                        │
│                                              │
│  Concept: Body Weight (LOINC: 29463-7)  ✓   │
│  ─────────────────────────────────────────   │
│  Hoặc liên kết trực tiếp:                   │
│  Biểu mẫu nguồn: [Khám ban đầu ▾]          │
│  Trường nguồn:   [Cân nặng ▾]               │
│  Kiểu:           [Tự động điền ▾]            │
│  Chuyển đổi:     [                  ]        │
│                                [+ Thêm liên kết]│
└──────────────────────────────────────────────┘
```

### 10.3 Admin — Data Flow Matrix

```
┌────────────────────────────────────────────────────────────┐
│  📊 Ma trận luồng dữ liệu                                 │
│                                                            │
│                   TARGET →                                 │
│             │ Lâm sàng │ Xét nghiệm │ Kết quả │ Hành chính │
│  SOURCE  ──┼───────────┼────────────┼─────────┼────────────│
│  Lâm sàng  │     -     │  AutoFill  │ Suggest │     -      │
│  Xét nghiệm│     -     │     -      │AutoFill │     -      │
│  Kết quả   │     -     │     -      │    -    │   Copy     │
│  Hành chính│     -     │     -      │    -    │     -      │
└────────────────────────────────────────────────────────────┘
```

## 11. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Materialized snapshots vs JOIN queries** | Snapshots make pre-fill O(1) per field. Without them, we'd need `N` JOINs across potentially millions of FormFieldValues. Performance is critical for form load time. |
| **Concept-first, explicit-second** | Most IVF data is medical (vitals, lab values) with standard concept codes. Explicit field links are the escape hatch for non-medical data. |
| **Category rules over template rules** | IVF clinics have 5-20 categories but 50-200 templates. Category-level rules reduce admin overhead by 10x. |
| **CycleId scoping** | IVF data is cycle-specific. A patient's FSH from Cycle 1 may not be relevant in Cycle 3. CycleId scoping ensures data freshness. |
| **FlowType distinction** | AutoFill could silently insert wrong data. Suggest gives clinicians control. Reference prevents accidental modification. |
| **Snapshot upsert on submit** | Eventual consistency is acceptable (seconds of delay). No need for real-time event streaming for this use case. |

## 12. Migration Strategy

1. **Zero downtime**: All new tables, no changes to existing tables (except optional new columns on FormField/FormCategory)
2. **Backfill**: One-time migration to scan all existing responses and build initial snapshots
3. **Gradual rollout**: Phase 1 works with zero configuration (all concept-linked fields auto-suggest). Phases 2-5 add admin controls.
4. **Backward compatible**: Forms without concept-linked fields continue to work exactly as before

## 13. Estimated Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1: Foundation (Snapshots + Auto-fill) | 5 days | Week 1 |
| Phase 2: Data Flow Rules | 4 days | Week 2 |
| Phase 3: Explicit Field Links | 4 days | Week 3 |
| Phase 4: Concept Timeline | 3 days | Week 3-4 |
| Phase 5: Cycle Scoping + Advanced | 3 days | Week 4 |
| **Total** | **~19 dev days** | **4 weeks** |
