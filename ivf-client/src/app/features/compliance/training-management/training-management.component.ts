import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComplianceService } from '../../../core/services/compliance.service';
import { UserService } from '../../../core/services/user.service';
import { ComplianceTraining, AssignTrainingRequest } from '../../../core/models/compliance.model';

@Component({
  selector: 'app-training-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './training-management.component.html',
  styleUrls: ['./training-management.component.scss'],
})
export class TrainingManagementComponent implements OnInit {
  private complianceService = inject(ComplianceService);
  private userService = inject(UserService);

  trainings = signal<ComplianceTraining[]>([]);
  totalCount = signal(0);
  loading = signal(true);
  users = signal<{ id: string; username: string; fullName: string; role: string }[]>([]);
  userSearchQuery = '';

  get completedCount(): number {
    return this.trainings().filter((t) => t.isCompleted).length;
  }

  get overdueCount(): number {
    return this.trainings().filter((t) => this.isOverdue(t)).length;
  }

  filterType = '';
  filterCompleted = '';
  filterOverdue = false;
  filterUsername = '';
  page = 1;

  trainingTypes = [
    'HIPAA',
    'GDPR',
    'Security Awareness',
    'Incident Response',
    'Data Handling',
    'AI Ethics',
  ];

  showAssignModal = false;
  showCompleteModal = false;
  selectedTraining: ComplianceTraining | null = null;

  assignForm: AssignTrainingRequest = {
    userId: '',
    trainingType: '',
    trainingName: '',
    description: '',
    dueDate: '',
    passThreshold: 80,
  };

  completeScore = 0;
  completeEvidence = '';

  ngOnInit() {
    this.loadTrainings();
    this.loadUsers();
  }

  loadUsers(search?: string) {
    this.userService.getUsers(search, undefined, undefined, 1, 100).subscribe({
      next: (result: any) => {
        const items = result?.items ?? result?.Items ?? [];
        this.users.set(items);
      },
      error: (err: any) => console.error('Failed to load users:', err),
    });
  }

  onUserSearch(query: string) {
    this.userSearchQuery = query;
    this.loadUsers(query || undefined);
  }

  getUserDisplay(userId: string): string {
    const user = this.users().find((u) => u.id === userId);
    return user ? `${user.fullName} (${user.username})` : userId;
  }

  loadTrainings() {
    this.loading.set(true);
    this.complianceService
      .getTrainings({
        type: this.filterType || undefined,
        completed: this.filterCompleted === '' ? undefined : this.filterCompleted === 'true',
        overdue: this.filterOverdue || undefined,
        page: this.page,
      })
      .subscribe({
        next: (result) => {
          this.trainings.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onFilterChange() {
    this.page = 1;
    this.loadTrainings();
  }

  openAssignModal() {
    this.assignForm = {
      userId: '',
      trainingType: '',
      trainingName: '',
      description: '',
      dueDate: '',
      passThreshold: 80,
    };
    this.userSearchQuery = '';
    if (this.users().length === 0) this.loadUsers();
    this.showAssignModal = true;
  }

  submitAssign() {
    if (
      !this.assignForm.userId ||
      !this.assignForm.trainingType ||
      !this.assignForm.trainingName ||
      !this.assignForm.dueDate
    )
      return;
    this.complianceService.assignTraining(this.assignForm).subscribe({
      next: () => {
        this.showAssignModal = false;
        this.loadTrainings();
      },
    });
  }

  openCompleteModal(training: ComplianceTraining) {
    this.selectedTraining = training;
    this.completeScore = 0;
    this.completeEvidence = '';
    this.showCompleteModal = true;
  }

  submitComplete() {
    if (!this.selectedTraining || this.completeScore < 0) return;
    this.complianceService
      .completeTraining(
        this.selectedTraining.id,
        this.completeScore,
        this.completeEvidence || undefined,
      )
      .subscribe({
        next: () => {
          this.showCompleteModal = false;
          this.selectedTraining = null;
          this.loadTrainings();
        },
      });
  }

  isOverdue(training: ComplianceTraining): boolean {
    return !training.isCompleted && new Date(training.dueDate) < new Date();
  }

  isExpiringSoon(training: ComplianceTraining): boolean {
    if (!training.expiresAt) return false;
    const diff = new Date(training.expiresAt).getTime() - Date.now();
    return diff > 0 && diff < 30 * 86400000;
  }

  getDaysUntilDue(training: ComplianceTraining): number {
    return Math.ceil((new Date(training.dueDate).getTime() - Date.now()) / 86400000);
  }
}
