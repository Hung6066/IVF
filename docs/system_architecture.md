# IVF System - Comprehensive Architecture Documentation

**Version:** 1.0
**Date:** 2026-03-13
**Author:** Security Audit Team
**Status:** Production Ready

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Overview](#2-system-overview)
3. [Architecture Diagrams](#3-architecture-diagrams)
4. [Services & Components](#4-services--components)
5. [Network Architecture](#5-network-architecture)
6. [Authentication & Authorization](#6-authentication--authorization)
7. [Data Flow Diagrams](#7-data-flow-diagrams)
8. [Database Architecture](#8-database-architecture)
9. [Security Architecture](#9-security-architecture)
10. [Monitoring & Observability](#10-monitoring--observability)
11. [Disaster Recovery](#11-disaster-recovery)
12. [API Reference](#12-api-reference)
13. [Deployment Architecture](#13-deployment-architecture)
14. [Security Maturity Assessment](#14-security-maturity-assessment)

---

## 1. Executive Summary

### 1.1 System Description

IVF System là hệ thống quản lý phòng khám IVF (In Vitro Fertilization) đa tenant, được thiết kế theo kiến trúc Clean Architecture với các tiêu chuẩn bảo mật cấp Enterprise+.

### 1.2 Key Metrics

| Metric | Value |
|--------|-------|
| Security Maturity Score | **95/100** (Enterprise+) |
| Critical Vulnerabilities | **0** (All fixed) |
| High Vulnerabilities | **0** (All fixed) |
| API Endpoints | **64+** REST + **6** SignalR Hubs |
| Overlay Networks | **4** (Encrypted) |
| Defense Layers | **5** |
| SIEM Alert Rules | **15** |
| Prometheus Alert Rules | **31** |
| Grafana Dashboards | **4** |

### 1.3 Technology Stack

| Layer | Technology |
|-------|------------|
| Frontend | Angular 21, TypeScript 5.9, TailwindCSS v4 |
| API | ASP.NET Core 10, Minimal APIs, MediatR 12.5 |
| Database | PostgreSQL 16, Redis Alpine |
| Object Storage | MinIO (S3 Compatible) |
| PKI | EJBCA CE, SignServer CE, SoftHSM2 |
| Reverse Proxy | Caddy 2, Cloudflare WAF |
| Container | Docker Swarm, Kubernetes (Ready) |
| Monitoring | Prometheus, Grafana, Loki, OpenTelemetry |
| VPN | WireGuard |

---

## 2. System Overview

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                    EXTERNAL LAYER                                        │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   ┌───────────────┐    ┌───────────────┐    ┌───────────────┐    ┌───────────────┐     │
│   │  Cloudflare   │    │  Let's        │    │   Discord     │    │  Biometric    │     │
│   │  WAF + CDN    │    │  Encrypt      │    │  Webhooks     │    │  Devices      │     │
│   │  DDoS Protect │    │  (Auto TLS)   │    │  (Alerts)     │    │DigitalPersona │     │
│   └───────┬───────┘    └───────┬───────┘    └───────────────┘    └───────┬───────┘     │
│           │                    │                                          │             │
│           └────────────────────┴──────────────────────────────────────────┘             │
│                                           │                                              │
│                                           ▼                                              │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                           CADDY REVERSE PROXY                                    │   │
│   │                    (Port 80/443 - TLS Termination - HSTS)                       │   │
│   │         Security Headers: CSP, X-Frame-Options, Permissions-Policy              │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘
                                            │
                    ┌───────────────────────┼───────────────────────┐
                    ▼                       ▼                       ▼
            ┌───────────────┐      ┌───────────────┐      ┌───────────────┐
            │   /api/*      │      │  /grafana/*   │      │ /prometheus/* │
            │   /hubs/*     │      │ (Basic Auth)  │      │ (Basic Auth)  │
            └───────┬───────┘      └───────────────┘      └───────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                          IVF-PUBLIC NETWORK (Encrypted Overlay)                          │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                           IVF.API (ASP.NET Core 10)                              │   │
│   │                              Port 8080 × 2 Replicas                              │   │
│   │                                                                                   │   │
│   │   ┌─────────────────────────────────────────────────────────────────────────┐   │   │
│   │   │                    AUTHENTICATION MIDDLEWARE CHAIN                       │   │   │
│   │   │  Vault Token → API Key → JWT Bearer → Device Fingerprint → Security     │   │   │
│   │   └─────────────────────────────────────────────────────────────────────────┘   │   │
│   │                                                                                   │   │
│   │   ┌─────────────────────────────────────────────────────────────────────────┐   │   │
│   │   │                    MediatR PIPELINE (Zero Trust)                         │   │   │
│   │   │  Validation → FeatureGate → VaultPolicy → ZeroTrust → FieldAccess       │   │   │
│   │   └─────────────────────────────────────────────────────────────────────────┘   │   │
│   │                                                                                   │   │
│   │   ┌───────────────────────┐          ┌───────────────────────┐                  │   │
│   │   │   64+ REST Endpoints  │          │   6 SignalR Hubs      │                  │   │
│   │   └───────────────────────┘          └───────────────────────┘                  │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
│   ┌─────────────────┐                                                                   │
│   │     REDIS       │ ← Session, MFA Pending, Cache, Rate Limit                         │
│   │  (Auth Required)│                                                                   │
│   └─────────────────┘                                                                   │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘
                    │
                    │ mTLS Client Certificate
                    ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                      IVF-SIGNING NETWORK (Internal - Encrypted)                          │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   ┌─────────────────────────────┐         ┌─────────────────────────────┐               │
│   │         EJBCA CE            │         │       SignServer CE         │               │
│   │   (Certificate Authority)   │ ──────▶ │    (Document Signing)       │               │
│   │   Port: 8443 (Admin)        │         │    Port: 9443 (mTLS only)   │               │
│   └──────────────┬──────────────┘         └──────────────┬──────────────┘               │
│                  ▼                                        ▼                              │
│   ┌─────────────────────────────┐         ┌─────────────────────────────┐               │
│   │       EJBCA Database        │         │     SignServer Database     │               │
│   │       (PostgreSQL)          │         │       (PostgreSQL)          │               │
│   └─────────────────────────────┘         └─────────────────────────────┘               │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                        IVF-DATA NETWORK (Internal - Encrypted)                           │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                      PostgreSQL PRIMARY (Port 5433)                              │   │
│   │   WAL Archiving | Streaming Replication | SSL Mode=Prefer                        │   │
│   └───────────────────────────────────┬─────────────────────────────────────────────┘   │
│                                       │                                                  │
│              ┌────────────────────────┼────────────────────────┐                        │
│              ▼                        ▼                        ▼                        │
│   ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐            │
│   │  PostgreSQL Standby │  │  MinIO S3 Storage   │  │  Cloud Replica      │            │
│   │     (Hot Standby)   │  │  4 Buckets          │  │  (Disaster Recovery)│            │
│   └─────────────────────┘  └─────────────────────┘  └─────────────────────┘            │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                          IVF-MONITORING NETWORK (Internal)                               │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐                  │
│   │    Prometheus    │◀───│    Promtail      │───▶│      Loki        │                  │
│   │   31 Alert Rules │    │  (Log Shipper)   │    │   9 SIEM Rules   │                  │
│   └────────┬─────────┘    └──────────────────┘    └────────┬─────────┘                  │
│            └──────────────────────┬───────────────────────-┘                            │
│                                   ▼                                                      │
│                       ┌──────────────────────┐                                          │
│                       │       Grafana        │ → Discord Webhooks                       │
│                       │   4 Dashboards       │                                          │
│                       │   25 Alert Rules     │                                          │
│                       └──────────────────────┘                                          │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Clean Architecture Layers

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                          PRESENTATION LAYER                                          │
│                                                                                       │
│   ┌─────────────────────────────────────────────────────────────────────────────┐   │
│   │                    Angular 21 SPA (ivf-client)                               │   │
│   │   ├─ Auth (login, MFA, passkeys, SSO)                                        │   │
│   │   ├─ Features (patients, cycles, forms, queue, billing)                      │   │
│   │   ├─ Core (services, guards, interceptors, models)                           │   │
│   │   └─ Shared (reusable components)                                            │   │
│   └─────────────────────────────────────────────────────────────────────────────┘   │
│                                       │                                              │
│                              REST + SignalR                                          │
│                                       ▼                                              │
│   ┌─────────────────────────────────────────────────────────────────────────────┐   │
│   │                    IVF.API (Minimal APIs)                                    │   │
│   │   ├─ Auth Middleware (JWT, API Key, Vault Token)                             │   │
│   │   ├─ Endpoints (64 feature controllers)                                      │   │
│   │   ├─ SignalR Hubs (6 real-time channels)                                     │   │
│   │   └─ Middleware (logging, CORS, rate limiting)                               │   │
│   └─────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                       │
└──────────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                   DI Container
                                        ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                          APPLICATION LAYER                                           │
│                                                                                       │
│   ┌─────────────────────────────────────────────────────────────────────────────┐   │
│   │                    IVF.Application (Clean Architecture)                      │   │
│   │   ├─ CQRS Handlers (Commands, Queries via MediatR)                           │   │
│   │   ├─ Validators (FluentValidation)                                           │   │
│   │   ├─ Service Interfaces (IDigitalSigning, IBiometric)                        │   │
│   │   └─ Pipeline Behaviors (logging, validation, zero trust)                    │   │
│   └─────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                       │
└──────────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                          INFRASTRUCTURE LAYER                                        │
│                                                                                       │
│   ┌─────────────────────────────────────────────────────────────────────────────┐   │
│   │                    IVF.Infrastructure                                        │   │
│   │   ├─ Database (EF Core, PostgreSQL)                                          │   │
│   │   ├─ Repositories & Unit of Work                                             │   │
│   │   ├─ Services (MinIO, Redis, SignServer, EJBCA)                              │   │
│   │   ├─ Seeding (initial data population)                                       │   │
│   │   └─ External Integrations (Cloudflare, Discord)                             │   │
│   └─────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                       │
└──────────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                          DOMAIN LAYER                                                │
│                                                                                       │
│   ┌─────────────────────────────────────────────────────────────────────────────┐   │
│   │                    IVF.Domain (Pure Entities)                                │   │
│   │   ├─ Aggregates (Patient, Couple, Cycle, Embryo)                             │   │
│   │   ├─ Value Objects (Gender, PatientType)                                     │   │
│   │   ├─ Enums (TreatmentMethod, PhaseStatus)                                    │   │
│   │   └─ No external dependencies                                                │   │
│   └─────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                       │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Architecture Diagrams

### 3.1 Component Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              IVF SYSTEM COMPONENTS                                       │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                   FRONTEND                                               │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                         ivf-client (Angular 21)                                  │   │
│   │                                                                                   │   │
│   │   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │   │
│   │   │    Auth     │  │  Patients   │  │   Cycles    │  │    Queue    │            │   │
│   │   │   Module    │  │   Module    │  │   Module    │  │   Module    │            │   │
│   │   └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘            │   │
│   │                                                                                   │   │
│   │   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │   │
│   │   │   Forms     │  │   Billing   │  │   Reports   │  │   Admin     │            │   │
│   │   │   Module    │  │   Module    │  │   Module    │  │   Module    │            │   │
│   │   └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘            │   │
│   │                                                                                   │   │
│   │   ┌──────────────────────────────────────────────────────────────────────────┐  │   │
│   │   │                         Core Services                                     │  │   │
│   │   │  AuthService | HttpInterceptors | Guards | SignalRService | StateService │  │   │
│   │   └──────────────────────────────────────────────────────────────────────────┘  │   │
│   │                                                                                   │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘
                                            │
                                     HTTP/WebSocket
                                            ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                   BACKEND                                                │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                              IVF.API                                             │   │
│   │                                                                                   │   │
│   │   ┌──────────────────────────────────────────────────────────────────────────┐  │   │
│   │   │                         Endpoints                                         │  │   │
│   │   │                                                                            │  │   │
│   │   │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ │  │   │
│   │   │  │  Auth   │ │Patients │ │ Cycles  │ │ Embryos │ │  Forms  │ │  Queue  │ │  │   │
│   │   │  └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘ │  │   │
│   │   │                                                                            │  │   │
│   │   │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ │  │   │
│   │   │  │Billing  │ │ Digital │ │Compliance│ │ Backup  │ │KeyVault │ │  Infra  │ │  │   │
│   │   │  │         │ │ Signing │ │          │ │         │ │         │ │         │ │  │   │
│   │   │  └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘ │  │   │
│   │   │                                                                            │  │   │
│   │   └──────────────────────────────────────────────────────────────────────────┘  │   │
│   │                                                                                   │   │
│   │   ┌──────────────────────────────────────────────────────────────────────────┐  │   │
│   │   │                         SignalR Hubs                                      │  │   │
│   │   │  QueueHub | NotificationHub | FingerprintHub | BackupHub | EvidenceHub   │  │   │
│   │   └──────────────────────────────────────────────────────────────────────────┘  │   │
│   │                                                                                   │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                           IVF.Application                                        │   │
│   │                                                                                   │   │
│   │   ┌─────────────────────────────────────────────────────────────────────────┐   │   │
│   │   │                      MediatR Pipeline Behaviors                          │   │   │
│   │   │  ValidationBehavior → FeatureGateBehavior → VaultPolicyBehavior          │   │   │
│   │   │  → ZeroTrustBehavior → FieldAccessBehavior → Handler                     │   │   │
│   │   └─────────────────────────────────────────────────────────────────────────┘   │   │
│   │                                                                                   │   │
│   │   ┌────────────────┐  ┌────────────────┐  ┌────────────────┐                    │   │
│   │   │    Commands    │  │    Queries     │  │   Validators   │                    │   │
│   │   │  CreatePatient │  │  GetPatients   │  │ FluentValidation│                   │   │
│   │   │  UpdateCycle   │  │  GetCycleById  │  │                │                    │   │
│   │   └────────────────┘  └────────────────┘  └────────────────┘                    │   │
│   │                                                                                   │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                          IVF.Infrastructure                                      │   │
│   │                                                                                   │   │
│   │   ┌────────────────────────────────────────────────────────────────────────┐    │   │
│   │   │                          Services                                       │    │   │
│   │   │  DigitalSigningService | BiometricService | FormPdfService              │    │   │
│   │   │  ReportPdfService | DataBackupService | SystemRestoreService            │    │   │
│   │   │  SwarmAutoHealingService | CertificateExpiryMonitorService              │    │   │
│   │   │  CloudReplicationService | WalBackupService | CloudflareWafService      │    │   │
│   │   │  ComplianceScanSchedulerService | CtLogMonitorService                   │    │   │
│   │   │  VaultLeaseMaintenanceService | SecretRotationService                   │    │   │
│   │   └────────────────────────────────────────────────────────────────────────┘    │   │
│   │                                                                                   │   │
│   │   ┌────────────────────────────────────────────────────────────────────────┐    │   │
│   │   │                       Data Access                                       │    │   │
│   │   │  IvfDbContext (EF Core) | Repositories | Unit of Work                   │    │   │
│   │   └────────────────────────────────────────────────────────────────────────┘    │   │
│   │                                                                                   │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                              IVF.Domain                                          │   │
│   │                                                                                   │   │
│   │   ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐         │   │
│   │   │  Patient  │ │  Couple   │ │   Cycle   │ │  Embryo   │ │Ultrasound │         │   │
│   │   └───────────┘ └───────────┘ └───────────┘ └───────────┘ └───────────┘         │   │
│   │   ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐         │   │
│   │   │   User    │ │   Form    │ │  Document │ │   Queue   │ │Appointment│         │   │
│   │   └───────────┘ └───────────┘ └───────────┘ └───────────┘ └───────────┘         │   │
│   │                                                                                   │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Deployment Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              PRODUCTION DEPLOYMENT                                       │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                               CLOUDFLARE EDGE                                            │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │  WAF (Managed + OWASP + Custom) | CDN | DDoS Protection | Rate Limiting         │   │
│   │  Edge Rate Limits: login 5/min | auth 10/min | API 200/min                       │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
└───────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                                            ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                         PRIMARY DATA CENTER (Docker Swarm)                               │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                              SWARM MANAGER NODE                                  │   │
│   │                                                                                   │   │
│   │   ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐                 │   │
│   │   │     Caddy       │  │   IVF.API (1)   │  │     Redis       │                 │   │
│   │   │   Port 80/443   │  │   Port 8080     │  │   Port 6379     │                 │   │
│   │   └─────────────────┘  └─────────────────┘  └─────────────────┘                 │   │
│   │                                                                                   │   │
│   │   ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐                 │   │
│   │   │   PostgreSQL    │  │     EJBCA       │  │   SignServer    │                 │   │
│   │   │   Port 5433     │  │   Port 8443     │  │   Port 9443     │                 │   │
│   │   └─────────────────┘  └─────────────────┘  └─────────────────┘                 │   │
│   │                                                                                   │   │
│   │   ┌─────────────────┐  ┌─────────────────┐                                      │   │
│   │   │     MinIO       │  │  Socket Proxy   │                                      │   │
│   │   │  S3 Storage     │  │   (Read-only)   │                                      │   │
│   │   └─────────────────┘  └─────────────────┘                                      │   │
│   │                                                                                   │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│   │                              SWARM WORKER NODE                                   │   │
│   │                                                                                   │   │
│   │   ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐                 │   │
│   │   │   IVF.API (2)   │  │  DB Standby     │  │   Prometheus    │                 │   │
│   │   │   Port 8080     │  │   Port 5434     │  │   Port 9090     │                 │   │
│   │   └─────────────────┘  └─────────────────┘  └─────────────────┘                 │   │
│   │                                                                                   │   │
│   │   ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐                 │   │
│   │   │     Grafana     │  │      Loki       │  │    Promtail     │                 │   │
│   │   │   Port 3000     │  │   Port 3100     │  │   Log Shipper   │                 │   │
│   │   └─────────────────┘  └─────────────────┘  └─────────────────┘                 │   │
│   │                                                                                   │   │
│   └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                          │
└───────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                              Streaming Replication (SSL)
                                            │
                                            ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                         CLOUD DISASTER RECOVERY SITE                                     │
│                                                                                          │
│   ┌─────────────────────────────┐         ┌─────────────────────────────┐               │
│   │       db-replica            │         │      minio-replica          │               │
│   │   (PostgreSQL Hot Standby)  │         │    (S3 Sync Target)         │               │
│   │   Failover Candidate        │         │    Object Replication       │               │
│   └─────────────────────────────┘         └─────────────────────────────┘               │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Services & Components

### 4.1 Backend Services

| Service | Type | Port | Technology | Description |
|---------|------|------|------------|-------------|
| **IVF.API** | REST + SignalR | 8080 | ASP.NET Core 10 | Main application server |
| **IVF.Gateway** | Reverse Proxy | - | YARP 2.3.0 | API Gateway (optional) |
| **IVF.Application** | Library | - | MediatR 12.5 | CQRS handlers & business logic |
| **IVF.Infrastructure** | Library | - | EF Core 10 | Data access & external services |
| **IVF.Domain** | Library | - | Pure POCO | Domain entities & value objects |
| **IVF.FingerprintClient** | Desktop | - | WinForms .NET 9 | Biometric capture client |

### 4.2 Infrastructure Services

| Service | Image | Port(s) | Network(s) | Purpose |
|---------|-------|---------|------------|---------|
| **PostgreSQL** | postgres:16-alpine | 5433 | ivf-data | Primary database |
| **PostgreSQL Standby** | postgres:16-alpine | 5434 | ivf-data | Hot standby replica |
| **Redis** | redis:alpine | 6379 | ivf-data, ivf-public | Cache & session store |
| **MinIO** | minio/minio | 9000, 9001 | ivf-data, ivf-public | S3-compatible storage |
| **Caddy** | caddy:2-alpine | 80, 443 | ivf-public | Reverse proxy & TLS |
| **EJBCA** | keyfactor/ejbca-ce | 8443 | ivf-signing | Certificate Authority |
| **SignServer** | keyfactor/signserver-ce | 9443 | ivf-signing | Document signing |
| **Docker Socket Proxy** | tecnativa/docker-socket-proxy | 2375 | ivf-data | Secure Docker API access |

### 4.3 Monitoring Services

| Service | Image | Port | Network | Purpose |
|---------|-------|------|---------|---------|
| **Prometheus** | prom/prometheus | 9090 | ivf-monitoring | Metrics collection |
| **Grafana** | grafana/grafana | 3000 | ivf-monitoring | Visualization |
| **Loki** | grafana/loki | 3100 | ivf-monitoring | Log aggregation |
| **Promtail** | grafana/promtail | - | ivf-monitoring | Log shipping |
| **postgres-exporter** | prometheuscommunity/postgres-exporter | 9187 | ivf-monitoring | PostgreSQL metrics |
| **redis-exporter** | oliver006/redis_exporter | 9121 | ivf-monitoring | Redis metrics |

### 4.4 Application Services (Internal)

| Service | Class | Interval | Purpose |
|---------|-------|----------|---------|
| **SwarmAutoHealingService** | BackgroundService | 30s | Monitor & restart unhealthy containers |
| **CertificateExpiryMonitorService** | BackgroundService | 1h | PKI certificate expiration tracking |
| **ComplianceScanSchedulerService** | BackgroundService | 6h | Automated compliance scanning |
| **CtLogMonitorService** | BackgroundService | 12h | Certificate Transparency monitoring |
| **VaultLeaseMaintenanceService** | BackgroundService | 5min | Secret rotation execution |
| **CloudReplicationService** | On-demand | - | Disaster recovery replication |
| **WalBackupService** | On-demand | - | PostgreSQL WAL archival to S3 |

---

## 5. Network Architecture

### 5.1 Overlay Networks

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              DOCKER OVERLAY NETWORKS                                     │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│  ivf-public (driver: overlay, encrypted: true)                                           │
│  Purpose: External-facing services                                                       │
│                                                                                          │
│  Services:                                                                               │
│  ├─ api (port 5000→8080)                                                                │
│  ├─ caddy (ports 80, 443)                                                               │
│  ├─ redis (for session caching)                                                         │
│  └─ minio (S3 API access)                                                               │
│                                                                                          │
│  External Access: YES (via Caddy reverse proxy)                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│  ivf-signing (driver: overlay, encrypted: true, internal: true)                          │
│  Purpose: PKI and document signing services                                              │
│                                                                                          │
│  Services:                                                                               │
│  ├─ api (mTLS client cert validation)                                                   │
│  ├─ signserver (document signing)                                                       │
│  ├─ ejbca (certificate issuance)                                                        │
│  ├─ ejbca-db                                                                            │
│  └─ signserver-db                                                                       │
│                                                                                          │
│  External Access: NO (internal only, mTLS required)                                      │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│  ivf-data (driver: overlay, encrypted: true, internal: true)                             │
│  Purpose: Data storage services                                                          │
│                                                                                          │
│  Services:                                                                               │
│  ├─ db (PostgreSQL primary)                                                             │
│  ├─ db-standby (read-only replica)                                                      │
│  ├─ redis (persistent cache)                                                            │
│  ├─ minio (object storage)                                                              │
│  ├─ postgres-exporter                                                                   │
│  └─ redis-exporter                                                                      │
│                                                                                          │
│  External Access: NO (internal only)                                                     │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│  ivf-monitoring (driver: overlay, encrypted: true)                                       │
│  Purpose: Observability stack                                                            │
│                                                                                          │
│  Services:                                                                               │
│  ├─ prometheus (port 9090, 127.0.0.1 only)                                              │
│  ├─ grafana (port 3000, 127.0.0.1 only)                                                 │
│  ├─ loki (port 3100, 127.0.0.1 only)                                                    │
│  └─ promtail (log shipping)                                                             │
│                                                                                          │
│  External Access: Via Caddy with Basic Auth                                              │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Network Security Layers

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              5 LAYERS OF DEFENSE                                         │
└─────────────────────────────────────────────────────────────────────────────────────────┘

        INTERNET
            │
            ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 1: Cloudflare WAF + CDN                                                         │
│                                                                                         │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐  │
│  │  • Managed Ruleset (Cloudflare Managed Rules)                                    │  │
│  │  • OWASP Core Ruleset (SQL Injection, XSS, LFI, RFI)                            │  │
│  │  • Custom Rules:                                                                 │  │
│  │    - Scanner blocking (Nikto, SQLMap, Nmap, WPScan)                             │  │
│  │    - Path traversal detection                                                    │  │
│  │    - Suspicious login challenge (Tor, VPN, datacenter IPs)                      │  │
│  │  • Edge Rate Limiting:                                                           │  │
│  │    - /api/auth/login: 5 requests/minute                                         │  │
│  │    - /api/auth/*: 10 requests/minute                                            │  │
│  │    - /api/*: 200 requests/minute                                                │  │
│  │  • DDoS Protection (Layer 3/4/7)                                                │  │
│  └─────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                         │
└───────────────────────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 2: UFW Firewall                                                                 │
│                                                                                         │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐  │
│  │  ALLOW:                                                                          │  │
│  │  • 22/tcp (SSH - key-only, 2FA required)                                        │  │
│  │  • 80/tcp (HTTP - redirects to HTTPS)                                           │  │
│  │  • 443/tcp (HTTPS - Caddy TLS termination)                                      │  │
│  │  • 51820/udp (WireGuard VPN)                                                    │  │
│  │                                                                                  │  │
│  │  BLOCK on eth0 (public interface):                                              │  │
│  │  • 8443 (EJBCA Admin)                                                           │  │
│  │  • 9443 (SignServer Admin)                                                      │  │
│  │  • 5433 (PostgreSQL)                                                            │  │
│  │  • 6379 (Redis)                                                                 │  │
│  │  • 9001 (MinIO Console)                                                         │  │
│  │                                                                                  │  │
│  │  ALLOW on wg0 (VPN interface - 10.200.0.0/24):                                  │  │
│  │  • All admin ports accessible                                                   │  │
│  └─────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                         │
└───────────────────────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 3: Fail2ban + SSH Hardening                                                     │
│                                                                                         │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐  │
│  │  Fail2ban Jails:                                                                 │  │
│  │  • sshd: 5 attempts → 1 hour ban                                                │  │
│  │  • sshd-ddos: Aggressive SSH protection                                         │  │
│  │  • recidive: 3 bans → 1 week ban                                                │  │
│  │                                                                                  │  │
│  │  SSH Hardening:                                                                  │  │
│  │  • PermitRootLogin no ✓                                                         │  │
│  │  • PasswordAuthentication no ✓                                                  │  │
│  │  • PubkeyAuthentication yes (Ed25519 keys only) ✓                               │  │
│  │  • AuthenticationMethods publickey,keyboard-interactive ✓                       │  │
│  │  • 2FA via TOTP (Google Authenticator) ✓                                        │  │
│  └─────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                         │
└───────────────────────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 4: WireGuard VPN                                                                │
│                                                                                         │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐  │
│  │  Configuration:                                                                  │  │
│  │  • Port: 51820/UDP                                                              │  │
│  │  • Subnet: 10.200.0.0/24                                                        │  │
│  │  • Server: 10.200.0.1                                                           │  │
│  │  • Clients: 10.200.0.2 - 10.200.0.254                                           │  │
│  │                                                                                  │  │
│  │  Security:                                                                       │  │
│  │  • Preshared key per client (quantum-resistant)                                 │  │
│  │  • Split tunnel (only admin traffic via VPN)                                    │  │
│  │  • Client certificates for identification                                       │  │
│  │                                                                                  │  │
│  │  Required for:                                                                   │  │
│  │  • PostgreSQL direct access                                                     │  │
│  │  • Redis direct access                                                          │  │
│  │  • MinIO Console access                                                         │  │
│  │  • EJBCA/SignServer Admin                                                       │  │
│  │  • Grafana/Prometheus direct access                                             │  │
│  └─────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                         │
└───────────────────────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 5: Application Security                                                         │
│                                                                                         │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐  │
│  │  TLS & Headers:                                                                  │  │
│  │  • Caddy auto-TLS (Let's Encrypt)                                               │  │
│  │  • TLS 1.3 only                                                                 │  │
│  │  • HSTS preload (2 years)                                                       │  │
│  │  • CSP strict (default-src 'none')                                              │  │
│  │  • X-Frame-Options DENY                                                         │  │
│  │  • Permissions-Policy (camera, mic, geo blocked)                                │  │
│  │  • Trusted Types enabled                                                        │  │
│  │                                                                                  │  │
│  │  Authentication:                                                                 │  │
│  │  • JWT RS256 3072-bit + httpOnly cookie                                         │  │
│  │  • Device fingerprint binding (SHA-256)                                         │  │
│  │  • MFA (TOTP + Passkey/WebAuthn)                                               │  │
│  │  • Session validation on every request                                          │  │
│  │                                                                                  │  │
│  │  Zero Trust Pipeline:                                                            │  │
│  │  • MediatR behaviors validate every request                                     │  │
│  │  • Feature gating per tenant                                                    │  │
│  │  • Field-level access control                                                   │  │
│  │  • Vault policy enforcement                                                     │  │
│  │                                                                                  │  │
│  │  Internal Security:                                                              │  │
│  │  • Encrypted overlay networks                                                   │  │
│  │  • mTLS for SignServer/EJBCA                                                    │  │
│  │  • Redis AUTH required                                                          │  │
│  │  • Docker socket proxy (read-only, restricted API)                              │  │
│  └─────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                         │
└───────────────────────────────────────────────────────────────────────────────────────┘
```

### 5.3 Port Mapping

| Port | Service | Protocol | Binding | Access |
|------|---------|----------|---------|--------|
| 22 | SSH | TCP | 0.0.0.0 | Public (2FA required) |
| 80 | Caddy HTTP | TCP | 0.0.0.0 | Public (redirect to 443) |
| 443 | Caddy HTTPS | TCP | 0.0.0.0 | Public |
| 51820 | WireGuard | UDP | 0.0.0.0 | Public |
| 5433 | PostgreSQL | TCP | 127.0.0.1 | VPN only |
| 5434 | PostgreSQL Standby | TCP | 127.0.0.1 | VPN only |
| 6379 | Redis | TCP | 127.0.0.1 | VPN only |
| 8443 | EJBCA | TCP | 127.0.0.1 | VPN only |
| 9443 | SignServer | TCP | 127.0.0.1 | VPN only |
| 9000 | MinIO API | TCP | Internal | Docker network |
| 9001 | MinIO Console | TCP | 127.0.0.1 | VPN only |
| 9090 | Prometheus | TCP | 127.0.0.1 | VPN only |
| 3000 | Grafana | TCP | 127.0.0.1 | VPN only |
| 3100 | Loki | TCP | 127.0.0.1 | VPN only |

---

## 6. Authentication & Authorization

### 6.1 Authentication Flow

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              AUTHENTICATION FLOW                                         │
└─────────────────────────────────────────────────────────────────────────────────────────┘

                        ┌──────────────────────┐
                        │    Angular Client    │
                        │   (ivf-client)       │
                        └──────────┬───────────┘
                                   │
            ┌──────────────────────┼──────────────────────┐
            ▼                      ▼                      ▼
    ┌───────────────┐     ┌───────────────┐      ┌───────────────┐
    │   Username/   │     │    OIDC/SSO   │      │   Passkey     │
    │   Password    │     │ Google/Entra  │      │   WebAuthn    │
    └───────┬───────┘     └───────┬───────┘      └───────┬───────┘
            │                     │                      │
            └──────────────┬──────┴──────────────────────┘
                           ▼
                  ┌────────────────┐
                  │  POST /api/auth│
                  │    /login      │
                  └────────┬───────┘
                           │
            ┌──────────────┴──────────────┐
            ▼                             ▼
    ┌───────────────┐            ┌───────────────┐
    │  MFA Enabled? │───Yes────▶ │  TOTP/Passkey │
    │               │            │  Verification │
    └───────┬───────┘            └───────┬───────┘
            │ No                         │
            └────────────┬───────────────┘
                         ▼
            ┌─────────────────────────┐
            │   Generate JWT Token    │
            │   (RS256 3072-bit)      │
            │                         │
            │  + SHA-256 Refresh Token│
            │  + Device Fingerprint   │
            │  + Session Claims       │
            └─────────────┬───────────┘
                          │
            ┌─────────────┴─────────────┐
            ▼                           ▼
    ┌───────────────────┐    ┌───────────────────┐
    │   httpOnly Cookie │    │  Authorization    │
    │  __Host-ivf-token │    │  Bearer Token     │
    │   (Dual Mode)     │    │  (Header)         │
    └───────────────────┘    └───────────────────┘
```

### 6.2 Authentication Methods

| Method | Purpose | Storage | Security Features |
|--------|---------|---------|-------------------|
| **JWT Bearer** | User sessions | Memory + Redis | RS256 3072-bit, 60min expiry, token binding |
| **httpOnly Cookie** | XSS-immune auth | Browser cookie | `__Host-` prefix, Secure, SameSite=Strict |
| **API Key** | Service integration | Database (BCrypt) | Constant-time comparison, header-only |
| **Vault Token** | Secrets retrieval | Vault/KMS | Short-lived, scoped access |
| **TOTP** | 2FA | User device | RFC 6238, 30-second window |
| **Passkey/WebAuthn** | Passwordless | User device | FIDO2, hardware-backed |
| **OIDC/SSO** | Enterprise login | External IdP | Authorization Code + PKCE |
| **Biometric** | Patient ID | Server-side | DigitalPersona SDK |

### 6.3 JWT Token Structure

```json
{
  "header": {
    "alg": "RS256",
    "typ": "JWT",
    "kid": "ivf-2024-001"
  },
  "payload": {
    "sub": "user-uuid",
    "iss": "IVF_System",
    "aud": "IVF_Users",
    "iat": 1710345600,
    "exp": 1710349200,
    "jti": "unique-token-id",
    "role": ["Doctor", "Admin"],
    "tenant_id": "clinic-uuid",
    "session_id": "session-uuid",
    "device_fingerprint": "sha256-hash",
    "permissions": ["patient:read", "cycle:write"],
    "features": ["digital-signing", "biometric"]
  }
}
```

### 6.4 Authorization Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                           REQUEST AUTHORIZATION PIPELINE                                 │
└─────────────────────────────────────────────────────────────────────────────────────────┘

  HTTP Request
       │
       ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                              MIDDLEWARE CHAIN                                             │
│                                                                                           │
│   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────────────────────┐ │
│   │ Rate Limit  │ → │ CORS        │ → │ Auth        │ → │ SecurityEnforcement         │ │
│   │ (IP-based)  │   │ Validation  │   │ Middleware  │   │ (Device Fingerprint)        │ │
│   └─────────────┘   └─────────────┘   └─────────────┘   └─────────────────────────────┘ │
│                                             │                                            │
│                          ┌──────────────────┼──────────────────┐                        │
│                          ▼                  ▼                  ▼                        │
│                    ┌──────────┐       ┌──────────┐       ┌──────────┐                  │
│                    │ Vault    │       │ API Key  │       │   JWT    │                  │
│                    │ Token    │       │ Header   │       │  Bearer  │                  │
│                    │Middleware│       │Middleware│       │Middleware│                  │
│                    └──────────┘       └──────────┘       └──────────┘                  │
│                                                                                          │
└──────────────────────────────────────────────────────────────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                              MediatR PIPELINE                                             │
│                                                                                           │
│   ┌────────────────┐                                                                     │
│   │ 1. Validation  │  FluentValidation rules                                             │
│   │    Behavior    │  - Required fields                                                  │
│   │                │  - Data format validation                                           │
│   └───────┬────────┘                                                                     │
│           ▼                                                                              │
│   ┌────────────────┐                                                                     │
│   │ 2. FeatureGate │  Tenant feature flags                                               │
│   │    Behavior    │  - Check feature enabled for tenant                                 │
│   │                │  - License validation                                               │
│   └───────┬────────┘                                                                     │
│           ▼                                                                              │
│   ┌────────────────┐                                                                     │
│   │ 3. VaultPolicy │  Secret access policies                                             │
│   │    Behavior    │  - Check vault permissions                                          │
│   │                │  - Decrypt sensitive fields                                         │
│   └───────┬────────┘                                                                     │
│           ▼                                                                              │
│   ┌────────────────┐                                                                     │
│   │ 4. ZeroTrust   │  Per-request authorization                                          │
│   │    Behavior    │  - Re-validate session                                              │
│   │                │  - Check IP/device binding                                          │
│   │                │  - Verify MFA if required                                           │
│   └───────┬────────┘                                                                     │
│           ▼                                                                              │
│   ┌────────────────┐                                                                     │
│   │ 5. FieldAccess │  Field-level access control                                         │
│   │    Behavior    │  - Filter response fields by role                                   │
│   │                │  - Redact sensitive data                                            │
│   └───────┬────────┘                                                                     │
│           ▼                                                                              │
│   ┌────────────────┐                                                                     │
│   │ 6. Handler     │  Execute business logic                                             │
│   │                │  - Process request                                                  │
│   │                │  - Return response                                                  │
│   └────────────────┘                                                                     │
│                                                                                           │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

### 6.5 Role-Based Access Control (RBAC)

| Role | Description | Permissions |
|------|-------------|-------------|
| **Admin** | System administrator | Full access to all features |
| **Doctor** | Medical doctor | Patient care, prescriptions, cycles |
| **Nurse** | Nursing staff | Patient data, queue, appointments |
| **LabTech** | Laboratory technician | Lab results, semen analysis |
| **Embryologist** | Embryology specialist | Embryo data, culture records |
| **Receptionist** | Front desk staff | Registration, queue, billing |
| **Cashier** | Billing staff | Payments, invoices |
| **Pharmacist** | Pharmacy staff | Medications, prescriptions |

---

## 7. Data Flow Diagrams

### 7.1 Patient Registration Flow

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                         PATIENT REGISTRATION FLOW                                        │
└─────────────────────────────────────────────────────────────────────────────────────────┘

  ┌──────────────────┐
  │  Angular Client  │
  │  (Patient Form)  │
  └────────┬─────────┘
           │ POST /api/patients
           │ Headers: Authorization, X-Device-Fingerprint
           │ Body: { name, dob, gender, contact, ... }
           ▼
  ┌──────────────────┐
  │     Caddy        │ ──▶ TLS Termination
  │  Reverse Proxy   │     Security Headers (CSP, HSTS)
  │                  │     Request Logging
  └────────┬─────────┘
           │
           ▼
  ┌──────────────────┐
  │     IVF.API      │
  │  PatientEndpoints│ ──▶ Route: POST /api/patients
  │                  │     Auth: [Authorize(Roles = "Admin,Doctor,Nurse")]
  └────────┬─────────┘
           │
           ▼
  ┌──────────────────┐
  │    MediatR       │
  │CreatePatientCmd  │ ──▶ Command dispatched to pipeline
  └────────┬─────────┘
           │
           ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────┐
  │                           PIPELINE BEHAVIORS                                          │
  │                                                                                        │
  │  1. ValidationBehavior                                                                │
  │     ├─ Required: Name, DOB, Gender                                                    │
  │     ├─ Format: Phone (regex), Email (regex)                                           │
  │     └─ Business: DOB not in future                                                    │
  │                                                                                        │
  │  2. FeatureGateBehavior                                                               │
  │     └─ Check: "patient-registration" feature enabled for tenant                       │
  │                                                                                        │
  │  3. ZeroTrustBehavior                                                                 │
  │     ├─ Verify: Session still valid                                                    │
  │     ├─ Check: Device fingerprint matches                                              │
  │     └─ Validate: User has permission "patient:create"                                 │
  │                                                                                        │
  └──────────────────────────────────────────────────────────────────────────────────────┘
           │
           ▼
  ┌──────────────────┐
  │CreatePatientHandler│
  │                  │
  │  Business Logic: │
  │  - Generate UUID │
  │  - Assign tenant │
  │  - Set created_at│
  └────────┬─────────┘
           │
           ▼
  ┌────────────────────────────────────────────────────────────────────────────────┐
  │                              DATA PERSISTENCE                                   │
  │                                                                                  │
  │  ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐           │
  │  │   PostgreSQL    │     │     MinIO       │     │   Audit Log     │           │
  │  │   (patients)    │     │  (documents)    │     │  (partitioned)  │           │
  │  │                 │     │                 │     │                 │           │
  │  │ INSERT INTO     │     │ PUT Object      │     │ INSERT INTO     │           │
  │  │ patients (...)  │     │ tenants/{id}/   │     │ audit_logs_     │           │
  │  │ VALUES (...)    │     │ photos/{uuid}   │     │ 2026_03 (...)   │           │
  │  └─────────────────┘     └─────────────────┘     └─────────────────┘           │
  │                                                                                  │
  └────────────────────────────────────────────────────────────────────────────────┘
           │
           ▼
  ┌──────────────────┐
  │  SignalR Hub     │
  │  Notification    │ ──▶ Broadcast: "PatientCreated" event
  │  (Real-time)     │     To: Connected users in tenant group
  └──────────────────┘
           │
           ▼
  ┌──────────────────┐
  │   HTTP Response  │
  │   201 Created    │ ──▶ Body: { id, name, mrn, ... }
  │                  │     Headers: Location: /api/patients/{id}
  └──────────────────┘
```

### 7.2 Document Signing Flow

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                         DOCUMENT SIGNING FLOW (PKI)                                      │
└─────────────────────────────────────────────────────────────────────────────────────────┘

  ┌──────────────────┐
  │  Angular Client  │
  │  (Sign Request)  │ ──▶ User clicks "Sign Document"
  └────────┬─────────┘
           │ POST /api/digital-signing/sign-document
           │ Body: { documentId, signerId, reason }
           ▼
  ┌──────────────────┐
  │     IVF.API      │
  │ DigitalSigning   │
  │   Endpoints      │
  └────────┬─────────┘
           │
           ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────┐
  │                    SignServerDigitalSigningService                                    │
  │                                                                                        │
  │  Step 1: Load Document                                                                │
  │  ┌─────────────────────────────────────────────────────────────────────────────────┐ │
  │  │  MinIO.GetObjectAsync("ivf-documents", "tenants/{id}/docs/{docId}.pdf")         │ │
  │  └─────────────────────────────────────────────────────────────────────────────────┘ │
  │                                                                                        │
  │  Step 2: Prepare Signing Request                                                      │
  │  ┌─────────────────────────────────────────────────────────────────────────────────┐ │
  │  │  {                                                                               │ │
  │  │    "workerName": "PDFSigner",                                                    │ │
  │  │    "data": "<base64-encoded-pdf>",                                               │ │
  │  │    "encoding": "BASE64",                                                         │ │
  │  │    "metaData": {                                                                 │ │
  │  │      "REASON": "Medical Report Approval",                                        │ │
  │  │      "LOCATION": "IVF Clinic",                                                   │ │
  │  │      "SIGNER_NAME": "Dr. John Smith"                                            │ │
  │  │    }                                                                             │ │
  │  │  }                                                                               │ │
  │  └─────────────────────────────────────────────────────────────────────────────────┘ │
  │                                                                                        │
  └──────────────────────────────────────────────────────────────────────────────────────┘
           │
           │ mTLS (Client Certificate: api-client.p12)
           │ Network: ivf-signing (internal)
           ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────┐
  │                          SignServer CE (Port 9443)                                    │
  │                                                                                        │
  │  ┌─────────────────────────────────────────────────────────────────────────────────┐ │
  │  │                         PDFSigner Worker                                         │ │
  │  │                                                                                   │ │
  │  │  1. Parse PDF document                                                           │ │
  │  │  2. Calculate document hash (SHA-256)                                            │ │
  │  │  3. Request certificate from EJBCA (or use cached)                               │ │
  │  │  4. Access signing key via PKCS#11                                               │ │
  │  │  5. Generate PKCS#7 signature                                                    │ │
  │  │  6. Embed signature in PDF                                                       │ │
  │  │  7. Request timestamp (optional TSA)                                             │ │
  │  └─────────────────────────────────────────────────────────────────────────────────┘ │
  │                                      │                                                │
  │                                      ▼                                                │
  │  ┌─────────────────────────────────────────────────────────────────────────────────┐ │
  │  │                         SoftHSM2 (PKCS#11)                                       │ │
  │  │                                                                                   │ │
  │  │  Token: SignServerToken                                                          │ │
  │  │  Key Label: document-signing-key                                                 │ │
  │  │  Algorithm: RSA 2048 / ECDSA P-256                                               │ │
  │  │                                                                                   │ │
  │  │  Operations:                                                                      │ │
  │  │  - C_Sign(mechanism=CKM_SHA256_RSA_PKCS, data=hash)                              │ │
  │  │  - Returns: signature bytes                                                       │ │
  │  └─────────────────────────────────────────────────────────────────────────────────┘ │
  │                                                                                        │
  └──────────────────────────────────────────────────────────────────────────────────────┘
           │
           │ Certificate validation
           ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────┐
  │                          EJBCA CE (Port 8443)                                         │
  │                                                                                        │
  │  Certificate Chain:                                                                   │
  │  ├─ Root CA (self-signed, offline)                                                   │
  │  ├─ Intermediate CA (IVF Document Signing CA)                                        │
  │  └─ End Entity (PDFSigner certificate)                                               │
  │                                                                                        │
  │  Validation:                                                                          │
  │  - OCSP check (real-time revocation status)                                          │
  │  - CRL check (certificate revocation list)                                           │
  │  - Chain validation (signature + validity period)                                    │
  │                                                                                        │
  └──────────────────────────────────────────────────────────────────────────────────────┘
           │
           │ Signed PDF bytes
           ▼
  ┌──────────────────┐
  │  Store Signed    │
  │     Document     │
  └────────┬─────────┘
           │
           ▼
  ┌──────────────────┐
  │     MinIO        │
  │ ivf-signed-pdfs  │ ──▶ PUT: tenants/{id}/signed/{docId}-signed.pdf
  │    bucket        │     Metadata: { signer, timestamp, signature_id }
  └────────┬─────────┘
           │
           ▼
  ┌──────────────────┐
  │   Audit Log      │ ──▶ Record signing event
  │                  │     { action: "DOCUMENT_SIGNED", documentId, signerId, ... }
  └────────┬─────────┘
           │
           ▼
  ┌──────────────────┐
  │   HTTP Response  │
  │   200 OK         │ ──▶ { signedDocumentUrl, signatureId, timestamp }
  └──────────────────┘
```

### 7.3 Real-time Queue Update Flow

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                         REAL-TIME QUEUE UPDATE FLOW                                      │
└─────────────────────────────────────────────────────────────────────────────────────────┘

  ┌──────────────────┐                              ┌──────────────────┐
  │  Queue Display   │                              │  Receptionist    │
  │  (Waiting Room)  │                              │  (Call Patient)  │
  └────────┬─────────┘                              └────────┬─────────┘
           │                                                  │
           │ WebSocket: /hubs/queue                          │ POST /api/queue/call
           │ Event: subscribe("queue-{tenantId}")             │
           ▼                                                  ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────┐
  │                              IVF.API                                                   │
  │                                                                                        │
  │  ┌─────────────────────────────────────────────────────────────────────────────────┐ │
  │  │                           QueueHub (SignalR)                                     │ │
  │  │                                                                                   │ │
  │  │  // Client connects                                                              │ │
  │  │  OnConnectedAsync() {                                                            │ │
  │  │    var tenantId = Context.User.Claims["tenant_id"];                              │ │
  │  │    await Groups.AddToGroupAsync(ConnectionId, $"queue-{tenantId}");              │ │
  │  │  }                                                                               │ │
  │  │                                                                                   │ │
  │  │  // Methods                                                                       │ │
  │  │  - JoinQueueGroup(tenantId)                                                      │ │
  │  │  - LeaveQueueGroup(tenantId)                                                     │ │
  │  │  - GetCurrentQueue()                                                             │ │
  │  │                                                                                   │ │
  │  └─────────────────────────────────────────────────────────────────────────────────┘ │
  │                                                                                        │
  │  ┌─────────────────────────────────────────────────────────────────────────────────┐ │
  │  │                         QueueEndpoints                                           │ │
  │  │                                                                                   │ │
  │  │  POST /api/queue/call                                                            │ │
  │  │  {                                                                               │ │
  │  │    1. Update queue status in database                                            │ │
  │  │    2. Inject IHubContext<QueueHub>                                               │ │
  │  │    3. Broadcast to group                                                         │ │
  │  │  }                                                                               │ │
  │  │                                                                                   │ │
  │  └─────────────────────────────────────────────────────────────────────────────────┘ │
  │                                                                                        │
  └──────────────────────────────────────────────────────────────────────────────────────┘
           │                                                  │
           │                                                  │
           │                  ┌───────────────────────────────┘
           │                  │
           │                  ▼
           │         ┌──────────────────┐
           │         │   PostgreSQL     │
           │         │  (queue table)   │
           │         │                  │
           │         │ UPDATE queues    │
           │         │ SET status =     │
           │         │ 'CALLED',        │
           │         │ called_at = NOW()│
           │         └────────┬─────────┘
           │                  │
           │                  ▼
           │         ┌──────────────────┐
           │         │  IHubContext     │
           │         │  <QueueHub>      │
           │         │                  │
           │         │ hubContext       │
           │         │ .Clients         │
           │         │ .Group("queue-X")│
           │         │ .SendAsync(...)  │
           │         └────────┬─────────┘
           │                  │
           ◀──────────────────┘
           │
           │ WebSocket message:
           │ {
           │   "type": "QueueUpdated",
           │   "data": {
           │     "ticketNumber": "A001",
           │     "patientName": "John Doe",
           │     "room": "Room 1",
           │     "status": "CALLED"
           │   }
           │ }
           │
           ▼
  ┌──────────────────┐
  │  Queue Display   │
  │  (Updates UI)    │ ──▶ Shows: "A001 - John Doe - Room 1"
  │                  │     Audio: "Now calling ticket A001"
  └──────────────────┘
```

---

## 8. Database Architecture

### 8.1 PostgreSQL Configuration

| Parameter | Value | Purpose |
|-----------|-------|---------|
| **Version** | 16-alpine | Latest stable |
| **Port** | 5433 | Non-default for security |
| **wal_level** | replica | Enable streaming replication |
| **max_wal_senders** | 5 | Concurrent replication connections |
| **max_replication_slots** | 5 | Replication slot limit |
| **wal_keep_size** | 256MB | WAL retention for slow standby |
| **archive_mode** | on | Enable WAL archiving |
| **archive_command** | cp to /archive | Local + S3 archiving |
| **hot_standby** | on | Allow queries on standby |
| **SSL Mode** | Prefer | Encrypted connections |

### 8.2 Database Schema Overview

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              DATABASE SCHEMA                                             │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                   CLINICAL                                               │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │   patients    │────▶│    couples    │◀────│   patients    │                          │
│  │               │     │               │     │   (partner)   │                          │
│  │  - id (PK)    │     │  - id (PK)    │     │               │                          │
│  │  - mrn        │     │  - wife_id    │     │               │                          │
│  │  - name       │     │  - husband_id │     │               │                          │
│  │  - dob        │     │  - created_at │     │               │                          │
│  │  - gender     │     │               │     │               │                          │
│  │  - tenant_id  │     │               │     │               │                          │
│  └───────────────┘     └───────┬───────┘     └───────────────┘                          │
│                                │                                                         │
│                                ▼                                                         │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │    cycles     │────▶│   embryos     │     │  ultrasounds  │                          │
│  │               │     │               │     │               │                          │
│  │  - id (PK)    │     │  - id (PK)    │     │  - id (PK)    │                          │
│  │  - couple_id  │     │  - cycle_id   │     │  - cycle_id   │                          │
│  │  - method     │     │  - grade      │     │  - date       │                          │
│  │  - status     │     │  - status     │     │  - findings   │                          │
│  │  - start_date │     │  - created_at │     │  - images     │                          │
│  └───────────────┘     └───────────────┘     └───────────────┘                          │
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │ semen_analyses│     │   andrology   │     │  sperm_bank   │                          │
│  │               │     │               │     │               │                          │
│  │  - id (PK)    │     │  - id (PK)    │     │  - id (PK)    │                          │
│  │  - patient_id │     │  - patient_id │     │  - donor_id   │                          │
│  │  - count      │     │  - procedure  │     │  - vials      │                          │
│  │  - motility   │     │  - results    │     │  - location   │                          │
│  └───────────────┘     └───────────────┘     └───────────────┘                          │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                 OPERATIONAL                                              │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │    queues     │     │ appointments  │     │   billing     │                          │
│  │               │     │               │     │               │                          │
│  │  - id (PK)    │     │  - id (PK)    │     │  - id (PK)    │                          │
│  │  - ticket_no  │     │  - patient_id │     │  - patient_id │                          │
│  │  - status     │     │  - doctor_id  │     │  - items      │                          │
│  │  - room       │     │  - datetime   │     │  - total      │                          │
│  │  - called_at  │     │  - status     │     │  - status     │                          │
│  └───────────────┘     └───────────────┘     └───────────────┘                          │
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │    forms      │     │   documents   │     │  signatures   │                          │
│  │               │     │               │     │               │                          │
│  │  - id (PK)    │     │  - id (PK)    │     │  - id (PK)    │                          │
│  │  - schema     │     │  - type       │     │  - doc_id     │                          │
│  │  - data       │     │  - s3_key     │     │  - signer_id  │                          │
│  │  - version    │     │  - signed     │     │  - cert_serial│                          │
│  └───────────────┘     └───────────────┘     └───────────────┘                          │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                   SECURITY                                               │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │    users      │     │   sessions    │     │  permissions  │                          │
│  │               │     │               │     │               │                          │
│  │  - id (PK)    │     │  - id (PK)    │     │  - id (PK)    │                          │
│  │  - username   │     │  - user_id    │     │  - role       │                          │
│  │  - password   │     │  - token_hash │     │  - resource   │                          │
│  │  - roles      │     │  - device_fp  │     │  - action     │                          │
│  │  - mfa_secret │     │  - ip_address │     │  - granted    │                          │
│  │  - tenant_id  │     │  - expires_at │     │               │                          │
│  └───────────────┘     └───────────────┘     └───────────────┘                          │
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │ refresh_tokens│     │  login_history│     │   consents    │                          │
│  │               │     │               │     │               │                          │
│  │  - id (PK)    │     │  - id (PK)    │     │  - id (PK)    │                          │
│  │  - token_hash │     │  - user_id    │     │  - patient_id │                          │
│  │  - family_id  │     │  - timestamp  │     │  - type       │                          │
│  │  - user_id    │     │  - ip_address │     │  - granted_at │                          │
│  │  - expires_at │     │  - user_agent │     │  - revoked_at │                          │
│  │  - revoked    │     │  - success    │     │               │                          │
│  └───────────────┘     └───────────────┘     └───────────────┘                          │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                    AUDIT                                                 │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│  ┌───────────────────────────────────────────────────────────────────────────────────┐ │
│  │                        audit_logs (PARTITIONED BY MONTH)                          │ │
│  │                                                                                    │ │
│  │  - id (PK)                                                                         │ │
│  │  - timestamp (partition key)                                                       │ │
│  │  - user_id                                                                         │ │
│  │  - action (CREATE, READ, UPDATE, DELETE, LOGIN, LOGOUT, SIGN, ...)               │ │
│  │  - resource_type                                                                   │ │
│  │  - resource_id                                                                     │ │
│  │  - old_value (JSONB)                                                              │ │
│  │  - new_value (JSONB)                                                              │ │
│  │  - ip_address                                                                      │ │
│  │  - user_agent                                                                      │ │
│  │  - correlation_id                                                                  │ │
│  │                                                                                    │ │
│  │  Partitions:                                                                       │ │
│  │  ├─ audit_logs_2026_01                                                            │ │
│  │  ├─ audit_logs_2026_02                                                            │ │
│  │  ├─ audit_logs_2026_03 (current)                                                  │ │
│  │  └─ ...                                                                           │ │
│  └───────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐                                                │
│  │security_events│     │   incidents   │                                                │
│  │               │     │               │                                                │
│  │  - id (PK)    │     │  - id (PK)    │                                                │
│  │  - type       │     │  - severity   │                                                │
│  │  - severity   │     │  - status     │                                                │
│  │  - details    │     │  - details    │                                                │
│  │  - timestamp  │     │  - resolved_at│                                                │
│  └───────────────┘     └───────────────┘                                                │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                  MULTI-TENANT                                            │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│  ┌───────────────┐     ┌───────────────┐     ┌───────────────┐                          │
│  │    tenants    │     │tenant_features│     │ tenant_limits │                          │
│  │               │     │               │     │               │                          │
│  │  - id (PK)    │     │  - tenant_id  │     │  - tenant_id  │                          │
│  │  - name       │     │  - feature    │     │  - resource   │                          │
│  │  - subdomain  │     │  - enabled    │     │  - max_value  │                          │
│  │  - plan       │     │  - config     │     │  - current    │                          │
│  │  - status     │     │               │     │               │                          │
│  └───────────────┘     └───────────────┘     └───────────────┘                          │
│                                                                                          │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### 8.3 MinIO Buckets

| Bucket | Purpose | Access Pattern | Lifecycle |
|--------|---------|----------------|-----------|
| **ivf-documents** | Patient documents, medical records | Tenant-prefixed: `tenants/{tenantId}/docs/` | Retained 7 years |
| **ivf-signed-pdfs** | Digitally signed PDFs | Tenant-prefixed: `tenants/{tenantId}/signed/` | Retained indefinitely |
| **ivf-medical-images** | Ultrasounds, lab images | Tenant-prefixed: `tenants/{tenantId}/images/` | Retained 7 years |
| **ivf-audit-archive** | Audit logs, compliance archives | Timestamped: `yyyy/mm/dd/` | Retained 7 years |

---

## 9. Security Architecture

### 9.1 OWASP Top 10 Coverage

| Category | Status | Implementation |
|----------|--------|----------------|
| **A01 Broken Access Control** | 🟢 Strong | Role + Feature + Field-level access control |
| **A02 Cryptographic Failures** | 🟢 Strong | AES-256-GCM vault, RS256 JWT, SHA-256 tokens |
| **A03 Injection** | 🟢 Good | EF Core parameterized queries, FluentValidation |
| **A04 Insecure Design** | 🟢 Strong | CQRS + MediatR pipeline behaviors |
| **A05 Security Misconfiguration** | 🟢 Good | Strict CSP, Swagger gated, secrets externalized |
| **A06 Vulnerable Components** | 🟢 Good | Trivy image scanning in CI/CD |
| **A07 Auth Failures** | 🟢 Strong | MFA + Passkey + BCrypt + rate limiting |
| **A08 Data Integrity** | 🟢 Strong | Digital signing (SignServer + EJBCA) |
| **A09 Logging & Monitoring** | 🟢 Excellent | OpenTelemetry + Prometheus + Grafana + SIEM |
| **A10 SSRF** | 🟡 Partial | URL validation on external requests |

### 9.2 Security Headers

| Header | Value | Purpose |
|--------|-------|---------|
| **Strict-Transport-Security** | max-age=63072000; includeSubDomains; preload | Force HTTPS for 2 years |
| **Content-Security-Policy** | default-src 'none'; script-src 'self'; ... | Prevent XSS, clickjacking |
| **X-Frame-Options** | DENY | Prevent clickjacking |
| **X-Content-Type-Options** | nosniff | Prevent MIME sniffing |
| **Referrer-Policy** | strict-origin-when-cross-origin | Control referrer info |
| **Permissions-Policy** | camera=(), microphone=(), geolocation=(), payment=() | Block dangerous APIs |
| **Cross-Origin-Opener-Policy** | same-origin | Cross-origin isolation |
| **Cross-Origin-Embedder-Policy** | require-corp | Cross-origin isolation |
| **X-XSS-Protection** | 0 | Disabled (CSP preferred) |

### 9.3 Encryption Standards

| Data Type | At Rest | In Transit |
|-----------|---------|------------|
| **Patient Data** | AES-256-GCM (Vault) | TLS 1.3 |
| **Passwords** | BCrypt (12 rounds) | TLS 1.3 |
| **JWT** | RS256 (3072-bit RSA) | TLS 1.3 |
| **Refresh Tokens** | SHA-256 hash | TLS 1.3 |
| **API Keys** | BCrypt | TLS 1.3 |
| **Database** | PostgreSQL pgcrypto | SSL Mode=Prefer |
| **Object Storage** | MinIO server-side encryption | TLS 1.3 |
| **Container Network** | Docker encrypted overlay | N/A |
| **Inter-service** | mTLS (SignServer, EJBCA) | TLS 1.3 |

---

## 10. Monitoring & Observability

### 10.1 Metrics Collection

| Source | Endpoint | Scrape Interval | Metrics |
|--------|----------|-----------------|---------|
| IVF.API | /metrics | 10s | Request rate, latency, errors, business metrics |
| PostgreSQL | :9187/metrics | 15s | Connections, queries, replication lag |
| Redis | :9121/metrics | 15s | Memory, commands, keys |
| MinIO | /minio/v2/metrics/cluster | 15s | Storage, requests, errors |
| Caddy | /metrics | 15s | Requests, TLS, upstream health |

### 10.2 Alert Rules Summary

| Category | Rules | Examples |
|----------|-------|----------|
| **API Health** | 8 | High error rate, slow response, 5xx errors |
| **Database** | 6 | Connection pool, replication lag, slow queries |
| **Redis** | 4 | Memory usage, connection errors |
| **Infrastructure** | 5 | Container restarts, disk space, CPU/memory |
| **Security** | 8 | Failed logins, MFA failures, suspicious activity |
| **Total** | 31 | |

### 10.3 SIEM Alert Rules (Loki)

| Rule | Trigger | Severity |
|------|---------|----------|
| Credential Stuffing | >10 failed logins/5min from same IP | Critical |
| MFA Brute Force | >5 MFA failures/5min for same user | Critical |
| Token Replay | Same token used from different IPs | Critical |
| Session Hijacking | Device fingerprint mismatch | High |
| Privilege Escalation | Role change without admin action | Critical |
| Cross-Tenant Access | Access attempt to different tenant | Critical |
| SQL Injection | SQL keywords in parameters | High |
| XSS Attack | Script tags in input | High |
| Path Traversal | "../" patterns in paths | High |
| Security Scanner | Known scanner user-agents | Medium |

### 10.4 Grafana Dashboards

| Dashboard | Purpose | Panels |
|-----------|---------|--------|
| **IVF System Overview** | Service health, RED metrics | 12 |
| **IVF API Monitoring** | API usage, errors, latency | 16 |
| **IVF Logs & Errors** | Log viewer, error tracking | 8 |
| **IVF Infrastructure** | Alerts, targets, performance | 10 |

---

## 11. Disaster Recovery

### 11.1 Backup Strategy

| Component | Method | Frequency | Retention |
|-----------|--------|-----------|-----------|
| **PostgreSQL** | pg_dump + streaming replication | Continuous | 7 days full, 30 days WAL |
| **MinIO** | mc mirror + versioning | Hourly sync | 90 days |
| **Configuration** | Git repository | On change | Indefinite |
| **Secrets** | Docker secrets backup | Daily | 30 days |
| **WAL Archives** | S3 upload | Continuous | 7 days |

### 11.2 Recovery Objectives

| Metric | Target | Current |
|--------|--------|---------|
| **RPO** (Recovery Point Objective) | <5 minutes | ~1 minute (streaming replication) |
| **RTO** (Recovery Time Objective) | <1 hour | ~15 minutes (hot standby) |

### 11.3 Failover Procedure

```
1. Detect Primary Failure
   └─ Prometheus alert: PostgreSQLDown
   └─ Grafana notification to Discord

2. Promote Standby
   └─ SSH to standby server
   └─ pg_ctl promote -D /var/lib/postgresql/data

3. Update DNS/Load Balancer
   └─ Point API to new primary
   └─ Update connection strings

4. Verify Data Integrity
   └─ Check replication lag
   └─ Verify last transaction

5. Restore Original Primary
   └─ pg_basebackup from new primary
   └─ Configure as standby
```

---

## 12. API Reference

### 12.1 Endpoint Categories

| Category | Base Path | Endpoints | Auth Required |
|----------|-----------|-----------|---------------|
| **Authentication** | /api/auth | 8 | Partial |
| **Patients** | /api/patients | 6 | Yes |
| **Couples** | /api/couples | 6 | Yes |
| **Cycles** | /api/cycles | 8 | Yes |
| **Embryos** | /api/embryos | 6 | Yes |
| **Queue** | /api/queue | 5 | Yes |
| **Forms** | /api/forms | 8 | Yes |
| **Digital Signing** | /api/digital-signing | 6 | Yes |
| **Compliance** | /api/compliance | 4 | Yes (Admin) |
| **Backup** | /api/data-backup | 5 | Yes (Admin) |
| **Key Vault** | /api/key-vault | 56 | Yes |
| **Infrastructure** | /api/infrastructure | 8 | Yes (Admin) |

### 12.2 SignalR Hubs

| Hub | Path | Purpose | Events |
|-----|------|---------|--------|
| **QueueHub** | /hubs/queue | Queue updates | QueueUpdated, TicketCalled |
| **NotificationHub** | /hubs/notifications | System notifications | NewNotification, Alert |
| **FingerprintHub** | /hubs/fingerprint | Biometric capture | FingerprintCaptured, MatchResult |
| **BackupHub** | /hubs/backup | Backup progress | BackupProgress, BackupComplete |
| **EvidenceHub** | /hubs/evidence | Compliance evidence | EvidenceCollected, ScanComplete |
| **InfrastructureHub** | /hubs/infrastructure | System monitoring | ServiceStatus, AlertTriggered |

---

## 13. Deployment Architecture

### 13.1 Docker Compose Files

| File | Purpose | Services |
|------|---------|----------|
| **docker-compose.yml** | Development | All services, hot-reload |
| **docker-compose.production.yml** | Production overrides | Secrets, replicas, limits |
| **docker-compose.stack.yml** | Docker Swarm | Full stack deployment |
| **docker-compose.monitoring.yml** | Monitoring stack | Prometheus, Grafana, Loki |
| **docker-compose.replica.yml** | DR replication | db-replica, minio-replica |

### 13.2 Kubernetes Manifests (k8s/)

| Manifest | Purpose |
|----------|---------|
| namespace.yaml | IVF namespace isolation |
| configmap.yaml | Application configuration |
| api-deployment.yaml | API pods with health checks |
| db-statefulset.yaml | PostgreSQL with PVC |
| redis-deployment.yaml | Redis cache |
| minio-statefulset.yaml | MinIO with PVC |
| network-policies.yaml | Network segmentation |
| ingress.yaml | External traffic routing |
| linkerd/service-profiles.yaml | Service mesh config |

### 13.3 CI/CD Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              CI/CD PIPELINE                                              │
└─────────────────────────────────────────────────────────────────────────────────────────┘

  ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
  │   Code   │────▶│   Build  │────▶│   Test   │────▶│   Scan   │────▶│  Deploy  │
  │   Push   │     │          │     │          │     │          │     │          │
  └──────────┘     └──────────┘     └──────────┘     └──────────┘     └──────────┘
       │                │                │                │                │
       ▼                ▼                ▼                ▼                ▼
  ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
  │  GitHub  │     │  Docker  │     │   Unit   │     │  Trivy   │     │  Swarm   │
  │  Actions │     │  Build   │     │  Tests   │     │  Image   │     │  Stack   │
  │          │     │          │     │ Integration│   │  Scan    │     │  Deploy  │
  └──────────┘     └──────────┘     └──────────┘     └──────────┘     └──────────┘
```

---

## 14. Security Maturity Assessment

### 14.1 Score Summary

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                    SECURITY MATURITY SCORE: 95/100 (Enterprise+)                         │
└─────────────────────────────────────────────────────────────────────────────────────────┘

   API Security              ████████████████████░░░░  93%  🟢 Enterprise-grade
   Authentication            █████████████████████░░░  95%  🟢 Enterprise+
   Security Headers (CSP)    ██████████████████████░░  96%  🟢 OWASP A+
   MediatR Zero Trust        █████████████████████░░░  95%  🟢 BeyondCorp level
   Docker Security           ████████████████░░░░░░░░  82%  🟢 Hardened
   Network & Firewall        ████████████████████░░░░  92%  🟢 Strong
   Secret Management         ██████████████████░░░░░░  85%  🟢 Strong
   PKI & Certificates        ████████████████░░░░░░░░  82%  🟢 Strong
   Monitoring & Logging      ████████████████████░░░░  88%  🟢 Strong
   Frontend Security         ████████████████████░░░░  90%  🟢 Strong
   Remote Access (VPN/SSH)   ██████████████████░░░░░░  85%  🟢 Strong
   Compliance (HIPAA/GDPR)   ██████████████████░░░░░░  86%  🟢 Strong
```

### 14.2 Vulnerability Status

| Severity | Before | After | Status |
|----------|--------|-------|--------|
| 🔴 Critical | 4 | 0 | ✅ All fixed |
| 🟠 High | 12 | 0 | ✅ All fixed |
| 🟡 Medium | 16 | 1 | ⬇️ HSM pending |
| 🟢 Low | 8 | 6 | Improve when time permits |

### 14.3 Comparison with Big Tech

| Capability | Google BeyondCorp | IVF System | Gap |
|------------|-------------------|------------|-----|
| Identity-based access | ✅ Context-aware proxy | ✅ JWT + MFA + Device fingerprint | Equivalent |
| Per-request authorization | ✅ Access Proxy | ✅ ZeroTrustBehavior pipeline | Equivalent |
| Network segmentation | ✅ Per-service policies | ✅ Overlay networks + Linkerd ready | Equivalent |
| Hardware security | ✅ HSM-backed | ⚠️ SoftHSM (functional) | Gap |
| WAF/DDoS | ✅ Cloud Armor | ✅ Cloudflare WAF | Equivalent |
| Compliance automation | ✅ Full toolset | ✅ Automated scanning | Equivalent |

### 14.4 Key Achievements

1. **Zero Trust Pipeline** — 6 MediatR behaviors enforce security on every request
2. **JWT RS256 3072-bit** — Stronger than industry standard (2048-bit minimum)
3. **httpOnly Cookie** — XSS-immune token storage with dual-mode support
4. **SHA-256 Refresh Tokens** — Hashed before storage, preventing token theft
5. **5-Layer Defense** — UFW → Fail2ban → SSH 2FA → WireGuard → mTLS
6. **Cloudflare WAF** — Managed + OWASP + custom rules + edge rate limiting
7. **15 SIEM Rules** — Detect credential stuffing, MFA brute force, token replay
8. **OpenTelemetry** — Distributed tracing for full request visibility
9. **SSO/OIDC Federation** — Google + Microsoft Entra ID support
10. **Automated Compliance** — 6-hour scanning interval with alerting

### 14.5 Remaining Gaps

1. **Hardware HSM** — SoftHSM is functional but not FIPS 140-2 Level 3
2. **Bug Bounty** — No formal program yet
3. **Red Team Exercises** — Not yet conducted
4. **SOC 2 Type II** — Audit pending

---

## Appendix A: Quick Reference

### A.1 Service URLs (Production)

| Service | URL | Auth |
|---------|-----|------|
| Application | https://natra.site | JWT |
| API | https://natra.site/api | JWT/API Key |
| Grafana | https://natra.site/grafana | Basic Auth |
| Prometheus | https://natra.site/prometheus | Basic Auth |

### A.2 Internal Service Ports

| Service | Port | Network |
|---------|------|---------|
| PostgreSQL | 5433 | ivf-data |
| PostgreSQL Standby | 5434 | ivf-data |
| Redis | 6379 | ivf-data |
| MinIO API | 9000 | ivf-data |
| MinIO Console | 9001 | ivf-data |
| EJBCA | 8443 | ivf-signing |
| SignServer | 9443 | ivf-signing |
| Prometheus | 9090 | ivf-monitoring |
| Grafana | 3000 | ivf-monitoring |
| Loki | 3100 | ivf-monitoring |

### A.3 Docker Commands

```bash
# Deploy stack
docker stack deploy -c docker-compose.stack.yml ivf

# View services
docker service ls

# View logs
docker service logs ivf_api -f

# Scale API
docker service scale ivf_api=3

# Update service
docker service update --image ivf-api:latest ivf_api
```

### A.4 Emergency Contacts

| Role | Contact | Escalation |
|------|---------|------------|
| On-Call Engineer | Discord #alerts | 15 min |
| Security Team | security@ivf.clinic | Immediate for Critical |
| DBA | Discord #database | 30 min |

---

**Document Version:** 1.0
**Last Updated:** 2026-03-13
**Next Review:** 2026-06-13
