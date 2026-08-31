# للفرونت‑إند: لوحة الإدارة + دفع رسوم السكن (نقاط النهاية الجديدة)

كل المسارات عبر البوابة (Gateway): محليًا `http://localhost:5067`، إنتاج `http://gateway001.runasp.net`.
كل النقاط تتطلّب `Authorization: Bearer <JWT>`. التعدادات (enums) تُرجَع **كأرقام** (انظر §4).

---

## 0. حالة الجاهزية

| العنصر | الحالة |
|---|---|
| الكود (HousingService + FeedbackService + Gateway) | ✅ مكتمل، مبني، مختبَر، مدفوع على `main` |
| نشر HousingService بنقطة `/dashboard` و`/pay` و`/settings` | ⏳ يلزم إعادة نشر (النشر السابق كان قبل إضافة `/dashboard`) |
| نشر FeedbackService بنقطة `/feedbacks/dashboard` | ⏳ يلزم نشر |
| نشر Gateway بتجميعة `/api/dashboard` | ⏳ يلزم نشر **بعد** الخدمتين أعلاه |
| ضبط `housingFeeAmount` (`PUT /api/housing-requests/settings`) | ⏳ يلزم — الدفع يرجع `409` طالما = 0 |

> حتى يكتمل النشر: `GET /api/dashboard` عبر البوابة قد يرجع `404` (تمرير من خدمة لم تُنشر بعد). ابنِ الواجهة على العقد أدناه الآن، والربط الحيّ يعمل فور اكتمال النشر.

---

## 1. `GET /api/dashboard` — لوحة النظرة العامة (مُجمَّعة)

- **الدور:** `admin` / `super_admin` فقط.
- **الطريقة:** `GET` فقط، بلا معاملات.
- **السلوك:** البوابة تنادي خدمتَي السكن والشكاوى بالتوازي وتدمج ردَّيهما في **كائن مسطّح واحد**. إذا فشلت إحداهما (رمز ناقص/منتهٍ) يُمرَّر ردّها كما هو (`401`/`4xx`) — تعامل مع ذلك كـ «تعذّر تحميل اللوحة».

### مثال الرد (`200`)

```json
{
  "pendingRequests": 37,
  "occupancyRate": 74.3,
  "occupiedBeds": 900,
  "totalBeds": 21120,
  "totalHousedStudents": 890,
  "rooms": { "available": 5100, "occupied": 170, "outOfService": 10, "total": 5280 },
  "recentRequests": [
    {
      "id": 482,
      "studentId": "6a74f8579ae122df4c9c063d",
      "studentName": "خالد سعيد المطيري",
      "governorateId": 1,
      "academicLevel": 1,
      "isGroup": false,
      "status": 2,
      "admissionStatus": 0,
      "submittedAt": "2026-08-31T12:00:00Z"
    }
  ],
  "weeklyOccupancy": [
    { "date": "2026-08-25", "occupiedBeds": 870 },
    { "date": "2026-08-26", "occupiedBeds": 872 },
    { "date": "2026-08-27", "occupiedBeds": 880 },
    { "date": "2026-08-28", "occupiedBeds": 885 },
    { "date": "2026-08-29", "occupiedBeds": 890 },
    { "date": "2026-08-30", "occupiedBeds": 895 },
    { "date": "2026-08-31", "occupiedBeds": 900 }
  ],

  "openComplaints": 14,
  "unreadCount": 20,
  "totalComplaints": 120,
  "totalSuggestions": 60,
  "recentFeedback": [
    {
      "id": 88,
      "type": 1,
      "title": "مشكلة في التكييف — A-204",
      "studentName": "أحمد محمود",
      "isAnonymous": false,
      "isRead": false,
      "isReplied": false,
      "createdAt": "2026-08-28T09:15:00Z"
    }
  ]
}
```

### مرجع الحقول

**من السكن (HousingService):**

| الحقل | النوع | المعنى / الاستخدام في الشاشة |
|---|---|---|
| `pendingRequests` | int | «طلبات سكن معلّقة» — طلبات بلا قرار قبول بعد |
| `occupancyRate` | number (خانة عشرية واحدة) | «نسبة الإشغال» — % = `occupiedBeds / totalBeds * 100` |
| `occupiedBeds` / `totalBeds` | int | أسرّة مشغولة / إجمالي أسرّة الغرف غير المعطّلة |
| `totalHousedStudents` | int | «إجمالي الطلاب المسكّنين» (مميَّزون) |
| `rooms.available` | int | «شاغرة» (غرف حالتها Available) |
| `rooms.occupied` | int | «مشغولة» (Occupied + Full) |
| `rooms.outOfService` | int | صيانة + مغلق |
| `rooms.total` | int | مجموع الغرف |
| `recentRequests[]` | array (حتى 6) | جدول «طلبات السكن الجديدة» — الأحدث أولًا |
| `recentRequests[].studentName` | string \| null | `null` لو تعذّر جلب الاسم من AuthService |
| `recentRequests[].isGroup` | bool | `true` = «مشتركة»، `false` = «فردية» |
| `recentRequests[].academicLevel` | int (1..5) | السنة الدراسية — انظر §4 |
| `recentRequests[].status` | int (0..2) | حالة الطلب — انظر §4 |
| `recentRequests[].admissionStatus` | int (0..3) | «الحالة» في الجدول (قيد المراجعة/مقبول/...) — انظر §4 |
| `weeklyOccupancy[]` | array (7 عناصر) | «إشغال المباني خلال الأسبوع» — الأقدم أولًا؛ `date` بصيغة `YYYY-MM-DD` |

