using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IVF.Infrastructure.Seeding;

public static class FormTemplateSeeder
{
    private const string LibraryCategoryName = "📚 Thư viện mẫu";

    public static async Task SeedFormTemplatesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        // Check if library category already exists
        var existing = await context.Set<FormCategory>()
            .AnyAsync(c => c.Name == LibraryCategoryName);
        if (existing)
        {
            Console.WriteLine("✓ Form template library already seeded");
            return;
        }

        Console.WriteLine("Seeding form template library...");

        // Create library category
        var category = FormCategory.Create(LibraryCategoryName, "Thư viện biểu mẫu IVF mẫu sẵn", "📚", 0);
        context.Set<FormCategory>().Add(category);
        await context.SaveChangesAsync();

        // === 1. Phiếu Khám Ban Đầu (Initial Consultation) ===
        var t1 = FormTemplate.Create(category.Id, "Phiếu khám ban đầu - Vô sinh", null, "Biểu mẫu khám lần đầu dành cho bệnh nhân vô sinh");
        t1.AddField("patient_name", "Họ và tên", FieldType.Text, 1, true, "Nhập họ tên bệnh nhân", validationRulesJson: "[{\"type\":\"minLength\",\"value\":\"2\"}]", layoutJson: "{\"colSpan\":2}");
        t1.AddField("dob", "Ngày sinh", FieldType.Date, 2, true, layoutJson: "{\"colSpan\":1}");
        t1.AddField("gender", "Giới tính", FieldType.Radio, 3, true, optionsJson: "[{\"value\":\"female\",\"label\":\"Nữ\"},{\"value\":\"male\",\"label\":\"Nam\"}]", layoutJson: "{\"colSpan\":1}");
        t1.AddField("phone", "Số điện thoại", FieldType.Text, 4, true, "0xxx xxx xxx", validationRulesJson: "[{\"type\":\"pattern\",\"value\":\"^0[0-9]{9}$\",\"message\":\"SĐT phải 10 số\"}]", layoutJson: "{\"colSpan\":2}");
        t1.AddField("address", "Địa chỉ", FieldType.Address, 5, false, optionsJson: "[{\"key\":\"street\",\"label\":\"Đường/Số nhà\",\"type\":\"text\",\"required\":true,\"width\":100},{\"key\":\"ward\",\"label\":\"Phường/Xã\",\"type\":\"text\",\"required\":false,\"width\":50},{\"key\":\"district\",\"label\":\"Quận/Huyện\",\"type\":\"text\",\"required\":false,\"width\":50},{\"key\":\"province\",\"label\":\"Tỉnh/TP\",\"type\":\"text\",\"required\":true,\"width\":50},{\"key\":\"country\",\"label\":\"Quốc gia\",\"type\":\"text\",\"required\":false,\"width\":50}]", layoutJson: "{\"colSpan\":4}");
        t1.AddField("section_history", "Tiền sử bệnh", FieldType.Section, 6);
        t1.AddField("marriage_years", "Số năm kết hôn", FieldType.Number, 7, true, "Năm", validationRulesJson: "[{\"type\":\"min\",\"value\":\"0\"},{\"type\":\"max\",\"value\":\"50\"}]", layoutJson: "{\"colSpan\":1}");
        t1.AddField("infertility_years", "Số năm hiếm muộn", FieldType.Number, 8, true, "Năm", layoutJson: "{\"colSpan\":1}");
        t1.AddField("infertility_type", "Loại vô sinh", FieldType.Radio, 9, true, optionsJson: "[{\"value\":\"primary\",\"label\":\"Vô sinh nguyên phát\"},{\"value\":\"secondary\",\"label\":\"Vô sinh thứ phát\"}]", layoutJson: "{\"colSpan\":2}");
        t1.AddField("previous_treatment", "Đã điều trị trước", FieldType.Checkbox, 10, false, optionsJson: "[{\"value\":\"iui\",\"label\":\"IUI\"},{\"value\":\"ivf\",\"label\":\"IVF\"},{\"value\":\"icsi\",\"label\":\"ICSI\"},{\"value\":\"none\",\"label\":\"Chưa điều trị\"}]", layoutJson: "{\"colSpan\":2}");
        t1.AddField("previous_cycles", "Số chu kỳ đã thực hiện", FieldType.Number, 11, false, layoutJson: "{\"colSpan\":1}");
        t1.AddField("medical_history", "Tiền sử bệnh lý", FieldType.TextArea, 12, false, "Ghi chú bệnh lý, phẫu thuật, dị ứng...", layoutJson: "{\"colSpan\":4}");
        t1.AddField("doctor_notes", "Ghi chú bác sĩ", FieldType.TextArea, 13, false, "Nhận xét, đề xuất phác đồ...", layoutJson: "{\"colSpan\":4}");
        t1.Publish();
        context.Set<FormTemplate>().Add(t1);

