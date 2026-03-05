---
description: "Scaffold a new domain entity with full-stack CRUD. Provide the entity name and properties as input."
argument-hint: "EntityName: Prop1 (type), Prop2 (type), ..."
agent: "full-stack-feature"
---

Scaffold a complete full-stack CRUD feature for the following entity definition.

## Input Format

The user provides: `EntityName: Prop1 (type), Prop2 (type), ...`

Example: `Medication: Name (string), Dosage (decimal), Unit (string), Category (string), IsActive (bool)`

## Parse Rules

- Entity name = first word before the colon
- Properties = comma-separated list after the colon
- Types in parentheses map to .NET types: `string`, `int`, `decimal`, `bool`, `DateTime`, `Guid`
- Properties without types default to `string`
- The entity is always tenant-scoped (`ITenantEntity`) unless `--no-tenant` is appended
- The entity is feature-gated if `--feature=code` is appended

## What to Generate

Delegate to the `full-stack-feature` agent with these requirements:

**Backend:**

1. Domain entity with all properties + `Create()` factory
2. DTO with `FromEntity()` mapping
3. CQRS commands: Create, Update, Delete (colocated with validators and handlers)
4. CQRS queries: GetById, Search (with pagination)
5. Repository interface + EF Core implementation
6. Minimal API endpoint (GET list, GET by id, POST, PUT, DELETE)
7. EF Core entity configuration
8. Register in Program.cs

**Frontend:**

1. TypeScript model interface + list response type
2. Service with CRUD methods
3. List component with search + pagination (signals, `@if`/`@for`, Vietnamese labels)
4. Route registration with lazy loading

## Examples

```
/new-entity Medication: Name (string), Dosage (decimal), Unit (string), Category (string), IsActive (bool)
```

```
/new-entity LabResult: Value (decimal), ReferenceRange (string), TestType (string), CycleId (Guid), Notes (string) --feature=lab
```

```
/new-entity StorageLocation: Name (string), Capacity (int), CurrentCount (int), Type (string) --no-tenant
```
