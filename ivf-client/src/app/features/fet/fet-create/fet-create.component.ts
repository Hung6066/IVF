import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { FetService } from '../../../core/services/fet.service';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';

@Component({
  selector: 'app-fet-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, CycleSearchComponent],
  templateUrl: './fet-create.component.html',
  styleUrls: ['./fet-create.component.scss'],
})
export class FetCreateComponent {
  private service = inject(FetService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');

  form = {
    cycleId: '',
    prepType: 'Natural',
    startDate: '',
    cycleDay: 2,
    notes: '',
  };

  prepTypes = ['Natural', 'Artificial', 'Modified Natural'];

  submit() {
    if (!this.form.cycleId.trim()) {
      this.error.set('Vui lòng nhập ID chu kỳ');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.service
      .create({
        cycleId: this.form.cycleId,
        prepType: this.form.prepType,
        startDate: this.form.startDate || undefined,
        cycleDay: this.form.cycleDay,
        notes: this.form.notes || undefined,
      })
      .subscribe({
        next: (result) => this.router.navigate(['/fet', result.id]),
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi tạo FET protocol');
          this.saving.set(false);
        },
      });
  }
}
