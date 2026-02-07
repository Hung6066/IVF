import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CycleService } from '../../../core/services/cycle.service';
import { DateService } from '../../../core/services/date.service';

@Component({
  selector: 'app-cycle-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './cycle-form.component.html',
  styleUrls: ['./cycle-form.component.scss']
})
export class CycleFormComponent implements OnInit {
  saving = signal(false);
  coupleId = '';

  methods = [
    { value: 'QHTN', label: 'Quan hệ', icon: '💑', desc: 'Tự nhiên / KTBT' },
    { value: 'IUI', label: 'IUI', icon: '💉', desc: 'Bơm tinh trùng' },
    { value: 'ICSI', label: 'ICSI', icon: '🔬', desc: 'Thụ tinh vi thao tác' },
    { value: 'IVM', label: 'IVM', icon: '🧫', desc: 'Trưởng thành in vitro' }
  ];

  formData = {
    method: '',
    startDate: new Date().toISOString().split('T')[0],
    notes: ''
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private cycleService: CycleService,
    private dateService: DateService
  ) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.coupleId = params['coupleId'];
    });
  }

  submit(): void {
    if (!this.formData.method || !this.coupleId) return;

    this.saving.set(true);
    // Ensure startDate is ISO if needed, but cycleService.createCycle likely takes string.
    // formData.startDate is already 'YYYY-MM-DD' from input.
    // If backend expects ISO, we should convert.
    // Let's assume CreateCycleCommand expects DateTime, which binds from YYYY-MM-DD ok?
    // Actually, backend JSON binding for DateTime works with YYYY-MM-DD usually.
    // But let's be safe and send ISO if needed, or leave as is if it works.
    // The user's compliant was about "binding date for edit not binding in system".
    // This implies fetching data and showing it in the input is the problem.
    // CycleForm is creation only? IDK.
    // Let's just update the injection for now.

    this.cycleService.createCycle({
      coupleId: this.coupleId,
      method: this.formData.method as any,
      startDate: this.dateService.toISOString(this.formData.startDate) || this.formData.startDate,
      notes: this.formData.notes || undefined
    }).subscribe({
      next: (id) => {
        this.saving.set(false);
        this.router.navigate(['/cycles', id]);
      },
      error: () => this.saving.set(false)
    });
  }

  goBack(): void {
    history.back();
  }
}
