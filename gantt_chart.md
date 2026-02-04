# IVF Project - Gantt Chart Timeline

## Project Overview
- **Start Date:** February 10, 2026
- **End Date:** June 6, 2026
- **Total Duration:** 18 weeks (9 sprints × 2 weeks)
- **Total Tasks:** 45 tasks
- **Total Hours:** ~600 hours

---

## Timeline Visualization

```
WEEK:        1    2    3    4    5    6    7    8    9   10   11   12   13   14   15   16   17   18
DATE:       2/10 2/17 2/24 3/03 3/10 3/17 3/24 3/31 4/07 4/14 4/21 4/28 5/05 5/12 5/19 5/26 6/02 6/09
            ════════════════════════════════════════════════════════════════════════════════════════

SPRINT 1    ████████
Foundation  ████████

SPRINT 2             ████████
Patient/Queue        ████████

SPRINT 3                      ████████
Consultation                  ████████

SPRINT 4                               ████████
Ultrasound                             ████████

SPRINT 5                                        ████████
Lab (LABO)                                      ████████

SPRINT 6                                                 ████████
Andrology                                                ████████

SPRINT 7                                                          ████████
Sperm Bank                                                        ████████

SPRINT 8                                                                   ████████
Billing                                                                    ████████

SPRINT 9                                                                            ████████
Polish                                                                              ████████
```

---

## Detailed Gantt by Task

### Sprint 1: Foundation (Week 1-2)
```
Task              Feb 10   11   12   13   14   17   18   19   20   21
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
1.1 Solution      ████
1.2 PostgreSQL    ████ ████
1.3 Angular       ████ ████
1.4 JWT Auth                ████ ████ ████ ████ ████
1.5 Login UI                ████ ████ ████
```

### Sprint 2: Patient & Queue (Week 3-4)
```
Task              Feb 17   18   19   20   21   24   25   26   27   28
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
2.1 Patient Ent   ████ ████
2.2 Patient API        ████ ████ ████
2.3 Patient UI         ████ ████ ████
2.4 Queue API                         ████ ████ ████
2.5 SignalR                                ████ ████
2.6 Queue UI                          ████ ████ ████ ████ ████
```

### Sprint 3: Consultation (Week 5-6)
```
Task              Mar 03   04   05   06   07   10   11   12   13   14
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
3.1 Couple Mgmt   ████ ████
3.2 Cycle API          ████ ████ ████
3.3 Consult UI    ████ ████ ████ ████ ████
3.4 Prescrip API                      ████ ████
3.5 Prescrip UI                       ████ ████ ████
```

### Sprint 4: Ultrasound (Week 7-8)
```
Task              Mar 17   18   19   20   21   24   25   26   27   28
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
4.1 US API        ████ ████ ████
4.2 Follicle UI   ████ ████ ████ ████ ████
4.3 History View                      ████ ████
4.4 SA PK Form                        ████ ████ ████
```

### Sprint 5: Lab (Week 9-10)
```
Task              Mar 31  Apr 01  02   03   04   07   08   09   10   11
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
5.1 Embryo Ent    ████ ████
5.2 Embryo API         ████ ████ ████
5.3 Embryo UI          ████ ████ ████ ████
5.4 Cryo Mgmt                              ████ ████ ████
5.5 Cryo Map UI                            ████ ████ ████
```

### Sprint 6: Andrology (Week 11-12)
```
Task              Apr 14   15   16   17   18   21   22   23   24   25
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
6.1 Semen Domain  ████ ████
6.2 Semen API          ████ ████ ████
6.3 Semen UI      ████ ████ ████ ████
6.4 Sperm Wash                        ████ ████ ████
```

### Sprint 7: Sperm Bank (Week 13-14)
```
Task              Apr 28   29   30  May 01  02   05   06   07   08   09
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
7.1 Donor Domain  ████ ████
7.2 Sperm Bank         ████ ████ ████ ████
7.3 Donor RegUI   ████ ████ ████ ████
7.4 Matching                              ████ ████
7.5 HIV Tracking                               ████ ████
```

