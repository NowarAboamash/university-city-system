# المكتبة المشتركة — SharedKernel

> مكتبة صنفيّة (Class Library) لا تُنشر كخدمة، بل يرجع إليها كل مشروع خدمة. تجمّع الشيفرة المتكرّرة عبر الخدمات: التحقق من JWT، حماية النداء بين الخدمات، نشر الإشعارات، بحث المستخدمين، ورفع الصور. الهدف: عقد موحّد وسلوك متّسق (خصوصًا «الفشل الآمن») بلا تكرار.

---

## 1. نظرة عامة

| الخاصية | القيمة |
|---|---|
| النوع | مكتبة .NET 9 (`SharedKernel.csproj`)، بلا نقاط نهاية ولا قاعدة بيانات |
| المستهلكون | HousingService، FeedbackService، AdvertisingService، NotificationService، (والبوابة جزئيًا) |
| الأقسام | `Auth/`، `Notifications/`، `Users/`، `Media/` |
| نمط الإعداد الموحّد | «متغيّر البيئة أولًا، ثم مفتاح `appsettings.json`» في كل مكوّن |

---

## 2. قسم المصادقة — `Auth/`

### 2.1 `AddSharedJwtAuthentication(configuration)`

يُسجَّل في `Program.cs` لكل خدمة. يقرأ المفتاح العام لـ AuthService من `JWT_ACCESS_PUBLIC_KEY` (بيئة) أو `Jwt:AccessPublicKey` (إعداد)، ويُهيّئ التحقق من رمز JWT:

- **RS256**، تحقّق محلي من التوقيع مقابل المفتاح العام — **بلا نداء لـ AuthService لكل طلب**.
- `ValidateIssuerSigningKey = true`، `ValidateLifetime = true`.
- `ValidateIssuer = false`، `ValidateAudience = false` (لا يُستخدمان في هذا النظام).
- `MapInboundClaims = false` — تُحفَظ أسماء المطالبات الخام (`sub`، `role`، `email`) بدل تحويلها إلى URIs الطويلة.
- `RoleClaimType = "role"`، `NameClaimType = "sub"` — كي يعمل `[Authorize(Roles = ...)]` و`User.IsInRole(...)` مباشرةً.
- `NormalizePem` يقبل المفتاح بصيغة PEM كاملة أو Base64 خام (يعيد تغليفه بأسطر 64 حرفًا وترويسة PEM).
- **يفشل بسرعة عند الإقلاع** إن غاب المفتاح.

**شكل حمولة الرمز:** `{ "sub": "<userId>", "role": "student|admin|super_admin", "email": "...", "iat": ..., "exp": ... }` — `sub` هو Mongo ObjectId نصّي.

### 2.2 `ClaimsPrincipalExtensions`

دوال إضافية على `ClaimsPrincipal`:
- `TryGetUserId(out string userId)` — يقرأ `sub` (أو `ClaimTypes.NameIdentifier` احتياطيًا)؛ يعيد `false` إن غاب.
- `GetEmail()` — `email` أو `ClaimTypes.Email`.
- `GetRole()` — `role` أو `ClaimTypes.Role`.

هذه هي الآلية الوحيدة لاشتقاق هوية المستخدم في كل الخدمات — **لا يُقبل معرّف المستخدم من جسم الطلب أو ترويساته أبدًا**.

### 2.3 `RequireServiceKeyAttribute`

سِمة `IAsyncActionFilter` تحمي نقطة نهاية «خادم‑لخادم»:
- تقرأ السرّ المتوقَّع من `INTERNAL_SERVICE_KEY` (بيئة) أو `InternalApi:ServiceKey` (إعداد).
- تقارن ترويسة `X-Service-Key` بالسرّ عبر **مقارنة زمن‑ثابت (`CryptographicOperations.FixedTimeEquals`)** — تحصين ضد هجمات التوقيت.
- سرّ غير مضبوط ⇒ `500`؛ ترويسة مفقودة/خاطئة ⇒ `401`.
- الاستخدام الوحيد حاليًا: `POST /api/notifications/broadcast`.

### 2.4 `SwaggerJwtExtensions.AddJwtBearerSecurity()`

يضيف تعريف أمان `Bearer` إلى Swagger كي يظهر زرّ «Authorize» في واجهة كل خدمة.

---

## 3. قسم الإشعارات — `Notifications/`

### 3.1 `INotificationPublisher`

الواجهة العامّة التي تستدعيها أي خدمة لإرسال إشعار، بدل معرفة عقد HTTP الخاص بـ NotificationService:

| الدالة | الهدف |
|---|---|
| `NotifyUserAsync(studentId, title, body, data?)` | مستخدم واحد |
| `NotifyUsersAsync(studentIds, title, body, data?)` | عدّة مستخدمين |
| `NotifyRoleAsync(role, title, body, data?)` | كل من يحمل دورًا |
| `BroadcastAsync(title, body, data?)` | الجميع |