**من الشكاوى (FeedbackService):**

| الحقل | النوع | المعنى / الاستخدام في الشاشة |
|---|---|---|
| `openComplaints` | int | «شكاوى مفتوحة» — شكاوى (`type=1`) بلا رد |
| `unreadCount` | int | ملاحظات لم يفتحها موظّف بعد |
| `totalComplaints` / `totalSuggestions` | int | إجماليات (اختياري العرض) |
| `recentFeedback[]` | array (حتى 5) | «أحدث الشكاوى» — الأحدث أولًا |
| `recentFeedback[].type` | int (0/1) | `0` مقترح، `1` شكوى — انظر §4 |
| `recentFeedback[].studentName` | string \| null | `null` للمجهولة أو لو تعذّر الجلب |
| `recentFeedback[].isReplied` | bool | هل ردّت الإدارة |

---

## 2. `GET /api/housing-requests/dashboard` — نصف السكن فقط

نفس مفاتيح السكن أعلاه بالضبط (بلا مفاتيح الشكاوى). الدور `admin`/`super_admin`. استعمله لو أردت تحديث قسم السكن وحده دون الشكاوى.

## 3. `GET /api/feedbacks/dashboard` — نصف الشكاوى فقط

نفس مفاتيح الشكاوى أعلاه بالضبط. الدور `admin`/`super_admin` (`403` لغيره).

---

## 4. جداول تحويل التعدادات (Enums) — كلها أرقام

**`academicLevel`** (السنة الدراسية): `1` الأولى · `2` الثانية · `3` الثالثة · `4` الرابعة · `5` الخامسة

**`status`** (حالة طلب التسكين): `0` مُقدَّم (قيد المراجعة) · `1` يحتاج تعديل · `2` مقفول (صدر قرار)

**`admissionStatus`** (نتيجة القبول): `0` قيد المراجعة (لا قرار بعد) · `1` مقبول · `2` قائمة انتظار · `3` مرفوض

**`type`** (نوع الملاحظة): `0` مقترح (Suggestion) · `1` شكوى (Complaint)

> نفس التعداد `FeedbackType` مُستخدَم حرفيًا في **كل** نقاط الشكاوى: `GET /api/feedbacks` (القائمة)، `POST /api/feedbacks` (الإنشاء)، و`GET /api/feedbacks/dashboard`. لا يوجد ترقيم مختلف لكل نقطة — إذا كانت خريطتك الحالية لأي نقطة تعطي `1 = مقترح` فهي **مقلوبة** وتحتاج تصحيحًا (والأهم: نموذج الإنشاء سيُخزّن الشكاوى كمقترحات والعكس).

---

## 5. `POST /api/housing-requests/{id}/pay` — دفع رسوم السكن

- **الدور:** `student` فقط، ولطلبه هو. **يُنادى عبر عميل خدمة السكن** (`ApiClient.housing`) لا عميل المصادقة — خدمة السكن هي التي تُنادي محفظة AuthService داخليًا.
- **لا جسم للطلب.** المبلغ يُقرأ من `housingFeeAmount` في إعدادات النظام.
- **آمن للتكرار:** إعادة/تزامن نفس الطلب لا يخصم مرّتين.

### شكل الرد الدقيق (مؤكَّد من كود الـ endpoint)

| الحالة | الكود | الجسم |
|---|---|---|
| نجاح | `200` | `{ "message": "تم الدفع بنجاح.", "balance": 75.0 }` |
| رصيد غير كافٍ | `402` | `{ "message": "رصيدك لا يكفي لدفع رسوم السكن." }` |
| الطلب غير مقبول بعد | `400` | `{ "message": "لا يمكن الدفع إلا بعد قبول طلب التسكين." }` |
| مدفوع مسبقًا | `409` | `{ "message": "تم دفع رسوم هذا الطلب مسبقاً." }` |
| الرسم غير مُهيّأ (`housingFeeAmount = 0`) | `409` | `{ "message": "لم يتم تحديد رسم السكن بعد. حاول لاحقاً." }` |
| ليس مالك الطلب | `403` | `{ "message": "لا يمكنك دفع رسوم طلب لا يخصّك." }` |
| الطلب غير موجود | `404` | *(بلا جسم — `NotFound` قياسي)* |
| تعذّر بوابة الدفع (AuthService غير متاح/‏5xx) | `502` | `{ "message": "تعذّر إتمام الدفع حالياً. حاول لاحقاً." }` |

