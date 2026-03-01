import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SecurityService } from '../../../core/services/security.service';
import { SessionInfo } from '../../../core/models/security.model';

interface UserSession {
  userId: string;
  username: string;
  sessions: SessionInfo[];
  expanded: boolean;
}

@Component({
  selector: 'app-security-sessions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './security-sessions.component.html',
  styleUrls: ['./security-sessions.component.scss'],
})
export class SecuritySessionsComponent implements OnInit {
  loading = signal(false);
  lookupUserId = '';
  userSessions = signal<UserSession[]>([]);
  statusMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  constructor(private securityService: SecurityService) {}

  ngOnInit() {}

  searchSessions() {
    const userId = this.lookupUserId.trim();
    if (!userId) return;

    this.loading.set(true);
    this.securityService.getActiveSessions(userId).subscribe({
      next: (sessions) => {
        const existing = this.userSessions().filter((u) => u.userId !== userId);
        this.userSessions.set([
          {
            userId,
            username: sessions[0]?.userId || userId,
            sessions,
            expanded: true,
          },
          ...existing,
        ]);
        this.loading.set(false);
        if (sessions.length === 0) {
          this.showStatus('Không tìm thấy phiên hoạt động', 'error');
        }
      },
      error: () => {
        this.showStatus('Không thể tải phiên hoạt động', 'error');
        this.loading.set(false);
      },
    });
  }

  revokeSession(userSession: UserSession, session: SessionInfo) {
    if (!confirm(`Thu hồi phiên ${session.sessionId.substring(0, 8)}...?`)) return;

    this.securityService.revokeSession(session.sessionId).subscribe({
      next: () => {
        userSession.sessions = userSession.sessions.filter(
          (s) => s.sessionId !== session.sessionId,
        );
        this.userSessions.update((list) => [...list]);
        this.showStatus('Đã thu hồi phiên', 'success');
      },
      error: () => this.showStatus('Lỗi thu hồi phiên', 'error'),
    });
  }

  revokeAllSessions(userSession: UserSession) {
    if (!confirm(`Thu hồi tất cả ${userSession.sessions.length} phiên của user này?`)) return;

    const revokes = userSession.sessions.map((s) =>
      this.securityService.revokeSession(s.sessionId).subscribe(),
    );
    userSession.sessions = [];
    this.userSessions.update((list) => [...list]);
    this.showStatus('Đã thu hồi tất cả phiên', 'success');
  }

  toggleExpand(userSession: UserSession) {
    userSession.expanded = !userSession.expanded;
  }

  removeUser(userId: string) {
    this.userSessions.update((list) => list.filter((u) => u.userId !== userId));
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  formatTimeAgo(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return 'Vừa xong';
    if (mins < 60) return `${mins} phút trước`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours} giờ trước`;
    return `${Math.floor(hours / 24)} ngày trước`;
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMessage.set({ text, type });
    setTimeout(() => this.statusMessage.set(null), 4000);
  }
}