**ضمان جوهري:** كل دالة آمنة للـ `await` بلا `try/catch` — فشل التسليم يُسجَّل ويُبتلع داخليًا، **لا يُرمى استثناء أبدًا**. فلا يمكن لإشعار الطلاب أن يُعطِّل عملية الطرف المستدعي (إنشاء إعلان، قرار قبول، ردّ على شكوى...).

### 3.2 `HttpNotificationPublisher` (تنفيذ داخلي)

- يرسل `POST api/notifications/broadcast` بحمولة `{ title, body, data, targetType, targetStudentIds, targetRole, sourceService }`.
- `sourceService` = اسم الخدمة المُمرَّر عند التسجيل (يظهر كـ `CreatedBy` في NotificationService).
- استجابة غير ناجحة ⇒ تحذير في السجل؛ استثناء ⇒ تحذير في السجل — لا انتشار.

### 3.3 التسجيل

```csharp
builder.Services.AddSharedNotificationPublisher(builder.Configuration, "HousingService");
```

يحتاج على الخدمة المستدعِية: `NOTIFICATION_SERVICE_BASE_URL` / `NotificationService:BaseUrl` و`INTERNAL_SERVICE_KEY` / `InternalApi:ServiceKey` (يُرسَل كترويسة `X-Service-Key`).

### 3.4 `NotificationTargetType`

نسخة SharedKernel من التعداد (`User=0`، `Users=1`، `Role=2`، `Broadcast=3`) — مطابقة لنسخة NotificationService كي يتطابق التسلسل.

---

## 4. قسم بحث المستخدمين — `Users/`

### 4.1 `IUserLookupService`

```csharp
Task<IReadOnlyDictionary<string, UserInfo>> LookupUsersAsync(IReadOnlyCollection<string> ids, ...);
```

- `UserInfo(Id, FirstName, SecondName, Role, IsDeleted)` مع خاصية محسوبة `FullName = "$FirstName $SecondName"`.
- **لا يرمي استثناء** — بحث فاشل أو جزئي يعني أسماء أقلّ فقط؛ على المستدعي معاملة المفتاح المفقود كـ «اسم غير معروف».

### 4.2 `AuthServiceUserLookupService` (تنفيذ داخلي)

- يستدعي `POST api/internal/users/lookup` في AuthService بترويسة مفتاح داخلي.
- يقسّم القائمة إلى **دفعات حتى 200 معرّفًا** لكل نداء.
- يزيل المعرّفات الفارغة والمكرّرة قبل النداء.
- أي فشل (HTTP غير ناجح، استثناء، حمولة فارغة) ⇒ تحذير في السجل + إعادة ما جُمِع حتى الآن.

### 4.3 التسجيل

```csharp
builder.Services.AddAuthServiceUserLookup(builder.Configuration);
```

يحتاج: `AUTH_SERVICE_BASE_URL` / `AuthService:BaseUrl` و`AUTH_SERVICE_INTERNAL_API_KEY` / `AuthService:InternalApiKey`.

**المستهلكون:** FeedbackService (أسماء في قائمة الشكاوى)، HousingService (أسماء أعضاء الغروب وطالبي الانضمام).

### 4.4 عملاء AuthService الداخليون الآخرون — `IWalletClient` (محلي في HousingService، ليس في SharedKernel)

خصم رسوم السكن من محفظة الطالب في AuthService يتمّ عبر `IWalletClient` / `AuthServiceWalletClient`، وهو **يعيش في HousingService** (`HousingService/External/WalletClient.cs`) لا في SharedKernel — لأن HousingService هي الخدمة الوحيدة التي تشحن المحفظة، فلا داعٍ لترقيته إلى المكتبة المشتركة. لكنه **يتبع نفس نمط SharedKernel بدقّة**:

```csharp
Task<WalletChargeResult> ChargeAsync(string userId, decimal amount, string reference, string description, ...);
// WalletChargeResult { bool Success, bool InsufficientBalance, decimal? NewBalance }
```

