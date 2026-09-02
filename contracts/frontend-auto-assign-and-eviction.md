# دليل ربط الواجهات: التسكين التلقائي + الطرد لعدم الدفع

**housing-service · تحديث 2026‑09‑02 · الكود على `main`، لم يُنشر بعد.**

ثلاث ميزات جديدة:

| الميزة | السطح | النقطة |
|---|---|---|
| تسكين تلقائي جماعي (bin‑packing) مع معاينة | لوحة الأدمن (ويب) | `POST /api/allocations/auto-assign` |
| سبب الرفض المميّز (مراجعة / عدم دفع) | لوحة الأدمن + تطبيق الطالب (Flutter) | حقل `decision.rejectionReason` على طلب السكن |
| الطرد التلقائي لعدم الدفع | تلقائي بالكامل على السيرفر — لا نقطة تُستدعى | ينعكس عبر حالة الطلب + إشعار |

- Base URL عبر البوابة: إنتاج `http://gateway001.runasp.net` — نفس المسار بلا تغيير.
- المصادقة: `Authorization: Bearer <JWT>`.
- **الـ enums تُسلسَل كأرقام** (نفس اصطلاح الخدمة الحالي): `AdmissionDecisionStatus` = `0 Pending · 1 Accepted · 2 WaitingList · 3 Rejected` ، `RejectionReason` = `0 AdminReview · 1 NonPayment`. الاستثناء: `skipped[].targetType` نصّ صريح (`"individual"` / `"group"`).
- التواريخ UTC ISO‑8601.

### ⚠️ قبل البدء

النسخة المنشورة على `housingservice001.runasp.net` **ما زالت القديمة**. حتى يتم redeploy:

- `POST /api/allocations/auto-assign` → `404` على البوابة المنشورة (موجود محليًا فقط).
- `decision.rejectionReason` → لا يظهر في ردود النسخة المنشورة (العمود موجود بالقاعدة، لكن كود القراءة قديم).
- مهمة الطرد اليومية لا تعمل على المنشور.

**ابنِ الواجهة الآن على هذا العقد، وفعّلها فور تأكيد الـ redeploy.** لقطة الحالة الفعلية على القاعدة المشتركة في **القسم 4**.

---

## 1. التسكين التلقائي — `POST /api/allocations/auto-assign` (أدمن)

**الدور:** `admin` / `super_admin` فقط. الطالب يأخذ `403`.

يوزّع **كل** طلب/غروب حالته `Accepted` وما إله تخصيص فعّال، ضمن **الدورة المفتوحة الحالية**، على الغرف المتاحة، بهدف تسكين أكبر عدد.

### الطلب

```json
{ "dryRun": true }
```

| الحقل | المعنى |
|---|---|
| `dryRun` | `true` → يرجّع الخطة المقترحة بدون أي كتابة. `false` → ينفّذ الخطة. |

**التدفّق الموصى به في الواجهة:**
1. زر «معاينة التوزيع» → استدعاء بـ `dryRun: true` → اعرض الجدول (تخصيصات + مستبعدون).
2. زر «تنفيذ» → نفس النقطة بـ `dryRun: false` → اعرض ملخّص ما تم فعلاً.

> ما في شرط دفع — المقبول يُسكَّن حتى لو ما دفع. عدم الدفع يتكفّل فيه الطرد التلقائي لاحقاً (قسم 3).

### الرد `200` — `AutoAssignResultDto`

```json
{
  "dryRun": true,
  "placedTargets": 128,
  "housedStudents": 372,
  "skippedTargets": 3,
  "assignments": [
    {
      "housingRequestId": null,
      "housingGroupId": 44,
      "size": 4,
      "roomId": 1021,
      "roomNumber": "312",
      "buildingId": 5,
      "buildingName": "5",
      "studentIds": ["…", "…", "…", "…"]
    },
    {
      "housingRequestId": 981,
      "housingGroupId": null,
      "size": 1,
      "roomId": 1021,
      "roomNumber": "312",
      "buildingId": 5,
      "buildingName": "5",
      "studentIds": ["…"]
    }
  ],
  "skipped": [
    { "targetType": "group", "targetId": 51, "size": 3, "reason": "Not all group members have been accepted yet." },
    { "targetType": "individual", "targetId": 990, "size": 1, "reason": "No available room has a free bed." },
    { "targetType": "group", "targetId": 52, "size": 4, "reason": "No available room has 4 free beds together for the group." }
  ]
}
```

