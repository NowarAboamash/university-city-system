# خدمة الإعلانات — AdvertisingService

> تدير الإعلانات الموجّهة للطلاب داخل تطبيق المدينة الجامعية: لافتات (Banners) تُعرض في الواجهة، وإشعارات (Notifications)، مع استهداف حسب الجنس والكلية والمحافظة ونافذة زمنية وأولوية عرض.

---

## 1. نظرة عامة

### 1.1 الغرض

تمكين الإدارة من نشر محتوى إعلاني/توعوي موجّه للطلاب، يظهر تلقائيًا ضمن نافذته الزمنية ولجمهوره المستهدَف فقط، ويُطفأ تلقائيًا بعد انتهاء صلاحيته.

### 1.2 الموقع في المعمارية

| الخاصية | القيمة |
|---|---|
| التقنية | ASP.NET Core (.NET 9)، EF Core، SQL Server |
| المنفذ المحلي | `5000` (خلف البوابة `5067`) |
| قاعدة البيانات | `AdvertisingServiceDb` (مستقلة) |
| المسارات الجذرية | `/api/ads`، `/api/ad-types`، `/api/target-genders` |
| الاعتماديات الخارجية | NotificationService (إشعار الطلاب بإعلان جديد)، Cloudinary (تخزين صور اللافتات) |
| المهام الخلفية | `ExpiredAdvertisementCleanupService` — كل 3 ساعات: يُطفئ الإعلانات المنتهية ويحذف صورها |

---

## 2. النموذج البرمجي وعلاقات الجداول

### 2.1 الجداول

| الجدول | الوصف | المفتاح |
|---|---|---|
| `Advertisements` | إعلان: عنوان، وصف، صورة، نوع، جنس مستهدَف، أولوية، نافذة زمنية، منشئه | `Id` (GUID) |
| `AdvertisementColleges` | استهداف الإعلان بكلّيات (رابط كثير‑لكثير مع كليات AuthService) | مركّب `(AdvertisementId, CollegeId)` |
| `AdvertisementGovernorates` | استهداف الإعلان بمحافظات | مركّب `(AdvertisementId, GovernorateId)` |

### 2.2 الحقول

**Advertisement:**
| الحقل | النوع | ملاحظات |
|---|---|---|
| `Id` | `uniqueidentifier` | GUID يُولَّد بالتطبيق |
| `Title` | `nvarchar(200)` | إلزامي، مفهرَس ضمنيًا عبر قيود |
| `Description` | `nvarchar(max)?` | اختياري |
| `ImageUrl` | `nvarchar(max)?` | رابط Cloudinary مطلق؛ إلزامي لنوع `Banner` |
| `Type` | enum `AdType` | `Banner = 1`، `Notification = 2` |
| `TargetGender` | enum `TargetGender` | `Male = 1`، `Female = 2`، `Both = 3` |
| `IsActive` | `bit` | يبدأ `true`؛ يُطفأ يدويًا أو تلقائيًا عند الانتهاء |
| `Priority` | `int` | ترتيب العرض تنازليًا |
| `StartDate` / `EndDate` | `datetime2` | نافذة الظهور؛ مفهرَسان |
| `CreatedBy` | `nvarchar(64)` | يُشتقّ من مطالبة `sub` (لا من ترويسة يرسلها العميل) |
| `CreatedAt` | `datetime2` | وقت الإنشاء (UTC) |

**AdvertisementCollege / AdvertisementGovernorate:** مجرّد `(AdvertisementId, CollegeId/GovernorateId)` — `CollegeId` و`GovernorateId` معرّفات منطقية من AuthService، بلا مفتاح أجنبي فعلي.

### 2.3 العلاقات

```
Advertisement 1───∞ AdvertisementCollege        (FK = AdvertisementId، OnDelete = Cascade)
Advertisement 1───∞ AdvertisementGovernorate    (FK = AdvertisementId، OnDelete = Cascade)
```

- إعلان واحد ⇐ صفر أو أكثر من قيود الكلية، وصفر أو أكثر من قيود المحافظة.
- **دلالة القوائم الفارغة:** إعلان بلا أي قيد كلية = موجَّه لكل الكليات؛ وكذلك المحافظات. أي وجود قيد واحد يقصر الاستهداف على قيمه.
- حذف الإعلان يشلّل صفوف الاستهداف. التعديل يمسح صفوف الاستهداف ويعيد بناءها من القوائم الجديدة (بعد إزالة التكرار).