- `HttpClient` مُنمَّط يُسجَّل بـ `AddHousingWalletClient(configuration)`، بنمط **«بيئة ثم إعداد»**: يقرأ `AUTH_SERVICE_BASE_URL` / `AuthService:BaseUrl` و`AUTH_SERVICE_INTERNAL_API_KEY` / `AuthService:InternalApiKey` — **نفس مفتاح `X-Internal-Api-Key` ونفس العنوان** المستخدمَين لـ `IUserLookupService`، فلا إعداد جديد.
- ينادي `POST api/internal/wallet/charge` بترويسة `X-Internal-Api-Key`. الردود: `200` ⇒ نجاح + `data.balance`؛ `402` ⇒ رصيد غير كافٍ؛ أي شيء آخر (404 مستخدم غير موجود، 401 مفتاح، 5xx) ⇒ **يُرمى استثناء** يلتقطه المستدعي (الطلب يبقى غير مدفوع).
- **خلاف `IUserLookupService`/`INotificationPublisher`، هذا العميل يرمي استثناءً عند الخطأ عمدًا** — تحريك المال ليس تكاملًا اختياريًا «آمن الفشل»؛ يجب أن يعرف المستدعي بدقّة أن الخصم لم ينجح.
- AuthService **مُحايِد للتكرار (idempotent)** على `(userId, reference)` (المرجع `housing-request-{id}`)، ما يجعل نداءات الدفع المتزامنة آمنة.

**متى يُرقَّى إلى SharedKernel؟** إذا احتاجت خدمة ثانية شحن المحفظة، يُنقَل إلى `SharedKernel/Users/` (أو قسم `Wallet/` جديد) بنفس أسلوب `AddAuthServiceUserLookup`.

---

## 5. قسم الوسائط — `Media/`

### 5.1 `IImageUploader`

```csharp
Task<string> UploadAsync(Stream fileStream, string fileName, string folder, ...);   // يعيد رابط https مطلق
Task<bool>   DeleteAsync(string secureUrl, ...);
```

### 5.2 `CloudinaryImageUploader` (تنفيذ)

- يرفع إلى **Cloudinary** ضمن مجلد مُمرَّر (`housing-documents`، `feedback`، `advertisements`)، مع `UniqueFilename = true` و`Overwrite = false`.
- يعيد `SecureUrl` (رابط `https` مطلق، قابل للجلب مباشرةً بلا توجيه بوابة).
- الحذف يستخرج `publicId` من الرابط (يتعامل مع مقطع الإصدار `v123...`) ثم `DestroyAsync`.
- **حلّ بيانات الاعتماد كسول (`Lazy<Cloudinary>`)** — عند أول رفع/حذف لا عند الإقلاع؛ فتُقلِع الخدمة وتخدم نقاطها غير الصوريّة قبل توفير المفاتيح. مفتاح مفقود ⇒ استثناء واضح عند أول استخدام فعلي.
- الإعداد: `CLOUDINARY_CLOUD_NAME` / `Cloudinary:CloudName` وما يماثلها للـ `ApiKey` و`ApiSecret`.

### 5.3 `AddCloudinaryImageUploader()`

امتداد تسجيل بسيط يربط `IImageUploader` بـ `CloudinaryImageUploader`.

---

## 6. المبادئ التصميمية المتحقّقة (للأطروحة)

| المبدأ | كيف تحقّقه SharedKernel |
|---|---|
| **الفشل الآمن (Fail‑safe)** | `INotificationPublisher` و`IUserLookupService` لا يرميان استثناءً أبدًا — نقاط التكامل الاختيارية لا تُسقط العمليات الأساسية. |
| **مصدر هوية واحد** | `ClaimsPrincipalExtensions` هو الطريق الوحيد لاشتقاق `sub`/`role`؛ لا هوية من العميل. |
| **تحقّق لا‑مركزي بلا نقطة فشل وحيدة** | تحقّق JWT محلي RS256 في كل خدمة، بلا نداء لـ AuthService لكل طلب. |
| **إعداد موحّد** | نمط «بيئة ثم إعداد» في كل مكوّن؛ فشل بسرعة عند غياب إعداد حرِج. |
| **تحصين النداء بين الخدمات** | سرّ مشترك بترويسة + مقارنة زمن‑ثابت (`RequireServiceKeyAttribute`). |
| **حلّ كسول للاعتماديات الخارجية** | Cloudinary (وكذلك Firebase في NotificationService) — الخدمة تُقلِع قبل توفير المفاتيح. |
| **تقليل التكرار (DRY)** | تعريف واحد لـ JWT/الإشعارات/البحث/الرفع بدل نسخة لكل خدمة. |

---

## 7. القيود والملاحظات

- `PagedResult<T>` و`PaginationParams` **ليسا** في SharedKernel — كل خدمة تعرّف نسختها الخاصّة بشكل متطابق (يراها مولّد العملاء غلافًا موحّدًا رغم ذلك).
- تعداد `NotificationTargetType` مكرّر بين SharedKernel وNotificationService (يجب إبقاؤهما متطابقين يدويًا).
- SharedKernel يفترض بنية AuthService الحالية لنقاط `/api/internal/*` وشكل حمولاتها.
- `IWalletClient` (شحن محفظة AuthService) **ليس في SharedKernel** بل محلي في HousingService — مستهلك وحيد؛ يتبع نمط الإعداد نفسه ويشارك مفاتيح `AuthService:*` (انظر §4.4). يُرقَّى عند ظهور مستهلك ثانٍ.
