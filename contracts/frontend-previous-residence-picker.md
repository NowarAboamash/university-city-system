# دليل التطبيق: منتقي «السكن السابق» (المبنى / الطابق / الغرفة)

عند تقديم طلب تسكين، لو الطالب **ساكن سابق** (`isPreviousResident = true`) يلزمه تحديد مبناه/طابقه/غرفته السابقة. هذا الدليل يشرح نقطتَي البحث (lookup) اللتين تغذّيان المنتقي.

- **Base URL:** عبر البوابة — محليًا `http://localhost:5067`، إنتاج `http://gateway001.runasp.net`.
- **المصادقة:** `Authorization: Bearer <JWT>` — **أي مستخدم مصادَق** (لا يلزم دور أدمن).
- التعدادات تُرجَع كأرقام؛ التواريخ UTC ISO‑8601.

---

## 1. `GET /api/buildings/lookup` — قائمة المباني

قائمة مختصرة بكل المباني. لا معاملات.

**الرد** `200` — مصفوفة (بلا ترقيم صفحات):
```json
[
  { "id": 1,  "name": "1",  "floorsCount": 6 },
  { "id": 2,  "name": "2",  "floorsCount": 6 },
  { "id": 11, "name": "11", "floorsCount": 6 }
]
```

| الحقل | النوع | الاستخدام |
|---|---|---|
| `id` | int | يُرسَل لاحقًا كـ `previousBuildingId` |
| `name` | string | اسم المبنى للعرض (أرقام مثل «1».. «20» في بيانات جامعة حلب) |
| `floorsCount` | int \| **null** | عدد الطوابق — لبناء قائمة الطوابق `1..floorsCount`. لو `null` استخرج أقصى `floor` من نقطة الغرف أدناه |

> هذه النقطة تكشف فقط `id/name/floorsCount` — لا جنس المبنى ولا حالته ولا سعته (ذلك للأدمن عبر `GET /api/buildings`).

---

## 2. `GET /api/buildings/{buildingId}/rooms/lookup` — غرف مبنى

قائمة غرف المبنى المحدَّد، مختصرة، مرتّبة حسب الطابق ثم رقم الغرفة.

```
GET /api/buildings/1/rooms/lookup
```

**الرد** `200` — مصفوفة:
```json
[
  { "id": 45,  "floor": 1, "roomNumber": "101" },
  { "id": 46,  "floor": 1, "roomNumber": "102" },
  { "id": 89,  "floor": 2, "roomNumber": "201" }
]
```

| الحقل | النوع | الاستخدام |
|---|---|---|
| `id` | int | معرّف الغرفة الداخلي (اختياري الاستخدام) |
| `floor` | int | لفلترة الغرف على الطابق المختار |
| `roomNumber` | string | يُرسَل لاحقًا كـ `previousRoomNumber` (نصّي) |

**الردود الأخرى:** `404` لو `buildingId` غير موجود · `401` بلا توكن.

> لا حالة الغرفة ولا قائمة شاغليها — للحفاظ على الخصوصية (تلك في `GET /api/buildings/{id}/rooms` الإدارية).

---

## 3. تدفّق المنتقي في الواجهة

1. المستخدم يفعّل «ساكن سابق» → `isPreviousResident = true`.
2. **المبنى:** حمّل `GET /api/buildings/lookup` → Dropdown بالأسماء → عند الاختيار خزّن `id` كـ `previousBuildingId`.
3. **الطابق:** من `floorsCount` للمبنى المختار اعرض `1..N`؛ لو `null` حمّل غرف المبنى (خطوة 4) واستخرج `max(floor)`. القيمة المختارة → `previousFloor`.
4. **الغرفة:** حمّل `GET /api/buildings/{previousBuildingId}/rooms/lookup`، فلتر العناصر على `floor == previousFloor`، اعرض `roomNumber` → المختار → `previousRoomNumber`.

اقتراح: حمّل غرف المبنى مرّة واحدة بعد اختيار المبنى، واشتق منها الطوابق والغرف معًا (يغطّي حالة `floorsCount = null` مجانًا).

---

## 4. إرسال القيم مع الطلب

`POST /api/housing-requests` (multipart/form-data، مع الوثائق) — الحقول ذات الصلة:

| الحقل | النوع | ملاحظة |
|---|---|---|
| `isPreviousResident` | bool | لو `false` لا ترسل بقية الحقول |
| `previousBuildingId` | int | **إلزامي** عندما `isPreviousResident = true` (الباك إند يتحقق من وجود المبنى؛ خطأ ⇒ `400`) |
| `previousFloor` | int? | اختياري على مستوى الباك إند |
| `previousRoomNumber` | string? | اختياري على مستوى الباك إند، نصّي |

نفس الحقول في `PUT /api/housing-requests/mine/{id}` عند التعديل.

**تنبيه:** الباك إند حاليًا **لا يتحقق** أن `previousFloor`/`previousRoomNumber` يطابقان غرفة حقيقية — المنتقي هو ما يضمن صحّة الاختيار. (لو لزم تحقّق فعلي لاحقًا، يُضاف.)

---

## 5. ملاحظات

- الحقول الثلاثة موجودة أصلًا في `HousingRequestDto` (تُرجَع في `GET /api/housing-requests/mine/{id}` إلخ) — استخدمها لملء المنتقي مسبقًا عند تعديل طلب قائم.
- العقد الكامل: `contracts/housing-service.openapi.json` (`/api/buildings/lookup`، `/api/buildings/{buildingId}/rooms/lookup`، سكيمات `BuildingLookupDto` و`RoomLookupDto`).
- كلا النقطتين مضافتان حديثًا — تحتاج نشر HousingService المحدَّث ليعملا على الإنتاج.
