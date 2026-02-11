Phân tích & Kế hoạch: Report Designer kiểu DevExpress
So sánh hiện trạng vs DevExpress Report Designer
Từ screenshot, DevExpress Report Designer có các tính năng chính:

Feature	DevExpress	IVF hiện tại	Gap
Band-based layout (PageHeader, Detail, PageFooter, GroupHeader/Footer)	✅	❌ Chỉ có 5 report types cố định	Lớn
Drag & Drop controls (Label, RichText, Table, Image, Shape, Chart, Barcode, etc.)	✅ 20+ controls	❌ Không có designer	Lớn
Data binding expressions ([Data.tenhs], Upper(), Iif())	✅	❌ Chỉ render cả form fields	Lớn
Properties panel (Data Source, Filter, Appearance, Behavior, Design)	✅	❌ Không có	Lớn
DESIGN / PREVIEW toggle	✅	❌ Chỉ có viewer	Trung bình
Multiple report bands	✅	❌	Lớn
Export PDF	✅	✅ QuestPDF	OK
Page Settings (margins, orientation, paper size)	✅	❌ Fixed A4 Landscape	Nhỏ
Expression editor (functions, operators, data fields)	✅	❌	Lớn
Multi-tab reports ("Bìa hồ sơ" tab)	✅	❌	Trung bình
Kiến trúc đề xuất
Phase Plan
Phase 1: Report Configuration Editor (MVP - 2 tuần)
Mở rộng configurationJson để chọn columns, filters, sorting, grouping:

Frontend: Thêm configuration editor UI vào report-builder (chọn cột, page settings, header/footer)
Backend: Update GenerateReportQuery để đọc configurationJson và filter/sort/group data

Phase 2: Visual Band Designer (3-4 tuần)
Report layout designer với band system:

Frontend Components:

report-designer.component.ts — Main 3-panel layout (toolbox + canvas + properties)
design-canvas.component.ts — Band renderer with drag-drop controls (CDK DragDrop)
report-toolbox.component.ts — Control palette
properties-panel.component.ts — Selected control properties editor
expression-editor.component.ts — Data binding expression builder
report-preview.component.ts — Live preview render
Backend:

Update ReportPdfService.cs to render from ReportDesign JSON (interpret bands + controls + expressions)
Expression evaluator: parse Iif(), Upper(), Substring(), Format(), field references [Data.fieldKey]
Phase 3: Advanced Features (2-3 tuần)
Sub-reports (embed report inside band)
Cross-tab / Pivot tables
Multi-page reports (tabs)
Conditional formatting (if value > X → red)
Parameters panel (runtime user input)
Template library (pre-built IVF report templates)