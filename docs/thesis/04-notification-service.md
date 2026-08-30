# خدمة الإشعارات — NotificationService

> المكوّن المركزي للإشعارات في المنصة. تؤلّف الرسالة، تحلّ قائمة المستلمين، تدفعها إلى تطبيق الجوال عبر Firebase Cloud Messaging، وتحفظ نسخة في «صندوق الوارد» داخل التطبيق لكل مستلم. تستدعيها بقية الخدمات عبر واجهة `INotificationPublisher` المشتركة.

---

## 1. نظرة عامة

### 1.1 الغرض

توحيد كل مسارات الإشعار في النظام في خدمة واحدة: إشعارات موجّهة لمستخدم واحد أو عدّة مستخدمين أو دور كامل أو بثّ عام، مع تسليم مزدوج — دفع فوري (Push) + سجل دائم في صندوق الوارد يُقرأ ويُوسَم «مقروء».

### 1.2 الموقع في المعمارية

| الخاصية | القيمة |
|---|---|
| التقنية | ASP.NET Core (.NET 9)، EF Core، SQL Server، Firebase Admin SDK |
| المنفذ المحلي | `5081` (خلف البوابة `5067`) |
| قاعدة البيانات | `NotificationServiceDb` (مستقلة) |
| المسار الجذري | `/api/notifications` |
| الاعتماديات الخارجية | AuthService (بحث المستخدمين + جلب رموز FCM، مفتاح داخلي)، Firebase Cloud Messaging (الدفع) |

### 1.3 مبدأ أساسي

**رموز أجهزة FCM لا تُخزَّن هنا.** AuthService هو المصدر الوحيد لرموز الأجهزة؛ يرسل تطبيق الجوال رمزه إلى AuthService عند تسجيل الدخول. NotificationService تستعلم عن الرموز من AuthService عند كل إرسال — «مكان واحد أقلّ يمكن أن يتقادم فيه الرمز».

---

## 2. النموذج البرمجي وعلاقات الجداول

### 2.1 الجداول

| الجدول | الوصف | المفتاح |
|---|---|---|
| `Notifications` | الرسالة المؤلَّفة: عنوان، نص، حمولة `data` اختيارية، الهدف، منشئها، وقت الإرسال | `Id` (int، تسلسلي) |
| `NotificationRecipients` | سطر تسليم وقراءة لكل مستلم مستهدَف: هل سُلّم، هل قُرئ، وقت القراءة | `Id` (int، تسلسلي) |

**لا يوجد جدول رموز أجهزة محلي.**

### 2.2 الحقول

**Notification:**
| الحقل | النوع | ملاحظات |
|---|---|---|
| `Title` | `nvarchar(200)` | إلزامي |
| `Body` | `nvarchar(2000)` | إلزامي |
| `Data` | `nvarchar(2000)?` | حمولة JSON حرّة (deep‑link)، معتمَة على الخادم |
| `TargetType` | enum `NotificationTargetType` | `User = 0`، `Users = 1`، `Role = 2`، `Broadcast = 3` (يُخزَّن كـ int) |
| `TargetRole` | `nvarchar(50)?` | إلزامي حين `TargetType = Role` |
| `CreatedBy` | `nvarchar(64)` | معرّف الإداري من `sub`، أو اسم الخدمة المصدر عند النداء الداخلي |
| `CreatedAt` | `datetime2` | افتراضي `GETUTCDATE()`؛ يُجبَر `Kind=Utc` عند القراءة |
| `SentAt` | `datetime2?` | يُختم بعد حلّ المستلمين والدفع |

**NotificationRecipient:** `NotificationId` (FK)، `StudentId` (`nvarchar(64)`)، `IsRead` (`bit`)، `ReadAt` (`datetime2?`)، `DeliveredSuccessfully` (`bit` — هل نجح دفع FCM لهذا المستلم).

### 2.3 العلاقات

```
Notification 1───∞ NotificationRecipient    (FK = NotificationId، OnDelete = Cascade)
                                             فهرس فريد: (NotificationId, StudentId)
```

