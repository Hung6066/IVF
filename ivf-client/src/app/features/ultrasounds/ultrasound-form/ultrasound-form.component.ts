import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { UltrasoundService } from '../../../core/services/ultrasound.service';

@Component({
  selector: 'app-ultrasound-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './ultrasound-form.component.html',
  styleUrls: ['./ultrasound-form.component.scss']
})
export class UltrasoundFormComponent implements OnInit {
  saving = signal(false);
  cycleId = '';

  formData = {
    ultrasoundType: 'NangNoan',
    examDate: new Date().toISOString().split('T')[0],
    leftOvaryCount: null as number | null,
    rightOvaryCount: null as number | null,
    leftFollicles: '',
    rightFollicles: '',
    endometriumThickness: null as number | null,
    findings: ''
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private ultrasoundService: UltrasoundService
  ) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.cycleId = params['cycleId'];
    });
  }

  submit(): void {
    this.saving.set(true);
    this.ultrasoundService.createUltrasound({
      cycleId: this.cycleId,
      ...this.formData
    } as any).subscribe({
      next: () => {
        this.saving.set(false);
        this.goBack();
      },
      error: () => this.saving.set(false)
    });
  }

  goBack(): void {
    this.router.navigate(['/cycles', this.cycleId]);
  }
}
