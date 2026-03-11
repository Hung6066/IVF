# IVF Agents — Quick Reference & Examples

> 10 specialized agents for the IVF Information System. Use `@agent-name` in chat to invoke.

---

## @software-engineer (Orchestrator)

Coordinates all agents for complex cross-cutting tasks. Analyzes the request, creates a phased execution plan, and delegates to specialized agents sequentially.

### When to use

- Tasks spanning 2+ domains (e.g., feature + security + tests + deployment)
- Complex end-to-end implementations
- Cross-cutting concerns that touch backend, frontend, security, and infrastructure

### Example prompts

```
@software-engineer Build a complete MedicalAlert feature:
entity with PatientId, AlertType, Severity, IsActive.
Need CRUD API, Angular list/form UI, backend tests,
security event logging, and a Prometheus alert for high-severity alerts.

@software-engineer Implement HIPAA-compliant audit logging for all patient data access.
Need backend SecurityEvent emission, compliance evidence collection,
frontend audit trail viewer, and tests for the handler.

@software-engineer Add a new Prescription module end-to-end:
backend entity + API, frontend form with dynamic medication rows,
unit tests for both layers, and rate limiting on the create endpoint.

@software-engineer Harden the authentication flow:
add impossible travel detection, write tests for the threat detection logic,
update compliance scoring for the new control, and add a Grafana alert panel.
```

---

## @backend-feature

Scaffolds backend features end-to-end: entity → CQRS (commands/queries/validators/handlers) → DTOs → Minimal API endpoints → EF Core config → migration.

### When to use

- Creating a new backend module (e.g., Medication, LabResult, Appointment)
- Adding new commands/queries to an existing feature
- Setting up a new entity with repository + endpoint

### Example prompts

```
@backend-feature Create a Medication entity with Name, Dosage, Unit, ActiveIngredient fields.
Need CRUD + search by name.

@backend-feature Add a GetPatientTreatmentHistoryQuery to the TreatmentCycle feature
that returns all cycles for a patient with pagination.

@backend-feature Create a WaitingRoom entity that tracks patient check-in/check-out times,
queue position, and assigned doctor. Needs real-time status updates.

@backend-feature Add a BulkUpdateStatusCommand to the Appointment feature
that marks multiple appointments as completed.

@backend-feature Create a MedicalNote entity with PatientId, DoctorId, Content (text),
NoteType (enum: Consultation, FollowUp, Procedure), and AttachmentIds (list).
Feature-gated with FeatureCodes.MedicalNotes.
```

---

## @frontend-feature

Scaffolds Angular 21 frontend features: standalone component → service → TypeScript model → route registration → HTML template with Tailwind + Vietnamese labels.

### When to use

- Creating a new UI page (list, detail, form, dashboard)
- Adding components or services to an existing feature
- Building admin panels or patient-facing views

### Example prompts

```
@frontend-feature Create a medication list page at /medications with search, pagination,
and an "Add" button that opens a modal form.

@frontend-feature Add a patient treatment timeline component that shows
all treatment cycles as a vertical timeline with status badges.

@frontend-feature Create a dashboard widget showing today's appointment count,
waiting patients, and completed consultations.

@frontend-feature Add a lab result detail view at /lab-results/:id
that displays test values in a table with normal range indicators.

@frontend-feature Create an embryo grading form component with dropdown selects
for Grade, Fragmentation, Symmetry, and a notes textarea.
```

---

## @full-stack-feature

Orchestrates `@backend-feature` + `@frontend-feature` sequentially for a complete feature from database to UI.

### When to use

- Building a brand-new module that needs both backend and frontend
- End-to-end CRUD features
- Any request that spans backend entity through Angular UI

### Example prompts

```
@full-stack-feature Create a Medication module with full CRUD.
Backend: entity with Name, Dosage, Unit, Category, IsActive.
Frontend: list page with search + modal form for create/edit.

@full-stack-feature Build an Appointment Scheduling feature.
Patients book slots with a doctor, select date/time, add notes.
Need list view with filters, calendar view, and booking form.

@full-stack-feature Create a Lab Results module.
Entity: PatientId, TestType (enum), Values (JSON), ResultDate, Status.
UI: searchable list, detail view with value table, upload form.

@full-stack-feature Build a Doctor Schedule management feature.
Doctors set weekly availability (day, start time, end time, max patients).
Admin can view/edit all schedules. Frontend calendar grid layout.

@full-stack-feature Create a Prescription feature.
DoctorId, PatientId, list of PrescriptionItems (MedicationId, Dosage, Duration, Instructions).
Frontend: form with dynamic item rows, print preview.
```