        // === 2. Phiếu Xét Nghiệm Tinh Dịch (Semen Analysis) ===
        var t2 = FormTemplate.Create(category.Id, "Phiếu xét nghiệm tinh dịch đồ", null, "Biểu mẫu kết quả xét nghiệm tinh dịch theo chuẩn WHO 2021");
        t2.AddField("collection_date", "Ngày lấy mẫu", FieldType.DateTime, 1, true, layoutJson: "{\"colSpan\":2}");
        t2.AddField("abstinence_days", "Ngày kiêng", FieldType.Number, 2, true, "Ngày", validationRulesJson: "[{\"type\":\"min\",\"value\":\"0\"},{\"type\":\"max\",\"value\":\"14\"}]", layoutJson: "{\"colSpan\":1}");
        t2.AddField("collection_method", "Phương pháp lấy", FieldType.Dropdown, 3, true, optionsJson: "[{\"value\":\"masturbation\",\"label\":\"Thủ dâm\"},{\"value\":\"coitus_interruptus\",\"label\":\"Giao hợp gián đoạn\"},{\"value\":\"condom\",\"label\":\"Bao cao su\"}]", layoutJson: "{\"colSpan\":1}");
        t2.AddField("section_macro", "Đại thể", FieldType.Section, 4);
        t2.AddField("volume", "Thể tích (ml)", FieldType.Decimal, 5, true, "ml", validationRulesJson: "[{\"type\":\"min\",\"value\":\"0\"},{\"type\":\"max\",\"value\":\"20\"}]", layoutJson: "{\"colSpan\":1}");
        t2.AddField("ph", "pH", FieldType.Decimal, 6, true, layoutJson: "{\"colSpan\":1}");
        t2.AddField("appearance", "Màu sắc", FieldType.Dropdown, 7, true, optionsJson: "[{\"value\":\"normal\",\"label\":\"Trắng đục\"},{\"value\":\"yellow\",\"label\":\"Vàng\"},{\"value\":\"bloody\",\"label\":\"Có máu\"},{\"value\":\"clear\",\"label\":\"Trong\"}]", layoutJson: "{\"colSpan\":1}");
        t2.AddField("liquefaction", "Thời gian hóa lỏng (phút)", FieldType.Number, 8, true, "phút", layoutJson: "{\"colSpan\":1}");
        t2.AddField("section_micro", "Vi thể", FieldType.Section, 9);
        t2.AddField("concentration", "Mật độ (triệu/ml)", FieldType.Decimal, 10, true, layoutJson: "{\"colSpan\":1}");
        t2.AddField("total_count", "Tổng số tinh trùng (triệu)", FieldType.Decimal, 11, true, layoutJson: "{\"colSpan\":1}");
        t2.AddField("motility_pr", "Di động tiến tới - PR (%)", FieldType.Decimal, 12, true, "%", layoutJson: "{\"colSpan\":1}");
        t2.AddField("motility_np", "Di động tại chỗ - NP (%)", FieldType.Decimal, 13, false, "%", layoutJson: "{\"colSpan\":1}");
        t2.AddField("immotile", "Bất động - IM (%)", FieldType.Decimal, 14, false, "%", layoutJson: "{\"colSpan\":1}");
        t2.AddField("morphology_normal", "Hình dạng bình thường (%)", FieldType.Decimal, 15, true, "%", layoutJson: "{\"colSpan\":1}");
        t2.AddField("section_other", "Các chỉ số khác", FieldType.Section, 16);
        t2.AddField("wbc", "Bạch cầu (triệu/ml)", FieldType.Decimal, 17, false, layoutJson: "{\"colSpan\":1}");
        t2.AddField("vitality", "Sống (%)", FieldType.Decimal, 18, false, "%", layoutJson: "{\"colSpan\":1}");
        t2.AddField("diagnosis", "Chẩn đoán", FieldType.Dropdown, 19, true, optionsJson: "[{\"value\":\"normal\",\"label\":\"Bình thường\"},{\"value\":\"oligospermia\",\"label\":\"Thiểu tinh\"},{\"value\":\"asthenospermia\",\"label\":\"Yếu tinh\"},{\"value\":\"teratospermia\",\"label\":\"Dị dạng tinh\"},{\"value\":\"azoospermia\",\"label\":\"Vô tinh\"},{\"value\":\"oat\",\"label\":\"OAT (Thiểu-yếu-dị dạng)\"}]", layoutJson: "{\"colSpan\":2}");
        t2.AddField("notes", "Ghi chú", FieldType.TextArea, 20, false, layoutJson: "{\"colSpan\":4}");
        t2.Publish();
        context.Set<FormTemplate>().Add(t2);

