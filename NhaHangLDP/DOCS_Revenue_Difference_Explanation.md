# ?? Gi?i Thích S? Khác Bi?t Doanh Thu Gi?a Các Báo Cáo

## ?? T?i Sao Doanh Thu Khác Nhau?

### ?? **Trang "Th?ng Kê L?i Nhu?n Theo Món Ãn"**
```
Doanh thu = SUM(S? lý?ng món × Giá món ãn)
Ngu?n: OrderDetails.Quantity × OrderDetails.PriceAtTime
```

**Bao g?m:**
- ? Giá tr? món ãn ð? bán
- ? KHÔNG bao g?m VAT
- ? KHÔNG bao g?m phí d?ch v?
- ? KHÔNG bao g?m gi?m giá hóa ðõn

### ?? **Trang "Th?ng Kê Doanh Thu T?ng H?p"**
```
Doanh thu = SUM(T?ng ti?n hóa ðõn cu?i cùng)
Ngu?n: Bills.FinalAmount
```

**Bao g?m:**
- ? Giá tr? món ãn ð? bán
- ? VAT (thý?ng 10%)
- ? Phí d?ch v? (thý?ng 5-10%)
- ? Gi?m giá/khuy?n m?i
- ? Các kho?n ph? thu khác

---

## ?? Công Th?c Tính Toán

### **Hóa Ðõn M?u:**
```
Món ãn:           500,000 VNÐ
Phí d?ch v? 5%:    25,000 VNÐ
VAT 10%:           52,500 VNÐ (trên t?ng)
?????????????????????????????
T?ng thanh toán:   577,500 VNÐ
```

### **K?t Qu? Trong Báo Cáo:**
- **L?i Nhu?n Theo Món**: `500,000 VNÐ` (ch? giá món)
- **Doanh Thu T?ng**: `577,500 VNÐ` (toàn b? hóa ðõn)
- **Chênh l?ch**: `77,500 VNÐ` (phí + VAT)

---

## ?? M?c Ðích C?a T?ng Báo Cáo

### ?? **Báo Cáo L?i Nhu?n Theo Món**
**M?c ðích:** Phân tích hi?u qu? kinh doanh t?ng món ãn
- Tính l?i nhu?n = Giá bán - Chi phí nguyên li?u
- Xác ð?nh món ãn có t? l? l?i cao
- Ðýa ra quy?t ð?nh v? menu và giá c?

### ?? **Báo Cáo Doanh Thu T?ng**
**M?c ðích:** Theo d?i t?ng thu nh?p th?c t?
- Tính toán cashflow th?c t?
- Báo cáo thu? và tài chính
- Ðánh giá hi?u su?t kinh doanh t?ng th?

---

## ? K?t Lu?n

**C? hai báo cáo ð?u ðúng**, ch? khác **ph?m vi tính toán**:

1. **L?i nhu?n theo món** ? Phân tích **hi?u qu? s?n ph?m**
2. **Doanh thu t?ng** ? Theo d?i **tài chính t?ng th?**

**Công th?c ki?m tra:**
```
Doanh Thu T?ng = Doanh Thu Món Ãn + VAT + Phí D?ch V? + Các Kho?n Khác
```

---

## ?? Cách Ki?m Tra Trong H? Th?ng

1. **Truy c?p:** `/ReportsManagement/CompareRevenueReports`
2. **API Test:** POST v?i parameter `period=month`
3. **So sánh k?t qu?** t? các ngu?n d? li?u khác nhau
4. **Xác minh** công th?c tính toán

**Lýu ?:** N?u chênh l?ch quá l?n (>20%), c?n ki?m tra:
- Cài ð?t VAT và phí d?ch v?
- D? li?u OrderDetails vs Bills
- Tr?ng thái hóa ðõn (Paid/Unpaid)