| الحقل | المعنى |
|---|---|
| `placedTargets` | عدد الوحدات (فرد + غروب) اللي انتسكّنت. بالمعاينة = المقترح، بالتنفيذ = المكتوب فعلاً. |
| `housedStudents` | عدد الأشخاص (مجموع `size`). |
| `skippedTargets` | عدد الوحدات اللي ما انتسكّنت = طول `skipped`. |
| `assignments[]` | كل عنصر إمّا `housingRequestId` أو `housingGroupId` (واحد فقط ≠ null). `size` = 1 لفرد، عدد الأعضاء لغروب. `studentIds` = المتأثرون. |
| `skipped[]` | `targetType` = `"individual"` / `"group"` ، `targetId` = رقم الطلب أو الغروب ، `reason` نصّ إنجليزي جاهز للعرض أو للتصنيف. |

**أسباب الاستبعاد الممكنة:**
- `"Not all group members have been accepted yet."` — الغروب فيه عضو غير مقبول؛ الأدمن يحسم قراره أولاً.
- `"No available room has a free bed."` — فرد، ما في سرير شاغر يطابق جنسه.
- `"No available room has N free beds together for the group."` — الغروب ما بيلاقي غرفة وحدة تسعه (ما بينقسم).
- `"Rejected at commit: <سبب>"` — يظهر **فقط بعد `dryRun: false`**: التخصيص كان بالخطة بس رُفض وقت الكتابة (أدمن تاني حجز الغرفة بنفس اللحظة مثلاً). هالعنصر **مش** ضمن `assignments`.

### الخوارزمية (للفهم فقط، لا يلزم عرضها)

الغروبات أولاً (الأكبر ثم الأقدم قبولاً) بأضيق غرفة تسعها؛ ثم الأفراد (الأقدم قبولاً) بالغرفة الأكثر امتلاءً اللي فيها سرير — تجميع لإبقاء غرف فاضية نظيفة. الجنس مفروض دايماً؛ الغرف `Maintenance`/`Closed`/`Full` مستثناة. تفضيل «غرفتي القديمة» غير مأخوذ بالحسبان بهالإصدار.

### الأخطاء

| الحالة | HTTP | الجسم |
|---|---|---|
| ما في دورة سكن مفتوحة | `400` | `"No housing cycle is currently open."` |

عند `dryRun: false` كل تخصيص يُنفَّذ عبر نفس مسار `POST /api/allocations` ويُرسل للطالب/الأعضاء إشعار «تم تخصيص سكنك» تلقائياً (سيرفر‑side).

---

## 2. سبب الرفض المميّز — `decision.rejectionReason`

`AdmissionDecision` صار يحمل حقل `rejectionReason` (nullable)، يظهر داخل `decision` على كل `HousingRequestDto`:

```jsonc
"decision": {
  "id": 12,
  "status": 3,                 // Rejected
  "rejectionReason": 1,        // 0 = AdminReview (رفض بالمراجعة) · 1 = NonPayment (طرد لعدم الدفع) · null لأي حالة غير Rejected
  "decisionReason": "لم يتم دفع رسوم السكن خلال المهلة.",
  "decisionDate": "2026-09-17T00:00:00Z",
  "reviewedBy": "system:payment-enforcement"
}
```

- له معنى **فقط** لمّا `status == 3` (Rejected)؛ غير هيك `null`.
- `reviewedBy == "system:payment-enforcement"` يميّز إجراء النظام عن إجراء أدمن بشري.

### لوحة الأدمن (ويب)