- إشعار واحد ⇐ سطر مستلم واحد لكل طالب مستهدَف (فريد بالزوج).
- حذف الإشعار يشلّل صفوف مستلميه.
- `StudentId` و`CreatedBy` مراجع منطقية لمستخدمي AuthService.

---

## 3. قواعد العمل (Business Rules)

| # | القاعدة |
|---|---|
| BR‑N1 | كل نقاط النهاية تتطلّب JWT، عدا `POST /api/notifications/broadcast` المحميّة بمفتاح خدمة (`X-Service-Key`). |
| BR‑N2 | تأليف/إدارة الإشعارات (`GET`/`POST`/`PUT`/`DELETE /api/notifications`, `GET /{id}`) **للإداري فقط** — أداة بثّ لا ينشرها الطالب. |
| BR‑N3 | **أي دور** يقرأ صندوق وارده (`GET /api/notifications/mine`، مُصفَّح) ويوسم عنصرًا مقروءًا (`POST /api/notifications/{id}/read`)؛ المعرّف دائمًا من الرمز. |
| BR‑N4 | لا توجد نقطة تسجيل رمز جهاز على هذه الخدمة — تطبيق الجوال يرسل رمزه إلى AuthService. |
| BR‑N5 | تحقّق الهدف: `User` يتطلّب معرّفًا واحدًا بالضبط؛ `Users` معرّفًا واحدًا على الأقل؛ `Role` يتطلّب `TargetRole`؛ `Broadcast` بلا شرط. |
| BR‑N6 | **حلّ المستلمين:** لـ `User`/`Users` القائمة هي المعرّفات المُمرَّرة (يحصل كلٌّ منها على سطر صندوق وارد بغضّ النظر عن وجود رمز جهاز)؛ رموز الدفع تأتي من `POST /api/internal/users/lookup`. لـ `Role`/`Broadcast` تأتي القائمة والرموز معًا من `GET /api/internal/users/fcm-tokens` (المستخدمون النشطون الذين لديهم رمز فقط). |
| BR‑N7 | **احترام تفضيل الإشعارات:** مستخدم أطفأ الدفع (`NotificationsEnabled == false`) يُسقَط من خريطة رموز الدفع فقط — يبقى سطر صندوق وارده (تمامًا كفشل دفع لجهاز ميت). |
| BR‑N8 | فشل دفع FCM (استثناء) لا يمنع وجود الإشعار في صناديق الوارد — `deliveredTokens` تبقى فارغة والسجلات تُنشأ. |
| BR‑N9 | `DeliveredSuccessfully` لكل مستلم يُضبط حسب نجاح رمزه في استجابة FCM المجمّعة. |
| BR‑N10 | الدفع يُرسَل بدفعات حتى 500 رمزًا (`SendEachForMulticastAsync`). |
| BR‑N11 | تعديل الإشعار (`PUT`) يعدّل `Title`/`Body`/`Data` فقط، لا الهدف ولا المستلمين. |
| BR‑N12 | بيانات اعتماد Firebase تُحلّ كسلاً عند أول إرسال — تُقلِع الخدمة قبل توفيرها (الإشعارات تُنشأ وتظهر داخل التطبيق، والدفع يصل صفر مستلمين حتى الضبط). |
| BR‑N13 | فشل نداء AuthService لا يرمي استثناء — يعني مستلمين أقلّ/لا مستلمين للدفع فقط. |
| BR‑N14 | `RecipientCount` و`DeliveredCount` محسوبان في DTO الإشعار للإحصاء الإداري. |

### حمولة `data` الاصطلاحية

بقيّة الخدمات ترسل `data` بصيغة `{ "type": "...", "relatedId": <id> }` لتوجيه التطبيق. أمثلة الأنواع: `group_join_request`، `group_join_accepted`، `group_member_left`، `group_leadership_transferred`، `housing_payment_reminder`، `housing_payment_completed`. FCM يغلّفها تحت مفتاح `payload`.