        // === 3. Phiếu Siêu Âm Nang Noãn (Follicle Monitoring) ===
        var t3 = FormTemplate.Create(category.Id, "Phiếu siêu âm theo dõi nang noãn", null, "Biểu mẫu ghi nhận kết quả siêu âm nang noãn trong chu kỳ kích thích buồng trứng");
        t3.AddField("exam_date", "Ngày siêu âm", FieldType.DateTime, 1, true, layoutJson: "{\"colSpan\":2}");
        t3.AddField("cycle_day", "Ngày chu kỳ", FieldType.Number, 2, true, layoutJson: "{\"colSpan\":1}");
        t3.AddField("stim_day", "Ngày kích thích", FieldType.Number, 3, false, layoutJson: "{\"colSpan\":1}");
        t3.AddField("section_right", "Buồng trứng phải", FieldType.Section, 4);
        t3.AddField("right_count", "Số nang phải", FieldType.Number, 5, true, layoutJson: "{\"colSpan\":1}");
        t3.AddField("right_sizes", "Kích thước nang phải (mm)", FieldType.Text, 6, false, "VD: 18, 16, 14, 12", layoutJson: "{\"colSpan\":3}");
        t3.AddField("section_left", "Buồng trứng trái", FieldType.Section, 7);
        t3.AddField("left_count", "Số nang trái", FieldType.Number, 8, true, layoutJson: "{\"colSpan\":1}");
        t3.AddField("left_sizes", "Kích thước nang trái (mm)", FieldType.Text, 9, false, "VD: 15, 14, 12", layoutJson: "{\"colSpan\":3}");
        t3.AddField("section_uterus", "Tử cung", FieldType.Section, 10);
        t3.AddField("endometrium", "Độ dày NMTC (mm)", FieldType.Decimal, 11, true, "mm", layoutJson: "{\"colSpan\":1}");
        t3.AddField("endo_pattern", "Dạng NMTC", FieldType.Radio, 12, false, optionsJson: "[{\"value\":\"trilinear\",\"label\":\"3 lá\"},{\"value\":\"homogeneous\",\"label\":\"Đồng nhất\"},{\"value\":\"heterogeneous\",\"label\":\"Không đồng nhất\"}]", layoutJson: "{\"colSpan\":2}");
        t3.AddField("doctor_comment", "Nhận xét", FieldType.TextArea, 13, false, "Nhận xét của bác sĩ siêu âm...", layoutJson: "{\"colSpan\":4}");
        t3.AddField("next_appointment", "Hẹn siêu âm lại", FieldType.Date, 14, false, layoutJson: "{\"colSpan\":2}");
        t3.Publish();
        context.Set<FormTemplate>().Add(t3);

