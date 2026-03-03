import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PatientService } from '../../../core/services/patient.service';
import {
  PatientAnalytics,
  Patient,
  PATIENT_TYPE_LABELS,
  PATIENT_STATUS_LABELS,
  RISK_LEVEL_LABELS,
  RISK_LEVEL_COLORS,
} from '../../../core/models/patient.models';

@Component({
  selector: 'app-patient-analytics',
  standalone: true,
  imports: [CommonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patient-analytics.component.html',
  styleUrl: './patient-analytics.component.scss',
})
export class PatientAnalyticsComponent implements OnInit {
  private patientService = inject(PatientService);

  analytics = signal<PatientAnalytics | null>(null);
  loading = signal(true);
  followUpPatients = signal<Patient[]>([]);
  expiredRetention = signal<Patient[]>([]);
  activeTab = signal<'overview' | 'demographics' | 'compliance' | 'followup'>('overview');

  readonly typeLabels = PATIENT_TYPE_LABELS;
  readonly statusLabels = PATIENT_STATUS_LABELS;
  readonly riskLabels = RISK_LEVEL_LABELS;
  readonly riskColors = RISK_LEVEL_COLORS;

  ngOnInit(): void {
    this.loadAnalytics();
    this.loadFollowUp();
    this.loadExpiredRetention();
  }

  loadAnalytics(): void {
    this.loading.set(true);
    this.patientService.getAnalytics().subscribe({
      next: (data) => {
        this.analytics.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadFollowUp(): void {
    this.patientService.getFollowUpPatients(90).subscribe({
      next: (patients) => this.followUpPatients.set(patients),
    });
  }

  loadExpiredRetention(): void {
    this.patientService.getExpiredDataRetention().subscribe({
      next: (patients) => this.expiredRetention.set(patients),
    });
  }

  setTab(tab: 'overview' | 'demographics' | 'compliance' | 'followup'): void {
    this.activeTab.set(tab);
  }

  getPercentage(value: number, total: number): number {
    return total > 0 ? Math.round((value / total) * 100) : 0;
  }

  getMaxValue(data: Record<string, number>): number {
    const values = Object.values(data);
    return values.length > 0 ? Math.max(...values) : 1;
  }

  getBarWidth(value: number, max: number): number {
    return max > 0 ? Math.round((value / max) * 100) : 0;
  }

  objectEntries(obj: Record<string, number>): [string, number][] {
    return Object.entries(obj);
  }
}