---

## 4. حالات الاستخدام (Use Cases)

### UC‑01 إرسال إشعار موجّه (إداري)
- **الفاعل:** الإداري.
- **المسار:** `POST /api/notifications` بـ `{Title, Body, Data?, TargetType, TargetStudentIds?, TargetRole?}` ← تحقّق الهدف ← حفظ `Notification` ← حلّ المستلمين والرموز ← دفع FCM (بدفعات) ← إنشاء سطر `NotificationRecipient` لكل مستلم مع `DeliveredSuccessfully` ← ختم `SentAt`.
- **ما بعد:** إشعار مُرسَل؛ صناديق وارد محدَّثة؛ دفعات وصلت للأجهزة الفعّالة.

### UC‑02 بثّ عام / لدور
- **الفاعل:** الإداري.
- **المسار:** كـ UC‑01 لكن `TargetType = Role` (مع `TargetRole`) أو `Broadcast` ← المستلمون والرموز من `GET /api/internal/users/fcm-tokens` (النشطون ذوو الرمز فقط).

### UC‑03 إشعار من خدمة أخرى (داخلي)
- **الفاعل:** خدمة خلفية (HousingService، FeedbackService، AdvertisingService) عبر `INotificationPublisher`.
- **المسار:** الخدمة تستدعي `NotifyUserAsync` / `NotifyUsersAsync` / `NotifyRoleAsync` / `BroadcastAsync` ← `HttpNotificationPublisher` يرسل `POST /api/notifications/broadcast` بترويسة `X-Service-Key` ← نفس مسار UC‑01 داخليًا مع `CreatedBy` = اسم الخدمة المصدر.
- **ضمان:** الاستدعاء آمن بلا `try/catch` — أي فشل يُسجَّل ويُبتلع.

### UC‑04 قراءة صندوق الوارد
- **الفاعل:** أي مستخدم مصادَق.
- **المسار:** `GET /api/notifications/mine?pageNumber=&pageSize=` ← عناصر مرتّبة بـ `Notification.CreatedAt` تنازليًا، مع `IsRead` و`ReadAt`.

### UC‑05 وسم إشعار مقروءًا
- **الفاعل:** أي مستخدم مصادَق.
- **المسار:** `POST /api/notifications/{id}/read` ← إيجاد سطر المستلم بـ `(NotificationId, sub)` ← ضبط `IsRead = true` و`ReadAt` (إن لم يكن مقروءًا).

### UC‑06 إدارة الإشعارات (إداري)
- `GET /api/notifications` (مُصفَّح، مع `RecipientCount`/`DeliveredCount`) — `GET /api/notifications/{id}` — `PUT /api/notifications/{id}` (عنوان/نص/حمولة) — `DELETE /api/notifications/{id}` (تشليل المستلمين).

---

## 5. مرجع نقاط النهاية (API)

| الطريقة والمسار | الدور | الوظيفة |
|---|---|---|
| `GET /api/notifications` | A | قائمة الإشعارات المؤلَّفة، مُصفَّحة + إحصاء |
| `GET /api/notifications/{id}` | A | تفصيل إشعار |
| `POST /api/notifications` | A | تأليف وإرسال إشعار |
| `PUT /api/notifications/{id}` | A | تعديل نصّ الإشعار |
| `DELETE /api/notifications/{id}` | A | حذف إشعار (+ مستلميه) |
| `GET /api/notifications/mine` | Any | صندوق وارد المستدعي، مُصفَّح |
| `POST /api/notifications/{id}/read` | Any | وسم عنصر مقروءًا |
| `POST /api/notifications/broadcast` | خدمة (X‑Service‑Key) | نقطة الدخول الداخلية لـ `INotificationPublisher` |

المعاملات: `pageNumber` (افتراضي 1)، `pageSize` (افتراضي 10، بين 1 و50). شكل الاستجابة المُصفَّحة موحّد مع بقيّة الخدمات.