**انتبه — مواضع خطأ شائعة:**
- **`balance` في جسم النجاح هو حقل جذري** (`{ "message", "balance" }`) — **ليس** `data.balance`. وقد يكون `null` لو لم يُرجِع AuthService الرصيد؛ في هذه الحالة أعِد جلب المحفظة من شاشة المحفظة بدل قراءته من هنا.
- **`402` حصريًا للرصيد غير الكافي.** «غير مقبول بعد» = `400` لا `402`.
- **`409` له معنيان** (مدفوع مسبقًا / الرسم = صفر). ميّزهما بإعادة جلب الطلب وفحص `isPaid` (انظر §7): `isPaid = true` ⇒ «مدفوع»؛ غير ذلك ⇒ «الدفع غير متاح حاليًا».

### الوصفة الموصى بها لدورة حياة الزر

1. اقرأ الطلب من `GET /api/housing-requests/mine/{id}`.
2. أظهر «ادفع رسوم السكن» **فقط** عندما `decision.status == 1` (مقبول) **و** `isPaid == false`.
3. عند الضغط → `POST .../pay`:
   - `200` ⇒ رسالة نجاح + حدّث رصيد المحفظة (من `balance` أو أعِد جلبه) + أعِد جلب الطلب (سيرجع `isPaid = true` فيختفي الزر دائمًا).
   - `402` ⇒ «رصيدك لا يكفي».
   - `400` ⇒ لا يُفترض حدوثه لو بوّبت الزر صح؛ اعرض الرسالة وأعِد جلب الطلب.
   - `409` ⇒ أعِد جلب الطلب: `isPaid` صار `true` ⇒ «مدفوع»؛ وإلا «الدفع غير متاح حاليًا».
   - `403`/`404`/`502` ⇒ خطأ عام + أعِد جلب الطلب.

الدفع بعد انقضاء المهلة ما زال مسموحًا (المهلة تُحرّك التذكير فقط).

---

## 6. `GET` / `PUT /api/housing-requests/settings` — إعدادات الدفع (إدارة)

- **الدور:** `admin` / `super_admin`.

**`GET`** → `200`:
```json
{ "paymentDeadlineDays": 15, "reminderDaysBefore": 3, "housingFeeAmount": 25.00, "updatedAt": "2026-08-31T10:00:00Z" }
```

**`PUT`** — الجسم يستبدل الحقول الثلاثة دفعةً واحدة:
```json
{ "paymentDeadlineDays": 15, "reminderDaysBefore": 3, "housingFeeAmount": 25 }
```
- تحقّق: `paymentDeadlineDays > 0`، `0 < reminderDaysBefore < paymentDeadlineDays`، `housingFeeAmount >= 0` وبحدّ أقصى خانتان عشريتان.
- خرق أي شرط ⇒ `400` مع رسالة نصّية.
- نجاح ⇒ `200` بالكائن المحدَّث.

---

## 7. حقول جديدة على `HousingRequestDto` (موجودة ومنشورة الآن)

تظهر في **كل** ردود الطلب: `GET /api/housing-requests`, `/mine`, `/mine/{id}`, `/{id}`:

| الحقل | النوع | المعنى |
|---|---|---|
| `paymentDueDate` | string (date-time) \| null | مهلة الدفع — تُضبط لحظة أول قبول؛ `null` قبل القبول |
| `isPaid` | bool | **مصدر الحقيقة الدائم لحالة الدفع** — لا حاجة لتتبّع محلي مؤقّت |
| `paidAt` | string (date-time) \| null | لحظة الدفع الناجح |

- `isPaid` **دائم ومخزَّن في قاعدة البيانات** — استخدمه لإظهار/إخفاء الزر وحالة «مدفوع»؛ لا تعتمد على تتبّع في الذاكرة يختفي بإغلاق الشاشة.
- عدّاد المهلة من `paymentDueDate`.

---

## 8. ملاحظات

- العقد الكامل (OpenAPI) محدَّث في `contracts/housing-service.openapi.json` و`contracts/feedback-service.openapi.json`.
- لا يوجد converter لتحويل التعدادات إلى نصوص — كلها أرقام كما في §4 (متّسق مع باقي واجهات النظام).
- التواريخ كلها UTC بصيغة ISO‑8601 (`weeklyOccupancy[].date` استثناء: تاريخ فقط `YYYY-MM-DD`).