        // === 4. Phiếu Đồng Ý IVF (IVF Consent Form) ===
        var t4 = FormTemplate.Create(category.Id, "Phiếu đồng ý thực hiện IVF/ICSI", null, "Biểu mẫu cam kết đồng ý của bệnh nhân khi thực hiện IVF/ICSI");
        t4.AddField("page1", "Trang 1/2", FieldType.PageBreak, 0);
        t4.AddField("patient_wife", "Họ tên vợ", FieldType.Text, 1, true, layoutJson: "{\"colSpan\":2}");
        t4.AddField("patient_husband", "Họ tên chồng", FieldType.Text, 2, true, layoutJson: "{\"colSpan\":2}");
        t4.AddField("wife_dob", "Ngày sinh vợ", FieldType.Date, 3, true, layoutJson: "{\"colSpan\":1}");
        t4.AddField("husband_dob", "Ngày sinh chồng", FieldType.Date, 4, true, layoutJson: "{\"colSpan\":1}");
        t4.AddField("wife_id", "CCCD vợ", FieldType.Text, 5, true, layoutJson: "{\"colSpan\":1}");
        t4.AddField("husband_id", "CCCD chồng", FieldType.Text, 6, true, layoutJson: "{\"colSpan\":1}");
        t4.AddField("address_couple", "Địa chỉ", FieldType.Address, 7, true, optionsJson: "[{\"key\":\"street\",\"label\":\"Đường/Số nhà\",\"type\":\"text\",\"required\":true,\"width\":100},{\"key\":\"ward\",\"label\":\"Phường/Xã\",\"type\":\"text\",\"required\":false,\"width\":50},{\"key\":\"district\",\"label\":\"Quận/Huyện\",\"type\":\"text\",\"required\":false,\"width\":50},{\"key\":\"province\",\"label\":\"Tỉnh/TP\",\"type\":\"text\",\"required\":true,\"width\":50}]", layoutJson: "{\"colSpan\":4}");
        t4.AddField("page2", "Trang 2/2", FieldType.PageBreak, 8);
        t4.AddField("section_consent", "Nội dung đồng ý", FieldType.Section, 9);
        t4.AddField("label_info", "Chúng tôi đã được bác sĩ giải thích đầy đủ về quy trình IVF/ICSI bao gồm: kích thích buồng trứng, chọc hút noãn, thụ tinh, nuôi cấy phôi và chuyển phôi.", FieldType.Label, 10);
        t4.AddField("consent_procedure", "Đồng ý thực hiện IVF/ICSI", FieldType.Checkbox, 11, true, "Tôi đồng ý thực hiện kỹ thuật IVF/ICSI", layoutJson: "{\"colSpan\":4}");
        t4.AddField("consent_risks", "Đã hiểu rủi ro", FieldType.Checkbox, 12, true, "Tôi đã được giải thích và hiểu rõ các rủi ro có thể xảy ra", layoutJson: "{\"colSpan\":4}");
        t4.AddField("consent_freeze", "Đồng ý đông phôi", FieldType.Checkbox, 13, false, "Tôi đồng ý đông phôi dư (nếu có)", layoutJson: "{\"colSpan\":4}");
        t4.AddField("treatment_method", "Phương pháp", FieldType.Radio, 14, true, optionsJson: "[{\"value\":\"ivf\",\"label\":\"IVF\"},{\"value\":\"icsi\",\"label\":\"ICSI\"},{\"value\":\"ivf_icsi\",\"label\":\"IVF + ICSI\"}]", layoutJson: "{\"colSpan\":2}");
        t4.AddField("consent_date", "Ngày ký", FieldType.Date, 15, true, layoutJson: "{\"colSpan\":2}");
        t4.Publish();
        context.Set<FormTemplate>().Add(t4);

        // === 5. Phiếu Chọc Hút Noãn (Egg Retrieval Report) ===
        var t5 = FormTemplate.Create(category.Id, "Phiếu chọc hút noãn", null, "Biểu mẫu ghi nhận kết quả chọc hút noãn");
        t5.AddField("procedure_date", "Ngày thực hiện", FieldType.DateTime, 1, true, layoutJson: "{\"colSpan\":2}");
        t5.AddField("anesthesia", "Phương pháp vô cảm", FieldType.Dropdown, 2, true, optionsJson: "[{\"value\":\"general\",\"label\":\"Gây mê toàn thân\"},{\"value\":\"sedation\",\"label\":\"An thần\"},{\"value\":\"local\",\"label\":\"Gây tê tại chỗ\"}]", layoutJson: "{\"colSpan\":2}");
        t5.AddField("section_results", "Kết quả", FieldType.Section, 3);
        t5.AddField("right_follicles", "Nang phải chọc", FieldType.Number, 4, true, layoutJson: "{\"colSpan\":1}");
        t5.AddField("left_follicles", "Nang trái chọc", FieldType.Number, 5, true, layoutJson: "{\"colSpan\":1}");
        t5.AddField("total_oocytes", "Tổng noãn thu được", FieldType.Number, 6, true, layoutJson: "{\"colSpan\":1}");
        t5.AddField("mature_oocytes", "Noãn trưởng thành (MII)", FieldType.Number, 7, true, layoutJson: "{\"colSpan\":1}");
        t5.AddField("immature_oocytes", "Noãn chưa trưởng thành", FieldType.Number, 8, false, layoutJson: "{\"colSpan\":1}");
        t5.AddField("degenerated", "Noãn thoái hóa", FieldType.Number, 9, false, layoutJson: "{\"colSpan\":1}");
        t5.AddField("complications", "Biến chứng", FieldType.Dropdown, 10, true, optionsJson: "[{\"value\":\"none\",\"label\":\"Không\"},{\"value\":\"bleeding\",\"label\":\"Chảy máu\"},{\"value\":\"infection\",\"label\":\"Nhiễm trùng\"},{\"value\":\"ohss\",\"label\":\"Quá kích buồng trứng\"},{\"value\":\"other\",\"label\":\"Khác\"}]", layoutJson: "{\"colSpan\":2}");
        t5.AddField("notes", "Ghi chú", FieldType.TextArea, 11, false, layoutJson: "{\"colSpan\":4}");
        t5.Publish();
        context.Set<FormTemplate>().Add(t5);

        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {5} form templates in '{LibraryCategoryName}' category");
    }
}