---

## 3. قواعد العمل (Business Rules)

| # | القاعدة |
|---|---|
| BR‑AD1 | **القراءات عامّة بلا مصادقة:** `GET /api/ads`، `/api/ads/{id}`، `/api/ads/active`، `/api/ad-types`، `/api/target-genders` — لأن الإعلانات مُوجَّهة لأي متصفّح. |
| BR‑AD2 | **الكتابة للإداري فقط** (`admin` / `super_admin`): إنشاء/تعديل/حذف. لا مفهوم «الطالب يملك إعلانه» — الإعلانات محتوى إداري. |
| BR‑AD3 | `CreatedBy` يُشتقّ من مطالبة `sub`؛ ترويسة `X-User-Id` أُلغيت (كانت تسمح بالانتحال). |
| BR‑AD4 | تحقّق الإنشاء/التعديل: العنوان غير فارغ، و`StartDate <= EndDate`، وصورة إلزامية لنوع `Banner` (أو صورة قائمة عند التعديل). |
| BR‑AD5 | عند التعديل مع صورة جديدة: تُرفع الجديدة ثم تُحذف القديمة من Cloudinary. |
| BR‑AD6 | تحقّق الصورة: غير فارغة، ≤ 5 ميغابايت، الامتدادات `.jpg`/`.jpeg`/`.png`/`.webp`. |
| BR‑AD7 | **منطق «الإعلانات النشطة»** (`GET /api/ads/active`): يُعيد الإعلانات حيث `IsActive` و`StartDate <= now <= EndDate`، و(`TargetGender == Both` أو يطابق المُمرَّر)، و(لا قيد كلية أو `collegeId` المُمرَّر ضمن القيود)، و(لا قيد محافظة أو `governorateId` ضمنها) — مرتّبةً بالأولوية تنازليًا ثم `StartDate` تصاعديًا. |
| BR‑AD8 | `GET /api/ads` (كل الإعلانات) مرتّب بالأولوية تنازليًا ثم `StartDate`. |
| BR‑AD9 | إنشاء إعلان يُطلق **إشعارًا لدور الطلاب** «إعلان جديد» بعنوان الإعلان (بأسلوب أفضل جهد — تعطُّل NotificationService لا يُفشل الإنشاء). |
| BR‑AD10 | **التنظيف التلقائي:** `ExpiredAdvertisementCleanupService` كل 3 ساعات يجلب الإعلانات حيث `EndDate < now && IsActive`، يحذف صورها من Cloudinary، ويضبط `IsActive = false`. |
| BR‑AD11 | `GET /api/ad-types` و`/api/target-genders` يُعيدان قوائم `{Id, Name}` مشتقّة من التعدادات — نقاط مرجعية للواجهة. |

---

## 4. حالات الاستخدام (Use Cases)

### UC‑01 نشر إعلان
- **الفاعل:** الإداري.
- **المسار:** `POST /api/ads` (multipart) بـ `{Title, Description?, Image?, Type, TargetGender, StartDate, EndDate, Priority, CollegeIds[], GovernorateIds[]}` ← تحقّق ← رفع الصورة (إن وُجدت) ← إنشاء الإعلان `IsActive = true` + صفوف الاستهداف ← **إشعار الطلاب** «إعلان جديد».
- **استثناء:** عنوان فارغ / `StartDate > EndDate` / صورة ناقصة للافتة ⇒ `400`.

### UC‑02 عرض الإعلانات النشطة للطالب
- **الفاعل:** الطالب / أي زائر (بلا مصادقة).
- **المسار:** `GET /api/ads/active?targetGender=&collegeId=&governorateId=` ← تطبيق منطق BR‑AD7 ← قائمة مرتّبة.

### UC‑03 تصفّح / تفصيل إعلان
- `GET /api/ads` — كل الإعلانات مرتّبة.
- `GET /api/ads/{id}` — تفصيل إعلان.

### UC‑04 تعديل إعلان
- **الفاعل:** الإداري.
- **المسار:** `PUT /api/ads/{id}` (multipart) ← تحقّق ← (إن صورة جديدة: رفع + حذف القديمة) ← تحديث الحقول ← مسح صفوف الاستهداف وإعادة بنائها.

