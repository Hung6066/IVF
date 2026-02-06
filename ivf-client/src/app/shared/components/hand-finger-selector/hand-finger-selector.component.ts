import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FingerprintType } from '../../../core/services/patient-biometrics.service';

@Component({
    selector: 'app-hand-finger-selector',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="hand-selector-container">
      <div class="hands-wrapper">
        <!-- Left Hand -->
        <div class="hand left-hand">
          <svg viewBox="0 0 200 250" class="hand-svg">
            <!-- Palm -->
            <path class="palm" d="M60 120 C60 120 40 180 60 210 C80 240 150 240 170 210 C190 180 170 120 170 120 L60 120" />
            
            <!-- Fingers -->
            <!-- Little (Left) -->
            <g class="finger" [class.active]="selected() === 10" [class.enrolled]="isEnrolled(10)" (click)="selectFinger(10)">
               <rect x="20" y="80" width="30" height="70" rx="15" transform="rotate(-20 35 150)" />
               <text x="15" y="70" class="finger-label">Út</text>
            </g>
            <!-- Ring (Left) -->
            <g class="finger" [class.active]="selected() === 9" [class.enrolled]="isEnrolled(9)" (click)="selectFinger(9)">
               <rect x="60" y="40" width="32" height="90" rx="16" transform="rotate(-10 76 130)" />
               <text x="65" y="30" class="finger-label">Nhẫn</text>
            </g>
            <!-- Middle (Left) -->
            <g class="finger" [class.active]="selected() === 8" [class.enrolled]="isEnrolled(8)" (click)="selectFinger(8)">
               <rect x="100" y="20" width="34" height="100" rx="17" />
               <text x="105" y="15" class="finger-label">Giữa</text>
            </g>
            <!-- Index (Left) -->
            <g class="finger" [class.active]="selected() === 7" [class.enrolled]="isEnrolled(7)" (click)="selectFinger(7)">
               <rect x="140" y="40" width="32" height="90" rx="16" transform="rotate(10 156 130)" />
               <text x="145" y="30" class="finger-label">Trỏ</text>
            </g>
            <!-- Thumb (Left) -->
            <g class="finger thumb" [class.active]="selected() === 6" [class.enrolled]="isEnrolled(6)" (click)="selectFinger(6)">
               <ellipse cx="190" cy="150" rx="25" ry="35" transform="rotate(30 190 150)" />
               <text x="210" y="150" class="finger-label">Cái</text>
            </g>

            <text x="100" y="240" text-anchor="middle" class="hand-label">Trái</text>
          </svg>
        </div>

        <!-- Right Hand -->
        <div class="hand right-hand">
          <svg viewBox="0 0 200 250" class="hand-svg">
            <!-- Palm -->
            <path class="palm" d="M30 120 C30 120 10 180 30 210 C50 240 120 240 140 210 C160 180 140 120 140 120 L30 120" transform="scale(-1, 1) translate(-200, 0)" />

            <!-- Fingers (Mirrored Logic roughly) -->
             <!-- Thumb (Right) -->
             <g class="finger thumb" [class.active]="selected() === 1" [class.enrolled]="isEnrolled(1)" (click)="selectFinger(1)">
                <ellipse cx="10" cy="150" rx="25" ry="35" transform="rotate(-30 10 150)" />
                <text x="-20" y="150" class="finger-label">Cái</text>
             </g>
             <!-- Index (Right) -->
            <g class="finger" [class.active]="selected() === 2" [class.enrolled]="isEnrolled(2)" (click)="selectFinger(2)">
               <rect x="28" y="40" width="32" height="90" rx="16" transform="rotate(-10 44 130)" />
               <text x="33" y="30" class="finger-label">Trỏ</text>
            </g>
             <!-- Middle (Right) -->
            <g class="finger" [class.active]="selected() === 3" [class.enrolled]="isEnrolled(3)" (click)="selectFinger(3)">
               <rect x="66" y="20" width="34" height="100" rx="17" />
               <text x="71" y="15" class="finger-label">Giữa</text>
            </g>
             <!-- Ring (Right) -->
            <g class="finger" [class.active]="selected() === 4" [class.enrolled]="isEnrolled(4)" (click)="selectFinger(4)">
               <rect x="108" y="40" width="32" height="90" rx="16" transform="rotate(10 124 130)" />
               <text x="113" y="30" class="finger-label">Nhẫn</text>
            </g>
             <!-- Little (Right) -->
            <g class="finger" [class.active]="selected() === 5" [class.enrolled]="isEnrolled(5)" (click)="selectFinger(5)">
               <rect x="150" y="80" width="30" height="70" rx="15" transform="rotate(20 165 150)" />
               <text x="160" y="70" class="finger-label">Út</text>
            </g>

            <text x="100" y="240" text-anchor="middle" class="hand-label">Phải</text>
          </svg>
        </div>
      </div>
    </div>
  `,
    styles: [`
    .hand-selector-container {
      display: flex;
      justify-content: center;
      padding: 1rem;
      background: #f8f9fa;
      border-radius: 12px;
      margin-bottom: 1.5rem;
    }

    .hands-wrapper {
      display: flex;
      gap: 2rem;
      flex-wrap: wrap;
      justify-content: center;
    }

    .hand {
      width: 200px;
      height: 250px;
    }

    .hand-svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }

    .palm {
      fill: #e9ecef;
      stroke: #dee2e6;
      stroke-width: 2;
    }

    .finger {
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .finger rect,
    .finger ellipse {
      fill: #e9ecef;
      stroke: #ced4da;
      stroke-width: 2;
      transition: all 0.2s ease;
    }

    .finger:hover rect,
    .finger:hover ellipse {
      fill: #e3f2fd;
      stroke: #90caf9;
    }

    /* Active State (Selected) */
    .finger.active rect,
    .finger.active ellipse {
      fill: #2196f3;
      stroke: #1976d2;
      filter: drop-shadow(0 0 4px rgba(33, 150, 243, 0.4));
    }

    /* Enrolled State (Has Data) */
    .finger.enrolled rect,
    .finger.enrolled ellipse {
      fill: #4caf50;
      stroke: #388e3c;
    }

    /* Both Active and Enrolled */
    .finger.active.enrolled rect,
    .finger.active.enrolled ellipse {
      fill: #1976d2; /* Selection overrides color, but maybe indicate success too? */
      stroke: #4caf50;
      stroke-width: 3;
    }

    .finger-label {
      font-size: 12px;
      fill: #6c757d;
      pointer-events: none;
      opacity: 0;
      transition: opacity 0.2s;
    }

    .finger:hover .finger-label,
    .finger.active .finger-label {
      opacity: 1;
      font-weight: bold;
    }

    .hand-label {
      font-size: 16px;
      font-weight: 600;
      fill: #495057;
    }
  `]
})
export class HandFingerSelectorComponent {
    @Input() selected: any = signal(FingerprintType.RightIndex);
    @Input() enrolledFingers: any = signal([]);
    @Output() fingerSelected = new EventEmitter<FingerprintType>();

    selectFinger(type: number) {
        this.fingerSelected.emit(type as FingerprintType);
    }

    isEnrolled(type: number): boolean {
        const fingerType = type as FingerprintType;
        return this.enrolledFingers().includes(fingerType);
    }
}
