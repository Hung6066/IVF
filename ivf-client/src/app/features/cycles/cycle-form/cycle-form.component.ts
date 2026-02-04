import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

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
    private api: ApiService
  ) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.coupleId = params['coupleId'];
    });
  }

  submit(): void {
    if (!this.formData.method || !this.coupleId) return;

    this.saving.set(true);
    this.api.createCycle({
      coupleId: this.coupleId,
      method: this.formData.method,
      notes: this.formData.notes || undefined
    }).subscribe({
      next: (cycle) => {
        this.saving.set(false);
        this.router.navigate(['/cycles', cycle.id]);
      },
      error: () => this.saving.set(false)
    });
  }

  goBack(): void {
    history.back();
  }
}
