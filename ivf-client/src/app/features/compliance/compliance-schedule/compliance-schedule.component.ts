import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComplianceService } from '../../../core/services/compliance.service';
import { UserService } from '../../../core/services/user.service';
import { ComplianceScheduleTask, ComplianceFrequency } from '../../../core/models/compliance.model';

@Component({
  selector: 'app-compliance-schedule',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './compliance-schedule.component.html',
  styleUrls: ['./compliance-schedule.component.scss'],
})
export class ComplianceScheduleComponent implements OnInit {
  private complianceService = inject(ComplianceService);
  private userService = inject(UserService);

  tasks = signal<ComplianceScheduleTask[]>([]);
  totalCount = signal(0);
  loading = signal(true);
  selectedTask = signal<ComplianceScheduleTask | null>(null);
  users = signal<{ id: string; username: string; fullName: string; role: string }[]>([]);
  userSearchQuery = '';

  filterFramework = '';
  filterFrequency = '';
  filterCategory = '';
  filterOverdue = false;
  filterUpcoming = false;
  page = 1;

  showCreateModal = false;
  showCompleteModal = false;
  showAssignModal = false;

  createForm = {
    taskName: '',
    description: '',
    framework: 'ALL',
    frequency: 'Monthly' as ComplianceFrequency,
    category: 'Review',
    owner: '',
    nextDueDate: '',
    evidenceRequired: '',
    priority: 'Medium',
  };
  completeForm = { completedBy: '', notes: '' };
  assignForm = { userId: '' };

  frameworks = ['ALL', 'SOC2', 'ISO27001', 'HIPAA', 'GDPR', 'HITRUST', 'NIST_AI_RMF', 'ISO42001'];
  frequencies: ComplianceFrequency[] = [
    'Daily',
    'Weekly',
    'Monthly',
    'Quarterly',
    'SemiAnnual',
    'Annual',
  ];
  categories = ['Review', 'Audit', 'Training', 'Monitoring', 'Documentation', 'Testing'];

  ngOnInit() {
    this.loadTasks();
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

  loadTasks() {
    this.loading.set(true);
    this.complianceService
      .getScheduleList({
        framework: this.filterFramework || undefined,
        frequency: this.filterFrequency || undefined,
        category: this.filterCategory || undefined,
        overdue: this.filterOverdue || undefined,
        upcoming: this.filterUpcoming || undefined,
        page: this.page,
        pageSize: 50,
      })
      .subscribe({
        next: (result) => {
          this.tasks.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onFilterChange() {
    this.page = 1;
    this.loadTasks();
  }

  seedDefaults() {
    this.complianceService.seedScheduleDefaults().subscribe({
      next: () => this.loadTasks(),
    });
  }

  selectTask(task: ComplianceScheduleTask) {
    this.complianceService.getScheduleTask(task.id).subscribe({
      next: (data) => this.selectedTask.set(data),
    });
  }

  openComplete(task: ComplianceScheduleTask) {
    this.selectTask(task);
    this.completeForm = { completedBy: '', notes: '' };
    this.userSearchQuery = '';
    if (this.users().length === 0) this.loadUsers();
    this.showCompleteModal = true;
  }

  submitComplete() {
    const task = this.selectedTask();
    if (!task || !this.completeForm.completedBy) return;
    this.complianceService
      .completeScheduleTask(task.id, this.completeForm.completedBy, this.completeForm.notes)
      .subscribe({
        next: () => {
          this.showCompleteModal = false;
          this.loadTasks();
        },
      });
  }

  openAssign(task: ComplianceScheduleTask) {
    this.selectTask(task);
    this.assignForm = { userId: '' };
    this.userSearchQuery = '';
    if (this.users().length === 0) this.loadUsers();
    this.showAssignModal = true;
  }

  submitAssign() {
    const task = this.selectedTask();
    if (!task || !this.assignForm.userId) return;
    this.complianceService.assignScheduleTask(task.id, this.assignForm.userId).subscribe({
      next: () => {
        this.showAssignModal = false;
        this.loadTasks();
      },
    });
  }

  pauseTask(task: ComplianceScheduleTask) {
    this.complianceService.pauseScheduleTask(task.id).subscribe(() => this.loadTasks());
  }

  resumeTask(task: ComplianceScheduleTask) {
    this.complianceService.resumeScheduleTask(task.id).subscribe(() => this.loadTasks());
  }

  openCreate() {
    this.createForm = {
      taskName: '',
      description: '',
      framework: 'ALL',
      frequency: 'Monthly',
      category: 'Review',
      owner: '',
      nextDueDate: '',
      evidenceRequired: '',
      priority: 'Medium',
    };
    this.showCreateModal = true;
  }

  submitCreate() {
    if (!this.createForm.taskName || !this.createForm.owner || !this.createForm.nextDueDate) return;
    this.complianceService.createScheduleTask(this.createForm).subscribe({
      next: () => {
        this.showCreateModal = false;
        this.loadTasks();
      },
    });
  }

  getFrequencyLabel(freq: string): string {
    const labels: Record<string, string> = {
      Daily: 'Hàng ngày',
      Weekly: 'Hàng tuần',
      Monthly: 'Hàng tháng',
      Quarterly: 'Hàng quý',
      SemiAnnual: '6 tháng',
      Annual: 'Hàng năm',
    };
    return labels[freq] || freq;
  }

  getPriorityIcon(priority: string): string {
    const icons: Record<string, string> = { Critical: '🔴', High: '🟠', Medium: '🟡', Low: '🟢' };
    return icons[priority] || '⚪';
  }
}
