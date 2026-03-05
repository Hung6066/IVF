---
description: "Use when writing or modifying Angular HTML templates. Enforces @if/@for control flow, Vietnamese labels, Tailwind/SCSS styling, modal patterns, and signal-based state access for the IVF Angular 21 project."
applyTo: "ivf-client/**/*.html"
---

# Angular Template Conventions

## Control Flow — Required Syntax

Use Angular's built-in control flow. NEVER use structural directives (`*ngIf`, `*ngFor`, `*ngSwitch`).

```html
<!-- Conditionals -->
@if (loading()) {
<div>Đang tải...</div>
} @else {
<div>Content</div>
}

<!-- Loops — always track by id -->
@for (item of items(); track item.id) {
<tr>
  {{ item.name }}
</tr>
} @empty {
<tr>
  <td colspan="6" class="empty-state">Không có dữ liệu</td>
</tr>
}

<!-- Switch -->
@switch (status) { @case ('Active') { <span>Hoạt động</span> } @case
('Inactive') { <span>Ngừng hoạt động</span> } @default {
<span>{{ status }}</span> } }
```

## Signals — Call to Read

Signals must be invoked in templates: `items()`, `loading()`, `page()`, `total()`.

```html
<!-- Correct -->
@if (loading()) { ... } @for (item of items(); track item.id) { ... }
<span>Trang {{ page() }}</span>

<!-- WRONG — missing parentheses -->
@if (loading) { ... }
```

## Vietnamese Labels

ALL user-facing text must be in Vietnamese. Common vocabulary:

| English       | Vietnamese         |
| ------------- | ------------------ |
| Search        | Tìm kiếm           |
| Add new       | Thêm mới           |
| Edit          | Chỉnh sửa / Sửa    |
| Delete        | Xoá                |
| Save          | Lưu                |
| Cancel        | Huỷ / Huỷ bỏ       |
| Update        | Cập nhật           |
| Detail / View | Chi tiết / Xem     |
| Loading...    | Đang tải...        |
| No data       | Không có dữ liệu   |
| Total         | Tổng               |
| Page          | Trang              |
| Previous      | Trước              |
| Next          | Sau                |
| Actions       | Thao tác           |
| Full name     | Họ tên / Họ và tên |
| Date of birth | Ngày sinh          |
| Gender        | Giới tính          |
| Male / Female | Nam / Nữ           |
| Phone         | SĐT                |
| Address       | Địa chỉ            |
| Status        | Trạng thái         |
| Patient       | Bệnh nhân          |
| Doctor        | Bác sĩ             |
| Patient code  | Mã BN              |
| Required      | Bắt buộc           |

## Page Layout

```html
<div class="p-6">
  <!-- Page header -->
  <header class="page-header">
    <div class="header-content">
      <h1>Danh sách {feature}</h1>
      <p>Quản lý {description}</p>
    </div>
    <div class="header-actions">
      <button class="btn btn-primary" (click)="showAddModal = true">
        ➕ Thêm mới
      </button>
    </div>
  </header>

  <!-- Search bar -->
  <div class="search-bar">
    <input
      type="text"
      class="form-control"
      [(ngModel)]="searchQuery"
      (input)="onSearch()"
      placeholder="🔍 Tìm kiếm..."
    />
  </div>

  <!-- Data table -->
  <div class="card table-container">
    <table class="data-table">
      ...
    </table>
  </div>

  <!-- Pagination -->
  <div class="pagination">
    <span>Tổng: {{ total() }}</span>
    <div class="page-controls">
      <button [disabled]="page() <= 1" (click)="changePage(page() - 1)">
        ← Trước
      </button>
      <span>Trang {{ page() }}</span>
      <button
        [disabled]="items().length < pageSize"
        (click)="changePage(page() + 1)"
      >
        Sau →
      </button>
    </div>
  </div>
</div>
```

## Modals

```html
@if (showModal) {
<div class="modal-overlay" (click)="showModal = false">
  <div class="modal-content" (click)="$event.stopPropagation()">
    <div class="modal-header">
      <h2>Tiêu đề</h2>
      <button class="close-btn" (click)="showModal = false">×</button>
    </div>
    <form (ngSubmit)="submit()">
      <div class="modal-body">
        <div class="form-grid">
          <div class="form-group">
            <label>Label <span class="required">*</span></label>
            <input
              class="form-control"
              [(ngModel)]="model.field"
              name="field"
              required
            />
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button
          type="button"
          class="btn btn-secondary"
          (click)="showModal = false"
        >
          Huỷ
        </button>
        <button type="submit" class="btn btn-primary">Lưu</button>
      </div>
    </form>
  </div>
</div>
}
```

- Click-outside dismiss: `(click)` on overlay + `$event.stopPropagation()` on content
- Forms use `(ngSubmit)` + `[(ngModel)]` with `name` attribute
- Required fields: `<span class="required">*</span>` after label

## Dashboard Stat Cards

```html
<div class="stats-grid">
  <div class="stat-card">
    <div class="stat-icon">📊</div>
    <div class="stat-content">
      <span class="value">{{ count() }}</span>
      <span class="label">Nhãn</span>
    </div>
  </div>
</div>
```

## Status Badges

```html
<span
  class="status-badge"
  [ngClass]="{
  'active': item.status === 'Active',
  'inactive': item.status === 'Inactive'
}"
>
  {{ item.status === 'Active' ? 'Hoạt động' : 'Ngừng HĐ' }}
</span>
```

## Icons

Use emoji inline (📊, ➕, ✏️, 👁️, 🔍, ⏳, ✅, ⚠️) or FontAwesome 7:

```html
<i class="fa-solid fa-plus"></i>
<i class="fa-solid fa-edit"></i>
<i class="fa-solid fa-trash"></i>
<i class="fa-solid fa-eye"></i>
```

## Styling

- Use CSS classes defined in component SCSS: `form-control`, `btn`, `btn-primary`, `card`, `data-table`
- Use Tailwind utilities for spacing/layout: `p-6`, `mb-4`, `flex`, `justify-between`
- Date display: `{{ formatDate(item.date) }}` — formatted with `vi-VN` locale in component
- Empty values: `{{ item.field || '-' }}`