- **ما في باراميتر فلترة لـ `rejectionReason` على السيرفر.** الفلاتر المتاحة على `GET /api/housing-requests` تشمل `admissionStatus` فقط. للتمييز: مرّر `?admissionStatus=3` ثم صنّف بالواجهة على `decision.rejectionReason` (`1` = مطرود لعدم الدفع، `0`/`null` = رفض مراجعة).
- في `POST /api/housing-requests/{id}/decision` يمكن تمرير `rejectionReason` اختيارياً؛ حذفه مع `status: 3` يعني `AdminReview` تلقائياً. عملياً الواجهة ما بتحتاج ترسله — النظام هو اللي يضع `NonPayment`.

### تطبيق الطالب (Flutter)

- `GET /api/housing-requests/mine` → لكل طلب، إذا `decision.status == 3`:
  - `decision.rejectionReason == 1` → اعرض: «تم إلغاء تخصيصك لعدم دفع الرسوم خلال المهلة. يمكنك التقديم من جديد في دورة تسكين قادمة.» واعتبر الطلب منتهياً لهالدورة (لا زر دفع، لا زر تعديل).
  - غير هيك → نصّ الرفض العادي، مع `decisionReason` إن وُجد.
- `GET /api/allocations/mine` يرجّع `404` بعد الطرد (ما عاد إله غرفة).
- `POST /api/housing-requests/{id}/pay` بعد الطرد يرجّع `400` («لا يمكن الدفع إلا بعد قبول طلب التسكين») — أخفِ زر الدفع.

---

## 3. الطرد التلقائي لعدم الدفع (خلفية — لا استدعاء)

مهمة يومية على السيرفر: أي طلب `Accepted` + غير مدفوع + عدّى يوم `paymentDueDate` بالكامل (**بدون مهلة سماح**) → يُقلب `Rejected` / `rejectionReason = NonPayment`، تُحرَّر غرفته، ويُشال من أي غروب (رفقاؤه يبقون)، ويصله إشعار.

**انعكاسه على الواجهات:** لا شي يُستدعى. حدّث الحالة عند استقبال الإشعار أو عند إعادة جلب الطلب.

| الإشعار | العنوان | النصّ |
|---|---|---|
| طرد لعدم الدفع | `تم إلغاء تخصيص السكن` | «لم يتم دفع رسوم السكن خلال المهلة المحددة، وتم إلغاء طلب التسكين وأي غرفة كانت مخصصة لك…» |

> إشعار الطرد يعيد استخدام إشعار «تغيّر القرار» ولا يحمل `data.type` مخصّصاً — اعتمد على إعادة جلب `GET /api/housing-requests/mine` لتحديد السبب (`rejectionReason`).

نقاط الدفع/المهلة الكاملة موثّقة في `HousingService/PAYMENT_API.md`.

---

## 4. حالة قاعدة البيانات المشتركة (`db64974`) — لقطة 2026‑09‑02

تشغيل فعلي أول لـ `auto-assign` تمّ على القاعدة المشتركة (`dryRun:false`). ما تحتاجه الواجهة معرفته:

- **الدورة المفتوحة:** `id = 2`، الاسم `"2027-2028"`.
- **تمّ تسكين 4 طلاب** (3 غروبات: 21، 22، 24) — كلهم في **غرفة `roomId = 2641`، رقم `"101"`، مبنى `11`** (ذكور)، والغرفة الآن `Full` (4/4).
  - غروب 24 → تخصيص `id 17` · غروب 22 → `id 18` · غروب 21 → `id 19`.
  - حالة الغروبات الثلاثة الآن `Allocated` (`status = 2`).
- كل تخصيصات الغرف القديمة قبل هذا التاريخ كانت **مُخلاة** (تجارب سابقة) — لا تعرضها كتسكين حالي؛ اعتمد `vacatedAt == null` فقط.
- **إشعار واحد من إشعارات «تم تخصيص سكنك» فشل** (NotificationService رجّع `500`). التخصيص نجح رغم ذلك؛ إن بنيت شاشة إشعارات، قد لا يظهر هذا الإشعار لبعض الطلاب الأربعة — الحقيقة الموثوقة هي `GET /api/allocations/mine`.

> تذكير: هذه النقاط غير منشورة بعد على `housingservice001.runasp.net` — راجع «⚠️ قبل البدء» في أعلى الملف.
