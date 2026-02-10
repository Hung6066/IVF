import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-form-actions-bar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './form-actions-bar.component.html',
  styleUrls: ['./form-actions-bar.component.scss'],
})
export class FormActionsBarComponent {
  @Input() isMultiPage = false;
  @Input() isFirstPage = true;
  @Input() isLastPage = true;
  @Input() isEditMode = false;
  @Input() isSubmitting = false;
  @Input() showReview = false;
  @Input() autoSaveEnabled = true;
  @Input() autoSaveStatus: 'idle' | 'saving' | 'saved' | 'error' = 'idle';
  @Input() lastAutoSavedAt: Date | null = null;

  @Output() cancelClicked = new EventEmitter<void>();
  @Output() saveDraftClicked = new EventEmitter<void>();
  @Output() prevPageClicked = new EventEmitter<void>();
  @Output() nextPageClicked = new EventEmitter<void>();
  @Output() toggleReviewClicked = new EventEmitter<void>();
  @Output() clearDraftClicked = new EventEmitter<void>();
  @Output() submitClicked = new EventEmitter<void>();
}
