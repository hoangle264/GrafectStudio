# GrafcetStudio — Thư Thư Viện Tài Liệu Kỹ Thuật (Technical Docs)

Thư mục `docs/` chứa các tài liệu kỹ thuật chi tiết, hướng dẫn tích hợp và đặc tả kiến trúc cho dự án **GrafcetStudio**.

---

## 🗂️ Danh Mục Tài Liệu Theo Chủ Đề

### 1. Siemens TIA Portal & SimaticML Integration
Các tài liệu hướng dẫn sinh mã Ladder Logic (LAD) và tích hợp với phần mềm Siemens TIA Portal:

- 📘 [SIEMENS_LAD_DSL_REFERENCE.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/SIEMENS_LAD_DSL_REFERENCE.md)  
  *Đặc tả cú pháp Siemens LAD JSON DSL, cấu trúc template `.lad.json` và bảng tra cứu các block/networks.*
- ⚙️ [SIEMENS_TIA_OPENNESS_SETUP.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/SIEMENS_TIA_OPENNESS_SETUP.md)  
  *Hướng dẫn cấu hình môi trường TIA Portal Openness API, cấp quyền Windows User Group và biến môi trường bridge.*
- 📥 [SIEMENS_TIA_OPENNESS_IMPORT.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/SIEMENS_TIA_OPENNESS_IMPORT.md)  
  *Quy trình xuất SimaticML XML từ GrafcetStudio và import/compile trực tiếp vào TIA Portal project.*
- 🚀 [SIEMENS_TIA_V19_QUICK_SETUP.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/SIEMENS_TIA_V19_QUICK_SETUP.md)  
  *Hướng dẫn thiết lập nhanh cho phiên bản Siemens TIA Portal V19.*
- ✅ [SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/SIEMENS_TIA_LAD_MANUAL_TEST_CHECKLIST.md)  
  *Danh sách kiểm thử thủ công (Checklist) cho tính năng sinh mã Siemens LAD.*

---

### 2. AI Service Integration (Gemini & Claude)
- 🤖 [Plan_AI.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/Plan_AI.md)  
  *Kế hoạch & đặc tả tích hợp AI API Client: Bảo mật API Key, Sanitization layer, Context Builder, proposal dry-run và streaming response.*

---

### 3. Kiến Trúc & Phát Triển Frontend (Web & TypeScript)
- 📐 [P10-architecture-review.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/docs/P10-architecture-review.md)  
  *Báo cáo đánh giá kiến trúc `src/web`, phân tích độ phụ thuộc module, kích thước file và định hướng refactor.*
- 💻 [src/web/instruction.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/src/web/instruction.md)  
  *Cẩm nang lập trình viên Frontend: Mô hình dữ liệu `project`, quy tắc Sync Variable Table, CSV import pipeline và Handlebars context rendering.*

---

### 4. Báo Cáo Kiểm Thử & Tri Thức RAG
- 🧪 [tests/robot_stability_results.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/tests/robot_stability_results.md)  
  *Báo cáo kết quả kiểm thử độ ổn định (Stability Test) cho bộ sinh mã ABB RAPID Robot.*
- 📦 [assets/rag/abb/](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/assets/rag/abb/)  
  *Thư mục chứa tri thức RAG (Patterns & Rules) hỗ trợ AI sinh lập trình Robot ABB.*

---

## 🔗 Liên Kết Nhanh
- 🏠 Quay lại trang chính: [README.md (Project Root)](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/README.md)
- 🏛️ Xem kiến trúc tổng quan: [ARCHITECTURE.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/ARCHITECTURE.md)
- 📋 Danh sách Nợ Kỹ Thuật: [TECH_DEBT.md](file:///c:/Users/NITRO%205/source/vscode/GrafectStudio/TECH_DEBT.md)