### UC‑05 حذف إعلان
- **الفاعل:** الإداري.
- **المسار:** `DELETE /api/ads/{id}` ← حذف صورة Cloudinary ← حذف السجل (تشليل صفوف الاستهداف).

### UC‑06 إطفاء تلقائي للإعلانات المنتهية
- **الفاعل:** النظام (`ExpiredAdvertisementCleanupService`).
- **المسار:** كل 3 ساعات ← جلب `EndDate < now && IsActive` ← حذف الصور ← `IsActive = false`.

### UC‑07 جلب القوائم المرجعية
- `GET /api/ad-types` ⇒ `[{Id:1,Name:"Banner"},{Id:2,Name:"Notification"}]`.
- `GET /api/target-genders` ⇒ `[{Id:1,"Male"},{Id:2,"Female"},{Id:3,"Both"}]`.

---

## 5. مرجع نقاط النهاية (API)

| الطريقة والمسار | الدور | الوظيفة |
|---|---|---|
| `POST /api/ads` | A | إنشاء إعلان (+ إشعار الطلاب) |
| `PUT /api/ads/{id}` | A | تعديل إعلان |
| `DELETE /api/ads/{id}` | A | حذف إعلان (+ حذف صورته) |
| `GET /api/ads` | عام | كل الإعلانات مرتّبة |
| `GET /api/ads/{id}` | عام | تفصيل إعلان |
| `GET /api/ads/active` | عام | الإعلانات النشطة المطابقة للاستهداف |
| `GET /api/ad-types` | عام | قائمة أنواع الإعلانات |
| `GET /api/target-genders` | عام | قائمة قيم الجنس المستهدَف |

---

## 6. التكامل مع الخدمات الأخرى

| الوجهة | الآلية | الاستخدام |
|---|---|---|
| **NotificationService** | `INotificationPublisher.NotifyRoleAsync("student", ...)` (لا يرمي استثناء) | إشعار الطلاب عند نشر إعلان جديد؛ حمولة `data` = `{ adId }`. |
| **Cloudinary** | `IImageUploader` عبر `ImageStorageService` | تخزين/حذف صور اللافتات في مجلد `advertisements`؛ روابط `https` مطلقة. |
| **AuthService** | لا نداء مباشر | `CollegeId` و`GovernorateId` معرّفات منطقية فقط، تُمرَّر من الواجهة. |

---

## 7. الأمان والصلاحيات

- القراءات مفتوحة تمامًا (بلا `[Authorize]`).
- الكتابة: `[Authorize(Roles = "admin,super_admin")]` على `POST`/`PUT`/`DELETE`.
- `CreatedBy` من الرمز حصريًا؛ لا ترويسات هوية من العميل.
- روابط الصور روابط Cloudinary عامّة مطلقة.

---

## 8. قرارات تصميمية (للأطروحة)

| القرار | المبرّر |
|---|---|
| القوائم الفارغة تعني «الكل» | تبسيط الاستهداف: إعلان عام لا يحتاج تعداد كل الكليات/المحافظات. |
| `Id` من نوع GUID | لا حاجة لترقيم تسلسلي؛ يسهّل التوليد على مستوى التطبيق ويمنع تخمين المعرّفات. |
| `CreatedBy` نصّي من `sub` | نفس عدم تطابق ObjectId/Guid الذي واجهته FeedbackService؛ حُلّ بعمود `nvarchar(64)`. |
| إلغاء ترويسة `X-User-Id` | كانت بلا تحقّق؛ سمحت بانتحال أي منشئ. |
| إطفاء تلقائي بمهمة خلفية | تجنّب عرض إعلانات منتهية، وتحرير مساحة Cloudinary دون تدخّل إداري. |
| صورة إلزامية للّافتة فقط | اللافتة محتوى بصري بطبيعته؛ إعلان النوع `Notification` قد يكون نصيًا. |
| الإشعار «أفضل جهد» | نشر الإعلان يجب أن ينجح حتى لو كانت NotificationService متوقّفة. |

---

## 9. القيود والأعمال المستقبلية

- لا تتبّع لمرّات ظهور/نقر الإعلان (Impressions/Clicks).
- لا جدولة نشر مستقبلية صريحة عدا `StartDate` (الإعلان يُنشأ `IsActive = true` فورًا لكنه لا يظهر في `active` قبل `StartDate`).
- الاستهداف بالكلية/المحافظة يعتمد على تمرير الواجهة للقيم الصحيحة (لا تحقّق مقابل AuthService).