### Sprint 8: Billing (Week 15-16)
```
Task              May 12   13   14   15   16   19   20   21   22   23
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
8.1 Invoice Dom   ████ ████
8.2 Invoice API        ████ ████ ████ ████
8.3 Billing UI    ████ ████ ████ ████ ████
8.4 Svc Pricing                       ████ ████
8.5 Pharmacy                          ████ ████ ████
```

### Sprint 9: Polish (Week 17-18)
```
Task              May 26   27   28   29   30  Jun 02  03   04   05   06
                  Mon  Tue  Wed  Thu  Fri  Mon  Tue  Wed  Thu  Fri
────────────────────────────────────────────────────────────────────
9.1 Reports API   ████ ████ ████ ████
9.2 Analytics UI  ████ ████ ████ ████
9.3 Performance                       ████ ████ ████
9.4 UI/UX Polish                      ████ ████ ████ ████
9.5 Testing                           ████ ████ ████ ████ ████
9.6 Documentation                                    ████ ████
```

---

## Resource Allocation

### Backend Team
```
Sprint 1:  ████████████████░░░░░░░░░░░░░░░░  36h (1.1+1.2+1.4)
Sprint 2:  ████████████████████░░░░░░░░░░░░  44h (2.1+2.2+2.4+2.5)
Sprint 3:  ████████████████████░░░░░░░░░░░░  40h (3.1+3.2+3.4)
Sprint 4:  ████████░░░░░░░░░░░░░░░░░░░░░░░░  16h (4.1)
Sprint 5:  ██████████████████████░░░░░░░░░░  44h (5.1+5.2+5.4)
Sprint 6:  ████████████░░░░░░░░░░░░░░░░░░░░  24h (6.1+6.2)
Sprint 7:  ████████████████████░░░░░░░░░░░░  40h (7.1+7.2+7.4)
Sprint 8:  ██████████████████████░░░░░░░░░░  44h (8.1+8.2+8.4)
Sprint 9:  ████████████████████░░░░░░░░░░░░  40h (9.1+9.3)
────────────────────────────────────────────────────────────────
TOTAL:     328 hours
```

### Frontend Team
```
Sprint 1:  ████████░░░░░░░░░░░░░░░░░░░░░░░░  16h (1.3+1.5)
Sprint 2:  ████████████████░░░░░░░░░░░░░░░░  32h (2.3+2.6)
Sprint 3:  ████████████████████░░░░░░░░░░░░  40h (3.3+3.5)
Sprint 4:  ██████████████████████████░░░░░░  52h (4.2+4.3+4.4)
Sprint 5:  ████████████████████░░░░░░░░░░░░  40h (5.3+5.5)
Sprint 6:  ████████████░░░░░░░░░░░░░░░░░░░░  20h (6.3)
Sprint 7:  ████████░░░░░░░░░░░░░░░░░░░░░░░░  16h (7.3)
Sprint 8:  ████████████░░░░░░░░░░░░░░░░░░░░  24h (8.3)
Sprint 9:  ████████████████████████░░░░░░░░  48h (9.2+9.4)
────────────────────────────────────────────────────────────────
TOTAL:     288 hours
```

---

## Milestones

| Milestone | Date | Deliverable |
|-----------|------|-------------|
| M1 | Feb 21 | Auth + Patient Registration working |
| M2 | Feb 28 | Queue system with real-time display |
| M3 | Mar 14 | Consultation + Prescription flow |
| M4 | Mar 28 | Ultrasound monitoring complete |
| M5 | Apr 11 | Lab embryo tracking + Cryo storage |
| M6 | Apr 25 | Andrology module complete |
| M7 | May 09 | Sperm Bank operational |
| M8 | May 23 | Billing + Pharmacy ready |
| M9 | Jun 06 | **Production Ready** |

---

## Risk Buffer

Each sprint includes 10% buffer time (not shown in task estimates):
- Sprint contingency: 4 hours per sprint
- Total buffer: 36 hours across project

## Critical Path

```
1.1 → 1.2 → 1.4 → 2.1 → 2.2 → 3.1 → 3.2 → 5.1 → 5.2 → 7.1 → 7.2 → 8.1 → 8.2 → 9.1
```

Any delay in this path will delay the entire project.