---

## @advanced-security

Implements security features: MFA (Passkeys/TOTP/SMS), threat detection, rate limiting, geo-fencing, account lockouts, IP whitelist, conditional access, incident response, Zero Trust, KeyVault.

### When to use

- Adding or modifying authentication/authorization flows
- Implementing threat detection rules
- Configuring rate limiting, geo-blocking, or IP policies
- Working with Zero Trust policies or KeyVault secrets
- Enterprise security features (incidents, impersonation, behavioral analytics)

### Example prompts

```
@advanced-security Add impossible travel detection — if a user logs in
from two countries within 1 hour, trigger a SecurityIncident and lock the account.

@advanced-security Create a new incident response rule: after 10 failed logins
from the same IP in 5 minutes, auto-block the IP and notify admin via Discord.

@advanced-security Add TOTP recovery code regeneration — endpoint to issue
new recovery codes and invalidate old ones. Emit SecurityEvent.

@advanced-security Update the conditional access policy evaluation to support
time-of-day restrictions (e.g., block access outside business hours 7AM-7PM).

@advanced-security Add a "trusted device" management page — users can view
their registered devices, revoke trust, and see last-used timestamps.

@advanced-security Implement rate limiting for the /api/auth/login endpoint:
5 attempts per minute per IP, with exponential backoff after lockout.

@advanced-security Add behavioral anomaly alerting — when a user's risk score
exceeds 80, auto-require MFA for the next 24 hours.
```

---

## @compliance-audit

Manages compliance features: HIPAA/GDPR/SOC 2/ISO 27001 frameworks, breach notifications, training tracking, data retention, evidence collection, scoring dashboards, audit trails.

### When to use

- Implementing breach notification workflows
- Managing compliance training records
- Configuring data retention policies
- Collecting or exporting audit evidence
- Building compliance dashboards or reports
- Password policy enforcement

### Example prompts

```
@compliance-audit Create a breach notification for a data export incident —
set severity to High, affected records 150, and auto-start the 72-hour DPA timer.

@compliance-audit Add a new data retention policy for AuditLog entities:
retain for 6 years (SOC 2 requirement), then archive to S3.

@compliance-audit Update the compliance scoring engine to weight
HIPAA training completion at 25% of the overall HIPAA score.

@compliance-audit Add a quarterly evidence refresh automation that runs
collect-evidence.ps1 and uploads results to MinIO.

@compliance-audit Create a compliance training assignment flow — admin selects
users/roles, assigns training type (HIPAA Annual, GDPR Awareness, Incident Response),
sets due date. Frontend: assignment form + completion tracking table.

@compliance-audit Add a control matrix export endpoint that generates
a CSV mapping each SOC 2 control (CC1-CC9) to its implementation status.

@compliance-audit Build a breach notification timeline view showing
state transitions (Created → Assessed → Contained → Notified) with timestamps.
```

---

## @infrastructure-ops

Manages infrastructure: Docker Swarm, deployment/CI/CD, monitoring (Prometheus/Grafana/Loki), auto-healing, DR/failover, PostgreSQL replication, backup/restore, Caddy, Ansible, PKI/signing infrastructure.

### When to use

- Docker Swarm service management (scale, update, rollback)
- Monitoring configuration (alerts, dashboards, log queries)
- Backup/restore operations (S3, WAL, PITR)
- Deployment pipelines and rolling updates
- Certificate management (EJBCA, SignServer, mTLS)
- Ansible playbooks and infrastructure provisioning
- Disaster recovery drills and failover

### Example prompts

```
@infrastructure-ops Add a Prometheus alert rule for API p99 latency > 2s
sustained for 5 minutes, with Discord notification.

@infrastructure-ops Update the Caddyfile to add rate limiting on /api/auth/*
endpoints — 10 req/s per IP with 429 response.

@infrastructure-ops Set up WAL archiving for the standby replica on VPS2
with 14-day retention and S3 sync every 5 minutes.

@infrastructure-ops Create a Grafana dashboard panel showing PostgreSQL
replication lag between primary and standby in real-time.

@infrastructure-ops Add a new Ansible role for Redis Sentinel setup
with automatic failover and health monitoring.

@infrastructure-ops Rotate the SignServer mTLS certificates —
generate new certs, enroll with EJBCA, update Docker secrets.

@infrastructure-ops Create a disaster recovery drill script that simulates
VPS1 failure: promotes VPS2 standby, verifies API health, tests MinIO access.

@infrastructure-ops Add a Loki alert rule for detecting repeated
"connection refused" errors from PostgreSQL within a 1-minute window.

@infrastructure-ops Update the deploy.sh script to support blue-green
deployment with health check validation before traffic switch.
```

