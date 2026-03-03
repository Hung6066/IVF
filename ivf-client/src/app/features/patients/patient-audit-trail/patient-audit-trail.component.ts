import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PatientService } from '../../../core/services/patient.service';
import { PatientAuditTrail, PatientAuditEntry } from '../../../core/models/patient.models';

@Component({
  selector: 'app-patient-audit-trail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patient-audit-trail.component.html',
  styleUrl: './patient-audit-trail.component.scss',
})
export class PatientAuditTrailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private patientService = inject(PatientService);

  auditTrail = signal<PatientAuditTrail | null>(null);
  loading = signal(true);
  page = signal(1);
  pageSize = 20;
  expandedEntry = signal<string | null>(null);
  patientId = '';

  ngOnInit(): void {
    this.route.params.subscribe((params) => {
      this.patientId = params['id'];
      this.loadAuditTrail();
    });
  }

  loadAuditTrail(): void {
    this.loading.set(true);
    this.patientService.getAuditTrail(this.patientId, this.page(), this.pageSize).subscribe({
      next: (trail) => {
        this.auditTrail.set(trail);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggleEntry(id: string): void {
    this.expandedEntry.set(this.expandedEntry() === id ? null : id);
  }

  getActionLabel(action: string): string {
    switch (action) {
      case 'Create':
        return 'Tạo mới';
      case 'Update':
        return 'Cập nhật';
      case 'Delete':
        return 'Xóa';
      default:
        return action;
    }
  }

  getActionIcon(action: string): string {
    switch (action) {
      case 'Create':
        return '➕';
      case 'Update':
        return '✏️';
      case 'Delete':
        return '🗑️';
      default:
        return '📝';
    }
  }

  parseJson(value: string | undefined): Record<string, unknown> | null {
    if (!value) return null;
    try {
      return JSON.parse(value);
    } catch {
      return null;
    }
  }

  objectEntries(obj: Record<string, unknown>): [string, unknown][] {
    return Object.entries(obj);
  }

  nextPage(): void {
    this.page.set(this.page() + 1);
    this.loadAuditTrail();
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.set(this.page() - 1);
      this.loadAuditTrail();
    }
  }

  getTotalPages(): number {
    const trail = this.auditTrail();
    return trail ? Math.ceil(trail.totalCount / this.pageSize) : 1;
  }
}
