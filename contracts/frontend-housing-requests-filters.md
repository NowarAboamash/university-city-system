# فلترة `GET /api/housing-requests` — منفَّذة

كل الفلاتر اختيارية، تُطبَّق على مستوى قاعدة البيانات (query + ترقيم صفحات)، وتُدمَج مع بعضها ومع الفلاتر القديمة بـ **AND**. **لا تغيير على شكل الـ response** (نفس `HousingRequestDto` و `PagedResult`).

أسماء الباراميترات **غير حسّاسة لحالة الأحرف** (`isPaid` = `IsPaid`).

---

## الفلاتر القديمة (بلا تغيير)

`housingCycleId` (int) · `governorateId` (int) · `status` (0=مُقدَّم، 1=يحتاج تعديل، 2=مقفول) · `admissionStatus` (0=قيد المراجعة، 1=مقبول، 2=قائمة انتظار، 3=مرفوض) · `pageNumber` · `pageSize`

## الفلاتر الجديدة

| الباراميتر | النوع | السلوك |
|---|---|---|
| `studentIds` | `string[]` — كرّر المفتاح: `?studentIds=a&studentIds=b` | يُبقي الطلبات التي `studentId` تبعها ضمن القائمة (SQL `IN`). قائمة فارغة/غير مُرسَلة ⇒ تُتجاهَل. |
| `academicLevel` | int `1..5` | مطابقة تامّة (`1` الأولى … `5` الخامسة) |
| `gender` | int `0..2` | مطابقة تامّة (`0` ذكر، `1` أنثى، `2` مختلط) |
| `isPaid` | bool | `true` = المدفوعة فقط، `false` = غير المدفوعة فقط |
| `hasSpecialNeeds` | bool | ذوو الاحتياجات الخاصة فقط / غيرهم فقط |
| `isPreviousResident` | bool | السكان السابقون فقط / غيرهم فقط |
| `isGrouped` | bool | `true` = ضمن غروب فقط، `false` = فردية فقط (بلا غروب) |
| `submittedFrom` | date-time (UTC) | `SubmittedAt >= القيمة` (شامل) |
| `submittedTo` | date-time (UTC) | `SubmittedAt <= القيمة` (شامل) |

### أمثلة

```
GET /api/housing-requests?studentIds=507f1f77bcf86cd799439011&studentIds=507f191e810c19729de860ea&pageNumber=1&pageSize=10
GET /api/housing-requests?academicLevel=3&gender=1&admissionStatus=1
GET /api/housing-requests?isPaid=false&admissionStatus=1                 # مقبولون ولم يدفعوا بعد
GET /api/housing-requests?hasSpecialNeeds=true&isGrouped=false
GET /api/housing-requests?submittedFrom=2026-09-01T00:00:00Z&submittedTo=2026-09-07T23:59:59Z
```

## ملاحظات

- البحث بالاسم: كما اقترحتم — الفرونت يجيب الـ IDs من `GET /api/admin/users?role=student&q=...` على auth-service ثم يمرّرها في `studentIds`. لا نداء جديد من housing-service لأي خدمة.
- الترتيب ثابت: الأحدث تقديمًا أولًا (`SubmittedAt` تنازليًا).
- `TotalCount` / `TotalPages` في الرد تعكس نتيجة الفلترة لا العدد الكلي.
- العقد الكامل محدَّث في `contracts/housing-service.openapi.json`.