---

## @frontend-testing

Writes and fixes Angular 21 frontend unit tests using Vitest + Angular TestBed. Covers standalone component tests (signals), service tests (HttpClient), guard tests, and interceptor tests.

### When to use

- Writing tests for Angular components, services, guards, or interceptors
- Setting up the Vitest test infrastructure (if missing)
- Fixing broken spec files
- Adding test coverage for untested frontend code

### Example prompts

```
@frontend-testing Set up Vitest for this Angular project —
install dependencies, create config, and verify with a smoke test.

@frontend-testing Write tests for PatientListComponent —
cover creation, data loading, search, pagination, and delete confirmation.

@frontend-testing Write tests for PatientService —
cover searchPatients, createPatient, and error handling.

@frontend-testing Write tests for authGuard —
test authenticated access and redirect to login.

@frontend-testing Write tests for authInterceptor —
test token injection, skip for login endpoint, and 401 refresh logic.

@frontend-testing Add tests for the QueueService —
cover getQueue, callPatient, and SignalR connection methods.
```

---

## @backend-testing

Writes and fixes backend unit tests following xUnit + Moq + FluentAssertions conventions. Covers handler tests, entity tests, service tests, and validator tests.

### When to use

- Writing tests for a new or existing feature
- Fixing broken tests
- Adding test coverage for uncovered handlers/entities/services
- Debugging test failures

### Example prompts

```
@backend-testing Write unit tests for the CreatePatientHandler —
cover success, duplicate code, and tenant limit exceeded cases.

@backend-testing Add tests for the TreatmentCycle entity —
test Create, AdvancePhase, and MarkAsDeleted methods.

@backend-testing Fix the failing VaultSecretServiceTests —
the PutSecretAsync test is getting a NullReferenceException.

@backend-testing Write tests for the ComplianceScoringEngine —
cover HIPAA scoring with full training completion and with missing training.

@backend-testing Add validation tests for CreateAppointmentValidator —
test empty PatientId, past date, overlapping time slot.

@backend-testing Increase test coverage for the SecurityEventPublisher service —
test event creation, batch processing, and error handling.
```

---

## @code-review

Reviews code changes for correctness, security, conventions, performance, and maintainability. Read-only — reports findings and recommends which agent to use for fixes.

### When to use

- Reviewing changed files before committing
- Checking PR code quality
- Verifying convention compliance after a feature is built
- Security audit of new endpoints or components

### Example prompts

```
@code-review Review the files I changed in the last commit —
check for security issues, convention violations, and missing tests.

@code-review Review src/IVF.Application/Features/Medication/ —
is the CQRS pattern correct? Any missing validation?

@code-review Check the new PatientEndpoints.cs for security issues —
authorization, input validation, and multi-tenancy compliance.

@code-review Review the Angular MedicationListComponent —
check for convention violations, missing Vietnamese labels, and signal usage.

@code-review Audit all files changed in this branch —
focus on OWASP Top 10 vulnerabilities and data leakage risks.
```

---

## Agent Selection Cheat Sheet

| I need to...                                      | Use                   |
| ------------------------------------------------- | --------------------- |
| Complex task spanning multiple domains            | `@software-engineer`  |
| Create a new backend entity + API                 | `@backend-feature`    |
| Build a new Angular page or component             | `@frontend-feature`   |
| Build a complete feature (backend + frontend)     | `@full-stack-feature` |
| Write or fix backend unit tests                   | `@backend-testing`    |
| Write or fix Angular frontend tests               | `@frontend-testing`   |
| Review code quality, security, conventions        | `@code-review`        |
| Add MFA, threat detection, or access policies     | `@advanced-security`  |
| Handle breach notifications, training, retention  | `@compliance-audit`   |
| Manage Docker, monitoring, backups, or deployment | `@infrastructure-ops` |
