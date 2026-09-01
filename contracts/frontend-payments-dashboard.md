# دليل التطبيق: لوحة المدفوعات

الشاشة تجمع رقمين من مصدرين:

| المصدر | يجاوب على | النقطة |
|---|---|---|
| **housing-service** | كم المطلوب كلياً · كم طلب مدفوع/غير مدفوع | `GET /api/housing-requests/payment-summary` |
| **auth-service** | حركات الأموال الفعلية عبر المحفظة (مين دفع، كم، متى) | `GET /api/admin/wallet/transactions?type=payment&…` |

- Base URL عبر البوابة: إنتاج `http://gateway001.runasp.net`.
- المصادقة: `Authorization: Bearer <JWT>` — **دور `admin` / `super_admin`**.
- المبالغ `decimal(18,2)`؛ التواريخ UTC ISO‑8601.

---

## 1. `GET /api/housing-requests/payment-summary` — الإجماليات (housing-service)

### باراميترات (كلها اختيارية)

| الباراميتر | الأثر |
|---|---|
| `housingCycleId` | يقيّد **كل** الأرقام على دورة سكن واحدة |
| `paidFrom`, `paidTo` (date-time) | يقيّدان **فقط** `paidInRange` / `countPaidInRange` (على `paidAt`) — للمطابقة مع دفتر auth لفترة. باقي الحقول تبقى لقطة «الآن». |

### الرد `200`

```json
{
  "feeAmount": 25.00,
  "totalRequired": 500.00,
  "totalPaid": 300.00,
  "totalOutstanding": 200.00,
  "countAccepted": 20,
  "countPaid": 12,
  "countUnpaid": 8,
  "paidInRange": 300.00,
  "countPaidInRange": 12,
  "asOf": "2026-09-01T20:15:00Z"
}
```

| الحقل | المعنى |
|---|---|
| `feeAmount` | الرسم المُعدّ حالياً (للعرض فقط — الطلبات القديمة قد تكون بقيمة مختلفة) |
| `totalRequired` | مجموع الرسم عبر **كل طلب مقبول** (مدفوع + غير مدفوع). محسوب من الرسم **المجمَّد على كل طلب وقت قبوله**، فتغيير الرسم لاحقاً لا يغيّره بأثر رجعي |
| `totalPaid` | المُحصَّل فعلياً عبر كل الوقت (من `amountPaid` الحقيقي لكل طلب) |
| `totalOutstanding` | `totalRequired − totalPaid` |
| `countAccepted` | عدد الطلبات المقبولة (اللي تدين برسم) |
| `countPaid` | المقبولة والمدفوعة |
| `countUnpaid` | `countAccepted − countPaid` |
| `paidInRange` / `countPaidInRange` | المُحصَّل/العدد ضمن `[paidFrom, paidTo]` — يساوي `totalPaid`/`countPaid` لو ما أرسلت مدى |

**للاستخدام:**
- بطاقة «المطلوب / المدفوع / المتبقّي / عدد الطلبات» → استخدم الحقول الأساسية بلا مدى تاريخ (اختيارياً `?housingCycleId=` لعرض دورة).
- تسوية مالية لفترة (شهر مثلاً) → مرّر نفس `paidFrom/paidTo` هنا و`dateFrom/dateTo` لنقطة auth، وقارن `paidInRange` مع `totalAmount` عند auth.

---

## 2. `GET /api/admin/wallet/transactions` — سجل الحركات (auth-service)

*(تبنيها/توثّقها جهة auth-service — الشكل حسب اتفاقهم؛ متوقَّع: قائمة `{ userId, amount, reference, description, createdAt }` + `totalAmount` + `count` لنفس الفلتر، مع `?type=payment&dateFrom=&dateTo=&page=&limit=`.)*

- `reference` بصيغة **`housing-request-{id}`** — به تربط سطر الدفتر بطلب سكن محدَّد (وبالتالي بالطالب في housing-service عبر `GET /api/housing-requests/{id}`).

---

## 3. منطق التسوية (Reconciliation)

لفترة زمنية واحدة، بعد جلب الطرفين بنفس المدى:

```
housing.paidInRange  ==  auth.totalAmount        ✅ متطابق
housing.countPaidInRange  ==  auth.count
```

**لو اختلفا** → مؤشّر خلل، اعرض تنبيهاً. الأسباب المحتملة:
- خصم نجح على المحفظة (200) لكن housing-service تعطّل قبل حفظ `isPaid` → الحركة في دفتر auth ولا يقابلها طلب مدفوع عند housing (`auth > housing`).
- فرق في مدى التاريخ المُرسَل للطرفين (تأكّد أنه **نفسه بالضبط**، ونفس المنطقة الزمنية UTC).

> ملاحظة: إعادة نداء الدفع بنفس `reference` (idempotent) تُرجع 200 بلا سطر دفتر جديد وhousing يعيد الضبط بلا أثر — لا تُحدث اختلافاً.

---

## 4. حقول جديدة على `HousingRequestDto` (لعرض تفاصيل طلب)

| الحقل | المعنى |
|---|---|
| `feeAmount` | الرسم المجمَّد على الطلب (null قبل القبول) |
| `amountPaid` | المبلغ المخصوم فعلاً عند الدفع (null قبل الدفع) |
| `isPaid`, `paidAt`, `paymentDueDate` | كما في `frontend-dashboard-and-payment.md` |

---

## 5. ملاحظات

- كل النقاط تحتاج نشر housing-service المحدَّث (عمود `feeAmount`/`amountPaid` + migration) لتعمل على الإنتاج.
- العقد الكامل: `contracts/housing-service.openapi.json` (`/api/housing-requests/payment-summary`, `PaymentSummaryDto`).
