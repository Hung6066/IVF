# Wazuh SIEM & Lynis Security Auditing — Hướng dẫn & Luồng hoạt động

> **Tài liệu này** mô tả toàn bộ kiến trúc, luồng dữ liệu, cách triển khai và tích hợp của **Wazuh 4.9.2** (SIEM) và **Lynis** (Security Auditing) trên hạ tầng IVF Platform.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Wazuh — Kiến trúc chi tiết](#2-wazuh--kiến-trúc-chi-tiết)
3. [Wazuh — Luồng dữ liệu end-to-end](#3-wazuh--luồng-dữ-liệu-end-to-end)
4. [Wazuh — Agent monitoring Docker Swarm](#4-wazuh--agent-monitoring-docker-swarm)
5. [Wazuh — Alert pipeline & Active Response](#5-wazuh--alert-pipeline--active-response)
6. [Wazuh — Triển khai & Vận hành](#6-wazuh--triển-khai--vận-hành)
7. [Lynis — Kiến trúc & Luồng audit](#7-lynis--kiến-trúc--luồng-audit)
8. [Lynis — Tích hợp Wazuh & MinIO](#8-lynis--tích-hợp-wazuh--minio)
9. [Tích hợp tổng thể Wazuh + Lynis](#9-tích-hợp-tổng-thể-wazuh--lynis)
10. [Hardening Ansible Role — Nâng cao Lynis Score](#10-hardening-ansible-role--nâng-cao-lynis-score)
11. [Tham chiếu: Ports, Credentials, Rule IDs](#11-tham-chiếu-ports-credentials-rule-ids)

---

## 1. Tổng quan kiến trúc

### Topology hạ tầng

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                            IVF Platform — 2 VPS                             │
│                                                                              │
│  ┌────────────────────────────────┐   ┌──────────────────────────────────┐  │
│  │  VPS1 (vmi3129107)            │   │  VPS2 (vmi3129111)               │  │
│  │  IP: 194.163.181.19           │   │  IP: 45.134.226.56               │  │
│  │  Docker Swarm: Leader         │   │  Docker Swarm: Reachable          │  │
│  │  Role: Standby                │   │  Role: Primary                    │  │
│  │                               │   │                                   │  │
│  │  ┌─────────────────────────┐  │   │  ┌─────────────────────────────┐  │  │
│  │  │  wazuh-agent (native)   │  │   │  │  wazuh-agent (native)       │  │  │
│  │  │  ID: 002                │  │   │  │  ID: 001                    │  │  │
│  │  │  systemd managed        │  │   │  │  systemd managed             │  │  │
│  │  └────────────┬────────────┘  │   │  └──────────────┬──────────────┘  │  │
│  │               │               │   │                  │                │  │
│  │  ┌─────────────────────────┐  │   │  ┌─────────────────────────────┐  │  │
│  │  │  IVF Stack (Swarm)      │  │   │  │  Wazuh Stack (Swarm + ivf) │  │  │
│  │  │  (API, EJBCA, SignSrv)  │  │   │  │  wazuh-manager             │  │  │
│  │  └─────────────────────────┘  │   │  │  wazuh-indexer (OpenSearch) │  │  │
│  │                               │   │  │  wazuh-dashboard            │  │  │
│  └────────────────────────────────┘   │  └─────────────────────────────┘  │  │
│                                       └──────────────────────────────────┘  │
│                                                                              │
│  WireGuard VPN: 10.200.0.1 (VPS2) ←──────── 10.200.0.2 (VPS1)              │
│  Internet access: https://natra.site/wazuh/ (Caddy → VPS2:5601)             │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Thành phần chính

| Thành phần             | Version | Node     | Vai trò                                     |
| ---------------------- | ------- | -------- | ------------------------------------------- |
| **wazuh-manager**      | 4.9.2   | VPS2     | Nhận/phân tích log từ agents, quản lý rules |
| **wazuh-indexer**      | 4.9.2   | VPS2     | Lưu trữ alerts/events (OpenSearch)          |
| **wazuh-dashboard**    | 4.9.2   | VPS2     | Giao diện web phân tích bảo mật             |
| **wazuh-agent (VPS1)** | 4.9.2   | VPS1     | Thu thập log/events từ VPS1, gửi về manager |
| **wazuh-agent (VPS2)** | 4.9.2   | VPS2     | Thu thập log/events từ VPS2, gửi về manager |
| **Lynis**              | latest  | cả 2 VPS | Audit bảo mật hệ thống hàng tuần            |

---

## 2. Wazuh — Kiến trúc chi tiết

### Sơ đồ thành phần

```mermaid
graph TB
    subgraph VPS1["VPS1 — vmi3129107 (194.163.181.19)"]
        direction TB
        A1[wazuh-agent<br/>ID: 002<br/>systemd service]

        subgraph MON_VPS1["Monitoring sources"]
            S1["/var/log/syslog<br/>/var/log/auth.log"]
            S2["Docker daemon<br/>journald"]
            S3["File Integrity<br/>/etc /bin /opt/ivf"]
            S4["Command output<br/>docker service ls<br/>docker node ls"]
            S5["Lynis audit<br/>/var/log/lynis/"]
        end
        S1 & S2 & S3 & S4 & S5 --> A1
    end

    subgraph VPS2["VPS2 — vmi3129111 (45.134.226.56)"]
        direction TB
        A2[wazuh-agent<br/>ID: 001<br/>systemd service]

        subgraph MON_VPS2["Monitoring sources"]
            S6["/var/log/syslog<br/>/var/log/auth.log"]
            S7["Docker daemon<br/>journald"]
            S8["File Integrity<br/>/etc /bin /opt/ivf"]
            S9["Command output<br/>docker service ls<br/>docker node ls"]
            S10["Lynis audit<br/>/var/log/lynis/"]
        end
        S6 & S7 & S8 & S9 & S10 --> A2

        subgraph WAZUH_STACK["Wazuh Stack (Docker Swarm)"]
            WM["wazuh-manager<br/>TCP 1514 (events)<br/>TCP 1515 (enrollment)<br/>TCP 55000 (API)"]
            WI["wazuh-indexer<br/>(OpenSearch)<br/>TCP 9200"]
            WD["wazuh-dashboard<br/>TCP 5601"]
            FB["Filebeat<br/>(built-in)"]
        end

        A2 -->|"TCP 1514<br/>AES encrypted"| WM
        WM --> FB
        FB -->|"HTTPS + mTLS"| WI
        WI --> WD
    end

    A1 -->|"TCP 1514<br/>AES encrypted"| WM

    subgraph CADDY["Caddy Reverse Proxy"]
        CP["https://natra.site/wazuh/<br/>Basic Auth: monitor/***"]
    end

    WD --> CP
    CP -->|"Browser"| USER([👤 Security Analyst])

    style VPS1 fill:#e8f4f8,stroke:#2196F3
    style VPS2 fill:#e8f8e8,stroke:#4CAF50
    style WAZUH_STACK fill:#fff3e0,stroke:#FF9800
    style CADDY fill:#fce4ec,stroke:#E91E63
```

### Internal communication trong Wazuh Stack

```mermaid
sequenceDiagram
    participant A as wazuh-agent
    participant M as wazuh-manager
    participant F as Filebeat (built-in)
    participant I as wazuh-indexer
    participant D as wazuh-dashboard

    Note over A,M: Enrollment (1 lần đầu)
    A->>M: TCP 1515 — OSSEC enrollment request
    M->>A: Enrollment key (agent ID + shared secret)
    A->>A: Lưu key vào /var/ossec/etc/client.keys

    Note over A,M: Event streaming (liên tục)
    loop every 10s notify, events realtime
        A->>M: TCP 1514 — Encrypted events (AES)
        M->>M: Decode + Analyze rules
        M->>M: Generate alert nếu match rule
    end

    Note over M,I: Alert indexing
    M->>F: Write alerts.json
    F->>I: HTTPS PUT /wazuh-alerts-* (mTLS)

    Note over I,D: Visualization
    D->>I: Query alerts/events
    I->>D: Aggregated results
    D->>D: Render dashboards
```

---

## 3. Wazuh — Luồng dữ liệu end-to-end

```mermaid
flowchart LR
    subgraph COLLECTION["Thu thập dữ liệu"]
        direction TB
        L1["Syslog\n/var/log/syslog\n/var/log/auth.log\n/var/log/kern.log"]
        L2["Docker Journald\ndocker.service events"]
        L3["Command Monitor\ndocker service ls\ndocker node ls\ndocker ps, df"]
        L4["File Integrity\n/etc, /bin, /sbin\n/opt/ivf/docker\n/var/lib/docker/swarm"]
        L5["Syscollector\nHW, OS, Network\nPackages, Ports\nProcesses (1h)"]
        L6["Rootcheck\nTrojans, PIDs\nPorts, Interfaces"]
        L7["SCA\nSecurity Config\nAssessment (12h)"]
        L8["Lynis .dat\n/var/log/lynis/\nreports/*.dat"]
    end

    subgraph AGENT["wazuh-agent"]
        direction TB
        BUF["Client Buffer\nQueue: 5000 events\n500 eps"]
        WODLE["Wodle\ndocker-listener\nsyscollector"]
    end

    subgraph MANAGER["wazuh-manager"]
        direction TB
        REMOTE["remoted\nTCP 1514\ndecrypt + queue"]
        ANALYSIS["analysisd\nDecode → Rules\nAlert generation"]
        DECODERS["Custom Decoders\ndocker-swarm-decoders.xml\nJSON_Decoder"]
        RULES["Custom Rules\ndocker_swarm_rules.xml\nRule IDs: 100100–100199"]
        AR["Active Response\nAutomatic block\nby rule trigger"]
    end

    subgraph INDEXER["wazuh-indexer\n(OpenSearch)"]
        IDX1["wazuh-alerts-*\nAll security alerts"]
        IDX2["wazuh-archives-*\nAll raw events"]
        IDX3["wazuh-monitoring-*\nAgent status"]
    end

    subgraph DASHBOARD["wazuh-dashboard"]
        DISC["Discover\nLog search"]
        DASH["Security Events\nDashboard"]
        INT["Integrity Monitoring"]
        VULN["Vulnerability Det."]
        SCA_DASH["SCA Compliance"]
    end

    L1 & L2 & L3 & L4 & L5 & L6 & L7 & L8 --> BUF
    WODLE --> BUF
    BUF -->|"TCP 1514\nAES"| REMOTE
    REMOTE --> ANALYSIS
    ANALYSIS --> DECODERS
    DECODERS --> RULES
    RULES -->|"level >= 3"| IDX1
    RULES -->|"logall=no"| IDX2
    RULES --> AR
    IDX1 & IDX2 & IDX3 --> DISC & DASH & INT & VULN & SCA_DASH

    style COLLECTION fill:#e3f2fd,stroke:#1976D2
    style AGENT fill:#e8f5e9,stroke:#388E3C
    style MANAGER fill:#fff8e1,stroke:#F57F17
    style INDEXER fill:#fce4ec,stroke:#C62828
    style DASHBOARD fill:#f3e5f5,stroke:#6A1B9A
```

---

## 4. Wazuh — Agent monitoring Docker Swarm

Đây là tính năng **tùy chỉnh** của IVF platform — agent thu thập trạng thái Docker Swarm thông qua `full_command` wodle.

### Luồng giám sát Docker Swarm

```mermaid
sequenceDiagram
    participant OS as Wazuh Agent (OS)
    participant CMD as Command Runner
    participant DOCKER as Docker CLI
    participant M as wazuh-manager
    participant DEC as analysisd + Decoders
    participant RULES as Custom Rules
    participant ALERT as Alert/Active Response

    Note over OS,CMD: Chu kỳ thu thập (mỗi command có frequency riêng)

    rect rgb(232, 245, 233)
        Note right of CMD: Mỗi 120s
        CMD->>DOCKER: docker service ls --format json
        DOCKER-->>CMD: {"name":"ivf_api","replicas":"1/1",...}
        CMD->>M: ossec: output: 'docker-swarm-services': {json}
    end

    rect rgb(232, 240, 254)
        Note right of CMD: Mỗi 120s
        CMD->>DOCKER: docker node ls --format json
        DOCKER-->>CMD: {"hostname":"vmi3129111","status":"Ready",...}
        CMD->>M: ossec: output: 'docker-swarm-nodes': {json}
    end

    rect rgb(255, 243, 224)
        Note right of CMD: Mỗi 300s
        CMD->>DOCKER: docker info --format json
        DOCKER-->>CMD: {"ServerVersion":"...","Swarm":"active",...}
        CMD->>M: ossec: output: 'docker-info': {json}
    end

    rect rgb(252, 228, 236)
        Note right of CMD: Mỗi 60s
        CMD->>DOCKER: docker ps --filter health=unhealthy
        DOCKER-->>CMD: {"name":"ivf_api","status":"unhealthy",...}
        CMD->>M: ossec: output: 'docker-unhealthy': {json}
    end

    M->>DEC: Parse log entry
    DEC->>DEC: prematch: 'docker-swarm-services'<br/>plugin_decoder: JSON_Decoder
    DEC->>RULES: Decoded fields available

    alt Replicas = 0/N
        RULES->>ALERT: Rule 100111 level=12<br/>"Service has 0 running replicas"
        ALERT->>ALERT: Log + alert_by_email
    else Node Down
        RULES->>ALERT: Rule 100101 level=12<br/>"Swarm Node is DOWN"
        ALERT->>ALERT: Log + alert_by_email
    else Container Unhealthy
        RULES->>ALERT: Rule 100131 level=10<br/>"Container is UNHEALTHY"
        ALERT->>ALERT: Log to indexer
    else All OK
        RULES->>RULES: Base rule (level=0)<br/>No alert generated
    end
```

### Custom Decoders cho Docker Swarm (`docker_swarm_decoders.xml`)

```xml
<!-- Mỗi command alias có 1 decoder riêng biệt -->
<decoder name="docker-swarm-services">
  <prematch>^ossec: output: 'docker-swarm-services': </prematch>
  <plugin_decoder offset="after_prematch">JSON_Decoder</plugin_decoder>
</decoder>

<decoder name="docker-swarm-nodes">
  <prematch>^ossec: output: 'docker-swarm-nodes': </prematch>
  <plugin_decoder offset="after_prematch">JSON_Decoder</plugin_decoder>
</decoder>
```

### Custom Rules — ID range & mức độ

| Rule ID    | Trigger                       | Level     | Group                     |
| ---------- | ----------------------------- | --------- | ------------------------- |
| 100100     | Node status received          | 0 (base)  | docker-swarm-nodes        |
| **100101** | Node status = **Down**        | **12** 🔴 | swarm-critical            |
| 100102     | Node availability = Drain     | 7 🟡      | swarm-warning             |
| 100103     | Node availability = Pause     | 7 🟡      | swarm-warning             |
| 100110     | Service list received         | 0 (base)  | docker-swarm-services     |
| **100111** | Service replicas = **0/N**    | **12** 🔴 | swarm-critical            |
| 100112     | Service replicas degraded     | 8 🟠      | swarm-warning             |
| 100120     | Docker info received          | 0 (base)  | docker-info               |
| 100121     | Swarm mode NOT active         | 10 🔴     | swarm-critical            |
| 100122     | Stopped containers > 20       | 5 🟡      | docker-maintenance        |
| 100130     | Unhealthy check received      | 0 (base)  | docker-unhealthy          |
| **100131** | Container **UNHEALTHY**       | **10** 🔴 | swarm-critical            |
| 100140     | Failed tasks received         | 0 (base)  | docker-swarm-failed-tasks |
| **100141** | Task **failed/rejected**      | **10** 🔴 | swarm-critical            |
| **100150** | SSH brute force (10+ in 2min) | **10** 🔴 | ssh-brute-force           |
| **100160** | `/etc/docker/` modified       | **10** 🔴 | docker-config-change      |
| **100161** | Docker Swarm secret/config    | **12** 🔴 | docker-config-change      |

---

## 5. Wazuh — Alert pipeline & Active Response

```mermaid
flowchart TD
    subgraph INPUT["Event Input"]
        E1["Agent event\n(TCP 1514, AES)"]
        E2["Manager local log"]
    end

    subgraph DECODE["Decode phase"]
        D1["Pre-decoder\n(hostname, timestamp, program)"]
        D2["Plugin decoder\n(JSON_Decoder, regex)"]
        D3["Custom decoders\ndocker_swarm_decoders.xml"]
    end

    subgraph ANALYZE["Analyze phase"]
        R1["Ruleset matching\nParent → Child rules"]
        R2{"Rule match?"}
        LEVEL{"level ≥ 3?"}
    end

    subgraph OUTPUT["Output"]
        DISCARD["Discard\n(level 0–2)"]
        LOCALFILE["alerts.json\n/var/ossec/logs/alerts/"]
        FB["Filebeat → Indexer\nwazuh-alerts-YYYY.MM.DD"]
    end

    subgraph AR["Active Response"]
        AR_CHECK{"level ≥ 10 AND\ngroup=ssh-brute-force?"}
        BLOCK["firewall-drop.sh\nBlock src IP 1800s"]
        LOG_ONLY["Log only"]
    end

    E1 & E2 --> D1 --> D2 --> D3
    D3 --> R1 --> R2

    R2 -->|"No match"| DISCARD
    R2 -->|"Match"| LEVEL
    LEVEL -->|"< 3"| DISCARD
    LEVEL -->|"≥ 3"| LOCALFILE
    LOCALFILE --> FB
    LOCALFILE --> AR_CHECK
    AR_CHECK -->|"Yes"| BLOCK
    AR_CHECK -->|"No"| LOG_ONLY

    style DISCARD fill:#ffcdd2
    style BLOCK fill:#ff8f00,color:#fff
    style FB fill:#c8e6c9
```

### File Integrity Monitoring (FIM) flow

```mermaid
stateDiagram-v2
    [*] --> BaselineScan: Agent khởi động\n(scan_on_start=yes)
    BaselineScan --> Monitoring: Baseline tạo xong\n(checksum, permissions, ownership)

    Monitoring --> ChangeDetected: Realtime inotify event\nhoặc periodic scan (12h)

    ChangeDetected --> Compare: So sánh với baseline

    Compare --> AlertGenerated: Thuộc tính thay đổi
    Compare --> Monitoring: Không thay đổi

    AlertGenerated --> RuleMatch: analysisd rule check

    RuleMatch --> LowAlert: /etc/hosts.deny, mtab\n(ignored paths)
    RuleMatch --> HighAlert: /etc/docker/ → level 10\n/var/lib/docker/swarm → level 12

    LowAlert --> Monitoring
    HighAlert --> IndexAlert: Gửi lên wazuh-indexer
    IndexAlert --> Dashboard: Hiển thị Integrity Monitoring tab
    Dashboard --> Monitoring
```

**Thư mục được giám sát (realtime):**

- `/etc/ssh` — SSH config changes
- `/etc/docker` — Docker daemon config
- `/opt/ivf/docker` — IVF app config
- `/var/lib/docker/swarm` — Swarm secrets/configs

**Thư mục bị loại trừ (high-churn):**

- `/var/lib/docker/containers`, `/overlay2`, `/network`, `/volumes`

---

## 6. Wazuh — Triển khai & Vận hành

### Quy trình triển khai ban đầu

```mermaid
flowchart TD
    START([Bắt đầu deploy]) --> CERTS

    subgraph CERTS["Bước 1: SSL Certificates"]
        C1["bash docker/wazuh/setup-certs.sh"]
        C2["Tạo root-ca.pem, wazuh-manager.pem\nwazuh-indexer.pem, wazuh-dashboard.pem\nadmin.pem"]
        C1 --> C2
    end

    CERTS --> SECRETS

    subgraph SECRETS["Bước 2: Docker Secrets"]
        S1["docker secret create\nwazuh_indexer_password"]
        S2["docker secret create\nwazuh_api_password"]
        S1 & S2 --> S3["Secrets tồn tại trong\nSwarm encrypted store"]
    end

    SECRETS --> NETWORK

    subgraph NETWORK["Bước 3: Overlay Network"]
        N1["docker network create\n--driver overlay --attachable\nivf-monitoring"]
        N2["Wazuh stack và IVF stack\nchia sẻ network này"]
        N1 --> N2
    end

    NETWORK --> DEPLOY

    subgraph DEPLOY["Bước 4: Stack Deploy"]
        D1["docker stack deploy\n-c docker/wazuh/docker-compose.yml\nwazuh"]
        D2["3 services khởi động:\nwazuh-manager, wazuh-indexer\nwazuh-dashboard"]
        D3["Constraint: node.hostname==vmi3129111\n(tất cả chạy trên VPS2)"]
        D1 --> D2 & D3
    end

    DEPLOY --> CADDY

    subgraph CADDY["Bước 5: Caddy Config"]
        CA1["Tạo docker config\ncaddyfile_vN (immutable)"]
        CA2["Route: /wazuh/*\n→ wazuh-dashboard:5601\nBasic auth bảo vệ"]
        CA1 --> CA2
    end

    CADDY --> AGENT

    subgraph AGENT["Bước 6: Agent Setup (VPS)"]
        AG1["Cài native: apt install wazuh-agent=4.9.2-1"]
        AG2["Ghi client.keys:\n002 vps1-vmi3129107 any <hash>"]
        AG3["Cấu hình manager IP:\n<address>45.134.226.56</address>"]
        AG4["Bật systemd service:\nsystemctl enable --now wazuh-agent"]
        AG1 --> AG2 --> AG3 --> AG4
    end

    AGENT --> VERIFY

    subgraph VERIFY["Bước 7: Xác minh"]
        V1["docker exec wazuh-manager\nagent_control -l"]
        V2["Kiểm tra: ID 001 vps2 Active\nID 002 vps1 Active"]
        V1 --> V2
    end

    VERIFY --> END([✓ Hoàn tất])

    style CERTS fill:#e3f2fd
    style SECRETS fill:#e8f5e9
    style NETWORK fill:#fff8e1
    style DEPLOY fill:#fce4ec
    style CADDY fill:#f3e5f5
    style AGENT fill:#e0f2f1
    style VERIFY fill:#fff3e0
```

### Lệnh vận hành thường dùng

```bash
# === Kiểm tra trạng thái ===
# Xem tất cả services Wazuh
docker stack ps wazuh

# Xem agents đang kết nối
docker exec wazuh_wazuh-manager.1.<task-id> /var/ossec/bin/agent_control -l

# Xem logs manager real-time
docker service logs -f wazuh_wazuh-manager

# Xem alerts.json (5 alerts gần nhất)
docker exec wazuh_wazuh-manager.1.<task-id> \
  tail -5 /var/ossec/logs/alerts/alerts.json

# === Wazuh API ===
# Lấy JWT token
TOKEN=$(curl -sk -u "wazuh-wui:0TLUTyAWNN5Xk0Gb9aeXdktR2Pp4Ww" \
  -X POST https://localhost:55000/security/user/authenticate \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['data']['token'])")

# Xem agent list
curl -sk -H "Authorization: Bearer $TOKEN" \
  https://localhost:55000/agents?pretty=true

# Xem health check
curl -sk -H "Authorization: Bearer $TOKEN" \
  https://localhost:55000/manager/status?pretty=true

# === API rate limit ===
# Xem cấu hình hiện tại (1500 req/min)
docker exec wazuh_wazuh-manager.1.<task-id> \
  cat /var/ossec/api/configuration/api.yaml
# access:
#   max_request_per_minute: 1500

# === Debug agent connection ===
# Trên VPS1: kiểm tra agent status
systemctl status wazuh-agent
/var/ossec/bin/wazuh-control status
cat /var/ossec/etc/client.keys

# Test kết nối port 1514 tới VPS2
nc -z -w5 45.134.226.56 1514 && echo "PORT OPEN" || echo "PORT BLOCKED"
```

### Cấu hình quan trọng

```
# Wazuh API rate limit — tránh 429 khi dashboard polling
# File: /var/ossec/api/configuration/api.yaml (trong named volume)
access:
  max_request_per_minute: 1500   # default 300 → bị 429 khi Caddy proxy single IP

# Manager disconnect detection
# agents_disconnection_time: 10m  → agent offline 10 phút mới đánh dấu Disconnected

# Alert level threshold
# log_alert_level: 3   → lưu vào alerts.json
# email_alert_level: 12 → gửi email
```

---

## 7. Lynis — Kiến trúc & Luồng audit

### Tổng quan Lynis

**Lynis** là công cụ audit bảo mật hệ thống mã nguồn mở, không cần agent, chạy trực tiếp trên host. Trên IVF Platform, Lynis được triển khai qua **Ansible** và tích hợp với **MinIO** (lưu báo cáo) và **Wazuh** (phân tích log).

```mermaid
graph TB
    subgraph ANSIBLE["Ansible (máy local → VPS)"]
        PLAY["ansible-playbook site.yml\n--tags lynis"]
        ROLE["Role: lynis\ntasks/main.yml"]
        TMPL["Templates:\nlynis-profile.j2\nlynis-cron.j2\nlynis-ship.j2"]
        PLAY --> ROLE --> TMPL
    end

    subgraph VPS["Mỗi VPS (VPS1 + VPS2)"]
        direction TB
        BIN["/usr/sbin/lynis\n(từ packages.cisofy.com)"]
        PROF["/etc/lynis/custom.prf\n(skip-test, quick=yes)"]
        CRON["/etc/cron.d/lynis-audit\nChủ nhật 02:30"]
        SHIP["/usr/local/bin/lynis-ship.sh"]
        LOGDIR["/var/log/lynis/reports/\nlynis-YYYY-MM-DD.dat\nlynis-YYYY-MM-DD.json"]
        LOGOUT["/var/log/lynis/lynis.log"]

        CRON -->|"chạy"| BIN
        PROF -->|"--profile"| BIN
        BIN -->|"output"| LOGDIR
        BIN -->|"log"| LOGOUT
        LOGDIR -->|"trigger"| SHIP
    end

    subgraph MINIO["MinIO (VPS2:9000)"]
        BUCKET["Bucket: ivf-documents\nObject: system/lynis/{hostname}/\nlynis-YYYY-MM-DD.json"]
    end

    subgraph WAZUH["Wazuh Agent"]
        WLOG["localfile: /var/log/lynis/lynis.log\nformat: syslog"]
        WDAT["localfile: /var/log/lynis/*.dat\nformat: full_command (nếu cấu hình)"]
    end

    TMPL -->|"deploy"| BIN & PROF & CRON & SHIP
    SHIP -->|"mc cp"| BUCKET
    LOGDIR --> WLOG
    WLOG --> WM[("wazuh-manager\nanalysis")]

    style ANSIBLE fill:#e3f2fd
    style VPS fill:#e8f5e9
    style MINIO fill:#fff8e1
    style WAZUH fill:#fce4ec
```

### Luồng Audit chi tiết

```mermaid
flowchart TD
    START(["Cron trigger\nChủ nhật 02:30"])

    subgraph AUDIT["Lynis Audit System"]
        direction TB
        A1["System Boot & Kernel\n/boot, kernel modules\nkernel hardening params"]
        A2["Authentication\nPAM, SSH config\npassword policy, sudo"]
        A3["File System\nPartitions, mount opts\n/tmp noexec, nosuid"]
        A4["Containers Detection\nDocker socket\ncgroup namespaces"]
        A5["Networking\nFirewall active?\nOpen ports, services\nWireGuard interfaces"]
        A6["Integrity\nPackage checksums\ninstalled packages\nvulnerable packages"]
        A7["Logging\nSyslog/rsyslog active?\nlog rotation\nremote logging"]
        A8["Cryptography\nSSL/TLS certs\ncipher suites\nkeystore analysis"]
    end

    REPORT["Tạo report\n/var/log/lynis/reports/\nlynis-YYYY-MM-DD.dat"]

    subgraph METRICS["Chỉ số kết quả"]
        M1["hardening_index: 0–100\n(điểm tổng hợp)"]
        M2["tests_executed: N\n(số test đã chạy)"]
        M3["warnings[]: [...]\n(cảnh báo nghiêm trọng)"]
        M4["suggestions[]: [...]\n(đề xuất cải thiện)"]
        M5["vulnerable_packages[]: [...]\n(gói có CVE)"]
    end

    PARSE["parse_lynis_dat()\nbash parser → JSON\nlynis-YYYY-MM-DD.json"]

    UPLOAD["mc cp .json\nMinIO: ivf-documents/\nsystem/lynis/{hostname}/"]

    WAZUH_LOG["logger -t lynis\nsyslog → /var/log/syslog\n→ Wazuh agent pickup"]

    START --> A1 & A2 & A3 & A4 & A5 & A6 & A7 & A8
    A1 & A2 & A3 & A4 & A5 & A6 & A7 & A8 --> REPORT
    REPORT --> M1 & M2 & M3 & M4 & M5
    M1 & M2 & M3 & M4 & M5 --> PARSE
    PARSE --> UPLOAD
    PARSE --> WAZUH_LOG

    style AUDIT fill:#e8f5e9
    style METRICS fill:#fff8e1
```

### Custom Profile (`/etc/lynis/custom.prf`)

Template nguồn: `ansible/roles/lynis/templates/lynis-profile.j2`

```ini
#
# Lynis Custom Profile — IVF Platform
# /etc/lynis/custom.prf
#
# Cập nhật: Sau khi áp dụng role hardening (site.yml Phase 1.5),
# chỉ skip các test thực sự KHÔNG áp dụng được cho môi trường VPS/Docker.
# Các test đã được FIX bởi role hardening sẽ không bị skip nữa.
#
# CÁC TEST ĐÃ ĐƯỢC FIX bởi role `hardening` (KHÔNG skip):
#   KRNL-6000, KRNL-5820, SSH-7408, SSH-7412, SSH-7440, SSH-7480
#   AUTH-9286, AUTH-9230, AUTH-9262, FILE-6374, PKGS-7386
#   ACCT-9628, ACCT-9626, MALW-3280, TIME-3104, HRDN-7222(kernel modules)
#   FINT-4350, LOGG-2190, FILE-7524, PKGS-7370, BANN-7126, NETW-3032
#

# ─── Skip tests không áp dụng cho VPS/Docker ───────────────

# BOOT-5122: GRUB password protection — Cloud VPS không có console vật lý
skip-test=BOOT-5122

# CONT-8004: Docker content trust — Dùng GHCR với image signing riêng
skip-test=CONT-8004

# LOGG-2154: Remote syslog — Loki/Promtail đã xử lý log aggregation
skip-test=LOGG-2154

# NETW-3200: Disable IPv6 — Docker/overlay networking cần IPv6
skip-test=NETW-3200

# FIRE-4512: Checks for iptables (UFW đã cấu hình trong role common)
skip-test=FIRE-4512

# DEB-0880: Check apt-show-versions — unattended-upgrades đã xử lý
skip-test=DEB-0880

# ─── Settings ──────────────────────────────────────────────

# Tắt màu để log sạch (Promtail parse)
colors=no

# Không dừng khi có lỗi nhỏ
quick=yes

# Luôn log kết quả test sai OS
log_tests_incorrect_os=yes
```

> **Lưu ý quan trọng**: `HRDN-7222` đã bị xóa khỏi danh sách `skip-test` — test này hiện được xử lý đúng bởi role `hardening` (vô hiệu hóa unused kernel modules). Nếu bạn thấy `HRDN-7222` trong skip list của version trước, đó là phiên bản cũ trước khi chạy Phase 1.5.

### Cron schedule

```
# /etc/cron.d/lynis-audit
# Chạy mỗi Chủ nhật 02:30 sáng (server time)
30 2 * * 0 root \
  lynis audit system \
    --profile /etc/lynis/custom.prf \
    --report-file /var/log/lynis/reports/lynis-$(date +%Y-%m-%d).dat \
    --logfile /var/log/lynis/lynis.log \
    --no-colors --quiet \
  2>&1 | logger -t lynis \
  && /usr/local/bin/lynis-ship.sh 2>&1 | logger -t lynis-ship

# Retention: xóa reports cũ hơn 90 ngày
0 3 * * 0 root find /var/log/lynis/reports -name "*.dat" -mtime +90 -delete
0 3 * * 0 root find /var/log/lynis/reports -name "*.json" -mtime +90 -delete
```

---

## 8. Lynis — Tích hợp Wazuh & MinIO

### Luồng upload MinIO

```mermaid
sequenceDiagram
    participant CRON as cron (02:30 Sun)
    participant LYNIS as lynis binary
    participant DAT as .dat report file
    participant SHIP as lynis-ship.sh
    participant PARSE as bash parser
    participant JSON as .json output
    participant MC as minio-client (mc)
    participant MINIO as MinIO (VPS2:9000)

    CRON->>LYNIS: lynis audit system --profile custom.prf
    LYNIS->>DAT: /var/log/lynis/reports/lynis-2026-03-15.dat
    Note over DAT: Key=Value format:\nhardening_index[]=72\nwarning[]=SSH-7408\nsuggestion[]=BOOT-5122

    LYNIS-->>CRON: Exit (0=pass, 1=warning)
    CRON->>SHIP: /usr/local/bin/lynis-ship.sh

    SHIP->>DAT: Read source file
    SHIP->>PARSE: parse_lynis_dat()
    PARSE->>PARSE: get_val() / get_list()\nExtract: hardening_index\nwarnings, suggestions\nvulnerable_packages
    PARSE->>JSON: /var/log/lynis/reports/lynis-2026-03-15.json

    Note over JSON: JSON format:\n{\n  "hostname": "vps2",\n  "hardening_index": 72,\n  "warning_count": 3,\n  "warnings": ["SSH-7408",...],\n  "vulnerable_packages": []\n}

    SHIP->>MC: mc alias set minio \n  http://vps2:9000 access secret
    SHIP->>MC: mc cp lynis-2026-03-15.json \n  minio/ivf-documents/system/lynis/vps2/
    MC->>MINIO: PUT /ivf-documents/system/lynis/vps2/lynis-2026-03-15.json
    MINIO-->>MC: 200 OK
    MC-->>SHIP: Upload success

    SHIP-->>CRON: Exit 0
    CRON->>CRON: logger -t lynis-ship "done"
```

### JSON output format

```json
{
  "hostname": "vmi3129111",
  "report_date": "2026-03-15",
  "generated_at": "2026-03-15T02:45:18Z",
  "lynis_version": "3.1.1",
  "os": "Ubuntu 24.04.1 LTS",
  "kernel": "6.8.0-51-generic",
  "hardening_index": 72,
  "tests_executed": 247,
  "firewall_active": "yes",
  "malware_scanner": "",
  "compiler_installed": "no",
  "warning_count": 3,
  "warnings": ["SSH-7408", "AUTH-9328", "LOGG-2154"],
  "suggestion_count": 18,
  "suggestions": ["BOOT-5122", "KRNL-6000", "..."],
  "vulnerable_packages": [],
  "source_file": "/var/log/lynis/reports/lynis-2026-03-15.dat"
}
```

### Wazuh integration — đọc log Lynis

Wazuh agent trên mỗi VPS thu thập log Lynis qua syslog (logger output của cron):

```xml
<!-- Agent config: ossec.conf — không cần cấu hình thêm -->
<!-- lynis output qua logger đi vào /var/log/syslog -->
<localfile>
  <log_format>syslog</log_format>
  <location>/var/log/syslog</location>
</localfile>
```

**Rule gợi ý thêm trong `docker_swarm_rules.xml`** để parse Lynis alerts:

```xml
<!-- Lynis hardening index thấp -->
<rule id="100200" level="7">
  <if_group>syslog</if_group>
  <match>lynis</match>
  <description>Lynis audit completed on $(hostname)</description>
  <group>lynis,security-audit</group>
</rule>
```

---

## 9. Tích hợp tổng thể Wazuh + Lynis

```mermaid
graph TB
    subgraph HOSTS["VPS Hosts"]
        subgraph VPS1H["VPS1 (vmi3129107)"]
            V1A["wazuh-agent\n(systemd, ID:002)"]
            V1L["Lynis\n(cron weekly)"]
            V1S["System logs\n/var/log/syslog\n/var/log/auth.log"]
            V1D["Docker events\njournald + commands"]
            V1F["File changes\ninotify realtime"]
        end

        subgraph VPS2H["VPS2 (vmi3129111)"]
            V2A["wazuh-agent\n(systemd, ID:001)"]
            V2L["Lynis\n(cron weekly)"]
            V2S["System logs"]
            V2D["Docker events"]
            V2F["File changes"]
        end
    end

    subgraph WAZUH["Wazuh Stack (VPS2, Docker Swarm)"]
        WM["wazuh-manager\nAnalysis + Rules + AR"]
        WI["wazuh-indexer\n(OpenSearch)"]
        WD["wazuh-dashboard\n:5601"]
    end

    subgraph STORAGE["Persistent Storage"]
        MINIO["MinIO\nivf-documents/system/lynis/\nJSON reports"]
        WVOL["Docker Volumes\nwazuh_api_configuration\nwazuh_logs\nwazuh_indexer_data"]
    end

    subgraph ACCESS["Access Layer"]
        CADDY["Caddy Reverse Proxy\nhttps://natra.site/wazuh/\nBasic Auth: monitor/***"]
        PROM["Prometheus\nMetrics integration\n(future)"]
    end

    subgraph ADMIN["Administrators"]
        SEC["Security Analyst\nbrowser"]
        ANSIBLE_CTRL["Ansible Controller\nlocal machine"]
    end

    V1S & V1D & V1F --> V1A
    V2S & V2D & V2F --> V2A
    V1L -->|"lynis-ship.sh"| MINIO
    V2L -->|"lynis-ship.sh"| MINIO
    V1L -->|"logger → syslog"| V1A
    V2L -->|"logger → syslog"| V2A

    V1A -->|"TCP 1514 AES"| WM
    V2A -->|"TCP 1514 AES"| WM
    WM --> WI --> WD
    WM <-->|"named volumes"| WVOL

    WD --> CADDY --> SEC
    ANSIBLE_CTRL -->|"ansible-playbook\n--tags lynis"| V1L & V2L
    MINIO -.->|"Manual download\nor API"| SEC

    style HOSTS fill:#e8f5e9,stroke:#4CAF50
    style WAZUH fill:#fff3e0,stroke:#FF9800
    style STORAGE fill:#e3f2fd,stroke:#2196F3
    style ACCESS fill:#fce4ec,stroke:#E91E63
    style ADMIN fill:#f3e5f5,stroke:#9C27B0
```

### Bảng tích hợp tổng hợp

| Công cụ                  | Loại         | Tần suất             | Dữ liệu                       | Lưu trữ          |
| ------------------------ | ------------ | -------------------- | ----------------------------- | ---------------- |
| **Wazuh Agent**          | Real-time    | Liên tục (1s events) | System logs, FIM, Docker      | OpenSearch index |
| **Wazuh SCA**            | Periodic     | 12h                  | Security configuration        | OpenSearch index |
| **Wazuh Rootcheck**      | Periodic     | 12h                  | Trojans, PIDs, ports          | OpenSearch index |
| **Wazuh Syscollector**   | Periodic     | 1h                   | HW/OS/Packages inventory      | OpenSearch index |
| **Wazuh Docker Monitor** | Periodic     | 60–600s              | Container/Service/Node status | OpenSearch index |
| **Lynis**                | Scheduled    | Weekly (Sun 02:30)   | Full security audit           | MinIO + syslog   |
| **FIM (realtime)**       | Event-driven | Immediate            | File changes /etc /opt/ivf    | OpenSearch index |

---

## 10. Hardening Ansible Role — Nâng cao Lynis Score

### Tổng quan

Role `hardening` được tạo để **tự động hóa việc fix tất cả Lynis warnings/suggestions** có thể xử lý bằng cấu hình hệ thống. Role này chạy trong **Phase 1.5** của `site.yml`, sau `common` và trước `docker`.

```mermaid
graph LR
    A["Phase 1\ncommon\n(base packages, UFW, WireGuard)"]
    B["Phase 1.5\nhardening\n(22 Lynis fixes)"]
    C["Phase 2\ndocker\n(Docker + Swarm)"]
    D["Phase 3\napp\n(IVF stack deploy)"]
    E["Phase 4\nlynis\n(audit + ship to MinIO)"]
    F["Phase 5\nwazuh-agent\n(SIEM agent)"]

    A --> B --> C --> D --> E --> F

    style B fill:#fff3e0,stroke:#FF9800,stroke-width:2px
```

### Cấu trúc role

```
ansible/roles/hardening/
├── defaults/
│   └── main.yml          # Toggle variables (aide, rkhunter, auditd, usb)
├── handlers/
│   └── main.yml          # restart sshd/auditd/sysstat/timesyncd
└── tasks/
    └── main.yml          # 22 task groups, tagged by Lynis test ID
```

### Bảng các Lynis test được fix

| Lynis Test ID     | Mô tả vấn đề                            | Giải pháp trong role                                                                   |
| ----------------- | --------------------------------------- | -------------------------------------------------------------------------------------- |
| **KRNL-6000**     | Kernel sysctl chưa hardened             | `/etc/sysctl.d/99-hardening.conf` — 20+ kernel params                                  |
| **KRNL-5820**     | Core dumps không bị hạn chế             | `limits.d/99-no-coredump.conf` + `systemd coredump.conf.d`                             |
| **SSH-7408**      | TCP forwarding enabled                  | `AllowTcpForwarding no` trong sshd_config                                              |
| **SSH-7412**      | MaxAuthTries quá cao                    | `MaxAuthTries 3`                                                                       |
| **SSH-7440**      | Agent forwarding enabled                | `AllowAgentForwarding no`                                                              |
| **SSH-7480**      | LogLevel không phải VERBOSE             | `LogLevel VERBOSE`                                                                     |
| **SSH-7490/7498** | Không có timeout SSH session            | `ClientAliveInterval 300`, `ClientAliveCountMax 2`                                     |
| **AUTH-9286**     | umask tại login.defs quá rộng           | `UMASK 027` trong `/etc/login.defs`                                                    |
| **FILE-6430**     | umask mặc định 022 (không bảo mật)      | `UMASK 027` (cùng task AUTH-9286)                                                      |
| **AUTH-9230**     | Password expiry chưa cấu hình           | `PASS_MAX_DAYS 90`, `PASS_MIN_DAYS 1`, `PASS_WARN_AGE 14`                              |
| **AUTH-9262**     | PAM password quality không được enforce | `libpam-pwquality` + `pwquality.conf` (minlen=12, minclass=3)                          |
| **FILE-6374**     | `/tmp` mount thiếu nodev/nosuid         | `tmpfs /tmp nodev,nosuid,size=1G` ⚠️ _noexec bỏ chủ ý (Docker cần)_                    |
| **PKGS-7386**     | Không có audit daemon                   | `auditd` + rules tại `/etc/audit/rules.d/99-ivf.rules`                                 |
| **ACCT-9628**     | sysstat chưa cài                        | `sysstat` package + enable collection                                                  |
| **ACCT-9626**     | Process accounting chưa bật             | `acct` package + `accton /var/log/account/pacct`                                       |
| **MALW-3280**     | Không có malware scanner                | `rkhunter` + DB update + weekly cron                                                   |
| **TIME-3104**     | NTP không được cấu hình rõ ràng         | `systemd-timesyncd` + `pool.ntp.org`                                                   |
| **HRDN-7222**     | Unused kernel modules vẫn loadable      | `/etc/modprobe.d/disable-filesystems.conf` _(squashfs giữ lại!)_                       |
| **NETW-3032**     | Unused network protocols enabled        | `/etc/modprobe.d/disable-protocols.conf` (dccp/sctp/rds/tipc)                          |
| **FINT-4350**     | Không có file integrity tool (AIDE)     | `aide` + `/etc/aide/aide.conf` + daily cron + init DB                                  |
| **LOGG-2190**     | Log rotation chưa tối ưu                | logrotate 13 tuần cho auth.log/syslog/kern.log                                         |
| **FILE-7524**     | Quyền file hệ thống sai                 | Fix permissions: cron.d (0700), shadow (0640), passwd (0644)                           |
| **PKGS-7370**     | Chưa có auto security updates           | `unattended-upgrades` + `20auto-upgrades` config                                       |
| **BANN-7126**     | Không có login warning banner           | `/etc/issue.net` banner + SSH Banner directive + `/etc/motd`                           |
| **USB-1000**      | USB storage module còn active (VPS)     | `/etc/modprobe.d/disable-usb-storage.conf` (khi `hardening_disable_usb_storage: true`) |

### ⚠️ Lưu ý quan trọng khi hardening với Docker

| Vấn đề                          | Chi tiết                                                                                                |
| ------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `/tmp` **không** có `noexec`    | Docker containers cần exec quyền trên `/tmp`. Role đặt `nodev,nosuid,size=1G` nhưng **không** `noexec`. |
| `squashfs` **không** bị disable | Docker overlay2 driver yêu cầu `squashfs`. Chỉ disable các FS: cramfs, hfs, hfsplus, jffs2, udf.        |
| `AIDE` khởi tạo tốn thời gian   | `aideinit --yes` chạy async với timeout 300s. Sau khoảng 5 phút mới xong lần đầu.                       |
| `auditd` quy tắc `-e 2`         | Mode immutable — sau khi load rules, cần reboot để thay đổi rules. Phù hợp production.                  |

### Chạy Hardening qua Ansible

```bash
# Chạy lần đầu trên tất cả VPS (bao gồm hardening)
ansible-playbook -i ansible/hosts.yml ansible/site.yml --tags setup,hardening

# Chạy hardening-only (cập nhật lại sau khi sửa role)
ansible-playbook -i ansible/hosts.yml ansible/site.yml --tags hardening

# Chỉ chạy trên VPS1
ansible-playbook -i ansible/hosts.yml ansible/site.yml --tags hardening --limit vps1

# Dry-run (check mode, không thay đổi hệ thống)
ansible-playbook -i ansible/hosts.yml ansible/site.yml --tags hardening --check --diff
```

### Variables có thể override

File `ansible/roles/hardening/defaults/main.yml`:

```yaml
# Bật/tắt cài đặt các tool nặng
hardening_install_aide: true # AIDE file integrity (tốn ~200MB)
hardening_install_rkhunter: true # rkhunter malware scanner
hardening_install_auditd: true # auditd daemon
hardening_tmp_nodev_nosuid: true # Mount /tmp với nodev,nosuid
hardening_disable_usb_storage: true # Disable USB storage module (VPS = true)
```

Override trong `ansible/hosts.yml` hoặc `group_vars/`:

```yaml
# Ví dụ: tắt AIDE nếu có giới hạn storage
hardening_install_aide: false
```

### Quy trình kiểm tra kết quả sau hardening

```bash
# Bước 1: Chạy Lynis manual sau khi hardening xong
ssh root@VPS1 "lynis audit system --profile /etc/lynis/custom.prf --quiet 2>&1 | tail -30"

# Bước 2: Xem hardening index mới
ssh root@VPS1 "grep 'hardening_index' /var/log/lynis/reports/lynis-\$(date +%Y-%m-%d).dat"

# Bước 3: Xem danh sách warnings còn lại (nên = 0 sau hardening)
ssh root@VPS1 "grep '^warning\[\]=' /var/log/lynis/reports/lynis-\$(date +%Y-%m-%d).dat"

# Bước 4: Xem suggestions còn lại (chỉ còn BOOT-5122 và các skip-test)
ssh root@VPS1 "grep '^suggestion\[\]=' /var/log/lynis/reports/lynis-\$(date +%Y-%m-%d).dat"

# Bước 5: Kiểm tra AIDE đã init chưa
ssh root@VPS1 "stat /var/lib/aide/aide.db 2>/dev/null && echo 'AIDE DB OK' || echo 'AIDE DB MISSING'"

# Bước 6: Kiểm tra auditd đang chạy
ssh root@VPS1 "systemctl is-active auditd && auditctl -l | head -5"

# Bước 7: Kiểm tra SSH config đúng
ssh root@VPS1 "sshd -T | grep -E 'allowtcpforwarding|maxauthtries|loglevel|clientaliveinterval'"
```

### Điểm hardening_index kỳ vọng

| Trạng thái                           | Điểm (ước tính) | Ghi chú                                   |
| ------------------------------------ | --------------- | ----------------------------------------- |
| Default Ubuntu 24.04 (chưa hardened) | 55–65           | Baseline trước khi apply role             |
| Sau Phase 1 (common + UFW)           | 60–68           | UFW active, basic packages                |
| **Sau Phase 1.5 (hardening)**        | **78–88**       | Target score sau khi apply role hardening |
| Skip tests skip lý do hợp lệ         | ~83–90+         | Sau khi `skip-test` loại bỏ N/A tests     |

> **Lưu ý**: Điểm chính xác phụ thuộc vào Lynis version và số lượng test áp dụng được. Tests bị skip (`BOOT-5122`, `CONT-8004`, v.v.) không tính vào điểm, nhưng giúp báo cáo sạch hơn.

### Workflow tổng thể Lynis CI cycle

```mermaid
flowchart TD
    DEPLOY["ansible-playbook site.yml\n(full deployment)"]

    subgraph HARDENING["Phase 1.5: Hardening"]
        H1["22 Lynis test fixes\nauto-applied"]
        H2["SSH / kernel / audit\nFIM / logging / permissions"]
        H1 --> H2
    end

    subgraph LYNIS_ROLE["Phase 4: Lynis"]
        L1["Install lynis binary"]
        L2["Deploy custom.prf\n(skip N/A tests only)"]
        L3["Deploy cron\n(Sun 02:30)"]
        L4["Deploy lynis-ship.sh\n(MinIO upload)"]
        L1 --> L2 --> L3 --> L4
    end

    subgraph WEEKLY["Tuần tiếp theo (tự động)"]
        W1["Cron trigger 02:30 Sun"]
        W2["lynis audit system\n--profile custom.prf"]
        W3["Parse .dat → .json"]
        W4["mc cp → MinIO\nsystem/lynis/{hostname}/"]
        W5["logger → syslog → Wazuh"]
        W1 --> W2 --> W3 --> W4 & W5
    end

    DEPLOY --> HARDENING --> LYNIS_ROLE --> WEEKLY

    subgraph DASHBOARD["IVF Dashboard"]
        D1["Lynis Dashboard\n/admin/lynis"]
        D2["Đọc JSON từ MinIO\nHiển thị hardening_index"]
        D3["Warnings / Suggestions\ntheo host"]
        D1 --> D2 --> D3
    end

    W4 -->|"MinIO REST API"| DASHBOARD

    style HARDENING fill:#fff3e0,stroke:#FF9800,stroke-width:2px
    style LYNIS_ROLE fill:#e8f5e9,stroke:#4CAF50
    style WEEKLY fill:#e3f2fd,stroke:#2196F3
    style DASHBOARD fill:#f3e5f5,stroke:#9C27B0
```

### Audit rules chi tiết (`/etc/audit/rules.d/99-ivf.rules`)

```bash
# Theo dõi thay đổi user/group
-w /etc/passwd     -p wa  -k user-changes
-w /etc/shadow     -p wa  -k user-changes
-w /etc/group      -p wa  -k user-changes
-w /etc/gshadow    -p wa  -k user-changes

# Theo dõi privilege escalation
-w /etc/sudoers    -p wa  -k privilege-escalation
-w /etc/sudoers.d/ -p wa  -k privilege-escalation

# Theo dõi SSH config
-w /etc/ssh/sshd_config -p wa -k ssh-config

# Theo dõi auth logs
-w /var/log/auth.log    -p wa  -k auth-log
-w /var/log/faillog     -p wa  -k auth-log

# Theo dõi Docker config (tích hợp với Wazuh rule 100160)
-w /etc/docker/ -p wa  -k docker-config
-w /usr/bin/docker -p x -k docker-exec

# Buffer size và immutable mode sau reboot
-b 8192
-e 2
```

> Các rule `docker-config` và `docker-exec` tích hợp trực tiếp với Wazuh rules **100160** và **100161** — mọi thay đổi `/etc/docker/` sẽ tạo alert level 10+.

### Troubleshooting Hardening

| Vấn đề                           | Lệnh kiểm tra                                         | Giải pháp                                                   |
| -------------------------------- | ----------------------------------------------------- | ----------------------------------------------------------- |
| `sysctl` task fail (permission)  | `dmesg \| grep sysctl`                                | VPS bị giới hạn namespace — `ignore_errors: true` đã set    |
| AIDE init chạy lâu > 5 phút      | `ps aux \| grep aide`                                 | Bình thường — `async: 300 poll: 10`. Kiểm tra sau ~5 phút   |
| auditd rules không load          | `auditctl -l` (nếu rỗng: `augenrules --load`)         | Restart: `systemctl restart auditd`                         |
| SSH bị lock sau khi hardening    | Local console hoặc VPS panel                          | Kiểm tra `sshd_config` syntax: `sshd -t` trước khi apply    |
| `/tmp` size=1G vẫn tràn          | `df -h /tmp`                                          | Tăng size trong role: `opts: defaults,nodev,nosuid,size=2G` |
| rkhunter false positive cảnh báo | `rkhunter --check --nocolors 2>&1 \| grep -i warning` | Chạy `rkhunter --propupd` sau khi cập nhật packages         |

---

## 11. Tham chiếu: Ports, Credentials, Rule IDs

### Ports Wazuh

| Port      | Protocol | Dịch vụ         | Mô tả                                           |
| --------- | -------- | --------------- | ----------------------------------------------- |
| **1514**  | TCP      | wazuh-manager   | Agent event communication (encrypted AES)       |
| **1515**  | TCP      | wazuh-manager   | Agent enrollment                                |
| **1516**  | TCP      | wazuh-manager   | Cluster communication (internal)                |
| **55000** | TCP      | wazuh-manager   | REST API (internal only, không expose ra ngoài) |
| **9200**  | TCP      | wazuh-indexer   | OpenSearch HTTP (internal)                      |
| **9300**  | TCP      | wazuh-indexer   | OpenSearch transport (internal)                 |
| **5601**  | TCP      | wazuh-dashboard | Web UI (qua Caddy proxy)                        |

### Credentials

| Dịch vụ              | User         | Password                           | Ghi chú                         |
| -------------------- | ------------ | ---------------------------------- | ------------------------------- |
| Wazuh Dashboard      | `admin`      | `NXPPTSMdDcOAC9AzlhfNxN0ZYVrOpW1g` | OpenSearch admin                |
| Wazuh API            | `wazuh-wui`  | `0TLUTyAWNN5Xk0Gb9aeXdktR2Pp4Ww`   | API user                        |
| Caddy Basic Auth     | `monitor`    | `wDDaI8zzSTBPyzfGp3wRc6JkDGgIv6ZF` | HTTPS proxy auth                |
| MinIO (Lynis upload) | `minioadmin` | `minioadmin123`                    | S3 API (ghi đè trong hosts.yml) |

> ⚠️ **Bảo mật**: Thay đổi tất cả credentials trong môi trường production. MinIO credentials nên được ghi đè trong `ansible/hosts.yml` hoặc `group_vars/all.yml`.

### Agent IDs

| ID  | Name            | VPS  | IP        | Status                |
| --- | --------------- | ---- | --------- | --------------------- |
| 000 | wazuh-manager   | VPS2 | 127.0.0.1 | Active/Local (server) |
| 001 | vps2-vmi3129111 | VPS2 | any       | Active ✅             |
| 002 | vps1-vmi3129107 | VPS1 | any       | Active ✅             |

### Lynis paths

| Path                                            | Mô tả               |
| ----------------------------------------------- | ------------------- |
| `/usr/sbin/lynis`                               | Binary              |
| `/etc/lynis/custom.prf`                         | Custom profile      |
| `/var/log/lynis/lynis.log`                      | Audit log           |
| `/var/log/lynis/reports/lynis-YYYY-MM-DD.dat`   | Raw report          |
| `/var/log/lynis/reports/lynis-YYYY-MM-DD.json`  | Parsed JSON         |
| `/usr/local/bin/lynis-ship.sh`                  | MinIO upload script |
| `/etc/cron.d/lynis-audit`                       | Cron definition     |
| `MinIO: ivf-documents/system/lynis/{hostname}/` | Remote storage      |

### Hardening paths

| Path                                         | Mô tả                    |
| -------------------------------------------- | ------------------------ |
| `/etc/sysctl.d/99-hardening.conf`            | Kernel sysctl params     |
| `/etc/security/limits.d/99-no-coredump.conf` | Core dump limits         |
| `/etc/systemd/coredump.conf.d/disable.conf`  | Systemd coredump disable |
| `/etc/ssh/sshd_config`                       | SSH hardening config     |
| `/etc/audit/rules.d/99-ivf.rules`            | Audit rules              |
| `/etc/security/pwquality.conf`               | Password complexity      |
| `/etc/modprobe.d/disable-filesystems.conf`   | Disabled FS modules      |
| `/etc/modprobe.d/disable-protocols.conf`     | Disabled network modules |
| `/etc/modprobe.d/disable-usb-storage.conf`   | Disabled USB storage     |
| `/etc/aide/aide.conf`                        | AIDE config              |
| `/var/lib/aide/aide.db`                      | AIDE baseline database   |
| `/etc/cron.d/aide-daily`                     | AIDE daily check cron    |
| `/etc/cron.d/rkhunter-weekly`                | rkhunter weekly cron     |
| `/etc/issue.net`                             | SSH login banner         |
| `/etc/apt/apt.conf.d/50unattended-upgrades`  | Auto security updates    |

### Chạy Lynis thủ công

```bash
# Audit toàn hệ thống với custom profile
lynis audit system \
  --profile /etc/lynis/custom.prf \
  --report-file /var/log/lynis/reports/lynis-manual.dat \
  --logfile /var/log/lynis/lynis.log \
  --no-colors

# Xem hardening index ngay sau khi chạy
grep "hardening_index" /var/log/lynis/reports/lynis-manual.dat

# Upload thủ công lên MinIO
/usr/local/bin/lynis-ship.sh

# Xem warnings trong report
grep "^warning\[\]=" /var/log/lynis/reports/lynis-$(date +%Y-%m-%d).dat

# Deploy lại Lynis qua Ansible
ansible-playbook ansible/site.yml --tags lynis -i ansible/hosts.yml

# Deploy hardening + lynis cùng lúc
ansible-playbook ansible/site.yml --tags hardening,lynis -i ansible/hosts.yml
```

### Troubleshooting

| Vấn đề                          | Kiểm tra                                             | Giải pháp                                                      |
| ------------------------------- | ---------------------------------------------------- | -------------------------------------------------------------- |
| Agent `Never connected`         | `agent_control -l` trên manager                      | Kiểm tra port 1514 từ agent tới manager; xem `client.keys`     |
| Agent `Disconnected`            | `systemctl status wazuh-agent`                       | `systemctl start wazuh-agent`; xem `/var/ossec/logs/ossec.log` |
| Dashboard 429 Too Many Requests | `cat /var/ossec/api/configuration/api.yaml`          | Tăng `max_request_per_minute: 1500`                            |
| Lynis ship fail                 | `journalctl -t lynis-ship`                           | Kiểm tra MinIO access key; `mc ping minio`                     |
| FIM false positives nhiều       | `wazuh-dashboard → Integrity Monitoring`             | Thêm `<ignore>` vào ossec.conf                                 |
| Custom rules không hoạt động    | `agent_control -m 1 -f docker-swarm-services` (test) | Kiểm tra XML syntax; `ossec-logtest` trên manager              |

---

_Tài liệu được tạo: 2026-03-15 | Cập nhật: 2026-03-16 | Version: 2.0 | IVF Platform Security Infrastructure_
