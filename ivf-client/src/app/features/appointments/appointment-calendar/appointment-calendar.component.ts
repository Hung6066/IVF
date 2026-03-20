import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AppointmentService } from '../../../core/services/appointment.service';
import { Appointment } from '../../../core/models/appointment.models';

interface CalendarDay {
  date: Date;
  isCurrentMonth: boolean;
  isToday: boolean;
  appointments: Appointment[];
}

@Component({
  selector: 'app-appointment-calendar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './appointment-calendar.component.html',
  styleUrls: ['./appointment-calendar.component.scss'],
})
export class AppointmentCalendarComponent implements OnInit {
  private service = inject(AppointmentService);

  loading = signal(true);
  appointments = signal<Appointment[]>([]);
  currentDate = new Date();
  selectedDay = signal<CalendarDay | null>(null);

  weeks = signal<CalendarDay[][]>([]);
  weekDays = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];

  ngOnInit() {
    this.buildCalendar();
    const start = new Date(this.currentDate.getFullYear(), this.currentDate.getMonth(), 1);
    const end = new Date(this.currentDate.getFullYear(), this.currentDate.getMonth() + 1, 0);
    this.service.getAppointments(start, end).subscribe({
      next: (appts) => {
        this.appointments.set(appts);
        this.buildCalendar();
        this.loading.set(false);
      },
      error: () => {
        this.buildCalendar();
        this.loading.set(false);
      },
    });
  }

  buildCalendar() {
    const year = this.currentDate.getFullYear();
    const month = this.currentDate.getMonth();
    const today = new Date();
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const days: CalendarDay[] = [];

    for (let i = 0; i < firstDay.getDay(); i++) {
      const d = new Date(year, month, -firstDay.getDay() + i + 1);
      days.push({ date: d, isCurrentMonth: false, isToday: false, appointments: [] });
    }
    for (let d = 1; d <= lastDay.getDate(); d++) {
      const date = new Date(year, month, d);
      const isToday = date.toDateString() === today.toDateString();
      const dayAppts = this.appointments().filter((a) => {
        const ad = new Date(a.scheduledAt);
        return ad.getFullYear() === year && ad.getMonth() === month && ad.getDate() === d;
      });
      days.push({ date, isCurrentMonth: true, isToday, appointments: dayAppts });
    }
    while (days.length % 7 !== 0) {
      const d = new Date(year, month + 1, days.length - lastDay.getDate() - firstDay.getDay() + 1);
      days.push({ date: d, isCurrentMonth: false, isToday: false, appointments: [] });
    }

    const w: CalendarDay[][] = [];
    for (let i = 0; i < days.length; i += 7) w.push(days.slice(i, i + 7));
    this.weeks.set(w);
  }

  prevMonth() {
    this.currentDate = new Date(this.currentDate.getFullYear(), this.currentDate.getMonth() - 1, 1);
    this.ngOnInit();
  }
  nextMonth() {
    this.currentDate = new Date(this.currentDate.getFullYear(), this.currentDate.getMonth() + 1, 1);
    this.ngOnInit();
  }

  get monthLabel(): string {
    return this.currentDate.toLocaleDateString('vi-VN', { month: 'long', year: 'numeric' });
  }

  selectDay(day: CalendarDay) {
    this.selectedDay.set(day);
  }

  getApptClass(status: string): string {
    switch (status) {
      case 'Scheduled':
        return 'bg-blue-100 text-blue-700';
      case 'Confirmed':
        return 'bg-green-100 text-green-700';
      case 'Completed':
        return 'bg-gray-100 text-gray-600';
      case 'Cancelled':
        return 'bg-red-100 text-red-600';
      default:
        return 'bg-gray-100 text-gray-600';
    }
  }
}
