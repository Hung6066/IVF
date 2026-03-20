import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-trigger-decision',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './trigger-decision.component.html',
  styleUrls: ['./trigger-decision.component.scss'],
})
export class TriggerDecisionComponent {
  @Input() saving = false;
  @Input() triggerGiven = false;
  @Output() submitTrigger = new EventEmitter<any>();
  @Output() submitDecision = new EventEmitter<{ decision: string; reason: string }>();
  @Output() cancel = new EventEmitter<void>();

  activePanel: 'trigger' | 'decision' = 'trigger';

  triggerForm = {
    triggerDrug: '',
    triggerDate: new Date().toISOString().slice(0, 10),
    triggerTime: '22:00',
    lhLab: null as number | null,
    e2Lab: null as number | null,
    p4Lab: null as number | null,
  };

  decision = 'Proceed';
  decisionReason = '';

  /** Planned OPU = trigger datetime + 36 hours */
  get plannedOpuDate(): string {
    const { triggerDate, triggerTime } = this.triggerForm;
    if (!triggerDate || !triggerTime) return '';
    const trigger = new Date(`${triggerDate}T${triggerTime}`);
    if (isNaN(trigger.getTime())) return '';
    const opu = new Date(trigger.getTime() + 36 * 60 * 60 * 1000);
    return opu.toLocaleString('vi-VN', {
      weekday: 'short',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  onSubmitTrigger() {
    this.submitTrigger.emit({
      ...this.triggerForm,
      triggerTime: this.triggerForm.triggerTime + ':00',
    });
  }

  onSubmitDecision() {
    this.submitDecision.emit({ decision: this.decision, reason: this.decisionReason });
  }
}