---

## 6. التكامل

### 6.1 من هذه الخدمة إلى الخارج

| الوجهة | النداء | الغرض |
|---|---|---|
| **AuthService** | `POST /api/internal/users/lookup` (`X-Internal-Api-Key`) | رموز FCM لمستلمي `User`/`Users` |
| **AuthService** | `GET /api/internal/users/fcm-tokens[?role=]` | قائمة المستلمين + رموزهم لـ `Role`/`Broadcast` (النشطون ذوو الرمز) |
| **Firebase Cloud Messaging** | `SendEachForMulticastAsync` (بدفعات 500) | الدفع الفعلي إلى الأجهزة |

- الخدمة **تفشل بسرعة عند الإقلاع** إن غاب `AUTH_SERVICE_BASE_URL` أو `AUTH_SERVICE_INTERNAL_API_KEY`.
- بيانات Firebase (`FIREBASE_SERVICE_ACCOUNT_JSON`) تُحلّ كسلاً.

### 6.2 من الخدمات الأخرى إلى هذه الخدمة

عبر `SharedKernel.Notifications.INotificationPublisher` ⇐ `POST /api/notifications/broadcast` بترويسة `X-Service-Key` تُقارَن بـ `INTERNAL_SERVICE_KEY` (نفس القيمة على كل خدمة مستدعِية وعلى هذه الخدمة).

---

## 7. الأمان والصلاحيات

- تحقّق JWT محلي (RS256) على مستوى الـ Controller (`[Authorize]`).
- إدارة الإشعارات: `[Authorize(Roles = "admin,super_admin")]`.
- صندوق الوارد والوسم: أي مستخدم مصادَق، مقيّد بمعرّفه من الرمز.
- `broadcast`: `[AllowAnonymous]` + `[RequireServiceKey]` — لا رمز مستخدم، سرّ مشترك بمقارنة زمن‑ثابت.
- المسار `/api/internal/*` في AuthService **لا يُوجَّه عبر البوابة** عمدًا (خادم‑لخادم فقط).

---

## 8. قرارات تصميمية (للأطروحة)

| القرار | المبرّر |
|---|---|
| عدم تخزين رموز FCM محليًا | AuthService مصدر حقيقة واحد؛ مكان أقلّ للتقادم؛ استعلام لحظي عند كل إرسال. |
| `NotificationRecipient` منفصل عن `Notification` | فصل «الرسالة» عن «حالة تسليم/قراءة كل مستلم»؛ يتيح صندوق وارد لكل مستخدم وإحصاء تسليم. |
| كتابة سطر صندوق الوارد حتى لو فشل الدفع | التطبيق يعرض الإشعار داخليًا حتى بلا جهاز مسجَّل أو مع دفع فاشل. |
| احترام `NotificationsEnabled` في الدفع فقط لا في صندوق الوارد | المستخدم أطفأ الإزعاج لا سجلّ الإشعارات. |
| حلّ Firebase كسلاً | الخدمة تُقلِع وتخدم قبل توفير بيانات الاعتماد. |
| نقطة `broadcast` بمفتاح خدمة لا JWT | نداء خادم‑لخادم؛ لا يوجد مستخدم في السياق. |
| `INotificationPublisher` لا يرمي استثناء | إشعار الطلاب لا يجوز أن يُعطِّل عملية الطرف المستدعي (إنشاء إعلان/طلب/ردّ). |
| `data` معتمة على الخادم | مرونة للواجهة في تعريف حمولات deep‑link دون تغيير الخدمة. |

---

## 9. القيود والأعمال المستقبلية

- لا إعادة محاولة للدفع الفاشل (لا طابور إعادة إرسال).
- `PUT` لا يعيد الإرسال ولا يحدّث قائمة المستلمين.
- لا تجميع/كتم لفئات الإشعارات على مستوى المستخدم (عدا مبدّل عام `NotificationsEnabled` في AuthService).
- لا جدولة إرسال مستقبلي.
