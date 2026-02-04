import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { QueueItem } from '../lab-dashboard.models';

@Component({
    selector: 'app-lab-queue',
    standalone: true,
    imports: [CommonModule],
    template: `
    <section class="content-section card">
      <div class="section-header">
        <h2>Danh sách chờ ({{ queue.length }})</h2>
      </div>
      <div class="table-container">
        <table class="data-table">
          <thead>
            <tr>
              <th>STT</th>
              <th>Họ tên</th>
              <th>Mã BN</th>
              <th>Giờ lấy số</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            @for (q of queue; track q.id) {
              <tr>
                <td class="stt">{{ q.number }}</td>
                <td>{{ q.patientName }}</td>
                <td class="code">{{ q.patientCode }}</td>
                <td>{{ formatTime(q.issueTime) }}</td>
                <td>
                  @if (q.status === 'Waiting') { <span class="badge badge-warning">Đang chờ</span> }
                  @else if (q.status === 'Called') { <span class="badge badge-info">Đang gọi</span> }
                  @else if (q.status === 'InService') { <span class="badge badge-success">Đang làm</span> }
                  @else { <span class="badge badge-neutral">{{ q.status }}</span> }
                </td>
                <td>
                  <button class="btn btn-warning btn-sm" (click)="onCall(q)">📢 Gọi</button>
                  <button class="btn btn-primary btn-sm" (click)="onStart(q)">🧪 Thực hiện</button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty-state">Không có bệnh nhân chờ</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
    styles: [`
    .btn-sm {
      padding: 0.25rem 0.5rem;
      font-size: 0.75rem;
      margin-right: 0.5rem;
    }
    .empty-state {
        text-align: center;
        padding: 2rem;
        color: var(--text-light);
    }
  `]
})
export class LabQueueComponent {
    @Input() queue: QueueItem[] = [];
    @Output() callPatient = new EventEmitter<QueueItem>();
    @Output() startProcedure = new EventEmitter<QueueItem>();

    formatTime(date: string): string {
        return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    }

    onCall(q: QueueItem) {
        this.callPatient.emit(q);
    }

    onStart(q: QueueItem) {
        this.startProcedure.emit(q);
    }
}
